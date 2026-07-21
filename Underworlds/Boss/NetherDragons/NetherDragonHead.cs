using AncientChineseMythology.Underworlds.Boss.NetherDragons.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙头部 — V3《冥界之门的看门龙》。
    ///
    /// 设计主轴: <b>门即舞台</b> — 一切强力行为经由冥界之门 (NetherPortal / NetherDragonGate 着色器),
    /// 门的开裂本身就是 telegraph 三拍: 裂缝发光(紫) → 裂纹分叉转红 → 龙头轰出。
    ///
    /// 结构 (手写循环表, 确定性推进; 随机决策只在服务器 + netUpdate 同步):
    ///   ● P0《开门》入场: 裂缝 96f 生长 → 破门俯冲 → 凝视静止。
    ///   ● P1《巡墓》(&gt;60%): 蛇形游弋 + 吐息锥(锥形 shader 预警) + 掘墓冲刺(后撤蓄势→46px/f 爆发→硬刹鞭波)。
    ///   ● P1→P2《裂土》: 被门吞没 → 60f 全场静默 → 三符阵门两假一真 → 真门转红轰出。
    ///   ● P2 (60–30%): 门梭三连(teleport-loop 戳刺, 红色戳刺线) + 魂束扫射(扇形预警+恒速) + 双段吐息。
    ///   ● P2→P3《噬墓》: 仰天怒吼蜕鳞演出, 逆鳞被动开启。
    ///   ● P3 (≤30%): 受击蜕逆鳞(清账反制; 超时→暴怒: 只提速不减前摇) + 双向剪刀扫射 + 门梭四连 + 二连冲刺。
    ///   ● ≤15% 一次《万魂门》: 绕场 2~4 门(怨念账定数)依次切向扫射, 龙从最后一门轰出。
    ///   ● 死亡《葬门》: 尾→头逐节爆燃链(音高上行) → 巨门吸入收葬 → 砰然合拢 (全场唯一 shake 16)。
    /// </summary>
    [AutoloadBossHead]
    public class NetherDragonHead : NetherDragon
    {
        public override WormType NPCWormType => WormType.Head;

        // ============================================================
        //  状态机 (ai[0]=状态 ai[1]=计时 ai[2]=子状态/循环 ai[3]=循环表索引)
        // ============================================================

        private const int StIntro = 0;
        private const int StWeave = 1;
        private const int StBreath = 2;
        private const int StDash = 3;
        private const int StShuttle = 4;
        private const int StSweep = 5;
        private const int StPhaseRift = 6;
        private const int StShed = 7;
        private const int StMyriad = 8;
        private const int StDeath = 9;

        private int State {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }
        private ref float StateTimer => ref NPC.ai[1];
        private ref float SubState => ref NPC.ai[2];
        private ref float RotIndex => ref NPC.ai[3];

        // 手写循环表 (PACING §2: 攻击序列本身就是编排 — 换阶段后首项必为温和的游弋)
        private static readonly int[] CycleP1 = { StWeave, StBreath, StWeave, StDash, StWeave, StBreath, StDash };
        private static readonly int[] CycleP2 = { StWeave, StSweep, StShuttle, StDash, StWeave, StBreath, StShuttle };
        private static readonly int[] CycleP3 = { StWeave, StShuttle, StSweep, StDash, StBreath };

        // —— 节拍常量 ——
        private const int IntroCrack = 96;       // 入场门裂纹时长
        private const int IntroBurst = 105;      // 破门帧
        private const int IntroDur = 205;
        private const int BreathTell = 55;       // 吐息锥预警
        private const int DashReel = 36;         // 冲刺后撤蓄势 (beep 固定前置 36f)
        private const int DashLaunch = 66;       // 冲刺发射帧 (30 对齐 + 36 蓄势)
        private const int SweepTell = 75;        // 扫射扇形预警 (处决级 §6.1)
        private const int SweepDur = 90;         // 扫射时长 (≈80° 恒速)
        private const int DeathBodyStart = 40;   // 死亡逐节爆燃起始帧
        private const int DeathGateAt = 190;     // 葬门裂开帧
        private const int DeathEnd = 268;

        // —— 同步字段 (SendExtraAI) ——
        private Vector2 anchor;          // 门锚点 / 吞没点
        private float aimAngle;          // 出击方向 (rad)
        private int enrageTimer;
        private int scaleAccum;          // P3 受击蜕鳞累计伤害
        private int myriadGateCount = 2; // 万魂门门数 (怨念账定)
        private bool myriadDone;

        // —— 服务器/本地 ——
        private int lastLife = -1;
        private int lastPhase = 1;
        private int enrageBreathWindup;
        private bool deathRealAllowed;

        // —— 演出标量 (纯本地视觉) ——
        private float fogWarp;           // GenericWarp·fog 限视冥雾
        private float riftWarp;          // GenericWarp·rift 裂隙吸入
        private float breathBloom;       // RadialBloom 吐息泛光
        private float runic;             // ArenaRunic 符阵预警
        private Vector2 runicCenter;
        private float runicRadius = 360f;
        private bool runicLethal;
        private float ribbonWave = -1f;  // 冥焰披风鞭波位置 (0~1; <0 无波)
        private float ribbonBoost;       // 条带临时增亮
        private float coneVis;           // 吐息锥预警强度 (本地平滑)

        // ===== 供体节 / 弹幕读取的演出协议 =====

        /// <summary>整虫是否隐匿于门内 (绘制跳过 + 零接触伤害 + 免伤)。由确定性状态推导, 无需同步。</summary>
        public bool BodyHidden {
            get {
                int s = State;
                float t = StateTimer;
                int sub = (int)SubState;
                return s switch {
                    StIntro => t < IntroBurst,
                    StPhaseRift => sub == 1 || (sub == 2 && t < 2),
                    StShuttle => sub % 2 == 1,               // 奇数子状态 = 门内候场
                    StMyriad => sub == 1 || (sub == 2 && t < 2),
                    StDeath => t >= DeathGateAt + 62,        // 头没入葬门后
                    _ => false
                };
            }
        }

        /// <summary>接触伤害窗口 (演出/换阶段一律关闭 — 伤害窗口与视觉严格对齐)。</summary>
        public bool ContactDamageOn => State is not (StIntro or StPhaseRift or StShed or StMyriad or StDeath);

        /// <summary>暴怒可视强度 0~1 (体节泛红 / 条带 uEnrage)。</summary>
        public float EnrageVis { get; private set; }

        /// <summary>死亡逐节爆燃波是否推进中。</summary>
        public bool DeathWaveActive => State == StDeath && StateTimer >= DeathBodyStart;

        /// <summary>死亡波前所在节序 (自尾 SummonMax+1 向头 0 递减; 体节序 ≥ 波前即爆)。</summary>
        public int DeathWaveFront => SummonMax + 1 - (int)((StateTimer - DeathBodyStart) / 4f);

        /// <summary>阶段 (按 life 比例确定, 无需额外同步)。</summary>
        public int Phase {
            get {
                float r = NPC.lifeMax > 0 ? NPC.life / (float)NPC.lifeMax : 1f;
                if (r > 0.6f) return 1;
                if (r > 0.3f) return 2;
                return 3;
            }
        }

        private int FlameDamage => Main.masterMode ? 70 : (Main.expertMode ? 55 : 40);
        private int LaserDamage => Main.masterMode ? 95 : (Main.expertMode ? 75 : 50);
        private float SpeedMul => enrageTimer > 0 ? 1.35f : 1f;

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<NetherDragonBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 14;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.height = 50;
            NPC.lifeMax = 120000;
            NPC.damage = 100;
            NPC.defense = 40;
            UnderworldField.SetGrudgeMax(NPC, (int)(NPC.lifeMax * 0.7f));
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherDragonScale>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<NetherStaff>(),
                ModContent.ItemType<Netherlayer>(),
                ModContent.ItemType<Netherthrower>(),
                ModContent.ItemType<NetherSutom>()
            ));
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);
            State = StIntro;
            StateTimer = 0;
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.Activate(NPC.whoAmI);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(anchor.X);
            writer.Write(anchor.Y);
            writer.Write(aimAngle);
            writer.Write(enrageTimer);
            writer.Write(scaleAccum);
            writer.Write((byte)myriadGateCount);
            var flags = new BitsByte();
            flags[0] = myriadDone;
            writer.Write(flags);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            anchor.X = reader.ReadSingle();
            anchor.Y = reader.ReadSingle();
            aimAngle = reader.ReadSingle();
            enrageTimer = reader.ReadInt32();
            scaleAccum = reader.ReadInt32();
            myriadGateCount = reader.ReadByte();
            BitsByte flags = reader.ReadByte();
            myriadDone = flags[0];
        }

        // ============================================================
        //  主循环
        // ============================================================

        public override void AI() {
            base.AI();
            UnderworldPlayer.UnderworldEffect = true;
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 冥雾惰性激活 (OnSpawn 仅服务器执行, 多人客户端在此补挂)
            if (Main.netMode == NetmodeID.MultiplayerClient && !NetherDragonFogSystem.IsActive && State != StDeath)
                NetherDragonFogSystem.Activate(NPC.whoAmI);

            // 全灭撤退: 无有效目标 → 加速离场退场 (状态机保底出口)
            if (State != StDeath && (!Main.player[NPC.target].active || Main.player[NPC.target].dead)) {
                NPC.velocity.Y += 0.3f;
                NPC.velocity.X *= 0.98f;
                NPC.EncourageDespawn(60);
                return;
            }

            // —— 怨念账: 按头部血量损失累计 (供万魂门规模 / 暴怒吐息密度) + P3 蜕鳞累计 ——
            if (lastLife < 0) lastLife = NPC.life;
            int lost = lastLife - NPC.life;
            if (lost > 0) {
                UnderworldField.AddGrudge(NPC, lost);
                if (Phase == 3 && State != StDeath)
                    scaleAccum += lost;
            }
            lastLife = NPC.life;

            // —— 阶段转换 / 万魂门插入 (隐匿与演出中不打断, 出门后再触发) ——
            if (!BodyHidden && State is not (StIntro or StPhaseRift or StShed or StMyriad or StDeath)) {
                CheckPhaseTransition();
                if (!myriadDone && NPC.life <= NPC.lifeMax * 0.15f && State != StMyriad)
                    EnterState(StMyriad);
            }

            StateTimer++;
            switch (State) {
                case StIntro: RunIntro(); break;
                case StWeave: RunWeave(); break;
                case StBreath: RunBreath(); break;
                case StDash: RunDash(); break;
                case StShuttle: RunShuttle(); break;
                case StSweep: RunSweep(); break;
                case StPhaseRift: RunPhaseRift(); break;
                case StShed: RunShed(); break;
                case StMyriad: RunMyriad(); break;
                case StDeath: RunDeath(); break;
            }

            // —— P3 逆鳞被动: 受击累计到位即蜕 (清账反制窗口) ——
            if (Phase == 3 && State is not (StShed or StMyriad or StDeath) && !BodyHidden)
                TryShedScale();

            // —— 暴怒: 只提速与增密, 不缩任何前摇; 暴怒吐息独立 telegraph → 释放 ——
            if (enrageTimer > 0) enrageTimer--;
            EnrageVis = MathHelper.Lerp(EnrageVis, enrageTimer > 0 ? 1f : 0f, 0.08f);
            RunEnrageBreath();

            // 无敌裁决: 演出期免伤 (体节经 realLife 传播)
            NPC.dontTakeDamage = State is StIntro or StPhaseRift or StShed or StDeath || BodyHidden;

            // 朝向
            if (NPC.velocity.LengthSquared() > 0.25f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }

            UpdatePresentation();
        }

        // ============================================================
        //  状态: 入场《开门》
        // ============================================================

        private void RunIntro() {
            float t = StateTimer;

            if ((int)t == 1) {
                // 门锚点确定性可推 (各端一致), 服务器额外生成门并同步
                anchor = Target.Center + new Vector2(Target.direction * -300f, -430f);
                aimAngle = (Target.Center + new Vector2(Target.direction * 200f, -80f) - anchor).ToRotation();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.netUpdate = true;
                    SpawnGate(anchor, aimAngle, IntroCrack, 40, 190f);
                }
            }

            if (t < IntroBurst) {
                // 裂缝生长期: 龙候场于门后, 低频 rumble 渐起
                NPC.velocity = Vector2.Zero;
                NPC.Center = anchor;
                ACMUtils.AddScreenShake(0.8f + t / IntroBurst * 1.8f);
                if ((int)t == IntroBurst - 18)
                    breathBloom = MathF.Max(breathBloom, 0.5f); // 空间破碎白闪前奏
            }
            else if ((int)t == IntroBurst) {
                // 破门俯冲
                StackBodyAt(anchor);
                ClearTrailCache();
                NPC.velocity = aimAngle.ToRotationVector2() * 40f;
                ribbonWave = 0f;
                ribbonBoost = 1f;
                riftWarp = MathF.Max(riftWarp, 0.8f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                if (Main.netMode != NetmodeID.Server)
                    NetherDragonFogSystem.CreateRipple(anchor, 3f);
            }
            else if (t < 150f) {
                // 冲出弧线: 拉起减速
                NPC.velocity = NPC.velocity.RotatedBy(-0.012f * NPC.spriteDirection) * 0.965f;
            }
            else {
                // 凝视静止 (威仪 = 静止): 缓缓滑停, 面向玩家
                NPC.velocity *= 0.90f;
                if (NPC.velocity.LengthSquared() < 4f) {
                    Vector2 face = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = face * 2.2f; // 微速保持转头朝向
                }
            }

            if (t >= IntroDur) {
                RotIndex = 0;
                EnterState(CurrentCycle()[0]);
            }
        }

        // ============================================================
        //  状态: 蛇形游弋 (连接拍 — 有压迫的接近, 不是脱战绕圈)
        // ============================================================

        private void RunWeave() {
            float t = StateTimer;
            int dur = Phase == 1 ? 110 : (Phase == 2 ? 70 : 45);

            // 环绕航点: 缓慢进动 + 呼吸半径; 距离栓绳防脱战
            float orbit = NPC.whoAmI * 1.7f + RotIndex * 1.3f + t * 0.016f;
            float radius = 340f + MathF.Sin(t * 0.05f) * 80f;
            Vector2 waypoint = Target.Center + orbit.ToRotationVector2() * radius;

            float dist = Vector2.Distance(NPC.Center, Target.Center);
            float speed = (dist > 900f ? 19f : 12f) * SpeedMul;
            float steer = dist > 900f ? 0.09f : 0.055f;
            SteerTowards(waypoint, speed, steer);

            // 游弋中身体轻微鞭摆 (活物感)
            SpringOffset = MathF.Sin(t * 0.11f) * 6f;

            if (t >= dur)
                AdvanceCycle();
        }

        // ============================================================
        //  状态: 吐息锥 (锥形 shader 预警 紫→红收口 → 单帧释放)
        //  P2/P3 双段: 第二段短预警重瞄准
        // ============================================================

        private void RunBreath() {
            float t = StateTimer;
            bool twin = Phase >= 2;
            int dur = twin ? 130 : 100;

            // 侧位锁定 (t=1 定边, 不随玩家横跳抖动); 预警期减速 (slow startup 阀门)
            if ((int)t == 1)
                NPC.localAI[3] = NPC.Center.X >= Target.Center.X ? 1f : -1f;
            Vector2 hover = Target.Center + new Vector2(NPC.localAI[3] * 300f, -300f);
            SteerTowards(hover, t < BreathTell ? 7f : 10f, 0.06f);

            // —— 第一段: [0..55] 预警 (前 30f 跟瞄, 后锁定 → 给足逃逸窗口) ——
            if (t <= BreathTell) {
                if (t < 30f)
                    aimAngle = (Target.Center - NPC.Center).ToRotation();
                if ((int)t == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                    aimAngle = (Target.Center - NPC.Center).ToRotation();
                    NPC.netUpdate = true;
                }
                if ((int)t == 6)
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = 0.3f }, NPC.Center);

                // 后撤蓄势: pow(t,8) 末段猛吸 (sharp inhale)
                float reel = MathF.Pow(t / BreathTell, 8f) * 130f;
                Vector2 reelPoint = hover - aimAngle.ToRotationVector2() * reel;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (reelPoint - NPC.Center) * 0.10f, 0.25f);

                coneVis = MathF.Max(coneVis, t / BreathTell);
                breathBloom = MathF.Max(breathBloom, t / BreathTell * 0.55f);
                PublishCone(0, t / BreathTell);
            }

            if ((int)t == BreathTell)
                ReleaseBreath(0.55f, 7);

            // —— 第二段 (P2/P3): [62..92] 短预警重瞄 → 更窄更快 ——
            if (twin) {
                if (t > 62f && t <= 92f) {
                    if (t < 78f)
                        aimAngle = (Target.Center - NPC.Center).ToRotation();
                    if ((int)t == 78 && Main.netMode != NetmodeID.MultiplayerClient) {
                        aimAngle = (Target.Center - NPC.Center).ToRotation();
                        NPC.netUpdate = true;
                    }
                    coneVis = MathF.Max(coneVis, (t - 62f) / 30f);
                    PublishCone(0, (t - 62f) / 30f);
                }
                if ((int)t == 92)
                    ReleaseBreath(0.4f, 6, 12.5f);
            }

            if (t >= dur)
                AdvanceCycle();
        }

        private void ReleaseBreath(float spread, int count, float speed = 10.5f) {
            Vector2 dir = aimAngle.ToRotationVector2();
            int extra = enrageTimer > 0 ? 2 : (int)(UnderworldField.GetGrudgeNormalized(NPC) * 2f);
            BreathVolley(dir, count + extra, spread, speed, FlameDamage);
            NPC.velocity -= dir * 7f;                 // 后坐 (mass is reaction)
            breathBloom = 1f;
            coneVis = 0f;
            ribbonWave = 0f;
            ACMUtils.AddScreenShake(7f);
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f }, NPC.Center);
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.CreateRipple(NPC.Center, 1.4f);
        }

        // ============================================================
        //  状态: 掘墓冲刺 (对齐 → 后撤蓄势 36f → 一帧 46px/f → 硬刹鞭波)
        //  P3 二连 (第二段重新预警)
        // ============================================================

        private void RunDash() {
            float t = StateTimer;
            int cycles = Phase == 3 ? 2 : 1;

            if (t <= 30f) {
                // [0..30] 对齐: 移到玩家侧向 (t=1 定边), 减速 (慢启动阀门)
                if ((int)t == 1)
                    NPC.localAI[2] = NPC.Center.X >= Target.Center.X ? 1f : -1f;
                Vector2 side = Target.Center + new Vector2(NPC.localAI[2] * 480f, -160f);
                SteerTowards(side, 11f * SpeedMul, 0.07f);
                NPC.velocity *= 0.97f;
                if (t < 28f)
                    aimAngle = (Target.Center + Target.velocity * 14f - NPC.Center).ToRotation();
            }
            else if (t < DashLaunch) {
                // [30..66] 蓄势: beep 固定 36f 前置; 锁定瞄准 (预判 14f); pow(t,8) 后撤
                if ((int)t == 31) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        aimAngle = (Target.Center + Target.velocity * 14f - NPC.Center).ToRotation();
                        NPC.netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 1f, Pitch = -0.35f }, NPC.Center);
                }
                float rt = (t - 30f) / DashReel;
                Vector2 reelVel = -aimAngle.ToRotationVector2() * MathF.Pow(rt, 8f) * 34f;
                NPC.velocity = Vector2.Lerp(NPC.velocity * 0.90f, reelVel, 0.35f);
            }
            else if ((int)t == DashLaunch) {
                // 一帧 set 46px/f (launch is a set, not a ramp)
                NPC.velocity = aimAngle.ToRotationVector2() * 46f * SpeedMul;
                ribbonWave = 0f;
                ribbonBoost = 1f;
                SpringOffset = 16f;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(6f);
                if (Main.netMode != NetmodeID.Server)
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.8f);
            }
            else if (t < DashLaunch + 12) {
                // [66..78] 爆发: 复利加速, 零转向 (straight reads fast)
                NPC.velocity *= 1.015f;
                // 早退: 冲过玩家 250px 即进入刹车 (不越屏绕圈)
                if (Vector2.Dot(Target.Center - NPC.Center, NPC.velocity) < 0f &&
                    Vector2.Distance(NPC.Center, Target.Center) > 250f)
                    StateTimer = DashLaunch + 12;
            }
            else if (t < DashLaunch + 27) {
                // [78..93] 硬刹 (slam into position) + 刹车点环形慢火
                NPC.velocity *= 0.86f;
                if ((int)t == DashLaunch + 13) {
                    SpringOffset = -14f;   // 反甩 → 鞭波沿身传播
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 v = (MathHelper.TwoPi * i / 6f).ToRotationVector2() * 3.4f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, v,
                                ModContent.ProjectileType<NetherFlameProjectile>(), FlameDamage, 0f);
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = -0.2f }, NPC.Center);
                }
            }
            else {
                // 循环 / 收招
                if ((int)SubState + 1 < cycles) {
                    SubState++;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
                else {
                    AdvanceCycle();
                }
            }
        }

        // ============================================================
        //  状态: 门梭 (teleport-loop 戳刺 — 门网络穿梭, 无"飞回"死时间)
        //  子状态: 0=入门俯冲; 奇数=门内候场(出口裂缝预警); 偶数≥2=戳刺飞行
        // ============================================================

        private void RunShuttle() {
            int pokes = Phase == 3 ? 4 : 3;
            int sub = (int)SubState;
            float t = StateTimer;
            int crackTime = Phase == 3 ? 30 : 34;

            if (sub == 0) {
                // —— 入门俯冲: 门先开, 龙再钻 (可见的"潜入门中", 非凭空消失) ——
                if ((int)t == 1) {
                    anchor = NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitX) * 200f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.netUpdate = true;
                        SpawnGate(anchor, 0f, 6, 34, 140f); // 短快门 (吞没非攻击, 无红线)
                    }
                }
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    (anchor - NPC.Center).SafeNormalize(Vector2.UnitX) * 20f, 0.2f);

                // 门破开 (~20f) 后才允许钻入 — 先见门后见钻
                if (t >= 30f || (t >= 20f && Vector2.DistanceSquared(NPC.Center, anchor) < 3600f)) {
                    SwallowBody();
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (sub % 2 == 1) {
                // —— 门内候场: 出口门开裂 (裂缝转红即戳刺预警线) ——
                NPC.velocity = Vector2.Zero;
                NPC.Center = anchor;
                int pokeIdx = (sub - 1) / 2;

                if ((int)t == 8 && Main.netMode != NetmodeID.MultiplayerClient) {
                    // 出口: 自上一门方位黄金角进动 + 位移预判; crackTime≥24 → 门自带红色戳刺线
                    float baseAng = (anchor - Target.Center).ToRotation() + ACMUtils.GoldenAngle;
                    Vector2 gatePos = Target.Center + baseAng.ToRotationVector2() * 430f;
                    Vector2 thrust = (Target.Center + Target.velocity * 14f - gatePos).SafeNormalize(Vector2.UnitY);
                    // 最小距离阀: 玩家贴门时门后撤一步, 防止贴脸戳刺
                    if (Vector2.Distance(gatePos, Target.Center) < 240f)
                        gatePos -= thrust * 160f;
                    anchor = gatePos;
                    aimAngle = thrust.ToRotation();
                    NPC.netUpdate = true;
                    SpawnGate(anchor, aimAngle, crackTime, 46, 130f);
                }

                if (t >= 8 + crackTime + 2) {
                    // 破门戳刺: 一帧 52px/f (逐戳递增)
                    StackBodyAt(anchor);
                    ClearTrailCache();
                    NPC.velocity = aimAngle.ToRotationVector2() * (52f + pokeIdx * 2f);
                    SubState++;
                    StateTimer = 0;
                    ribbonWave = 0f;
                    ribbonBoost = 1f;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
                    ACMUtils.AddScreenShake(5f);
                    NPC.netUpdate = true;
                }
            }
            else {
                // —— 戳刺飞行: 16f 直线 → 减速 → 回潜/收尾 ——
                int pokeIdx = sub / 2 - 1;
                if (t < 16f) {
                    NPC.velocity *= 1.01f;
                }
                else if (t < 30f) {
                    NPC.velocity *= 0.88f;
                    if ((int)t == 20 && pokeIdx + 1 < pokes) {
                        // 回潜门在头部去向前方开一道快门
                        anchor = NPC.Center + NPC.velocity.SafeNormalize(aimAngle.ToRotationVector2()) * 150f;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            NPC.netUpdate = true;
                            SpawnGate(anchor, 0f, 6, 30, 120f);
                        }
                    }
                }
                else if (pokeIdx + 1 < pokes) {
                    // 潜入回潜门 (~40f 门开) → 下一戳
                    if (t >= 42f || (t >= 36f && Vector2.DistanceSquared(NPC.Center, anchor) < 3600f)) {
                        SwallowBody();
                        SubState++;
                        StateTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        NPC.velocity = Vector2.Lerp(NPC.velocity,
                            (anchor - NPC.Center).SafeNormalize(Vector2.UnitX) * 20f, 0.25f);
                    }
                }
                else {
                    // 最后一戳: 不回潜 — 门在身后关闭, ~50f 明确的可打喘息窗
                    NPC.velocity *= 0.94f;
                    SpringOffset = MathF.Sin(t * 0.2f) * 8f;
                    if (t >= 78f)
                        AdvanceCycle();
                }
            }
        }

        // ============================================================
        //  状态: 魂束扫射 (扇形预警 75f → 恒速扫射 90f; P3 双向剪刀)
        // ============================================================

        private void RunSweep() {
            float t = StateTimer;
            int dur = 20 + SweepTell + SweepDur + 25;

            // 锚定漂移: 与玩家保持 460px, 缓慢横移 (持续攻击的内部调制)
            Vector2 hold = Target.Center + (NPC.Center - Target.Center).SafeNormalize(Vector2.UnitX) * 460f;
            hold += (t * 0.02f).ToRotationVector2() * 40f;
            SteerTowards(hold, 6.5f, 0.05f);

            if ((int)t == 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                aimAngle = (Target.Center - NPC.Center).ToRotation();
                NPC.netUpdate = true;
                float arc = MathHelper.ToRadians(80f);
                if (Phase == 3) {
                    // 双向剪刀: 两束自玩家线同时向两侧扫开
                    SpawnSweepBeam(aimAngle, +arc * 0.5f);
                    SpawnSweepBeam(aimAngle, -arc * 0.5f);
                }
                else {
                    // 单束: 起点偏后, 扫过玩家线
                    float dir = Main.rand.NextBool() ? 1f : -1f;
                    SpawnSweepBeam(aimAngle - dir * arc * 0.5f, dir * arc);
                }
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
            }

            // 扫射期头部后坐微退 (发射器的反作用)
            if (t > 20 + SweepTell && t < 20 + SweepTell + SweepDur)
                NPC.velocity -= aimAngle.ToRotationVector2() * 0.05f;

            if (t >= dur)
                AdvanceCycle();
        }

        private void SpawnSweepBeam(float startAngle, float signedArc) {
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<NetherLaserBeam>(), LaserDamage, 0f,
                ai0: startAngle, ai1: signedArc, ai2: NPC.whoAmI);
        }

        // ============================================================
        //  状态: P1→P2《裂土》换阶段 (吞没 → 静默 → 两假一真 → 轰出)
        // ============================================================

        private void RunPhaseRift() {
            float t = StateTimer;
            int sub = (int)SubState;

            if (sub == 0) {
                // —— 吞没: 身后开门倒吸 ——
                if ((int)t == 1) {
                    ClearOwnProjectiles();
                    anchor = NPC.Center - NPC.velocity.SafeNormalize(Vector2.UnitX) * 260f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.netUpdate = true;
                        SpawnGate(anchor, 0f, 16, 46, 170f);
                    }
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                }
                // 被门倒吸 (先挣扎减速再猛拽)
                Vector2 pull = anchor - NPC.Center;
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    pull.SafeNormalize(Vector2.UnitX) * MathF.Min(pull.Length() * 0.2f, 30f),
                    t < 20f ? 0.05f : 0.22f);
                riftWarp = MathF.Max(riftWarp, MathHelper.Clamp(t / 44f, 0f, 0.7f));

                // 门破开 (~30f) 后才吞没 — 先见门后见吞
                if (t >= 44f || (t >= 32f && pull.LengthSquared() < 3600f)) {
                    SwallowBody();
                    ACMUtils.AddScreenShake(8f);
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (sub == 1) {
                // —— 静默 60f (全场只剩雾) → 三符阵门两假一真 ——
                NPC.velocity = Vector2.Zero;
                NPC.Center = anchor;

                if ((int)t == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
                    int realIdx = Main.rand.Next(3);
                    for (int i = 0; i < 3; i++) {
                        float ang = baseAng + MathHelper.TwoPi * i / 3f;
                        Vector2 pos = Target.Center + ang.ToRotationVector2() * 480f;
                        if (i == realIdx) {
                            anchor = pos;
                            aimAngle = (Target.Center - pos).ToRotation();
                            SpawnGate(pos, aimAngle, 48, 60, 170f);
                        }
                        else {
                            SpawnGate(pos, ang + MathHelper.Pi, 48, 0, -150f); // 负高 = 假门, 转红前枯萎
                        }
                    }
                    NPC.netUpdate = true;
                }

                // 符阵指示真门落点 (先紫后红, 与门转红同步)
                if (t > 60f) {
                    runicCenter = anchor;
                    runicRadius = 260f;
                    runic = MathF.Max(runic, MathHelper.Clamp((t - 60f) / 30f, 0f, 1f) * 0.9f);
                    runicLethal = t > 60f + 28f;
                }

                // 真门 48f 裂纹 + 14f 破开 → 62f 后轰出
                if (t >= 60f + 62f) {
                    StackBodyAt(anchor);
                    ClearTrailCache();
                    NPC.velocity = aimAngle.ToRotationVector2() * 34f;
                    SubState = 2;
                    StateTimer = 0;
                    ribbonWave = 0f;
                    ribbonBoost = 1f;
                    riftWarp = 0.85f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                    ACMUtils.AddScreenShake(9f);
                    if (Main.netMode != NetmodeID.Server)
                        NetherDragonFogSystem.CreateRipple(anchor, 3f);
                    NPC.netUpdate = true;
                }
            }
            else {
                // —— 轰出恢复: 45f 缓落 → 回到 P2 循环 ——
                NPC.velocity *= 0.97f;
                if (t >= 45f) {
                    RotIndex = 0;
                    EnterState(CurrentCycle()[0]);
                }
            }
        }

        // ============================================================
        //  状态: P2→P3《噬墓》换阶段 (仰天怒吼蜕鳞 — 短促不断节奏)
        // ============================================================

        private void RunShed() {
            float t = StateTimer;

            if (t < 20f) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -5f), 0.08f);
                if ((int)t == 1)
                    ClearOwnProjectiles();
            }
            else if ((int)t == 20) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.4f, Volume = 1.1f }, NPC.Center);
                ACMUtils.AddScreenShake(9f);
                ribbonWave = 0f;
                ribbonBoost = 1f;
                breathBloom = MathF.Max(breathBloom, 0.8f);
                if (Main.netMode != NetmodeID.Server)
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 2.2f);
                // 首次蜕鳞演出: 2 枚逆鳞弹出
                SpawnScaleOrbs(2);
            }
            else {
                NPC.velocity *= 0.96f;
                SpringOffset = MathF.Sin(t * 0.5f) * (10f * (1f - t / 90f));
            }

            if (t >= 90f) {
                RotIndex = 0;
                EnterState(CurrentCycle()[0]);
            }
        }

        // ============================================================
        //  状态: 万魂门 (≤15% 一次性终结技 — 怨念账清算)
        // ============================================================

        private void RunMyriad() {
            float t = StateTimer;
            int sub = (int)SubState;
            const float ringR = 620f;

            if (sub == 0) {
                // —— 吞没 ——
                if ((int)t == 1) {
                    ClearOwnProjectiles();
                    anchor = NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitX) * 140f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        float g = UnderworldField.GetGrudgeNormalized(NPC);
                        myriadGateCount = 2 + (g > 0.35f ? 1 : 0) + (g > 0.7f ? 1 : 0);
                        aimAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        NPC.netUpdate = true;
                        SpawnGate(anchor, 0f, 14, 30, 150f);
                    }
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                }
                Vector2 pull = anchor - NPC.Center;
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    pull.SafeNormalize(Vector2.UnitX) * MathF.Min(pull.Length() * 0.25f, 26f), 0.2f);

                // 门破开 (~28f) 后才吞没
                if (t >= 34f || (t >= 28f && pull.LengthSquared() < 3600f)) {
                    SwallowBody();
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (sub == 1) {
                // —— 环上诸门依次开裂扫射; 最后一门 = 龙的出口 (红色戳刺线) ——
                NPC.velocity = Vector2.Zero;
                NPC.Center = anchor;

                runicCenter = Target.Center;
                runicRadius = ringR;
                runic = MathF.Max(runic, MathHelper.Clamp(t / 40f, 0f, 1f) * 0.8f);
                runicLethal = false;

                int n = myriadGateCount;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 各扫射门: t = 20 + k*35 开裂 (短裂纹无戳刺线 — 束不沿门轴) → 其魂束自带 50f 预警
                    for (int k = 0; k < n; k++) {
                        if ((int)t == 20 + k * 35) {
                            float ang = aimAngle + MathHelper.TwoPi * k / n;
                            Vector2 pos = Target.Center + ang.ToRotationVector2() * ringR;
                            SpawnGate(pos, ang + MathHelper.Pi, 22, 220, 140f);
                            // 切向扫束: 40° 弧, 方向交替 → 交叉火网但有安全缝
                            float dir = k % 2 == 0 ? 1f : -1f;
                            float tangent = ang + MathHelper.PiOver2 * dir;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                                ModContent.ProjectileType<NetherLaserBeam>(), LaserDamage, 0f,
                                ai0: tangent - MathHelper.ToRadians(20f) * dir,
                                ai1: MathHelper.ToRadians(40f) * dir, ai2: -1);
                        }
                    }
                    // 出口门: 环外远点, 长裂纹 + 戳刺线穿环心 (crackTime≥30 自动红线)
                    if ((int)t == 20 + n * 35 + 20) {
                        float ang = aimAngle + MathHelper.Pi / n;
                        anchor = Target.Center + ang.ToRotationVector2() * (ringR + 200f);
                        aimAngle = (Target.Center + Target.velocity * 16f - anchor).ToRotation();
                        NPC.netUpdate = true;
                        SpawnGate(anchor, aimAngle, 34, 40, 170f);
                    }
                }

                // 出口轰出 (34 裂纹 + 14 破开 + 2)
                if (t >= 20 + n * 35 + 20 + 50) {
                    StackBodyAt(anchor);
                    ClearTrailCache();
                    NPC.velocity = aimAngle.ToRotationVector2() * 44f;
                    SubState = 2;
                    StateTimer = 0;
                    myriadDone = true;
                    ribbonWave = 0f;
                    ribbonBoost = 1f;
                    riftWarp = 0.9f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    ACMUtils.AddScreenShake(11f);
                    NPC.netUpdate = true;
                }
            }
            else {
                // —— 穿环而出 → 收尾 ——
                NPC.velocity *= 0.97f;
                if (t >= 50f) {
                    RotIndex = 0;
                    EnterState(CurrentCycle()[0]);
                }
            }
        }

        // ============================================================
        //  状态: 死亡《葬门》
        // ============================================================

        private void RunDeath() {
            float t = StateTimer;
            NPC.dontTakeDamage = true;

            if (t < DeathBodyStart) {
                // 硬刹抽搐 + 长吼
                NPC.velocity *= 0.90f;
                if ((int)t % 9 == 0)
                    NPC.velocity += Main.rand.NextVector2Circular(2.2f, 2.2f);
                if ((int)t == 2) {
                    ClearOwnProjectiles();
                    KillScaleOrbs();
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                }
            }
            else if (t < DeathGateAt) {
                // 逐节爆燃波推进中 (体节侧自爆, 见 NetherDragon.AI); 头部衰弱爬升
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(MathF.Sin(t * 0.05f) * 2f, -2.4f), 0.04f);
                ribbonBoost = 0.9f;
            }
            else if ((int)t == DeathGateAt) {
                // 葬门在下方裂开 (巨门)
                anchor = NPC.Center + new Vector2(0f, 250f);
                aimAngle = -MathHelper.PiOver2;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.netUpdate = true;
                    SpawnGate(anchor, aimAngle, 20, 28, 300f);
                }
                SoundEngine.PlaySound(SoundID.Item101 with { Pitch = -0.6f, Volume = 1.1f }, NPC.Center);
            }
            else if (t < DeathGateAt + 62) {
                // 被葬门吸入
                Vector2 pull = anchor - NPC.Center;
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    pull.SafeNormalize(Vector2.UnitY) * MathF.Min(4f + (t - DeathGateAt) * 0.35f, 24f), 0.16f);
                riftWarp = MathF.Max(riftWarp, MathHelper.Clamp((t - DeathGateAt) / 60f, 0f, 0.9f));
                ACMUtils.AddScreenShake(1.5f + (t - DeathGateAt) / 60f * 3f);
            }
            else if ((int)t == DeathGateAt + 62) {
                // 没入 → 巨门砰然合拢: 全场唯一 shake 16
                NPC.Center = anchor;
                NPC.velocity = Vector2.Zero;
                breathBloom = 1f;
                ACMUtils.AddScreenShake(16f);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 1.3f }, anchor);
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.6f }, anchor);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++)
                        NetherDragonFogSystem.CreateRipple(anchor + Main.rand.NextVector2Circular(40f, 40f), 3f - i * 0.4f);
                }
            }

            if (t >= DeathEnd) {
                // 真死 (掉落自葬门喷出)
                deathRealAllowed = true;
                NPC.Center = anchor;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.life = 0;
                    NPC.checkDead();
                }
            }
        }

        public override bool CheckDead() {
            if (deathRealAllowed)
                return true;
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            if (State != StDeath) {
                EnterState(StDeath);
                NPC.netUpdate = true;
            }
            return false;
        }

        // ============================================================
        //  阶段 / 循环表
        // ============================================================

        private int[] CurrentCycle() => Phase switch {
            1 => CycleP1,
            2 => CycleP2,
            _ => CycleP3
        };

        private void AdvanceCycle() {
            int[] table = CurrentCycle();
            RotIndex = ((int)RotIndex + 1) % table.Length;
            EnterState(table[(int)RotIndex]);
        }

        private void EnterState(int state) {
            State = state;
            StateTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private void CheckPhaseTransition() {
            int p = Phase;
            if (p == lastPhase)
                return;
            lastPhase = p;

            ACMUtils.AddScreenShake(8f);
            riftWarp = MathF.Max(riftWarp, 0.6f);
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.CreateRipple(NPC.Center, 2.4f);
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);

            if (p == 2)
                EnterState(StPhaseRift);
            else if (p == 3)
                EnterState(StShed);
        }

        // ============================================================
        //  P3 逆鳞 / 暴怒
        // ============================================================

        private void TryShedScale() {
            // 受击蜕落: 累计伤害达阈值且场上逆鳞 < 2 → 蜕 1 枚 (服务器权威)
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (scaleAccum < NPC.lifeMax * 0.09f || CountScaleOrbs() >= 2)
                return;
            scaleAccum = 0;
            NPC.netUpdate = true;
            SpawnScaleOrbs(1);
        }

        private void SpawnScaleOrbs(int count) {
            ribbonWave = 0.3f;   // 鞭波自中段荡开 (蜕鳞的身体反应)
            SoundEngine.PlaySound(SoundID.NPCDeath39 with { Pitch = -0.3f }, NPC.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
                return;
            int orbType = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < count; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, orbType,
                    0, NPC.whoAmI, ang, NPC.target);
                if (idx >= 0 && idx < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: idx);
            }
        }

        private int CountScaleOrbs() {
            int c = 0;
            int t = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == t && (int)n.ai[0] == NPC.whoAmI)
                    c++;
            }
            return c;
        }

        private void KillScaleOrbs() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int t = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == t && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        /// <summary>逆鳞超时未清 → 暴怒 (由 NetherScaleOrb 服务器端回调)。</summary>
        public void TriggerEnrage() {
            enrageTimer = 240;
            enrageBreathWindup = 45;
            NPC.netUpdate = true;
            ACMUtils.AddScreenShake(8f);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.4f }, NPC.Center);
        }

        private void RunEnrageBreath() {
            if (enrageBreathWindup <= 0 || BodyHidden)
                return; // 门内暂停, 出门再吐 (预警不会凭空出现)
            enrageBreathWindup--;
            aimAngle = (Target.Center - NPC.Center).ToRotation();
            coneVis = MathF.Max(coneVis, 1f - enrageBreathWindup / 45f);
            PublishCone(1, 1f - enrageBreathWindup / 45f);
            breathBloom = MathF.Max(breathBloom, 1f - enrageBreathWindup / 45f);
            if (enrageBreathWindup == 0) {
                float grudge = UnderworldField.GetGrudgeNormalized(NPC);
                BreathVolley(aimAngle.ToRotationVector2(), 9 + (int)(grudge * 4f), 0.7f, 11f, FlameDamage);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                breathBloom = 1f;
                coneVis = 0f;
            }
        }

        // ============================================================
        //  攻击 / 移动 / 工具
        // ============================================================

        private void BreathVolley(Vector2 dir, int count, float spreadRad, float speed, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
                return;
            dir = dir.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                float k = count <= 1 ? 0.5f : i / (float)(count - 1);
                float ang = MathHelper.Lerp(-spreadRad, spreadRad, k);
                // 速度分层 → 锥内留可穿缝隙
                float v = speed + (i % 2 == 0 ? 1.4f : -1.1f) + Main.rand.NextFloat(-0.6f, 0.6f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 44f, dir.RotatedBy(ang) * v,
                    ModContent.ProjectileType<NetherFlameProjectile>(), damage, 0f);
            }
        }

        private void SteerTowards(Vector2 pos, float speed, float inertia) {
            Vector2 desired = (pos - NPC.Center).SafeNormalize(Vector2.UnitX) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, inertia);
        }

        /// <summary>生成一道冥界之门 (服务器); crack/hold 打包入 ai → 各端确定性推进。</summary>
        private void SpawnGate(Vector2 pos, float dirRad, int crackTime, int holdTime, float halfHeight) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<NetherPortal>(), 0, 0f,
                ai0: dirRad, ai1: NetherPortal.PackTimes(crackTime, holdTime), ai2: halfHeight);
        }

        /// <summary>整虫吞没: 体节向头收拢 + 沿身魂尘内爆 (掩护消失帧)。</summary>
        private void SwallowBody() {
            if (!Main.dedServ) {
                var chain = NetherDragonVFX.CollectChain(NPC);
                foreach (Vector2 p in chain) {
                    for (int i = 0; i < 2; i++) {
                        var d = Dust.NewDustPerfect(p + Main.rand.NextVector2Circular(10f, 10f),
                            Main.rand.NextBool() ? DustID.GreenTorch : DustID.PurpleTorch, Vector2.Zero, 110,
                            new Color(110, 230, 150), 1.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f, Volume = 0.8f }, NPC.Center);
            }
            StackBodyAt(anchor);
            NPC.velocity = Vector2.Zero;
        }

        /// <summary>全部体节收拢到一点 (随后自然从门中鱼贯拉出)。</summary>
        private void StackBodyAt(Vector2 pos) {
            NPC.Center = pos;
            NPC.netUpdate = true;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.realLife != NPC.whoAmI || n.whoAmI == NPC.whoAmI)
                    continue;
                if (n.ModNPC is not NetherDragon seg)
                    continue;
                n.Center = pos;
                n.velocity = Vector2.Zero;
                seg.SpringOffset = 0f;
                seg.SpringVel = 0f;
                n.netUpdate = true;
            }
        }

        private void ClearTrailCache() {
            for (int i = 0; i < NPC.oldPos.Length; i++)
                NPC.oldPos[i] = NPC.position;
        }

        private void ClearOwnProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int flame = ModContent.ProjectileType<NetherFlameProjectile>();
            int trail = ModContent.ProjectileType<NetherFlameTrail>();
            int beam = ModContent.ProjectileType<NetherLaserBeam>();
            int portal = ModContent.ProjectileType<NetherPortal>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active)
                    continue;
                if (p.type == flame || p.type == trail || p.type == beam)
                    p.Kill();
                else if (p.type == portal && p.ModProjectile is NetherPortal gate)
                    gate.StartClosing();
            }
        }

        public override void OnKill() {
            base.OnKill();
            ClearOwnProjectiles();
            KillScaleOrbs();

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++)
                    NetherDragonFogSystem.CreateRipple(NPC.Center + Main.rand.NextVector2Circular(50f, 50f), 2.5f - i * 0.1f);
                NetherDragonFogSystem.Deactivate();
            }

            NetherDragonDownedSystem.OnNetherDragonKilled();
        }

        // ============================================================
        //  演出标量 / 绘制
        // ============================================================

        private void UpdatePresentation() {
            // 冥雾: P1 最浓; 裂土静默拍短暂拉满 (全场只剩雾的恐怖); 葬门合拢后散尽
            float fogTarget = Phase == 1 ? 0.42f : (Phase == 2 ? 0.32f : 0.30f);
            if (State == StPhaseRift && (int)SubState == 1)
                fogTarget = 0.55f;
            if (State == StDeath && StateTimer > DeathGateAt + 62)
                fogTarget = 0f;
            fogWarp = MathHelper.Lerp(fogWarp, fogTarget, 0.02f);

            riftWarp = MathHelper.Lerp(riftWarp, 0f, 0.05f);
            breathBloom = MathHelper.Lerp(breathBloom, 0f, 0.08f);
            runic = MathHelper.Lerp(runic, 0f, 0.06f);
            coneVis = MathHelper.Lerp(coneVis, 0f, 0.15f);
            ribbonBoost = MathHelper.Lerp(ribbonBoost, 0f, 0.05f);

            // 鞭波推进: 0→1 沿身行进后熄灭
            if (ribbonWave >= 0f) {
                ribbonWave += 1f / 38f;
                if (ribbonWave > 1f)
                    ribbonWave = -1f;
            }

            if (!Main.dedServ) {
                NetherDragonScreenSystem.Publish(breathBloom, NPC.Center,
                    runic, runicCenter, runicRadius, runicLethal, (float)Main.GlobalTimeWrappedHourly);
            }
        }

        private void PublishCone(int slot, float progress) {
            if (Main.dedServ)
                return;
            NetherDragonScreenSystem.PublishCone(slot, NPC.Center + aimAngle.ToRotationVector2() * 30f,
                aimAngle, 0.55f, 640f, MathHelper.Clamp(progress, 0f, 1f),
                MathHelper.Clamp(coneVis, 0f, 1f));

            // 着色器关闭时的 dust 退化: 锥形两界线描点 (预警不可缺席)
            if (!MythologyConfig.FullscreenShadersEnabled && Main.GameUpdateCount % 2 == 0) {
                bool hot = progress > 0.6f;
                Color c = hot ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                for (int e = -1; e <= 1; e += 2) {
                    Vector2 edge = (aimAngle + 0.55f * e).ToRotationVector2();
                    for (int i = 0; i < 4; i++) {
                        Vector2 p = NPC.Center + edge * (640f * (i + 1) / 4f);
                        var d = Dust.NewDustPerfect(p, hot ? DustID.RedTorch : DustID.PurpleTorch,
                            Vector2.Zero, 100, c, 1.1f);
                        d.noGravity = true;
                        d.velocity = Vector2.Zero;
                    }
                }
            }
        }

        // ===== 全屏 screenTarget 扭曲 (GenericWarp · fog/rift) — 占唯一全屏名额 =====
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            // 冲刺蓄势的红色路径线 (§6.1: 只画在蓄势与爆发早段)
            if (State == StDash && StateTimer > 31f && StateTimer < DashLaunch + 5) {
                float k = MathHelper.Clamp((StateTimer - 31f) / DashReel, 0f, 1f);
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + aimAngle.ToRotationVector2() * 820f,
                    2.5f + k * 3f, TelegraphColors.Lethal, TelegraphColors.Lethal with { A = 0 },
                    0.35f + k * 0.5f, flowSpeed: 2.4f, flowScale: 3f, coreSharp: 3f);
            }

            bool useRift = riftWarp > 0.04f;
            float intensity = useRift ? riftWarp : fogWarp;
            if (intensity <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            if (useRift) {
                fx.Parameters["uRadius"]?.SetValue(0.55f);
                fx.Parameters["uWarpScale"]?.SetValue(1.3f);
                fx.Parameters["uChroma"]?.SetValue(0.7f);
                fx.Parameters["uRadialPull"]?.SetValue(0.6f);   // 向心吸入 = 裂隙
                fx.Parameters["uMode"]?.SetValue(3f);           // rift
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.55f));
            }
            else {
                fx.Parameters["uRadius"]?.SetValue(1.0f);
                fx.Parameters["uWarpScale"]?.SetValue(0.8f);
                fx.Parameters["uChroma"]?.SetValue(0.25f);
                fx.Parameters["uRadialPull"]?.SetValue(0f);
                fx.Parameters["uMode"]?.SetValue(2f);           // fog
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.4f));
            }

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (BodyHidden)
                return false;

            // 冥焰披风: 头部收集全身链铺一条条带 (画在本体之下)
            float deathBreak = 0f;
            if (State == StDeath && StateTimer >= DeathBodyStart)
                deathBreak = MathHelper.Clamp((StateTimer - DeathBodyStart) / ((SummonMax + 1) * 4f), 0f, 1f);
            NetherDragonVFX.DrawBodyRibbon(NPC, ribbonWave, EnrageVis, deathBreak,
                0.45f + ribbonBoost * 0.5f);

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            SpriteEffects fxFlip = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Color netherColor = Color.Lerp(drawColor, new Color(120, 90, 200), 0.5f);
            if (EnrageVis > 0.01f)
                netherColor = Color.Lerp(netherColor, new Color(235, 90, 80), EnrageVis * 0.45f);

            // 高速残影 (速度门控 — 常开即噪声)
            float spd = NPC.velocity.Length();
            if (spd > 28f) {
                float ghostA = MathHelper.Clamp((spd - 28f) / 22f, 0f, 1f) * 0.5f;
                for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                    if (NPC.oldPos[i] == Vector2.Zero)
                        continue;
                    float k = 1f - i / (float)NPC.oldPos.Length;
                    Vector2 gpos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    spriteBatch.Draw(tex, gpos, null, (TelegraphColors.GhostGreen with { A = 0 }) * (ghostA * k),
                        NPC.rotation + MathHelper.PiOver2, origin, NPC.scale * (0.98f - i * 0.015f), fxFlip, 0);
                }
            }

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2,
                origin, NPC.scale, fxFlip, 0);

            if (hitFlash > 0.05f) {
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, (Color.White with { A = 0 }) * (hitFlash * 0.55f),
                    NPC.rotation + MathHelper.PiOver2, origin, NPC.scale, fxFlip, 0);
            }

            return false;
        }
    }
}

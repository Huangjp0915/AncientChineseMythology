using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 尸骸·判官之手 —— V3 执行器化重做。
    ///
    /// 设计要点 (Docs/BossRedo/Corpses.md §4):
    ///   ● 手不再自主攻击, 全部招式由 <see cref="Corpses"/> 节拍表下达指令 (确定性时刻, 多端一致);
    ///   ● 每招严格三段波形: 长前摇(慢/高/counter-motion) → 瞬发爆发(poly 高次) → 收招;
    ///   ● <b>伤害窗口只在爆发段</b> (CanHitPlayer 严格对齐视觉), 预警一律走 CorpsesBoneRing decal;
    ///   ● 旧版抓取锁位 (player.Center 直改, 多人不安全) 删除, 命中改叠魂蚀/冥律身份层;
    ///   ● Controlled/Channeling/Stunned 编排接口保留 (万骸旋冢 / 引魂大阵 / 破阵硬直)。
    /// </summary>
    internal class CorpsesHand : ModNPC
    {
        // ====== IK 骨臂参数 ======
        private const float UpperArmLength = 120f;
        private const float ForearmLength = 100f;
        private const float MaxReach = UpperArmLength + ForearmLength - 10f;
        [VaultLoaden("AncientChineseMythology/Underworlds/Boss/Corpseses/")]
        private static Texture2D CorpsesArm = null; // 手臂纹理 26x98

        private Vector2 shoulderPos;
        private Vector2 elbowPos;
        private Vector2 handPos;

        // ====== 状态机 ======
        public enum HandState
        {
            Idle,           // 待机呼吸浮动 (跟随头颅)
            Retracting,     // 回位
            PalmSlam,       // 崩掌拍落: 抬手蓄势 → 顶点悬停 → 瞬拍 → 落地锁定
            BoneSweep,      // 白骨横扫: 后摆锁线 → 瞬扫 → 硬刹
            BoneVolley,     // 指骨连环: 后摆 → 3 波甩腕骨镖 (带后坐)
            ClapPincer,     // 合掌夹击: 飞位 → 反向拉开 → 静止 → 瞬合 → 锁定
            Controlled,     // Boss 外部驱动 (旋冢环绕 / 就坛飞行)
            Channeling,     // 就坛施法 (引魂大阵)
            Stunned,        // 破阵硬直坠落
            Materializing,  // 入场演出: 尸雾中重凝现身
            Dying           // 死亡演出: 坠地崩解
        }

        private HandState currentState = HandState.Idle;
        private int stateTimer = 0;

        // ====== 招式同步数据 ======
        private Vector2 aimPoint;          // 落点 / 合击点 / 扫掠通过点 / 齐射目标
        private Vector2 axisDir = Vector2.UnitX; // 合掌轴向 / 扫掠方向 (单位向量)
        private Vector2 startPos;          // 招式起手位置 (发令帧锁定)
        private bool sprayOnImpact;        // 拍落是否溅射骨镖 (P2/P3)
        private int volleyWave;            // 指骨连环已发波数

        // ====== 编排接口数据 (V2 保留) ======
        private Vector2 controlledPos;
        private bool controlledCanHit;
        private int stunTimer;
        private float detachDissolve = 0f;   // 骨→魂→骨 溶解脉冲 0~1
        private int detachDissolveDir = 0;   // +1 溶出 / -1 重凝

        // ====== 纯视觉 (本地) ======
        private int clapBloomTimer;
        private Vector2 clapBloomPos;
        private int impactRingTimer;         // 冲击环 decal 残留
        private Vector2 impactRingPos;
        private readonly List<Vector2> oldPositions = new();
        private readonly List<float> oldRotations = new();
        private const int TrailLength = 14;
        private float flameSeed;

        public HandState State => currentState;
        public bool InControlled => currentState == HandState.Controlled;
        public bool InChanneling => currentState == HandState.Channeling;
        public bool IsStunned => currentState == HandState.Stunned;
        public bool IsIdle() => currentState == HandState.Idle;

        /// <summary>攻击指令仅在待机/回位时接受 (节拍表已保证间距, 此为兜底)。</summary>
        private bool CanAcceptCommand => currentState == HandState.Idle || currentState == HandState.Retracting;

        public int Direction {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        // ====== 招式时间表 (帧) ======
        // 崩掌拍落: 抬手 38 + 顶点悬停 14 + 拍落 5 + 落地锁定 16 + 收 30
        private const int SlamHoist = 38, SlamHold = 14, SlamDrop = 5, SlamLock = 16, SlamRecover = 30;
        private const int SlamDropStart = SlamHoist + SlamHold;
        private const float SlamHoverHeight = 420f;
        // 白骨横扫: 后摆 34 + 静止 8 + 扫 10 + 刹车回摆 26
        private const int SweepBack = 34, SweepStill = 8, SweepStrike = 10, SweepBrake = 26;
        private const int SweepStrikeStart = SweepBack + SweepStill;
        private const float SweepHalfLen = 430f;
        // 指骨连环: 后摆 22 + 3 波 × 16 + 收 24
        private const int VolleyBack = 22, VolleyWaveTime = 16, VolleyWaves = 3;
        // 合掌夹击: 飞位 26 + 外拉 30 + 静止 12 + 合拢 4 + 锁定 18 + 弹开 22
        private const int PincerFly = 26, PincerPull = 30, PincerStill = 12, PincerSnap = 4, PincerLock = 18, PincerRecoil = 22;
        private const int PincerSnapStart = PincerFly + PincerPull + PincerStill;
        private const float PincerFarDist = 430f, PincerNearDist = 280f;

        // ================================================================
        //  Boss 指令接口
        // ================================================================

        /// <summary>崩掌拍落: 落点由 Boss 探地锁定, 抬手期不追踪 (公平阀门)。</summary>
        public bool CommandPalmSlam(Vector2 impactPoint, bool spray) {
            if (!CanAcceptCommand)
                return false;
            aimPoint = impactPoint;
            sprayOnImpact = spray;
            BeginMove(HandState.PalmSlam);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
            return true;
        }

        /// <summary>白骨横扫: 过 through 点、沿 sweepDir 的线段, 后摆期锁线。</summary>
        public bool CommandBoneSweep(Vector2 through, Vector2 sweepDir) {
            if (!CanAcceptCommand)
                return false;
            aimPoint = through;
            axisDir = sweepDir.SafeNormalize(Vector2.UnitX);
            BeginMove(HandState.BoneSweep);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f }, NPC.Center);
            return true;
        }

        /// <summary>指骨连环: 3 波扇形骨镖, 每波带甩腕与后坐。</summary>
        public bool CommandBoneVolley(Vector2 aim) {
            if (!CanAcceptCommand)
                return false;
            aimPoint = aim;
            volleyWave = 0;
            BeginMove(HandState.BoneVolley);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f, Volume = 0.8f }, NPC.Center);
            return true;
        }

        /// <summary>合掌夹击: 两手同帧受令, 合击点/轴向锁定, 轴垂直方向永远敞开。</summary>
        public bool CommandClapPincer(Vector2 meet, Vector2 axis) {
            if (!CanAcceptCommand)
                return false;
            aimPoint = meet;
            axisDir = axis.SafeNormalize(Vector2.UnitX);
            BeginMove(HandState.ClapPincer);
            return true;
        }

        /// <summary>入场演出: 于 pos 处从尸雾中重凝现身。</summary>
        public void BeginMaterialize(Vector2 pos) {
            NPC.Center = pos;
            currentState = HandState.Materializing;
            stateTimer = 0;
            detachDissolve = 1f;
            detachDissolveDir = -1;
            NPC.netUpdate = true;
        }

        /// <summary>死亡演出: 停止一切, 坠地崩解。</summary>
        public void BeginDeathCollapse() {
            currentState = HandState.Dying;
            stateTimer = 0;
            controlledCanHit = false;
            NPC.velocity = new Vector2(NPC.velocity.X * 0.3f, MathF.Min(NPC.velocity.Y, 0f));
            NPC.netUpdate = true;
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
        }

        private void BeginMove(HandState move) {
            currentState = move;
            stateTimer = 0;
            startPos = NPC.Center;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }

        // ====== V2 编排接口 (保留) ======

        /// <summary>进入"外部驱动"状态: 由 Boss 每帧 <see cref="DriveControlled"/> 设定位置。</summary>
        public void EnterControlled(Vector2 pos, bool canHit) {
            if (currentState != HandState.Controlled) {
                detachDissolve = 0f;
                detachDissolveDir = 1; // 脱体溶出
            }
            currentState = HandState.Controlled;
            controlledPos = pos;
            controlledCanHit = canHit;
            stateTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>更新外部驱动目标位 (每帧调用, 不重置 dissolve)。</summary>
        public void DriveControlled(Vector2 pos, bool canHit) {
            if (currentState == HandState.Controlled) {
                controlledPos = pos;
                controlledCanHit = canHit;
            }
        }

        /// <summary>进入"就坛施法"状态 (引魂大阵)。</summary>
        public void EnterChanneling(Vector2 altarPos) {
            if (currentState != HandState.Channeling) {
                detachDissolve = 0f;
                detachDissolveDir = 1;
            }
            currentState = HandState.Channeling;
            controlledPos = altarPos;
            controlledCanHit = false;
            stateTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>仪式被破: 硬直坠落 (头颅破绽窗口)。</summary>
        public void StunHand(int duration) {
            currentState = HandState.Stunned;
            stunTimer = duration;
            controlledCanHit = false;
            detachDissolveDir = -1;
            stateTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>释放回体。</summary>
        public void ReleaseToIdle() {
            if (currentState == HandState.Idle || currentState == HandState.Retracting)
                return;
            currentState = HandState.Retracting;
            controlledCanHit = false;
            detachDissolveDir = -1;
            stateTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>由 Boss 在拍掌命中点注入径向泛光残留 (客户端表现)。</summary>
        public void FlagClapBloom(Vector2 worldPos) {
            clapBloomTimer = 10;
            clapBloomPos = worldPos;
        }

        // ================================================================
        //  ModNPC
        // ================================================================

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            var drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 80;
            NPC.defense = 40;
            NPC.lifeMax = 100000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 0f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)currentState);
            writer.Write(stateTimer);
            writer.WriteVector2(aimPoint);
            writer.WriteVector2(axisDir);
            writer.WriteVector2(startPos);
            writer.Write(sprayOnImpact);
            writer.Write(volleyWave);
            writer.WriteVector2(controlledPos);
            writer.Write(controlledCanHit);
            writer.Write(stunTimer);
            writer.Write(detachDissolve);
            writer.Write(detachDissolveDir);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            currentState = (HandState)reader.ReadInt32();
            stateTimer = reader.ReadInt32();
            aimPoint = reader.ReadVector2();
            axisDir = reader.ReadVector2();
            startPos = reader.ReadVector2();
            sprayOnImpact = reader.ReadBoolean();
            volleyWave = reader.ReadInt32();
            controlledPos = reader.ReadVector2();
            controlledCanHit = reader.ReadBoolean();
            stunTimer = reader.ReadInt32();
            detachDissolve = reader.ReadSingle();
            detachDissolveDir = reader.ReadInt32();
        }

        public override void AI() {
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.active || boss.ModNPC is not Corpses) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            Player target = Main.player[boss.target];
            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;

            if (flameSeed == 0f)
                flameSeed = NPC.whoAmI * 3.71f + 1.3f;

            stateTimer++;

            // 骨→魂→骨 溶解脉冲推进 (脱体/回体瞬间的魂态化)
            if (detachDissolveDir != 0) {
                detachDissolve = MathHelper.Clamp(detachDissolve + detachDissolveDir * 0.16f, 0f, 1f);
                if (detachDissolve >= 1f && detachDissolveDir > 0)
                    detachDissolveDir = -1;
                else if (detachDissolve <= 0f && detachDissolveDir < 0)
                    detachDissolveDir = 0;
            }
            if (clapBloomTimer > 0) clapBloomTimer--;
            if (impactRingTimer > 0) impactRingTimer--;

            // 兜底出口: 攻击态超时强制回位 (编排态由 Boss 管理, 豁免)
            if (stateTimer > 400 && currentState != HandState.Idle
                && currentState != HandState.Controlled && currentState != HandState.Channeling
                && currentState != HandState.Stunned && currentState != HandState.Dying) {
                ReleaseToIdle();
            }

            Vector2 prevCenter = NPC.Center;

            switch (currentState) {
                case HandState.Idle: TickIdle(boss); break;
                case HandState.Retracting: TickRetracting(boss); break;
                case HandState.PalmSlam: TickPalmSlam(boss); break;
                case HandState.BoneSweep: TickBoneSweep(boss); break;
                case HandState.BoneVolley: TickBoneVolley(boss); break;
                case HandState.ClapPincer: TickClapPincer(boss); break;
                case HandState.Controlled: TickControlled(target); break;
                case HandState.Channeling: TickChanneling(target); break;
                case HandState.Stunned: TickStunned(); break;
                case HandState.Materializing: TickMaterializing(); break;
                case HandState.Dying: TickDying(); break;
            }

            UpdateIKSystem(boss);
            UpdateTrail();

            // 位置直设状态: 位移折算为 velocity 并回退半步, 由引擎统一施加
            // (避免"直设位置 + 引擎再加 velocity"的双重位移; Stunned/Dying 本就是 velocity 驱动)
            if (currentState is not (HandState.Stunned or HandState.Dying)) {
                Vector2 moved = NPC.Center - prevCenter;
                NPC.position -= moved;
                NPC.velocity = moved;
            }

            // 距离栓绳 (脱体编排态豁免)
            if (currentState is HandState.Idle or HandState.Retracting or HandState.BoneVolley) {
                float distanceToBoss = Vector2.Distance(NPC.Center, boss.Center);
                if (distanceToBoss > 1300f)
                    NPC.Center = boss.Center + (NPC.Center - boss.Center).SafeNormalize(Vector2.Zero) * 1300f;
            }
        }

        // ================================================================
        //  招式实现
        // ================================================================

        private Vector2 RestPos(NPC boss) =>
            boss.Center + new Vector2(Direction * (150f + MathF.Sin(Main.GameUpdateCount * 0.02f + Direction) * 16f),
                                      -46f + MathF.Cos(Main.GameUpdateCount * 0.025f + Direction * 2f) * 13f);

        private void TickIdle(NPC boss) {
            // 待机呼吸浮动; 不自主攻击 (指挥权全在节拍表)
            NPC.Center += (RestPos(boss) - NPC.Center) * 0.12f;
            NPC.rotation = NPC.rotation.AngleLerp(Direction > 0 ? 0.35f : MathHelper.Pi - 0.35f, 0.08f);
        }

        private void TickRetracting(NPC boss) {
            Vector2 rest = RestPos(boss);
            NPC.Center += (rest - NPC.Center) * 0.2f;
            if (Vector2.Distance(NPC.Center, rest) < 36f || stateTimer > 40) {
                currentState = HandState.Idle;
                stateTimer = 0;
                volleyWave = 0;
            }
        }

        // —— 崩掌拍落 ——
        private void TickPalmSlam(NPC boss) {
            Vector2 hover = new(aimPoint.X, aimPoint.Y - SlamHoverHeight);
            int t = stateTimer;
            // 掌心朝下的姿态角: 左手绘制时补偿 +π, 此处预扣使两手视觉一致
            float palmDown = Direction > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2;

            if (t <= SlamHoist) {
                // 抬手蓄势: 慢而高, SineInOut 上举
                float p = ACMUtils.SineInOut(t / (float)SlamHoist);
                NPC.Center = Vector2.Lerp(startPos, hover, p);
                NPC.rotation = NPC.rotation.AngleLerp(palmDown, 0.15f); // 掌心朝下
                // 蓄势聚魂粒子 (向掌心收束)
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(70f, 70f);
                    var d = Dust.NewDustPerfect(NPC.Center + off, DustID.Shadowflame);
                    d.noGravity = true; d.scale = 1.3f;
                    d.velocity = -off * 0.09f;
                }
            }
            else if (t <= SlamDropStart) {
                // 顶点悬停: 末端反向再抬 (吸气), 粒子熄灭 = pre-silence
                float p = (t - SlamHoist) / (float)SlamHold;
                NPC.Center = hover - new Vector2(0f, MathF.Pow(p, 3f) * 30f);
                if (t == SlamDropStart - 8)
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.5f, Volume = 0.7f }, NPC.Center);
            }
            else if (t <= SlamDropStart + SlamDrop) {
                // 瞬拍: poly ease-out, 首帧即走完大半 (launch is a set)
                float p = (t - SlamDropStart) / (float)SlamDrop;
                float e = 1f - MathF.Pow(1f - p, 12f);
                NPC.Center = Vector2.Lerp(hover - new Vector2(0, 30f), aimPoint, e);
                NPC.rotation = palmDown;

                if (t == SlamDropStart + SlamDrop)
                    SlamImpact(boss);
            }
            else if (t <= SlamDropStart + SlamDrop + SlamLock) {
                // 落地锁定: 完全静止 (顿帧), 也是玩家的输出窗口
                NPC.Center = aimPoint;
            }
            else if (t <= SlamDropStart + SlamDrop + SlamLock + SlamRecover) {
                // 收招: 缓缓抬回
                float p = (t - SlamDropStart - SlamDrop - SlamLock) / (float)SlamRecover;
                NPC.Center = Vector2.Lerp(aimPoint, RestPos(boss), ACMUtils.QuadInOut(p));
            }
            else {
                currentState = HandState.Retracting;
                stateTimer = 0;
            }
        }

        private void SlamImpact(NPC boss) {
            impactRingTimer = 14;
            impactRingPos = aimPoint;
            ACMUtils.AddScreenShake(9f);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1.2f }, aimPoint);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.7f }, aimPoint);

            // 头颅受震反馈 (secondary motion)
            if (boss.ModNPC is Corpses head)
                head.NotifyImpactShake(3.2f);

            if (!Main.dedServ) {
                for (int i = 0; i < 26; i++) {
                    Vector2 vel = new(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-8f, -1.5f));
                    var d = Dust.NewDustPerfect(aimPoint + new Vector2(Main.rand.NextFloat(-40f, 40f), 8f), DustID.Bone, vel);
                    d.noGravity = Main.rand.NextBool();
                    d.scale = Main.rand.NextFloat(1.1f, 1.9f);
                }
            }

            // P2/P3: 落点向上溅射骨镖 (可读弧线, 服务器生成)
            if (sprayOnImpact && Main.netMode != NetmodeID.MultiplayerClient && boss.ModNPC is Corpses c) {
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = new(i * 2.6f, -9.5f + Math.Abs(i) * 0.8f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), aimPoint + new Vector2(0, -20f), vel,
                        ModContent.ProjectileType<CorpsesBoneShower>(), c.GetBossDamage(0.5f), 2f, Main.myPlayer, 0f, 1f);
                }
            }
        }

        // —— 白骨横扫 ——
        private void TickBoneSweep(NPC boss) {
            Vector2 lineStart = aimPoint - axisDir * SweepHalfLen;
            Vector2 lineEnd = aimPoint + axisDir * SweepHalfLen;
            Vector2 backPos = lineStart - axisDir * 130f - new Vector2(0f, 120f);
            int t = stateTimer;

            if (t <= SweepBack) {
                // 后摆: 抬到扫线起点侧后上方
                float p = ACMUtils.SineInOut(t / (float)SweepBack);
                NPC.Center = Vector2.Lerp(startPos, backPos, p);
                NPC.rotation = NPC.rotation.AngleLerp(axisDir.ToRotation(), 0.12f);
            }
            else if (t <= SweepStrikeStart) {
                // 静止蓄势 (扫线已锁定, 预警轴亮起)
                NPC.Center = backPos;
                if (t == SweepStrikeStart - 4)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
            }
            else if (t <= SweepStrikeStart + SweepStrike) {
                // 瞬扫: poly16 从起点掠到终点
                float p = (t - SweepStrikeStart) / (float)SweepStrike;
                float e = 1f - MathF.Pow(1f - p, 16f);
                NPC.Center = Vector2.Lerp(lineStart, lineEnd, e) - new Vector2(0f, MathF.Sin(p * MathHelper.Pi) * 26f);
                NPC.rotation = axisDir.ToRotation();

                if (!Main.dedServ && Main.rand.NextBool()) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(28f, 28f), DustID.Shadowflame);
                    d.noGravity = true; d.scale = 1.6f;
                    d.velocity = axisDir * Main.rand.NextFloat(3f, 8f);
                }
                if (t == SweepStrikeStart + SweepStrike && boss.ModNPC is Corpses head)
                    head.NotifyImpactShake(1.6f);
            }
            else if (t <= SweepStrikeStart + SweepStrike + SweepBrake) {
                // 硬刹 + 回摆
                float p = (t - SweepStrikeStart - SweepStrike) / (float)SweepBrake;
                NPC.Center = Vector2.Lerp(lineEnd, RestPos(boss), ACMUtils.QuadIn(p));
            }
            else {
                currentState = HandState.Retracting;
                stateTimer = 0;
            }
        }

        // —— 指骨连环 ——
        private void TickBoneVolley(NPC boss) {
            Vector2 aimDir = (aimPoint - boss.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = boss.Center + new Vector2(Direction * 170f, -80f) - aimDir * 40f;
            int t = stateTimer;

            if (t <= VolleyBack) {
                // 后摆聚焰
                float p = ACMUtils.QuadInOut(t / (float)VolleyBack);
                NPC.Center = Vector2.Lerp(startPos, anchor, p);
                NPC.rotation = NPC.rotation.AngleLerp(aimDir.ToRotation(), 0.15f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(46f, 46f);
                    var d = Dust.NewDustPerfect(NPC.Center + off, DustID.CursedTorch);
                    d.noGravity = true; d.scale = 1.1f;
                    d.velocity = -off * 0.1f;
                }
            }
            else if (t <= VolleyBack + VolleyWaves * VolleyWaveTime) {
                int wt = (t - VolleyBack - 1) % VolleyWaveTime;
                if (wt < 6) {
                    // 甩腕: 向目标方向捅出
                    float p = 1f - MathF.Pow(1f - wt / 6f, 6f);
                    NPC.Center = anchor + aimDir * p * 96f;
                }
                else {
                    // 后坐: 弹回
                    float p = (wt - 6) / (float)(VolleyWaveTime - 6);
                    NPC.Center = anchor + aimDir * (96f - ACMUtils.QuadOut(p) * 118f);
                }

                // 甩腕末帧发射扇形骨镖 (服务器)
                if (wt == 5 && volleyWave < VolleyWaves) {
                    volleyWave++;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.15f }, NPC.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient && boss.ModNPC is Corpses c) {
                        Player tgt = Main.player[NPC.target];
                        Vector2 dir = (tgt.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        for (int i = -2; i <= 2; i++) {
                            Vector2 vel = dir.RotatedBy(i * 0.16f) * 13.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 30f, vel,
                                ModContent.ProjectileType<CorpsesBoneShower>(), c.GetBossDamage(0.45f), 2f, Main.myPlayer, 0f, 0f);
                        }
                    }
                }
            }
            else if (t <= VolleyBack + VolleyWaves * VolleyWaveTime + 24) {
                float p = (t - VolleyBack - VolleyWaves * VolleyWaveTime) / 24f;
                NPC.Center = Vector2.Lerp(NPC.Center, RestPos(boss), p * 0.3f);
            }
            else {
                currentState = HandState.Retracting;
                stateTimer = 0;
            }
        }

        // —— 合掌夹击 ——
        private void TickClapPincer(NPC boss) {
            float side = Direction >= 0 ? 1f : -1f;
            Vector2 nearPos = aimPoint + axisDir * side * PincerNearDist;
            Vector2 farPos = aimPoint + axisDir * side * PincerFarDist;
            Vector2 meet = aimPoint + axisDir * side * 26f; // 手对手, 不完全重叠
            int t = stateTimer;

            if (t <= PincerFly) {
                float p = ACMUtils.SineInOut(t / (float)PincerFly);
                NPC.Center = Vector2.Lerp(startPos, nearPos, p);
                NPC.rotation = NPC.rotation.AngleLerp((aimPoint - NPC.Center).ToRotation(), 0.2f);
            }
            else if (t <= PincerFly + PincerPull) {
                // 反向拉开: t² 加速外撤 (ramped reverse, 吸气感)
                float p = (t - PincerFly) / (float)PincerPull;
                NPC.Center = Vector2.Lerp(nearPos, farPos, p * p);
                NPC.rotation = (aimPoint - NPC.Center).ToRotation();
            }
            else if (t <= PincerSnapStart) {
                // 静止蓄势 (轴线预警最亮, pre-silence)
                NPC.Center = farPos;
                if (t == PincerSnapStart - 5 && Direction > 0)
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f, Volume = 0.8f }, aimPoint);
            }
            else if (t <= PincerSnapStart + PincerSnap) {
                // 瞬合
                float p = (t - PincerSnapStart) / (float)PincerSnap;
                float e = 1f - MathF.Pow(1f - p, 14f);
                NPC.Center = Vector2.Lerp(farPos, meet, e);

                if (t == PincerSnapStart + PincerSnap && Direction > 0)
                    PincerImpact(boss);
            }
            else if (t <= PincerSnapStart + PincerSnap + PincerLock) {
                // 合击锁定 (顿帧)
                NPC.Center = meet;
            }
            else if (t <= PincerSnapStart + PincerSnap + PincerLock + PincerRecoil) {
                // 弹开 recoil
                float p = (t - PincerSnapStart - PincerSnap - PincerLock) / (float)PincerRecoil;
                NPC.Center = Vector2.Lerp(meet, aimPoint + axisDir * side * 200f - new Vector2(0, 40f), ACMUtils.QuadOut(p));
            }
            else {
                currentState = HandState.Retracting;
                stateTimer = 0;
            }
        }

        private void PincerImpact(NPC boss) {
            FlagClapBloom(aimPoint);
            impactRingTimer = 14;
            impactRingPos = aimPoint;
            ACMUtils.AddScreenShake(8f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.4f, Pitch = -0.1f }, aimPoint);

            if (boss.ModNPC is Corpses head)
                head.NotifyImpactShake(2.6f);

            if (!Main.dedServ) {
                for (int i = 0; i < 34; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(16f, 16f);
                    var d = Dust.NewDustPerfect(aimPoint, DustID.PurpleTorch, vel);
                    d.noGravity = true; d.scale = Main.rand.NextFloat(1.6f, 2.6f);
                }
            }

            // 环形冥掌冲击波: 从合击点向外 → 贴着合击点反而是安全芯
            if (Main.netMode != NetmodeID.MultiplayerClient && boss.ModNPC is Corpses c) {
                int count = 14;
                for (int i = 0; i < count; i++) {
                    float a = MathHelper.TwoPi * i / count;
                    Vector2 vel = a.ToRotationVector2() * 11.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), aimPoint, vel,
                        ModContent.ProjectileType<CorpsesClapWave>(), c.GetBossDamage(0.55f), 3f, Main.myPlayer, 0f, 0f);
                }
            }
        }

        // —— 编排态 (V2 保留) ——
        private void TickControlled(Player target) {
            NPC.Center += (controlledPos - NPC.Center) * 0.35f;
            Vector2 toTarget = target.Center - NPC.Center;
            if (toTarget.LengthSquared() > 1f)
                NPC.rotation = toTarget.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Shadowflame);
                d.noGravity = true; d.scale = 1.4f;
                d.velocity = NPC.velocity * 0.3f;
            }
        }

        private void TickChanneling(Player target) {
            float bob = MathF.Sin(stateTimer * 0.08f) * 10f;
            Vector2 anchor = controlledPos + new Vector2(0, bob);
            NPC.Center += (anchor - NPC.Center) * 0.2f;
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            if (!Main.dedServ && stateTimer % 3 == 0) {
                Vector2 off = Main.rand.NextVector2Circular(36f, 36f);
                var d = Dust.NewDustPerfect(NPC.Center + off, DustID.PurpleTorch);
                d.noGravity = true; d.scale = 1.6f;
                d.velocity = -off.SafeNormalize(Vector2.Zero) * 2.5f;
            }
        }

        private void TickStunned() {
            NPC.velocity.Y += 0.25f;
            NPC.velocity.X *= 0.96f;
            if (NPC.velocity.Y > 7f) NPC.velocity.Y = 7f;
            NPC.rotation += 0.05f * (Direction >= 0 ? 1 : -1);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Smoke);
                d.noGravity = true; d.scale = 1.6f;
            }

            stunTimer--;
            if (stunTimer <= 0) {
                currentState = HandState.Retracting;
                detachDissolveDir = -1;
                stateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        private void TickMaterializing() {
            // 尸雾中重凝: 位置原地, 轻微下沉浮出
            NPC.Center += new Vector2(0f, MathF.Sin(stateTimer * 0.12f) * 0.6f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 off = Main.rand.NextVector2Circular(50f, 50f);
                var d = Dust.NewDustPerfect(NPC.Center + off, DustID.Shadowflame);
                d.noGravity = true; d.scale = 1.4f;
                d.velocity = -off * 0.06f;
            }
            if (stateTimer > 34) {
                currentState = HandState.Idle;
                stateTimer = 0;
            }
        }

        private void TickDying() {
            // 坠地崩解: 加速下坠 + 翻转, 落到 Boss 记录的崩解高度即溶解消亡
            NPC.velocity.Y += 0.32f;
            if (NPC.velocity.Y > 11f) NPC.velocity.Y = 11f;
            NPC.velocity.X *= 0.97f;
            NPC.rotation += 0.07f * (Direction >= 0 ? 1 : -1);
            detachDissolve = MathHelper.Clamp(detachDissolve + 0.012f, 0f, 1f);
            detachDissolveDir = 0;

            if (!Main.dedServ) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(34f, 34f), DustID.Bone);
                d.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 3f));
                d.scale = Main.rand.NextFloat(1f, 1.7f);
            }

            // 落过地表或超时 → 崩解消亡
            bool grounded = Collision.SolidCollision(NPC.position + new Vector2(0, NPC.height * 0.5f), NPC.width, 24);
            if (grounded || stateTimer > 150) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 30; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, DustID.Bone, Main.rand.NextVector2Circular(8f, 6f));
                        d.scale = Main.rand.NextFloat(1.2f, 2f);
                    }
                }
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.4f }, NPC.Center);
                ACMUtils.AddScreenShake(6f);
                NPC.life = 0;
                NPC.active = false;
                NPC.netUpdate = true;
            }
        }

        // ================================================================
        //  IK / 拖尾 / 碰撞
        // ================================================================

        private void UpdateIKSystem(NPC boss) {
            shoulderPos = boss.Center + new Vector2(Direction * 60f, 30f);
            Vector2 targetPos = NPC.Center;
            Vector2 shoulderToHand = targetPos - shoulderPos;
            float distance = shoulderToHand.Length();

            if (distance > MaxReach) {
                targetPos = shoulderPos + shoulderToHand.SafeNormalize(Vector2.Zero) * MaxReach;
                distance = MaxReach;
            }

            if (distance > 1f) {
                float a = UpperArmLength;
                float baseAngle = shoulderToHand.ToRotation();
                float elbowAngle = baseAngle + MathHelper.PiOver2 * Direction;
                float elbowOffset = MathF.Sqrt(MathHelper.Max(0, a * a - (distance * 0.5f) * (distance * 0.5f)));
                Vector2 elbowDir = elbowAngle.ToRotationVector2();
                elbowPos = shoulderPos + (targetPos - shoulderPos) * 0.5f + elbowDir * elbowOffset * 0.5f;
            }
            else {
                elbowPos = shoulderPos;
            }

            handPos = targetPos;
        }

        private void UpdateTrail() {
            oldPositions.Add(NPC.Center);
            oldRotations.Add(NPC.rotation);
            if (oldPositions.Count > TrailLength) {
                oldPositions.RemoveAt(0);
                oldRotations.RemoveAt(0);
            }
        }

        public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox) {
            int hitboxSize = 60;
            npcHitbox = new Rectangle(
                (int)(NPC.Center.X - hitboxSize / 2),
                (int)(NPC.Center.Y - hitboxSize / 2),
                hitboxSize, hitboxSize);
            return true;
        }

        /// <summary>伤害窗口严格对齐爆发段视觉 (公平契约): 前摇/收招零伤害。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return currentState switch {
                HandState.PalmSlam => stateTimer > SlamDropStart && stateTimer <= SlamDropStart + SlamDrop + 8,
                HandState.BoneSweep => stateTimer > SweepStrikeStart && stateTimer <= SweepStrikeStart + SweepStrike,
                HandState.ClapPincer => stateTimer > PincerSnapStart && stateTimer <= PincerSnapStart + PincerSnap + 10,
                HandState.Controlled => controlledCanHit,
                _ => false
            };
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            // 地府身份层: 判官之手命中叠魂蚀; 合掌重击另记一笔冥律
            UnderworldField.AddSoulErosion(target, 2);
            if (currentState == HandState.ClapPincer || currentState == HandState.PalmSlam)
                UnderworldField.AddNetherDecree(target, 1);
        }

        public override bool CheckActive() => false;

        // ================================================================
        //  绘制
        // ================================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            Texture2D handTexture = TextureAssets.Npc[NPC.type].Value;
            Vector2 handOrigin = Direction > 0
                ? new Vector2(0, handTexture.Height / 2)
                : new Vector2(handTexture.Width, handTexture.Height / 2);

            DrawTelegraphs(spriteBatch);

            // 拍掌命中泛光残留
            if (clapBloomTimer > 0) {
                float bloomT = clapBloomTimer / 10f;
                ACMShaders.DrawRadialBloomAt(clapBloomPos, 0.16f * bloomT + 0.05f, bloomT,
                    new Color(180, 80, 255), 8f, 2.4f);
            }

            DrawArmOrSoulChain(spriteBatch, drawColor);

            // 爆发段残影 (速度门控: 只在瞬发帧出现, dressing 不常开)
            bool strikeAct = IsInStrikeAct();
            if (strikeAct) {
                float trailOpacity = 0.45f;
                for (int i = oldPositions.Count - 2; i >= 0; i -= 2) {
                    float progress = i / (float)oldPositions.Count;
                    Vector2 drawPos = oldPositions[i] - Main.screenPosition;
                    Color trailColor = TelegraphColors.GhostGreen with { A = 0 } * (trailOpacity * progress);
                    float rot = oldRotations[i] + (Direction > 0 ? 0 : MathHelper.Pi);
                    SpriteEffects fx = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    spriteBatch.Draw(handTexture, drawPos, null, trailColor, rot, handOrigin, NPC.scale * 0.92f, fx, 0);
                }
            }

            // 脱体编排态掌心魂焰
            if (currentState is HandState.Controlled or HandState.Channeling)
                Corpses.DrawSoulFlame(spriteBatch, NPC.Center, 0.9f, 0.8f, flameSeed);

            float rotation = NPC.rotation + (Direction > 0 ? 0 : MathHelper.Pi);
            SpriteEffects mainEffects = Direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color mainColor = drawColor;
            if (strikeAct)
                mainColor = Color.Lerp(drawColor, TelegraphColors.GhostGreen, 0.35f);
            if (currentState == HandState.Dying)
                mainColor = Color.Lerp(drawColor, new Color(60, 50, 70), detachDissolve * 0.6f);

            // 骨→魂→骨 溶解过渡 / 死亡崩解
            float dissolveAmount = detachDissolve;
            if (dissolveAmount > 0.02f && ACMShaders.DissolveBurn is Effect dissolve) {
                dissolve.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                dissolve.Parameters["uIntensity"]?.SetValue(1f);
                dissolve.Parameters["uThreshold"]?.SetValue(dissolveAmount * 0.85f);
                dissolve.Parameters["uEdgeWidth"]?.SetValue(0.12f);
                dissolve.Parameters["uNoiseScale"]?.SetValue(2.2f);
                dissolve.Parameters["uEdgeColor"]?.SetValue(new Color(180, 90, 255).ToVector4());
                dissolve.Parameters["uDirection"]?.SetValue(Vector2.Zero);
                dissolve.Parameters["uSweepStrength"]?.SetValue(0f);

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, dissolve, Main.GameViewMatrix.TransformationMatrix);
                spriteBatch.Draw(handTexture, NPC.Center - Main.screenPosition, null, mainColor, rotation, handOrigin, NPC.scale, mainEffects, 0);
                ACMShaders.RestoreDefaultBatch(spriteBatch);
            }
            else {
                spriteBatch.Draw(handTexture, NPC.Center - Main.screenPosition, null, mainColor, rotation, handOrigin, NPC.scale, mainEffects, 0);
            }

            return false;
        }

        private bool IsInStrikeAct() {
            return currentState switch {
                HandState.PalmSlam => stateTimer > SlamDropStart && stateTimer <= SlamDropStart + SlamDrop + 4,
                HandState.BoneSweep => stateTimer > SweepStrikeStart && stateTimer <= SweepStrikeStart + SweepStrike + 4,
                HandState.ClapPincer => stateTimer > PincerSnapStart && stateTimer <= PincerSnapStart + PincerSnap + 6,
                _ => false
            };
        }

        // 预警 decal: 落点光柱 / 扫掠轴线 / 合掌轴线 / 冲击环 (CorpsesBoneRing, 不占全屏名额)
        private void DrawTelegraphs(SpriteBatch sb) {
            // 崩掌拍落: 抬手期落点光柱渐强 (红=致命)
            if (currentState == HandState.PalmSlam && stateTimer <= SlamDropStart + SlamDrop) {
                float prog = MathHelper.Clamp(stateTimer / (float)SlamDropStart, 0f, 1f);
                Corpses.DrawBoneRingDecal(sb, 0, aimPoint, 52f, 0.85f, prog,
                    Vector2.UnitX, 0f, TelegraphColors.Lethal, TelegraphColors.NetherViolet);
            }

            // 白骨横扫: 后摆期轴线束
            if (currentState == HandState.BoneSweep && stateTimer <= SweepStrikeStart + SweepStrike) {
                float prog = MathHelper.Clamp(stateTimer / (float)SweepStrikeStart, 0f, 1f);
                Corpses.DrawBoneRingDecal(sb, 2, aimPoint, 42f, 0.8f, prog,
                    axisDir, SweepHalfLen + 80f, TelegraphColors.Lethal, TelegraphColors.NetherViolet);
            }

            // 合掌夹击: 拉开期轴线束 (仅右手绘制, 避免双份)
            if (currentState == HandState.ClapPincer && Direction > 0
                && stateTimer > PincerFly / 2 && stateTimer <= PincerSnapStart + PincerSnap) {
                float prog = MathHelper.Clamp((stateTimer - PincerFly / 2) / (float)(PincerSnapStart - PincerFly / 2), 0f, 1f);
                Corpses.DrawBoneRingDecal(sb, 2, aimPoint, 46f, 0.85f, prog,
                    axisDir, PincerFarDist + 110f, TelegraphColors.Lethal, TelegraphColors.NetherViolet);
            }

            // 冲击环残留
            if (impactRingTimer > 0) {
                float p = 1f - impactRingTimer / 14f;
                Corpses.DrawBoneRingDecal(sb, 1, impactRingPos, 340f, 1f, p,
                    Vector2.UnitX, 0f, new Color(225, 240, 220), TelegraphColors.GhostGreen);
            }
        }

        // 臂绘制: 近距画 IK 骨臂, 超距化为魂链光束 (脱体幽手)
        private void DrawArmOrSoulChain(SpriteBatch sb, Color drawColor) {
            if (currentState == HandState.Dying)
                return; // 死亡崩解: 臂已断

            float dist = Vector2.Distance(shoulderPos, NPC.Center);
            if (dist <= MaxReach + 40f && CorpsesArm != null) {
                DrawArmSegment(sb, shoulderPos, elbowPos, CorpsesArm, drawColor, 1.0f);
                DrawArmSegment(sb, elbowPos, handPos, CorpsesArm, drawColor, 0.9f);
            }
            else {
                // 魂链: 肩部残臂短段 + 鬼绿锁链光束
                if (CorpsesArm != null) {
                    Vector2 stumpEnd = shoulderPos + (NPC.Center - shoulderPos).SafeNormalize(Vector2.UnitX) * 60f;
                    DrawArmSegment(sb, shoulderPos, stumpEnd, CorpsesArm, drawColor, 1.0f);
                }
                float fade = MathHelper.Clamp(1.6f - dist / 1400f, 0.35f, 1f);
                ACMShaders.DrawBeam(shoulderPos, NPC.Center, 7f,
                    TelegraphColors.GhostGreen, TelegraphColors.NetherViolet, 0.55f * fade, 1.8f, 2.6f);
            }
        }

        private void DrawArmSegment(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Texture2D armTexture, Color color, float scale = 1f) {
            Vector2 diff = end - start;
            float rotation = diff.ToRotation();
            float length = diff.Length();
            if (length < 4f)
                return;

            float armSegmentLength = armTexture.Height * scale;
            int segmentCount = (int)Math.Ceiling(length / armSegmentLength);

            for (int i = 0; i < segmentCount; i++) {
                float progress = i / (float)segmentCount;
                Vector2 segmentPos = Vector2.Lerp(start, end, progress);
                float segmentLength = Math.Min(armSegmentLength, length - i * armSegmentLength);
                float lengthScale = segmentLength / armTexture.Height;

                Vector2 origin = new(armTexture.Width * 0.5f, armTexture.Height);
                Vector2 drawScale = new(scale, lengthScale * scale);

                spriteBatch.Draw(armTexture, segmentPos - Main.screenPosition, null, color,
                    rotation + MathHelper.PiOver2, origin, drawScale, SpriteEffects.None, 0);
            }
        }
    }
}

using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.NetherKitsunes.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥妖狐 Boss —— V3「雾祟」全面重做。
    ///
    /// 主题: 你在她的雾里, 而不是她在你的场里。雾的浓淡呼吸即攻击节拍 (浓=蓄势, 骤清=爆发);
    /// 雾隐→狐眼 telegraph→瞳缩倒计时→扑袭是核心机制; 动作语言为僵-爆-僵的鬼怪节奏。
    ///
    /// 阶段: Intro雾先至 → P1游祟(100~60%) → 雾葬 → P2雾狩(60~30%) → 怨决 → P3怨主(30~0)
    /// → 死亡《九火归寂》(CheckDead 拦截完整死亡弧, 回光返照暖金呼应生前九尾狐)。
    ///
    /// 出招: 每阶段手写循环表 (机动压制/找真身/zoning 严格相间); 全部服务器掷骰量经 SendExtraAI 同步,
    /// 视觉 (尾巴/狐眼/雾) 由同步状态确定性推导 — 多人观感一致。
    /// </summary>
    [AutoloadBossHead]
    internal class NetherKitsune : ModNPC
    {
        [VaultLoaden("{@namespace}/")]
        public static Texture2D NetherMissesBody;
        [VaultLoaden("{@namespace}/")]
        public static Texture2D NetherMissesTop;

        #region 常量定义

        /// <summary>尾巴数量</summary>
        public const int TailCount = 9;

        /// <summary>二阶段血量阈值 (雾葬)</summary>
        public const float Phase2Threshold = 0.60f;

        /// <summary>三阶段血量阈值 (怨决)</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>百鬼夜行解锁阈值</summary>
        public const float NightParadeThreshold = 0.20f;

        /// <summary>距离栓绳: 超过此距离强制向内偏置 (防飞屏绕圈)</summary>
        private const float LeashDistance = 1400f;

        /// <summary>死亡演出总长</summary>
        private const int DeathDuration = 335;

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,              // 雾先至
            P1Hub,              // P1 悬停/connector
            P1TailArpeggio,     // 尾击琶音
            P1FoxfireBreath,    // 狐火吐息
            P1PincerStab,       // 双尾钳击
            P1VoidStrike,       // 虚空九刺
            P1PhantomSlam,      // 幻影下砸
            Transition2,        // 雾葬
            P2Hub,
            P2MistAmbush,       // 雾隐扑袭 (核心招; P3 复用快速版)
            P2GhostSweeps,      // 鬼影三掠
            P2MirrorMist,       // 镜雾九影
            P2VoidStrike,       // 九刺·雾
            Transition3,        // 怨决
            P3Hub,
            P3Possession,       // 虚实九影
            P3NightParade,      // 百鬼夜行 (≤20% set-piece)
            Death               // 九火归寂
        }

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        /// <summary>九条幽冥尾巴</summary>
        public NetherKitsuneTail[] Tails { get; private set; }

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // ===== 同步状态 (服务器掷骰 → SendExtraAI) =====
        private int seed;
        private Random random;
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private int attackTableIndex;      // 循环表游标
        private float rolledAngle;         // 通用角度骰 (钳击/镜雾环相位/终扑方向)
        private int rolledIndex;           // 通用索引骰 (真身/真车道)
        private Vector2 anchorPos;         // 通用锚点 (扑袭眼位/镜雾中心/夜行中心)
        private float dashDirection;       // 冲刺方向

        // ===== 本地推导状态 (确定性/纯视觉, 不同步) =====
        private float globalTime;
        private int voidStrikeRepeatCount;
        private int pincerRound;
        private int sweepCount;
        private int possessionBeat;
        private float mistAim = 0.35f;      // 本帧雾目标密度
        private float mistFreeze = 0f;      // 雾冻结 (死亡)
        private float dissolveAmount = 0f;  // 0=实体 1=全溶散 (雾隐)
        private float deathGold = 0f;       // 死亡回光返照暖金
        private float ghostFlicker = 1f;
        private float connectorDroop = 0f;  // 尾巴垂落系数 (connector/喘息)
        private bool contactEnabled = true; // 接触伤害总开关 (演出期关)

        // 演出标量 (衰减式, 经 FogSystem 发布绘制)
        private float soulBloom = 0f;
        private Color soulBloomColor = new Color(130, 210, 255);
        private float runicTelegraph = 0f;
        private Vector2 runicCenter;
        private float runicRadius = 360f;
        private bool runicLethal = false;

        // 幻影系统 (P3 虚实九影)
        private int phantomCount;
        private float[] phantomAlpha;
        private Vector2[] phantomPositions;
        private float[] phantomRotations;

        // 尾尖魂焰批 (每帧收集重建, 复用列表避免分配)
        private readonly List<NetherKitsuneFogSystem.SoulflameSpec> tipFlames = new(TailCount + 2);

        // 尾扇点燃级别 (入场/怨决/死亡演出)
        private readonly float[] fanIgnite = new float[TailCount];

        #endregion

        #region 攻击循环表

        // 手写循环表: 机动压制 ↔ zoning ↔ 找真身负荷招严格相间 (PACING §2)
        private static readonly BossPhase[] TableP1 = {
            BossPhase.P1TailArpeggio, BossPhase.P1FoxfireBreath, BossPhase.P1PincerStab,
            BossPhase.P1VoidStrike, BossPhase.P1PhantomSlam, BossPhase.P1FoxfireBreath,
        };

        private static readonly BossPhase[] TableP2 = {
            BossPhase.P2MistAmbush, BossPhase.P2GhostSweeps, BossPhase.P2VoidStrike,
            BossPhase.P2MirrorMist, BossPhase.P2GhostSweeps, BossPhase.P1FoxfireBreath,
        };

        private static readonly BossPhase[] TableP3 = {
            BossPhase.P3Possession, BossPhase.P3NightParade, BossPhase.P3Possession,
            BossPhase.P2MistAmbush,
        };

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 130;
            NPC.height = 130;
            NPC.damage = 110;
            NPC.defense = 65;
            NPC.lifeMax = 180000; // 地府Boss强度
            NPC.HitSound = SoundID.NPCHit54; // 幽灵音效
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = Item.buyPrice(0, 25, 0, 0);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 15f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherKyuubiBook>()));
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            InitializeTails();

            phantomAlpha = new float[5];
            phantomPositions = new Vector2[5];
            phantomRotations = new float[5];

            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            dissolveAmount = 1f; // 从雾中凝聚

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(attackTableIndex);
            writer.Write(rolledAngle);
            writer.Write(rolledIndex);
            writer.WriteVector2(anchorPos);
            writer.Write(dashDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            attackTableIndex = reader.ReadInt32();
            rolledAngle = reader.ReadSingle();
            rolledIndex = reader.ReadInt32();
            anchorPos = reader.ReadVector2();
            dashDirection = reader.ReadSingle();

            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        public override void OnKill() {
            NetherKitsuneFogSystem.Deactivate();
        }

        /// <summary>接触伤害窗口与视觉严格对齐: 溶散半透期 / 演出期不咬人。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return contactEnabled && dissolveAmount < 0.4f;
        }

        /// <summary>死亡拦截 → 《九火归寂》完整死亡弧, 播完才真死。</summary>
        public override bool CheckDead() {
            if (Phase != BossPhase.Death) {
                TransitionTo(BossPhase.Death);
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                ClearOwnedProjectiles();
                NPC.netUpdate = true;
                return false;
            }
            return PhaseTimer >= DeathDuration - 2;
        }

        #endregion

        #region AI主循环

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            if (!NetherKitsuneFogSystem.IsActive) {
                NetherKitsuneFogSystem.Activate(NPC.whoAmI);
            }

            random ??= new Random(seed);
            globalTime += 1f / 60f;
            ghostFlicker = 0.85f + 0.15f * MathF.Sin(globalTime * 4f);

            if (Tails == null) {
                InitializeTails();
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if ((!target.active || target.dead) && Phase != BossPhase.Death) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 幽冥消散 (死亡演出期间不despawn — 掉落不可回退)
                    NPC.velocity.Y -= 0.3f;
                    NPC.alpha += 3;
                    if (NPC.alpha >= 255) {
                        NPC.active = false;
                        NetherKitsuneFogSystem.Deactivate();
                    }
                    return;
                }
            }

            // 每帧默认: 可受击、可接触; 各状态显式覆盖 (保底出口原则)
            contactEnabled = true;
            mistFreeze = 0f;
            connectorDroop = MathF.Max(0f, connectorDroop - 0.05f);

            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;

            // 阶段基准雾密度 (状态内可覆盖)
            mistAim = didPhase3Transition ? 0.6f : (didPhase2Transition ? 0.5f : 0.35f);

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.P1Hub: RunHub(target, TableP1); break;
                case BossPhase.P1TailArpeggio: RunTailArpeggio(target); break;
                case BossPhase.P1FoxfireBreath: RunFoxfireBreath(target); break;
                case BossPhase.P1PincerStab: RunPincerStab(target); break;
                case BossPhase.P1VoidStrike: RunVoidStrike(target, false); break;
                case BossPhase.P1PhantomSlam: RunPhantomSlam(target); break;
                case BossPhase.Transition2: RunTransition2(target); break;
                case BossPhase.P2Hub: RunHub(target, TableP2); break;
                case BossPhase.P2MistAmbush: RunMistAmbush(target); break;
                case BossPhase.P2GhostSweeps: RunGhostSweeps(target); break;
                case BossPhase.P2MirrorMist: RunMirrorMist(target); break;
                case BossPhase.P2VoidStrike: RunVoidStrike(target, true); break;
                case BossPhase.Transition3: RunTransition3(target); break;
                case BossPhase.P3Hub: RunHub(target, TableP3); break;
                case BossPhase.P3Possession: RunPossession(target); break;
                case BossPhase.P3NightParade: RunNightParade(target); break;
                case BossPhase.Death: RunDeath(target); break;
            }

            // 溶散期无敌 (伤害窗=视觉窗); 死亡演出恒无敌
            NPC.dontTakeDamage = dissolveAmount > 0.55f || Phase == BossPhase.Death;

            UpdateAllTails();

            // 演出标量衰减 + 发布
            if (soulBloom > 0f) soulBloom = MathF.Max(0f, soulBloom - 0.03f);
            if (runicTelegraph > 0f) runicTelegraph = MathF.Max(0f, runicTelegraph - 0.02f);
            NetherKitsuneFogSystem.PublishBloom(NPC.Center, soulBloom, soulBloomColor);
            NetherKitsuneFogSystem.PublishRunic(runicCenter, runicRadius, runicTelegraph, runicLethal);
            NetherKitsuneFogSystem.PublishMist(mistAim, didPhase3Transition ? 1f : 0f, mistFreeze, NPC.velocity * 0.02f);

            // 幽蓝光照 (P3 偏鬼绿)
            Vector3 lightC = Vector3.Lerp(new Vector3(0.3f, 0.5f, 0.8f), new Vector3(0.3f, 0.75f, 0.5f), didPhase3Transition ? 1f : 0f);
            Lighting.AddLight(NPC.Center, lightC * (0.6f + mistAim * 0.4f) * (1f - dissolveAmount));
        }

        /// <summary>触发一次魂火泛光。</summary>
        private void TriggerBloom(float strength, Color color) {
            soulBloom = MathF.Max(soulBloom, strength);
            soulBloomColor = color;
        }

        /// <summary>触发一次法阵预警 (世界点 + 世界半径 + 强度 + 是否致命转红)。</summary>
        private void TriggerRunic(Vector2 center, float worldRadius, float strength, bool lethal) {
            runicCenter = center;
            runicRadius = worldRadius;
            runicTelegraph = MathF.Max(runicTelegraph, strength);
            runicLethal = lethal;
        }

        private void InitializeTails() {
            Tails = new NetherKitsuneTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new NetherKitsuneTail(i);
                Tails[i].Initialize(GetTailRootPosition(i), GetTailBaseAngle(i));
            }
        }

        private static float GetTailBaseAngle(int tailIndex) {
            float angleRange = MathHelper.Pi;
            float startAngle = -MathHelper.Pi * 0.75f;
            return startAngle + angleRange * tailIndex / (TailCount - 1);
        }

        private Vector2 GetTailRootPosition(int tailIndex) {
            float angle = GetTailBaseAngle(tailIndex);
            Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 40f;
            return NPC.Center + offset;
        }

        /// <summary>九尾孔雀屏角度 (上半扇形)。</summary>
        private static float GetFanAngle(int tailIndex) {
            return -MathHelper.PiOver2 + (tailIndex - 4) / 4f * (MathHelper.Pi * 0.42f);
        }

        private void UpdateAllTails() {
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i] == null) continue;

                Vector2 rootPos = GetTailRootPosition(i);
                float baseAngle = GetTailBaseAngle(i);

                if (NPC.velocity.LengthSquared() > 1f) {
                    float velocityAngle = NPC.velocity.ToRotation();
                    float oppositeAngle = velocityAngle + MathHelper.Pi;
                    float spreadOffset = (i - 4) / 4f * MathHelper.PiOver4;
                    baseAngle = MathHelper.Lerp(baseAngle, oppositeAngle + spreadOffset, 0.35f);
                }

                // 幽冥尾巴飘逸摆动
                float swayOffset = MathF.Sin(globalTime * 2.5f + i * 0.8f) * 0.12f;
                baseAngle += swayOffset;

                Tails[i].Droop = connectorDroop;
                Tails[i].Update(rootPos, baseAngle, NPC.velocity, globalTime);
            }
        }

        private void CheckPhaseTransition() {
            bool inCinematic = Phase is BossPhase.Intro or BossPhase.Transition2 or BossPhase.Transition3 or BossPhase.Death;
            if (inCinematic)
                return;

            if (!didPhase2Transition && IsPhase2) {
                didPhase2Transition = true;
                BeginCinematic(BossPhase.Transition2);
            }
            else if (didPhase2Transition && !didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                BeginCinematic(BossPhase.Transition3);
            }
        }

        /// <summary>进入演出节拍: 清弹 + 收尾 + 清眼。</summary>
        private void BeginCinematic(BossPhase phase) {
            ClearOwnedProjectiles();
            CancelAllTails();
            NetherKitsuneFogSystem.ClearEyes();
            TransitionTo(phase);
        }

        private void CancelAllTails() {
            if (Tails == null) return;
            for (int i = 0; i < TailCount; i++)
                Tails[i]?.CancelAttack();
        }

        /// <summary>清除本 Boss 的敌意弹幕 (换阶段公平阀门; 不动其他 Boss 的弹)。</summary>
        private void ClearOwnedProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int foxfire = ModContent.ProjectileType<NetherFoxfireSoul>();
            int patch = ModContent.ProjectileType<NetherGhostflamePatch>();
            int strike = ModContent.ProjectileType<NetherTailStrike>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.hostile && (p.type == foxfire || p.type == patch || p.type == strike))
                    p.Kill();
            }
        }

        /// <summary>
        /// 生成尾击判定线 (服务器权威伤害载体, 伤害窗=延迟后 7f, 与尾巴 poly12 爆发段对齐)。
        /// </summary>
        private void SpawnTailStrike(Vector2 from, float angle, float length, int delayFrames, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, Vector2.Zero,
                ModContent.ProjectileType<NetherTailStrike>(), damage, 2f, Main.myPlayer,
                angle, length, delayFrames);
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            // 招内计数器清零 (防中断残留污染下一招)
            voidStrikeRepeatCount = 0;
            pincerRound = 0;
            sweepCount = 0;
            possessionBeat = 0;
            NPC.netUpdate = true;
        }

        /// <summary>距离栓绳: 飞出栓绳距离时向内硬偏置。</summary>
        private void ApplyLeash(Player target) {
            float dist = NPC.Distance(target.Center);
            if (dist > LeashDistance) {
                Vector2 inward = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 900f;
                NPC.Center = Vector2.Lerp(NPC.Center, inward, 0.05f);
            }
        }

        /// <summary>悬停追随 (幽魂漂浮)。</summary>
        private void HoverAround(Player target, Vector2 offset, float lerpV, float approach) {
            Vector2 hoverPos = target.Center + offset;
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * approach, lerpV);
        }

        #endregion

        #region 入场《雾先至》

        private void RunIntro(Player target) {
            contactEnabled = false;
            mistAim = MathHelper.Lerp(0.1f, 0.6f, MathHelper.Clamp(PhaseTimer / 60f, 0f, 1f));

            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -380);
                NPC.velocity = Vector2.Zero;
                dissolveAmount = 1f;
                SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = -0.6f, Volume = 0.8f }, target.Center);
            }

            NPC.velocity *= 0.9f;

            // 60~132f: 九点鬼火自左及右渐次点燃 (音阶下行) — 尾巴展屏承载
            if (PhaseTimer == 60) {
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].StartFanDisplay(GetFanAngle(i), 3.2f);
                    fanIgnite[i] = 0f;
                }
            }
            if (PhaseTimer > 60 && PhaseTimer <= 132) {
                int lit = (int)((PhaseTimer - 60) / 8f);
                for (int i = 0; i < TailCount && i <= lit; i++) {
                    if (fanIgnite[i] <= 0f && i == lit) {
                        fanIgnite[i] = 0.01f;
                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.5f - i * 0.1f, Volume = 0.7f }, NPC.Center);
                    }
                }
                for (int i = 0; i < TailCount; i++) {
                    if (fanIgnite[i] > 0f)
                        fanIgnite[i] = MathF.Min(1f, fanIgnite[i] + 0.08f);
                    Tails[i].SetFanIgnite(fanIgnite[i]);
                }
            }

            // 132~182f: 狐身自雾凝聚 (反向溶解)
            if (PhaseTimer > 132 && PhaseTimer <= 182) {
                dissolveAmount = 1f - ACMUtils.SineInOut((PhaseTimer - 132) / 50f);
                for (int i = 0; i < TailCount; i++)
                    Tails[i].SetFanIgnite(fanIgnite[i]);

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }

            // 182~237f: 凝视静止 (menace is stillness — 只有眼睛与尾火亮着)
            if (PhaseTimer > 182 && PhaseTimer < 237) {
                dissolveAmount = 0f;
                for (int i = 0; i < TailCount; i++)
                    Tails[i].SetFanIgnite(fanIgnite[i] * (0.7f + 0.3f * MathF.Sin(globalTime * 5f + i)));
            }

            // 237f: 尾扇炸开 + 怨啸 — 战斗开始
            if (PhaseTimer == 237) {
                CancelAllTails();
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(0.9f, new Color(150, 200, 255));
                NetherKitsuneFogSystem.MistPulseAdd(-0.3f); // 呼气骤清
                NetherKitsuneFogSystem.CreateRipple(NPC.Center, 2f);
            }

            if (PhaseTimer > 245) {
                dissolveAmount = 0f;
                TransitionTo(BossPhase.P1Hub);
            }
        }

        #endregion

        #region Hub / 循环表

        /// <summary>攻击间 connector: 尾巴垂落喘息 25f (段落停顿) → 循环表点招。</summary>
        private void RunHub(Player target, BossPhase[] table) {
            HoverAround(target, new Vector2(MathF.Sin(globalTime * 1.2f) * 110f, -360f + MathF.Cos(globalTime * 0.8f) * 40f), 0.08f, 0.02f);
            ApplyLeash(target);

            if (PhaseTimer < 25)
                connectorDroop = 1f; // 垂尾喘息

            if (PhaseTimer >= 40) {
                BossPhase next = table[attackTableIndex % table.Length];
                attackTableIndex++;

                // 百鬼夜行押后: 血量未到 20% 时该槽换九刺·雾
                if (next == BossPhase.P3NightParade && NPC.life > NPC.lifeMax * NightParadeThreshold)
                    next = BossPhase.P2VoidStrike;

                TransitionTo(next);
            }
        }

        #endregion

        #region P1 招式

        /// <summary>T1 尾击琶音: 九尾自左向右每 5f 一条僵-爆-僵刺击, 每刺一次身体反冲。</summary>
        private void RunTailArpeggio(Player target) {
            HoverAround(target, new Vector2(0, -340), 0.06f, 0.02f);
            ApplyLeash(target);

            int idx = (int)(PhaseTimer / 5) - 1;
            if (PhaseTimer % 5 == 0 && idx >= 0 && idx < TailCount && !Tails[idx].IsAttacking) {
                Vector2 lead = target.Center + target.velocity * 8f;
                Tails[idx].StartGhostStabAttack(lead, 0.62f);

                // 判定线: 0.62s * 44% ≈ 16f 后进入爆发窗
                Vector2 root = GetTailRootPosition(idx);
                Vector2 toLead = lead - root;
                float reach = MathF.Min(toLead.Length() + 90f, Tails[idx].TotalLength * 1.05f);
                SpawnTailStrike(root, toLead.ToRotation(), reach, 16, NPC.damage / 3);
            }

            // 尾巴爆发瞬间的身体反冲 (mass is reaction)
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i].InStrikeWindow) {
                    NPC.velocity -= Tails[i].GetTipDirection() * 0.9f;
                }
            }

            if (PhaseTimer > TailCount * 5 + 45) {
                TransitionTo(CurrentHub());
            }
        }

        /// <summary>T2 狐火吐息: 聚火 (converging + 72% 截止 + 末段静默) → 三波扇形狐火 + 后坐。</summary>
        private void RunFoxfireBreath(Player target) {
            switch ((int)SubState) {
                case 0: // 前摇 50f: 悬停急停 + 聚火
                    NPC.velocity *= 0.9f;
                    mistAim += 0.25f;

                    if (PhaseTimer == 1) {
                        NetherKitsuneFogSystem.MistPulseAdd(0.2f); // 蓄势涌浓
                        SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.4f, Volume = 1.1f }, NPC.Center);
                    }

                    // 聚火粒子: 密度 ∝ sqrt(t), 72% 处截止 → 最后的死寂 (charge-up grammar)
                    float chargeT = PhaseTimer / 50f;
                    if (Main.netMode != NetmodeID.Server && chargeT < 0.72f) {
                        Vector2 mouth = NPC.Center + (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 55f;
                        int count = (int)(MathF.Sqrt(chargeT) * 4f);
                        for (int i = 0; i < count; i++) {
                            Vector2 dustPos = mouth + Main.rand.NextVector2CircularEdge(180f, 180f) * Main.rand.NextFloat(0.6f, 1f);
                            Dust d = Dust.NewDustPerfect(dustPos, DustID.BlueTorch, (mouth - dustPos) * 0.085f, 120, default, 1.6f);
                            d.noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 爆发: 3 波扇形狐火, 波间 18f, 每波后坐
                    mistAim += 0.1f;
                    if (PhaseTimer % 18 == 1 && PhaseTimer < 54) {
                        int wave = (int)(PhaseTimer / 18);
                        Vector2 aim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int perWave = Main.expertMode ? 7 : 5;
                            float speed = 8.5f + wave * 1.75f;
                            for (int i = 0; i < perWave; i++) {
                                float off = MathHelper.Lerp(-0.61f, 0.61f, perWave <= 1 ? 0.5f : i / (float)(perWave - 1));
                                // variant 3 = 吐息狐火 (熄灭时留怨火地灾)
                                SpawnFoxfireSoul(NPC.Center + aim * 50f, aim.RotatedBy(off) * speed, NPC.damage / 3, 3);
                            }
                        }

                        NPC.velocity -= aim * 4f; // 后坐
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(4f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.1f); // 每波呼气
                        TriggerBloom(0.55f, new Color(130, 210, 255));
                    }

                    NPC.velocity *= 0.95f;
                    if (PhaseTimer > 60) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 收招 25f
                    NPC.velocity *= 0.94f;
                    if (PhaseTimer > 25)
                        TransitionTo(CurrentHub());
                    break;
            }
        }

        /// <summary>T1' 双尾钳击: 两条对侧尾飞至玩家两翼悬停亮尖 → 同帧相向合刺 (钳形)。</summary>
        private void RunPincerStab(Player target) {
            const int roundLen = 68; // 54f 攻击 + 14f 间隔

            if (PhaseTimer == 1 && SubState == 0) {
                pincerRound = 0;
                SubState = 1;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    rolledAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    NPC.netUpdate = true;
                }
            }

            HoverAround(target, new Vector2(0, -330), 0.05f, 0.018f);
            ApplyLeash(target);

            // 每轮自 f4 起 (给掷骰包到达余量)
            float roundTimer = PhaseTimer - 4 - pincerRound * roundLen;
            if (roundTimer == 1) {
                float ang = rolledAngle + pincerRound * MathHelper.PiOver2;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 hoverA = target.Center + dir * 340f;
                Vector2 hoverB = target.Center - dir * 340f;
                (int a, int b) = PincerPair(pincerRound);
                // 相向穿过身位 (strikeThrough = 对侧悬停点)
                Tails[a].StartPincerStabAttack(hoverA, hoverB, 0.9f);
                Tails[b].StartPincerStabAttack(hoverB, hoverA, 0.9f);
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.4f, Volume = 0.9f }, target.Center);

                // 判定线: 0.9s * 58% ≈ 31f 后合刺 (两条相向线)
                float span = Vector2.Distance(hoverA, hoverB);
                SpawnTailStrike(hoverA, (hoverB - hoverA).ToRotation(), span, 31, NPC.damage / 3);
                SpawnTailStrike(hoverB, (hoverA - hoverB).ToRotation(), span, 31, NPC.damage / 3);
            }

            // 合刺瞬间音效/涟漪 (54f 攻击中 burst 段 ≈ 0.58*54 ≈ 31f)
            if (roundTimer == 33) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 1.1f }, target.Center);
                ACMScreenShakeSystem.Add(4f);
                NetherKitsuneFogSystem.CreateRipple(target.Center, 1.2f);
            }

            if (roundTimer >= roundLen) {
                pincerRound++;
                int maxRounds = Main.expertMode ? 3 : 2;
                if (pincerRound >= maxRounds)
                    TransitionTo(CurrentHub());
            }
        }

        private static (int, int) PincerPair(int round) => (round % 3) switch {
            0 => (0, 8),
            1 => (2, 6),
            _ => (1, 7),
        };

        /// <summary>T3/T7 虚空九刺: 收拢 (末段死寂) → 法阵转红 → 九向同刺; enhanced=九刺·雾 (P2+, 留怨火)。</summary>
        private void RunVoidStrike(Player target, bool enhanced) {
            int telegraphLen = enhanced ? 30 : 42;
            int maxRepeats = enhanced ? 3 : (Main.expertMode ? 3 : 2);

            switch ((int)SubState) {
                case 0:
                    voidStrikeRepeatCount = 0;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        rolledAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        NPC.netUpdate = true;
                    }
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.velocity *= 0.4f;
                    break;

                case 1: // 预备 (尾巴收拢, 末 20% 死寂在尾巴曲线内)
                    NPC.velocity *= 0.92f;
                    Vector2 hoverPos = target.Center + new Vector2(0, enhanced ? -300 : -350);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.025f);

                    // f3 起手 (给 rolledAngle 网络包 2f 到达余量, 远端首轮方向不错位)
                    if (PhaseTimer == 3) {
                        float telegraphSec = telegraphLen / 60f;
                        for (int i = 0; i < TailCount; i++) {
                            float angle = rolledAngle + MathHelper.TwoPi * i / TailCount;
                            Tails[i].StartVoidPierceAttack(angle.ToRotationVector2(), telegraphSec, 0.1f, enhanced ? 0.33f : 0.45f);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = enhanced ? 0.5f : 0.3f }, NPC.Center);
                        TriggerRunic(NPC.Center, enhanced ? 440f : 420f, 0.85f, false);
                        NetherKitsuneFogSystem.MistPulseAdd(0.12f);
                    }

                    if (PhaseTimer > telegraphLen + 2) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 刺出
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(5f);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 1.5f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.18f);
                        TriggerBloom(0.85f, new Color(130, 210, 255));
                        TriggerRunic(NPC.Center, enhanced ? 440f : 420f, 1f, true); // 刺出瞬间转红致命

                        // 九向判定线 (即刻起爆, 与穿刺帧对齐)
                        float pierceReach = NetherKitsuneTail.JointCount * NetherKitsuneTail.BaseSegmentLength * 4.1f;
                        for (int i = 0; i < TailCount; i++) {
                            float angle = rolledAngle + MathHelper.TwoPi * i / TailCount;
                            SpawnTailStrike(NPC.Center, angle, pierceReach, 0, NPC.damage / 3);
                        }

                        // 九刺·雾: 三尖留怨火地灾 (zoning 遗留)
                        if (enhanced && Main.netMode != NetmodeID.MultiplayerClient) {
                            int patchBase = voidStrikeRepeatCount % 3;
                            for (int i = patchBase; i < TailCount; i += 3) {
                                float angle = rolledAngle + MathHelper.TwoPi * i / TailCount;
                                Vector2 tipEnd = NPC.Center + angle.ToRotationVector2() * NetherKitsuneTail.JointCount * NetherKitsuneTail.BaseSegmentLength * 3.6f;
                                SpawnGhostflamePatch(tipEnd);
                            }
                        }
                    }

                    if (PhaseTimer > 7) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 回收
                    if (PhaseTimer > (enhanced ? 18 : 27)) {
                        voidStrikeRepeatCount++;
                        if (voidStrikeRepeatCount >= maxRepeats) {
                            TransitionTo(CurrentHub());
                        }
                        else {
                            rolledAngle += MathHelper.ToRadians(enhanced ? 14f : 20f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>T1'' 幻影下砸: 三尾高举 (落点法阵) → poly12 砸落 → 落点留怨火。</summary>
        private void RunPhantomSlam(Player target) {
            HoverAround(target, new Vector2(0, -360), 0.05f, 0.02f);
            ApplyLeash(target);

            if (PhaseTimer == 10) {
                // 三落点: 玩家脚下与两侧 (确定性, 无需掷骰)
                for (int k = 0; k < 3; k++) {
                    int tailIdx = 1 + k * 3;
                    Vector2 slamTarget = target.Center + new Vector2((k - 1) * 150f, 20f);
                    Tails[tailIdx].StartPhantomSlamAttack(slamTarget, 0.75f);
                    // 判定线: 0.75s * 46% ≈ 21f 后砸落, 竖直短线覆盖落点
                    SpawnTailStrike(slamTarget - new Vector2(0, 90f), MathHelper.PiOver2, 150f, 21, NPC.damage / 3);
                }
                TriggerRunic(target.Center, 240f, 0.8f, false);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f }, NPC.Center);
            }

            // 砸落瞬间 (0.75s * 0.46 ≈ 21f 后)
            if (PhaseTimer == 33) {
                ACMScreenShakeSystem.Add(5f);
                TriggerRunic(target.Center, 240f, 1f, true);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.2f }, target.Center);
                NetherKitsuneFogSystem.CreateRipple(target.Center, 1.6f);

                // 落点怨火 (zoning)
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int k = 0; k < 3; k++) {
                        int tailIdx = 1 + k * 3;
                        SpawnGhostflamePatch(Tails[tailIdx].GetTipPosition());
                    }
                }
            }

            if (PhaseTimer > 80)
                TransitionTo(CurrentHub());
        }

        private BossPhase CurrentHub() {
            if (didPhase3Transition) return BossPhase.P3Hub;
            if (didPhase2Transition) return BossPhase.P2Hub;
            return BossPhase.P1Hub;
        }

        #endregion

        #region 转场《雾葬》/《怨决》

        /// <summary>转场1 雾葬: 雾吞全屏 → Boss 溶散 → 20f 全静默 → 巨眼 → 教学扑袭。</summary>
        private void RunTransition2(Player target) {
            contactEnabled = PhaseTimer >= 110; // 只有扑袭段咬人
            mistAim = 1.0f;

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                NetherKitsuneFogSystem.MistPulseAdd(0.5f);
                CancelAllTails();
            }

            if (PhaseTimer < 40) {
                NPC.velocity *= 0.9f;
                // Max: 若从雾隐招被打断进转场, 保持已溶散状态不回弹
                dissolveAmount = MathF.Max(dissolveAmount, MathHelper.Clamp((PhaseTimer - 15f) / 25f, 0f, 1f));
            }
            else if (PhaseTimer < 60) {
                // 全静默: 最浓的雾里什么都没有
                dissolveAmount = 1f;
                NPC.velocity = Vector2.Zero;
            }
            else if (PhaseTimer == 60) {
                // 巨眼位置: 服务器掷方向 → 同步
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    rolledAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    anchorPos = target.Center + rolledAngle.ToRotationVector2() * 430f;
                    NPC.netUpdate = true;
                }
            }
            else if (PhaseTimer == 64) {
                // 客户端生成巨眼 (fadeIn30 + squint20 → 扑袭在 ~114f)
                if (!Main.dedServ)
                    NetherKitsuneFogSystem.SpawnEye(anchorPos, 30, 0, 20, 300f, new Color(150, 220, 255));
                SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = -0.2f, Volume = 1.2f }, anchorPos);
            }
            else if (PhaseTimer == 114) {
                // 教学扑袭: 眼睛处爆出
                NPC.Center = anchorPos;
                dissolveAmount = 0.25f;
                Vector2 aim = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = aim * 42f;
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
                NetherKitsuneFogSystem.MistPulseAdd(-0.4f);
                NetherKitsuneFogSystem.CreateRipple(anchorPos, 2f);
                TriggerBloom(0.9f, new Color(150, 200, 255));
            }
            else if (PhaseTimer > 114) {
                if (PhaseTimer > 126)
                    NPC.velocity *= 0.62f; // 硬刹
                dissolveAmount = MathF.Max(0f, dissolveAmount - 0.03f);
            }

            if (PhaseTimer > 168) {
                dissolveAmount = 0f;
                TransitionTo(BossPhase.P2Hub);
            }
        }

        /// <summary>转场2 怨决: 雾转鬼绿, 九尾孔雀屏逐尖点燃 (音阶上行) → 白闪。</summary>
        private void RunTransition3(Player target) {
            contactEnabled = false;
            mistAim = 0.85f;
            NPC.velocity *= 0.9f;
            dissolveAmount = MathF.Max(0f, dissolveAmount - 0.06f); // 从雾隐招打断进来时先显形

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.5f, Volume = 1.8f }, NPC.Center);
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].StartFanDisplay(GetFanAngle(i), 2.0f);
                    fanIgnite[i] = 0f;
                }
            }

            // 逐尖点燃: 自外向内每 6f 一朵 (0,8,1,7,2,6,3,5,4), 音阶上行
            if (PhaseTimer > 20 && PhaseTimer <= 74) {
                int step = (int)((PhaseTimer - 20) / 6f);
                for (int k = 0; k < TailCount && k <= step; k++) {
                    int i = ExtinguishOrder(k);
                    if (fanIgnite[i] <= 0f && k == step) {
                        fanIgnite[i] = 0.01f;
                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = -0.4f + k * 0.12f, Volume = 0.8f }, NPC.Center);
                    }
                }
            }
            for (int i = 0; i < TailCount; i++) {
                if (fanIgnite[i] > 0f)
                    fanIgnite[i] = MathF.Min(1f, fanIgnite[i] + 0.1f);
                Tails[i].SetFanIgnite(fanIgnite[i]);
            }

            // 第九朵点燃 → 白闪 + 定格
            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = 0.4f, Volume = 1.6f }, NPC.Center);
                ACMScreenShakeSystem.Add(13f);
                TriggerBloom(1f, new Color(200, 255, 220));
                NetherKitsuneFogSystem.MistPulseAdd(-0.3f);
                NetherKitsuneFogSystem.CreateRipple(NPC.Center, 2.2f);
            }

            if (PhaseTimer > 108) {
                CancelAllTails();
                TransitionTo(BossPhase.P3Hub);
            }
        }

        #endregion

        #region P2 招式

        /// <summary>
        /// T4 雾隐扑袭 (核心招): 后拉溶散入雾 → 雾中狐眼凝视 → 瞳缩倒计时 (固定 14f 常数) → 扑袭 → 硬刹。
        /// 循环后硬直喘息 = 大惩罚窗。P3 复用快速版 (瞳缩 10f / 1 次 / 短喘息)。
        /// </summary>
        private void RunMistAmbush(Player target) {
            bool rapid = didPhase3Transition;
            int squintLen = rapid ? 10 : 14;
            int repeats = rapid ? 1 : (Main.expertMode ? 3 : 2);

            switch ((int)SubState) {
                case 0: // 入雾 25f: counter-motion 后拉 + 溶散
                    contactEnabled = false;
                    mistAim += 0.3f;
                    if (PhaseTimer == 1) {
                        NetherKitsuneFogSystem.MistPulseAdd(0.25f);
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
                        CancelAllTails();
                    }

                    // 反向抽身 (InverseLerp² 渐加速)
                    float back = MathF.Pow(MathHelper.Clamp(PhaseTimer / 25f, 0f, 1f), 2f) * 11f;
                    NPC.velocity = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * back;
                    dissolveAmount = MathHelper.Clamp(PhaseTimer / 22f, 0f, 1f);

                    if (PhaseTimer >= 25) {
                        NPC.velocity = Vector2.Zero;
                        SubState = 1;
                        PhaseTimer = 0;
                        // 服务器掷眼位: 最小 380px 防 telefrag (公平阀门)
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            rolledAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            anchorPos = target.Center + rolledAngle.ToRotationVector2() * Main.rand.NextFloat(390f, 460f);
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case 1: // 眼现 + 瞳缩 (18f fadeIn + squint) — 纯雾隐期, 无敌但不攻击
                    contactEnabled = false;
                    mistAim += 0.35f;
                    NPC.velocity = Vector2.Zero;
                    dissolveAmount = 1f;

                    if (PhaseTimer == 3 && !Main.dedServ) {
                        Color eyeC = rapid ? new Color(170, 255, 200) : new Color(150, 220, 255);
                        NetherKitsuneFogSystem.SpawnEye(anchorPos, 18, 0, squintLen, 170f, eyeC);
                    }
                    if (PhaseTimer == 4)
                        SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = 0.1f, Volume = 0.9f }, anchorPos);

                    if (PhaseTimer >= 18 + squintLen + 3) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 扑袭 12f: 眼位实体化 + set 46px/f 直线掠向预测点
                    if (PhaseTimer == 1) {
                        NPC.Center = anchorPos;
                        dissolveAmount = 0.25f; // 半透实体 (可受击可咬人)
                        Vector2 aim = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = aim * 46f;
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0f, Volume = 1.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(6f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.25f);
                        NetherKitsuneFogSystem.CreateRipple(anchorPos, 1.6f);
                    }
                    dissolveAmount = MathF.Max(0.15f, dissolveAmount - 0.02f);

                    if (PhaseTimer >= 12) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 硬刹 14f + 雾涡
                    NPC.velocity *= 0.6f;
                    dissolveAmount = MathF.Max(0f, dissolveAmount - 0.05f);
                    if (PhaseTimer >= 14) {
                        sweepCount++; // 复用计数器统计扑袭次数
                        if (sweepCount >= repeats) {
                            sweepCount = 0;
                            SubState = 4;
                            PhaseTimer = 0;
                            connectorDroop = 1f;
                            NetherKitsuneFogSystem.MistPulseAdd(-0.4f); // 雾骤清 = 呼气
                        }
                        else {
                            SubState = 0;
                            PhaseTimer = 0;
                        }
                    }
                    break;

                case 4: // 硬直喘息 (大惩罚窗): 尾垂 + 全实体
                    dissolveAmount = 0f;
                    mistAim = 0.3f;
                    connectorDroop = 1f;
                    NPC.velocity *= 0.93f;
                    if (PhaseTimer >= (rapid ? 22 : 40))
                        TransitionTo(CurrentHub());
                    break;
            }
        }

        /// <summary>T5 鬼影三掠: pow8 后拉 + 冲刺线预告 → set 40px/f 9f → 硬刹 → 雾步侧移 → ×3。</summary>
        private void RunGhostSweeps(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力 24f
                    mistAim += 0.15f;
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 aim = target.Center + target.velocity * 12f - NPC.Center;
                            dashDirection = aim.ToRotation();
                            NPC.netUpdate = true;
                        }
                        for (int i = 0; i < TailCount; i++)
                            if (!Tails[i].IsAttacking)
                                Tails[i].StartNetherCoilAttack(0.4f);
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f, Volume = 0.8f }, NPC.Center);
                    }

                    // pow8 late-snap 后拉 (几乎不动 → 末几帧猛然吸回)
                    float reel = MathF.Pow(MathHelper.Clamp(PhaseTimer / 24f, 0f, 1f), 8f);
                    Vector2 backDir = (-dashDirection.ToRotationVector2());
                    NPC.velocity = backDir * reel * 14f;

                    if (PhaseTimer >= 24) {
                        SubState = 1;
                        PhaseTimer = 0;
                        NPC.velocity = dashDirection.ToRotationVector2() * 40f;
                        SoundEngine.PlaySound(SoundID.Item130 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(4f);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 1.2f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.12f);
                    }
                    break;

                case 1: // 冲刺 9f (直线读得快)
                    if (PhaseTimer >= 9) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 硬刹 10f + 雾步侧移 8f
                    if (PhaseTimer <= 10) {
                        NPC.velocity *= 0.62f;
                    }
                    else {
                        // 雾步: 溶散遮掩的短侧移 (traveled 位移, 非瞬移)
                        float stepT = (PhaseTimer - 10) / 8f;
                        float bump = MathF.Sin(MathHelper.Clamp(stepT, 0f, 1f) * MathF.PI);
                        dissolveAmount = bump * 0.8f;
                        Vector2 lateral = (dashDirection + MathHelper.PiOver2 * (sweepCount % 2 == 0 ? 1f : -1f)).ToRotationVector2();
                        NPC.velocity = lateral * bump * 20f;
                        if (PhaseTimer == 11)
                            NetherKitsuneFogSystem.CreateRipple(NPC.Center, 0.9f);
                    }

                    if (PhaseTimer >= 18) {
                        dissolveAmount = 0f;
                        NPC.velocity *= 0.5f;
                        sweepCount++;
                        if (sweepCount >= 3) {
                            sweepCount = 0;
                            // 第三掠终点: 甩尾半环狐火
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 backAim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                                for (int k = -2; k <= 2; k++)
                                    SpawnFoxfireSoul(NPC.Center, backAim.RotatedBy(k * 0.3f) * 9f, NPC.damage / 3, 0);
                            }
                            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.1f }, NPC.Center);
                            TransitionTo(CurrentHub());
                        }
                        else {
                            SubState = 0;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// T6 镜雾九影: 环上多对狐眼同时亮起, 真身的眼睛会眨一次 (读数线索) → 全体瞳缩 →
        /// 真身实体冲刺 + 假影散幽紫虚弹。命中/穿过后真身硬直可受击。
        /// </summary>
        private void RunMirrorMist(Player target) {
            int ringCount = Main.expertMode ? 5 : 4;

            switch ((int)SubState) {
                case 0: // 布影: 溶散 + 掷真身
                    contactEnabled = false;
                    mistAim += 0.3f;
                    NPC.velocity *= 0.85f;
                    dissolveAmount = MathHelper.Clamp(PhaseTimer / 12f, 0f, 1f);

                    if (PhaseTimer == 1) {
                        CancelAllTails();
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            anchorPos = target.Center;
                            rolledAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            rolledIndex = Main.rand.Next(ringCount);
                            NPC.netUpdate = true;
                        }
                        NetherKitsuneFogSystem.MistPulseAdd(0.2f);
                        SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = 0.2f, Volume = 1f }, target.Center);
                    }

                    if (PhaseTimer == 14 && !Main.dedServ) {
                        // 环上生成 N 对狐眼: 真身 blinkAt=8 (凝视中段眨一次)
                        for (int i = 0; i < ringCount; i++) {
                            Vector2 pos = RingSlot(i, ringCount);
                            int blink = i == rolledIndex ? 8 : -1;
                            NetherKitsuneFogSystem.SpawnEye(pos, 20, 25, 20, 160f,
                                didPhase3Transition ? new Color(170, 255, 200) : new Color(150, 220, 255), blink);
                        }
                    }

                    if (PhaseTimer >= 14 + 20 + 25 + 20 + 2) { // fadeIn+stare+squint 走完
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 收束: 真身实体冲刺, 假影散虚弹
                    if (PhaseTimer == 1) {
                        Vector2 truePos = RingSlot(rolledIndex, ringCount);
                        NPC.Center = truePos;
                        dissolveAmount = 0.2f;
                        Vector2 aim = (anchorPos - truePos).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = aim * 22f;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < ringCount; i++) {
                                if (i == rolledIndex) continue;
                                Vector2 fakePos = RingSlot(i, ringCount);
                                Vector2 fakeAim = (anchorPos - fakePos).SafeNormalize(Vector2.UnitY);
                                for (int k = -1; k <= 1; k++)
                                    SpawnFoxfireSoul(fakePos, fakeAim.RotatedBy(k * 0.2f) * 9f, 0, 1); // 幽紫虚弹无害
                            }
                        }
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.2f, Volume = 1.1f }, truePos);
                        ACMScreenShakeSystem.Add(5f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.25f);
                    }

                    dissolveAmount = MathF.Max(0f, dissolveAmount - 0.02f);
                    if (PhaseTimer > 20)
                        NPC.velocity *= 0.85f;

                    if (PhaseTimer >= 40) {
                        SubState = 2;
                        PhaseTimer = 0;
                        connectorDroop = 1f;
                    }
                    break;

                case 2: // 硬直 30f (惩罚窗)
                    dissolveAmount = 0f;
                    connectorDroop = 1f;
                    NPC.velocity *= 0.92f;
                    if (PhaseTimer >= 30)
                        TransitionTo(CurrentHub());
                    break;
            }
        }

        private Vector2 RingSlot(int index, int count) {
            float ang = rolledAngle + MathHelper.TwoPi * index / count;
            return anchorPos + ang.ToRotationVector2() * 360f;
        }

        #endregion

        #region P3 招式

        /// <summary>
        /// T8 虚实九影 (三节拍循环): A 顺序幽刺琶音 → B 真身法阵锚+九向柔白实弹/幻影幽紫虚弹 → C 全尾横扫。
        /// beat 间 15f connector 停顿。
        /// </summary>
        private void RunPossession(Player target) {
            ghostFlicker = 0.5f + 0.5f * MathF.Abs(MathF.Sin(globalTime * 8f));
            mistAim = 0.7f;

            switch ((int)SubState) {
                case 0:
                    possessionBeat = 0;
                    phantomCount = 0;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // Beat A —— 加速琶音 (4f/尾)
                    ChasePossession(target, 12f, 0.13f);
                    if (PhaseTimer % 4 == 0) {
                        int idx = (int)(PhaseTimer / 4) - 1;
                        if (idx >= 0 && idx < TailCount && !Tails[idx].IsAttacking) {
                            Vector2 lead = target.Center + target.velocity * 6f;
                            Tails[idx].StartGhostStabAttack(lead, 0.5f);
                            // 判定线: 0.5s * 44% ≈ 13f 后爆发
                            Vector2 root = GetTailRootPosition(idx);
                            Vector2 toLead = lead - root;
                            float reach = MathF.Min(toLead.Length() + 90f, Tails[idx].TotalLength * 1.05f);
                            SpawnTailStrike(root, toLead.ToRotation(), reach, 13, NPC.damage / 3);
                        }
                    }
                    if (PhaseTimer > 52) {
                        SubState = 5; // connector
                        AttackTimer = 0;
                    }
                    break;

                case 5: // connector 15f → Beat B
                    NPC.velocity *= 0.9f;
                    connectorDroop = 0.6f;
                    if (AttackTimer > 15) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // Beat B —— 虚实九影
                    NPC.velocity *= 0.9f;
                    NPC.Center = Vector2.Lerp(NPC.Center, target.Center + new Vector2(0, -260), 0.03f);

                    if (PhaseTimer == 1) {
                        phantomCount = Main.expertMode ? 4 : 3;
                        for (int i = 0; i < phantomCount; i++) {
                            float a = MathHelper.TwoPi * i / phantomCount + globalTime;
                            phantomRotations[i] = a;
                            phantomPositions[i] = target.Center + a.ToRotationVector2() * 360f;
                            phantomAlpha[i] = 0f;
                        }
                        SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(6f);
                        TriggerRunic(NPC.Center, 300f, 0.9f, false); // 真身锚
                    }

                    for (int i = 0; i < phantomCount; i++) {
                        phantomAlpha[i] = MathHelper.Clamp(PhaseTimer / 30f, 0f, 0.75f);
                        phantomRotations[i] += 0.015f;
                        phantomPositions[i] = target.Center + phantomRotations[i].ToRotationVector2() * 360f;
                    }

                    if (PhaseTimer == 46) {
                        float baseAng = (target.Center - NPC.Center).ToRotation();
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < TailCount; i++) {
                                float ang = baseAng + MathHelper.TwoPi * i / TailCount;
                                SpawnFoxfireSoul(NPC.Center, ang.ToRotationVector2() * 9.5f, NPC.damage / 3, 2); // 真身柔白裁决
                            }
                            for (int p = 0; p < phantomCount; p++) {
                                float pbase = (target.Center - phantomPositions[p]).ToRotation();
                                for (int k = -2; k <= 2; k++)
                                    SpawnFoxfireSoul(phantomPositions[p], (pbase + k * 0.18f).ToRotationVector2() * 9f, 0, 1);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.6f }, NPC.Center);
                        TriggerBloom(0.85f, new Color(235, 245, 255));
                        ACMScreenShakeSystem.Add(6f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.15f);
                    }

                    if (PhaseTimer > 70) {
                        for (int i = 0; i < phantomCount; i++)
                            phantomAlpha[i] = MathHelper.Clamp(phantomAlpha[i] - 0.06f, 0f, 0.75f);
                    }

                    if (PhaseTimer > 95) {
                        phantomCount = 0;
                        SubState = 6; // connector
                        AttackTimer = 0;
                    }
                    break;

                case 6: // connector 15f → Beat C
                    NPC.velocity *= 0.9f;
                    connectorDroop = 0.6f;
                    if (AttackTimer > 15) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // Beat C —— 全尾魂魄横扫收束
                    ChasePossession(target, 9f, 0.1f);
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            if (!Tails[i].IsAttacking)
                                Tails[i].StartSoulSweepAttack(target.Center, MathHelper.PiOver2 * 0.7f, 0.5f);
                        SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.1f }, NPC.Center);
                    }
                    if (PhaseTimer > 50) {
                        possessionBeat++;
                        int maxBeats = Main.expertMode ? 3 : 2;
                        if (possessionBeat >= maxBeats)
                            TransitionTo(CurrentHub());
                        else {
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }

            // 幽冥粒子 (氛围)
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        private void ChasePossession(Player target, float speed, float lerp) {
            Vector2 desired = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, lerp);
            ApplyLeash(target);
        }

        /// <summary>
        /// T9 百鬼夜行 (≤20% set-piece): 浓雾中 4 波鬼行列横掠 —— 有眼发光者=真身有伤,
        /// 暗影车道只有无害虚弹; 终拍玩家背后红瞳 → 最终扑袭 → 落地硬直大惩罚窗。
        /// </summary>
        private void RunNightParade(Player target) {
            const int waveCount = 4;
            const int laneGap = 230;
            const float crossSpeed = 34f;
            mistAim = 1.05f;

            int wave = (int)SubState - 1;

            if ((int)SubState == 0) {
                // 入雾 20f
                contactEnabled = false;
                dissolveAmount = MathHelper.Clamp(PhaseTimer / 15f, 0f, 1f);
                NPC.velocity *= 0.85f;
                if (PhaseTimer == 1) {
                    CancelAllTails();
                    NetherKitsuneFogSystem.MistPulseAdd(0.4f);
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                }
                if (PhaseTimer >= 20) {
                    SubState = 1;
                    PhaseTimer = 0;
                }
                return;
            }

            if (wave >= 0 && wave < waveCount) {
                int dir = wave % 2 == 0 ? 1 : -1; // 方向交替 (确定性)

                if (PhaseTimer == 1) {
                    // 波首: 服务器掷真车道 + 锚定车道中心
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        anchorPos = target.Center;
                        rolledIndex = Main.rand.Next(3);
                        NPC.netUpdate = true;
                    }
                    dissolveAmount = 1f;
                    contactEnabled = false;
                    NPC.velocity = Vector2.Zero;
                }

                // f4: 真车道入口亮眼 (fadeIn10 + stare8 + squint10 → 掠出 ≈ f32)
                if (PhaseTimer == 4 && !Main.dedServ) {
                    Vector2 eyePos = anchorPos + new Vector2(-dir * 950f, (rolledIndex - 1) * laneGap);
                    NetherKitsuneFogSystem.SpawnEye(eyePos, 10, 8, 10, 150f, new Color(170, 255, 200));
                    SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = 0.3f, Volume = 0.8f }, eyePos);
                }

                // f32: 真身掠出 + 假车道虚弹横穿
                if (PhaseTimer == 32) {
                    NPC.Center = anchorPos + new Vector2(-dir * 1000f, (rolledIndex - 1) * laneGap);
                    NPC.velocity = new Vector2(dir * crossSpeed, 0f);
                    dissolveAmount = 0.25f;
                    contactEnabled = true;
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.1f, Volume = 1f }, target.Center);
                    ACMScreenShakeSystem.Add(4f);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int lane = 0; lane < 3; lane++) {
                            if (lane == rolledIndex) continue;
                            for (int k = 0; k < 2; k++) {
                                Vector2 spawn = anchorPos + new Vector2(-dir * (1000f + k * 220f), (lane - 1) * laneGap);
                                SpawnFoxfireSoul(spawn, new Vector2(dir * 30f, 0f), 0, 1); // 暗影车道无害虚影
                            }
                        }
                    }
                }

                // 掠行期间维持速度 (32~80f)
                if (PhaseTimer > 32 && PhaseTimer < 80) {
                    NPC.velocity = new Vector2(dir * crossSpeed, 0f);
                    dissolveAmount = 0.25f;
                }

                // f80: 波末隐没 + 呼吸
                if (PhaseTimer == 80) {
                    dissolveAmount = 1f;
                    contactEnabled = false;
                    NPC.velocity = Vector2.Zero;
                }

                if (PhaseTimer >= 95) {
                    SubState++;
                    PhaseTimer = 0;
                }
                return;
            }

            // 终拍: 玩家背后红瞳 → 最终扑袭 → 硬直
            switch ((int)SubState - 1 - waveCount) {
                case 0: // 红瞳凝视
                    contactEnabled = false;
                    dissolveAmount = 1f;
                    NPC.velocity = Vector2.Zero;
                    if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int behind = target.direction >= 0 ? -1 : 1;
                        anchorPos = target.Center + new Vector2(behind * 480f, -50f);
                        NPC.netUpdate = true;
                    }
                    if (PhaseTimer == 4 && !Main.dedServ) {
                        NetherKitsuneFogSystem.SpawnEye(anchorPos, 10, 0, 20, 220f, new Color(255, 120, 120)); // 唯一红瞳 (致命预警)
                        SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = -0.4f, Volume = 1.2f }, anchorPos);
                    }
                    if (PhaseTimer >= 34) {
                        SubState++;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 最终扑袭
                    if (PhaseTimer == 1) {
                        NPC.Center = anchorPos;
                        dissolveAmount = 0.2f;
                        contactEnabled = true;
                        Vector2 aim = (target.Center + target.velocity * 8f - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = aim * 50f;
                        SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.2f, Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        NetherKitsuneFogSystem.MistPulseAdd(-0.5f);
                        TriggerBloom(0.9f, didPhase3Transition ? new Color(170, 255, 200) : new Color(150, 200, 255));
                    }
                    if (PhaseTimer > 12)
                        NPC.velocity *= 0.6f;
                    dissolveAmount = MathF.Max(0f, dissolveAmount - 0.04f);
                    if (PhaseTimer >= 26) {
                        SubState++;
                        PhaseTimer = 0;
                        connectorDroop = 1f;
                    }
                    break;

                default: // 落地硬直 50f (大惩罚窗)
                    dissolveAmount = 0f;
                    connectorDroop = 1f;
                    mistAim = 0.35f;
                    NPC.velocity *= 0.92f;
                    if (PhaseTimer >= 50)
                        TransitionTo(CurrentHub());
                    break;
            }
        }

        #endregion

        #region 死亡《九火归寂》

        /// <summary>死亡拦截演出: 顿帧 → 雾收束 → 回光返照 (暖金) → 九火递熄 → 白闪爆散。</summary>
        private void RunDeath(Player target) {
            contactEnabled = false;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            mistAim = MathHelper.Lerp(1f, 0.15f, MathHelper.Clamp((PhaseTimer - 40f) / 60f, 0f, 1f));
            if (PhaseTimer < 250)
                dissolveAmount = MathF.Max(0f, dissolveAmount - 0.06f); // 半透中被打死也先显形再演出

            if (PhaseTimer == 1) {
                CancelAllTails();
                NetherKitsuneFogSystem.ClearEyes();
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].StartFanDisplay(GetFanAngle(i), (DeathDuration + 20) / 60f);
                    fanIgnite[i] = 1f;
                }
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = -0.7f, Volume = 1.4f }, NPC.Center);
                ACMScreenShakeSystem.Add(4f);
            }

            // 0-40f 顿帧: 雾冻结
            if (PhaseTimer < 40) {
                mistFreeze = 1f;
                NPC.velocity = Vector2.Zero;
            }
            // 40-100f 世界吸气: 全场雾收束吸入狐身
            else if (PhaseTimer < 100) {
                NetherKitsuneFogSystem.SetGather(true, NPC.Center);
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(400, 400);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.BlueTorch, (NPC.Center - dustPos) * 0.04f, 130, default, 1.8f);
                    d.noGravity = true;
                }
            }
            // 100-160f 回光返照: 渐变生前暖金, 仰首, 一拍安静
            else if (PhaseTimer < 160) {
                deathGold = MathHelper.Clamp((PhaseTimer - 100f) / 45f, 0f, 1f);
                NPC.velocity = new Vector2(0, -0.3f);
                if (PhaseTimer == 130)
                    SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = 0.5f, Volume = 0.9f }, NPC.Center);
            }
            // 160-250f 九火递熄: 自外向内, 间隔递减加速, 每熄一点降调
            else if (PhaseTimer < 252) {
                NetherKitsuneFogSystem.SetGather(false, NPC.Center);
                int t = (int)PhaseTimer - 160;
                for (int k = 0; k < TailCount; k++) {
                    if (t >= ExtinguishOffset(k) && fanIgnite[ExtinguishOrder(k)] > 0f) {
                        int i = ExtinguishOrder(k);
                        if (fanIgnite[i] >= 0.99f) {
                            SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.4f - k * 0.12f, Volume = 0.75f }, Tails[i].GetTipPosition());
                            if (Main.netMode != NetmodeID.Server) {
                                for (int di = 0; di < 6; di++) {
                                    Dust d = Dust.NewDustPerfect(Tails[i].GetTipPosition(), DustID.GoldFlame,
                                        Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0, -1f), 120, default, 1.4f);
                                    d.noGravity = true;
                                }
                            }
                        }
                        fanIgnite[i] = MathF.Max(0f, fanIgnite[i] - 0.12f);
                    }
                }
            }
            // 252f 终拍: 白闪 + 全场唯一 shake 15
            else if (PhaseTimer == 252) {
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.4f, Volume = 1.6f }, NPC.Center);
                ACMScreenShakeSystem.Add(15f);
                TriggerBloom(1f, Color.Lerp(new Color(255, 225, 150), Color.White, 0.4f));
                NetherKitsuneFogSystem.MistPulseAdd(-0.6f);
                NetherKitsuneFogSystem.CreateRipple(NPC.Center, 2.5f);
            }
            // 252-330f 爆散成冥蓝雾雨 + 溶解
            else {
                dissolveAmount = MathHelper.Clamp((PhaseTimer - 252f) / 60f, 0f, 1f);
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                    Vector2 vel = Main.rand.NextVector2Circular(6f, 6f) + new Vector2(0, 1.5f);
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(70f, 70f), DustID.BlueTorch, vel, 100, default, 2f);
                    d.noGravity = true;
                }
            }

            // 展屏点燃级别持续同步到尾巴 (金色渐变经绘制层)
            for (int i = 0; i < TailCount; i++)
                Tails[i].SetFanIgnite(fanIgnite[i]);

            if (PhaseTimer >= DeathDuration) {
                NetherKitsuneFogSystem.SetGather(false, NPC.Center);
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead(); // CheckDead 此时放行 → OnKill/掉落正常走
            }
        }

        /// <summary>熄灭顺序: 自外向内交替 (0,8,1,7,2,6,3,5,4)。</summary>
        private static int ExtinguishOrder(int k) {
            int half = k / 2;
            return k % 2 == 0 ? half : TailCount - 1 - half;
        }

        /// <summary>第 k 朵熄灭的时刻偏移 (间隔 18→5f 递减加速)。</summary>
        private static int ExtinguishOffset(int k) {
            int off = 0;
            for (int i = 0; i < k; i++)
                off += Math.Max(5, 18 - i * 2);
            return off;
        }

        #endregion

        #region 射弹

        /// <summary>
        /// 生成幽冥狐火魂弹。variant: 0=实狐火 1=虚幻影(damage 0) 2=真身裁决 3=吐息(熄灭留怨火)。
        /// 仅服务端/单机生成。
        /// </summary>
        private void SpawnFoxfireSoul(Vector2 pos, Vector2 vel, int damage, int variant) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (variant == 1)
                damage = 0; // 虚影无害, 只作真假误导
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<NetherFoxfireSoul>(), damage, 2f, Main.myPlayer, variant);
        }

        /// <summary>生成怨火地灾 (带同屏上限)。仅服务端/单机。</summary>
        private void SpawnGhostflamePatch(Vector2 pos) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int patchType = ModContent.ProjectileType<NetherGhostflamePatch>();
            int count = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == patchType)
                    count++;
            }
            if (count >= 10)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero, patchType, 0, 0f, Main.myPlayer);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawTelegraphBeams(spriteBatch, screenPos);
            DrawTrail(spriteBatch, screenPos);
            DrawPhantoms(spriteBatch, screenPos, drawColor);
            DrawTails(spriteBatch, screenPos, drawColor);
            DrawMainBody(spriteBatch, screenPos, drawColor);
            DrawTipFlames();

            return false;
        }

        // ===== 全屏冥雾后处理 (NetherKitsuneMist) — 占唯一全屏名额 =====
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            float density = NetherKitsuneFogSystem.MistDensity;
            if (density <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = NetherKitsuneFogSystem.MistFx;
            if (fx == null)
                return;

            Player lp = Main.LocalPlayer;
            Vector2 clearUV = (lp.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uDensity"]?.SetValue(density);
            fx.Parameters["uGhost"]?.SetValue(NetherKitsuneFogSystem.MistGhost);
            fx.Parameters["uFreeze"]?.SetValue(NetherKitsuneFogSystem.MistFreeze);
            fx.Parameters["uClearCenter"]?.SetValue(clearUV);
            fx.Parameters["uClearRadius"]?.SetValue(0.16f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWind"]?.SetValue(NetherKitsuneFogSystem.MistWind);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        /// <summary>招式预告光束: 冲刺线 / 钳击连线 / 扑袭线 / 夜行车道。</summary>
        private void DrawTelegraphBeams(SpriteBatch spriteBatch, Vector2 screenPos) {
            Color violet = TelegraphColors.NetherViolet;
            Color lethal = TelegraphColors.Lethal;

            // 鬼影三掠: 蓄力期冲刺线 (幽紫→末 5f 转红)
            if (Phase == BossPhase.P2GhostSweeps && (int)SubState == 0 && PhaseTimer > 4) {
                float t = MathHelper.Clamp(PhaseTimer / 24f, 0f, 1f);
                bool late = PhaseTimer >= 19;
                Color core = late ? lethal : violet;
                Vector2 dir = dashDirection.ToRotationVector2();
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1500f,
                    MathHelper.Lerp(3f, 8f, t), core, new Color(60, 30, 110), 0.35f + 0.45f * t, 1.8f);
            }

            // 双尾钳击: 悬停期两尖连线 (幽紫→合刺前转红)
            if (Phase == BossPhase.P1PincerStab) {
                (int a, int b) = PincerPair(pincerRound);
                if (Tails != null && Tails[a].CurrentAttack == NetherKitsuneTail.TailAttackType.PincerStab && Tails[a].IsAttacking) {
                    float at = Tails[a].AttackTimer / 0.9f;
                    if (at > 0.30f && at < 0.68f) {
                        bool late = at > 0.52f;
                        Color core = late ? lethal : violet;
                        ACMShaders.DrawBeam(Tails[a].GetTipPosition(), Tails[b].GetTipPosition(),
                            late ? 7f : 4f, core, new Color(60, 30, 110), late ? 0.9f : 0.5f, 2f);
                    }
                }
            }

            // 雾隐扑袭: 瞳缩期扑袭线 (幽紫→末 6f 转红)
            if (Phase == BossPhase.P2MistAmbush && (int)SubState == 1 && PhaseTimer > 18) {
                bool rapid = didPhase3Transition;
                int squintLen = rapid ? 10 : 14;
                float t = MathHelper.Clamp((PhaseTimer - 18) / (float)squintLen, 0f, 1f);
                bool late = PhaseTimer >= 18 + squintLen - 6;
                Player tgt = Main.player[NPC.target];
                if (tgt.active) {
                    Vector2 aim = (tgt.Center - anchorPos).SafeNormalize(Vector2.UnitX);
                    ACMShaders.DrawBeam(anchorPos, anchorPos + aim * 1100f,
                        MathHelper.Lerp(2.5f, 6f, t), late ? lethal : violet, new Color(60, 30, 110), 0.3f + 0.5f * t, 2f);
                }
            }

            // 百鬼夜行: 波首车道预告 (真车道亮)
            if (Phase == BossPhase.P3NightParade) {
                int wave = (int)SubState - 1;
                if (wave >= 0 && wave < 4 && PhaseTimer > 4 && PhaseTimer < 32) {
                    float t = MathHelper.Clamp((PhaseTimer - 4) / 26f, 0f, 1f);
                    for (int lane = 0; lane < 3; lane++) {
                        Vector2 y = anchorPos + new Vector2(0, (lane - 1) * 230f);
                        bool isTrue = lane == rolledIndex;
                        Color core = isTrue && PhaseTimer > 24 ? lethal : violet;
                        float inten = (isTrue ? 0.65f : 0.3f) * t;
                        ACMShaders.DrawBeam(y - new Vector2(1050f, 0), y + new Vector2(1050f, 0),
                            isTrue ? 7f : 4f, core, new Color(50, 25, 95), inten, 1.5f);
                    }
                }
            }
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            // 速度门控残影: 只在爆发时刻出现 (speed-gated dressing)
            if (NPC.velocity.Length() < 14f || dissolveAmount > 0.9f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(80, 150, 220), new Color(90, 210, 150), didPhase3Transition ? 1f : 0f)
                    * progress * 0.3f * ghostFlicker * (1f - dissolveAmount);
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.85f;

                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation,
                    texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawPhantoms(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Phase != BossPhase.P3Possession || phantomAlpha == null)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            for (int i = 0; i < phantomCount; i++) {
                if (phantomAlpha[i] <= 0.01f)
                    continue;

                // 幽紫虚影 + soul-dissolve: 噪声 clip + 灼烧边重凝/溶散
                float vis = MathHelper.Clamp(phantomAlpha[i] / 0.75f, 0f, 1f);
                Color phantomColor = new Color(150, 130, 230) * phantomAlpha[i];
                phantomColor.A = (byte)(phantomAlpha[i] * 150);

                WeaponVFX.ApplyDissolveBurn(
                    texture, phantomPositions[i], null, phantomColor,
                    NPC.rotation, origin, NPC.scale * 0.9f,
                    threshold: 1f - vis,
                    intensity: MathHelper.Clamp(vis, 0.05f, 1f),
                    edgeColor: new Color(180, 140, 255, 200), edgeWidth: 0.1f, noiseScale: 2.4f);
            }
        }

        private void DrawTails(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Tails == null)
                return;
            // 入场雾中隐藏 (九火先行)
            if (Phase == BossPhase.Intro && PhaseTimer < 60)
                return;
            // 全溶散时尾巴一同隐没
            if (dissolveAmount > 0.85f)
                return;

            float ghostTint = didPhase3Transition ? 1f : 0f;
            float vis = 1f - dissolveAmount;

            // 死亡回光返照: 尾色染金
            Color themeColor = Color.Lerp(new Color(100, 160, 220), new Color(255, 200, 130), deathGold);
            Color tailColor = Color.Lerp(drawColor, themeColor, 0.4f + deathGold * 0.4f);
            tailColor *= ghostFlicker * vis;

            // 入场渐显
            if (Phase == BossPhase.Intro)
                tailColor *= MathHelper.Clamp((PhaseTimer - 60f) / 60f, 0f, 1f);

            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.DrawTelegraph(spriteBatch, screenPos);
            }

            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.Draw(spriteBatch, screenPos, tailColor, deathGold > 0.05f ? 0f : ghostTint);
            }
        }

        /// <summary>尾尖魂焰批 (TipGlow 驱动; 死亡演出染回生前暖金)。</summary>
        private void DrawTipFlames() {
            if (Main.dedServ || Tails == null || dissolveAmount > 0.85f)
                return;

            tipFlames.Clear();
            float ghost = didPhase3Transition && deathGold < 0.05f ? 1f : 0f;
            for (int i = 0; i < TailCount; i++) {
                float glow = Tails[i].TipGlow;
                if (glow < 0.08f)
                    continue;

                Vector2 tipDir = Tails[i].GetTipDirection();
                Color core = Color.Lerp(new Color(190, 240, 255), new Color(255, 235, 170), deathGold);
                Color edge = Color.Lerp(new Color(60, 110, 200), new Color(230, 140, 60), deathGold);
                tipFlames.Add(new NetherKitsuneFogSystem.SoulflameSpec {
                    WorldPos = Tails[i].GetTipPosition() + tipDir * 4f,
                    WidthPx = 34f + glow * 14f,
                    HeightPx = 60f + glow * 40f,
                    Intensity = glow * (1f - dissolveAmount),
                    Ghost = deathGold > 0.05f ? 0f : ghost,
                    Rotation = tipDir.ToRotation() + MathHelper.PiOver2,
                    Seed = i * 1.31f,
                    Core = core,
                    Edge = edge,
                });
            }

            if (tipFlames.Count > 0)
                NetherKitsuneFogSystem.DrawSoulflameBatch(tipFlames);
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (dissolveAmount >= 0.98f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = texture.Size() / 2f;

            // 本体主题色 (冥蓝 / 鬼绿 / 死亡暖金)
            Color theme = Color.Lerp(new Color(120, 180, 230), new Color(120, 220, 170), didPhase3Transition ? 0.6f : 0f);
            theme = Color.Lerp(theme, new Color(255, 205, 140), deathGold);
            Color bodyColor = Color.Lerp(drawColor, theme, 0.35f + deathGold * 0.35f);
            bodyColor *= ghostFlicker;

            // 溶散中: DissolveBurn 噪声撕碎 + 灼烧边
            if (dissolveAmount > 0.01f) {
                Color edge = didPhase3Transition ? new Color(140, 255, 190, 210) : new Color(150, 200, 255, 210);
                WeaponVFX.ApplyDissolveBurn(
                    texture, NPC.Center, null, bodyColor * (1f - dissolveAmount * 0.3f),
                    NPC.rotation, origin, NPC.scale,
                    threshold: dissolveAmount,
                    intensity: 1f - dissolveAmount * 0.5f,
                    edgeColor: edge, edgeWidth: 0.09f, noiseScale: 2.2f);
                return;
            }

            // 幽光晕层
            Color glowColor = Color.Lerp(new Color(80, 150, 220), new Color(255, 200, 120), deathGold) * 0.4f * ghostFlicker;
            glowColor.A = 0;

            for (int i = 0; i < 4; i++) {
                Vector2 offset = new Vector2(
                    MathF.Cos(globalTime * 3f + i * MathHelper.PiOver2),
                    MathF.Sin(globalTime * 3f + i * MathHelper.PiOver2)) * 4f;

                spriteBatch.Draw(texture, drawPos + offset, null, glowColor, NPC.rotation,
                    origin, NPC.scale * 1.08f, SpriteEffects.None, 0f);
            }

            bodyColor.A = (byte)(255 - NPC.alpha);
            spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation,
                origin, NPC.scale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}

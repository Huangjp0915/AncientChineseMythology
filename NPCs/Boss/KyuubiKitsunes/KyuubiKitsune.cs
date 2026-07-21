using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.NPCs.Boss.KyuubiKitsunes.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 九尾妖狐 Boss (世纪之花后) — V3 重做「妖异优雅」。
    /// 身份: "你要读的不是狐, 而是九尾的合奏 — 扇形展开是蓄势, 逐尾点火是倒计时, 尾尖所指是死线。"
    /// 一阶段·金焰宫舞: 天狐问剑(扇面贯刺) / 狐火天灯 / 金风横扫 / 叩月坠砸, 固定连段 + 连接节拍。
    /// 二阶段·魅影乱舞(60%): 魅影环舞三段冲 / 狐影九重·镜舞 / 双色天灯 / 狐火曼陀罗 2.0 (本体入阵眼)。
    /// 终结技(≤25% 一次): 万狐朝月 5 连九方位贯刺 + 力竭喘息奖励窗, 此后狂化提速。
    /// 三大演出: 狐火聚形入场 / 妖狐显世换阶段 / 狐火归天死亡 (CheckDead 拦截)。
    /// 全部尾巴攻击伤害由权威弹幕承载 (KyuubiTailLance 等), 视觉与判定严格对齐;
    /// 接触伤害仅在冲刺/坠砸/横掠的高速窗口开启 (CanHitPlayer 速度门)。
    /// </summary>
    [AutoloadBossHead]
    internal class KyuubiKitsune : ModNPC
    {
        [VaultLoaden("{@namespace}/")]
        public static Texture2D MissesBody;
        [VaultLoaden("{@namespace}/")]
        public static Texture2D MissesTop;

        #region 常量定义

        /// <summary>尾巴数量</summary>
        public const int TailCount = 9;

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.6f;

        /// <summary>终结技血量阈值 (≤25%)</summary>
        public const float FinisherThreshold = 0.25f;

        /// <summary>曼陀罗九边形半径 (世界像素)</summary>
        public const float MandalaRadiusValue = 340f;

        /// <summary>接触伤害速度门 (px/f): 只有真正的冲锋才咬人</summary>
        private const float ContactSpeedGate = 20f;

        #endregion

        #region 阶段枚举与脚本

        public enum BossPhase
        {
            Intro = 0,          // 入场: 狐火聚形
            P1_Connector = 1,   // 一阶段连接节拍 (理尾 + 选招)
            P1_FanStab = 2,     // 天狐问剑·扇面九连刺
            P1_Lanterns = 3,    // 狐火天灯
            P1_Sweep = 4,       // 金风横扫
            P1_Slam = 5,        // 叩月坠砸
            PhaseTransition = 6,// 换阶段演出: 妖狐显世
            P2_Connector = 7,   // 二阶段连接节拍
            P2_Dash = 8,        // 魅影环舞·三段冲
            P2_Mirror = 9,      // 狐影九重·镜舞
            P2_Lanterns = 10,   // 狐火天灯·双色
            P2_Mandala = 11,    // 狐火曼陀罗 2.0
            Finisher = 12,      // 万狐朝月 (≤25% 一次)
            Death = 13          // 死亡演出: 狐火归天
        }

        /// <summary>一阶段固定连段 (序列即编排: 几何招与机动招交替, PACING §2)。</summary>
        private static readonly BossPhase[] P1Script = {
            BossPhase.P1_FanStab, BossPhase.P1_Lanterns, BossPhase.P1_Sweep,
            BossPhase.P1_Slam, BossPhase.P1_FanStab, BossPhase.P1_Lanterns
        };

        /// <summary>二阶段固定连段 (曼陀罗 set-piece 押后)。</summary>
        private static readonly BossPhase[] P2Script = {
            BossPhase.P2_Dash, BossPhase.P2_Mirror, BossPhase.P2_Lanterns,
            BossPhase.P2_Dash, BossPhase.P2_Mandala
        };

        /// <summary>尾巴角色色 (尾尖辉光色编码): 刺客暖橙 / 术士金 / 鞭尾紫。</summary>
        private static readonly Color[] RoleTints = {
            new(255, 140, 50),
            new(255, 215, 120),
            new(190, 120, 255)
        };

        // 主题色
        private static readonly Color GoldFlame = new(255, 200, 100);
        private static readonly Color CharmPink = new(255, 120, 170);
        private static readonly Color CharmViolet = new(190, 60, 160);

        #endregion

        #region 专属着色器 (静态缓存, 参考 Xuanwu 写法; 不注册进 ACMShaders)

        private static Asset<Effect> charmVeilRef;
        private static Asset<Effect> mandalaRef;

        internal static Effect CharmVeilFx {
            get {
                if (Main.dedServ)
                    return null;
                charmVeilRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/KyuubiCharmVeil", AssetRequestMode.ImmediateLoad);
                return charmVeilRef?.Value;
            }
        }

        internal static Effect MandalaFx {
            get {
                if (Main.dedServ)
                    return null;
                mandalaRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/KyuubiMandala", AssetRequestMode.ImmediateLoad);
                return mandalaRef?.Value;
            }
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

        /// <summary>九条尾巴</summary>
        public KyuubiTail[] Tails { get; private set; }

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        // ===== 狐火曼陀罗 — 供边墙 KyuubiMandalaEdge 读取的权威状态 =====
        /// <summary>当前是否正进行曼陀罗 set-piece。</summary>
        public bool InMandala => Phase == BossPhase.P2_Mandala;
        /// <summary>九边形中心 (开场捕获玩家位置, 同步)。</summary>
        public Vector2 MandalaCenter => mandalaCenter;
        /// <summary>九边形半径。</summary>
        public float MandalaRadius => MandalaRadiusValue;
        /// <summary>整环旋转角。</summary>
        public float MandalaRotation => mandalaRotation;
        /// <summary>当前安全缺口边索引。</summary>
        public int MandalaGapIndex => mandalaGapIndex;
        /// <summary>是否进入致命伤害窗口 (预告结束后)。</summary>
        public bool MandalaDamaging => InMandala && (int)SubState == 2;
        /// <summary>边墙可见度 0~1。</summary>
        public float MandalaEdgeAlpha => mandalaEdgeAlpha;

        // ===== 同步状态 (SendExtraAI / ReceiveExtraAI) =====
        private bool didPhaseTransition;
        private bool didFinisher;
        private bool enraged;           // 终结技后狂化: 连段提速
        private bool deathTriggered;    // CheckDead 拦截标记
        private int p1ScriptIndex;
        private int p2ScriptIndex;
        private int leadTail;           // 扇面波纹领尾
        private float fanBaseAngle;     // 扇面/终结技基准角 (施放瞬间锁定)
        private Vector2 slamTargetPos;  // 坠砸落点 (提前锁定, 不追踪)
        private float sweepDir;         // 横扫方向 +1/-1
        private float dashAngle;        // 冲刺/镜舞突进方向角
        private int dashCount;
        private Vector2 mirrorCenter;   // 镜舞环心
        private int trueSlot;           // 镜舞真身槽位
        private int mirrorCount;        // 幻影数量
        private Vector2 lungeTarget;    // 镜舞突进锁定点
        private Vector2 mandalaCenter;
        private float mandalaRotation;
        private int mandalaGapIndex;

        // ===== 本地状态 (确定性推进或纯视觉, 不同步) =====
        private float globalTime;
        private bool mandalaSpawnedEdges;   // 仅服务器使用
        private int lanternWave;            // 天灯当前波次 (定时器推进, 各端一致)
        private int pendingLanternStyle;    // 天灯样式: 1=金追踪灯 2=紫直线妖火

        // 演出/绘制字段 (纯视觉)
        private float mandalaEdgeAlpha;
        private float petalPulse;           // 法阵花瓣脉冲 0~1
        private float ceremonyDecal;        // 典仪法阵强度 (入场/换阶段/终结技/死亡)
        private Color ceremonyColor = new(255, 200, 100);
        private float ceremonyRadius = 300f;
        private float bloomPower;
        private Vector2 bloomPos;
        private Color bloomColor = Color.Gold;
        private float shockRadius;
        private float shockAlpha;
        private Vector2 shockCenter;
        private Color shockColor = Color.Gold;
        private float phase2Tint;           // 0=金焰 1=狐魅紫红
        private float paletteFlash;         // 短暂染屏强度 (换阶段/死亡终响)
        private float introDissolve = 1f;   // 入场显形溶解 (1=不可见)
        private float deathDissolve;        // 死亡消散溶解
        private float bodySquash;           // 竖向挤压 (-0.2~0.2)
        private float stillBreath = 1f;     // 静默节拍时压制粒子的开关 (0=完全静默)

        // 镜舞幻影 (位置确定性计算, 溶解为本地视觉)
        private float[] illusionDissolve;
        private Vector2[] illusionPositions;
        private float mirrorOrbit;
        private float illusionAlpha;
        private float lungeVisualOffset;    // 幻影内突可视偏移

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 80;
            NPC.defense = 50;
            NPC.lifeMax = 75000; // 世纪之花后强度
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.Roar;
            NPC.value = Item.buyPrice(0, 15, 0, 0);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 10f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.3f);
            }

            Music = MusicID.Boss4;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.HealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 15, 25));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 12, 18));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<KyuubiBook>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedKyuubi = true;
        }

        public override void OnSpawn(IEntitySource source) {
            InitializeTails();
            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override bool CheckActive() => false;

        /// <summary>死亡拦截: 进入「狐火归天」死亡演出, 演出末真正结算击杀。</summary>
        public override bool CheckDead() {
            if (!deathTriggered) {
                deathTriggered = true;
                NPC.life = Math.Max(NPC.life, 1);
                NPC.dontTakeDamage = true;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    ClearOwnedProjectiles();
                TransitionTo(BossPhase.Death);
                return false;
            }
            return true;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldFlame,
                    hit.HitDirection * 2f, -1f, 150, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(globalTime);
            writer.Write(didPhaseTransition);
            writer.Write(didFinisher);
            writer.Write(enraged);
            writer.Write(deathTriggered);
            writer.Write(p1ScriptIndex);
            writer.Write(p2ScriptIndex);
            writer.Write(leadTail);
            writer.Write(fanBaseAngle);
            writer.WriteVector2(slamTargetPos);
            writer.Write(sweepDir);
            writer.Write(dashAngle);
            writer.Write(dashCount);
            writer.WriteVector2(mirrorCenter);
            writer.Write(trueSlot);
            writer.Write(mirrorCount);
            writer.WriteVector2(lungeTarget);
            writer.WriteVector2(mandalaCenter);
            writer.Write(mandalaRotation);
            writer.Write(mandalaGapIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            globalTime = reader.ReadSingle();
            didPhaseTransition = reader.ReadBoolean();
            didFinisher = reader.ReadBoolean();
            enraged = reader.ReadBoolean();
            deathTriggered = reader.ReadBoolean();
            p1ScriptIndex = reader.ReadInt32();
            p2ScriptIndex = reader.ReadInt32();
            leadTail = reader.ReadInt32();
            fanBaseAngle = reader.ReadSingle();
            slamTargetPos = reader.ReadVector2();
            sweepDir = reader.ReadSingle();
            dashAngle = reader.ReadSingle();
            dashCount = reader.ReadInt32();
            mirrorCenter = reader.ReadVector2();
            trueSlot = reader.ReadInt32();
            mirrorCount = reader.ReadInt32();
            lungeTarget = reader.ReadVector2();
            mandalaCenter = reader.ReadVector2();
            mandalaRotation = reader.ReadSingle();
            mandalaGapIndex = reader.ReadInt32();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        /// <summary>
        /// 接触伤害速度门 (公平阀门): 只有冲刺/坠砸/横掠的高速窗口才咬人,
        /// 悬浮/施法/曼陀罗/演出期本体无接触伤害 — 伤害窗口与视觉严格对齐。
        /// </summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            float spd = NPC.velocity.Length();
            return Phase switch {
                BossPhase.P1_Sweep => (int)SubState == 2 && spd > 14f,
                BossPhase.P1_Slam => (int)SubState == 3,
                BossPhase.P2_Dash => (int)SubState == 2 && spd > ContactSpeedGate,
                BossPhase.P2_Mirror => (int)SubState == 3 && spd > ContactSpeedGate,
                _ => false
            };
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            globalTime += 1f / 60f;

            if (Tails == null)
                InitializeTails();
            illusionDissolve ??= new float[8];
            illusionPositions ??= new Vector2[8];

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.5f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;
            stillBreath = 1f; // 各状态的静默节拍自行压为 0

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.P1_Connector: RunConnector(target, phase2: false); break;
                case BossPhase.P1_FanStab: RunFanStab(target); break;
                case BossPhase.P1_Lanterns: RunLanterns(target, dual: false); break;
                case BossPhase.P1_Sweep: RunSweep(target); break;
                case BossPhase.P1_Slam: RunSlam(target); break;
                case BossPhase.PhaseTransition: RunPhaseTransition(target); break;
                case BossPhase.P2_Connector: RunConnector(target, phase2: true); break;
                case BossPhase.P2_Dash: RunDash(target); break;
                case BossPhase.P2_Mirror: RunMirror(target); break;
                case BossPhase.P2_Lanterns: RunLanterns(target, dual: true); break;
                case BossPhase.P2_Mandala: RunMandala(target); break;
                case BossPhase.Finisher: RunFinisher(target); break;
                case BossPhase.Death: RunDeath(target); break;
            }

            UpdateAllTails();
            UpdateBodyLanguage(target);
            UpdateVisualDecay();

            // 妖力发光: 二阶段转狐魅紫红
            Vector3 light = Vector3.Lerp(new Vector3(1f, 0.62f, 0.22f), new Vector3(1f, 0.42f, 0.55f), phase2Tint);
            Lighting.AddLight(NPC.Center, light * 0.8f * NPC.Opacity);
        }

        /// <summary>身体语言: 面向 / 速度倾斜 (纯表现, 每帧统一处理)。</summary>
        private void UpdateBodyLanguage(Player target) {
            // 冲刺/坠砸中面向锁定运动方向, 其余面向玩家
            if (NPC.velocity.LengthSquared() > 36f)
                NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
            else
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

            float targetRot = MathHelper.Clamp(NPC.velocity.X * 0.016f, -0.32f, 0.32f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.15f);

            // 竖向挤压自然回弹
            bodySquash = MathHelper.Lerp(bodySquash, 0f, 0.12f);
        }

        private void UpdateVisualDecay() {
            if (bloomPower > 0f)
                bloomPower = MathF.Max(0f, bloomPower - 0.04f);
            if (petalPulse > 0f)
                petalPulse = MathF.Max(0f, petalPulse - 0.03f);
            if (paletteFlash > 0f)
                paletteFlash = MathF.Max(0f, paletteFlash - 0.08f);
            if (shockAlpha > 0f) {
                shockAlpha = MathF.Max(0f, shockAlpha - 0.035f);
                shockRadius += 14f;
            }
            // 二阶段色调平滑推进
            float tintTarget = didPhaseTransition ? 1f : 0f;
            phase2Tint = MathHelper.Lerp(phase2Tint, tintTarget, 0.02f);
        }

        private void InitializeTails() {
            Tails = new KyuubiTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new KyuubiTail(i);
                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);
                Tails[i].Initialize(GetTailRootPosition(i), baseAngle);
            }
        }

        private Vector2 GetTailRootPosition(int tailIndex) {
            float angleRange = MathHelper.Pi;
            float startAngle = -MathHelper.Pi * 0.75f;
            float angle = startAngle + angleRange * tailIndex / (TailCount - 1);
            float radius = 35f;
            Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            return NPC.Center + offset;
        }

        private void UpdateAllTails() {
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i] == null) continue;

                Vector2 rootPos = GetTailRootPosition(i);

                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);

                if (NPC.velocity.LengthSquared() > 1f) {
                    float velocityAngle = NPC.velocity.ToRotation();
                    float oppositeAngle = velocityAngle + MathHelper.Pi;
                    float spreadOffset = (i - 4) / 4f * MathHelper.PiOver4;
                    baseAngle = MathHelper.Lerp(baseAngle, oppositeAngle + spreadOffset, 0.4f);
                }

                float swayOffset = MathF.Sin(globalTime * 2f + i * 0.7f) * 0.1f;
                baseAngle += swayOffset;

                Tails[i].Update(rootPos, baseAngle, NPC.velocity, globalTime);

                if (Tails[i].ShouldFireProjectile())
                    FireTailProjectile(i);
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhaseTransition && IsPhase2 && Phase != BossPhase.PhaseTransition &&
                Phase != BossPhase.Intro && Phase != BossPhase.Death) {
                TransitionTo(BossPhase.PhaseTransition);
                didPhaseTransition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            // 离开任何阶段前解除钉位与点火, 避免尾巴姿态残留
            if (Tails != null) {
                for (int i = 0; i < TailCount; i++) {
                    if (Tails[i] == null) continue;
                    Tails[i].Pinned = false;
                    Tails[i].FlameBoost = 0f;
                    Tails[i].TipGlowBoost = 0f;
                }
            }

            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private void TriggerBloom(Vector2 pos, float power, Color color) {
            bloomPos = pos;
            bloomPower = MathHelper.Clamp(power, 0f, 1f);
            bloomColor = color;
        }

        private void TriggerShockwave(Vector2 pos, Color color) {
            shockCenter = pos;
            shockRadius = 30f;
            shockAlpha = 0.9f;
            shockColor = color;
        }

        /// <summary>清空本 Boss 名下全部敌方弹幕 (换阶段/死亡公平阀门)。仅服务器调用。</summary>
        private void ClearOwnedProjectiles() {
            int[] types = {
                ModContent.ProjectileType<KyuubiFoxFire>(),
                ModContent.ProjectileType<KyuubiTailLance>(),
                ModContent.ProjectileType<KyuubiFoxfirePearl>(),
                ModContent.ProjectileType<KyuubiWindCrescent>(),
                ModContent.ProjectileType<KyuubiMandalaEdge>()
            };
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.hostile)
                    continue;
                for (int t = 0; t < types.Length; t++) {
                    if (p.type == types[t]) {
                        p.Kill();
                        break;
                    }
                }
            }
        }

        /// <summary>平滑悬浮到目标点。</summary>
        private void SmoothHover(Vector2 dest, float accel = 0.1f, float speedScale = 0.035f) {
            NPC.velocity = Vector2.Lerp(NPC.velocity, (dest - NPC.Center) * speedScale, accel);
        }

        /// <summary>连接节拍/施法期的可读悬浮锚点: 玩家侧上方, 保证本体在画面里。</summary>
        private Vector2 ReadableAnchor(Player target) {
            float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
            return target.Center + new Vector2(side * 250f + MathF.Sin(globalTime * 1.3f) * 30f,
                -195f + MathF.Sin(globalTime * 2f) * 16f);
        }

        /// <summary>狂化时长缩放 (终结技后连段提速 ~15%)。</summary>
        private int ET(int frames) => enraged ? (int)(frames * 0.85f) : frames;

        #endregion

        #region 尾巴姿态 (钉位通道复用)

        /// <summary>孔雀扇面姿态: 尾 i 指向 fanBase + (i-4)*spread。</summary>
        private void PinTailsFan(float baseAngle, float spread, float reach, float ext, float glow) {
            for (int i = 0; i < TailCount; i++) {
                float a = baseAngle + (i - 4) * spread;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = NPC.Center + a.ToRotationVector2() * reach;
                Tails[i].PinExtension = ext;
                Tails[i].PinGlow = glow;
            }
        }

        /// <summary>九尾光环姿态 (换阶段/终结技蓄力): 环绕本体一圈。</summary>
        private void PinTailsHalo(float radius, float rotOffset, float glow) {
            for (int i = 0; i < TailCount; i++) {
                float a = rotOffset + MathHelper.TwoPi * i / TailCount - MathHelper.PiOver2;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = NPC.Center + a.ToRotationVector2() * radius;
                Tails[i].PinExtension = 1.25f;
                Tails[i].PinGlow = glow;
            }
        }

        /// <summary>尾巴垂落姿态 (力竭喘息/死亡): 软软下垂。</summary>
        private void PinTailsDroop(float glow = 0f) {
            for (int i = 0; i < TailCount; i++) {
                float x = (i - 4) * 30f;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = NPC.Center + new Vector2(x, 200f + MathF.Abs(i - 4) * 12f);
                Tails[i].PinExtension = 0.9f;
                Tails[i].PinGlow = glow;
            }
        }

        /// <summary>紧凑收拢姿态 (镜舞: 让真身轮廓与幻影一致)。</summary>
        private void PinTailsFolded() {
            for (int i = 0; i < TailCount; i++) {
                float a = -MathHelper.PiOver2 + (i - 4) * 0.16f;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = NPC.Center + a.ToRotationVector2() * 90f;
                Tails[i].PinExtension = 0.65f;
                Tails[i].PinGlow = 0.1f;
            }
        }

        private void ReleaseTails() {
            for (int i = 0; i < TailCount; i++)
                Tails[i].Pinned = false;
        }

        /// <summary>曼陀罗钉墙: 九尾尖钉住九边形边中点 (随环旋转)。</summary>
        private void PinTailsToMandala() {
            for (int i = 0; i < TailCount; i++) {
                float a = mandalaRotation + MathHelper.TwoPi * (i + 0.5f) / 9f;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = mandalaCenter + a.ToRotationVector2() * MandalaRadiusValue;
                Tails[i].PinExtension = KyuubiTail.MaxExtensionMultiplier * 0.85f;
                Tails[i].PinGlow = 0.65f;
            }
        }

        #endregion

        #region 入场: 狐火聚形 (~210f)

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = PhaseTimer < 165;
            NPC.velocity *= 0.9f;

            if (PhaseTimer <= 2) {
                NPC.Center = target.Center + new Vector2(0, -380);
                NPC.Opacity = 0f;
                introDissolve = 1f;
            }

            // 0-40f: 九缕狐火尘从四周螺旋汇入 (因: 收束)
            if (PhaseTimer < 100 && Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 4; i++) {
                    float a = globalTime * 2.4f + i * MathHelper.TwoPi / 4f;
                    float r = MathHelper.Lerp(340f, 30f, MathHelper.Clamp(PhaseTimer / 100f, 0f, 1f));
                    Vector2 dustPos = NPC.Center + a.ToRotationVector2() * r + Main.rand.NextVector2Circular(18f, 18f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame,
                        (NPC.Center - dustPos).SafeNormalize(Vector2.Zero).RotatedBy(0.7f) * 4f, 140, default, 1.8f);
                    d.noGravity = true;
                }
            }

            // 40-100f: 本体溶解逆放显形 + 弹性缩放
            if (PhaseTimer >= 40 && PhaseTimer < 100) {
                float t = (PhaseTimer - 40) / 60f;
                introDissolve = 1f - t;
                NPC.Opacity = t;
                NPC.scale = 0.3f + ACMUtils.BackOut(t) * 0.7f;
            }
            else if (PhaseTimer >= 100) {
                introDissolve = 0f;
                NPC.Opacity = 1f;
                NPC.scale = 1f;
            }

            // 尾巴逐条弹开成孔雀扇 (6f/条)
            if (PhaseTimer >= 40 && PhaseTimer < 160) {
                for (int i = 0; i < TailCount; i++) {
                    int order = (i % 2 == 0) ? i / 2 : TailCount - 1 - i / 2; // 中心向两侧交替展开
                    if (PhaseTimer >= 46 + order * 6) {
                        float a = -MathHelper.PiOver2 + (i - 4) * 0.30f;
                        Tails[i].Pinned = true;
                        Tails[i].PinnedTarget = NPC.Center + a.ToRotationVector2() * 250f;
                        Tails[i].PinExtension = 1.4f;
                        Tails[i].PinGlow = 0.35f;
                        if ((int)PhaseTimer == 46 + order * 6) {
                            Tails[i].ApplyImpulse(a.ToRotationVector2() * 8f);
                            Tails[i].FlameBoost = 0.8f;
                            if (Main.netMode != NetmodeID.Server)
                                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.3f + order * 0.06f, Volume = 0.6f }, NPC.Center);
                        }
                    }
                }
            }

            // 100-160f: 全然静止 (威压主要由静止构成); 呼吸缩放在绘制层
            if (PhaseTimer >= 100 && PhaseTimer < 160)
                stillBreath = 0f;

            // 160f: 仰天长啸 — 唯一入场强拍
            if (PhaseTimer == 160) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
                TriggerBloom(NPC.Center, 0.85f, TelegraphColors.Gold);
                TriggerShockwave(NPC.Center, GoldFlame);
                petalPulse = 1f;
                ceremonyDecal = 1f;
                ceremonyColor = GoldFlame;
                ceremonyRadius = 320f;
            }
            if (PhaseTimer > 160)
                ceremonyDecal = MathF.Max(0f, ceremonyDecal - 0.03f);

            if (PhaseTimer > 195) {
                NPC.dontTakeDamage = false;
                ceremonyDecal = 0f;
                ReleaseTails();
                TransitionTo(BossPhase.P1_Connector);
            }
        }

        #endregion

        #region 连接节拍 (理尾 + 选招)

        private void RunConnector(Player target, bool phase2) {
            SmoothHover(ReadableAnchor(target));

            // 理尾: 辉光沿九尾逐条流过 (段落呼吸, 也是"下一招要来了"的软预告)
            for (int i = 0; i < TailCount; i++) {
                float local = PhaseTimer - i * 3f;
                Tails[i].FlameBoost = MathHelper.Clamp(1f - MathF.Abs(local - 10f) / 10f, 0f, 1f) * 0.55f;
            }

            int wait = phase2 ? ET(30) : ET(40);
            if (PhaseTimer < wait)
                return;

            // 终结技闸口: ≤25% 一次
            if (phase2 && !didFinisher && NPC.life <= NPC.lifeMax * FinisherThreshold) {
                didFinisher = true;
                TransitionTo(BossPhase.Finisher);
                return;
            }

            if (phase2) {
                BossPhase next = P2Script[p2ScriptIndex % P2Script.Length];
                p2ScriptIndex++;
                TransitionTo(next);
            }
            else {
                BossPhase next = P1Script[p1ScriptIndex % P1Script.Length];
                p1ScriptIndex++;
                TransitionTo(next);
            }
        }

        #endregion

        #region P1: 天狐问剑·扇面九连刺

        /// <summary>
        /// 招牌几何招: 扇形展开(蓄势) → 逐尖点火(倒计时) → 波纹式九连贯刺 (KyuubiTailLance 权威伤害)。
        /// 扇面在施放瞬间锁定 — 横移出扇即安全 (公平阀门)。两波, 第二波更快更窄。
        /// </summary>
        private void RunFanStab(Player target) {
            switch ((int)SubState) {
                case 0: // 布置 (服务器锁定扇面参数)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        fanBaseAngle = (target.Center - NPC.Center).ToRotation();
                        leadTail = Main.rand.Next(TailCount);
                        NPC.netUpdate = true;
                    }
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 波 A: 展扇 36f → 5f/尾 波纹贯刺 (预告 26f)
                    RunFanWave(spread: 0.32f, unfoldTime: 36, cascade: 5, telegraph: ET(26));
                    if (PhaseTimer >= 140) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            fanBaseAngle = (target.Center - NPC.Center).ToRotation();
                            leadTail = (leadTail + 4) % TailCount;
                            NPC.netUpdate = true;
                        }
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 波 B: 更窄更快 (3f/尾, 预告 22f)
                    RunFanWave(spread: 0.26f, unfoldTime: 28, cascade: 3, telegraph: ET(22));
                    if (PhaseTimer >= 118) {
                        ReleaseTails();
                        TransitionTo(BossPhase.P1_Connector);
                    }
                    break;
            }
        }

        /// <summary>扇面波公共体: 姿态钉扇 + 逐尾点火 + 贯刺弹幕与尾巴刺出对齐。</summary>
        private void RunFanWave(float spread, int unfoldTime, int cascade, int telegraph) {
            NPC.velocity *= 0.9f; // 施放期慢启动 (读招窗口)

            float unfoldT = MathHelper.Clamp(PhaseTimer / unfoldTime, 0f, 1f);
            float reach = MathHelper.Lerp(120f, 300f, ACMUtils.QuadOut(unfoldT));
            PinTailsFan(fanBaseAngle, spread, reach, 1.4f + unfoldT * 0.8f, 0.3f + unfoldT * 0.3f);

            for (int i = 0; i < TailCount; i++) {
                int order = (i - leadTail + TailCount) % TailCount;

                // 逐尖点火 (4f/尾): 倒计时读法
                int igniteAt = order * 4;
                if (PhaseTimer >= igniteAt)
                    Tails[i].FlameBoost = MathF.Min(1f, Tails[i].FlameBoost + 0.12f);

                // 贯刺: 弹幕(权威伤害+红线预告) 与尾巴刺出动作同帧启动
                int lanceAt = unfoldTime + order * cascade;
                if ((int)PhaseTimer == lanceAt) {
                    float a = fanBaseAngle + (i - 4) * spread;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<KyuubiTailLance>(), Math.Max(1, NPC.damage / 2), 2f,
                            Main.myPlayer, a, telegraph, 12f);
                    }
                    // 尾巴动作: 前摇=telegraph, 爆发 8f, 收招 30f — 尾尖爆发与光束亮起同刻
                    Tails[i].StartLongRangeStabAttack(a.ToRotationVector2(),
                        telegraph / 60f, 8f / 60f, 30f / 60f);
                }
            }

            // 逐尾点火音 (每 4f 一声, 音高沿波上行)
            if (Main.netMode != NetmodeID.Server && PhaseTimer < unfoldTime && (int)PhaseTimer % 4 == 0) {
                int step = (int)PhaseTimer / 4;
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f + step * 0.07f, Volume = 0.45f }, NPC.Center);
            }
        }

        #endregion

        #region P1/P2: 狐火天灯

        /// <summary>
        /// 蓄力 50f (72% 处粒子截止 → 静默吸气) → 3 波 ×3 盏灯笼狐火。
        /// 灯笼悬浮期金色(惰性, 可提前打掉方位), 点火后变色 (金=追踪 / 紫=直线 的固定色语言)。
        /// </summary>
        private void RunLanterns(Player target, bool dual) {
            switch ((int)SubState) {
                case 0: // 蓄力: 尾巴内卷, 金尘汇聚, 72% 截止
                    SmoothHover(ReadableAnchor(target), 0.08f, 0.03f);
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].StartCoilAttack(50f / 60f);
                        lanternWave = 0;
                    }

                    // 汇聚粒子在 36f (72%) 截止 — 最后的安静就是预告
                    if (PhaseTimer < 36 && Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(160f, 160f);
                            Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame,
                                (NPC.Center - dustPos) * 0.05f, 120, default, 1.6f);
                            d.noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 36)
                        stillBreath = 0f;

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.15f }, NPC.Center);
                    }
                    break;

                case 1: // 3 波灯笼 (18f/波), 双色模式金紫交替
                    SmoothHover(ReadableAnchor(target), 0.06f, 0.025f);

                    int waves = 3;
                    if (lanternWave < waves && PhaseTimer >= 1 + lanternWave * ET(18)) {
                        pendingLanternStyle = dual && lanternWave % 2 == 1 ? 2 : 1;
                        // 三条术士尾做发射动作 → ShouldFireProjectile 触发 FireTailProjectile
                        // (时长 0.3s → 发射点在 ~11f, 早于下一波 18f, 保证波内样式一致)
                        for (int k = 0; k < 3; k++) {
                            int idx = (leadTail + lanternWave * 3 + k) % TailCount;
                            Tails[idx].StartProjectileAttack(target.Center, 0.3f);
                        }
                        lanternWave++;
                    }

                    if (PhaseTimer >= waves * ET(18) + 40) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 收招缓冲 (灯笼各自活着, 玩家处理方位)
                    SmoothHover(ReadableAnchor(target), 0.06f, 0.025f);
                    if (PhaseTimer >= 26)
                        TransitionTo(IsPhase2 ? BossPhase.P2_Connector : BossPhase.P1_Connector);
                    break;
            }
        }

        /// <summary>尾尖发射狐火 (由尾巴 ProjectileFire 动作在 60% 进度自动触发)。</summary>
        private void FireTailProjectile(int tailIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            KyuubiTail tail = Tails[tailIndex];
            Vector2 tipPos = tail.GetTipPosition();
            int damage = Math.Max(1, NPC.damage / 3);
            Player target = Main.player[NPC.target];

            bool lanternPhase = Phase == BossPhase.P1_Lanterns || Phase == BossPhase.P2_Lanterns;
            if (lanternPhase && pendingLanternStyle == 2) {
                // 紫直线妖火: 出手即锁定玩家方向 (点火后沿此直线)
                Vector2 dir = (target.Center - tipPos).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), tipPos, dir * 3f,
                    ModContent.ProjectileType<KyuubiFoxFire>(), damage, 2f, Main.myPlayer, 0f, 0f, 2f);
            }
            else if (lanternPhase) {
                // 金灯笼: 缓漂离尾尖, 悬浮后转追踪
                Vector2 drift = (tipPos - NPC.Center).SafeNormalize(Vector2.UnitY) * 2.2f + new Vector2(0f, -1.1f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), tipPos, drift,
                    ModContent.ProjectileType<KyuubiFoxFire>(), damage, 2f, Main.myPlayer, 0f, 1f, 1f);
            }
            else {
                // 通用狐火 (慢起 → 追踪)
                Projectile.NewProjectile(NPC.GetSource_FromAI(), tipPos, tail.GetTipDirection() * 4f,
                    ModContent.ProjectileType<KyuubiFoxFire>(), damage, 2f, Main.myPlayer, 0f, 1f, 0f);
            }

            SoundEngine.PlaySound(SoundID.Item20, tipPos);
        }

        #endregion

        #region P1: 金风横扫

        /// <summary>
        /// 滑至侧翼 (30f 读招) → 鞭尾后拉 14f → 17px/f 优雅横掠穿过玩家高度,
        /// 沿途 4 尾甩出金风狐刃 (最小出手距离 300px) → 硬刹收招。掠速 >14 时有接触伤害。
        /// </summary>
        private void RunSweep(Player target) {
            switch ((int)SubState) {
                case 0: // 布置: 服务器选侧
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        sweepDir = NPC.Center.X >= target.Center.X ? -1f : 1f; // 从当前侧掠向对侧
                        NPC.netUpdate = true;
                    }
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: { // 侧翼就位 + 鞭尾后拉蓄势
                    Vector2 flank = target.Center + new Vector2(-sweepDir * 430f, -46f);
                    SmoothHover(flank, 0.12f, 0.05f);

                    // 后 14f: 鞭尾统一后拉 (反向蓄势 = 可读前摇)
                    if (PhaseTimer >= 16) {
                        float pull = MathF.Pow((PhaseTimer - 16f) / 14f, 2f);
                        for (int i = 0; i < TailCount; i++) {
                            float lift = (i - 4) * 0.11f;
                            Vector2 back = new Vector2(-sweepDir, 0f).RotatedBy(lift);
                            Tails[i].Pinned = true;
                            Tails[i].PinnedTarget = NPC.Center + back * (200f + pull * 120f);
                            Tails[i].PinExtension = 1.2f + pull * 0.5f;
                            Tails[i].PinGlow = 0.25f + pull * 0.45f;
                        }
                    }

                    if (PhaseTimer >= 30) {
                        ReleaseTails();
                        NPC.velocity = new Vector2(sweepDir * 17f, 0f);
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.1f }, NPC.Center);
                        bodySquash = -0.12f; // 前扑拉伸
                    }
                    break;
                }

                case 2: { // 横掠: 匀速穿场, 4 尾依次甩刃
                    NPC.velocity.X = sweepDir * 17f;
                    NPC.velocity.Y *= 0.9f;
                    NPC.velocity.Y += MathHelper.Clamp((target.Center.Y - 40f - NPC.Center.Y) * 0.01f, -1.2f, 1.2f);

                    if ((int)PhaseTimer % 8 == 0 && PhaseTimer <= 32) {
                        int k = (int)PhaseTimer / 8;
                        int idx = (2 + k * 2) % TailCount;
                        Tails[idx].StartSweepAttack(target.Center, MathHelper.PiOver2 * 0.9f, 0.45f);

                        // 金风狐刃: 最小出手距离 300px (反贴脸阀门)
                        if (Main.netMode != NetmodeID.MultiplayerClient &&
                            Vector2.Distance(NPC.Center, target.Center) > 300f) {
                            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                            float curve = (k % 2 == 0 ? 1f : -1f) * 0.011f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 9f,
                                ModContent.ProjectileType<KyuubiWindCrescent>(), Math.Max(1, NPC.damage / 3), 2f,
                                Main.myPlayer, curve);
                        }
                    }

                    bool crossed = sweepDir > 0f ? NPC.Center.X > target.Center.X + 430f
                                                 : NPC.Center.X < target.Center.X - 430f;
                    if (crossed || PhaseTimer >= 60) {
                        SubState = 3;
                        PhaseTimer = 0;
                        bodySquash = 0.14f; // 刹车压缩
                    }
                    break;
                }

                case 3: // 硬刹收招 + 尾巴余摆
                    NPC.velocity *= 0.85f;
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].ApplyImpulse(new Vector2(sweepDir * 6f, (i - 4) * 1.2f));
                    }
                    if (PhaseTimer >= ET(40))
                        TransitionTo(BossPhase.P1_Connector);
                    break;
            }
        }

        #endregion

        #region P1: 叩月坠砸

        /// <summary>
        /// 贝塞尔跳弧到玩家上方 (位移可见, 不闪现) → 蝎式卷尾 22f (末 6f 静默收缩) →
        /// 10f poly 骤砸提前 52f 锁定并画了红色落点法阵的地点 → 冲击环 + 放射狐火 + 反弹后坐。
        /// </summary>
        private void RunSlam(Player target) {
            switch ((int)SubState) {
                case 0: // 布置: 服务器锁定落点 (带预判, 锁定后不追踪)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 lead = target.velocity * 14f;
                        if (lead.Length() > 220f)
                            lead = lead.SafeNormalize(Vector2.Zero) * 220f;
                        slamTargetPos = target.Center + lead;
                        NPC.netUpdate = true;
                    }
                    slamArcStart = NPC.Center;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: { // 跳弧 30f: 从当前位置画弧线跃至落点上方 (读招段 1)
                    float t = MathHelper.Clamp(PhaseTimer / 30f, 0f, 1f);
                    Vector2 hoverPos = slamTargetPos + new Vector2(0f, -430f);
                    Vector2 ctrl = (slamArcStart + hoverPos) * 0.5f + new Vector2(0f, -260f);
                    Vector2 wanted = ACMUtils.BezierQuad(slamArcStart, ctrl, hoverPos, ACMUtils.SineInOut(t));
                    NPC.velocity = (wanted - NPC.Center) * 0.5f;

                    if (PhaseTimer >= 30) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 2: // 蝎式卷尾 22f (读招段 2); 末 6f 静默收缩 = 爆发前吸气
                    NPC.velocity *= 0.85f;
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].StartCoilAttack(22f / 60f);
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 0.7f }, NPC.Center);
                    }
                    if (PhaseTimer >= 16) {
                        stillBreath = 0f;
                        bodySquash = MathHelper.Lerp(bodySquash, 0.16f, 0.3f); // 收缩蓄势
                        NPC.velocity.Y = -1.6f; // 微微上提
                    }
                    if (PhaseTimer >= 22) {
                        SubState = 3;
                        PhaseTimer = 0;
                        slamArcStart = NPC.Center;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f }, NPC.Center);
                    }
                    break;

                case 3: { // 骤砸 10f: poly4 ease-out — 首帧即接近全速 (接触伤害窗口)
                    float t = MathHelper.Clamp(PhaseTimer / 10f, 0f, 1f);
                    float eased = 1f - MathF.Pow(1f - t, 4f);
                    Vector2 wanted = Vector2.Lerp(slamArcStart, slamTargetPos, eased);
                    NPC.velocity = wanted - NPC.Center;
                    bodySquash = -0.18f; // 俯冲拉伸

                    if (PhaseTimer >= 10) {
                        // 落点强拍: 震 + 环 + 法阵盛放 + 放射狐火 + 尾巴外甩
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        TriggerBloom(NPC.Center, 0.7f, TelegraphColors.Flame);
                        TriggerShockwave(NPC.Center, GoldFlame);
                        petalPulse = 1f;
                        bodySquash = 0.2f;
                        NPC.velocity = new Vector2(0f, -6f); // 砸地反坐 (质量=反作用)

                        for (int i = 0; i < TailCount; i++) {
                            float a = MathHelper.TwoPi * i / TailCount;
                            Tails[i].StartSlamAttack(NPC.Center + a.ToRotationVector2() * 260f, 0.45f);
                        }

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int damage = Math.Max(1, NPC.damage / 3);
                            for (int i = 0; i < 10; i++) {
                                float a = MathHelper.TwoPi * i / 10f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                    a.ToRotationVector2() * 5f, ModContent.ProjectileType<KyuubiFoxFire>(),
                                    damage, 2f, Main.myPlayer, 0f, 0f, 0f);
                            }
                        }

                        SubState = 4;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 4: // 反弹缓升收招
                    NPC.velocity *= 0.93f;
                    NPC.velocity.Y -= 0.06f;
                    if (PhaseTimer >= ET(45))
                        TransitionTo(BossPhase.P1_Connector);
                    break;
            }
        }

        private Vector2 slamArcStart; // 跳弧/骤砸起点 (本地插值用, 由同步的落点+确定性时序对齐)

        #endregion

        #region 换阶段: 妖狐显世 (60%, ~240f)

        private void RunPhaseTransition(Player target) {
            NPC.velocity *= 0.94f;

            if (PhaseTimer == 1) {
                // 公平阀门: 清弹
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    ClearOwnedProjectiles();
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.35f }, NPC.Center);
            }

            // 0-90f: 九尾光环 + 金粒向心汇聚 (72% = 65f 处截止)
            if (PhaseTimer < 108) {
                float t = MathHelper.Clamp(PhaseTimer / 90f, 0f, 1f);
                PinTailsHalo(MathHelper.Lerp(160f, 210f, t), globalTime * 0.8f, 0.3f + t * 0.5f);
                ceremonyDecal = t * 0.8f;
                ceremonyColor = Color.Lerp(GoldFlame, CharmPink, t);
                ceremonyRadius = 300f;

                if (PhaseTimer < 65 && Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                    for (int i = 0; i < 5; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(230f, 230f);
                        Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame,
                            (NPC.Center - dustPos) * 0.05f, 120, default, 2f);
                        d.noGravity = true;
                    }
                }
            }

            // 90-108f: 静默收缩 — 爆发前的吸气
            if (PhaseTimer >= 90 && PhaseTimer < 108) {
                stillBreath = 0f;
                NPC.scale = MathHelper.Lerp(NPC.scale, 0.94f, 0.15f);
            }

            // 108f: 妖力解放强拍 (本场第二强拍)
            if (PhaseTimer == 108) {
                NPC.scale = 1.06f;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(NPC.Center, 1f, CharmPink);
                TriggerShockwave(NPC.Center, CharmPink);
                petalPulse = 1f;
                paletteFlash = 1f; // 短暂染屏定调 (≤0.15, 走全屏名额)
                AssignTailRoles();
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].FlameBoost = 1f;
                    Tails[i].ApplyImpulse(Main.rand.NextVector2CircularEdge(7f, 7f));
                }
            }

            if (PhaseTimer > 108) {
                NPC.scale = MathHelper.Lerp(NPC.scale, 1f, 0.1f);
                ceremonyDecal = MathF.Max(0f, ceremonyDecal - 0.02f);
                ReleaseTails();
                SmoothHover(ReadableAnchor(target), 0.05f, 0.02f); // 缓慢恢复 = 玩家呼吸窗
            }

            if (PhaseTimer > 240) {
                ceremonyDecal = 0f;
                TransitionTo(BossPhase.P2_Connector);
            }
        }

        /// <summary>二阶段分尾: 三组各三 — 0-2 刺客 / 3-5 术士 / 6-8 鞭尾, 尾尖辉光色编码。</summary>
        private void AssignTailRoles() {
            for (int i = 0; i < TailCount; i++) {
                int role = i / 3;
                Tails[i].Role = role;
                Tails[i].RoleTint = RoleTints[role];
            }
        }

        #endregion

        #region P2: 魅影环舞·三段冲

        /// <summary>
        /// 每段: 悬停校准 28f (末 14f pow8 后拉急吸) → 一帧点火 46px/f ×1.02 复利 11f,
        /// 沿途布撒狐火明珠 (延时点燃, 点燃前红闪) → 8f 硬刹 → 再瞄。3 段 (狂化 4 段)。
        /// 接触伤害仅 >20px/f; 冲刺全直线 (速度=对比)。
        /// </summary>
        private void RunDash(Player target) {
            int maxDash = (Main.expertMode ? 4 : 3) + (enraged ? 1 : 0);

            switch ((int)SubState) {
                case 0:
                    dashCount = 0;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: { // 悬停校准 + 末 14f 后拉急吸 (反向蓄势)
                    float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
                    Vector2 hover = target.Center + new Vector2(side * 420f, -70f);
                    SmoothHover(hover, 0.12f, 0.05f);

                    // 锁定冲刺方向 (服务器, 提前 14f — 玩家可读)
                    if ((int)PhaseTimer == 14 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 lead = target.velocity * 12f;
                        if (lead.Length() > 200f)
                            lead = lead.SafeNormalize(Vector2.Zero) * 200f;
                        dashAngle = (target.Center + lead - NPC.Center).ToRotation();
                        NPC.netUpdate = true;
                    }

                    // 后拉急吸: pow8 — 前 10f 几乎不动, 最后骤然吸气后撤
                    if (PhaseTimer > 14) {
                        float t = (PhaseTimer - 14f) / 14f;
                        NPC.velocity -= dashAngle.ToRotationVector2() * MathF.Pow(t, 8f) * 9.5f;
                        bodySquash = MathHelper.Lerp(bodySquash, 0.15f, 0.2f);
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].FlameBoost = t;
                    }

                    if (PhaseTimer >= 28) {
                        // 一帧点火 (launch is a set, not a ramp)
                        NPC.velocity = dashAngle.ToRotationVector2() * 46f;
                        SubState = 2;
                        PhaseTimer = 0;
                        bodySquash = -0.2f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.35f, Volume = 0.8f }, NPC.Center);
                        WeaponVFX.AddScreenShake(NPC.Center, 4f);
                    }
                    break;
                }

                case 2: // 冲刺激活 11f: 复利加速 + 匀撒明珠
                    NPC.velocity *= 1.02f;

                    if (Main.netMode != NetmodeID.MultiplayerClient && (int)PhaseTimer % 2 == 0 && PhaseTimer <= 10) {
                        int pearlIdx = (int)PhaseTimer / 2;
                        // 引信错开: 冲刺结束后逐颗点燃 (二次威胁波)
                        float fuse = 46f + pearlIdx * 5f + dashCount * 4f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                            Main.rand.NextVector2Circular(1.2f, 1.2f),
                            ModContent.ProjectileType<KyuubiFoxfirePearl>(), Math.Max(1, NPC.damage / 3), 1f,
                            Main.myPlayer, fuse);
                    }

                    if (PhaseTimer >= 11) {
                        SubState = 3;
                        PhaseTimer = 0;
                        dashCount++;
                    }
                    break;

                case 3: // 硬刹 8f (slam into position) + 14f 再瞄
                    if (PhaseTimer <= 8) {
                        NPC.velocity *= 0.62f;
                        if (PhaseTimer == 1)
                            bodySquash = 0.18f;
                    }
                    else {
                        SmoothHover(ReadableAnchor(target), 0.08f, 0.03f);
                        // 距离栓绳: 冲过头立即被悬停逻辑拉回, 不绕圈
                    }

                    if (PhaseTimer >= 22) {
                        if (dashCount >= maxDash)
                            TransitionTo(BossPhase.P2_Connector);
                        else { SubState = 1; PhaseTimer = 0; }
                    }
                    break;
            }
        }

        #endregion

        #region P2: 狐影九重·镜舞

        /// <summary>
        /// 幻纱环阵: 真身与幻影入环绕玩家共舞 (辨真伪窗口: 真身尾尖狐火 + 幻影中弹溶解) →
        /// 3 轮同步突进: 幻影无害内突 (蓝纱), 真身贯穿锁定点 (红线预告 22f, 接触门 >20px/f)。
        /// </summary>
        private void RunMirror(Player target) {
            int slots = (Main.expertMode ? 6 : 5);

            switch ((int)SubState) {
                case 0: // 布阵 (服务器锁环心与真身槽位)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        mirrorCenter = target.Center;
                        mirrorCount = slots - 1;
                        trueSlot = Main.rand.Next(slots);
                        NPC.netUpdate = true;
                    }
                    mirrorOrbit = 0f;
                    for (int i = 0; i < illusionDissolve.Length; i++)
                        illusionDissolve[i] = 0f;
                    illusionAlpha = 0f;
                    lungeVisualOffset = 0f;
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: { // 幻纱淡入 + 环舞 (50f 辨真伪窗口)
                    mirrorOrbit += 0.005f;
                    UpdateIllusionSlots(slots);
                    illusionAlpha = MathHelper.Clamp(PhaseTimer / 20f, 0f, 0.72f);

                    // 真身滑入自己的槽位; 尾巴收拢 (轮廓与幻影一致), 唯尾尖狐火不灭 = 真身破绽
                    Vector2 mySlot = SlotPosition(trueSlot, slots);
                    NPC.Center = Vector2.Lerp(NPC.Center, mySlot, 0.14f);
                    NPC.velocity *= 0.8f;
                    PinTailsFolded();
                    for (int i = 0; i < TailCount; i++)
                        Tails[i].TipGlowBoost = 0.5f;

                    UpdateIllusionDissolveOnHit();

                    if (PhaseTimer >= 70) {
                        SubState = 2;
                        PhaseTimer = 0;
                        dashCount = 0;
                    }
                    break;
                }

                case 2: { // 突进预告 22f: 全员后仰蓄势, 真身画红线
                    mirrorOrbit += 0.004f;
                    UpdateIllusionSlots(slots);
                    PinTailsFolded();
                    UpdateIllusionDissolveOnHit();

                    if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        lungeTarget = target.Center;
                        dashAngle = (lungeTarget - NPC.Center).ToRotation();
                        NPC.netUpdate = true;
                    }

                    // 后仰: 全员 (真身实际移动, 幻影由可视偏移模拟)
                    float rear = MathF.Pow(MathHelper.Clamp(PhaseTimer / 22f, 0f, 1f), 4f);
                    NPC.velocity = -dashAngle.ToRotationVector2() * rear * 4f;
                    lungeVisualOffset = -rear * 40f;

                    if (PhaseTimer >= ET(22)) {
                        NPC.velocity = dashAngle.ToRotationVector2() * 38f;
                        SubState = 3;
                        PhaseTimer = 0;
                        bodySquash = -0.2f;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
                    }
                    break;
                }

                case 3: { // 突进 9f + 刹车 15f: 真身贯穿 (接触门), 幻影无害内突
                    mirrorOrbit += 0.004f;
                    UpdateIllusionSlots(slots);
                    UpdateIllusionDissolveOnHit();

                    if (PhaseTimer <= 9) {
                        lungeVisualOffset = MathHelper.Lerp(0f, 150f, MathF.Pow(PhaseTimer / 9f, 0.5f));
                        // 真身沿途布狐火 (server)
                        if (Main.netMode != NetmodeID.MultiplayerClient && (int)PhaseTimer % 4 == 1) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<KyuubiFoxFire>(), Math.Max(1, NPC.damage / 3), 2f,
                                Main.myPlayer, 0f, 0.6f, 0f);
                        }
                    }
                    else {
                        NPC.velocity *= 0.72f;
                        lungeVisualOffset = MathHelper.Lerp(lungeVisualOffset, 0f, 0.2f);
                    }

                    if (PhaseTimer >= 24) {
                        dashCount++;
                        if (dashCount >= 3) {
                            SubState = 4;
                            PhaseTimer = 0;
                        }
                        else {
                            // 回环: 真身溜回最近槽位继续演
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                trueSlot = Main.rand.Next(slots);
                                NPC.netUpdate = true;
                            }
                            SubState = 5;
                            PhaseTimer = 0;
                        }
                    }
                    break;
                }

                case 5: { // 归位间奏 18f (视觉重置 = 下一轮从干净版面开始)
                    mirrorOrbit += 0.005f;
                    UpdateIllusionSlots(slots);
                    Vector2 mySlot = SlotPosition(trueSlot, slots);
                    NPC.Center = Vector2.Lerp(NPC.Center, mySlot, 0.16f);
                    NPC.velocity *= 0.8f;
                    PinTailsFolded();
                    UpdateIllusionDissolveOnHit();
                    if (PhaseTimer >= ET(18)) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 4: // 幻影溶解谢幕
                    illusionAlpha = MathHelper.Clamp(0.72f - PhaseTimer / 25f, 0f, 1f);
                    for (int i = 0; i < mirrorCount; i++)
                        illusionDissolve[i] = MathHelper.Clamp(illusionDissolve[i] + 0.05f, 0f, 1f);
                    for (int i = 0; i < TailCount; i++)
                        Tails[i].TipGlowBoost = MathF.Max(0f, Tails[i].TipGlowBoost - 0.04f);
                    if (PhaseTimer >= 30) {
                        ReleaseTails();
                        TransitionTo(BossPhase.P2_Connector);
                    }
                    break;
            }
        }

        /// <summary>槽位世界坐标 (真身与幻影共用): 环绕 mirrorCenter 匀布 + 缓旋。</summary>
        private Vector2 SlotPosition(int slot, int slots) {
            float a = mirrorOrbit + MathHelper.TwoPi * slot / slots - MathHelper.PiOver2;
            return mirrorCenter + a.ToRotationVector2() * 340f;
        }

        /// <summary>把幻影摆到除真身槽位外的所有槽位 (突进时加内突可视偏移)。</summary>
        private void UpdateIllusionSlots(int slots) {
            int idx = 0;
            for (int s = 0; s < slots && idx < illusionPositions.Length; s++) {
                if (s == trueSlot)
                    continue;
                Vector2 pos = SlotPosition(s, slots);
                Vector2 inward = (mirrorCenter - pos).SafeNormalize(Vector2.Zero);
                illusionPositions[idx] = pos + inward * lungeVisualOffset;
                idx++;
            }
        }

        /// <summary>诱饵幻影被玩家弹幕"命中"(就近)即开始溶解 — 主动辨真伪 (本地视觉)。</summary>
        private void UpdateIllusionDissolveOnHit() {
            for (int i = 0; i < mirrorCount && i < illusionPositions.Length; i++) {
                if (illusionDissolve[i] >= 1f)
                    continue;
                bool hit = false;
                for (int p = 0; p < Main.maxProjectiles; p++) {
                    Projectile proj = Main.projectile[p];
                    if (!proj.active || !proj.friendly || proj.damage <= 0)
                        continue;
                    if (Vector2.DistanceSquared(proj.Center, illusionPositions[i]) < 70f * 70f) {
                        hit = true;
                        break;
                    }
                }
                if (hit)
                    illusionDissolve[i] = MathHelper.Clamp(illusionDissolve[i] + 0.08f, 0f, 1f);
            }
        }

        #endregion

        #region P2: 狐火曼陀罗 2.0

        /// <summary>
        /// 招牌 set-piece: 捕获玩家位置成阵, 本体入阵眼起舞 — 九墙旋转 + 缺口跳位 +
        /// 阵眼风车狐火螺旋 + 每 120f 花瓣脉冲放射。九秒致命窗, 墙伤害与亮起严格对齐。
        /// </summary>
        private void RunMandala(Player target) {
            switch ((int)SubState) {
                case 0: // 布阵: 捕获中心, 生成九边墙
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        mandalaCenter = target.Center;
                        mandalaRotation = Main.rand.NextFloat(MathHelper.TwoPi);
                        mandalaGapIndex = Main.rand.Next(9);
                        if (!mandalaSpawnedEdges) {
                            for (int i = 0; i < 9; i++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), mandalaCenter, Vector2.Zero,
                                    ModContent.ProjectileType<KyuubiMandalaEdge>(), Math.Max(1, NPC.damage / 2), 3f,
                                    Main.myPlayer, NPC.whoAmI, i);
                            }
                            mandalaSpawnedEdges = true;
                        }
                        NPC.netUpdate = true;
                    }

                    mandalaEdgeAlpha = 0f;
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, NPC.Center);
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 预告 45f: 法阵地纹旋起, 红线渐显, 本体入阵眼, 尾巴钉墙
                    NPC.Center = Vector2.Lerp(NPC.Center, mandalaCenter, 0.08f);
                    NPC.velocity *= 0.85f;
                    mandalaEdgeAlpha = MathHelper.Clamp(PhaseTimer / 45f, 0f, 1f);
                    PinTailsToMandala();

                    if (PhaseTimer >= 60) {
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        TriggerBloom(mandalaCenter, 0.8f, TelegraphColors.Flame);
                        petalPulse = 1f;
                    }
                    break;

                case 2: { // 致命窗 540f: 墙亮 + 缺口跳位 + 阵眼风车 + 花瓣脉冲
                    NPC.Center = Vector2.Lerp(NPC.Center, mandalaCenter, 0.1f);
                    NPC.velocity *= 0.9f;
                    mandalaEdgeAlpha = 1f;
                    mandalaRotation += 0.005f; // 双端确定性推进 (初值已同步)
                    PinTailsToMandala();

                    // 缺口每 100f 跳位 (确定性 +2, 音效提示)
                    if (PhaseTimer > 0 && (int)PhaseTimer % 100 == 0) {
                        mandalaGapIndex = (mandalaGapIndex + 2) % 9;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }

                    // 阵眼风车: 每 14f 一枚切向弧线狐火, 三臂螺旋 (狂化 12f); 曲率对应 ~430px 转弯半径
                    if (Main.netMode != NetmodeID.MultiplayerClient && (int)PhaseTimer % ET(14) == 0) {
                        int n = (int)PhaseTimer / ET(14);
                        float a = n * (MathHelper.TwoPi / 3f + 0.06f);
                        Vector2 dir = a.ToRotationVector2();
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 56f,
                            new Vector2(-dir.Y, dir.X) * 5.2f, ModContent.ProjectileType<KyuubiFoxFire>(),
                            Math.Max(1, NPC.damage / 3), 2f, Main.myPlayer, 0f, 0.012f, 3f);
                    }

                    // 花瓣脉冲: 每 120f 法阵盛放 + 6 枚慢速放射狐火
                    if ((int)PhaseTimer % 120 == 60) {
                        petalPulse = 1f;
                        WeaponVFX.AddScreenShake(mandalaCenter, 5f);
                        if (Main.netMode != NetmodeID.Server)
                            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f, Volume = 0.7f }, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int pulseIdx = (int)PhaseTimer / 120;
                            for (int i = 0; i < 6; i++) {
                                float a = pulseIdx * 0.35f + MathHelper.TwoPi * i / 6f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                    a.ToRotationVector2() * 3.2f, ModContent.ProjectileType<KyuubiFoxFire>(),
                                    Math.Max(1, NPC.damage / 3), 2f, Main.myPlayer, 0f, 0f, 0f);
                            }
                        }
                    }

                    if (PhaseTimer >= 540) { SubState = 3; PhaseTimer = 0; }
                    break;
                }

                case 3: // 收束: 边墙溶解淡出
                    mandalaEdgeAlpha = MathHelper.Clamp(1f - PhaseTimer / 40f, 0f, 1f);
                    NPC.velocity *= 0.94f;
                    if (PhaseTimer >= 45) {
                        mandalaSpawnedEdges = false;
                        ReleaseTails();
                        TransitionTo(BossPhase.P2_Connector);
                    }
                    break;
            }
        }

        #endregion

        #region 终结技: 万狐朝月 (≤25% 一次)

        /// <summary>
        /// 90f 大蓄力 (光环 + 汇聚 72% 截止 + 16f 全静默 + 法阵盛放) → 5 轮九方位贯刺
        /// (每轮 18f 预告, 基准角逐轮 +11° 且向玩家偏置; 3/5 轮附加狐火环) → 60f 力竭喘息奖励窗。
        /// 此后狂化: 连段提速 ~15%, 冲刺 +1 段。
        /// </summary>
        private void RunFinisher(Player target) {
            const int RoundTime = 34;
            const int Rounds = 5;

            switch ((int)SubState) {
                case 0: { // 大蓄力 90f
                    Vector2 hover = target.Center + new Vector2(0f, -300f);
                    SmoothHover(hover, 0.08f, 0.035f);

                    float t = MathHelper.Clamp(PhaseTimer / 90f, 0f, 1f);
                    PinTailsHalo(MathHelper.Lerp(150f, 230f, t), globalTime * 1.2f, 0.4f + t * 0.6f);
                    ceremonyDecal = t;
                    ceremonyColor = Color.Lerp(CharmPink, CharmViolet, 0.5f);
                    ceremonyRadius = 380f;
                    ACMScreenShakeSystem.Add(t * t * t * 5f); // 渐强震 (t³)

                    if (PhaseTimer < 65 && Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(300f, 300f);
                            Dust d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.GoldFlame : DustID.PinkFairy,
                                (NPC.Center - dustPos) * 0.055f, 120, default, 2f);
                            d.noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 74)
                        stillBreath = 0f; // 末 16f 全静默

                    if (PhaseTimer >= 90) {
                        SubState = 1;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.1f }, NPC.Center);
                        TriggerBloom(NPC.Center, 1f, CharmPink);
                        petalPulse = 1f;
                    }
                    break;
                }

                case 1: { // 5 轮九方位贯刺
                    NPC.velocity *= 0.9f;
                    ceremonyDecal = MathF.Max(0.4f, ceremonyDecal - 0.01f);

                    int round = (int)((PhaseTimer - 1) / RoundTime);
                    int local = (int)((PhaseTimer - 1) % RoundTime);

                    if (local == 0 && round < Rounds) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 基准角: 向玩家方位偏置 + 逐轮旋转 11°
                            float toPlayer = (target.Center - NPC.Center).ToRotation();
                            fanBaseAngle = toPlayer + MathHelper.ToRadians(11f) * round + MathHelper.TwoPi / 18f;
                            NPC.netUpdate = true;

                            for (int i = 0; i < TailCount; i++) {
                                float a = fanBaseAngle + MathHelper.TwoPi * i / TailCount;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<KyuubiTailLance>(), Math.Max(1, NPC.damage / 2), 2f,
                                    Main.myPlayer, a, ET(18), 10f);
                            }
                            // 3/5 轮附加狐火环
                            if (round == 2 || round == 4) {
                                for (int i = 0; i < TailCount; i++) {
                                    float a = fanBaseAngle + MathHelper.TwoPi * (i + 0.5f) / TailCount;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                        a.ToRotationVector2() * 4f, ModContent.ProjectileType<KyuubiFoxFire>(),
                                        Math.Max(1, NPC.damage / 3), 2f, Main.myPlayer, 0f, 0f, 0f);
                                }
                            }
                        }

                        for (int i = 0; i < TailCount; i++) {
                            float a = fanBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Tails[i].StartLongRangeStabAttack(a.ToRotationVector2(), ET(18) / 60f, 7f / 60f, 24f / 60f);
                        }
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f + round * 0.08f }, NPC.Center);
                        WeaponVFX.AddScreenShake(NPC.Center, 6f);
                        TriggerBloom(NPC.Center, 0.6f, CharmPink);
                    }

                    if (PhaseTimer >= Rounds * RoundTime + 10) {
                        SubState = 2;
                        PhaseTimer = 0;
                        enraged = true;
                        NPC.netUpdate = true;
                    }
                    break;
                }

                case 2: // 力竭喘息 60f: 绝对安全的奖励输出窗
                    NPC.velocity *= 0.95f;
                    NPC.velocity.Y += 0.03f; // 缓缓下沉
                    PinTailsDroop();
                    ceremonyDecal = MathF.Max(0f, ceremonyDecal - 0.03f);

                    if (Main.netMode != NetmodeID.Server && PhaseTimer % 6 == 0) {
                        // 尾尖冒烟 (力竭叙事)
                        int i = Main.rand.Next(TailCount);
                        Dust d = Dust.NewDustPerfect(Tails[i].GetTipPosition(), DustID.Smoke,
                            new Vector2(0f, -1f), 160, default, 1.2f);
                        d.noGravity = true;
                    }

                    if (PhaseTimer >= 60) {
                        ReleaseTails();
                        TransitionTo(BossPhase.P2_Connector);
                    }
                    break;
            }
        }

        #endregion

        #region 死亡演出: 狐火归天 (~300f)

        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            // 0-140f: 缓缓上浮, 九尾逐条垂落熄灭 (10f/条)
            NPC.velocity = new Vector2(0f, MathHelper.Lerp(-1.4f, 0f, MathHelper.Clamp(PhaseTimer / 200f, 0f, 1f)));

            for (int i = 0; i < TailCount; i++) {
                float local = (PhaseTimer - i * 10f) / 40f;
                float fade = MathHelper.Clamp(local, 0f, 1f);
                if (fade > 0f && Tails[i].DeathFade <= 0f && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.3f - i * 0.04f, Volume = 0.5f }, NPC.Center);
                Tails[i].DeathFade = MathF.Max(Tails[i].DeathFade, fade);
            }
            PinTailsDroop();

            // 140-180f: 幻纱溶解爬满全身, 粒子密度随进度爬升 (伤情叙事)
            if (PhaseTimer >= 140 && PhaseTimer < 180) {
                deathDissolve = (PhaseTimer - 140f) / 40f * 0.45f;
                if (Main.netMode != NetmodeID.Server && Main.rand.NextFloat() < deathDissolve) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldFlame,
                        0f, -2f, 130, default, 1.7f);
                    d.noGravity = true;
                }
                ceremonyDecal = deathDissolve;
                ceremonyColor = Color.Lerp(GoldFlame, CharmPink, 0.5f);
                ceremonyRadius = 340f;
            }

            // 180-210f: 30f 全静默 — 一切截停
            if (PhaseTimer >= 180 && PhaseTimer < 210) {
                stillBreath = 0f;
                NPC.velocity = Vector2.Zero;
                NPC.scale = MathHelper.Lerp(NPC.scale, 0.92f, 0.1f);
            }

            // 210f: 终响 (本场唯一 shake-14 + 双色泛光 + 法阵盛放)
            if (PhaseTimer == 210) {
                SoundEngine.PlaySound(SoundID.NPCDeath62, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(14f);
                TriggerBloom(NPC.Center, 1f, Color.Lerp(GoldFlame, CharmPink, 0.5f));
                TriggerShockwave(NPC.Center, CharmPink);
                petalPulse = 1f;
                paletteFlash = 1f;
                ceremonyDecal = 1f;

                if (Main.netMode != NetmodeID.Server) {
                    // 九缕狐火余烬向九方位飘散
                    for (int i = 0; i < 36; i++) {
                        float a = MathHelper.TwoPi * i / 36f;
                        Dust d = Dust.NewDustPerfect(NPC.Center, i % 2 == 0 ? DustID.GoldFlame : DustID.PinkFairy,
                            a.ToRotationVector2() * Main.rand.NextFloat(3f, 10f), 100, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }

            // 210-300f: 本体溶解上升消散
            if (PhaseTimer > 210) {
                deathDissolve = 0.45f + (PhaseTimer - 210f) / 90f * 0.55f;
                NPC.velocity = new Vector2(0f, -0.7f);
                ceremonyDecal = MathF.Max(0f, ceremonyDecal - 0.015f);
            }

            if (PhaseTimer >= 300 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.dontTakeDamage = false; // 放行终结一击
                NPC.StrikeInstantKill();    // 真正结算击杀 (CheckDead 二次进入返回 true → 掉落)
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 底层: 法阵地纹 (曼陀罗 / 坠砸落点 / 典仪节拍, 每帧至多一张)
            DrawMandalaDecal(spriteBatch);

            // 全屏节拍 (走名额契约, 每帧 ≤1): 染屏优先于泛光
            if (paletteFlash > 0.03f) {
                WeaponVFX.ApplyPaletteTint(spriteBatch,
                    shadowTint: new Color(60, 15, 45), highlightTint: new Color(255, 170, 215),
                    intensity: 0.14f * paletteFlash, saturation: 1.08f);
            }
            else if (bloomPower > 0.02f) {
                ACMShaders.DrawRadialBloomAt(bloomPos, 0.16f, bloomPower * 0.8f, bloomColor, 12f, 2.4f);
            }

            // 冲击环
            if (shockAlpha > 0.02f) {
                WeaponVFX.DrawShockwaveRing(shockCenter, shockRadius, 22f, shockAlpha, shockColor,
                    Color.Lerp(shockColor, Color.Black, 0.5f));
            }

            DrawAfterimages(spriteBatch, screenPos);
            DrawIllusions(screenPos, drawColor);
            DrawTails(spriteBatch, screenPos, drawColor);
            DrawTailFlames();
            DrawMainBody(spriteBatch, screenPos, drawColor);

            return false;
        }

        /// <summary>法阵地纹: 曼陀罗 set-piece > 坠砸落点 > 典仪节拍, 每帧只画一张。</summary>
        private void DrawMandalaDecal(SpriteBatch spriteBatch) {
            if (Main.dedServ)
                return;
            Effect fx = MandalaFx;
            if (fx == null)
                return;

            Vector2 center;
            float radius, intensity, rotation;
            int gap;
            Color primary, secondary;

            if (InMandala && mandalaEdgeAlpha > 0.02f) {
                center = mandalaCenter;
                radius = MandalaRadiusValue;
                intensity = mandalaEdgeAlpha * 0.8f;
                rotation = mandalaRotation;
                gap = mandalaGapIndex;
                primary = new Color(255, 180, 80);
                secondary = new Color(190, 70, 30);
            }
            else if (Phase == BossPhase.P1_Slam && (int)SubState >= 1 && (int)SubState <= 3) {
                // 坠砸落点: 红色警戒法阵 (提前 52f 可读)
                center = slamTargetPos;
                radius = 150f;
                float grow = (int)SubState == 1 ? MathHelper.Clamp(PhaseTimer / 30f, 0f, 1f) * 0.6f
                            : (int)SubState == 2 ? 0.6f + MathHelper.Clamp(PhaseTimer / 22f, 0f, 1f) * 0.4f : 1f;
                intensity = grow * 0.85f;
                rotation = globalTime * 0.8f;
                gap = -1;
                primary = TelegraphColors.Lethal;
                secondary = new Color(120, 20, 26);
            }
            else if (ceremonyDecal > 0.02f) {
                center = NPC.Center;
                radius = ceremonyRadius;
                intensity = ceremonyDecal * 0.7f;
                rotation = globalTime * 0.5f;
                gap = -1;
                primary = ceremonyColor;
                secondary = Color.Lerp(ceremonyColor, Color.Black, 0.55f);
            }
            else {
                return;
            }

            ACMShaders.WorldDecalParams(center, radius, out Vector2 uvCenter, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRotation"]?.SetValue(rotation);
            fx.Parameters["uGapIndex"]?.SetValue((float)gap);
            fx.Parameters["uPetalPulse"]?.SetValue(petalPulse);

            ACMShaders.DrawScreenSpaceDecal(spriteBatch, fx, BlendState.Additive);
        }

        /// <summary>速度门控残影: 只有真正的冲刺才有 (速度感=对比, 常开即噪声)。</summary>
        private void DrawAfterimages(SpriteBatch spriteBatch, Vector2 screenPos) {
            float spd = NPC.velocity.Length();
            if (spd < 18f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            float strength = MathHelper.Clamp((spd - 18f) / 24f, 0f, 1f);
            SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 1; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero)
                    continue;
                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Gold, CharmPink, phase2Tint) * progress * 0.34f * strength * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation,
                    texture.Size() / 2f, NPC.scale * progress * 0.95f, fx, 0f);
            }
        }

        /// <summary>镜舞幻影: CharmVeil 着色器批量绘制 (鬼影错位 + 流光 + 被击溶解)。</summary>
        private void DrawIllusions(Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Phase != BossPhase.P2_Mirror || illusionAlpha <= 0.02f)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Effect fx = CharmVeilFx;
            Texture2D noise = ACMShaders.NoiseTexture;
            SpriteBatch sb = Main.spriteBatch;

            if (fx == null || noise == null) {
                // 兜底: 普通半透明青紫绘制
                for (int i = 0; i < mirrorCount && i < illusionPositions.Length; i++) {
                    Color c = Color.Lerp(drawColor, new Color(160, 120, 255), 0.55f) * illusionAlpha * (1f - illusionDissolve[i]);
                    sb.Draw(texture, illusionPositions[i] - screenPos, null, c, NPC.rotation,
                        texture.Size() / 2f, NPC.scale, SpriteEffects.None, 0f);
                }
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            for (int i = 0; i < mirrorCount && i < illusionPositions.Length; i++) {
                if (illusionDissolve[i] >= 0.99f)
                    continue;
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(illusionAlpha);
                fx.Parameters["uDissolve"]?.SetValue(illusionDissolve[i]);
                fx.Parameters["uGhost"]?.SetValue(0.016f);
                fx.Parameters["uVeilColor"]?.SetValue(new Vector4(0.62f, 0.45f, 1f, 0.6f));
                fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(0.5f, 0.8f, 1f, 0.9f));
                fx.Parameters["uSeed"]?.SetValue(i * 1.618f);
                fx.CurrentTechnique.Passes[0].Apply();

                bool faceRight = mirrorCenter.X > illusionPositions[i].X;
                sb.Draw(texture, illusionPositions[i] - screenPos, null, Color.White, 0f,
                    texture.Size() / 2f, NPC.scale, faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        private void DrawTails(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Tails == null)
                return;

            for (int i = 0; i < TailCount; i++)
                Tails[i]?.DrawTelegraph(spriteBatch, screenPos);

            for (int i = 0; i < TailCount; i++)
                Tails[i]?.Draw(spriteBatch, screenPos, drawColor);
        }

        /// <summary>
        /// 尾尖狐火: KyuubiFoxFlame 着色器 9 枚一批集中绘制 (单次批切换, Immediate 逐尾设参)。
        /// 常态小火苗 = "九尾狐火"的身份符号; 点火/攻击时增高变亮 = 倒计时读法。
        /// </summary>
        private void DrawTailFlames() {
            if (Main.dedServ || Tails == null || NPC.Opacity < 0.3f)
                return;
            Effect fx = KyuubiFoxFire.FlameEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = new(noise.Width * 0.5f, noise.Height * 0.88f);

            for (int i = 0; i < TailCount; i++) {
                KyuubiTail tail = Tails[i];
                if (tail == null || tail.DeathFade > 0.9f)
                    continue;

                float flame = MathF.Max(0.3f, tail.TipFlameIntensity) * NPC.Opacity;
                if (Phase == BossPhase.Death)
                    flame = tail.TipFlameIntensity * 0.8f;
                if (flame < 0.06f)
                    continue;

                Color edgeColor = tail.RoleTint.A > 0 && tail.Role >= 0
                    ? tail.RoleTint
                    : Color.Lerp(new Color(255, 170, 60), new Color(235, 90, 180), phase2Tint);

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(flame, 0f, 1f));
                fx.Parameters["uColorCore"]?.SetValue(new Color(255, 250, 230).ToVector4());
                fx.Parameters["uColorEdge"]?.SetValue(edgeColor.ToVector4());
                fx.Parameters["uSeed"]?.SetValue(tail.FlameSeed);
                fx.Parameters["uTall"]?.SetValue(0.9f + flame * 0.5f);
                fx.CurrentTechnique.Passes[0].Apply();

                Vector2 tip = tail.GetTipPosition();
                Vector2 dir = tail.GetTipDirection();
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                float len = 0.22f + flame * 0.26f;
                sb.Draw(noise, tip + dir * 6f - Main.screenPosition, null, Color.White,
                    rot, origin, new Vector2(0.17f, len), SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 本体: 呼吸缩放 + 挤压拉伸 + 面向翻转; 入场/死亡走 CharmVeil 溶解, 二阶段罩薄纱。
        /// </summary>
        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            SpriteEffects flip = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 呼吸 + 挤压
            float breath = stillBreath > 0.5f ? 1f + 0.02f * MathF.Sin(globalTime * 2.2f) : 1f;
            Vector2 scale = new Vector2(NPC.scale * (1f - bodySquash * 0.6f), NPC.scale * (1f + bodySquash)) * breath;

            float dissolve = Phase == BossPhase.Intro ? introDissolve
                           : Phase == BossPhase.Death ? deathDissolve : 0f;
            bool useVeil = dissolve > 0.01f || phase2Tint > 0.35f;

            // 妖力辉光底衬 (加性抖动叠影)
            Color glowColor = Color.Lerp(Color.Gold, CharmPink, phase2Tint) * 0.3f * NPC.Opacity * (1f - dissolve);
            glowColor.A = 0;
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(3, 3);
                spriteBatch.Draw(texture, drawPos + offset, null, glowColor, NPC.rotation,
                    texture.Size() / 2f, scale * 1.05f, flip, 0f);
            }

            if (useVeil && CharmVeilFx != null && ACMShaders.NoiseTexture != null) {
                Effect fx = CharmVeilFx;
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
                Main.graphics.GraphicsDevice.Textures[1] = ACMShaders.NoiseTexture;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                float ghost = Phase == BossPhase.P2_Mirror ? 0.012f : 0.006f * phase2Tint + dissolve * 0.01f;
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(NPC.Opacity);
                fx.Parameters["uDissolve"]?.SetValue(dissolve);
                fx.Parameters["uGhost"]?.SetValue(ghost);
                fx.Parameters["uVeilColor"]?.SetValue(new Vector4(1f, 0.55f, 0.75f, 0.28f * phase2Tint));
                fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(1f, 0.75f, 0.35f, 1f));
                fx.Parameters["uSeed"]?.SetValue(3.7f);
                fx.CurrentTechnique.Passes[0].Apply();

                sb.Draw(texture, NPC.Center - Main.screenPosition, null, Color.White, NPC.rotation,
                    texture.Size() / 2f, scale, flip, 0f);

                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }
            else {
                Color bodyColor = drawColor * NPC.Opacity;
                spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation,
                    texture.Size() / 2f, scale, flip, 0f);
            }
        }

        #endregion
    }
}

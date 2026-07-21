using AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items;
using AncientChineseMythology.Items.Materials;
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
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭监察者 Celestial Overseer — 天庭入侵终局 Boss（机关造物 / 监察天官）。
    ///
    /// V3 重做核心：<b>"天庭的机械法眼, 精确得可怕"</b>。
    ///  - 几何运动语汇：伺服直线位移 + 近点减速斜坡 + 到位死停；瞬时 set-launch 冲刺 + ×0.62 硬刹；
    ///    悬停位取量化档位; 静止期周期性 1~2px 伺服校正微抖（纯视觉）。
    ///  - 天眼台阵构型系统：六眼在 巡航环/收拢/十字/炮列/扇面/窥视外扩 构型间弹簧-snap 展开变形,
    ///    依次就位 + 机械 click + 白闪 —— 武器台阵展开的机械仪式感; 本体与眼间绘制机关连杆。
    ///  - 演算-执行节奏：每招前摇 = 静止悬浮"演算"（OverseerCalcRing 法阵 t³ 加速运转 + 吸入尘,
    ///    末端骤停熄灭 = pre-silence）→ 一帧内全弹发射 + 反冲 → 短收招。
    ///  - 监察扫描世界层：OverseerScanline 全屏扫描线/数据流/锁定红化/故障/开机（名额契约）,
    ///    ScreenSystem 锁定框 UI 随监视槽收紧咬合。
    ///  - 三大演出：接入-降临-静默注视-台阵启动的入场; 清弹过载-骤停-重构的换阶段;
    ///    系统故障-天眼连环坠毁-核心过载-静默-终爆的死亡演出（CheckDead 拦截）。
    ///
    /// 保留的机制身份（V2）：监视槽 Surveillance Meter / 窥视相位 Scrying / 天庭陪审团 JuryTrial /
    /// 三阶段全知循环 / 签名十字激光。多人安全：AI 只依赖 ai[]/synced 字段, 随机量由 seed+attackIndex
    /// 确定性派生（机关=决定论）, 弹幕仅服务器生成。
    /// </summary>
    [AutoloadBossHead]
    internal class CelestialOverseer : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值（规则改变：加入炮列连射/缉拿冲刺攻击族）</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值（规则改变：进入全知循环，废除随机 hub）</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>天眼环绕数量</summary>
        public const int CelestialEyeCount = 6;

        /// <summary>陪审团事件持续时间（帧）</summary>
        public const int JuryDuration = 1200;

        /// <summary>裁决叠层上限</summary>
        public const int MaxVerdictStacks = 3;

        // —— 缉拿冲刺固定节拍（常量化让玩家可内化）——
        private const int DashWindup = 40;      // 后仰演算总帧
        private const int DashLockFrame = 22;   // 红色锁定线出现帧（其后方向不再变 = 18f 固定预告）
        private const int DashActive = 9;       // 冲刺持续（零转向）
        private const float DashSpeed = 66f;    // 一帧 set-launch 速度
        private const float DashMinRange = 320f;// 最小发动距离（防贴脸秒杀, 过近先闪现拉开）

        #endregion

        #region 阶段/构型枚举

        public enum BossPhase
        {
            Intro,
            Observe,                // 短促重定位/休整节拍（无喷弹），选下一攻击
            Attack_CrossLaser,      // 签名：地面十字预告 + 旋转激光面
            Attack_PillarGrid,      // 地标光柱阵
            Attack_GazeSweep,       // 带安全扇区的旋转凝视扫描
            Attack_StarVolley,      // 预判星陨（提前量 + 锁定准星）
            Attack_EyeBarrage,      // 二阶段：天眼炮列连射（V 形台阵）
            Attack_DivineDash,      // 二阶段：缉拿冲刺（reel-back + 闪现重定位）
            Scrying,                // 窥视相位（静止无敌 + 假预告 + 真实攻击）
            MarkedForJudgment,      // 监视满槽中断：单发高伤预告射线
            JuryTrial,              // 入侵终局事件：天庭陪审团
            PhaseTransition,        // 阶段过渡（清弹 + 过载重构演出）
            P3_OmniscientCycle,     // 三阶段：全知循环
            Death,                  // 死亡演出（系统崩溃）
            Attack_ScanLock         // 二阶段+：锁定扫描（探照锥搜索→锁定冻结→机关齐射）
        }

        /// <summary>天眼台阵构型 —— 部件展开变形是机关造物的"武器台阵仪式感"。</summary>
        public enum EyeFormation
        {
            Orbit,      // 巡航环
            Tuck,       // 收拢护体（冲刺/过渡/死亡）
            Cross,      // 十字阵（十字激光/审判）
            Battery,    // V 形炮列（连射）
            Fan,        // 星位扇面（星陨）
            ScryOut     // 窥视外扩
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

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // 私有状态
        private float globalTime;
        private int seed;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 监视槽机制
        private float surveillanceMeter;     // 0..100
        private bool judgmentQueued;         // 满槽后排队，于安全边界触发
        private int judgmentCooldown;        // 触发后冷却（帧）
        private bool[] eyeHasLOS;            // 每只天眼的视线状态（绘制用）
        private bool coreHasLOS;

        // 天眼台阵
        private float[] eyeAngles;           // 巡航环相位
        private Vector2[] eyeRenderPos;      // 弹簧渲染位（LOS/发射源同用）
        private Vector2[] eyeRenderVel;      // 弹簧速度
        private bool[] eyeArrived;           // 本构型是否已就位（click 一次）
        private float[] eyeSnapFlash;        // 就位白闪 0~1
        private bool[] eyeGone;              // 死亡演出中已坠毁
        private float eyeOrbitSpeed;
        private int eyeFormation;            // (EyeFormation) 同步
        private float formationTimer;        // 换构型后计帧（依次释放）
        private int prevDeployed;            // 入场部署边沿检测（本地）
        private bool scryActive;             // 窥视中：本体天眼隐藏，改由眼泡 NPC 表现

        // 窥视
        private int scryRealRemaining;
        private int eyesPopped;

        // 陪审团事件
        private bool didJury50;
        private bool didJury25;
        private int verdictStacks;
        private int[] jurorIds = new int[8];
        private int jurorCount;
        private int juryTimer;

        // 攻击序列
        private int attackIndex;
        private int attacksSinceScry;

        // 十字激光 / 凝视 / 地标
        private float crossAngle;
        private Vector2[] markerPositions = new Vector2[16];
        private int markerCount;
        private float gazeAngle;
        private float gazeDir;
        private float safeWedgeAngle;
        private const float SafeWedgeHalf = 0.55f; // 安全扇区半角（弧度）

        // 缉拿冲刺
        private Vector2 dashTarget;
        private Vector2 dashVelocity;
        private Vector2 dashReelAnchor;
        private int dashCount;
        private int maxDashCount;

        // 全知循环
        private int cycleBeat;
        private bool beatFired;
        private bool beatFired2;

        // 死亡演出
        private bool reallyDead;

        // "天网恢恢"播报（每场一次, 本地视觉）
        private bool announcedHeavensNet;

        // 视觉（纯本地, 由同步状态推导）
        private float haloRotation;
        private float haloScale = 1f;
        private float glowIntensity = 1f;
        private float divineAuraAlpha;
        private float bloomPulse;            // 处决/开火加性泛光脉冲 0~1
        private Vector2 servoJitter;         // 伺服校正微抖（绘制偏移）
        private int servoTick;

        // 演算法阵（OverseerCalcRing 驱动量, 每帧由当前招式推导）
        private float calcCharge;            // 演算进度 0~1
        private float calcFade;              // 骤停熄灭系数（1=亮, →0.12=pre-silence）
        private float calcSpin;              // CPU 积分自转角
        private bool calcDriven;             // 本帧是否有招式在驱动演算环

        // 屏幕层发布量
        private float vignettePublish;       // 监视压迫暗角 0~1
        private float runicPublish;          // 全视眼穹/审判庭法阵 0~1
        private float scanPublish;           // 扫描线强度 0~1
        private float glitchPublish;         // 故障强度 0~1
        private float bootPublish = 1f;      // 开机进度（入场）
        private float lockLethalPublish;     // 锁定框审判红化 0~1

        /// <summary>监视主题冷钢蓝。</summary>
        private static readonly Color SurveillanceBlue = new(90, 150, 215);
        /// <summary>机关玉色。</summary>
        private static readonly Color MechJade = new(110, 220, 170);

        // 专属着色器缓存（参考 Xuanwu 写法, 不注册进 ACMShaders）
        private static Asset<Effect> scanlineRef;
        private static Asset<Effect> calcRingRef;

        private static Effect GetScanlineEffect() {
            if (Main.dedServ)
                return null;
            scanlineRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/OverseerScanline", AssetRequestMode.ImmediateLoad);
            return scanlineRef?.Value;
        }

        private static Effect GetCalcRingEffect() {
            if (Main.dedServ)
                return null;
            calcRingRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/OverseerCalcRing", AssetRequestMode.ImmediateLoad);
            return calcRingRef?.Value;
        }

        public override void Unload() {
            scanlineRef = null;
            calcRingRef = null;
        }

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
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 150;
            NPC.defense = 80;
            NPC.lifeMax = 1200000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(platinum: 2);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.LunarBoss;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);

            InitializeEyes();
            eyeOrbitSpeed = 0.012f;
            eyeFormation = (int)EyeFormation.Tuck;
            prevDeployed = 0;

            haloScale = 1f;
            glowIntensity = 1f;
            divineAuraAlpha = 0f;

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write(globalTime);
            writer.Write(surveillanceMeter);
            writer.Write(judgmentQueued);
            writer.Write(judgmentCooldown);
            writer.Write(verdictStacks);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didJury50);
            writer.Write(didJury25);
            writer.Write(attackIndex);
            writer.Write(attacksSinceScry);
            writer.Write(cycleBeat);
            writer.Write(crossAngle);
            writer.Write(gazeAngle);
            writer.Write(safeWedgeAngle);
            writer.WriteVector2(dashVelocity);
            writer.Write(dashCount);
            writer.Write(scryActive);
            writer.Write(eyeFormation);
            writer.Write(reallyDead);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            globalTime = reader.ReadSingle();
            surveillanceMeter = reader.ReadSingle();
            judgmentQueued = reader.ReadBoolean();
            judgmentCooldown = reader.ReadInt32();
            verdictStacks = reader.ReadInt32();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didJury50 = reader.ReadBoolean();
            didJury25 = reader.ReadBoolean();
            attackIndex = reader.ReadInt32();
            attacksSinceScry = reader.ReadInt32();
            cycleBeat = reader.ReadInt32();
            crossAngle = reader.ReadSingle();
            gazeAngle = reader.ReadSingle();
            safeWedgeAngle = reader.ReadSingle();
            dashVelocity = reader.ReadVector2();
            dashCount = reader.ReadInt32();
            scryActive = reader.ReadBoolean();
            eyeFormation = reader.ReadInt32();
            reallyDead = reader.ReadBoolean();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>接触伤害窗口化：仅缉拿冲刺的高速帧有接触判定（伤害窗口与视觉严格对齐）。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return Phase == BossPhase.Attack_DivineDash && (int)SubState == 2 && NPC.velocity.Length() > 24f;
        }

        /// <summary>死亡演出接管：首次致死锁血 1 进入系统崩溃演出，末尾由服务器真正击杀（掉落/downed 保留）。</summary>
        public override bool CheckDead() {
            if (reallyDead)
                return true;
            if (Phase != BossPhase.Death)
                BeginDeath();
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            // 机关受击迸出电火花（轻量）
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, hit.HitDirection * 2f, -1f, 120, default, 0.9f);
                Main.dust[d].noGravity = true;
            }
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<OverseersEye>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<CelestialWatcherStaff>(),
                ModContent.ItemType<AllSeeingJadeTome>(),
                ModContent.ItemType<CelestialGearGreatsword>(),
                ModContent.ItemType<CelestialMechanismBow>(),
                ModContent.ItemType<CelestialJudgmentChakram>(),
                ModContent.ItemType<GoldenPhoenixSummonStaff>(),
                ModContent.ItemType<ClockworkPhoenixSpear>(),
                ModContent.ItemType<JadeDragonCloudDao>()
            ));
        }

        public override void OnKill() {
            DownedBossSystem.downedCelestialOverseer = true;
            // 终爆震屏已在死亡演出内给足（唯一 shake16）, 此处仅补一记轻收尾
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier mod = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 10f, 7f, 40, 2000f, FullName);
                Main.instance.CameraModifiers.Add(mod);
            }
        }

        /// <summary>窥视眼泡被击破时调用（降低监视槽 + 减少本轮真实攻击数）。</summary>
        public void OnScryingEyePopped() {
            surveillanceMeter = Math.Max(0f, surveillanceMeter - 30f);
            eyesPopped++;
            if (scryRealRemaining > 1) scryRealRemaining--;
            NPC.netUpdate = true;
        }

        #endregion

        #region 确定性派生 / 机关移动语汇

        /// <summary>由 seed+attackIndex+salt 确定性派生 0~1 —— 服务器与客户端天然一致（机关=决定论）。</summary>
        private float Derive01(int salt) {
            unchecked {
                uint h = (uint)seed * 2654435761u ^ (uint)(salt * 40503) ^ (uint)(attackIndex * 668265263);
                h ^= h >> 13;
                h *= 2246822519u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>伺服直线位移：恒速直线 + 近点减速斜坡 + 到位死停 —— "计算好的"机关移动。</summary>
        private void ServoMove(Vector2 dest, float speed) {
            Vector2 to = dest - NPC.Center;
            float d = to.Length();
            if (d < 6f) {
                NPC.velocity = Vector2.Zero;
                return;
            }
            float v = MathF.Min(speed, d * 0.16f + 1.2f);
            NPC.velocity = to / d * v;
        }

        /// <summary>伺服刹车：指数硬衰减到死停。</summary>
        private void ServoBrake() {
            NPC.velocity *= 0.82f;
            if (NPC.velocity.LengthSquared() < 0.16f)
                NPC.velocity = Vector2.Zero;
        }

        /// <summary>量化悬停档位（左/中/右三档, 高度固定）—— 拒绝 sin 摆动的果冻感。</summary>
        private Vector2 HoverSlot(Player target, int slot, float yOff = -370f)
            => new(target.Center.X + slot * 260f, target.Center.Y + yOff);

        #endregion

        #region 演算-执行框架

        /// <summary>
        /// 演算前摇驱动（每帧调用）：progress &lt; stopAt 时法阵随 t³ 加速自转、亮度随进度升温;
        /// 之后骤停熄灭（pre-silence, 尖叫前的吸气）。爆发帧后自然衰减。
        /// </summary>
        private void DriveCalc(float progress, float stopAt = 0.78f) {
            calcDriven = true;
            progress = MathHelper.Clamp(progress, 0f, 1f);
            if (progress < stopAt) {
                calcCharge = progress / stopAt;
                calcFade = MathHelper.Lerp(calcFade, 1f, 0.3f);
                calcSpin += 0.03f + calcCharge * calcCharge * calcCharge * 0.55f;
            }
            else {
                // 骤停：转速归零（不再积分）+ 快速熄灭
                calcFade = MathHelper.Lerp(calcFade, 0.12f, 0.3f);
            }
        }

        /// <summary>蓄力吸入尘：密度 ∝ √progress, 且 progress &gt; 0.72 后骤停（收气）。</summary>
        private void EmitConvergingDust(float progress) {
            if (Main.netMode == NetmodeID.Server || progress > 0.72f || progress <= 0f)
                return;
            int chance = Math.Max(1, (int)MathHelper.Lerp(7f, 2f, MathF.Sqrt(progress)));
            if (!Main.rand.NextBool(chance))
                return;
            for (int i = 0; i < 2; i++) {
                Vector2 spawn = NPC.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(180f, 340f);
                int type = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                int d = Dust.NewDust(spawn, 0, 0, type, 0, 0, 120, default, 1.1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (NPC.Center - spawn) * 0.085f;
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            globalTime += 1f / 60f;

            if (eyeAngles == null) InitializeEyes();

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if ((!target.active || target.dead) && Phase != BossPhase.Death) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 默认可受击；无敌状态由各相位自行开启
            NPC.dontTakeDamage = false;
            calcDriven = false;

            UpdateVisualEffects();
            UpdateEyeFormation(target);
            UpdateSurveillance(target);
            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Observe: RunObserve(target); break;
                case BossPhase.Attack_CrossLaser: RunCrossLaser(target); break;
                case BossPhase.Attack_PillarGrid: RunPillarGrid(target); break;
                case BossPhase.Attack_GazeSweep: RunGazeSweep(target); break;
                case BossPhase.Attack_StarVolley: RunStarVolley(target); break;
                case BossPhase.Attack_EyeBarrage: RunEyeBarrage(target); break;
                case BossPhase.Attack_DivineDash: RunDivineDash(target); break;
                case BossPhase.Scrying: RunScrying(target); break;
                case BossPhase.MarkedForJudgment: RunMarkedForJudgment(target); break;
                case BossPhase.JuryTrial: RunJuryTrial(target); break;
                case BossPhase.PhaseTransition: RunPhaseTransition(target); break;
                case BossPhase.P3_OmniscientCycle: RunOmniscientCycle(target); break;
                case BossPhase.Death: RunDeath(target); break;
                case BossPhase.Attack_ScanLock: RunScanLock(target); break;
            }

            // 未被驱动时演算环自然衰减
            if (!calcDriven) {
                calcCharge *= 0.88f;
                calcFade *= 0.92f;
            }

            // 伺服校正：静止期每 45 帧一次 1~2px 量化微抖（纯绘制偏移, 机械对位感）
            servoTick++;
            if (servoTick >= 45) {
                servoTick = 0;
                unchecked {
                    int h = (int)(Main.GameUpdateCount * 73856093u) ^ seed;
                    servoJitter = new Vector2((h & 3) - 1.5f, ((h >> 2) & 3) - 1.5f);
                }
            }

            float lightMul = Phase == BossPhase.Death && PhaseTimer >= 285 ? 0.2f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.95f, 0.7f) * glowIntensity * lightMul);
            for (int i = 0; i < CelestialEyeCount; i++) {
                if (i < EyesDeployedCount && !eyeGone[i])
                    Lighting.AddLight(GetEyePosition(i), new Vector3(0.8f, 0.9f, 1f) * 0.5f);
            }

            UpdatePresentation();
        }

        /// <summary>
        /// 屏幕层发布：暗角/法阵（ScreenSystem）+ 扫描线/故障/开机/审判红化（本体 PostDraw）
        /// + 锁定框（ScreenSystem）。纯本地视觉, 全部从已同步状态派生, 不新增 net 同步。
        /// </summary>
        private void UpdatePresentation() {
            float meterFrac = surveillanceMeter / 100f;
            bool finale = Phase == BossPhase.JuryTrial || Phase == BossPhase.MarkedForJudgment;

            // —— 监视压迫暗角 ——
            float targetVig = meterFrac * 0.7f;
            if (Phase == BossPhase.Scrying) targetVig = Math.Max(targetVig, 0.55f);
            if (finale) targetVig = Math.Max(targetVig, 0.85f);
            if (IsPhase3) targetVig = Math.Max(targetVig, 0.4f);
            if (Phase == BossPhase.Death) targetVig = 0.9f;
            vignettePublish = MathHelper.Lerp(vignettePublish, targetVig, 0.06f);

            // —— 扫描线（"被系统注视"的介质感, 随监视槽升压）——
            float targetScan = 0.2f + meterFrac * 0.5f;
            if (Phase == BossPhase.Scrying) targetScan = Math.Max(targetScan, 0.8f);
            if (Phase == BossPhase.MarkedForJudgment) targetScan = Math.Max(targetScan, 0.9f);
            if (Phase == BossPhase.Intro) targetScan = 0.55f;
            if (Phase == BossPhase.Death) targetScan = 1f;
            scanPublish = MathHelper.Lerp(scanPublish, targetScan, 0.08f);

            // —— 故障：窥视假预告 = 信号欺骗; 死亡 = 系统崩溃 ——
            float targetGlitch = 0f;
            if (Phase == BossPhase.Scrying && (int)SubState == 1)
                targetGlitch = 0.35f + 0.22f * MathF.Sin(globalTime * 21f);
            if (Phase == BossPhase.Death) {
                if (PhaseTimer < 70) targetGlitch = 0.9f;
                else if (PhaseTimer < 270) targetGlitch = 0.35f;
                else if (PhaseTimer < 285) targetGlitch = 0.05f;  // 终爆前静默
                else targetGlitch = 1f;                            // 信号中断
            }
            glitchPublish = MathHelper.Lerp(glitchPublish, MathHelper.Clamp(targetGlitch, 0f, 1f), 0.25f);

            // —— 开机（入场 0~30f 屏幕自上而下点亮）——
            bootPublish = Phase == BossPhase.Intro ? MathHelper.Clamp(PhaseTimer / 30f, 0f, 1f) : 1f;

            // —— 全视眼穹/审判庭法阵 ——
            float runic = 0f;
            float runicRadius = 360f;
            bool dome = false;
            if (Phase == BossPhase.Scrying) { runic = 0.6f; runicRadius = 380f; }
            else if (Phase == BossPhase.JuryTrial) { runic = 0.9f; runicRadius = 460f; dome = true; }
            else if (Phase == BossPhase.P3_OmniscientCycle) { runic = 0.35f; runicRadius = 420f; }
            runicPublish = MathHelper.Lerp(runicPublish, runic, 0.08f);

            // —— 泛光脉冲衰减 ——
            bloomPulse *= 0.9f;

            float warm = MathHelper.Clamp(meterFrac, 0f, 1f);
            if (finale) warm = 1f;

            // —— 锁定框：入场静默期浮现; 满槽排队时预警闪烁; 审判时全红咬合; 死亡=目标丢失 ——
            float lockAlpha = 1f;
            if (Phase == BossPhase.Intro) lockAlpha = MathHelper.Clamp((PhaseTimer - 75f) / 60f, 0f, 1f);
            if (Phase == BossPhase.Death) lockAlpha = 0f;
            float lockLethal = 0f;
            if (Phase == BossPhase.MarkedForJudgment) lockLethal = 1f;
            else if (judgmentQueued) lockLethal = 0.35f + 0.2f * MathF.Sin(globalTime * 18f);
            lockLethalPublish = MathHelper.Lerp(lockLethalPublish, lockLethal, 0.2f);

            OverseerSurveillanceScreenSystem.Publish(NPC.Center, globalTime, vignettePublish, warm,
                runicPublish, runicRadius, dome, meterFrac, lockAlpha, lockLethalPublish);
        }

        private void InitializeEyes() {
            eyeAngles = new float[CelestialEyeCount];
            eyeRenderPos = new Vector2[CelestialEyeCount];
            eyeRenderVel = new Vector2[CelestialEyeCount];
            eyeArrived = new bool[CelestialEyeCount];
            eyeSnapFlash = new float[CelestialEyeCount];
            eyeGone = new bool[CelestialEyeCount];
            eyeHasLOS = new bool[CelestialEyeCount];
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] = MathHelper.TwoPi * i / CelestialEyeCount;
                eyeRenderPos[i] = NPC.Center;
                eyeArrived[i] = true;
            }
            prevDeployed = CelestialEyeCount;
        }

        /// <summary>已部署天眼数：入场时随时间逐只弹出（由已同步的 PhaseTimer 派生, 各端一致）。</summary>
        private int EyesDeployedCount =>
            Phase == BossPhase.Intro ? (int)MathHelper.Clamp((PhaseTimer - 135f) / 13f + 1f, 0f, CelestialEyeCount) : CelestialEyeCount;

        /// <summary>切换天眼台阵构型：眼睛延迟依次弹簧-snap 就位（台阵展开仪式感）。</summary>
        private void SetFormation(EyeFormation f) {
            if ((EyeFormation)eyeFormation == f)
                return;
            eyeFormation = (int)f;
            formationTimer = 0;
            for (int i = 0; i < CelestialEyeCount; i++)
                eyeArrived[i] = false;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        /// <summary>当前构型下第 i 只天眼的锚位。</summary>
        private Vector2 EyeAnchor(int i, Player target) {
            switch ((EyeFormation)eyeFormation) {
                case EyeFormation.Tuck: {
                    float a = MathHelper.TwoPi * i / CelestialEyeCount - MathHelper.PiOver2;
                    return NPC.Center + a.ToRotationVector2() * 46f;
                }
                case EyeFormation.Cross: {
                    if (i < 4)
                        return NPC.Center + (crossAngle + MathHelper.PiOver2 * i).ToRotationVector2() * 185f;
                    float diag = crossAngle + MathHelper.PiOver4 + (i == 5 ? MathHelper.Pi : 0f);
                    return NPC.Center + diag.ToRotationVector2() * 72f;
                }
                case EyeFormation.Battery: {
                    float dir = target.Center.X >= NPC.Center.X ? 1f : -1f;
                    int col = i % 3;
                    int row = i / 3;
                    return NPC.Center + new Vector2(dir * (66f + col * 58f), (row == 0 ? -1f : 1f) * (26f + col * 17f));
                }
                case EyeFormation.Fan: {
                    float baseAng = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY).ToRotation();
                    float ang = baseAng + (i - (CelestialEyeCount - 1) * 0.5f) * 0.34f;
                    return NPC.Center + ang.ToRotationVector2() * 195f;
                }
                case EyeFormation.ScryOut:
                    return NPC.Center + eyeAngles[i].ToRotationVector2() * 320f;
                default: { // Orbit
                    float dist = 150f;
                    if (IsPhase2) dist = 180f;
                    if (IsPhase3) dist = 205f;
                    return NPC.Center + eyeAngles[i].ToRotationVector2() * dist;
                }
            }
        }

        /// <summary>
        /// 天眼台阵更新：弹簧追锚 + 依次释放 + 就位 click/白闪；死亡演出中接管为连环坠毁。
        /// </summary>
        private void UpdateEyeFormation(Player target) {
            formationTimer++;
            int deployed = EyesDeployedCount;

            // 入场部署边沿：每弹出一只 → 机械 click 音阶梯升调 + 白闪
            if (Phase == BossPhase.Intro && deployed > prevDeployed) {
                int idx = deployed - 1;
                eyeSnapFlash[idx] = 1f;
                eyeRenderPos[idx] = NPC.Center;
                eyeRenderVel[idx] = Vector2.Zero;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Mech with { Pitch = -0.4f + idx * 0.16f, Volume = 0.8f }, NPC.Center);
            }
            prevDeployed = deployed;

            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] += eyeOrbitSpeed;
                eyeSnapFlash[i] *= 0.85f;

                if (eyeGone[i])
                    continue;

                // —— 死亡演出：天眼连环失控坠毁 ——
                if (Phase == BossPhase.Death) {
                    int fallStart = 70 + i * 20;
                    if (PhaseTimer >= fallStart) {
                        if ((int)PhaseTimer == fallStart) {
                            // 失控瞬间：侧向弹出冲量
                            float side = Derive01(60 + i) > 0.5f ? 1f : -1f;
                            eyeRenderVel[i] = new Vector2(side * (2f + Derive01(70 + i) * 3f), -3f);
                            if (!Main.dedServ)
                                SoundEngine.PlaySound(SoundID.Mech with { Pitch = 0.3f - i * 0.1f, Volume = 0.7f }, eyeRenderPos[i]);
                        }
                        // 重力坠落
                        eyeRenderVel[i].Y += 0.55f;
                        eyeRenderVel[i].X *= 0.99f;
                        eyeRenderPos[i] += eyeRenderVel[i];

                        if (PhaseTimer >= fallStart + 34) {
                            eyeGone[i] = true;
                            ACMScreenShakeSystem.Add(4f);
                            if (!Main.dedServ) {
                                float pitch = MathHelper.Lerp(-0.3f, 0.6f, i / (float)(CelestialEyeCount - 1));
                                SoundEngine.PlaySound(SoundID.Item94 with { Pitch = pitch, Volume = 0.9f }, eyeRenderPos[i]);
                                for (int k = 0; k < 16; k++) {
                                    Vector2 v = Main.rand.NextVector2CircularEdge(5f, 5f);
                                    int d = Dust.NewDust(eyeRenderPos[i], 0, 0, DustID.GoldCoin, v.X, v.Y, 80, default, 1.8f);
                                    Main.dust[d].noGravity = true;
                                }
                            }
                        }
                        continue;
                    }
                }

                if (i >= deployed) {
                    // 未部署：藏于核心
                    eyeRenderPos[i] = NPC.Center;
                    eyeRenderVel[i] = Vector2.Zero;
                    continue;
                }

                Vector2 anchor = EyeAnchor(i, target);
                // 依次释放：换构型后第 i 只延迟 i*5 帧才开始移动
                if (formationTimer < i * 5)
                    anchor = eyeRenderPos[i];

                eyeRenderPos[i] = ACMUtils.SpringDamp2D(eyeRenderPos[i], anchor, ref eyeRenderVel[i], 340f, 26f, 1f / 60f);

                if (!eyeArrived[i] && formationTimer >= i * 5 && Vector2.DistanceSquared(eyeRenderPos[i], anchor) < 100f) {
                    eyeArrived[i] = true;
                    eyeSnapFlash[i] = 1f;
                    if (!Main.dedServ && Phase != BossPhase.Intro)
                        SoundEngine.PlaySound(SoundID.Mech with { Pitch = -0.2f + i * 0.08f, Volume = 0.5f }, eyeRenderPos[i]);
                }
            }
        }

        /// <summary>闪现时天眼随本体整体平移（防止弹簧甩尾横跨半屏）。</summary>
        private void ShiftEyes(Vector2 delta) {
            for (int i = 0; i < CelestialEyeCount; i++)
                eyeRenderPos[i] += delta;
        }

        private Vector2 GetEyePosition(int index) {
            if (eyeRenderPos == null)
                return NPC.Center;
            return eyeRenderPos[index];
        }

        private void UpdateVisualEffects() {
            haloRotation += 0.01f;

            // 入场静默注视期：全暗
            if (Phase == BossPhase.Intro && PhaseTimer < 135) {
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.15f, 0.1f);
                haloScale = MathHelper.Lerp(haloScale, 0.4f, 0.08f);
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0f, 0.1f);
                return;
            }
            if (Phase == BossPhase.Intro) {
                // 机械启动：halo BackOut 展开
                float t = MathHelper.Clamp((PhaseTimer - 135f) / 50f, 0f, 1f);
                haloScale = 0.4f + ACMUtils.BackOut(t) * 0.6f;
                glowIntensity = MathHelper.Lerp(0.15f, 1f, t);
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.3f * t, 0.1f);
                return;
            }
            if (Phase == BossPhase.Death) {
                // 死亡：核心过载收缩 → 熄灭
                if (PhaseTimer < 190) {
                    glowIntensity = MathHelper.Lerp(glowIntensity, 0.8f, 0.05f);
                }
                else if (PhaseTimer < 270) {
                    float t = (PhaseTimer - 190f) / 80f;
                    haloScale = MathHelper.Lerp(haloScale, 0.4f, 0.04f);
                    glowIntensity = MathHelper.Lerp(glowIntensity, 1.6f * t + 0.6f, 0.1f);
                }
                else {
                    glowIntensity = MathHelper.Lerp(glowIntensity, 0.05f, 0.3f);
                    divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0f, 0.3f);
                }
                return;
            }

            if (IsPhase3) {
                haloScale = 1.5f + MathF.Sin(globalTime * 4f) * 0.2f;
                glowIntensity = 1.5f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.8f, 0.05f);
            }
            else if (IsPhase2) {
                haloScale = 1.2f + MathF.Sin(globalTime * 3f) * 0.1f;
                glowIntensity = 1.2f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.5f, 0.05f);
            }
            else {
                haloScale = 1f + MathF.Sin(globalTime * 2f) * 0.05f;
                glowIntensity = 1f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.3f, 0.05f);
            }
        }

        /// <summary>监视槽：处于核心或 ≥4 只天眼的直视线内时上升；满槽排队审判。</summary>
        private void UpdateSurveillance(Player target) {
            if (judgmentCooldown > 0) judgmentCooldown--;

            coreHasLOS = Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1);
            int eyesWithLOS = 0;
            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i);
                bool los = Collision.CanHitLine(eyePos, 1, 1, target.Center, 1, 1);
                eyeHasLOS[i] = los;
                if (los) eyesWithLOS++;
            }

            bool watched = coreHasLOS || eyesWithLOS >= 4;

            // 窥视/陪审/过渡/审判/入场/死亡时不积累监视
            bool accumulate = Phase != BossPhase.Scrying && Phase != BossPhase.JuryTrial
                && Phase != BossPhase.PhaseTransition && Phase != BossPhase.MarkedForJudgment
                && Phase != BossPhase.Intro && Phase != BossPhase.Death;

            if (accumulate && judgmentCooldown <= 0) {
                float rise = 0.55f;
                if (IsPhase2) rise += 0.15f;
                if (IsPhase3) rise += 0.20f;
                rise += verdictStacks * 0.10f;

                // 扫描锁定锥内额外加压（信息与威胁同源：站在探照灯里 = 被系统重点记录）
                if (InActiveLockCone(target)) {
                    watched = true;
                    rise += 0.5f;
                }

                if (watched) surveillanceMeter += rise;
                else surveillanceMeter -= 1.25f; // 破除视线快速回落，奖励走位
            }

            surveillanceMeter = MathHelper.Clamp(surveillanceMeter, 0f, 100f);

            if (surveillanceMeter >= 100f && !judgmentQueued && judgmentCooldown <= 0 && Phase != BossPhase.Death) {
                judgmentQueued = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        private bool IsInterruptible() {
            return Phase == BossPhase.Observe
                || Phase == BossPhase.Attack_CrossLaser
                || Phase == BossPhase.Attack_PillarGrid
                || Phase == BossPhase.Attack_GazeSweep
                || Phase == BossPhase.Attack_StarVolley
                || Phase == BossPhase.Attack_EyeBarrage
                || Phase == BossPhase.Attack_DivineDash
                || Phase == BossPhase.Attack_ScanLock;
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 && IsInterruptible()) {
                didPhase2Transition = true;
                TransitionTo(BossPhase.PhaseTransition);
            }
            else if (!didPhase3Transition && IsPhase3 && IsInterruptible()) {
                didPhase3Transition = true;
                TransitionTo(BossPhase.PhaseTransition);
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            beatFired = false;
            beatFired2 = false;
            NPC.alpha = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        /// <summary>返回主循环：三阶段进入全知循环，否则进入观测节拍。</summary>
        private void ReturnToHub() {
            if (IsPhase3) {
                cycleBeat = 0;
                TransitionTo(BossPhase.P3_OmniscientCycle);
            }
            else {
                TransitionTo(BossPhase.Observe);
            }
        }

        /// <summary>清除本 Boss 的全部敌对弹幕（换阶段/死亡公平阀门）。</summary>
        private void ClearOwnProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int[] types = [
                ModContent.ProjectileType<HolyOrb>(),
                ModContent.ProjectileType<CelestialEyeBeam>(),
                ModContent.ProjectileType<DivineLightPillar>(),
                ModContent.ProjectileType<CelestialStar>(),
                ModContent.ProjectileType<HolyHaloRing>(),
                ModContent.ProjectileType<DivineDeathRay>(),
                ModContent.ProjectileType<OmegaCelestialLaser>(),
                ModContent.ProjectileType<CrossLaserBeam>(),
                ModContent.ProjectileType<SweepingLaserBolt>(),
                ModContent.ProjectileType<MinionSyncLaser>(),
                ModContent.ProjectileType<JudgmentBeam>(),
                ModContent.ProjectileType<OverseerGroundTelegraph>(),
                ModContent.ProjectileType<OverseerLockCone>(),
                ModContent.ProjectileType<OverseerNetBeam>()
            ];
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && Array.IndexOf(types, p.type) >= 0)
                    p.Kill();
            }
        }

        #endregion

        #region 入场演出（接入→降临→静默注视→台阵启动→定音）

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;

            if (PhaseTimer <= 75) {
                // 「接入/降临」：屏幕开机 + fake-Z 从深空立方收敛冲向镜头（绘制层处理缩放/透明）
                NPC.Center = target.Center + new Vector2(0, -380);
                NPC.velocity = Vector2.Zero;

                if ((int)PhaseTimer == 75) {
                    // 「到位」硬定帧
                    ACMScreenShakeSystem.Add(6f);
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 26; i++) {
                            Vector2 v = Main.rand.NextVector2CircularEdge(7f, 7f);
                            int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, v.X, v.Y, 100, default, 1.6f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                }
                return;
            }

            // 「静默注视」75~135：完全静止, 全暗, 锁定框缓缓咬合到玩家（威压来自静止）
            NPC.velocity = Vector2.Zero;
            if ((int)PhaseTimer == 100 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Camera with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);

            // 「机械启动」135~215：天眼依次弹出（EyesDeployedCount/UpdateEyeFormation 处理 click/白闪）
            if ((int)PhaseTimer == 215) {
                // 「定音」：台阵展开完毕 + 战斗播报（文字与色彩双通道可读）
                SetFormation(EyeFormation.Orbit);
                if (!Main.dedServ) {
                    string text = Terraria.Localization.Language.GetTextValue("Mods.AncientChineseMythology.NPCs.CelestialOverseer.Arrival");
                    CombatText.NewText(NPC.getRect(), TelegraphColors.Gold, text, true);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                bloomPulse = 1f;
                calcCharge = 1f;
                calcFade = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 40; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(10f, 10f);
                        int type = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                        int d = Dust.NewDust(NPC.Center, 0, 0, type, v.X, v.Y, 60, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 285) TransitionTo(BossPhase.Observe);
        }

        /// <summary>入场 fake-Z：30~75f 由 z=7 立方收敛至 0（从背景飞向镜头）。</summary>
        private float IntroZ() {
            if (Phase != BossPhase.Intro) return 0f;
            if (PhaseTimer <= 30) return 7f;
            if (PhaseTimer >= 75) return 0f;
            float inv = 1f - (PhaseTimer - 30f) / 45f;
            return 7f * inv * inv * inv;
        }

        #endregion

        #region 观测/选招

        private void RunObserve(Player target) {
            if ((int)PhaseTimer == 1)
                SetFormation(EyeFormation.Orbit);

            // 量化悬停档位（机关: 位置是"选"出来的, 不是漂出来的）
            int slot = attackIndex % 3 - 1;
            ServoMove(HoverSlot(target, slot), 19f);
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.014f, 0.1f);

            // 注视线尘（监察氛围）
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                int i = (int)(PhaseTimer / 4) % CelestialEyeCount;
                Vector2 eyePos = GetEyePosition(i);
                Vector2 dir = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                Vector2 dp = eyePos + dir * Main.rand.NextFloat(0, 400);
                int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 180, default, 0.7f);
                Main.dust[dust].noGravity = true;
            }

            if (PhaseTimer >= 46) {
                SelectNextAction();
            }
        }

        /// <summary>在安全边界选下一步：优先审判/陪审，否则确定性轮换攻击 + 周期窥视。</summary>
        private void SelectNextAction() {
            if (judgmentQueued) { TransitionTo(BossPhase.MarkedForJudgment); return; }
            if (TryStartJury()) return;

            attacksSinceScry++;
            int scryInterval = IsPhase2 ? 2 : 3;
            if (attacksSinceScry >= scryInterval) {
                attacksSinceScry = 0;
                TransitionTo(BossPhase.Scrying);
                return;
            }

            // 手工编排循环（PACING §2）：场控招与压制招交替, 序列本身即编排
            BossPhase[] list = IsPhase2
                ? new[] { BossPhase.Attack_CrossLaser, BossPhase.Attack_EyeBarrage, BossPhase.Attack_ScanLock, BossPhase.Attack_PillarGrid, BossPhase.Attack_DivineDash, BossPhase.Attack_GazeSweep, BossPhase.Attack_StarVolley }
                : new[] { BossPhase.Attack_CrossLaser, BossPhase.Attack_PillarGrid, BossPhase.Attack_ScanLock, BossPhase.Attack_GazeSweep, BossPhase.Attack_StarVolley };

            BossPhase next = list[attackIndex % list.Length];
            attackIndex++;
            TransitionTo(next);
        }

        /// <summary>50%/25% 触发陪审团事件。</summary>
        private bool TryStartJury() {
            if (!didJury50 && NPC.life < NPC.lifeMax * 0.5f) { didJury50 = true; TransitionTo(BossPhase.JuryTrial); return true; }
            if (!didJury25 && NPC.life < NPC.lifeMax * 0.25f) { didJury25 = true; TransitionTo(BossPhase.JuryTrial); return true; }
            return false;
        }

        #endregion

        #region 签名：十字审察

        private void RunCrossLaser(Player target) {
            switch ((int)SubState) {
                case 0: // 演算 64f：十字构型展开 + 地面十字预告 + 演算环
                    ServoMove(HoverSlot(target, 0, -330f), 17f);

                    if ((int)PhaseTimer == 1) {
                        crossAngle = Derive01(11) * MathHelper.PiOver4;
                        SetFormation(EyeFormation.Cross);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f }, NPC.Center);
                        SpawnCrossTelegraph(crossAngle, 1500f, 64);
                    }

                    float prog = PhaseTimer / 64f;
                    DriveCalc(prog);
                    EmitConvergingDust(prog);

                    if (Main.netMode != NetmodeID.Server && prog < 0.72f) {
                        for (int i = 0; i < 4; i++) {
                            float a = crossAngle + MathHelper.PiOver2 * i;
                            Vector2 d = a.ToRotationVector2();
                            Vector2 dp = NPC.Center + d * Main.rand.NextFloat(0, 700);
                            int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 64) {
                        SubState = 1; PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 4; i++) {
                                float a = crossAngle + MathHelper.PiOver2 * i;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<CrossLaserBeam>(), NPC.damage / 2, 0f, Main.myPlayer,
                                    ai0: NPC.whoAmI, ai1: a);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(10f);
                        bloomPulse = 1f;
                    }
                    break;

                case 1: // 开火：本体完全锁定（激光旋转是画面里唯一的运动 → 读数清晰）
                    NPC.velocity = Vector2.Zero;
                    if (PhaseTimer > 155) {
                        SetFormation(EyeFormation.Orbit);
                        ReturnToHub();
                    }
                    break;
            }
        }

        #endregion

        #region 地标光柱阵

        private void RunPillarGrid(Player target) {
            switch ((int)SubState) {
                case 0: // 地标预告 64f
                    ServoBrake();
                    if ((int)PhaseTimer == 1) {
                        markerCount = (Main.expertMode ? 6 : 4) + verdictStacks;
                        if (markerCount > markerPositions.Length) markerCount = markerPositions.Length;
                        for (int i = 0; i < markerCount; i++) {
                            float offsetX = (i - (markerCount - 1) / 2f) * 200f + (Derive01(21 + i) - 0.5f) * 40f;
                            markerPositions[i] = new Vector2(target.Center.X + offsetX, target.Center.Y + 50);
                            SpawnPillarTelegraph(markerPositions[i], 64);
                        }
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }
                    DriveCalc(PhaseTimer / 64f);
                    EmitConvergingDust(PhaseTimer / 64f);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < markerCount; i++) {
                            int dust = Dust.NewDust(markerPositions[i] + new Vector2(-20, -500), 40, 500, DustID.GoldCoin, 0, 2f, 100, default, 0.8f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 64) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 落柱：错相 3f 依次砸落（ripple 而非墙）
                    if ((int)PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                    }
                    if (Main.netMode != NetmodeID.MultiplayerClient && (int)(PhaseTimer - 1) % 3 == 0) {
                        int idx = (int)(PhaseTimer - 1) / 3;
                        if (idx < markerCount) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                new Vector2(markerPositions[idx].X, markerPositions[idx].Y - 800), new Vector2(0, 25f),
                                ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                        }
                    }
                    if (PhaseTimer > 60 + markerCount * 3) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 带安全扇区的旋转凝视扫描

        private void RunGazeSweep(Player target) {
            switch ((int)SubState) {
                case 0: // 预告 50f：确定安全扇区方向
                    ServoMove(HoverSlot(target, 0, -360f), 15f);
                    if ((int)PhaseTimer == 1) {
                        gazeAngle = Derive01(17) * MathHelper.TwoPi;
                        gazeDir = Derive01(18) > 0.5f ? 1f : -1f;
                        // 安全扇区朝向玩家当前侧，给一个可站位的缝
                        safeWedgeAngle = (target.Center - NPC.Center).ToRotation();
                        SpawnSafeWedgeTelegraph(safeWedgeAngle, 1400f, 50);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f }, NPC.Center);
                        eyeOrbitSpeed = 0.03f;
                    }
                    DriveCalc(PhaseTimer / 50f, 0.85f);
                    if (PhaseTimer >= 50) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 扫描：双臂螺旋点射（旋涡波纹, 非同心墙），仅保留安全扇区缺口
                    NPC.velocity = Vector2.Zero;
                    gazeAngle += gazeDir * 0.075f;

                    if (PhaseTimer % 2 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int arm = 0; arm < 2; arm++) {
                            float a = gazeAngle + MathHelper.Pi * arm;
                            float diff = MathHelper.WrapAngle(a - safeWedgeAngle);
                            if (Math.Abs(diff) < SafeWedgeHalf) continue; // 安全扇区缺口
                            Vector2 vel = a.ToRotationVector2() * 8f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<HolyOrb>(), NPC.damage / 3, 1f, Main.myPlayer, ai0: 1f);
                        }
                        if (PhaseTimer % 14 == 0)
                            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
                    }

                    // 持续标示安全扇区缝
                    if (Main.netMode != NetmodeID.Server) {
                        Vector2 d = safeWedgeAngle.ToRotationVector2();
                        Vector2 dp = NPC.Center + d * Main.rand.NextFloat(0, 800);
                        int dust = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(180, 220, 255), 0.9f);
                        Main.dust[dust].noGravity = true;
                    }

                    if (PhaseTimer > 170) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 预判星陨（锁定准星）

        private void RunStarVolley(Player target) {
            switch ((int)SubState) {
                case 0: // 演算 62f：玩家周围布下锁定准星（发射位即准星, 完全可读）
                    ServoBrake();
                    if ((int)PhaseTimer == 1) {
                        int count = (Main.expertMode ? 7 : 5) + verdictStacks;
                        if (count > markerPositions.Length) count = markerPositions.Length;
                        markerCount = count;
                        float baseA = Derive01(19) * MathHelper.TwoPi;
                        for (int i = 0; i < count; i++) {
                            float angle = baseA + MathHelper.TwoPi * i / count + (Derive01(20 + i) - 0.5f) * 0.3f;
                            float dist = 430f + (Derive01(40 + i) - 0.5f) * 80f;
                            markerPositions[i] = target.Center + angle.ToRotationVector2() * dist;
                        }
                        SetFormation(EyeFormation.Fan);
                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.3f }, NPC.Center);
                    }
                    DriveCalc(PhaseTimer / 62f);
                    EmitConvergingDust(PhaseTimer / 62f);
                    if (PhaseTimer >= 62) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 执行：一帧内全弹齐射（预判提前量）+ 整体反冲
                    if ((int)PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < markerCount; i++) {
                                Vector2 dir = ACMUtils.LeadTarget(markerPositions[i], target.Center, target.velocity, 14f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), markerPositions[i], dir * 14f,
                                    ModContent.ProjectileType<CelestialStar>(), NPC.damage / 2, 3f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        bloomPulse = 0.6f;
                        NPC.velocity = new Vector2(0f, -9f); // 齐射整体上顿反冲
                    }
                    ServoBrake();
                    if (PhaseTimer > 50) {
                        SetFormation(EyeFormation.Orbit);
                        markerCount = 0;
                        ReturnToHub();
                    }
                    break;
            }
        }

        #endregion

        #region 二阶段：天眼炮列 + 缉拿冲刺

        private void RunEyeBarrage(Player target) {
            ServoMove(HoverSlot(target, 0, -300f), 15f);

            if ((int)PhaseTimer == 1) {
                SetFormation(EyeFormation.Battery);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f }, NPC.Center);
            }

            // 台阵展开期 26f
            if (PhaseTimer < 26) return;

            int t = (int)PhaseTimer - 26;
            const int perEye = 12;

            if (t < CelestialEyeCount * perEye) {
                int idx = t / perEye;
                int localT = t % perEye;
                Vector2 eyePos = GetEyePosition(idx);

                // 注视线预告
                if (Main.netMode != NetmodeID.Server && localT < 8) {
                    Vector2 dir = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                    Vector2 dp = eyePos + dir * Main.rand.NextFloat(0, 350);
                    int dust = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(200, 220, 255), 1f);
                    Main.dust[dust].noGravity = true;
                }
                if (localT == perEye - 1) {
                    Vector2 toT = (target.Center - eyePos).SafeNormalize(Vector2.UnitY);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, toT * 10f,
                            ModContent.ProjectileType<CelestialEyeBeam>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                    // 单眼后坐（质量=反作用）
                    eyeRenderVel[idx] -= toT * 17f;
                    eyeSnapFlash[idx] = 0.7f;
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f }, eyePos);
                }
            }
            else if (t == CelestialEyeCount * perEye + 4) {
                bloomPulse = 0.5f; // 齐射预告
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.8f, Volume = 0.9f }, NPC.Center);
            }
            else if (t == CelestialEyeCount * perEye + 12) {
                // 末轮：六眼同帧齐射
                for (int i = 0; i < CelestialEyeCount; i++) {
                    Vector2 eyePos = GetEyePosition(i);
                    Vector2 toT = (target.Center - eyePos).SafeNormalize(Vector2.UnitY);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, toT * 13f,
                            ModContent.ProjectileType<MinionSyncLaser>(), NPC.damage / 2, 2f, Main.myPlayer);
                    }
                    eyeRenderVel[i] -= toT * 22f;
                    eyeSnapFlash[i] = 1f;
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(9f);
            }

            if (t > CelestialEyeCount * perEye + 40) {
                SetFormation(EyeFormation.Orbit);
                ReturnToHub();
            }
        }

        private void RunDivineDash(Player target) {
            // 保底出口：异常卡死时强制返回
            if (AttackTimer > 900) { SetFormation(EyeFormation.Orbit); ReturnToHub(); return; }

            switch ((int)SubState) {
                case 0: // 初始化
                    dashCount = 0;
                    maxDashCount = Main.expertMode ? 4 : 3;
                    SetFormation(EyeFormation.Tuck);
                    SubState = 1; PhaseTimer = 0;
                    break;

                case 1: // 演算后仰 40f：pow8 late-snap 反向蓄力 + 22f 处红色锁定线（其后方向不变）
                    if ((int)PhaseTimer == 1) {
                        // 最小发动距离阀门：过近先闪现拉开
                        if (Vector2.Distance(NPC.Center, target.Center) < DashMinRange) {
                            SubState = 4; PhaseTimer = 0;
                            break;
                        }
                        dashReelAnchor = NPC.Center;
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.1f, Volume = 0.8f }, NPC.Center);
                    }

                    float rt = PhaseTimer / (float)DashWindup;
                    Vector2 away = (dashReelAnchor - target.Center).SafeNormalize(-Vector2.UnitY);
                    ServoMove(dashReelAnchor + away * MathF.Pow(rt, 8f) * 120f, 30f);

                    if ((int)PhaseTimer == DashLockFrame) {
                        // 锁定：预判提前量, 之后不再追踪（可读的直线）
                        dashTarget = target.Center + target.velocity * 12f;
                        dashVelocity = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * DashSpeed;
                        SpawnDashTelegraph(dashVelocity.ToRotation(), DashWindup - DashLockFrame);
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.5f, Volume = 1.1f }, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                    }

                    if (Main.netMode != NetmodeID.Server && PhaseTimer < DashLockFrame) {
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        Vector2 dp = NPC.Center + dir * Main.rand.NextFloat(0, 500);
                        int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1f);
                        Main.dust[dust].noGravity = true;
                    }

                    if (PhaseTimer >= DashWindup) {
                        SubState = 2; PhaseTimer = 0;
                        NPC.velocity = dashVelocity; // 一帧 set-launch
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                    }
                    break;

                case 2: // 冲刺 9f：零转向（速度门控拖影自动亮起）
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dp = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f * i;
                            int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                    }
                    if (PhaseTimer >= DashActive) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3: // 硬刹 ×0.62（"砸进定位"的机关质感）
                    NPC.velocity *= 0.62f;
                    if (PhaseTimer >= 14) {
                        NPC.velocity = Vector2.Zero;
                        dashCount++;
                        if (dashCount >= maxDashCount) {
                            SetFormation(EyeFormation.Orbit);
                            ReturnToHub();
                        }
                        else { SubState = 4; PhaseTimer = 0; }
                    }
                    break;

                case 4: // 闪现遁出：淡出 + 旧位残闪（删除"飞回"死时间）
                    NPC.velocity = Vector2.Zero;
                    NPC.alpha = Math.Min(255, NPC.alpha + 36);
                    if ((int)PhaseTimer == 1 && Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 14; i++) {
                            Vector2 v = Main.rand.NextVector2CircularEdge(5f, 5f);
                            int d = Dust.NewDust(NPC.Center, 0, 0, DustID.YellowStarDust, v.X, v.Y, 80, default, 1.5f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 8) {
                        float side = Derive01(50 + dashCount) > 0.5f ? 1f : -1f;
                        Vector2 offset = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX).RotatedBy(side * 2.4f);
                        Vector2 newPos = target.Center + offset * 540f;
                        ShiftEyes(newPos - NPC.Center);
                        NPC.Center = newPos;
                        if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, newPos);
                        SubState = 5; PhaseTimer = 0;
                    }
                    break;

                case 5: // 遁入：淡入 8f → 回到后仰演算
                    NPC.velocity = Vector2.Zero;
                    NPC.alpha = Math.Max(0, NPC.alpha - 40);
                    if ((int)PhaseTimer == 1 && Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 14; i++) {
                            Vector2 v = Main.rand.NextVector2CircularEdge(4f, 4f);
                            int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, v.X, v.Y, 80, default, 1.4f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 8) {
                        NPC.alpha = 0;
                        SubState = 1; PhaseTimer = 0;
                    }
                    break;
            }
        }

        #endregion

        #region 锁定扫描（探照锥搜索→锁定冻结→机关齐射）

        /// <summary>
        /// 锁定扫描：双探照锥（OverseerLockCone, 冷钢蓝无伤）自两侧向玩家扫掠, 照住累计 30f 或超时
        /// 即锁定（金色收窄 + 咔哒）, 20f 固定逃逸窗后沿冻结锥心扇形齐射。锥内玩家监视槽额外上升
        /// （信息与威胁同源）—— 监视主题的核心演出。锥体自治, 本体保持横移施压。
        /// </summary>
        private void RunScanLock(Player target) {
            // 保底出口
            if (AttackTimer > 600) { SetFormation(EyeFormation.Orbit); ReturnToHub(); return; }

            switch ((int)SubState) {
                case 0: // 展开 24f：升至高位 + 扇面构型 + 演算
                    ServoMove(HoverSlot(target, 0, -430f), 17f);
                    if ((int)PhaseTimer == 1) {
                        SetFormation(EyeFormation.Fan);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.1f, Volume = 1.1f }, NPC.Center);
                    }
                    DriveCalc(PhaseTimer / 24f, 0.9f);
                    if (PhaseTimer >= 24) {
                        SubState = 1; PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float baseA = (target.Center - NPC.Center).ToRotation();
                            for (int s = -1; s <= 1; s += 2) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<OverseerLockCone>(), NPC.damage / 3, 0f, Main.myPlayer,
                                    ai0: NPC.whoAmI, ai1: baseA + s * 1.05f, ai2: 0f);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.4f }, NPC.Center);
                    }
                    break;

                case 1: // 锥体自治期：本体缓慢横移施压; 锥全灭或超时 → 收招
                    ServoMove(HoverSlot(target, (int)(MathF.Sin(globalTime * 0.8f) * 1.4f), -430f), 12f);
                    if (PhaseTimer > 30 && (!AnyLockConesAlive() || PhaseTimer > 300)) {
                        SubState = 2; PhaseTimer = 0;
                    }
                    break;

                case 2: // 收招
                    ServoBrake();
                    if (PhaseTimer > 30) {
                        SetFormation(EyeFormation.Orbit);
                        ReturnToHub();
                    }
                    break;
            }
        }

        private bool AnyLockConesAlive() {
            int type = ModContent.ProjectileType<OverseerLockCone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) return true;
            }
            return false;
        }

        /// <summary>玩家是否处于本体的活动扫描锥内（监视槽加压：信息与威胁同源）。</summary>
        private bool InActiveLockCone(Player target) {
            if (Phase != BossPhase.Attack_ScanLock)
                return false;
            int type = ModContent.ProjectileType<OverseerLockCone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type || (int)p.ai[0] != NPC.whoAmI) continue;
                Vector2 d = target.Center - p.Center;
                float halfA = OverseerLockCone.SearchHalfAngle * (p.ai[2] >= 1f ? 0.55f : 1f);
                if (d.Length() < OverseerLockCone.ConeLength
                    && Math.Abs(MathHelper.WrapAngle(d.ToRotation() - p.ai[1])) < halfA)
                    return true;
            }
            return false;
        }

        #endregion

        #region 窥视相位（签名）

        private void RunScrying(Player target) {
            NPC.dontTakeDamage = true; // 静止无敌
            ServoBrake();
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.06f, 0.05f);

            switch ((int)SubState) {
                case 0: // 脱离：眼睛外扩并生成可击破眼泡
                    scryActive = true;
                    if ((int)PhaseTimer == 1) {
                        SetFormation(EyeFormation.ScryOut);
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f }, NPC.Center);
                        eyesPopped = 0;
                        scryRealRemaining = 2;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < CelestialEyeCount; i++) {
                                Vector2 sp = GetEyePosition(i);
                                NPC.NewNPC(NPC.GetSource_FromAI(), (int)sp.X, (int)sp.Y,
                                    ModContent.NPCType<OverseerScryingEye>(), ai0: NPC.whoAmI, ai1: i);
                            }
                        }
                    }
                    if (PhaseTimer >= 30) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 假预告（纯尘，无伤; 屏幕故障闪烁揭示"这是信号欺骗"）—— 学会后可读
                    if (Main.netMode != NetmodeID.Server) {
                        if (PhaseTimer % 8 == 0) {
                            int fake = (int)(PhaseTimer / 8) % 3;
                            switch (fake) {
                                case 0: // 假十字
                                    for (int i = 0; i < 4; i++) {
                                        float a = globalTime + MathHelper.PiOver2 * i;
                                        for (int j = 0; j < 6; j++) {
                                            Vector2 dp = target.Center + a.ToRotationVector2() * (j * 90);
                                            int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 200, default, 0.7f);
                                            Main.dust[d].noGravity = true;
                                        }
                                    }
                                    break;
                                case 1: // 假光柱
                                    for (int i = -2; i <= 2; i++) {
                                        Vector2 dp = target.Center + new Vector2(i * 160, Main.rand.NextFloat(-250, 0));
                                        int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 2f, 200, default, 0.7f);
                                        Main.dust[d].noGravity = true;
                                    }
                                    break;
                                case 2: // 假星陨标记
                                    for (int i = 0; i < 5; i++) {
                                        Vector2 dp = target.Center + Main.rand.NextVector2Circular(300, 200);
                                        int d = Dust.NewDust(dp, 0, 0, DustID.YellowStarDust, 0, 0, 200, default, 0.8f);
                                        Main.dust[d].noGravity = true;
                                    }
                                    break;
                            }
                        }
                    }
                    if (PhaseTimer >= 120) { SubState = 2; PhaseTimer = 0; }
                    break;

                case 2: // 真实攻击：按 scryRealRemaining 依次释放（被击破眼泡会减少）
                    // 真攻击前 12f：演算环快闪骤停 —— 可学习的真假分辨信号
                    int cyc = (int)PhaseTimer % 70;
                    if (scryRealRemaining > 0 && cyc >= 58)
                        DriveCalc((cyc - 58) / 12f, 0.85f);

                    if ((int)PhaseTimer == 1 || (cyc == 1 && PhaseTimer > 60)) {
                        if (scryRealRemaining > 0) {
                            DoScryRealAttack(target, scryRealRemaining);
                            scryRealRemaining--;
                        }
                    }
                    if (scryRealRemaining <= 0 && cyc >= 50) { SubState = 3; PhaseTimer = 0; }
                    else if (PhaseTimer > 220) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3: // 收回眼睛
                    scryActive = false;
                    if ((int)PhaseTimer == 1) {
                        SetFormation(EyeFormation.Orbit);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            KillAllScryingEyes();
                    }
                    if (PhaseTimer >= 25) ReturnToHub();
                    break;
            }
        }

        private void DoScryRealAttack(Player target, int variant) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, NPC.Center);
            if (variant % 2 == 0) {
                // 光柱阵
                int count = Main.expertMode ? 6 : 4;
                for (int i = 0; i < count; i++) {
                    float offsetX = (i - (count - 1) / 2f) * 200f;
                    Vector2 pos = new(target.Center.X + offsetX, target.Center.Y - 800);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, new Vector2(0, 25f),
                        ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                }
            }
            else {
                // 旋转十字
                float baseA = Derive01(23 + (int)PhaseTimer) * MathHelper.PiOver4;
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<CrossLaserBeam>(), NPC.damage / 2, 0f, Main.myPlayer,
                        ai0: NPC.whoAmI, ai1: baseA + MathHelper.PiOver2 * i);
                }
            }
        }

        private void KillAllScryingEyes() {
            int type = ModContent.NPCType<OverseerScryingEye>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0; n.HitEffect(); n.active = false;
                    if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        #endregion

        #region 监视满槽：审判标记

        private void RunMarkedForJudgment(Player target) {
            ServoBrake();
            switch ((int)SubState) {
                case 0: // 锁定 + 预告（55f, 处决级: 渐强震屏 + 演算环 + 锁定框全红咬合）
                    if ((int)PhaseTimer == 1) {
                        crossAngle = (target.Center - NPC.Center).ToRotation(); // 锁定方向（不追踪）
                        SetFormation(EyeFormation.Cross);                      // 眼阵对齐成"瞄具"
                        SpawnJudgmentTelegraph(crossAngle, 2400f, 55);          // 致命锁定线（唯一红，固定方向）
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                        if (!Main.dedServ) {
                            string text = Terraria.Localization.Language.GetTextValue("Mods.AncientChineseMythology.NPCs.CelestialOverseer.JudgmentMark");
                            CombatText.NewText(NPC.getRect(), TelegraphColors.Lethal, text, true);
                        }
                    }
                    DriveCalc(PhaseTimer / 55f, 0.82f);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dp = NPC.Center + crossAngle.ToRotationVector2() * Main.rand.NextFloat(0, 1200);
                            int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 60, default, 1.6f);
                            Main.dust[d].noGravity = true;
                        }
                        if (PhaseTimer % 8 == 0) ACMScreenShakeSystem.Add(MathHelper.Clamp(PhaseTimer / 12f, 0f, 12f)); // 渐强震屏
                    }
                    if (PhaseTimer >= 55) {
                        SubState = 1; PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<JudgmentBeam>(), (int)(NPC.damage * 1.8f), 0f, Main.myPlayer,
                                ai0: NPC.whoAmI, ai1: crossAngle);
                        }
                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f);                                   // 处决级一次性
                        bloomPulse = 1f;                                                  // 全视看穿你的处决泛光
                        NPC.velocity = -crossAngle.ToRotationVector2() * 16f;             // 射线反冲（质量=反作用）
                    }
                    break;

                case 1: // 射线持续后复位
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer > 80) {
                        judgmentQueued = false;
                        surveillanceMeter = 0f;
                        judgmentCooldown = 180;
                        SetFormation(EyeFormation.Orbit);
                        ReturnToHub();
                    }
                    break;
            }
        }

        #endregion

        #region 陪审团事件

        private void RunJuryTrial(Player target) {
            switch ((int)SubState) {
                case 0: // 召唤
                    NPC.dontTakeDamage = true;
                    ServoBrake();
                    if ((int)PhaseTimer == 1) {
                        SetFormation(EyeFormation.Tuck);
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f, Volume = 1.4f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f);

                        int players = CountActivePlayers();
                        int extra = (didJury25 ? 1 : 0);
                        jurorCount = Math.Min(jurorIds.Length, players + extra);
                        juryTimer = JuryDuration;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < jurorCount; i++) {
                                float angle = MathHelper.TwoPi * i / Math.Max(1, jurorCount);
                                Vector2 sp = NPC.Center + angle.ToRotationVector2() * 260f;
                                jurorIds[i] = NPC.NewNPC(NPC.GetSource_FromAI(), (int)sp.X, (int)sp.Y,
                                    ModContent.NPCType<HeavenlyJuror>(), ai0: NPC.whoAmI);
                            }
                        }
                    }
                    if (PhaseTimer >= 50) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 审判中：本体半被动且无敌，玩家须清陪审团
                    NPC.dontTakeDamage = true;
                    ServoMove(HoverSlot(target, 0, -380f), 12f);
                    juryTimer--;

                    // 偶发预告光柱（保持压力，但稀疏）
                    if (PhaseTimer % 90 == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 pos = new(target.Center.X + Main.rand.NextFloat(-200, 200), target.Center.Y - 800);
                        SpawnPillarTelegraph(new Vector2(pos.X, target.Center.Y + 50), 50);
                    }
                    if (PhaseTimer % 90 == 55 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 pos = new(target.Center.X + Main.rand.NextFloat(-200, 200), target.Center.Y - 800);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, new Vector2(0, 22f),
                            ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                    }

                    bool allDead = !AnyJurorsAlive();
                    if (allDead) {
                        // 成功：进入惩戒输出窗
                        SubState = 2; PhaseTimer = 0;
                        SetFormation(EyeFormation.Orbit);
                        SoundEngine.PlaySound(SoundID.Item4, NPC.Center);
                    }
                    else if (juryTimer <= 0) {
                        // 失败：获得永久裁决叠层
                        if (verdictStacks < MaxVerdictStacks) verdictStacks++;
                        KillAllJurors();
                        SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f);
                        if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                        SetFormation(EyeFormation.Orbit);
                        SubState = 3; PhaseTimer = 0;
                    }
                    break;

                case 2: // 惩戒输出窗（清光奖励）：本体可受击且减速
                    ServoBrake();
                    if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(140, 140);
                        int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1.5f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 3f;
                    }
                    if (PhaseTimer > 150) ReturnToHub();
                    break;

                case 3: // 失败收场（短）
                    ServoBrake();
                    if (PhaseTimer > 40) ReturnToHub();
                    break;
            }
        }

        private int CountActivePlayers() {
            int c = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
                if (Main.player[i].active && !Main.player[i].dead) c++;
            return Math.Max(1, c);
        }

        private bool AnyJurorsAlive() {
            int type = ModContent.NPCType<HeavenlyJuror>();
            for (int i = 0; i < jurorCount; i++) {
                int id = jurorIds[i];
                if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active && Main.npc[id].type == type)
                    return true;
            }
            return false;
        }

        private void KillAllJurors() {
            int type = ModContent.NPCType<HeavenlyJuror>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0; n.HitEffect(); n.active = false;
                    if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        #endregion

        #region 阶段过渡（清弹 → 过载演算 → 骤停 → 重构）

        private void RunPhaseTransition(Player target) {
            NPC.dontTakeDamage = true; // 过渡无敌帧
            ServoBrake();

            if ((int)PhaseTimer == 1) {
                ClearOwnProjectiles(); // 公平阀门：换阶段清弹
                SetFormation(EyeFormation.Tuck);
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.4f }, NPC.Center);
            }

            // 过载演算：环 t³ 加速 + 吸入尘 + 渐强 rumble; ~100f 后骤停熄灭（pre-silence）
            float prog = PhaseTimer / 130f;
            DriveCalc(prog, 0.77f);
            EmitConvergingDust(prog);
            if (PhaseTimer < 100 && PhaseTimer % 10 == 0)
                ACMScreenShakeSystem.Add(prog * prog * 6f);

            if ((int)PhaseTimer == 115) {
                // 重构爆发帧
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = IsPhase3 ? -0.3f : 0.1f }, NPC.Center);
                ACMScreenShakeSystem.Add(IsPhase3 ? 14f : 12f);
                bloomPulse = 1f;
                SetFormation(EyeFormation.Orbit);
                eyeOrbitSpeed = 0.03f;
                if (Main.netMode != NetmodeID.Server) {
                    int n = IsPhase3 ? 48 : 36;
                    for (int i = 0; i < n; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(11f, 11f) * Main.rand.NextFloat(0.5f, 1f);
                        int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                        int d = Dust.NewDust(NPC.Center, 0, 0, dustType, v.X, v.Y, 50, default, 2.2f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 150) {
                ReturnToHub();
            }
        }

        #endregion

        #region 三阶段：全知循环

        private void RunOmniscientCycle(Player target) {
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.035f, 0.05f);

            switch (cycleBeat) {
                case 0: BeatHeavensNet(target); break;
                case 1: BeatGazeSweep(target); break;
                case 2: BeatCrossStrangle(target); break;
                case 3: BeatRest(target); break;
            }
        }

        // 节拍0：天网恢恢 —— 光线网格张开收拢, 快门开合留缝（P3 高光, "疏而不漏"却总给一条缝）
        private void BeatHeavensNet(Player target) {
            ServoBrake();
            if ((int)PhaseTimer == 1) {
                SetFormation(EyeFormation.Orbit);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                if (!announcedHeavensNet) {
                    announcedHeavensNet = true;
                    AnnounceHeavensNet();
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 gridCenter = target.Center;
                    // 4 纵 + 3 横（≤7 条, 性能上限）, 生成时带向心收拢速度（网收拢 14%）
                    float[] xs = { -510f, -170f, 170f, 510f };
                    float[] ys = { -340f, 0f, 340f };
                    foreach (float ox in xs) {
                        Vector2 pos = gridCenter + new Vector2(ox, 0);
                        Vector2 contract = new(-ox * 0.14f / OverseerNetBeam.TelegraphTime, 0);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, contract,
                            ModContent.ProjectileType<OverseerNetBeam>(), NPC.damage / 3, 0f, Main.myPlayer,
                            ai0: 0f, ai1: 0f, ai2: 0f);
                    }
                    foreach (float oy in ys) {
                        Vector2 pos = gridCenter + new Vector2(0, oy);
                        Vector2 contract = new(0, -oy * 0.14f / OverseerNetBeam.TelegraphTime);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, contract,
                            ModContent.ProjectileType<OverseerNetBeam>(), NPC.damage / 3, 0f, Main.myPlayer,
                            ai0: 1f, ai1: 0f, ai2: 0f);
                    }
                }
            }

            // 预告期演算环驱动（与网格细线同步收拢）
            if (PhaseTimer <= OverseerNetBeam.TelegraphTime)
                DriveCalc(PhaseTimer / (float)OverseerNetBeam.TelegraphTime, 0.85f);

            // 上膛瞬间的重锤反馈（与 OverseerNetBeam.TelegraphTime 对齐）
            if ((int)PhaseTimer == OverseerNetBeam.TelegraphTime) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f, Pitch = -0.2f }, target.Center);
                ACMScreenShakeSystem.Add(8f);
                bloomPulse = 0.8f;
            }
            if (PhaseTimer > OverseerNetBeam.TotalTime + 12) NextBeat();
        }

        /// <summary>"天网恢恢, 疏而不漏"播报（每场一次, 与 P3 高光绑定）。</summary>
        private void AnnounceHeavensNet() {
            if (Main.dedServ) return;
            string text = Terraria.Localization.Language.GetTextValue("Mods.AncientChineseMythology.NPCs.CelestialOverseer.HeavensNet");
            CombatText.NewText(NPC.getRect(), TelegraphColors.Gold, text, true);
        }

        private void NextBeat() {
            cycleBeat = (cycleBeat + 1) % 4;
            PhaseTimer = 0;
            beatFired = false;
            beatFired2 = false;
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        // 节拍0：地标光柱阵（错相 ripple）
        private void BeatPillarGrid(Player target) {
            ServoBrake();
            if ((int)PhaseTimer == 1) {
                SetFormation(EyeFormation.Orbit);
                markerCount = (Main.expertMode ? 7 : 5) + verdictStacks;
                if (markerCount > markerPositions.Length) markerCount = markerPositions.Length;
                for (int i = 0; i < markerCount; i++) {
                    float offsetX = (i - (markerCount - 1) / 2f) * 180f;
                    markerPositions[i] = new Vector2(target.Center.X + offsetX, target.Center.Y + 50);
                    SpawnPillarTelegraph(markerPositions[i], 60);
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
            }
            DriveCalc(PhaseTimer / 60f);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < markerCount; i++) {
                    int d = Dust.NewDust(markerPositions[i] + new Vector2(-20, -500), 40, 500, DustID.GoldCoin, 0, 2f, 100, default, 0.8f);
                    Main.dust[d].noGravity = true;
                }
            }
            if (PhaseTimer >= 60) {
                if (!beatFired) {
                    beatFired = true;
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(8f);
                }
                int idx = ((int)PhaseTimer - 60) / 3;
                if (Main.netMode != NetmodeID.MultiplayerClient && ((int)PhaseTimer - 60) % 3 == 0 && idx < markerCount) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        new Vector2(markerPositions[idx].X, markerPositions[idx].Y - 800), new Vector2(0, 26f),
                        ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                }
            }
            if (PhaseTimer > 130) NextBeat();
        }

        // 节拍1：带安全扇区的旋转凝视扫描（双臂螺旋）
        private void BeatGazeSweep(Player target) {
            NPC.velocity = Vector2.Zero;
            if ((int)PhaseTimer == 1) {
                gazeAngle = Derive01(27 + cycleBeat) * MathHelper.TwoPi;
                gazeDir = Derive01(28 + cycleBeat) > 0.5f ? 1f : -1f;
                safeWedgeAngle = (target.Center - NPC.Center).ToRotation();
                SpawnSafeWedgeTelegraph(safeWedgeAngle, 1400f, 50);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f }, NPC.Center);
            }
            if (PhaseTimer <= 50) {
                DriveCalc(PhaseTimer / 50f, 0.85f);
            }
            else {
                gazeAngle += gazeDir * 0.08f;
                if (PhaseTimer % 2 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int arm = 0; arm < 2; arm++) {
                        float a = gazeAngle + MathHelper.Pi * arm;
                        if (Math.Abs(MathHelper.WrapAngle(a - safeWedgeAngle)) < SafeWedgeHalf) continue;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * 8.5f,
                            ModContent.ProjectileType<HolyOrb>(), NPC.damage / 3, 1f, Main.myPlayer, ai0: 1f);
                    }
                }
                if (Main.netMode != NetmodeID.Server) {
                    Vector2 dp = NPC.Center + safeWedgeAngle.ToRotationVector2() * Main.rand.NextFloat(0, 800);
                    int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(180, 220, 255), 0.9f);
                    Main.dust[d].noGravity = true;
                }
            }
            if (PhaseTimer > 50 + 170) NextBeat();
        }

        // 节拍2：十字绞杀 —— 眼阵十字构型, 两轮同步齐射
        private void BeatCrossStrangle(Player target) {
            NPC.velocity = Vector2.Zero;
            if ((int)PhaseTimer == 1) {
                crossAngle = Derive01(33 + cycleBeat) * MathHelper.PiOver2;
                SetFormation(EyeFormation.Cross);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
            }
            DriveCalc(PhaseTimer / 50f);
            EmitConvergingDust(PhaseTimer / 50f);

            if (!beatFired && PhaseTimer >= 50) {
                beatFired = true;
                // 第一轮：沿十字臂射出高速直线弹
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 4; i++) {
                        float a = crossAngle + MathHelper.PiOver2 * i;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), GetEyePosition(i), a.ToRotationVector2() * 11f,
                            ModContent.ProjectileType<SweepingLaserBolt>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }
                for (int i = 0; i < 4; i++) {
                    eyeRenderVel[i] -= (crossAngle + MathHelper.PiOver2 * i).ToRotationVector2() * 20f;
                    eyeSnapFlash[i] = 1f;
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(8f);
            }
            if (!beatFired2 && PhaseTimer >= 88) {
                beatFired2 = true;
                // 第二轮：全眼对玩家同步激光
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < CelestialEyeCount; i++) {
                        Vector2 eyePos = GetEyePosition(i);
                        Vector2 toT = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, toT * 13f,
                            ModContent.ProjectileType<MinionSyncLaser>(), NPC.damage / 2, 2f, Main.myPlayer);
                    }
                }
                for (int i = 0; i < CelestialEyeCount; i++)
                    eyeRenderVel[i] -= (target.Center - GetEyePosition(i)).SafeNormalize(Vector2.Zero) * 22f;
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.4f, Pitch = 0.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
            }
            if (PhaseTimer > 130) {
                SetFormation(EyeFormation.Orbit);
                NextBeat();
            }
        }

        // 节拍3：休整（无弹）—— 玩家的呼吸口 + 审判/陪审中断检查
        private void BeatRest(Player target) {
            SetFormation(EyeFormation.Orbit);
            ServoMove(HoverSlot(target, 0, -360f), 15f);

            if (PhaseTimer >= 60) {
                if (judgmentQueued) { TransitionTo(BossPhase.MarkedForJudgment); return; }
                if (TryStartJury()) return;
                NextBeat();
            }
        }

        #endregion

        #region 死亡演出（系统崩溃）

        /// <summary>进入死亡演出：清弹清仆从 + 伤害归零 + 状态锁定。</summary>
        private void BeginDeath() {
            ClearOwnProjectiles();
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                KillAllScryingEyes();
                KillAllJurors();
            }
            scryActive = false;
            NPC.damage = 0;
            NPC.velocity = Vector2.Zero;
            SetFormation(EyeFormation.Tuck);
            TransitionTo(BossPhase.Death);
        }

        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;

            if ((int)PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
                ACMScreenShakeSystem.Add(8f);
            }

            if (PhaseTimer < 70) {
                // 「系统故障」：量化抖动（绘制层）+ 电弧尘 + 失谐 click
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(90f, 90f);
                    int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 80, default, 1.4f);
                    Main.dust[d].noGravity = true;
                }
                if (!Main.dedServ && PhaseTimer % 16 == 0)
                    SoundEngine.PlaySound(SoundID.Mech with { Pitch = Main.rand.NextFloat(-0.8f, 0.4f), Volume = 0.6f }, NPC.Center);
            }
            else if (PhaseTimer < 190) {
                // 「天眼连环坠毁」：UpdateEyeFormation 的死亡分支接管（音调递升 + 逐眼爆）
            }
            else if (PhaseTimer < 270) {
                // 「核心过载」：演算环转速拉满 + 吸入尘 + halo 收缩 + rumble
                float t = (PhaseTimer - 190f) / 80f;
                calcDriven = true;
                calcCharge = t;
                calcFade = 1f;
                calcSpin += 0.05f + t * t * 0.9f;
                EmitConvergingDust(t * 0.7f);
                if (PhaseTimer % 9 == 0)
                    ACMScreenShakeSystem.Add(t * t * 5f);
            }
            else if (PhaseTimer < 285) {
                // 「完全静默」15f：一切熄灭 —— 尖叫前的吸气
                calcDriven = true;
                calcCharge *= 0.8f;
                calcFade *= 0.75f;
            }
            else if ((int)PhaseTimer == 285) {
                // 「终爆」：全 fight 唯一 shake16
                ACMScreenShakeSystem.Add(16f);
                bloomPulse = 1f;
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 1.4f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 80; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(14f, 14f) * Main.rand.NextFloat(0.4f, 1f);
                        int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.JungleSpore;
                        int d = Dust.NewDust(NPC.Center, 0, 0, dustType, v.X, v.Y, 40, default, Main.rand.NextFloat(1.8f, 3f));
                        Main.dust[d].noGravity = true;
                    }
                    // 齿轮碎屑：带重力抛物线（机关解体的"实体感"）
                    for (int i = 0; i < 20; i++) {
                        Vector2 v = new(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-13f, -5f));
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, v.X, v.Y, 30, default, 2.4f);
                        Main.dust[d].noGravity = false;
                        Main.dust[d].fadeIn = 1.2f;
                    }
                }
            }
            else if (PhaseTimer < 370) {
                // 「余韵」：烟雾上飘 + 信号余辉衰减
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(70f, 40f);
                    int d = Dust.NewDust(dp, 0, 0, DustID.Smoke, 0, -1.5f, 120, default, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }
            else {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    reallyDead = true;
                    NPC.netUpdate = true;
                    NPC.StrikeInstantKill();
                }
            }
        }

        #endregion

        #region 预告生成辅助

        private void SpawnCrossTelegraph(float baseAngle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < 4; i++) {
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                    ai0: length, ai1: baseAngle + MathHelper.PiOver2 * i, ai2: NPC.whoAmI);
                if (p >= 0 && p < Main.maxProjectiles) {
                    Main.projectile[p].timeLeft = life;
                    Main.projectile[p].localAI[0] = 0f; // style: 线
                }
            }
        }

        private void SpawnSafeWedgeTelegraph(float safeAngle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: length, ai1: safeAngle, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 2f; // style: 安全扇区
            }
        }

        private void SpawnPillarTelegraph(Vector2 groundPos, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), groundPos, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: 800f, ai1: -MathHelper.PiOver2, ai2: -1f);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 1f; // style: 光柱列
            }
        }

        /// <summary>致命审判锁定线（唯一红, style=3）：单发固定方向。</summary>
        private void SpawnJudgmentTelegraph(float angle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: length, ai1: angle, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 3f; // style: 致命审判线
            }
        }

        /// <summary>缉拿冲刺锁定线（唯一红：接触伤害的真实路径, 方向锁定后不再变）。</summary>
        private void SpawnDashTelegraph(float angle, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: 900f, ai1: angle, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 3f; // style: 致命锁定线
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            float z = IntroZ();
            float zScale = 1f / (1f + z);
            float zOpacity = MathHelper.Clamp(1f - z / 6f, 0f, 1f);
            bool bodyHidden = (Phase == BossPhase.Intro && PhaseTimer <= 30)
                || (Phase == BossPhase.Death && PhaseTimer >= 285);

            // 演算法阵（身后层）
            DrawCalcRing(spriteBatch);

            if (!bodyHidden) {
                DrawDivineAura(spriteBatch, screenPos, zScale, zOpacity);
                DrawTrail(spriteBatch, screenPos);
                if (!scryActive) DrawEyeArray(spriteBatch, screenPos);
                DrawHalo(spriteBatch, screenPos, zScale, zOpacity);
                DrawMainBody(spriteBatch, screenPos, drawColor, zScale, zOpacity);
                DrawOuterGlow(spriteBatch, screenPos, zScale, zOpacity);
            }
            DrawSurveillanceMeter(spriteBatch, screenPos);
            DrawStarReticles(spriteBatch, screenPos);

            // 处决/开火加性泛光（金白权柄）。DrawRadialBloomAt 内部申请全屏名额 —— PreDraw 先于
            // PostDraw 执行, 故开火帧泛光优先取得名额, 扫描线当帧让位（全屏名额仲裁）。
            if (bloomPulse > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.32f, MathHelper.Clamp(bloomPulse, 0f, 1f), TelegraphColors.Holy, 12f, 2.4f);

            return false;
        }

        /// <summary>
        /// V3 监察扫描线全屏后处理（OverseerScanline）：CRT 介质感 + 下扫亮带 + 数据流字符雨
        /// + 窥视/死亡故障 + 审判红化 + 入场开机。喂 Main.screenTarget 的昂贵后处理, 受单一
        /// 全屏名额约束; 强度过低或名额被开火泛光占用时直接早退。
        /// 监视暗角 / 全视法阵 / 锁定框由 <see cref="OverseerSurveillanceScreenSystem"/> 单独承担。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            bool booting = Phase == BossPhase.Intro && PhaseTimer < 36;
            if (scanPublish <= 0.02f && glitchPublish <= 0.02f && lockLethalPublish <= 0.02f && !booting)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = GetScanlineEffect();
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(scanPublish, 0f, 1f));
            fx.Parameters["uGlitch"]?.SetValue(MathHelper.Clamp(glitchPublish, 0f, 1f));
            fx.Parameters["uLockdown"]?.SetValue(MathHelper.Clamp(lockLethalPublish, 0f, 1f));
            fx.Parameters["uBoot"]?.SetValue(bootPublish);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uFocus"]?.SetValue(centerUV);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        /// <summary>演算法阵绘制（OverseerCalcRing, Additive decal; 有活动批 → 用 DrawScreenSpaceDecal）。</summary>
        private void DrawCalcRing(SpriteBatch sb) {
            float inten = calcCharge * MathHelper.Clamp(calcFade, 0f, 1f);
            if (Main.dedServ || inten <= 0.03f)
                return;
            Effect fx = GetCalcRingEffect();
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(NPC.Center, 250f, out Vector2 uv, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.03f, 0.9f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(inten, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uSpin"]?.SetValue(calcSpin);
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(calcCharge, 0f, 1f));
            fx.Parameters["uCollapse"]?.SetValue(MathHelper.Clamp(1f - calcFade, 0f, 1f));
            fx.Parameters["uSides"]?.SetValue(IsPhase3 ? 6f : (IsPhase2 ? 4f : 3f));
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Gold.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(MechJade.ToVector4());

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        private void DrawSurveillanceMeter(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null || surveillanceMeter <= 1f) return;
            Texture2D tex = ACMAsset.BlankStar;
            Vector2 center = NPC.Center - screenPos + new Vector2(0, -130);
            int dots = 12;
            int lit = (int)(surveillanceMeter / 100f * dots);
            float t = surveillanceMeter / 100f;
            Color full = Color.Lerp(new Color(255, 240, 160), new Color(255, 70, 60), t);
            for (int i = 0; i < dots; i++) {
                float a = MathHelper.Pi + MathHelper.Pi * i / (dots - 1f);
                Vector2 pos = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 70f;
                Color c = (i < lit ? full : new Color(60, 60, 70)) * 0.9f;
                c.A = 0;
                spriteBatch.Draw(tex, pos, null, c, 0f, tex.Size() / 2f, 0.12f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>星陨锁定准星：双层旋转方框咬合收缩 + 末端白闪 —— 发射位即准星, 完全可读。</summary>
        private void DrawStarReticles(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (Phase != BossPhase.Attack_StarVolley || (int)SubState != 0 || markerCount <= 0)
                return;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (pixel == null)
                return;

            float prog = MathHelper.Clamp(PhaseTimer / 62f, 0f, 1f);
            bool flash = prog >= 50f / 62f;
            float half = MathHelper.Lerp(46f, 16f, ACMUtils.SineInOut(prog));
            float rot = (1f - prog) * 1.2f;
            Color col = Color.Lerp(TelegraphColors.Gold, Color.White, flash ? 0.5f + 0.5f * MathF.Sin(globalTime * 40f) : 0f);
            col *= 0.35f + 0.65f * prog;
            col.A = 0;

            Rectangle src = new(0, 0, 1, 1);
            for (int m = 0; m < markerCount; m++) {
                Vector2 c = markerPositions[m] - screenPos;
                // 双层: 正方框 + 45° 错层
                for (int layer = 0; layer < 2; layer++) {
                    float lr = rot + layer * MathHelper.PiOver4;
                    float lh = half * (layer == 0 ? 1f : 0.7f);
                    for (int e = 0; e < 4; e++) {
                        float ea = lr + MathHelper.PiOver2 * e;
                        Vector2 mid = c + ea.ToRotationVector2() * lh;
                        Vector2 tangent = (ea + MathHelper.PiOver2).ToRotationVector2();
                        // 每边只画中央 60% 的短杆 → 括号观感
                        float len = lh * 1.2f;
                        spriteBatch.Draw(pixel, mid - tangent * len * 0.5f, src, col * (layer == 0 ? 1f : 0.6f),
                            ea + MathHelper.PiOver2, new Vector2(0f, 0.5f), new Vector2(len, 2f), SpriteEffects.None, 0f);
                    }
                }
                // 中心点
                spriteBatch.Draw(pixel, c - new Vector2(1.5f), src, col, 0f, Vector2.Zero, new Vector2(3f, 3f), SpriteEffects.None, 0f);
            }
        }

        private void DrawDivineAura(SpriteBatch spriteBatch, Vector2 screenPos, float zScale, float zOpacity) {
            if (ACMAsset.LightShot == null) return;
            Texture2D auraTexture = ACMAsset.LightShot;
            Vector2 drawPos = NPC.Center - screenPos;
            Color auraColor = new Color(255, 240, 180) * divineAuraAlpha * zOpacity;
            auraColor.A = 0;
            float auraScale = 8f * haloScale * zScale;
            spriteBatch.Draw(auraTexture, drawPos, null, auraColor, MathHelper.PiOver2, auraTexture.Size() / 2f, auraScale, SpriteEffects.None, 0f);
        }

        /// <summary>拖尾：速度门控 —— 只在冲刺等高速帧亮起（速度的"证词", 常亮即噪声）。</summary>
        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            float speedGate = MathHelper.Clamp((NPC.velocity.Length() - 20f) / 30f, 0f, 1f);
            if (speedGate <= 0.02f)
                return;
            Texture2D texture = TextureAssets.Npc[Type].Value;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = new Color(255, 230, 150) * progress * 0.4f * speedGate * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;
                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>天眼台阵：机关连杆线 + 关节点 + 天眼本体（LOS 冷暖 + 就位白闪）。</summary>
        private void DrawEyeArray(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (eyeRenderPos == null) return;
            Texture2D eyeTexture = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (eyeTexture == null) return;

            int deployed = EyesDeployedCount;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 bodyPos = NPC.Center - screenPos;
            Player target = Main.player[Math.Clamp(NPC.target, 0, Main.maxPlayers - 1)];

            for (int i = 0; i < CelestialEyeCount; i++) {
                if (i >= deployed || eyeGone[i]) continue;
                Vector2 eyePos = eyeRenderPos[i] - screenPos;

                // 机关连杆（本体→眼, 细金线 + 中点关节）
                if (pixel != null && Phase != BossPhase.Death) {
                    Vector2 rod = eyePos - bodyPos;
                    float len = rod.Length();
                    if (len > 8f) {
                        float rrot = rod.ToRotation();
                        Color rodCol = TelegraphColors.Gold * 0.28f;
                        rodCol.A = 0;
                        spriteBatch.Draw(pixel, bodyPos, src, rodCol, rrot, new Vector2(0f, 0.5f), new Vector2(len, 1.6f), SpriteEffects.None, 0f);
                        Vector2 joint = bodyPos + rod * 0.45f;
                        Color jointCol = MechJade * 0.5f;
                        jointCol.A = 0;
                        spriteBatch.Draw(pixel, joint - new Vector2(2f), src, jointCol, rrot + MathHelper.PiOver4, Vector2.Zero, new Vector2(4f, 4f), SpriteEffects.None, 0f);
                    }
                }

                float eyeRot = target.active ? (target.Center - eyeRenderPos[i]).ToRotation() : 0f;

                // 视线高亮：有视线时偏暖（危险），无视线时偏冷
                Color outerGlow = (eyeHasLOS != null && eyeHasLOS[i]) ? new Color(255, 180, 120) * 0.7f : new Color(160, 200, 255) * 0.5f;
                outerGlow.A = 0;
                spriteBatch.Draw(eyeTexture, eyePos, null, outerGlow, globalTime + i * 0.5f, eyeTexture.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
                Color coreColor = new(255, 255, 220);
                coreColor.A = 0;
                spriteBatch.Draw(eyeTexture, eyePos, null, coreColor, -globalTime * 0.5f + i * 0.3f, eyeTexture.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
                if (CelestialEyeMinion.CelestialOverseerEye != null) {
                    spriteBatch.Draw(eyeTexture, eyePos, null, Color.White, eyeRot, eyeTexture.Size() / 2f, 0.35f, SpriteEffects.None, 0f);
                }

                // 就位/开火白闪
                if (eyeSnapFlash[i] > 0.05f) {
                    Color flash = Color.White * eyeSnapFlash[i];
                    flash.A = 0;
                    spriteBatch.Draw(eyeTexture, eyePos, null, flash, 0f, eyeTexture.Size() / 2f, 0.5f + eyeSnapFlash[i] * 0.25f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos, float zScale, float zOpacity) {
            if (ACMAsset.BlankStar == null) return;
            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.5f + i * 0.3f) * haloScale * zScale;
                Color layerColor = new Color(255, 245, 200) * (0.4f - i * 0.1f) * zOpacity;
                layerColor.A = 0;
                spriteBatch.Draw(haloTexture, drawPos, null, layerColor, layerRotation, haloTexture.Size() / 2f, layerScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, float zScale, float zOpacity) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos + servoJitter;

            // 死亡演出：量化故障错位（每 6 帧 snap 一次, 不做平滑 —— 数字信号损坏感）
            if (Phase == BossPhase.Death && PhaseTimer < 285) {
                unchecked {
                    int gs = (int)(PhaseTimer / 6f);
                    int h = gs * 374761393 + seed;
                    h ^= h >> 13;
                    drawPos += new Vector2((h & 15) - 7.5f, ((h >> 4) & 15) - 7.5f) * glitchPublish;
                }
            }

            float opacity = NPC.Opacity * zOpacity;
            Color glowColor = new Color(255, 240, 180) * 0.4f * opacity;
            glowColor.A = 0;
            for (int i = 0; i < 4; i++) {
                float angle = globalTime * 2f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                spriteBatch.Draw(texture, drawPos + offset, null, glowColor, NPC.rotation, texture.Size() / 2f, NPC.scale * 1.05f * zScale, SpriteEffects.None, 0f);
            }
            Color bodyColor = drawColor * opacity;
            spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation, texture.Size() / 2f, NPC.scale * zScale, SpriteEffects.None, 0f);
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos, float zScale, float zOpacity) {
            if (ACMAsset.Sparkle == null) return;
            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;
            Color sparkleColor = new Color(255, 250, 220) * 0.3f * glowIntensity * zOpacity;
            sparkleColor.A = 0;
            spriteBatch.Draw(sparkleTexture, drawPos, null, sparkleColor, globalTime * 0.5f, sparkleTexture.Size() / 2f, 2f * haloScale * zScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(sparkleTexture, drawPos, null, sparkleColor * 0.5f, -globalTime * 0.3f, sparkleTexture.Size() / 2f, 2.5f * haloScale * zScale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}

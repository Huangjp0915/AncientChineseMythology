using AncientChineseMythology.Helpers;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.Spectres.Items;
using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨灵 Spectre 「冤魂记账者 / The Grudge-Keeper」 — 地府 P1 怨念门控 Boss (V3 重做)。
    ///
    /// V3 设计核心 (Docs/BossRedo/Spectre.md)：「怨灯引魂 —— 灯火之间的虚实猎杀」。
    ///   1. 虚实转换 (诚实开关)：虚相 = 半透明 + 无敌 + <b>绝不攻击</b>；实相 = 攻击 + 可被打。
    ///      接触伤害只在实相冲刺的高速帧存在 (<see cref="CanHitPlayer"/> 与视觉严格对齐)。
    ///   2. 聚散成形：超距不追 — 旧位散形、新位凝形 (SpectreVeil uDissolve)，传送必带前后 telegraph。
    ///   3. 鬼影残像：oldPos 延迟残影经 SpectreVeil 逐影抬虚相绘制，速度门控 (静止不显)；
    ///      P3《幻影重演》把 Boss 刚冲过的路径重演成真实威胁。
    ///   4. 灯火引魂：灯笼锚点既是清账反制 (UnderworldField 怨念账)，也是哀嚎尖啸唯一的安全灯道；
    ///      死亡演出灯笼逐盏熄灭送葬。
    /// 三大演出节拍齐备：入场灯阵凝形 / 换阶段清弹定格 / CheckDead 210f 死亡脚本。
    /// 视觉：专属 SpectreVeil.fx (本体/残像统一鬼相 pass) + 共享 BeamGrad/ArenaRunic/RadialBloom/
    /// GenericWarp/PaletteLUT；红色只留给致命源 (§6.1)。
    /// </summary>
    [AutoloadBossHead]
    public class Spectre : ModNPC
    {
        #region Boss 脚本化幕 (确定性轮替, 非随机)

        public enum BossPhase
        {
            Intro,            // 出场：灯阵中溶解凝形 → 静场 → 尖啸
            Haunting,         // 缠 — 齐射循环 (前摇反漂 + 涟漪发射 + 反冲)
            GrudgeChain,      // 怨链 — 锁链盘旋蓄力 → 逐条甩链 (挂魂蚀)
            Wailing,          // 哀嚎 — 处决级蓄力尖啸环, 灯道 = 唯一安全缝
            Possession,       // 附身 — 转虚召分身, 必杀 2 只破虚 (虚相全程停火)
            SoulStorm,        // 灵魂风暴 — 旋转安全扇区环, 中途反转 (预告)
            GrudgeReckoning,  // 冤魂审判 — P3 每循环开局镜像清算 set-piece
            PhaseShift,       // 相变定格 (清弹 + 短无敌 beat)
            VeilRush,         // 相位突袭 — 签名招：转虚游标 → 凝实收缩 → 单帧爆发冲刺
            PhantomReplay,    // 幻影重演 — P3：金线记录冲刺路径, 幻影延迟重演
            Death             // 死亡演出脚本 (CheckDead 截获)
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SpecialTimer => ref NPC.ai[3];

        #endregion

        #region 时序常量

        // 入场 (200f): 灯阵亮起 → 凝形 → 30f 静场 → 尖啸 → 落位
        private const int IntroTime = 200;
        private const int IntroScreamFrame = 140;

        // 相位突袭: 周期内 launch = cycleLen - 42, 刹车 = launch + 18
        private const int RushTimeout = 420;

        // 哀嚎: 蓄力 88f (65f 断流入静默), 灯道 55f 锁定, 环 88/100/(110)
        private const int WailLaneLockFrame = 55;
        private const int WailSilenceFrame = 65;
        private const int WailReleaseFrame = 88;

        // 附身门控
        private const int PossessionGate = 2;
        private const int PossessionTimeout = 600;
        private const int MinionCap = 3;

        // 冤魂审判: 150f 蓄力 (108f = 72% 粒子硬切), 80f 收招
        private const int ReckoningCharge = 150;
        private const int ReckoningCutFrame = 108;
        private const int ReckoningRecover = 80;

        private const int PhaseShiftTime = 60;

        // 死亡演出: 踉跄 40 → 灯灭+频闪 120 → 坍缩 150 → 大爆 → 魂雨 210
        private const int DeathDuration = 210;
        private const int DeathCollapseFrame = 120;
        private const int DeathBurstFrame = 150;

        #endregion

        #region 状态变量

        // —— 同步状态 (SendExtraAI, 状态切换时 netUpdate) ——
        private bool isPhase2;  // 50%
        private bool isPhase3;  // 25%
        private int cycleIndex = -1;
        private int cycleCount;
        private Vector2 arenaCenter;
        private bool arenaSet;
        private float retaliationAngle;     // 审判报复来向
        private float laneAngle;            // 哀嚎安全灯道朝向
        private Vector2 dashDir = Vector2.UnitX; // 突袭/重演冲刺方向
        private Vector2 replayStart;        // 幻影重演记录线段
        private Vector2 replayEnd;
        private bool reckoningReleased;
        private bool possSpawned;
        private readonly int[] possMinions = new int[4];
        private float syncedGrudgeNorm;     // 客户端怨念可视 (归一化)

        // —— 服务器记账 ——
        private int grudgeDamageAccum;
        private readonly float[] sectorTime = new float[8];

        // —— 纯视觉 (客户端本地, 不同步) ——
        private float pulsePhase;
        private float hoverOffset;
        private float veilVisual;           // 虚相度平滑值
        private float veilTarget;           // 本帧逻辑虚相目标 (由各幕设置)
        private float dissolveVisual;       // 聚散进度 (1=散尽)
        private float flameVisual;          // 内焰强度平滑值
        private float flameTarget;
        private float dashBlur;             // 冲刺拖影强度 (爆发帧置 1, 指数衰减)
        private float bodyScaleMod = 1f;    // 收缩/坍缩缩放系数
        private float bodyJitter;           // 蓄力颤动幅度 (px)
        private bool faceRight = true;
        private float deathStrobePhase;     // 死亡加速频闪累计相位
        private Vector2 deathKnockDir = Vector2.UnitX;

        // 能量波纹 (释放/相变反馈)
        private readonly float[] waveRadius = new float[3];
        private readonly float[] waveAlpha = new float[3];

        #endregion

        #region 目标 / 派生状态

        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>当前怨念归一化 0~1 (服务器实时 / 客户端用同步值), 驱动褪色与报复波规模。</summary>
        private float GrudgeNorm() =>
            Main.netMode == NetmodeID.MultiplayerClient
                ? MathHelper.Clamp(syncedGrudgeNorm, 0f, 1f)
                : UnderworldField.GetGrudgeNormalized(NPC);

        /// <summary>突袭周期长度 (P3 提速)。</summary>
        private int RushCycleLen => isPhase3 ? 88 : 96;
        private int RushLaunchFrame => RushCycleLen - 42;
        private int RushCycles => isPhase2 ? 3 : 2;
        private float RushSpeed => isPhase3 ? 48f : 44f;

        private int WailDuration => isPhase3 ? 200 : 190;
        private int WailRingCount => isPhase3 ? 26 : (isPhase2 ? 22 : 18);

        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 16;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 80;
            NPC.height = 100;
            NPC.damage = 80;
            NPC.defense = 40;
            NPC.lifeMax = 120000;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = 150000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.alpha = 0;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            cycleIndex = -1;
            dissolveVisual = 1f; // 从全散开始凝形
            veilVisual = 1f;

            // 怨念账上限：约等于"打掉满血所需输出"才满账 → 终幕规模由玩家输出节奏决定。
            UnderworldField.SetGrudgeMax(NPC, 100);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        #region 网络同步

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(isPhase2);
            writer.Write(isPhase3);
            writer.Write(cycleIndex);
            writer.Write(cycleCount);
            writer.Write(arenaSet);
            writer.Write(arenaCenter.X);
            writer.Write(arenaCenter.Y);
            writer.Write(retaliationAngle);
            writer.Write(laneAngle);
            writer.Write(dashDir.X);
            writer.Write(dashDir.Y);
            writer.Write(replayStart.X);
            writer.Write(replayStart.Y);
            writer.Write(replayEnd.X);
            writer.Write(replayEnd.Y);
            writer.Write(reckoningReleased);
            writer.Write(possSpawned);
            for (int i = 0; i < possMinions.Length; i++)
                writer.Write(possMinions[i]);
            writer.Write(UnderworldField.GetGrudgeNormalized(NPC));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            isPhase2 = reader.ReadBoolean();
            isPhase3 = reader.ReadBoolean();
            cycleIndex = reader.ReadInt32();
            cycleCount = reader.ReadInt32();
            arenaSet = reader.ReadBoolean();
            arenaCenter.X = reader.ReadSingle();
            arenaCenter.Y = reader.ReadSingle();
            retaliationAngle = reader.ReadSingle();
            laneAngle = reader.ReadSingle();
            dashDir.X = reader.ReadSingle();
            dashDir.Y = reader.ReadSingle();
            replayStart.X = reader.ReadSingle();
            replayStart.Y = reader.ReadSingle();
            replayEnd.X = reader.ReadSingle();
            replayEnd.Y = reader.ReadSingle();
            reckoningReleased = reader.ReadBoolean();
            possSpawned = reader.ReadBoolean();
            for (int i = 0; i < possMinions.Length; i++)
                possMinions[i] = reader.ReadInt32();
            syncedGrudgeNorm = reader.ReadSingle();
        }

        #endregion

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.85f);
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => AccumulateGrudge(damageDone);
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => AccumulateGrudge(damageDone);

        /// <summary>记账：玩家造业累积到怨念。仅服务器/单机权威。</summary>
        private void AccumulateGrudge(int dmg) {
            if (Main.netMode == NetmodeID.MultiplayerClient || dmg <= 0)
                return;
            grudgeDamageAccum += dmg;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.netMode == NetmodeID.Server)
                return;
            // 受击魂缕: 顺打击方向飞散 (轻量 juice)
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24, 30),
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = new Vector2(hit.HitDirection * 3f, -1.5f) + Main.rand.NextVector2Circular(1.5f, 1.5f);
            }
        }

        #region 接触伤害门控 (伤害窗口与视觉严格对齐)

        /// <summary>
        /// 鬼体平时无接触伤害 — 全部威胁来自弹幕与冲刺。
        /// 接触伤害仅在实相冲刺的高速帧开启 (突袭/重演爆发段, |v| &gt; 22)。
        /// </summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if (Phase != BossPhase.VeilRush && Phase != BossPhase.PhantomReplay)
                return false;
            return veilTarget < 0.5f && NPC.velocity.Length() > 22f;
        }

        #endregion

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;

            // —— 视觉时基 ——
            pulsePhase += 0.08f;
            hoverOffset = MathF.Sin(pulsePhase * 0.5f) * 10f;
            UpdateEnergyWaves();

            // —— 本帧默认：实相 / 可被打 / 无颤动 / 缩放回弹, 由各幕显式覆盖 ——
            veilTarget = 0f;
            flameTarget = 0.35f;
            bodyJitter = 0f;
            bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 1f, 0.06f);
            NPC.dontTakeDamage = false;
            if (Phase != BossPhase.Death)
                NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.012f, 0.15f);

            ConvertGrudge();

            if (Phase != BossPhase.Intro && Phase != BossPhase.Death)
                CheckPhaseTransition();

            NPC.TargetClosest();
            Player target = Target;
            if ((!target.active || target.dead) && Phase != BossPhase.Death) {
                // 无目标: 上升消散离场 (死亡演出不中断 — 保证掉落与 downed 标记)
                NPC.velocity.Y -= 0.3f;
                NPC.alpha += 2;
                dissolveVisual = MathHelper.Clamp(dissolveVisual + 0.02f, 0f, 1f);
                if (NPC.alpha > 255 || NPC.timeLeft < 10)
                    NPC.active = false;
                return;
            }

            // 面向 (滞回, 稳定翻面)
            float dx = target.Center.X - NPC.Center.X;
            if (MathF.Abs(dx) > 40f)
                faceRight = dx > 0;

            // 记录玩家停留象限 (账记得清楚 — 决定报复来向)
            if (arenaSet && Phase != BossPhase.Intro && Phase != BossPhase.Death) {
                Vector2 rel = target.Center - arenaCenter;
                if (rel.LengthSquared() > 64f) {
                    int s = (int)Math.Round(rel.ToRotation() / (MathHelper.TwoPi / 8f));
                    s = ((s % 8) + 8) % 8;
                    sectorTime[s] += 1f;
                }
            }

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Haunting: RunHaunting(target); break;
                case BossPhase.GrudgeChain: RunGrudgeChain(target); break;
                case BossPhase.Wailing: RunWailing(target); break;
                case BossPhase.Possession: RunPossession(target); break;
                case BossPhase.SoulStorm: RunSoulStorm(target); break;
                case BossPhase.GrudgeReckoning: RunGrudgeReckoning(target); break;
                case BossPhase.PhaseShift: RunPhaseShift(target); break;
                case BossPhase.VeilRush: RunVeilRush(target); break;
                case BossPhase.PhantomReplay: RunPhantomReplay(target); break;
                case BossPhase.Death: RunDeath(target); break;
            }

            // —— 虚实/内焰/拖影平滑 ——
            veilVisual = MathHelper.Lerp(veilVisual, veilTarget, 0.16f);
            flameVisual = MathHelper.Lerp(flameVisual, flameTarget, 0.1f);
            dashBlur *= 0.92f;

            // 虚相 = 无敌 (诚实开关: 虚相各幕均不发起攻击)
            if (veilTarget > 0.5f)
                NPC.dontTakeDamage = true;

            // —— 天空/滤镜状态发布 (纯客户端视觉) ——
            if (!Main.dedServ) {
                int phaseLevel = isPhase3 ? 3 : (isPhase2 ? 2 : 1);
                float deathProg = Phase == BossPhase.Death ? PhaseTimer / (float)DeathDuration : 0f;
                float introDark = Phase == BossPhase.Intro ? MathHelper.Clamp(PhaseTimer / 60f, 0f, 1f) : 1f;
                SpectreSky.Publish(NPC.Center, phaseLevel, GrudgeNorm(), deathProg, introDark);
            }

            float lightIntensity = isPhase3 ? 1.2f : (isPhase2 ? 1f : 0.8f);
            lightIntensity *= 1f - veilVisual * 0.5f;
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreCyan.ToVector3() * lightIntensity * 0.5f);
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreYellow.ToVector3() * lightIntensity * 0.3f);
        }

        #region 怨念账 / 阶段 / 轮替

        private void ConvertGrudge() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int chunk = Math.Max(1, NPC.lifeMax / 100); // ~1% 血量造业 = 1 点怨念
            bool changed = false;
            while (grudgeDamageAccum >= chunk) {
                grudgeDamageAccum -= chunk;
                UnderworldField.AddGrudge(NPC, 1);
                changed = true;
            }
            if (changed && AttackTimer % 12 == 0)
                NPC.netUpdate = true;
        }

        private void CheckPhaseTransition() {
            if (Phase == BossPhase.PhaseShift)
                return;

            float lifePercent = (float)NPC.life / NPC.lifeMax;

            if (!isPhase2 && lifePercent <= 0.5f) {
                isPhase2 = true;
                BeginPhaseShift();
            }
            if (!isPhase3 && lifePercent <= 0.25f) {
                isPhase3 = true;
                // 进入终幕：相变后第一招强制为冤魂审判 (act 列表 index 0)。
                cycleIndex = -1;
                BeginPhaseShift();
            }
        }

        private void BeginPhaseShift() {
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 120f, 4, 20);
            SpectreHelper.CreateSpectreVortex(NPC.Center, 150f, 1.2f, 50);
            for (int i = 0; i < 3; i++) TriggerEnergyWave();
            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 0.8f);
            ACMUtils.AddScreenShake(10f);

            // 公平阀门: 相变清空本 Boss 全部敌对弹幕
            ClearHostileProjectiles();

            TransitionTo(BossPhase.PhaseShift);
        }

        private void RunPhaseShift(Player target) {
            // 转虚定格, 避免被秒过场; 同时是波形上的"段落空行"
            veilTarget = 1f;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            Vector2 hover = target.Center + new Vector2(0, -220f + hoverOffset);
            NPC.Center += (hover - NPC.Center) * 0.05f;

            if (PhaseTimer >= PhaseShiftTime)
                AdvanceCycle();
        }

        /// <summary>当前阶段的脚本幕序列 (确定性, 压制/机动/区域/处决交替编排)。</summary>
        private BossPhase[] BuildActList() {
            if (isPhase3)
                return new[] {
                    BossPhase.GrudgeReckoning, BossPhase.VeilRush, BossPhase.PhantomReplay,
                    BossPhase.SoulStorm, BossPhase.Wailing
                };
            if (isPhase2)
                return new[] {
                    BossPhase.Haunting, BossPhase.VeilRush, BossPhase.SoulStorm,
                    BossPhase.GrudgeChain, BossPhase.Possession, BossPhase.Wailing
                };
            return new[] {
                BossPhase.Haunting, BossPhase.VeilRush, BossPhase.GrudgeChain, BossPhase.Wailing
            };
        }

        private void AdvanceCycle() {
            BossPhase[] acts = BuildActList();
            cycleIndex++;
            if (cycleIndex >= acts.Length) {
                cycleIndex = 0;
                cycleCount++;
            }
            TransitionTo(acts[cycleIndex]);
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SpecialTimer = 0;
            possSpawned = false;
            reckoningReleased = false;
            NPC.netUpdate = true;
        }

        /// <summary>清空本 Boss 名下全部敌对弹幕 (相变/附身破虚/死亡的公平阀门)。</summary>
        private void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int t1 = ModContent.ProjectileType<SpectreWraithBolt>();
            int t2 = ModContent.ProjectileType<SpectreSoulChain>();
            int t3 = ModContent.ProjectileType<SpectreSoulOrb>();
            int t4 = ModContent.ProjectileType<SpectreWailingWave>();
            int t5 = ModContent.ProjectileType<SpectrePhantomRush>();
            int t6 = ModContent.ProjectileType<SpectreReckoningWave>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.hostile) continue;
                if (p.type == t1 || p.type == t2 || p.type == t3 || p.type == t4 || p.type == t5 || p.type == t6)
                    p.Kill();
            }
        }

        private void TriggerEnergyWave() {
            for (int i = 0; i < waveRadius.Length; i++) {
                if (waveAlpha[i] <= 0.1f) {
                    waveRadius[i] = 0f;
                    waveAlpha[i] = 1f;
                    break;
                }
            }
        }

        private void UpdateEnergyWaves() {
            for (int i = 0; i < waveRadius.Length; i++) {
                if (waveAlpha[i] > 0f) {
                    waveRadius[i] += 12f;
                    waveAlpha[i] -= 0.018f;
                    if (waveAlpha[i] < 0f) waveAlpha[i] = 0f;
                }
            }
        }

        #endregion

        #region 入场演出 (灯阵凝形 → 静场 → 尖啸)

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = PhaseTimer < IntroScreamFrame;
            veilTarget = PhaseTimer < IntroScreamFrame ? 0.9f : 0.2f;

            // 锁定竞技场 + 灯阵先行 (灯是"因", 凝形是"果")
            if (PhaseTimer == 2) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    arenaCenter = target.Center;
                    arenaSet = true;
                    NPC.Center = arenaCenter + new Vector2(0, -260f);
                    NPC.velocity = Vector2.Zero;
                    SpawnLanternAnchors(4);
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);
            }

            // 灯焰逐盏亮起的音阶 (客户端)
            if (!Main.dedServ && (PhaseTimer == 15 || PhaseTimer == 30 || PhaseTimer == 45 || PhaseTimer == 60))
                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.5f + PhaseTimer / 60f * 0.6f, Volume = 0.7f }, NPC.Center);

            // 凝形: 溶解 1→0, 缩放 1.18→1 沉降
            float appear = SpectreHelper.SmoothStep(MathHelper.Clamp((PhaseTimer - 30f) / 80f, 0f, 1f));
            dissolveVisual = 1f - appear;
            bodyScaleMod = MathHelper.Lerp(1.18f, 1f, appear);
            NPC.velocity *= 0.8f;

            // 魂缕从四方汇入 (30~110), 110~140 静场断流 — 尖啸前的吸气
            if (!Main.dedServ && PhaseTimer > 30 && PhaseTimer < 110 && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 380f * (1f - appear * 0.6f);
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * dist;
                var d = Dust.NewDustPerfect(pos, Main.rand.NextBool(3) ? DustID.YellowTorch : DustID.IceTorch);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 7f;
            }

            if (PhaseTimer == IntroScreamFrame) {
                // 尖啸爆点 — 入场唯一大拍
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.5f, Pitch = 0.25f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 110f, 3, 16);
                TriggerEnergyWave();
                TriggerEnergyWave();
                flameVisual = 1.4f;
            }

            if (PhaseTimer > IntroScreamFrame) {
                // 落位至战斗高度
                Vector2 hold = (arenaSet ? arenaCenter : target.Center) + new Vector2(0, -180f + hoverOffset);
                NPC.Center += (hold - NPC.Center) * 0.06f;
            }

            if (PhaseTimer > IntroTime)
                AdvanceCycle();
        }

        #endregion

        #region 缠 Haunting (齐射循环: 前摇反漂 → 涟漪发射 → 收招)

        private void RunHaunting(Player target) {
            int duration = isPhase3 ? 240 : (isPhase2 ? 270 : 300);
            int local = (int)PhaseTimer % 90;
            int volley = (int)PhaseTimer / 90;
            int side = volley % 2 == 0 ? 1 : -1;

            Vector2 aim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            Vector2 hover = target.Center + new Vector2(
                side * (210f + MathF.Sin(PhaseTimer * 0.02f) * 40f),
                -170f + hoverOffset);

            // 前摇 6~30f: 反向漂移蓄势 (counter-motion), 尾段渐止
            if (local >= 6 && local < 30) {
                float t = (local - 6) / 24f;
                hover -= aim * t * t * 90f;
                flameTarget = 0.4f + t * 0.6f;
                if (!Main.dedServ && local % 3 == 0) {
                    Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(60, 60);
                    var d = Dust.NewDustPerfect(pos, DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = (NPC.Center + aim * 40f - pos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.07f, 0.08f);

            // 涟漪齐射: 逐发 6f 间隔, 每发反冲 (最小射距 200 内跳过 — 邀请贴身)
            int shots = isPhase3 ? 4 : (isPhase2 ? 3 : 2);
            for (int i = 0; i < shots; i++) {
                if (local == 30 + i * 6 && NPC.Distance(target.Center) > 200f) {
                    ShootWraithBolt(target, i - (shots - 1) / 2f);
                    NPC.velocity -= aim * 3f;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f + i * 0.12f }, NPC.Center);
                }
            }

            if (PhaseTimer > duration)
                AdvanceCycle();
        }

        #endregion

        #region 相位突袭 VeilRush (转虚游标 → 凝实收缩 → 单帧爆发)

        private void RunVeilRush(Player target) {
            int cycleLen = RushCycleLen;
            int launch = RushLaunchFrame;
            int reelStart = launch - 8;
            int brakeStart = launch + 18;
            int local = (int)PhaseTimer % cycleLen;
            int cyc = (int)PhaseTimer / cycleLen;

            if (PhaseTimer >= RushCycles * cycleLen || PhaseTimer > RushTimeout) {
                NPC.velocity *= 0.8f; // 状态退出不留残速
                AdvanceCycle();
                return;
            }

            // 首周期锁定侧向基准 (ai[3] 自动同步)
            if (PhaseTimer == 1)
                SpecialTimer = NPC.Center.X >= target.Center.X ? 1 : -1;
            float side = (cyc % 2 == 0 ? 1f : -1f) * (SpecialTimer >= 0 ? 1f : -1f);

            if (local < reelStart) {
                // —— 虚相游标: 无敌但绝不攻击 (诚实开关) ——
                veilTarget = 1f;
                Vector2 anchor = target.Center + new Vector2(side * 340f, -46f + hoverOffset);

                if (local < 12) {
                    NPC.velocity *= 0.9f;
                    if (local == 2)
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f, Volume = 0.6f }, NPC.Center);
                }
                else {
                    // 弹簧追锚 (不完美跟踪 = 角色感); 超距散形传送 (聚散成形, 虚相期专属)
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.11f, 0.10f);
                    if (local == 14 && NPC.Distance(anchor) > 1050f) {
                        SpectreHelper.CreateSpectreVortex(NPC.Center, 90f, 1.2f, 26); // 旧位散形
                        NPC.Center = anchor;
                        NPC.velocity = Vector2.Zero;
                        dissolveVisual = 1f; // 新位凝形 (下方衰减)
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = 0.3f, Volume = 0.7f }, anchor);
                        SpectreHelper.CreateSpectreVortex(anchor, 90f, 1.2f, 26);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            NPC.netUpdate = true;
                    }
                }

                // 冲刺方向连续跟踪 (带 9f 提前量), 供预告线; 锁定于 reelStart
                dashDir = (target.Center + target.velocity * 9f - NPC.Center).SafeNormalize(Vector2.UnitX);

                // 预告蜂鸣 (固定 36f 提前, 危险等级的可学习常数)
                if (local == launch - 36)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.55f, Volume = 0.85f }, NPC.Center);
            }
            else if (local < launch) {
                // —— 凝实收缩: pow² 后仰 + 缩身 — 爆发前的吸气 ——
                float t = (local - reelStart) / (float)(launch - reelStart);
                veilTarget = 1f - t;
                NPC.velocity = -dashDir * t * t * 10f;
                bodyScaleMod = MathHelper.Lerp(1f, 0.86f, t);
                flameTarget = 1.2f;
            }
            else if (local == launch) {
                // —— 单帧爆发 ——
                veilTarget = 0f;
                NPC.velocity = dashDir * RushSpeed;
                NPC.netUpdate = true;
                dashBlur = 1f;
                bodyScaleMod = 1.06f;
                ACMUtils.AddScreenShake(5f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.15f, Volume = 1.1f }, NPC.Center);
                // 反冲魂缕从尾部喷出
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center - dashDir * 40f, Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                        d.noGravity = true;
                        d.scale = 1.6f;
                        d.velocity = -dashDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f);
                    }
                }
            }
            else if (local < brakeStart) {
                // 冲刺直线段: 零转向 (直=快)
                veilTarget = 0f;
                dashBlur = MathHelper.Max(dashBlur, 0.7f);
                if (!Main.dedServ)
                    SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.8f);
            }
            else {
                // 硬刹 ×0.70 — 撞进站位的读感
                veilTarget = 0f;
                NPC.velocity *= 0.70f;
                bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 1f, 0.2f);
                if (local == brakeStart + 2 && !Main.dedServ) {
                    SpectreHelper.CreateSpectreBurst(NPC.Center, 60f, 2, 10);
                    SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.7f }, NPC.Center);
                }
            }

            // 凝形残余衰减 (散形传送后)
            dissolveVisual = MathHelper.Clamp(dissolveVisual - 0.09f, 0f, 1f);
        }

        #endregion

        #region 怨链 GrudgeChain (盘旋蓄力 → 逐条甩链)

        private void RunGrudgeChain(Player target) {
            // 中距悬停 (保持 ~380px, 链有飞行距离可读)
            Vector2 away = (NPC.Center - target.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 hover = target.Center + away * 380f + new Vector2(0, hoverOffset - 40f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.06f);

            // 灯笼补给 (教清账反制)
            if (PhaseTimer == 40 && CountLanterns() < 2)
                SpawnLanternAnchors(2);

            int chains = isPhase3 ? 5 : (isPhase2 ? 4 : 3);

            // 30~70f 盘旋蓄力: 锁链环收紧 + 体表颤动 + 音调爬升
            if (PhaseTimer >= 30 && PhaseTimer < 70) {
                float t = (PhaseTimer - 30f) / 40f;
                bodyJitter = t * 2f;
                flameTarget = 0.4f + t * 0.7f;
                if (!Main.dedServ && PhaseTimer % 2 == 0) {
                    float radius = MathHelper.Lerp(160f, 60f, t);
                    float ang = pulsePhase * 3f + Main.rand.NextFloat(MathHelper.TwoPi);
                    var d = Dust.NewDustPerfect(NPC.Center + ang.ToRotationVector2() * radius, DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 4f;
                }
                if (PhaseTimer == 30 || PhaseTimer == 50 || PhaseTimer == 65)
                    SoundEngine.PlaySound(SoundID.Item125 with { Pitch = -0.4f + (PhaseTimer - 30f) / 35f * 0.7f, Volume = 0.8f }, NPC.Center);
            }

            // 两轮逐条甩链 (8f 间隔, 每条反冲 + 轻震)
            ThrowChainWave(target, chains, 70, 1f);
            ThrowChainWave(target, chains, 160, -1f);

            if (PhaseTimer > 280)
                AdvanceCycle();
        }

        private void ThrowChainWave(Player target, int chains, int startFrame, float mirror) {
            for (int i = 0; i < chains; i++) {
                if ((int)PhaseTimer != startFrame + i * 8)
                    continue;
                float spread = mirror * (i - (chains - 1) / 2f) * 0.22f;
                Vector2 from = NPC.Center + (spread * 2.2f).ToRotationVector2() * 50f;
                ShootSoulChain(from, target, spread);
                Vector2 aim = (target.Center - from).SafeNormalize(Vector2.UnitY);
                NPC.velocity -= aim * 5f;
                ACMUtils.AddScreenShake(2f);
                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.3f + i * 0.06f }, NPC.Center);
            }
        }

        #endregion

        #region 哀嚎 Wailing (处决级蓄力 → 尖啸环, 灯道 = 安全缝)

        private void RunWailing(Player target) {
            Vector2 hold = (arenaSet ? arenaCenter : target.Center) + new Vector2(0, -140f);
            NPC.Center += (hold - NPC.Center) * 0.10f;
            NPC.velocity *= 0.8f;

            // 灯道保底: 蓄力开场确保至少一盏灯 (无灯则玩家无路可站)
            if (PhaseTimer == 2 && Main.netMode != NetmodeID.MultiplayerClient && CountLanterns() == 0)
                SpawnLanternAt(arenaCenter + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * 320f);

            if (PhaseTimer < WailSilenceFrame) {
                // 蓄力: 魂缕内收密度 ∝ √t, 震屏 t² 爬升
                float t = PhaseTimer / (float)WailReleaseFrame;
                flameTarget = 0.4f + t * 1f;
                if (!Main.dedServ && Main.rand.NextFloat() < MathF.Sqrt(t)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 260f * (1f - t * 0.7f);
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * dist;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 8f;
                }
                ACMUtils.AddScreenShake(t * t * 6f);
            }
            else if (PhaseTimer < WailReleaseFrame) {
                // —— 断流静默: 尖啸前的吸气 (粒子/震动全停, 本体收缩颤抖) ——
                float t = (PhaseTimer - WailSilenceFrame) / (float)(WailReleaseFrame - WailSilenceFrame);
                bodyScaleMod = MathHelper.SmoothStep(1f, 0.92f + MathF.Cos(PhaseTimer * 0.9f) * 0.015f, t);
                flameTarget = 1.4f;
            }

            // 灯道锁定 (服务器 55f 选最近灯笼, 同步; 33f 亮灯道预告)
            if (PhaseTimer == WailLaneLockFrame && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile lantern = FindNearestLantern(NPC.Center);
                laneAngle = lantern != null
                    ? (lantern.Center - NPC.Center).ToRotation()
                    : (target.Center - NPC.Center).ToRotation(); // 兜底: 灯道让给玩家当前方向
                NPC.netUpdate = true;
            }

            // 尖啸环: 88 / 100 / (P3) 110, 全部跳过灯道
            if (PhaseTimer == WailReleaseFrame) {
                ReleaseWailRing(0f, 0.34f);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.5f, Volume = 1.5f }, NPC.Center);
                for (int i = 0; i < 2; i++) TriggerEnergyWave();
                ACMUtils.AddScreenShake(11f);
                SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 1f);
                bodyScaleMod = 1.1f;
            }
            if (PhaseTimer == WailReleaseFrame + 12) {
                ReleaseWailRing(MathHelper.Pi / WailRingCount, 0.30f);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.7f, Volume = 1.1f }, NPC.Center);
            }
            if (isPhase3 && PhaseTimer == WailReleaseFrame + 22) {
                ReleaseWailRing(0f, 0.30f);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.9f, Volume = 0.9f }, NPC.Center);
            }

            // 收招: 喘息下垂 (伤害窗口邀请)
            if (PhaseTimer > WailReleaseFrame + 22) {
                veilTarget = 0.2f;
                NPC.position.Y += 0.6f;
                bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 1f, 0.1f);
            }

            if (PhaseTimer > WailDuration)
                AdvanceCycle();
        }

        private void ReleaseWailRing(float offset, float gapHalf) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int count = WailRingCount;
            int damage = GetBossDamage(0.85f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + offset;
                if (MathF.Abs(MathHelper.WrapAngle(angle - laneAngle)) < gapHalf)
                    continue; // 灯道安全缝
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 2.2f,
                    ModContent.ProjectileType<SpectreWailingWave>(), damage, 2f);
            }
        }

        #endregion

        #region 附身 Possession (转虚召分身, 必杀破虚, 虚相全程停火)

        private void RunPossession(Player target) {
            Vector2 hover = (arenaSet ? arenaCenter : target.Center) + new Vector2(0, -230f + hoverOffset);
            NPC.Center += (hover - NPC.Center) * 0.04f;
            NPC.velocity *= 0.9f;

            if (!possSpawned && PhaseTimer == 40) {
                possSpawned = true;
                for (int i = 0; i < possMinions.Length; i++) possMinions[i] = -1;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 数量阀门: 全局存活分身封顶 MinionCap
                    int aliveGlobal = 0;
                    int minionType = ModContent.NPCType<SpectreMinion>();
                    foreach (var n in Main.ActiveNPCs)
                        if (n.type == minionType) aliveGlobal++;

                    int toSpawn = Math.Min(MinionCap - aliveGlobal, possMinions.Length);
                    for (int i = 0; i < toSpawn; i++) {
                        float angle = MathHelper.TwoPi * i / Math.Max(1, toSpawn) - MathHelper.PiOver2;
                        Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 110f;
                        possMinions[i] = NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y,
                            minionType, 0, NPC.whoAmI);
                    }
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f }, NPC.Center);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 100f, 3, 16);
            }

            // 门控: 分身存活时 Boss 转虚 (无敌 + 完全停火 — 威胁诚实地转移给分身)
            int aliveMinions = CountAliveMinions(out int killed);
            bool gateOpen = !possSpawned || killed >= PossessionGate || aliveMinions == 0 || PhaseTimer > PossessionTimeout;

            if (possSpawned && !gateOpen) {
                veilTarget = 1f;
                NPC.dontTakeDamage = true;
            }

            if (gateOpen && PhaseTimer > 80) {
                // 破虚正反馈: 净化残余分身 + 清弹脉冲 + 怨念减免
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    KillOwnedMinions();
                    ClearHostileProjectiles();
                    UnderworldField.ReduceGrudge(NPC, 6);
                }
                for (int i = 0; i < 2; i++) TriggerEnergyWave();
                SpectreHelper.CreateScreenFlash(NPC.Center, TelegraphColors.GhostGreen, 0.7f);
                ACMUtils.AddScreenShake(8f);
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.6f, Volume = 1.1f }, NPC.Center);
                AdvanceCycle();
            }
        }

        #endregion

        #region 灵魂风暴 SoulStorm (旋转安全扇区, 中途反转)

        // 扇区完全由同步量确定: 基准角随 cycleCount 变化, 170f 处反转旋向
        private float StormSafeAngle() {
            float baseA = cycleCount * 1.7f + (isPhase3 ? 0.9f : 0f);
            float t = PhaseTimer;
            const float flip = 170f;
            const float speed = 0.018f;
            return t < flip ? baseA + t * speed : baseA + flip * speed - (t - flip) * speed;
        }

        private void RunSoulStorm(Player target) {
            Vector2 hover = (arenaSet ? arenaCenter : target.Center) + new Vector2(0, -60f + hoverOffset * 0.4f);
            NPC.Center += (hover - NPC.Center) * 0.05f;
            NPC.velocity *= 0.92f;
            flameTarget = 0.7f;

            float safeAngle = StormSafeAngle();
            const float gapHalf = 0.45f;

            if (AttackTimer % 28 == 0 && PhaseTimer > 50 && PhaseTimer < 290)
                ShootSoulStormRing(safeAngle, gapHalf);

            // 反转预告: 140~170f 扇区边缘黄闪 (变奏先说后做)
            if (!Main.dedServ && PhaseTimer >= 140 && PhaseTimer < 170 && PhaseTimer % 4 == 0) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 dir = (safeAngle + s * gapHalf).ToRotationVector2();
                    var d = Dust.NewDustPerfect(NPC.Center + dir * 230f, DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = dir * 3f;
                }
            }

            // 安全缝鬼绿指示 (绿=可站)
            if (!Main.dedServ && PhaseTimer % 3 == 0 && PhaseTimer > 30) {
                Vector2 dir = safeAngle.ToRotationVector2();
                var d = Dust.NewDustPerfect(NPC.Center + dir * Main.rand.NextFloat(160f, 320f), DustID.GreenTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = dir * 2f;
            }

            if (PhaseTimer > 320)
                AdvanceCycle();
        }

        #endregion

        #region 冤魂审判 GrudgeReckoning (P3 镜像清算 set-piece)

        private void RunGrudgeReckoning(Player target) {
            Vector2 center = arenaSet ? arenaCenter : target.Center;
            NPC.Center += (center + new Vector2(0, -40f) - NPC.Center) * 0.06f;
            NPC.velocity *= 0.85f;

            if (!reckoningReleased) {
                NPC.dontTakeDamage = true; // set-piece 不可被秒断 (蓄力全程零攻击)

                if (PhaseTimer == 1) {
                    retaliationAngle = PickRetaliationAngle();
                    SpawnLanternAnchors(4); // 清账反制窗口
                    SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
                    NPC.netUpdate = true;
                }

                float t = PhaseTimer / (float)ReckoningCharge;
                flameTarget = 0.4f + t * 1.1f;
                ACMUtils.AddScreenShake(MathHelper.Lerp(0.5f, 7f, t * t));

                // 账本魂缕内收 — 72% 处硬切, 末段静默中只有光束转红
                if (!Main.dedServ && PhaseTimer < ReckoningCutFrame && PhaseTimer % 2 == 0) {
                    float dist = 340f * (1f - t);
                    Vector2 pos = NPC.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * dist;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 9f;
                }
                if (PhaseTimer >= ReckoningCutFrame)
                    bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 0.93f, 0.1f);

                if (PhaseTimer >= ReckoningCharge) {
                    reckoningReleased = true;
                    PhaseTimer = 0;
                    bodyScaleMod = 1.12f;
                    ReleaseReckoning(target);
                    NPC.netUpdate = true;
                }
            }
            else {
                bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 1f, 0.08f);
                if (PhaseTimer > ReckoningRecover)
                    AdvanceCycle();
            }
        }

        private float PickRetaliationAngle() {
            int best = 0;
            float bestVal = -1f;
            for (int i = 0; i < sectorTime.Length; i++) {
                if (sectorTime[i] > bestVal) {
                    bestVal = sectorTime[i];
                    best = i;
                }
            }
            // 衰减历史, 让每次清算反映最近停留
            for (int i = 0; i < sectorTime.Length; i++) sectorTime[i] *= 0.4f;
            return best * (MathHelper.TwoPi / 8f);
        }

        private void ReleaseReckoning(Player target) {
            float g = UnderworldField.GetGrudgeNormalized(NPC);
            ACMUtils.AddScreenShake(12f);
            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreRage, 1f);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.2f, Volume = 1.6f }, NPC.Center);
            for (int i = 0; i < 3; i++) TriggerEnergyWave();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 center = arenaSet ? arenaCenter : target.Center;

            // ONE 扩张报复波 (规模随怨念账)
            Projectile.NewProjectile(NPC.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<SpectreReckoningWave>(), GetBossDamage(1.1f), 4f,
                ai0: g);

            // 幻影突袭: 久留象限 + 对向合围 (积怨深则加一道)
            int rushCount = 2 + (g > 0.6f ? 1 : 0);
            for (int i = 0; i < rushCount; i++) {
                float ang = retaliationAngle + (i == 1 ? MathHelper.Pi : (i == 2 ? MathHelper.PiOver2 : 0f));
                Vector2 from = center + ang.ToRotationVector2() * 700f;
                Vector2 aim = (target.Center - from).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), from, aim * 0.1f,
                    ModContent.ProjectileType<SpectrePhantomRush>(), GetBossDamage(1f), 3f,
                    ai0: target.whoAmI);
            }

            // 怨念部分释放 (终幕代价已偿一部分)
            UnderworldField.ReduceGrudge(NPC, 35);
            NPC.netUpdate = true;
        }

        #endregion

        #region 幻影重演 PhantomReplay (P3: 金线记录 → 冲刺 → 幻影重演)

        private void RunPhantomReplay(Player target) {
            const int cycleLen = 130;
            const int launch = 36;
            const int dashEnd = 58;
            const int replayFrame = 80;
            int local = (int)PhaseTimer % cycleLen;

            if (PhaseTimer >= 2 * cycleLen || PhaseTimer > 300) {
                NPC.velocity *= 0.8f;
                AdvanceCycle();
                return;
            }

            if (local < launch) {
                // 金色记录预告: "这条路会被记住"
                NPC.velocity *= 0.88f;
                flameTarget = 0.9f;
                if (local <= 20)
                    dashDir = (target.Center + target.velocity * 9f - NPC.Center).SafeNormalize(Vector2.UnitX);
                if (local == 20 && Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true; // 锁定方向同步
                if (local == 0)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 0.9f }, NPC.Center);
                // 末 8f 凝实收缩
                if (local >= launch - 8) {
                    float t = (local - (launch - 8)) / 8f;
                    NPC.velocity = -dashDir * t * t * 8f;
                    bodyScaleMod = MathHelper.Lerp(1f, 0.88f, t);
                }
            }
            else if (local == launch) {
                replayStart = NPC.Center;
                NPC.velocity = dashDir * 44f;
                dashBlur = 1f;
                bodyScaleMod = 1.06f;
                ACMUtils.AddScreenShake(5f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0f, Volume = 1.1f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
            }
            else if (local < dashEnd) {
                dashBlur = MathHelper.Max(dashBlur, 0.7f);
                if (!Main.dedServ)
                    SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.8f);
            }
            else if (local == dashEnd) {
                replayEnd = NPC.Center;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
            }
            else {
                NPC.velocity *= 0.72f;
                bodyScaleMod = MathHelper.Lerp(bodyScaleMod, 1f, 0.15f);
            }

            // 幻影重演: 记录线段 ±55px 平行, 各带完整前摇
            if (local == replayFrame && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 lineDir = (replayEnd - replayStart).SafeNormalize(dashDir);
                Vector2 perp = lineDir.RotatedBy(MathHelper.PiOver2);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 from = replayStart + perp * s * 55f - lineDir * 30f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), from, lineDir * 0.1f,
                        ModContent.ProjectileType<SpectrePhantomRush>(), GetBossDamage(1f), 3f,
                        ai0: -1f); // 定向重演模式
                }
            }
            if (local == replayFrame)
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
        }

        #endregion

        #region 死亡演出 (CheckDead 截获, 210f 送葬脚本)

        public override bool CheckDead() {
            if (Phase == BossPhase.Death)
                return PhaseTimer >= DeathDuration - 2;

            BeginDeath();
            return false;
        }

        private void BeginDeath() {
            TransitionTo(BossPhase.Death);
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            deathKnockDir = (NPC.Center - Target.Center).SafeNormalize(-Vector2.UnitY);
            deathStrobePhase = 0f;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                ClearHostileProjectiles();
                KillOwnedMinions();
                // 灯笼保留 — 死亡演出里逐盏熄灭送葬
            }
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.7f, Volume = 1.5f }, NPC.Center);
        }

        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            if (PhaseTimer < 40) {
                // 踉跄后仰: 锁链崩断
                float t = PhaseTimer / 40f;
                NPC.velocity = deathKnockDir * (1f - t) * 4f;
                NPC.rotation = MathHelper.Lerp(NPC.rotation, deathKnockDir.X > 0 ? 0.18f : -0.18f, 0.1f);
                if (!Main.dedServ && (PhaseTimer == 5 || PhaseTimer == 18 || PhaseTimer == 32)) {
                    SpectreHelper.CreateSpectreBurst(NPC.Center, 70f, 2, 12);
                    SoundEngine.PlaySound(SoundID.Item125 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
                }
            }
            else if (PhaseTimer < DeathCollapseFrame) {
                // 灯笼每 20f 逐盏熄灭 (服务器); 本体加速频闪 (心跳变尖啸)
                NPC.velocity *= 0.9f;
                if (Main.netMode != NetmodeID.MultiplayerClient && (int)PhaseTimer % 20 == 0) {
                    Projectile lantern = FindNearestLantern(NPC.Center);
                    lantern?.Kill();
                }

                float k = (PhaseTimer - 40f) / 80f;
                float period = MathHelper.Lerp(24f, 4f, k * k);
                float prevStrobe = deathStrobePhase;
                deathStrobePhase += 1f / period;
                bool lit = (int)deathStrobePhase % 2 == 0;
                veilTarget = lit ? 0.1f : 0.95f;
                // 每次翻相位放一声心跳 (延迟数组加速 → 心跳变尖啸)
                if (!Main.dedServ && (int)deathStrobePhase != (int)prevStrobe && lit)
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.4f + k, Volume = 0.5f }, NPC.Center);
            }
            else if (PhaseTimer < DeathBurstFrame) {
                // 坍缩: 缩至 40% + 余弦频闪, 亮度反升 — 爆前变小
                float t = (PhaseTimer - DeathCollapseFrame) / (float)(DeathBurstFrame - DeathCollapseFrame);
                NPC.velocity *= 0.85f;
                bodyScaleMod = MathHelper.SmoothStep(1f, 0.4f, t) * (1f + MathF.Cos(PhaseTimer * 1.1f) * 0.06f * t);
                veilTarget = 0f;
                flameTarget = 1.5f;
            }
            else if (PhaseTimer == DeathBurstFrame) {
                // 唯一一次全场大爆 (震 16 是本 Boss 保留的最大震级)
                ACMUtils.AddScreenShake(16f);
                SpectreHelper.CreateSpectreVortex(NPC.Center, 200f, 1.5f, 80);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 180f, 5, 25);
                SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreYellow, 1.2f);
                for (int i = 0; i < 3; i++) TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.2f, Volume = 1.6f }, NPC.Center);
            }
            else {
                // 魂缕雨向上飘散
                NPC.velocity *= 0.9f;
                dissolveVisual = MathHelper.Clamp(dissolveVisual + 0.08f, 0f, 1f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(120, 80),
                        Main.rand.NextBool(3) ? DustID.YellowTorch : DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2f, 5f));
                }
            }

            if (PhaseTimer >= DeathDuration - 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead(); // CheckDead 此时放行 → OnKill 掉落/进度照常
            }
        }

        #endregion

        #region 灯笼锚点 / 分身门控

        private void SpawnLanternAnchors(int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !arenaSet)
                return;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + 0.3f;
                Vector2 pos = arenaCenter + angle.ToRotationVector2() * new Vector2(360f, 240f);
                SpawnLanternAt(pos);
            }
        }

        private void SpawnLanternAt(Vector2 pos) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<SpectreLanternAnchor>(), 0, 0f, Main.myPlayer, NPC.whoAmI);
        }

        private int CountLanterns() {
            int type = ModContent.ProjectileType<SpectreLanternAnchor>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                    count++;
            }
            return count;
        }

        private Projectile FindNearestLantern(Vector2 from) {
            int type = ModContent.ProjectileType<SpectreLanternAnchor>();
            Projectile best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type) continue;
                float d = p.DistanceSQ(from);
                if (d < bestDist) {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        private int CountAliveMinions(out int killed) {
            int alive = 0;
            int spawned = 0;
            int type = ModContent.NPCType<SpectreMinion>();
            for (int i = 0; i < possMinions.Length; i++) {
                int idx = possMinions[i];
                if (idx < 0 || idx >= Main.maxNPCs)
                    continue; // 未占用的槽位
                spawned++;
                NPC m = Main.npc[idx];
                if (m.active && m.type == type) alive++;
            }
            killed = spawned - alive;
            return alive;
        }

        private void KillOwnedMinions() {
            int type = ModContent.NPCType<SpectreMinion>();
            for (int i = 0; i < possMinions.Length; i++) {
                int idx = possMinions[i];
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].type == type) {
                    NPC m = Main.npc[idx];
                    m.life = 0;
                    m.HitEffect();
                    m.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                possMinions[i] = -1;
            }
        }

        #endregion

        #region 攻击发射

        private void ShootWraithBolt(Player target, float spreadIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.8f);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            Vector2 dir = toPlayer.RotatedBy(spreadIndex * 0.12f);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 40f, dir * 8.6f,
                ModContent.ProjectileType<SpectreWraithBolt>(), damage, 2f);
        }

        private void ShootSoulChain(Vector2 from, Player target, float spread) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.85f);
            Vector2 toPlayer = (target.Center - from).SafeNormalize(Vector2.UnitY).RotatedBy(spread * 0.5f);
            Vector2 aimPos = target.Center + toPlayer * 60f;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, toPlayer * 11f,
                ModContent.ProjectileType<SpectreSoulChain>(), damage, 3f,
                ai0: aimPos.X, ai1: aimPos.Y);
        }

        private void ShootSoulStormRing(float safeAngle, float gapHalf) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.7f);
            int count = isPhase3 ? 16 : 14;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                float diff = MathHelper.WrapAngle(angle - safeAngle);
                if (MathF.Abs(diff) < gapHalf) continue; // 旋转安全扇区
                Vector2 dir = angle.ToRotationVector2();
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 7.4f,
                    ModContent.ProjectileType<SpectreSoulOrb>(), damage, 1f, ai0: i % 2);
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, NPC.Center);
        }

        public int GetBossDamage(float scaling = 1f) => (int)(NPC.damage * scaling);

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 竞技场地纹 / 预告线 / 灯道 / 重演金线 (自管批)
            DrawArenaTells(spriteBatch);

            DrawEnergyWaves();

            // 本体 + 鬼影残像 (SpectreVeil 统一 pass)
            DrawVeilBody(spriteBatch, screenPos);

            // 蓄力核心辉光 (处决级招式的进度可读)
            bool wailCharge = Phase == BossPhase.Wailing && PhaseTimer < WailReleaseFrame;
            bool reckonCharge = Phase == BossPhase.GrudgeReckoning && !reckoningReleased;
            if (wailCharge || reckonCharge) {
                float prog = reckonCharge ? PhaseTimer / (float)ReckoningCharge : PhaseTimer / (float)WailReleaseFrame;
                SpectreHelper.DrawSpectreCore(spriteBatch, NPC.Center + new Vector2(0, -6f),
                    SpectreHelper.SpectreCyan, SpectreHelper.SpectreEmber,
                    0.5f + prog * 0.9f, pulsePhase, reckonCharge && prog > 0.72f);
            }

            if (isPhase2 && Phase != BossPhase.Death)
                SpectreHelper.DrawSoulOrbit(spriteBatch, NPC.Center, 70f, isPhase3 ? 5 : 3,
                    pulsePhase * 0.8f, pulsePhase);

            return false;
        }

        /// <summary>本体 + 残像统一走 SpectreVeil (残像速度门控: 静止不显, 冲刺增强)。</summary>
        private void DrawVeilBody(SpriteBatch sb, Vector2 screenPos) {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects fxFlip = faceRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float speed = NPC.velocity.Length();
            float speedGate = Utils.GetLerpValue(3f, 14f, speed, true);
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.05f;
            float scale = NPC.scale * bodyScaleMod * pulse;

            // 怨念染色: 青 → 黄 (账越厚越褪成纸钱黄); P3 叠狂怒红脉动
            float g = GrudgeNorm();
            Color tint = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.25f + g * 0.4f);
            float tintAmt = 0.35f + g * 0.2f;
            if (isPhase3)
                tint = Color.Lerp(tint, SpectreHelper.SpectreRage, (MathF.Sin(pulsePhase * 3f) * 0.5f + 0.5f) * 0.35f);
            Color edge = Color.Lerp(SpectreHelper.SpectreGhostFlame, SpectreHelper.SpectreCyan, 0.4f);

            Vector2 jitter = bodyJitter > 0.01f ? Main.rand.NextVector2Circular(bodyJitter, bodyJitter) : Vector2.Zero;

            // UV 空间冲刺方向 (翻面时镜像 X)
            Vector2 dashUV = new(dashDir.X * (faceRight ? 1f : -1f), dashDir.Y);

            if (SpectreHelper.BeginVeilBatch(sb)) {
                // 残像 (旧→新), 静止时门控为零
                if (speedGate > 0.05f) {
                    for (int i = 14; i >= 2; i -= 3) {
                        if (i >= NPC.oldPos.Length || NPC.oldPos[i] == Vector2.Zero) continue;
                        float prog = 1f - i / 16f;
                        float op = 0.30f * prog * speedGate * (0.6f + dashBlur * 0.9f);
                        if (op < 0.02f) continue;
                        SpectreHelper.ApplyVeilParams(
                            MathHelper.Min(1f, veilVisual + 0.45f + (1f - prog) * 0.3f), dissolveVisual,
                            op, 0f, dashUV, dashBlur * 0.6f, tint, tintAmt, edge, 0.7f);
                        sb.Draw(tex, NPC.oldPos[i] + NPC.Size / 2 - screenPos, null, Color.White,
                            NPC.rotation, origin, scale * (0.85f + prog * 0.15f), fxFlip, 0);
                    }
                }

                // 本体
                SpectreHelper.ApplyVeilParams(veilVisual, dissolveVisual, 1f, flameVisual,
                    dashUV, dashBlur, tint, tintAmt, edge, 0.9f);
                sb.Draw(tex, NPC.Center + jitter - screenPos, null, Color.White,
                    NPC.rotation, origin, scale, fxFlip, 0);

                SpectreHelper.EndVeilBatch(sb);
            }
            else {
                // 着色器不可用: 普通褪色绘制兜底
                Color body = tint * (1f - veilVisual * 0.6f) * (1f - dissolveVisual);
                sb.Draw(tex, NPC.Center + jitter - screenPos, null, body, NPC.rotation, origin, scale, fxFlip, 0);
            }
        }

        private void DrawArenaTells(SpriteBatch sb) {
            if (Main.dedServ) return;

            bool wailCharge = Phase == BossPhase.Wailing && PhaseTimer < WailReleaseFrame;
            bool reckonCharge = Phase == BossPhase.GrudgeReckoning && !reckoningReleased;
            bool storm = Phase == BossPhase.SoulStorm;

            // 竞技场地纹环 (ArenaRunic, 状态互斥 → 每帧至多 1 个)
            if (arenaSet && (wailCharge || reckonCharge || storm)) {
                float intensity = storm ? 0.5f : (reckonCharge
                    ? MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0.2f, 1f)
                    : MathHelper.Clamp(PhaseTimer / (float)WailReleaseFrame, 0.2f, 0.9f));
                Color prim = reckonCharge ? TelegraphColors.NetherViolet : TelegraphColors.GhostGreen;
                float radius = reckonCharge ? 520f : 300f;
                DrawArenaRing(sb, NPC.Center, radius, intensity, prim, SpectreHelper.SpectreYellow);
            }

            // 审判报复来向光束 (末段转红 = 致命源)
            if (reckonCharge && PhaseTimer > 8) {
                Vector2 dir = retaliationAngle.ToRotationVector2();
                Vector2 center = arenaSet ? arenaCenter : NPC.Center;
                float prog = MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0f, 1f);
                bool imminent = PhaseTimer >= ReckoningCutFrame;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                Color edgeC = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                ACMShaders.DrawBeam(center + dir * 700f, center, MathHelper.Lerp(8f, 26f, prog), core, edgeC, 0.4f + 0.6f * prog);
            }

            // 相位突袭冲刺预告线 (青白 → 末 10f 红)
            if (Phase == BossPhase.VeilRush) {
                int cycleLen = RushCycleLen;
                int launch = RushLaunchFrame;
                int local = (int)PhaseTimer % cycleLen;
                if (local >= 18 && local < launch) {
                    float prog = (local - 18f) / (launch - 18f);
                    bool imminent = local >= launch - 10;
                    Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                    Color edgeC = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                    ACMShaders.DrawBeam(NPC.Center, NPC.Center + dashDir * 900f,
                        MathHelper.Lerp(4f, 14f, prog), core, edgeC, 0.25f + 0.65f * prog);
                }
            }

            // 哀嚎灯道 (鬼绿 = 唯一安全缝, "顺着灯走")
            if (Phase == BossPhase.Wailing && PhaseTimer >= WailLaneLockFrame && PhaseTimer < WailReleaseFrame + 26) {
                float lanePulse = 0.55f + MathF.Sin(pulsePhase * 2.4f) * 0.2f;
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + laneAngle.ToRotationVector2() * 640f,
                    26f, TelegraphColors.GhostGreen, TelegraphColors.Safe, lanePulse, 1.8f, 1.6f);
            }

            // 幻影重演: 金色预告线 + 渐隐记录线
            if (Phase == BossPhase.PhantomReplay) {
                const int cycleLen = 130;
                const int launch = 36;
                int local = (int)PhaseTimer % cycleLen;
                if (local < launch) {
                    float prog = local / (float)launch;
                    bool imminent = local >= launch - 10;
                    Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Gold;
                    ACMShaders.DrawBeam(NPC.Center, NPC.Center + dashDir * 900f,
                        MathHelper.Lerp(4f, 13f, prog), core, SpectreHelper.SpectreGold, 0.3f + 0.6f * prog);
                }
                else if (local > 58 && replayStart != replayEnd) {
                    // 记录线渐隐 → 幻影到来前重新亮起 (二次预告)
                    float sinceDash = local - 58f;
                    float fade = sinceDash < 14f ? 1f - sinceDash / 14f * 0.6f
                        : MathHelper.Clamp((sinceDash - 14f) / 8f, 0f, 1f) * 0.8f;
                    Color core = sinceDash > 14f ? TelegraphColors.Lethal : SpectreHelper.SpectreGold;
                    ACMShaders.DrawBeam(replayStart, replayEnd, 10f, core, SpectreHelper.SpectreGold, fade * 0.6f);
                }
            }

            // 单帧释放泛光 (与蓄力期全屏冥雾错帧, 不抢名额)
            if (Phase == BossPhase.Wailing && PhaseTimer == WailReleaseFrame)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, 0.8f, SpectreHelper.SpectreCyan);
            else if (Phase == BossPhase.GrudgeReckoning && reckoningReleased && PhaseTimer < 5)
                ACMShaders.DrawRadialBloomAt(arenaSet ? arenaCenter : NPC.Center, 0.3f, 1f, SpectreHelper.SpectreRage);
            else if (Phase == BossPhase.Intro && PhaseTimer >= IntroScreamFrame && PhaseTimer < IntroScreamFrame + 3)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.24f, 0.85f, SpectreHelper.SpectreGhostFlame);
            else if (Phase == BossPhase.Death && PhaseTimer >= DeathBurstFrame && PhaseTimer < DeathBurstFrame + 4)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.34f, 1f, SpectreHelper.SpectreYellow);
        }

        private void DrawArenaRing(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity, Color prim, Color sec) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null) return;
            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float rf, out float asp);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rf);
            fx.Parameters["uAspect"]?.SetValue(asp);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorPrimary"]?.SetValue(prim.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(sec.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx);
        }

        private void DrawEnergyWaves() {
            for (int i = 0; i < waveRadius.Length; i++) {
                if (waveAlpha[i] <= 0.05f) continue;
                Color inner = isPhase3
                    ? SpectreHelper.SpectreRage
                    : (i % 2 == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow);
                WeaponVFX.DrawShockwaveRing(NPC.Center, waveRadius[i], 24f, waveAlpha[i] * 0.6f,
                    inner, SpectreHelper.SpectreDeepCyan);
            }
        }

        /// <summary>
        /// 全屏后处理 (每帧 ≤1, RequestFullscreenSlot 仲裁):
        /// 死亡坍缩 void &gt; 审判冥雾 fog &gt; 怨念褪色 PaletteLUT。强度 &lt; 0.05 直接 return。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // 1) 死亡坍缩 void 收束
            if (Phase == BossPhase.Death) {
                float t = Utils.GetLerpValue(DeathCollapseFrame, DeathBurstFrame, PhaseTimer, true)
                        * Utils.GetLerpValue(DeathBurstFrame + 12, DeathBurstFrame, PhaseTimer, true);
                if (t < 0.05f || !ACMShaders.RequestFullscreenSlot())
                    return;
                Effect vfx = ACMShaders.GenericWarp;
                if (vfx == null) return;
                ACMShaders.SetCommonParams(vfx, NPC.Center, t * 0.85f);
                vfx.Parameters["uRadius"]?.SetValue(0.9f);
                vfx.Parameters["uWarpScale"]?.SetValue(1.2f);
                vfx.Parameters["uChroma"]?.SetValue(0.6f);
                vfx.Parameters["uRadialPull"]?.SetValue(0.45f);
                vfx.Parameters["uMode"]?.SetValue(4f); // void
                vfx.Parameters["uTint"]?.SetValue(new Vector4(0.16f, 0.30f, 0.27f, 0.8f));
                ACMShaders.ApplyScreenPostProcess(spriteBatch, vfx, bindNoise: true);
                return;
            }

            bool charging = Phase == BossPhase.GrudgeReckoning && !reckoningReleased;
            float fog = charging ? MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0f, 1f) : 0f;
            float g = GrudgeNorm();
            if (g < 0.05f && fog < 0.05f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Vector2 center = arenaSet ? arenaCenter : NPC.Center;

            // 2) 审判冥雾
            if (fog > 0.05f) {
                Effect fx = ACMShaders.GenericWarp;
                if (fx == null) return;
                ACMShaders.SetCommonParams(fx, center, fog);
                fx.Parameters["uRadius"]?.SetValue(1.4f);
                fx.Parameters["uWarpScale"]?.SetValue(1.1f);
                fx.Parameters["uChroma"]?.SetValue(0.5f);
                fx.Parameters["uRadialPull"]?.SetValue(0.2f);
                fx.Parameters["uMode"]?.SetValue(2f); // fog
                fx.Parameters["uTint"]?.SetValue(new Vector4(0.22f, 0.34f, 0.27f, 0.85f));
                ACMShaders.ApplyScreenPostProcess(spriteBatch, fx, bindNoise: true);
            }
            // 3) 怨念账褪色
            else {
                Effect fx = ACMShaders.PaletteLUT;
                if (fx == null) return;
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(g, 0f, 1f));
                fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                fx.Parameters["uSaturation"]?.SetValue(MathHelper.Lerp(1f, 0.2f, g)); // 怨念越高越褪色
                fx.Parameters["uHueShift"]?.SetValue(0.06f * g);
                fx.Parameters["uShadowTint"]?.SetValue(new Vector4(0.40f, 0.55f, 0.50f, 0.40f)); // 青绿阴影
                fx.Parameters["uHighlightTint"]?.SetValue(new Vector4(0.92f, 0.85f, 0.45f, 0.30f)); // 青黄高光
                fx.Parameters["uSplit"]?.SetValue(0f);
                ACMShaders.ApplyScreenPostProcess(spriteBatch, fx, bindNoise: false);
            }
        }

        #endregion

        public override bool CheckActive() => false;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SpectreGrudgeCore>(), 1, 4, 7));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulFragment>(), 1, 3, 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<WraithLantern>(), 7));
        }

        public override void OnKill() {
            // 大爆已在死亡脚本 150f 打过 — 此处只做谢幕余韵
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            SpectreHelper.CreateSpectreVortex(NPC.Center, 140f, 1f, 40);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                KillOwnedMinions();
                // 熄灭残余灯笼
                int type = ModContent.ProjectileType<SpectreLanternAnchor>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    if (Main.projectile[i].active && Main.projectile[i].type == type)
                        Main.projectile[i].Kill();
                }
                DownedBossSystem.downedSpectre = true;
            }
        }
    }
}

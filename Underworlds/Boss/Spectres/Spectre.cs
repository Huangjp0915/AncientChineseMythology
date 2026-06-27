using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.Spectres.Items;
using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
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
    /// 怨灵 Spectre 「冤魂记账者 / The Grudge-Keeper」 — 地府 P1 怨念门控 Boss (V2 Wave-2 重做)。
    ///
    /// 设计核心 (docs/BOSS_REDO_V2/04_UNDERWORLD_V2.md §3.1)：怨灵<b>记住</b>你怎么打它，
    /// 再把你的造业原样还给你。三件事把它从"8 阶段实为 4 招的换皮模板"拔成自包含的签名战斗：
    ///   1. 怨念账 Grudge Ledger —— 消费 <see cref="UnderworldField"/> 怨念轴：记录玩家 DPS 与停留象限，
    ///      在终幕镜像回敬 (《怨念清算》)，竞技场鬼火灯笼锚点提供清账/断视线反制。
    ///   2. 脚本化轮替 —— 用确定性循环取代 <c>ChooseNextPhase()</c> 的 <c>rand.Next</c> 轮盘赌：
    ///      Intro → 缠 → 怨链 → 哀嚎(单次 90f 蓄力环) → 附身(必杀 2 分身解锁) → (+1) 重复；
    ///      50% SoulStorm 变"旋转安全扇区"(改规则非加速)。
    ///   3. 冤魂审判 GrudgeReckoning (替换 FinalGrudge 永久喷射)：居中 3s 蓄力 → 一道随怨念扩张的报复波
    ///      + 来自玩家久留象限的幻影突袭，每循环一次后回归脚本轮替，<b>无无限喷射</b>。
    ///   4. 魂蚀 DoT 经 <see cref="UnderworldField.AddSoulErosion"/> 挂在怨链/哀嚎命中上。
    /// 演出走硬化 <see cref="ACMShaders"/>：怨念褪色 PaletteLUT、冥雾 GenericWarp、报复波束 DrawBeam、
    /// 哀嚎泛光 DrawRadialBloomAt、竞技场地纹 ArenaRunic。红色只留给致命源 (§6.1)。
    /// </summary>
    [AutoloadBossHead]
    public class Spectre : ModNPC
    {
        #region Boss 脚本化幕 (确定性轮替, 非随机)

        public enum BossPhase
        {
            Intro,            // 出场凝形
            Haunting,         // 缠 — 单发怨念弹 (教学)
            GrudgeChain,      // 怨链 — 学锁链 tether + 撒灯笼锚点 (挂魂蚀)
            Wailing,          // 哀嚎 — 一次 90f 蓄力大环 (挂魂蚀)
            Possession,       // 附身 — 必杀 2 分身才解锁 (期间无敌)
            SoulStorm,        // 灵魂风暴 — 50% 后变"旋转安全扇区"环
            GrudgeReckoning,  // 冤魂审判 — 25% 后镜像清算 set-piece
            PhaseShift        // 相变定格 (短无敌 beat)
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

        private const int IntroTime = 180;
        private const int WailingWindup = 90;   // §6.1 处决前置, 单次大环
        private const int WailingRecover = 70;
        private const int PossessionGate = 2;   // 必须击杀的分身数
        private const int PossessionTimeout = 720;
        private const int ReckoningCharge = 180; // 3s 蓄力
        private const int ReckoningRecover = 90;
        private const int PhaseShiftTime = 46;

        #endregion

        #region 状态变量

        private int seed = -1;
        private Random random;
        private bool didIntroShock;
        private float introAppear;
        private float pulsePhase;
        private float auraRotation;
        private float hoverOffset;

        private bool isPhase2;  // 50%
        private bool isPhase3;  // 25%

        // 脚本轮替
        private int cycleIndex = -1;
        private int cycleCount;

        // 竞技场锚点 + 怨念记账
        private Vector2 arenaCenter;
        private bool arenaSet;
        private int grudgeDamageAccum;
        private readonly float[] sectorTime = new float[8];
        private float retaliationAngle;

        // 附身门控
        private readonly int[] possMinions = new int[4];
        private bool possSpawned;

        // 冤魂审判
        private bool reckoningReleased;

        // 能量波 (现有 VFX)
        private readonly float[] waveRadius = new float[3];
        private readonly float[] waveAlpha = new float[3];

        // 客户端可视怨念 (网络同步的归一化值, 驱动褪色)
        private float syncedGrudgeNorm;

        #endregion

        #region 目标获取

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

        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
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
            NPC.alpha = 50;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            introAppear = 0;
            cycleIndex = -1;

            // 怨念账上限：约等于"打掉满血所需输出"才满账 → 终幕规模由玩家输出节奏决定。
            UnderworldField.SetGrudgeMax(NPC, 100);

            SpectreHelper.CreateSpectreVortex(NPC.Center, 150f, 1f, 40);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write(introAppear);
            writer.Write(pulsePhase);
            writer.Write(isPhase2);
            writer.Write(isPhase3);
            writer.Write(cycleIndex);
            writer.Write(cycleCount);
            writer.Write(arenaSet);
            writer.Write(arenaCenter.X);
            writer.Write(arenaCenter.Y);
            writer.Write(retaliationAngle);
            writer.Write(reckoningReleased);
            writer.Write(UnderworldField.GetGrudgeNormalized(NPC));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            introAppear = reader.ReadSingle();
            pulsePhase = reader.ReadSingle();
            isPhase2 = reader.ReadBoolean();
            isPhase3 = reader.ReadBoolean();
            cycleIndex = reader.ReadInt32();
            cycleCount = reader.ReadInt32();
            arenaSet = reader.ReadBoolean();
            arenaCenter.X = reader.ReadSingle();
            arenaCenter.Y = reader.ReadSingle();
            retaliationAngle = reader.ReadSingle();
            reckoningReleased = reader.ReadBoolean();
            syncedGrudgeNorm = reader.ReadSingle();
            random ??= new Random(seed);
        }

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

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            random ??= new Random(seed);
            NPC.dontTakeDamage = false; // 默认可被打, 由各 beat 显式上盾

            // 视觉
            pulsePhase += 0.08f;
            auraRotation += 0.02f;
            hoverOffset = MathF.Sin(pulsePhase * 0.5f) * 10f;
            UpdateEnergyWaves();
            CreateAmbientParticles();

            // 怨念结算 (服务器): 把累计造业折算成怨念点
            ConvertGrudge();

            CheckPhaseTransition();

            NPC.TargetClosest();
            Player target = Target;
            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.3f;
                NPC.alpha += 2;
                if (NPC.alpha > 255 || NPC.timeLeft < 10)
                    NPC.active = false;
                return;
            }

            // 记录玩家停留象限 (账记得清楚 — 决定报复来向)
            if (arenaSet && Phase != BossPhase.Intro) {
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
            }

            float lightIntensity = isPhase3 ? 1.2f : (isPhase2 ? 1f : 0.8f);
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
            float lifePercent = (float)NPC.life / NPC.lifeMax;

            if (!isPhase2 && lifePercent <= 0.5f) {
                isPhase2 = true;
                BeginPhaseShift(2);
            }
            if (!isPhase3 && lifePercent <= 0.25f) {
                isPhase3 = true;
                // 进入终幕：相变后第一招强制为冤魂审判 (act 列表 index 0)。
                cycleIndex = -1;
                BeginPhaseShift(3);
            }
        }

        private void BeginPhaseShift(int phase) {
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 120f, 4, 20);
            SpectreHelper.CreateSpectreVortex(NPC.Center, 150f, 1.2f, 50);
            for (int i = 0; i < 3; i++) TriggerEnergyWave();
            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 0.8f);
            ACMUtils.AddScreenShake(10f);

            TransitionTo(BossPhase.PhaseShift);
        }

        private void RunPhaseShift(Player target) {
            // 短无敌定格, 避免被秒过场 (§6.3)
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            Vector2 hover = target.Center + new Vector2(0, -220f + hoverOffset);
            NPC.Center += (hover - NPC.Center) * 0.05f;

            if (PhaseTimer >= PhaseShiftTime)
                AdvanceCycle();
        }

        /// <summary>当前阶段的脚本幕序列 (确定性, 替代 rand 轮盘)。</summary>
        private BossPhase[] BuildActList() {
            if (isPhase3)
                return new[] {
                    BossPhase.GrudgeReckoning, BossPhase.Haunting, BossPhase.GrudgeChain,
                    BossPhase.SoulStorm, BossPhase.Wailing, BossPhase.Possession
                };
            if (isPhase2)
                return new[] {
                    BossPhase.Haunting, BossPhase.GrudgeChain, BossPhase.SoulStorm,
                    BossPhase.Wailing, BossPhase.Possession
                };
            return new[] {
                BossPhase.Haunting, BossPhase.GrudgeChain, BossPhase.Wailing, BossPhase.Possession
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
            possSpawned = false;
            reckoningReleased = false;
            NPC.netUpdate = true;
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

        private void CreateAmbientParticles() {
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 50f + Main.rand.NextFloat(30f);
                Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 1.5f;
                d.alpha = 120;
            }
        }

        #endregion

        #region 脚本幕实现

        private void RunIntro(Player target) {
            introAppear = MathHelper.Clamp(PhaseTimer / 150f, 0, 1);
            introAppear = SpectreHelper.SmoothStep(introAppear);

            Vector2 startPos = target.Center + new Vector2(0, 600);
            Vector2 endPos = target.Center + new Vector2(0, -200);
            Vector2 desired = Vector2.Lerp(startPos, endPos, introAppear);

            NPC.Center += (desired - NPC.Center) * 0.08f;
            NPC.velocity *= 0.9f;

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0)
                SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.5f);

            if (!didIntroShock && introAppear > 0.95f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.4f, Pitch = 0.2f }, NPC.Center);
                ACMUtils.AddScreenShake(14f);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 100f, 3, 16);
                TriggerEnergyWave();
            }

            if (PhaseTimer > IntroTime) {
                // 锁定竞技场锚点 (此后象限记账以它为原点)
                arenaCenter = target.Center;
                arenaSet = true;
                AdvanceCycle();
            }
        }

        // —— 缠 Haunting: 单发怨念弹, 慢盘旋, 教学读弹 ——
        private void RunHaunting(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(
                MathF.Sin(PhaseTimer * 0.03f) * 200f,
                -180f + hoverOffset);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.06f, 0.08f);

            int fireRate = isPhase2 ? 46 : 60;
            if (AttackTimer % fireRate == 0 && PhaseTimer > 24)
                ShootWraithBolt(target);

            if (PhaseTimer > (isPhase2 ? 280 : 340))
                AdvanceCycle();
        }

        // —— 怨链 GrudgeChain: 学锁链 tether + 撒灯笼锚点 + 挂魂蚀 ——
        private void RunGrudgeChain(Player target) {
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 3.5f, 0.04f);

            // 一次性撒灯笼锚点 (教玩家断视线/清账)
            if (PhaseTimer == 30)
                SpawnLanternAnchors(3);

            if (AttackTimer % (isPhase2 ? 70 : 90) == 0 && PhaseTimer > 40) {
                int chainCount = 1 + cycleCount; // +1 pattern: 每循环多一条
                chainCount = Math.Min(chainCount, isPhase2 ? 4 : 3);
                for (int i = 0; i < chainCount; i++) {
                    float angle = MathHelper.TwoPi * i / chainCount + PhaseTimer * 0.01f;
                    Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 50f;
                    ShootSoulChain(NPC.Center + offset, target);
                }
                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.3f }, NPC.Center);
            }

            if (PhaseTimer > (isPhase2 ? 280 : 340))
                AdvanceCycle();
        }

        // —— 哀嚎 Wailing: 一次 90f 蓄力的大环 (处决级预告), 挂魂蚀 ——
        private void RunWailing(Player target) {
            Vector2 hoverPos = arenaSet ? arenaCenter + new Vector2(0, -120f) : target.Center + new Vector2(0, -200f);
            NPC.Center += (hoverPos - NPC.Center) * 0.04f;
            NPC.velocity *= 0.85f;

            if (PhaseTimer < WailingWindup) {
                // 蓄力：内收粒子 + 渐强震屏 (§6.3 配方)
                if (Main.rand.NextBool(2)) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 220f * (1f - PhaseTimer / (float)WailingWindup);
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 8f;
                }
                ACMUtils.AddScreenShake(MathHelper.Lerp(0.5f, 6f, PhaseTimer / (float)WailingWindup));
            }

            if (PhaseTimer == WailingWindup) {
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.5f, Volume = 1.5f }, NPC.Center);
                int waveCount = isPhase2 ? 24 : 18;
                for (int i = 0; i < waveCount; i++)
                    ShootWailingWave(MathHelper.TwoPi * i / waveCount);
                for (int i = 0; i < 3; i++) TriggerEnergyWave();
                ACMUtils.AddScreenShake(12f);
                SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 1f);
            }

            if (PhaseTimer > WailingWindup + WailingRecover)
                AdvanceCycle();
        }

        // —— 附身 Possession: 召唤分身, 必杀 PossessionGate 个才解锁 (期间无敌) ——
        private void RunPossession(Player target) {
            Vector2 hoverPos = arenaSet ? arenaCenter + new Vector2(0, -180f) : target.Center + new Vector2(0, -250f);
            NPC.Center += (hoverPos - NPC.Center) * 0.04f;
            NPC.velocity *= 0.9f;

            if (!possSpawned && PhaseTimer == 40) {
                possSpawned = true;
                for (int i = 0; i < possMinions.Length; i++) possMinions[i] = -1;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int minionCount = isPhase2 ? 4 : 3;
                    for (int i = 0; i < minionCount && i < possMinions.Length; i++) {
                        float angle = MathHelper.TwoPi * i / minionCount;
                        Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 90f;
                        int minion = NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y,
                            ModContent.NPCType<SpectreMinion>(), 0, NPC.whoAmI);
                        possMinions[i] = minion;
                    }
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f }, NPC.Center);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 100f, 3, 16);
            }

            // 门控：分身存活时 Boss 无敌 (telegraph 护盾光环, PreDraw 绘制)
            int aliveMinions = CountAliveMinions(out int killed);
            bool gateOpen = !possSpawned || killed >= PossessionGate || aliveMinions == 0 || PhaseTimer > PossessionTimeout;
            NPC.dontTakeDamage = possSpawned && !gateOpen;

            // 偶尔施压 (有预告), 但不喷射
            if (AttackTimer % 80 == 0 && PhaseTimer > 80 && !gateOpen)
                ShootWraithBolt(target);

            if (gateOpen && PhaseTimer > 60) {
                // 解锁：净化残余分身, 给正反馈
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    KillOwnedMinions();
                AdvanceCycle();
            }
        }

        // —— 灵魂风暴 SoulStorm (50% 起): 旋转安全扇区环 (改规则非加速) ——
        private void RunSoulStorm(Player target) {
            Vector2 hoverPos = arenaSet ? arenaCenter + new Vector2(0, -60f) : target.Center + new Vector2(0, -160f);
            NPC.Center += (hoverPos - NPC.Center) * 0.05f;
            NPC.velocity *= 0.92f;

            float safeAngle = PhaseTimer * 0.018f; // 缓慢旋转的安全缝
            const float gapHalf = 0.42f;           // 安全扇区半角 (~24°)

            if (AttackTimer % 30 == 0 && PhaseTimer > 50)
                ShootSoulStormRing(safeAngle, gapHalf);

            // 安全缝绿色预告粒子 (非红, 表示可站)
            if (!Main.dedServ && PhaseTimer % 3 == 0) {
                Vector2 dir = safeAngle.ToRotationVector2();
                Vector2 pos = NPC.Center + dir * 260f;
                var d = Dust.NewDustPerfect(pos, DustID.GreenTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = dir * 2f;
            }

            if (PhaseTimer > 300)
                AdvanceCycle();
        }

        // —— 冤魂审判 GrudgeReckoning (25% 起): 镜像清算 set-piece ——
        private void RunGrudgeReckoning(Player target) {
            // 居中蓄力, 期间无敌 (单次 set-piece, 不可被秒断)
            Vector2 center = arenaSet ? arenaCenter : target.Center;
            NPC.Center += (center + new Vector2(0, -40f) - NPC.Center) * 0.06f;
            NPC.velocity *= 0.85f;

            if (!reckoningReleased) {
                NPC.dontTakeDamage = true;

                // 蓄力开始：选定报复来向 (玩家久留象限) + 撒灯笼锚点供清账反制
                if (PhaseTimer == 1) {
                    retaliationAngle = PickRetaliationAngle();
                    SpawnLanternAnchors(4);
                    SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
                    NPC.netUpdate = true;
                }

                float t = PhaseTimer / (float)ReckoningCharge;
                // 渐强震屏 + 内收账本残影
                ACMUtils.AddScreenShake(MathHelper.Lerp(1f, 8f, t));
                if (!Main.dedServ && PhaseTimer % 2 == 0) {
                    float dist = 320f * (1f - t);
                    Vector2 pos = NPC.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * dist;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 9f;
                }

                if (PhaseTimer >= ReckoningCharge) {
                    reckoningReleased = true;
                    PhaseTimer = 0;
                    ReleaseReckoning(target);
                }
            }
            else {
                // 释放后短余波 → 回归脚本轮替 (无无限喷射)
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
            for (int i = 0; i < 4; i++) TriggerEnergyWave();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 center = arenaSet ? arenaCenter : target.Center;

            // ONE 扩张报复波 (规模随怨念)
            Projectile.NewProjectile(NPC.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<SpectreReckoningWave>(), GetBossDamage(1.1f), 4f,
                ai0: g);

            // 幻影突袭：来自玩家久留象限, 朝玩家俯冲
            Vector2 from = center + retaliationAngle.ToRotationVector2() * 700f;
            Vector2 aim = (target.Center - from).SafeNormalize(Vector2.UnitX);
            int rushCount = 1 + (g > 0.6f ? 1 : 0);
            for (int i = 0; i < rushCount; i++) {
                Vector2 spawn = from + aim.RotatedBy((i - (rushCount - 1) / 2f) * 0.25f) * -40f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, aim * 6f,
                    ModContent.ProjectileType<SpectrePhantomRush>(), GetBossDamage(1f), 3f,
                    ai0: target.whoAmI);
            }

            // 怨念部分释放 (终幕代价已偿一部分)
            UnderworldField.ReduceGrudge(NPC, 35);
            NPC.netUpdate = true;
        }

        #endregion

        #region 灯笼锚点 / 分身门控

        private void SpawnLanternAnchors(int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !arenaSet)
                return;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + 0.3f;
                Vector2 pos = arenaCenter + angle.ToRotationVector2() * new Vector2(360f, 240f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<SpectreLanternAnchor>(), 0, 0f, Main.myPlayer, NPC.whoAmI);
            }
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

        private void ShootWraithBolt(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.8f);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = isPhase2 ? 2 : 1; // 缠为单发学习 (P2 双)
            float spread = 0.1f;
            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 dir = toPlayer.RotatedBy(angle);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 40f,
                    dir * (10f + Main.rand.NextFloat(-0.5f, 0.5f)),
                    ModContent.ProjectileType<SpectreWraithBolt>(), damage, 2f);
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, NPC.Center);
        }

        private void ShootSoulChain(Vector2 from, Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.85f);
            Vector2 toPlayer = (target.Center - from).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, toPlayer * 11f,
                ModContent.ProjectileType<SpectreSoulChain>(), damage, 3f,
                ai0: target.Center.X, ai1: target.Center.Y);
        }

        private void ShootSoulStormRing(float safeAngle, float gapHalf) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.7f);
            int count = isPhase2 ? 16 : 12;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                // 跳过旋转安全扇区
                float diff = MathHelper.WrapAngle(angle - safeAngle);
                if (MathF.Abs(diff) < gapHalf) continue;
                Vector2 dir = angle.ToRotationVector2();
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 8f,
                    ModContent.ProjectileType<SpectreSoulOrb>(), damage, 1f, ai0: i % 2);
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, NPC.Center);
        }

        private void ShootWailingWave(float angle) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int damage = GetBossDamage(0.85f);
            Vector2 dir = angle.ToRotationVector2();
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 6f,
                ModContent.ProjectileType<SpectreWailingWave>(), damage, 2f);
        }

        public int GetBossDamage(float scaling = 1f) => (int)(NPC.damage * scaling);

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 竞技场地纹 / 报复来向 / 安全扇区预告 (屏幕空间 decal, 内部自管批)
            DrawArenaTells(spriteBatch);

            DrawEnergyWaves(spriteBatch);
            DrawAura(spriteBatch);
            DrawTrail(spriteBatch);
            DrawMainBody(spriteBatch, screenPos, drawColor);

            // 附身门控护盾光环 (无敌时的可读提示)
            if (Phase == BossPhase.Possession && NPC.dontTakeDamage)
                DrawGateShield(spriteBatch);

            if (isPhase2)
                SpectreHelper.DrawSoulOrbit(spriteBatch, NPC.Center, 70f, isPhase3 ? 5 : 3,
                    pulsePhase * 0.8f, pulsePhase);

            return false;
        }

        private void DrawArenaTells(SpriteBatch sb) {
            if (Main.dedServ || !arenaSet) return;

            // 哀嚎蓄力 / 审判蓄力 / 风暴：画竞技场环 (ArenaRunic)
            bool wailCharge = Phase == BossPhase.Wailing && PhaseTimer < WailingWindup;
            bool reckonCharge = Phase == BossPhase.GrudgeReckoning && !reckoningReleased;
            bool storm = Phase == BossPhase.SoulStorm;

            if (wailCharge || reckonCharge || storm) {
                float intensity = storm ? 0.5f : (reckonCharge ? MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0.2f, 1f)
                                                              : MathHelper.Clamp(PhaseTimer / (float)WailingWindup, 0.2f, 0.9f));
                Color prim = reckonCharge ? TelegraphColors.NetherViolet : TelegraphColors.GhostGreen;
                Color sec = SpectreHelper.SpectreYellow;
                float radius = reckonCharge ? 520f : 300f;
                DrawArenaRing(sb, NPC.Center, radius, intensity, prim, sec);
            }

            // 审判：报复来向光束 (蓄力青白 → 末段转红, 红=致命源 §6.1)。DrawBeam 不占全屏名额。
            if (reckonCharge) {
                Vector2 dir = retaliationAngle.ToRotationVector2();
                Vector2 outer = arenaCenter + dir * 700f;
                float prog = MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0f, 1f);
                bool imminent = prog > 0.8f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                Color edge = imminent ? TelegraphColors.Execution : SpectreHelper.SpectreCyan;
                ACMShaders.DrawBeam(outer, arenaCenter, MathHelper.Lerp(8f, 26f, prog), core, edge, 0.4f + 0.6f * prog);
            }

            // 泛光只在"释放瞬间"放 (单帧), 避免与蓄力期的全屏冥雾 (PostDraw) 抢同一全屏名额。
            if (Phase == BossPhase.Wailing && PhaseTimer == WailingWindup)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, 0.8f, SpectreHelper.SpectreCyan);
            else if (Phase == BossPhase.GrudgeReckoning && reckoningReleased && PhaseTimer < 5)
                ACMShaders.DrawRadialBloomAt(arenaCenter, 0.3f, 1f, SpectreHelper.SpectreRage);
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

        private void DrawGateShield(SpriteBatch sb) {
            var tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;
            Color shield = TelegraphColors.NetherViolet;
            shield.A = 0;
            for (int i = 0; i < 6; i++) {
                float a = pulsePhase + i * MathHelper.TwoPi / 6f;
                Vector2 pos = NPC.Center + a.ToRotationVector2() * (70f + MathF.Sin(pulsePhase * 2f) * 6f);
                sb.Draw(tex, pos - Main.screenPosition, null, shield * 0.3f, a, origin, NPC.scale * 0.4f, SpriteEffects.None, 0);
            }
        }

        private void DrawEnergyWaves(SpriteBatch sb) {
            for (int i = 0; i < waveRadius.Length; i++) {
                if (waveAlpha[i] > 0.05f) {
                    Color waveColor = isPhase3
                        ? SpectreHelper.SpectreRage
                        : (i % 2 == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow);
                    SpectreHelper.DrawEnergyWave(sb, NPC.Center, waveRadius[i], 15f, waveColor, waveAlpha[i] * 0.5f);
                }
            }
        }

        private void DrawAura(SpriteBatch sb) {
            float auraRadius = 60f + MathF.Sin(pulsePhase) * 8f;
            SpectreHelper.DrawGrudgeAura(sb, NPC.Center, auraRadius, 10, auraRotation, pulsePhase);
        }

        private void DrawTrail(SpriteBatch sb) {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                float progress = 1f - i / (float)NPC.oldPos.Length;
                float fade = progress * 0.4f;
                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreCyan, progress);
                if (isPhase3) trailColor = Color.Lerp(trailColor, SpectreHelper.SpectreRage, 0.3f);
                trailColor *= fade;
                trailColor.A = 0;
                float trailScale = NPC.scale * (0.6f + progress * 0.4f);
                sb.Draw(tex, pos, null, trailColor, NPC.rotation, origin, trailScale, SpriteEffects.None, 0);
            }
        }

        private void DrawMainBody(SpriteBatch sb, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects spriteEffects = NPC.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float scale = NPC.scale;
            if (Phase == BossPhase.Intro)
                scale *= MathHelper.Lerp(0.6f, 1f, introAppear);
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.08f;
            scale *= pulse;

            // 怨念越高, 主体越褪色泛青黄 (呼应怨念账)
            float g = GrudgeNorm();
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f + g * 0.3f);
            if (isPhase3) {
                float rage = MathF.Sin(pulsePhase * 3f) * 0.3f + 0.5f;
                mainColor = Color.Lerp(mainColor, SpectreHelper.SpectreRage, rage * 0.4f);
            }

            Color glowColor = mainColor;
            glowColor.A = 0;
            for (int i = 3; i >= 0; i--) {
                float glowScale = scale * (1.3f + i * 0.12f);
                sb.Draw(tex, NPC.Center - screenPos, null, glowColor * (0.12f / (i + 1)),
                    NPC.rotation, origin, glowScale, spriteEffects, 0);
            }
            sb.Draw(tex, NPC.Center - screenPos, null, mainColor, NPC.rotation, origin, scale, spriteEffects, 0);

            Color highlight = Color.White;
            highlight.A = 0;
            sb.Draw(tex, NPC.Center - screenPos, null, highlight * 0.25f, NPC.rotation, origin, scale * 0.8f, spriteEffects, 0);
        }

        /// <summary>
        /// 全屏怨念褪色 (PaletteLUT) / 审判冥雾 (GenericWarp)。
        /// 性能契约：每帧 ≤ 1 个全屏后处理 (RequestFullscreenSlot 仲裁); 强度 &lt; 0.05 直接 return。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            bool charging = Phase == BossPhase.GrudgeReckoning && !reckoningReleased;
            float fog = charging ? MathHelper.Clamp(PhaseTimer / (float)ReckoningCharge, 0f, 1f) : 0f;
            float g = GrudgeNorm();
            if (g < 0.05f && fog < 0.05f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Vector2 center = arenaSet ? arenaCenter : NPC.Center;
            float aspect = (float)Main.screenWidth / Main.screenHeight;

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
            else {
                Effect fx = ACMShaders.PaletteLUT;
                if (fx == null) return;
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(g, 0f, 1f));
                fx.Parameters["uAspect"]?.SetValue(aspect);
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
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            SpectreHelper.CreateSpectreVortex(NPC.Center, 200f, 1.5f, 80);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 180f, 5, 25);
            for (int i = 0; i < 5; i++) TriggerEnergyWave();
            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreYellow, 1.2f);
            ACMUtils.AddScreenShake(14f);

            // 清除残余分身
            if (Main.netMode != NetmodeID.MultiplayerClient)
                KillOwnedMinions();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                DownedBossSystem.downedSpectre = true;
        }
    }
}

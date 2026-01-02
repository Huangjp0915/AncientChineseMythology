using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨灵 - 冥府中期Boss
    /// 由无数冤魂凝聚而成的怨念实体，青黄色调的幽魂形态
    /// </summary>
    [AutoloadBossHead]
    public class Spectre : ModNPC
    {
        #region Boss阶段系统

        public enum BossPhase
        {
            Intro,              // 出场
            Haunting,           // 缠魂（基础攻击）
            SoulStorm,          // 灵魂风暴
            GrudgeChain,        // 怨念锁链
            PhantomRush,        // 幻影突袭
            Possession,         // 附身（召唤小怨灵）
            Wailing,            // 哀嚎（大范围攻击）
            FinalGrudge         // 终极怨念（狂暴阶段）
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SpecialTimer => ref NPC.ai[3];

        #endregion

        #region Boss状态变量

        private int seed = -1;
        private Random random;
        private bool didIntroShock = false;
        private float introAppear = 0f;
        private float pulsePhase = 0f;
        private float auraRotation = 0f;
        private float hoverOffset = 0f;

        // 阶段控制
        private bool isPhase2 = false; // 50%血量以下
        private bool isPhase3 = false; // 25%血量以下

        // 冲刺参数
        private Vector2 dashTarget;
        private int dashCount = 0;
        private const int MaxDashes = 3;

        // 能量波
        private float[] waveRadius = new float[3];
        private float[] waveAlpha = new float[3];

        // 怨灵分身
        private int[] spectreMinions = new int[4];

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
            NPC.lifeMax = 120000; // 冥府中期Boss
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = 150000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.alpha = 50; // 半透明
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            introAppear = 0;

            // 出场特效
            SpectreHelper.CreateSpectreVortex(NPC.Center, 150f, 1f, 40);

            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(introAppear);
            writer.Write(pulsePhase);
            writer.Write(isPhase2);
            writer.Write(isPhase3);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            introAppear = reader.ReadSingle();
            pulsePhase = reader.ReadSingle();
            isPhase2 = reader.ReadBoolean();
            isPhase3 = reader.ReadBoolean();
            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.85f);
        }

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            random ??= new Random(seed);

            // 更新视觉效果
            pulsePhase += 0.08f;
            auraRotation += 0.02f;
            hoverOffset = MathF.Sin(pulsePhase * 0.5f) * 10f;
            UpdateEnergyWaves();

            // 持续粒子效果
            CreateAmbientParticles();

            // 检查阶段转换
            CheckPhaseTransition();

            // 目标验证
            NPC.TargetClosest();
            Player target = Target;
            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.3f;
                NPC.alpha += 2;
                if (NPC.alpha > 255 || NPC.timeLeft < 10) {
                    NPC.active = false;
                }
                return;
            }

            PhaseTimer++;
            AttackTimer++;

            // 执行AI阶段
            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Haunting:
                    RunHaunting(target);
                    break;
                case BossPhase.SoulStorm:
                    RunSoulStorm(target);
                    break;
                case BossPhase.GrudgeChain:
                    RunGrudgeChain(target);
                    break;
                case BossPhase.PhantomRush:
                    RunPhantomRush(target);
                    break;
                case BossPhase.Possession:
                    RunPossession(target);
                    break;
                case BossPhase.Wailing:
                    RunWailing(target);
                    break;
                case BossPhase.FinalGrudge:
                    RunFinalGrudge(target);
                    break;
            }

            // 发光
            float lightIntensity = isPhase3 ? 1.2f : (isPhase2 ? 1f : 0.8f);
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreCyan.ToVector3() * lightIntensity * 0.5f);
            Lighting.AddLight(NPC.Center, SpectreHelper.SpectreYellow.ToVector3() * lightIntensity * 0.3f);
        }

        #region 阶段转换

        private void CheckPhaseTransition() {
            float lifePercent = (float)NPC.life / NPC.lifeMax;

            if (!isPhase2 && lifePercent <= 0.5f) {
                isPhase2 = true;
                OnPhaseTransition(2);
            }

            if (!isPhase3 && lifePercent <= 0.25f) {
                isPhase3 = true;
                OnPhaseTransition(3);
                TransitionTo(BossPhase.FinalGrudge);
            }
        }

        private void OnPhaseTransition(int phase) {
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);

            // 阶段转换特效
            SpectreHelper.CreateSpectreBurst(NPC.Center, 120f, 4, 20);
            SpectreHelper.CreateSpectreVortex(NPC.Center, 150f, 1.2f, 50);

            // 触发能量波
            for (int i = 0; i < 3; i++) {
                TriggerEnergyWave();
            }

            // 屏幕闪烁
            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 0.8f);

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(12, 40);
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            dashCount = 0;
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
            // 环绕粒子
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

            // 狂暴阶段额外粒子
            if (isPhase3 && Main.rand.NextBool(2)) {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(60f, 60f);
                var d = Dust.NewDustPerfect(pos, DustID.Torch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        #endregion

        #region AI阶段实现

        private void RunIntro(Player target) {
            introAppear = MathHelper.Clamp(PhaseTimer / 150f, 0, 1);
            introAppear = SpectreHelper.SmoothStep(introAppear);

            Vector2 startPos = target.Center + new Vector2(0, 600);
            Vector2 endPos = target.Center + new Vector2(0, -200);
            Vector2 desired = Vector2.Lerp(startPos, endPos, introAppear);

            NPC.Center += (desired - NPC.Center) * 0.08f;
            NPC.velocity *= 0.9f;

            // 出场粒子
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.5f);
            }

            // 出场冲击
            if (!didIntroShock && introAppear > 0.95f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.4f, Pitch = 0.2f }, NPC.Center);

                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(15, 50);

                SpectreHelper.CreateSpectreBurst(NPC.Center, 100f, 3, 16);
                TriggerEnergyWave();
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Haunting);
            }
        }

        private void RunHaunting(Player target) {
            // 基础缠绕移动
            Vector2 hoverPos = target.Center + new Vector2(
                MathF.Sin(PhaseTimer * 0.03f) * 200f,
                -180f + hoverOffset
            );

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.06f, 0.08f);

            // 发射怨念弹
            int fireRate = isPhase2 ? 50 : 70;
            if (AttackTimer % fireRate == 0) {
                ShootWraithBolt(target);
            }

            // 灵魂链接攻击
            if (AttackTimer % 120 == 60) {
                ShootSoulChain(target);
            }

            // 选择下一阶段
            if (PhaseTimer > (isPhase2 ? 300 : 400)) {
                ChooseNextPhase();
            }
        }

        private void RunSoulStorm(Player target) {
            // 在玩家上方盘旋
            Vector2 hoverPos = target.Center + new Vector2(0, -300f + hoverOffset);
            NPC.Center += (hoverPos - NPC.Center) * 0.05f;
            NPC.velocity *= 0.92f;

            // 环形弹幕
            int fireRate = isPhase2 ? 25 : 35;
            if (AttackTimer % fireRate == 0 && PhaseTimer > 60) {
                ShootSoulStorm(target);
            }

            // 特效
            if (Main.rand.NextBool(2)) {
                SpectreHelper.CreateSpectreTrail(NPC.Center, Vector2.Zero, 0.8f);
            }

            if (PhaseTimer > (isPhase2 ? 250 : 320)) {
                ChooseNextPhase();
            }
        }

        private void RunGrudgeChain(Player target) {
            // 缓慢接近玩家
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 4f, 0.04f);

            // 多重链条攻击
            if (AttackTimer % 80 == 0 && PhaseTimer > 40) {
                int chainCount = isPhase2 ? 4 : 3;
                for (int i = 0; i < chainCount; i++) {
                    float angle = MathHelper.TwoPi * i / chainCount + PhaseTimer * 0.01f;
                    Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 50f;
                    ShootGrudgeChain(NPC.Center + offset, target);
                }

                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.3f }, NPC.Center);
            }

            if (PhaseTimer > (isPhase2 ? 280 : 350)) {
                ChooseNextPhase();
            }
        }

        private void RunPhantomRush(Player target) {
            if (dashCount < MaxDashes) {
                if (PhaseTimer % 60 == 30) {
                    // 准备冲刺
                    dashTarget = target.Center + target.velocity * 15f;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f }, NPC.Center);

                    // 蓄力特效
                    SpectreHelper.CreateSpectreVortex(NPC.Center, 80f, 0.6f, 20);
                }

                if (PhaseTimer % 60 == 0) {
                    // 执行冲刺
                    Vector2 direction = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                    NPC.velocity = direction * (isPhase2 ? 28f : 22f);
                    dashCount++;

                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.1f }, NPC.Center);
                    TriggerEnergyWave();
                }

                // 冲刺拖尾
                if (NPC.velocity.Length() > 15f) {
                    SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.5f);
                }
            }
            else {
                NPC.velocity *= 0.92f;
            }

            if (PhaseTimer > 200) {
                dashCount = 0;
                ChooseNextPhase();
            }
        }

        private void RunPossession(Player target) {
            // 悬停
            Vector2 hoverPos = target.Center + new Vector2(0, -250f + hoverOffset);
            NPC.Center += (hoverPos - NPC.Center) * 0.04f;
            NPC.velocity *= 0.9f;

            // 召唤小怨灵
            if (PhaseTimer == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                int minionCount = isPhase2 ? 4 : 3;
                for (int i = 0; i < minionCount; i++) {
                    float angle = MathHelper.TwoPi * i / minionCount;
                    Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 80f;

                    int minion = NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y,
                        ModContent.NPCType<SpectreMinion>(), 0, NPC.whoAmI);
                    if (minion < Main.maxNPCs && i < spectreMinions.Length) {
                        spectreMinions[i] = minion;
                    }
                }

                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f }, NPC.Center);
                SpectreHelper.CreateSpectreBurst(NPC.Center, 100f, 3, 16);
            }

            // 继续发射弹幕
            if (AttackTimer % 60 == 0 && PhaseTimer > 80) {
                ShootWraithBolt(target);
            }

            if (PhaseTimer > (isPhase2 ? 350 : 450)) {
                ChooseNextPhase();
            }
        }

        private void RunWailing(Player target) {
            // 固定在中央
            Vector2 hoverPos = target.Center + new Vector2(0, -200f);
            NPC.Center += (hoverPos - NPC.Center) * 0.03f;
            NPC.velocity *= 0.85f;

            // 哀嚎蓄力
            if (PhaseTimer < 90) {
                // 蓄力粒子
                if (Main.rand.NextBool(2)) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 200f * (1f - PhaseTimer / 90f);
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 8f;
                }
            }

            // 释放哀嚎波
            if (PhaseTimer == 90) {
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.5f, Volume = 1.5f }, NPC.Center);

                // 大范围弹幕
                int waveCount = isPhase2 ? 24 : 18;
                for (int i = 0; i < waveCount; i++) {
                    float angle = MathHelper.TwoPi * i / waveCount;
                    ShootWailingWave(angle);
                }

                // 多重能量波
                for (int i = 0; i < 3; i++) {
                    TriggerEnergyWave();
                }

                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(20, 60);
                SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreCyan, 1f);
            }

            if (PhaseTimer > 180) {
                ChooseNextPhase();
            }
        }

        private void RunFinalGrudge(Player target) {
            // 狂暴追击
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float targetSpeed = 8f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer * targetSpeed, 0.12f);

            // 持续拖尾
            SpectreHelper.CreateSpectreTrail(NPC.Center, NPC.velocity, 1.8f);

            // 高频攻击
            if (AttackTimer % 25 == 0) {
                ShootWraithBolt(target);
            }

            if (AttackTimer % 50 == 25) {
                ShootSoulChain(target);
            }

            // 周期性环形弹幕
            if (AttackTimer % 80 == 0) {
                ShootSoulStorm(target);
            }

            // 周期性能量波
            if (AttackTimer % 60 == 0) {
                TriggerEnergyWave();
            }

            // 狂暴阶段不切换
            if (PhaseTimer > 400) {
                PhaseTimer = 0;
            }
        }

        private void ChooseNextPhase() {
            int choice = Main.rand.Next(isPhase2 ? 6 : 5);

            switch (choice) {
                case 0:
                    TransitionTo(BossPhase.Haunting);
                    break;
                case 1:
                    TransitionTo(BossPhase.SoulStorm);
                    break;
                case 2:
                    TransitionTo(BossPhase.GrudgeChain);
                    break;
                case 3:
                    TransitionTo(BossPhase.PhantomRush);
                    break;
                case 4:
                    TransitionTo(BossPhase.Possession);
                    break;
                case 5:
                    TransitionTo(BossPhase.Wailing);
                    break;
            }

            if (isPhase3) {
                TransitionTo(BossPhase.FinalGrudge);
            }
        }

        #endregion

        #region 攻击方法

        private void ShootWraithBolt(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = GetBossDamage(0.8f);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            int count = isPhase2 ? 3 : 2;
            float spread = isPhase2 ? 0.12f : 0.08f;

            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 direction = toPlayer.RotatedBy(angle);
                float speed = 10f + Main.rand.NextFloat(-1f, 1f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 40f,
                    direction * speed,
                    ModContent.ProjectileType<SpectreWraithBolt>(),
                    damage, 2f
                );
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, NPC.Center);
        }

        private void ShootSoulChain(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = GetBossDamage(0.9f);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                toPlayer * 12f,
                ModContent.ProjectileType<SpectreSoulChain>(),
                damage, 3f,
                ai0: target.Center.X,
                ai1: target.Center.Y
            );

            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f }, NPC.Center);
        }

        private void ShootGrudgeChain(Vector2 from, Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = GetBossDamage(0.7f);
            Vector2 toPlayer = (target.Center - from).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                from,
                toPlayer * 10f,
                ModContent.ProjectileType<SpectreSoulChain>(),
                damage, 2f,
                ai0: target.Center.X,
                ai1: target.Center.Y
            );
        }

        private void ShootSoulStorm(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = GetBossDamage(0.75f);
            int count = isPhase2 ? 14 : 10;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + AttackTimer * 0.02f;
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                float speed = 8f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    direction * speed,
                    ModContent.ProjectileType<SpectreSoulOrb>(),
                    damage, 1f,
                    ai0: i % 2 // 颜色索引
                );
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, NPC.Center);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 60f, 2, 10);
        }

        private void ShootWailingWave(float angle) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = GetBossDamage(0.85f);
            Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                direction * 6f,
                ModContent.ProjectileType<SpectreWailingWave>(),
                damage, 2f
            );
        }

        public int GetBossDamage(float scaling = 1f) {
            return (int)(NPC.damage * scaling);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 绘制能量波
            DrawEnergyWaves(spriteBatch);

            // 绘制光环
            DrawAura(spriteBatch);

            // 绘制拖尾
            DrawTrail(spriteBatch);

            // 绘制主体
            DrawMainBody(spriteBatch, screenPos, drawColor);

            // 绘制环绕灵魂
            if (isPhase2) {
                SpectreHelper.DrawSoulOrbit(spriteBatch, NPC.Center, 70f, isPhase3 ? 5 : 3,
                    pulsePhase * 0.8f, pulsePhase);
            }

            return false;
        }

        private void DrawEnergyWaves(SpriteBatch sb) {
            for (int i = 0; i < waveRadius.Length; i++) {
                if (waveAlpha[i] > 0.05f) {
                    Color waveColor = isPhase3
                        ? SpectreHelper.SpectreRage
                        : (i % 2 == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow);
                    SpectreHelper.DrawEnergyWave(sb, NPC.Center, waveRadius[i], 15f,
                        waveColor, waveAlpha[i] * 0.5f);
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

                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan,
                    SpectreHelper.SpectreCyan, progress);
                if (isPhase3) {
                    trailColor = Color.Lerp(trailColor, SpectreHelper.SpectreRage, 0.3f);
                }
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
            if (Phase == BossPhase.Intro) {
                scale *= MathHelper.Lerp(0.6f, 1f, introAppear);
            }

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.08f;
            scale *= pulse;

            // 颜色
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f);
            if (isPhase3) {
                float rage = MathF.Sin(pulsePhase * 3f) * 0.3f + 0.5f;
                mainColor = Color.Lerp(mainColor, SpectreHelper.SpectreRage, rage * 0.4f);
            }

            // 外层光晕
            Color glowColor = mainColor;
            glowColor.A = 0;
            for (int i = 3; i >= 0; i--) {
                float glowScale = scale * (1.3f + i * 0.12f);
                sb.Draw(tex, NPC.Center - screenPos, null, glowColor * (0.12f / (i + 1)),
                    NPC.rotation, origin, glowScale, spriteEffects, 0);
            }

            // 主体
            sb.Draw(tex, NPC.Center - screenPos, null, mainColor, NPC.rotation, origin, scale, spriteEffects, 0);

            // 高光
            Color highlight = Color.White;
            highlight.A = 0;
            sb.Draw(tex, NPC.Center - screenPos, null, highlight * 0.25f, NPC.rotation, origin, scale * 0.8f, spriteEffects, 0);
        }

        #endregion

        public override bool CheckActive() => false;

        public override void OnKill() {
            // 死亡特效
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);

            SpectreHelper.CreateSpectreVortex(NPC.Center, 200f, 1.5f, 80);
            SpectreHelper.CreateSpectreBurst(NPC.Center, 180f, 5, 25);

            for (int i = 0; i < 5; i++) {
                TriggerEnergyWave();
            }

            SpectreHelper.CreateScreenFlash(NPC.Center, SpectreHelper.SpectreYellow, 1.2f);

            // 清除小怨灵
            foreach (int idx in spectreMinions) {
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                    Main.npc[idx].life = 0;
                    Main.npc[idx].active = false;
                }
            }
        }
    }
}

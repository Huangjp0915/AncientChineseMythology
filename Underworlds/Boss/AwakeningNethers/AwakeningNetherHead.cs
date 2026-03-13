using AncientChineseMythology.Underworlds.Boss.AwakeningNethers.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 头部
    /// 终局Boss，具备强大的AI和极致的视觉效果
    /// </summary>
    [AutoloadBossHead]
    public class AwakeningNetherHead : AwakeningNether
    {
        public override WormType NPCWormType => WormType.Head;
        // AI状态枚举
        private enum AIState
        {
            Circling,           // 环绕玩家
            DashPrepare,        // 冲刺准备
            Dash,               // 冲刺攻击
            VoidBreath,         // 虚空吐息
            SoulStorm,          // 灵魂风暴
            DimensionRift,      // 次元裂隙
            VoidDevour,         // 虚空吞噬（新增）
            DesperateFury       // 狂暴阶段（低血量时）
        }

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // 状态计时器
        private int stateTimer = 0;
        private int attackTimer = 0;
        private int dashCount = 0;
        private const int MaxDashes = 4; // 增加冲刺次数

        // 冲刺参数
        private Vector2 dashTarget;
        private float dashSpeed = 40f; // 提升冲刺速度

        // 阶段控制
        private bool isPhase2 = false; // 50%血量以下
        private bool isPhase3 = false; // 25%血量以下

        // 视觉效果参数
        private float pulsePhase = 0f;
        private float auraIntensity = 0f;
        private float[] energyWaveRadius = new float[3];
        private float[] energyWaveAlpha = new float[3];

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AwakeningNetherBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 100;
            NPC.height = 100;
            NPC.lifeMax = 11200000;
            NPC.damage = 200;
            NPC.defense = 90;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AbyssalSpine>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<PhantomBreath>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulErosionScepter>(), 2));
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);

            // Boss出场音效
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

            // 出场特效 - 虚空漩涡
            AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 200f, 1.5f, 60);
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 150f, 4, 24);

            // 触发能量波
            TriggerEnergyWave();
        }

        /// <summary>
        /// 触发能量冲击波
        /// </summary>
        private void TriggerEnergyWave() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] <= 0.1f) {
                    energyWaveRadius[i] = 0f;
                    energyWaveAlpha[i] = 1f;
                    break;
                }
            }
        }

        /// <summary>
        /// 更新能量波
        /// </summary>
        private void UpdateEnergyWaves() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0f) {
                    energyWaveRadius[i] += 15f;
                    energyWaveAlpha[i] -= 0.02f;
                    if (energyWaveAlpha[i] < 0f) energyWaveAlpha[i] = 0f;
                }
            }
        }

        public override void AI() {
            base.AI();
            UnderworldPlayer.UnderworldEffect = true;
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 更新视觉效果
            pulsePhase += 0.08f;
            auraIntensity = MathHelper.Lerp(auraIntensity, isPhase3 ? 1.5f : (isPhase2 ? 1.2f : 1f), 0.02f);
            UpdateEnergyWaves();

            // 持续的能量光环粒子
            CreateAuraParticles();

            // 检查阶段转换
            CheckPhaseTransition();

            // 初始化
            if (NPC.localAI[0] == 0f) {
                CurrentState = AIState.Circling;
                stateTimer = 180;
                NPC.localAI[0] = 1f;
            }

            // 状态机
            stateTimer--;
            attackTimer++;

            switch (CurrentState) {
                case AIState.Circling:
                    CirclingBehavior();
                    break;
                case AIState.DashPrepare:
                    DashPrepareBehavior();
                    break;
                case AIState.Dash:
                    DashBehavior();
                    break;
                case AIState.VoidBreath:
                    VoidBreathBehavior();
                    break;
                case AIState.SoulStorm:
                    SoulStormBehavior();
                    break;
                case AIState.DimensionRift:
                    DimensionRiftBehavior();
                    break;
                case AIState.VoidDevour:
                    VoidDevourBehavior();
                    break;
                case AIState.DesperateFury:
                    DesperateFuryBehavior();
                    break;
            }

            // 旋转和朝向
            UpdateRotation();
        }

        /// <summary>
        /// 创建持续的能量光环粒子
        /// </summary>
        private void CreateAuraParticles() {
            // 环绕粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 60f + Main.rand.NextFloat(30f);
                Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                int dustType = Main.rand.NextBool(3) ? DustID.PurpleTorch : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.2f * auraIntensity;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                d.alpha = 100;
            }

            // 狂暴阶段的额外粒子
            if (isPhase3 && Main.rand.NextBool(2)) {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(80f, 80f);
                var d = Dust.NewDustPerfect(pos, DustID.ShadowbeamStaff);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
            }
        }

        /// <summary>
        /// 检查阶段转换
        /// </summary>
        private void CheckPhaseTransition() {
            float lifePercent = (float)NPC.life / NPC.lifeMax;

            if (!isPhase2 && lifePercent <= 0.5f) {
                isPhase2 = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                // 阶段转换特效
                CreatePhaseTransitionEffect();
            }

            if (!isPhase3 && lifePercent <= 0.25f) {
                isPhase3 = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                CreatePhaseTransitionEffect();
                // 进入狂暴状态
                if (CurrentState != AIState.DesperateFury) {
                    CurrentState = AIState.DesperateFury;
                    stateTimer = 600;
                }
            }
        }

        /// <summary>
        /// 阶段转换特效
        /// </summary>
        private void CreatePhaseTransitionEffect() {
            // 大规模虚空爆发
            AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 150f, 1.2f, 50);
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 120f, 3, 20);

            // 多重能量波
            for (int i = 0; i < 3; i++) {
                TriggerEnergyWave();
            }

            // 传统粒子
            for (int i = 0; i < 100; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(15f, 15f);
                int dust = Dust.NewDust(NPC.Center, NPC.width, NPC.height,
                    DustID.Shadowflame, velocity.X, velocity.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // 屏幕闪烁
            AwakeningNetherHelper.CreateScreenFlash(NPC.Center, AwakeningNetherHelper.AwakeningPurple, 0.8f);
        }

        /// <summary>
        /// 环绕玩家移动
        /// </summary>
        private void CirclingBehavior() {
            float radius = isPhase2 ? 350f : 450f;
            float speed = isPhase2 ? 0.06f : 0.04f;

            NPC.ai[1] += speed;
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 200f
            );

            Vector2 toTarget = targetPos - NPC.Center;
            float inertia = isPhase2 ? 15f : 20f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 8f) / inertia;

            // 周期性发射弹幕
            if (attackTimer % (isPhase2 ? 60 : 90) == 0) {
                ShootVoidBolts();
            }

            // 状态转换
            if (stateTimer <= 0) {
                ChooseNextState();
            }
        }

        /// <summary>
        /// 冲刺准备
        /// </summary>
        private void DashPrepareBehavior() {
            // 减速并瞄准玩家
            NPC.velocity *= 0.92f;

            // 锁定目标位置
            if (stateTimer == 45) {
                dashTarget = Target.Center + Target.velocity * 25f; // 增强预判
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);

                // 蓄力漩涡特效
                AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 100f, 0.8f, 30);
            }

            // 持续蓄力粒子
            if (stateTimer < 45 && stateTimer > 0) {
                float chargeProgress = 1f - stateTimer / 45f;
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100f * (1f - chargeProgress), 100f * (1f - chargeProgress));
                    Vector2 dustVel = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + chargeProgress * 5f);
                    var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f + chargeProgress;
                    d.velocity = dustVel;
                }
            }

            if (stateTimer <= 0) {
                CurrentState = AIState.Dash;
                stateTimer = 50; // 延长冲刺时间
                Vector2 direction = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = direction * dashSpeed;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);

                // 冲刺爆发特效
                TriggerEnergyWave();
                AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 80f, 2, 12);
            }
        }

        /// <summary>
        /// 冲刺攻击
        /// </summary>
        private void DashBehavior() {
            // 高速拖尾特效
            AwakeningNetherHelper.CreateVoidTrail(NPC.Center, NPC.velocity, 1.5f);

            // 冲刺过程中产生次元撕裂
            if (Main.rand.NextBool(3) && stateTimer > 20) {
                Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Vector2 tearStart = NPC.Center + perpendicular * Main.rand.NextFloat(-50f, 50f);
                Vector2 tearEnd = tearStart - NPC.velocity.SafeNormalize(Vector2.Zero) * 80f;
                AwakeningNetherHelper.CreateDimensionTear(tearStart, tearEnd, 0.5f);
            }

            if (stateTimer <= 15) {
                NPC.velocity *= 0.94f;
            }

            if (stateTimer <= 0) {
                dashCount++;
                if (dashCount < MaxDashes && isPhase2) {
                    // 连续冲刺 - 更短的准备时间
                    CurrentState = AIState.DashPrepare;
                    stateTimer = isPhase3 ? 20 : 25;
                }
                else {
                    dashCount = 0;
                    ChooseNextState();
                }
            }
        }

        /// <summary>
        /// 虚空吐息 - 扇形弹幕
        /// </summary>
        private void VoidBreathBehavior() {
            // 缓慢跟踪玩家
            Vector2 targetPos = Target.Center - new Vector2(0, 300f);
            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.05f, 0.1f);

            // 发射扇形弹幕
            int fireRate = isPhase2 ? 8 : 12;
            if (attackTimer % fireRate == 0 && stateTimer > 60) {
                ShootVoidBreath();
            }

            if (stateTimer <= 0) {
                ChooseNextState();
            }
        }

        /// <summary>
        /// 灵魂风暴 - 环形弹幕
        /// </summary>
        private void SoulStormBehavior() {
            // 在玩家上方盘旋
            Vector2 targetPos = Target.Center - new Vector2(0, 400f);
            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.04f, 0.08f);

            // 发射环形弹幕
            int fireRate = isPhase2 ? 20 : 30;
            if (attackTimer % fireRate == 0 && stateTimer > 90) {
                ShootSoulStorm();
            }

            if (stateTimer <= 0) {
                ChooseNextState();
            }
        }

        /// <summary>
        /// 次元裂隙 - 召唤虚空裂隙
        /// </summary>
        private void DimensionRiftBehavior() {
            // 快速移动
            float angle = NPC.ai[1] * 0.08f;
            Vector2 targetPos = Target.Center + new Vector2(MathF.Cos(angle) * 500f, MathF.Sin(angle) * 300f);
            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.08f, 0.12f);
            NPC.ai[1]++;

            // 创建次元裂隙
            if (stateTimer == 150 || stateTimer == 100 || stateTimer == 50) {
                CreateDimensionRift();
            }

            if (stateTimer <= 0) {
                ChooseNextState();
            }
        }

        /// <summary>
        /// 虚空吞噬 - 新增的终极攻击模式
        /// </summary>
        private void VoidDevourBehavior() {
            // 移动到玩家上方
            Vector2 targetPos = Target.Center - new Vector2(0, 350f);
            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.1f);

            // 产生吸引效果
            if (stateTimer > 60 && stateTimer < 180) {
                // 创建持续的虚空漩涡
                if (stateTimer % 10 == 0) {
                    AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 200f, 0.6f, 20);
                }

                // 对玩家产生轻微吸引
                Vector2 pullDir = (NPC.Center - Target.Center).SafeNormalize(Vector2.Zero);
                float pullStrength = 2f * (1f - Vector2.Distance(Target.Center, NPC.Center) / 500f);
                if (pullStrength > 0) {
                    Target.velocity += pullDir * pullStrength * 0.1f;
                }

                // 周期性发射追踪弹
                if (stateTimer % 20 == 0) {
                    ShootVoidBolts();
                }
            }

            // 结束时的大爆发
            if (stateTimer == 60) {
                // 发射大量灵魂弹
                ShootMassiveSoulStorm();
                TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
            }

            if (stateTimer <= 0) {
                ChooseNextState();
            }
        }

        /// <summary>
        /// 狂暴阶段 - 低血量时的疯狂攻击
        /// </summary>
        private void DesperateFuryBehavior() {
            // 疯狂追击玩家
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float targetSpeed = 10f; // 提升速度

            NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer * targetSpeed, 0.18f);

            // 持续高速拖尾
            AwakeningNetherHelper.CreateVoidTrail(NPC.Center, NPC.velocity, 2f);

            // 持续发射弹幕
            if (attackTimer % 12 == 0) {
                ShootVoidBolts();
            }

            if (attackTimer % 40 == 0) {
                ShootSoulStorm();
            }

            // 周期性创建裂隙
            if (attackTimer % 90 == 0) {
                CreateDimensionRift();
            }

            // 狂暴特效 - 持续的能量爆发
            if (Main.rand.NextBool(2)) {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(60f, 60f);
                var d = Dust.NewDustPerfect(pos, DustID.ShadowbeamStaff);
                d.noGravity = true;
                d.scale = 2f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }

            // 周期性能量波
            if (attackTimer % 60 == 0) {
                TriggerEnergyWave();
            }

            if (stateTimer <= 0) {
                stateTimer = 250;
                // 狂暴阶段间歇性切换到冲刺或吞噬
                int choice = Main.rand.Next(3);
                if (choice == 0) {
                    CurrentState = AIState.DashPrepare;
                    stateTimer = 35;
                }
                else if (choice == 1) {
                    CurrentState = AIState.VoidDevour;
                    stateTimer = 200;
                }
            }
        }

        /// <summary>
        /// 选择下一个状态
        /// </summary>
        private void ChooseNextState() {
            int choice = Main.rand.Next(isPhase2 ? 6 : 4);

            switch (choice) {
                case 0:
                    CurrentState = AIState.Circling;
                    stateTimer = isPhase2 ? 120 : 180;
                    break;
                case 1:
                    CurrentState = AIState.DashPrepare;
                    stateTimer = 45;
                    dashCount = 0;
                    break;
                case 2:
                    CurrentState = AIState.VoidBreath;
                    stateTimer = isPhase2 ? 150 : 200;
                    attackTimer = 0;
                    break;
                case 3:
                    CurrentState = AIState.SoulStorm;
                    stateTimer = isPhase2 ? 180 : 220;
                    attackTimer = 0;
                    break;
                case 4:
                    CurrentState = AIState.DimensionRift;
                    stateTimer = 180;
                    NPC.ai[1] = 0;
                    break;
                case 5:
                    CurrentState = AIState.VoidDevour;
                    stateTimer = 240;
                    break;
            }

            // 狂暴阶段强制进入狂暴
            if (isPhase3 && Main.rand.NextBool(2)) {
                CurrentState = AIState.DesperateFury;
                stateTimer = 250;
            }
        }

        /// <summary>
        /// 更新旋转和朝向
        /// </summary>
        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }
        }

        #region 攻击方法

        /// <summary>
        /// 发射虚空弹 - 使用自定义追踪弹幕
        /// </summary>
        private void ShootVoidBolts() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(90);
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            int count = isPhase3 ? 7 : (isPhase2 ? 5 : 3);
            float spread = isPhase2 ? 0.18f : 0.12f;

            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 direction = toPlayer.RotatedBy(angle);
                float speed = 14f + Main.rand.NextFloat(-2f, 2f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 60f,
                    direction * speed,
                    ModContent.ProjectileType<AwakeningNetherVoidBolt>(),
                    damage,
                    0f,
                    ai0: isPhase3 ? 1 : 0, // 狂暴时为强化版
                    ai1: isPhase2 ? 1 : 0  // 追踪等级
                );
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);

            // 发射特效
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 40f, 1, 8);
        }

        /// <summary>
        /// 虚空吐息 - 使用自定义吐息弹幕
        /// </summary>
        private void ShootVoidBreath() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(70);
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            int count = isPhase3 ? 9 : (isPhase2 ? 7 : 5);
            float totalSpread = MathHelper.ToRadians(isPhase3 ? 75f : (isPhase2 ? 60f : 45f));

            for (int i = 0; i < count; i++) {
                float angle = -totalSpread / 2 + totalSpread * i / (count - 1);
                Vector2 direction = toPlayer.RotatedBy(angle);
                float speed = 12f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 50f,
                    direction * speed,
                    ModContent.ProjectileType<AwakeningNetherBreath>(),
                    damage,
                    0f,
                    ai0: isPhase3 ? 1 : 0 // 狂暴版
                );
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);

            // 吐息音效和特效
            AwakeningNetherHelper.CreateVoidVortex(NPC.Center + toPlayer * 60f, 60f, 0.5f, 15);
        }

        /// <summary>
        /// 灵魂风暴 - 环形弹幕
        /// </summary>
        private void ShootSoulStorm() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(80);
            int count = isPhase3 ? 20 : (isPhase2 ? 16 : 12);

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 direction = angle.ToRotationVector2();
                float speed = 10f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    direction * speed,
                    ModContent.ProjectileType<AwakeningNetherSoulOrb>(),
                    damage,
                    0f,
                    ai0: i % 2, // 0=直线，1=螺旋
                    ai1: i % 3  // 颜色索引
                );
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);

            // 灵魂风暴特效
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 100f, 4, 24);
            TriggerEnergyWave();
        }

        /// <summary>
        /// 大规模灵魂风暴 - 虚空吞噬结束时使用
        /// </summary>
        private void ShootMassiveSoulStorm() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(75);

            // 多波次发射
            for (int wave = 0; wave < 3; wave++) {
                int count = 12 + wave * 4;
                float angleOffset = wave * 0.15f;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + angleOffset;
                    Vector2 direction = angle.ToRotationVector2();
                    float speed = 8f + wave * 2f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        direction * speed,
                        ModContent.ProjectileType<AwakeningNetherSoulOrb>(),
                        damage,
                        0f,
                        ai0: 1,
                        ai1: wave
                    );
                }
            }

            // 大规模特效
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 150f, 5, 30);
            AwakeningNetherHelper.CreateScreenFlash(NPC.Center, AwakeningNetherHelper.SoulPink, 0.6f);
        }

        /// <summary>
        /// 创建次元裂隙
        /// </summary>
        private void CreateDimensionRift() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(100);

            // 在玩家附近随机位置创建裂隙
            Vector2 riftPos = Target.Center + Main.rand.NextVector2Circular(350f, 350f);

            // 确保裂隙不会太近或太远
            float dist = Vector2.Distance(riftPos, Target.Center);
            if (dist < 100f) {
                riftPos = Target.Center + (riftPos - Target.Center).SafeNormalize(Vector2.UnitX) * 150f;
            }

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                riftPos,
                Vector2.Zero,
                ModContent.ProjectileType<AwakeningNetherRift>(),
                damage,
                0f,
                ai0: isPhase2 ? 1 : 0 // 大小等级
            );

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.3f, Volume = 1.2f }, riftPos);

            // 裂隙创建特效
            AwakeningNetherHelper.CreateDimensionTear(NPC.Center, riftPos, 0.8f);
        }

        /// <summary>
        /// 获取弹幕伤害（根据难度调整）
        /// </summary>
        private int GetProjectileDamage(int baseDamage) {
            if (Main.masterMode)
                return (int)(baseDamage * 1.5f);
            if (Main.expertMode)
                return (int)(baseDamage * 1.25f);
            return baseDamage;
        }

        #endregion

        public override void OnKill() {
            base.OnKill();

            // 史诗级死亡特效
            AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 300f, 2f, 100);
            AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 250f, 5, 30);

            // 多重能量波
            for (int i = 0; i < 5; i++) {
                TriggerEnergyWave();
            }

            // 次元撕裂
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                AwakeningNetherHelper.CreateDimensionTear(NPC.Center, NPC.Center + dir * 200f, 1f);
            }

            // 大量粒子
            for (int i = 0; i < 200; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(25f, 25f);
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.ShadowbeamStaff;
                var d = Dust.NewDustPerfect(NPC.Center, dustType);
                d.noGravity = true;
                d.scale = 3f;
                d.velocity = velocity;
            }

            // 屏幕闪烁
            AwakeningNetherHelper.CreateScreenFlash(NPC.Center, AwakeningNetherHelper.AwakeningPurple, 1.5f);

            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1.8f }, NPC.Center);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height * 0.4f);

            // 绘制能量波
            DrawEnergyWaves(spriteBatch, screenPos);

            // 觉醒态的紫色幽冥色调
            Color netherColor = Color.Lerp(drawColor, AwakeningNetherHelper.AwakeningPurple, 0.5f);

            // 阶段2颜色变化
            if (isPhase2) {
                netherColor = Color.Lerp(netherColor, AwakeningNetherHelper.NetherCyan, 0.2f);
            }

            // 狂暴阶段颜色更深 + 闪烁
            if (isPhase3) {
                float flash = MathF.Sin(pulsePhase * 3f) * 0.3f + 0.7f;
                netherColor = Color.Lerp(netherColor, AwakeningNetherHelper.DestructionRed, 0.4f * flash);
            }

            // 绘制外层能量光环
            DrawEnergyAura(spriteBatch, screenPos);

            // 绘制多层拖尾
            DrawAdvancedTrail(spriteBatch, screenPos, tex, origin, netherColor);

            // 主体绘制
            SpriteEffects mainEffects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float drawRot = NPC.spriteDirection == -1 ? NPC.rotation - MathHelper.Pi : NPC.rotation;
            // 外层光晕
            Color glowColor = netherColor;
            glowColor.A = 0;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            for (int i = 3; i >= 0; i--) {
                float glowScale = NPC.scale * (1.2f + i * 0.15f) * pulse * auraIntensity;
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, glowColor * (0.15f / (i + 1)),
                    drawRot, origin, glowScale, mainEffects, 0);
            }

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, drawRot,
                origin, NPC.scale * pulse, mainEffects, 0);

            // 绘制环绕的灵魂球（狂暴阶段）
            if (isPhase3) {
                DrawOrbitingSouls(spriteBatch, screenPos);
            }

            return false;
        }

        /// <summary>
        /// 绘制能量波
        /// </summary>
        private void DrawEnergyWaves(SpriteBatch sb, Vector2 screenPos) {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0.05f) {
                    Color waveColor = isPhase3
                        ? AwakeningNetherHelper.DestructionRed
                        : AwakeningNetherHelper.AwakeningPurple;
                    AwakeningNetherHelper.DrawEnergyWave(sb, NPC.Center, energyWaveRadius[i], 20f,
                        waveColor, energyWaveAlpha[i] * 0.5f);
                }
            }
        }

        /// <summary>
        /// 绘制能量光环
        /// </summary>
        private void DrawEnergyAura(SpriteBatch sb, Vector2 screenPos) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            int auraPoints = 12;
            float auraRadius = 80f * auraIntensity;

            for (int i = 0; i < auraPoints; i++) {
                float angle = pulsePhase * 0.5f + MathHelper.TwoPi * i / auraPoints;
                float dist = auraRadius + MathF.Sin(pulsePhase * 2f + i * 0.5f) * 15f;
                Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Color auraColor = isPhase3
                    ? Color.Lerp(AwakeningNetherHelper.AwakeningPurple, AwakeningNetherHelper.DestructionRed, 0.5f)
                    : AwakeningNetherHelper.AwakeningPurple;
                auraColor.A = 0;
                float auraScale = 0.8f + MathF.Sin(pulsePhase + i) * 0.2f;

                sb.Draw(tex, pos - screenPos, null, auraColor * 0.5f * auraIntensity,
                    angle, origin, auraScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制高级拖尾
        /// </summary>
        private void DrawAdvancedTrail(SpriteBatch sb, Vector2 screenPos, Texture2D tex, Vector2 origin, Color baseColor) {
            // 外层光晕拖尾
            for (int layer = 0; layer < 2; layer++) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;

                    Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                    float progress = 1f - i / (float)NPC.oldPos.Length;
                    float fade = progress * (layer == 0 ? 0.3f : 0.5f) * auraIntensity;

                    Color trailColor = layer == 0
                        ? AwakeningNetherHelper.VoidDarkPurple
                        : baseColor;
                    trailColor *= fade;
                    if (layer == 0) trailColor.A = 0;

                    float trailScale = NPC.scale * (layer == 0 ? 1.4f : 1f) * (0.6f + progress * 0.4f);

                    SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                    sb.Draw(tex, pos, null, trailColor, NPC.rotation + MathHelper.PiOver2, origin, trailScale,
                        effects, 0);
                }
            }

            // 能量线连接
            var dustTex = BAWImpermanences.BAWHelper.DustTexture;
            if (dustTex != null) {
                for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                    if (NPC.oldPos[i] == Vector2.Zero || NPC.oldPos[i - 1] == Vector2.Zero) continue;

                    Vector2 start = NPC.oldPos[i - 1] + NPC.Size / 2;
                    Vector2 end = NPC.oldPos[i] + NPC.Size / 2;

                    float progress = 1f - i / (float)NPC.oldPos.Length;
                    Color lineColor = AwakeningNetherHelper.NetherCyan * progress * 0.2f * auraIntensity;

                    AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, lineColor, 6f * progress, pulsePhase);
                }
            }
        }

        /// <summary>
        /// 绘制环绕的灵魂球（狂暴阶段）
        /// </summary>
        private void DrawOrbitingSouls(SpriteBatch sb, Vector2 screenPos) {
            AwakeningNetherHelper.DrawSoulOrbit(sb, NPC.Center, 100f, 4, pulsePhase * 1.5f, pulsePhase,
                [AwakeningNetherHelper.DestructionRed, AwakeningNetherHelper.AwakeningPurple,
                 AwakeningNetherHelper.SoulPink, AwakeningNetherHelper.NetherCyan]);
        }
    }
}

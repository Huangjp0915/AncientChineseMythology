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

        /// <summary>
        /// 脚本化三幕结构（非血量密度档）。每一幕改变战斗"规则"而非数值。
        /// </summary>
        private enum AIState
        {
            ActI_Patrol,    // 第一幕 冥界巡游：环绕 + 单条预告吐息走廊（地面魂蚀残留）
            ActII_Rift,     // 第二幕 次元裂隙：成对传送门，龙穿门冲刺
            ActII_Storm,    // 第二幕 裂隙后的一次性预告灵魂风暴
            ActIII_Vortex,  // 第三幕 虚空吞噬：中央漩涡 + 可清除噬魂卫星 + 集中爆发
            Finality,       // 觉醒终末（一次性）：龙体拉直 + 巨型吐息扫射 + 同步体节激光
            ActTransition   // 幕间转换（含无敌预告节拍）
        }

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // 计时器与子状态
        private int stateTimer = 0;     // 倒数计时（用于转换/计时窗口）
        private int attackTimer = 0;    // 子步骤计时（递增）
        private int subPhase = 0;       // 当前状态内的子步骤
        private int globalTimer = 0;    // 全局计时（被动机制节拍）

        // 幕控制
        private int act = 1;            // 当前幕 1/2/3
        private int pendingAct = 1;     // 转换目标幕
        private bool finalityDone = false; // 觉醒终末是否已触发（一次性）

        // 次元裂隙之门
        private Vector2 gateEntrance;
        private Vector2 gateExit;
        private int gateDashes = 0;
        private const int GateDashTarget = 2;

        // 第一幕吐息走廊高度
        private float laneY;

        // 第三幕漩涡
        private Vector2 vortexCenter;

        // 阶段控制（仅用于绘制配色，由血量推导）
        private bool isPhase2 = false; // 进入第二幕及之后
        private bool isPhase3 = false; // 进入第三幕及之后

        // 视觉效果参数
        private float pulsePhase = 0f;
        private float auraIntensity = 0f;
        private float[] energyWaveRadius = new float[3];
        private float[] energyWaveAlpha = new float[3];

        // ===== V2 演出标量 (纯本地视觉, 经 AwakeningNetherScreenSystem / PostDraw 消费) =====
        private float fogTint = 0f;     // ElementalScreenTint 冥雾 (每幕递进加深)
        private float riftWarp = 0f;    // GenericWarp · rift (次元裂隙门冲刺)
        private float voidWarp = 0f;    // GenericWarp · void + uRadialPull (虚空吞噬"被吸入")
        private float bloom = 0f;       // RadialBloom (吐息/激光帘幕/终末喷发)
        private float runic = 0f;       // ArenaRunic (裂隙门/漩涡向心收口预警)
        private Vector2 bloomCenter;
        private Vector2 runicCenter;
        private float runicRadius = 340f;
        private bool runicLethal = false;

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
            // 每帧默认可受击，仅转换/预告节拍主动开启无敌。
            NPC.dontTakeDamage = false;
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 更新视觉效果 + 由血量推导绘制配色用阶段
            pulsePhase += 0.08f;
            float lifePct = (float)NPC.life / NPC.lifeMax;
            isPhase2 = lifePct <= 0.6f;
            isPhase3 = lifePct <= 0.3f;
            auraIntensity = MathHelper.Lerp(auraIntensity, isPhase3 ? 1.5f : (isPhase2 ? 1.2f : 1f), 0.02f);
            UpdateEnergyWaves();
            CreateAuraParticles();

            // 初始化
            if (NPC.localAI[0] == 0f) {
                CurrentState = AIState.ActI_Patrol;
                act = 1;
                stateTimer = 0;
                attackTimer = 0;
                subPhase = 0;
                NPC.localAI[0] = 1f;
            }

            // 幕转换（脚本化，改变规则）
            CheckActTransition(lifePct);

            stateTimer--;
            attackTimer++;
            globalTimer++;

            // 体节作为机制：持续释放虚空魂雾
            EmitSegmentMiasma();

            // V2 演出标量：冥雾每幕加深 + 非持续标量自然衰减（各 beat 按需抬升）
            UpdatePresentationTargets();

            switch (CurrentState) {
                case AIState.ActI_Patrol:
                    ActIPatrolBehavior();
                    break;
                case AIState.ActII_Rift:
                    ActIIRiftBehavior();
                    break;
                case AIState.ActII_Storm:
                    ActIIStormBehavior();
                    break;
                case AIState.ActIII_Vortex:
                    ActIIIVortexBehavior();
                    break;
                case AIState.Finality:
                    FinalityBehavior();
                    break;
                case AIState.ActTransition:
                    ActTransitionBehavior();
                    break;
            }

            UpdateRotation();

            // 发布 V2 演出标量（纯本地视觉）
            if (!Main.dedServ) {
                AwakeningNetherScreenSystem.Publish(fogTint, bloom, bloomCenter,
                    runic, runicCenter, runicRadius, runicLethal, (float)Main.GlobalTimeWrappedHourly);
            }
        }

        /// <summary>
        /// V2 演出标量驱动：冥雾随幕递进加深；裂隙/虚空扭曲、泛光、符阵预警为脉冲式，自然衰减、各 beat 抬升。
        /// </summary>
        private void UpdatePresentationTargets() {
            float fogTarget = act >= 3 ? 0.42f : (act == 2 ? 0.30f : 0.18f);
            fogTint = MathHelper.Lerp(fogTint, fogTarget, 0.02f);

            riftWarp = MathHelper.Lerp(riftWarp, 0f, 0.05f);
            voidWarp = MathHelper.Lerp(voidWarp, 0f, 0.06f);
            bloom = MathHelper.Lerp(bloom, 0f, 0.08f);
            runic = MathHelper.Lerp(runic, 0f, 0.06f);

            // 默认泛光中心跟随头部（具体 beat 会就近改写）
            bloomCenter = NPC.Center;
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
        /// 脚本化幕转换：60% → 第二幕，30% → 第三幕，15% → 觉醒终末（一次性）。
        /// 每次转换都有无敌预告节拍，且改变战斗"规则"。
        /// </summary>
        private void CheckActTransition(float lifePct) {
            if (CurrentState == AIState.ActTransition || CurrentState == AIState.Finality)
                return;

            // 觉醒终末：一次性，深入第三幕后触发（替代旧的 DesperateFury 喷弹狂暴）
            if (!finalityDone && lifePct <= 0.15f) {
                EnterFinality();
                return;
            }

            int targetAct = lifePct > 0.6f ? 1 : (lifePct > 0.3f ? 2 : 3);
            if (targetAct > act) {
                pendingAct = targetAct;
                EnterTransition();
            }
        }

        private void EnterTransition() {
            CurrentState = AIState.ActTransition;
            stateTimer = 75;
            subPhase = 0;
            attackTimer = 0;
            NPC.velocity *= 0.3f;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
            CreatePhaseTransitionEffect();
            ACMUtils.AddScreenShake(12f);
            bloom = 1f;
            bloomCenter = NPC.Center;
            riftWarp = System.Math.Max(riftWarp, 0.5f);
            NPC.netUpdate = true;
        }

        private void EnterFinality() {
            finalityDone = true;
            CurrentState = AIState.Finality;
            stateTimer = 0;
            attackTimer = 0;
            subPhase = 0;
            NPC.velocity *= 0.3f;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.5f }, NPC.Center);
            CreatePhaseTransitionEffect();
            ACMUtils.AddScreenShake(12f);
            bloom = 1f;
            bloomCenter = NPC.Center;
            voidWarp = System.Math.Max(voidWarp, 0.5f);
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 幕间转换 - 无敌预告节拍，结束后进入目标幕的起始状态。
        /// </summary>
        private void ActTransitionBehavior() {
            NPC.dontTakeDamage = true; // i-frame 节拍：转换中无敌，给玩家喘息与可读性
            NPC.velocity *= 0.9f;

            // 向玩家上方汇聚
            Vector2 hover = Target.Center + new Vector2(0, -320f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 v = Main.rand.NextVector2CircularEdge(12f, 12f);
                int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, v.X, v.Y, 100, default, 2.2f);
                Main.dust[d].noGravity = true;
            }

            if (stateTimer <= 0) {
                act = pendingAct;
                attackTimer = 0;
                subPhase = 0;
                CurrentState = act >= 3 ? AIState.ActIII_Vortex : AIState.ActII_Rift;
                NPC.netUpdate = true;
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

        // ============================ 第一幕 冥界巡游 ============================

        /// <summary>
        /// 环绕玩家的基础移动。
        /// </summary>
        private void CircleTarget(float radius, float speed) {
            NPC.ai[1] += speed;
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 200f
            );
            Vector2 toTarget = targetPos - NPC.Center;
            float inertia = 18f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 8f) / inertia;
        }

        /// <summary>
        /// 第一幕：环绕巡游 + 一条预告型吐息走廊（在地面留下魂蚀残留）。
        /// 体节同时被动释放魂雾。
        /// </summary>
        private void ActIPatrolBehavior() {
            CircleTarget(440f, 0.04f);

            switch (subPhase) {
                case 0: // 巡游窗口：少量可读追踪弹
                    if (attackTimer % 70 == 0)
                        ShootVoidBolts();
                    if (attackTimer >= 160) {
                        subPhase = 1;
                        attackTimer = 0;
                        laneY = Target.Center.Y + 170f; // 地面走廊高度
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                    }
                    break;

                case 1: // 吐息预告（45）：口部聚能，标示即将到来的走廊
                    NPC.velocity *= 0.96f;
                    if (Main.netMode != NetmodeID.Server) {
                        Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 50f;
                        for (int i = 0; i < 3; i++) {
                            Vector2 p = mouth + Main.rand.NextVector2Circular(70f, 70f);
                            var d = Dust.NewDustPerfect(p, DustID.Shadowflame);
                            d.noGravity = true;
                            d.scale = 1.6f;
                            d.velocity = (mouth - p) * 0.1f;
                        }
                        // 走廊地面预告标线
                        if (attackTimer % 3 == 0) {
                            float tx = Target.Center.X + Main.rand.NextFloat(-520f, 520f);
                            var d = Dust.NewDustPerfect(new Vector2(tx, laneY), DustID.PurpleTorch);
                            d.noGravity = true;
                            d.scale = 1.1f;
                        }
                    }
                    if (attackTimer >= 45) {
                        subPhase = 2;
                        attackTimer = 0;
                    }
                    break;

                case 2: // 吐息扫射（55）：沿走廊扫过，铺设魂雾残留
                    if (attackTimer % 7 == 0)
                        FireBreathSweep();
                    if (attackTimer % 13 == 0)
                        SpawnMiasma(new Vector2(Target.Center.X + Main.rand.NextFloat(-500f, 500f), laneY), 1.3f);
                    // 吐息走廊泛光（口部）
                    bloom = System.Math.Max(bloom, 0.45f);
                    bloomCenter = NPC.Center + NPC.rotation.ToRotationVector2() * 50f;
                    if (attackTimer >= 55) {
                        subPhase = 0;
                        attackTimer = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// 单条吐息走廊：吐息焦点沿走廊水平扫过。
        /// </summary>
        private void FireBreathSweep() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float t = attackTimer / 55f;
            float sweepX = Target.Center.X + MathHelper.Lerp(-500f, 500f, t);
            Vector2 lanePoint = new Vector2(sweepX, laneY);
            Vector2 dir = (lanePoint - NPC.Center).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 50f, dir * 13f,
                ModContent.ProjectileType<AwakeningNetherBreath>(), GetProjectileDamage(70), 0f, Main.myPlayer);
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 0.8f }, NPC.Center);
        }

        // ============================ 第二幕 次元裂隙 ============================

        /// <summary>
        /// 第二幕：成对传送门。龙在两门之间穿梭冲刺，玩家选择跟随哪扇门。
        /// 灵魂风暴只在一次成功的穿门序列之后释放。
        /// </summary>
        private void ActIIRiftBehavior() {
            switch (subPhase) {
                case 0: // 短暂巡游后开启成对门
                    CircleTarget(420f, 0.05f);
                    if (attackTimer >= 70) {
                        float side = Main.rand.NextBool() ? -1f : 1f;
                        gateEntrance = Target.Center + new Vector2(side * 640f, -120f);
                        gateExit = Target.Center + new Vector2(-side * 640f, -120f);
                        SpawnGate(gateEntrance);
                        SpawnGate(gateExit);
                        gateDashes = 0;
                        subPhase = 1;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                        // 入口裂隙门符阵预备（向心收口预告，可读落点；主题色非致命）
                        runicCenter = gateEntrance;
                        runicRadius = 300f;
                        runicLethal = false;
                        runic = System.Math.Max(runic, 0.45f);
                        riftWarp = System.Math.Max(riftWarp, 0.3f);
                    }
                    break;

                case 1: // 蓄力（等门完全开启，预告）
                    Vector2 charge = gateEntrance + (gateEntrance - Target.Center).SafeNormalize(Vector2.UnitX) * 220f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (charge - NPC.Center) * 0.06f, 0.1f);
                    // 裂隙扭曲渐强 + 符阵转致命（龙即将穿门冲刺）
                    riftWarp = System.Math.Max(riftWarp, 0.3f + attackTimer / 50f * 0.35f);
                    runicCenter = gateEntrance;
                    runic = System.Math.Max(runic, 0.6f);
                    runicLethal = attackTimer > 28;
                    if (Main.netMode != NetmodeID.Server && attackTimer % 2 == 0) {
                        Vector2 p = NPC.Center + Main.rand.NextVector2Circular(80f, 80f);
                        var d = Dust.NewDustPerfect(p, DustID.Shadowflame);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = (NPC.Center - p) * 0.12f;
                    }
                    if (attackTimer >= 50) {
                        subPhase = 2;
                        attackTimer = 0;
                        NPC.velocity = (gateEntrance - NPC.Center).SafeNormalize(Vector2.UnitY) * 42f;
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    }
                    break;

                case 2: // 穿门冲刺 - 在两门之间往返
                    AwakeningNetherHelper.CreateVoidTrail(NPC.Center, NPC.velocity, 1.5f);
                    // 持续转向入口门
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        (gateEntrance - NPC.Center).SafeNormalize(Vector2.UnitY) * 42f, 0.05f);
                    // 裂隙扭曲维持高位 + 入口门致命符阵
                    riftWarp = System.Math.Max(riftWarp, 0.65f);
                    runicCenter = gateEntrance;
                    runic = System.Math.Max(runic, 0.7f);
                    runicLethal = true;

                    if (NPC.Center.Distance(gateEntrance) < 90f) {
                        NPC.Center = gateExit; // 从另一扇门冲出
                        NPC.velocity = (gateEntrance - gateExit).SafeNormalize(Vector2.UnitY) * 42f;
                        gateDashes++;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                        AwakeningNetherHelper.CreateSoulBurst(NPC.Center, 80f, 2, 14);
                        ACMUtils.AddScreenShake(6f);
                        // 穿门贯出的泛光 + 裂隙脉冲
                        bloom = System.Math.Max(bloom, 0.8f);
                        bloomCenter = NPC.Center;
                        riftWarp = 0.85f;
                        if (gateDashes >= GateDashTarget) {
                            subPhase = 3;
                            attackTimer = 0;
                        }
                    }
                    if (attackTimer >= 150) { // 安全兜底
                        subPhase = 3;
                        attackTimer = 0;
                    }
                    break;

                case 3: // 收尾 → 释放灵魂风暴
                    NPC.velocity *= 0.9f;
                    if (attackTimer >= 25) {
                        CurrentState = AIState.ActII_Storm;
                        subPhase = 0;
                        attackTimer = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// 第二幕：穿门成功后的一次性预告灵魂风暴（非持续喷射）。
        /// </summary>
        private void ActIIStormBehavior() {
            Vector2 hover = Target.Center - new Vector2(0, 380f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            switch (subPhase) {
                case 0: // 预告聚能（50）
                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f }, NPC.Center);
                    if (Main.netMode != NetmodeID.Server && attackTimer % 2 == 0) {
                        Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(140f, 140f);
                        var d = Dust.NewDustPerfect(p, DustID.ShadowbeamStaff);
                        d.noGravity = true;
                        d.scale = 1.4f;
                        d.velocity = (NPC.Center - p) * 0.08f;
                    }
                    if (attackTimer >= 50) {
                        subPhase = 1;
                        attackTimer = 0;
                    }
                    break;

                case 1: // 一次性爆发
                    if (attackTimer == 1) {
                        ShootSoulStorm();
                        TriggerEnergyWave();
                        ACMUtils.AddScreenShake(10f);
                        bloom = 0.9f;
                        bloomCenter = NPC.Center;
                    }
                    if (attackTimer >= 45) {
                        CurrentState = AIState.ActII_Rift;
                        subPhase = 0;
                        attackTimer = 0;
                    }
                    break;
            }
        }

        // ============================ 第三幕 虚空吞噬 ============================

        /// <summary>
        /// 第三幕：中央漩涡固定约 5 秒。环绕的可清除噬魂卫星集中爆发，而非持续喷弹。
        /// </summary>
        private void ActIIIVortexBehavior() {
            switch (subPhase) {
                case 0: // 移到中央 + 预告 + 生成噬魂卫星
                    vortexCenter = Target.Center - new Vector2(0, 280f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (vortexCenter - NPC.Center) * 0.05f, 0.08f);
                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    // 漩涡奇点向心收口预警（虚空扭曲渐起）
                    voidWarp = System.Math.Max(voidWarp, attackTimer / 55f * 0.45f);
                    runicCenter = vortexCenter;
                    runicRadius = 360f;
                    runicLethal = false;
                    runic = System.Math.Max(runic, 0.35f + attackTimer / 55f * 0.35f);
                    if (attackTimer >= 55) {
                        SpawnSoulSatellites();
                        AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 220f, 1.2f, 50);
                        ACMUtils.AddScreenShake(12f);
                        bloom = 0.9f;
                        bloomCenter = vortexCenter;
                        voidWarp = System.Math.Max(voidWarp, 0.6f);
                        subPhase = 1;
                        attackTimer = 0;
                    }
                    break;

                case 1: // 漩涡活跃（约5秒）：将玩家拉向漩涡
                    Vector2 hover = Target.Center - new Vector2(0, 300f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.07f);
                    // 虚空吞噬"被吸入"：全屏向心扭曲 + 致命奇点符阵
                    voidWarp = System.Math.Max(voidWarp, isPhase3 ? 0.75f : 0.6f);
                    runicCenter = vortexCenter;
                    runic = System.Math.Max(runic, 0.6f);
                    runicLethal = true;

                    float dist = Target.Center.Distance(NPC.Center);
                    if (dist > 130f) {
                        Vector2 pull = (NPC.Center - Target.Center).SafeNormalize(Vector2.Zero);
                        Target.velocity += pull * 0.3f;
                    }
                    if (attackTimer % 12 == 0)
                        AwakeningNetherHelper.CreateVoidVortex(NPC.Center, 200f, 0.6f, 18);

                    if (attackTimer >= 300) {
                        subPhase = 2;
                        attackTimer = 0;
                    }
                    break;

                case 2: // 喘息后循环
                    NPC.velocity *= 0.92f;
                    if (attackTimer >= 60) {
                        subPhase = 0;
                        attackTimer = 0;
                    }
                    break;
            }
        }

        // ============================ 觉醒终末（一次性）============================

        /// <summary>
        /// 觉醒终末：龙体拉直 + 一次巨型吐息扫射 + 同步体节激光，随后回到第三幕循环。
        /// 取代旧的 DesperateFury 喷弹狂暴。
        /// </summary>
        private void FinalityBehavior() {
            switch (subPhase) {
                case 0: // 拉直龙体 + 预告（60），起手短暂无敌
                    if (attackTimer <= 22)
                        NPC.dontTakeDamage = true;
                    float dirSign = NPC.Center.X < Target.Center.X ? 1f : -1f;
                    Vector2 straightTarget = new Vector2(Target.Center.X + dirSign * 1000f, Target.Center.Y - 240f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        (straightTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 15f, 0.06f);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(160f, 160f);
                            var d = Dust.NewDustPerfect(p, DustID.ShadowbeamStaff);
                            d.noGravity = true;
                            d.scale = 2f;
                            d.velocity = (NPC.Center - p) * 0.1f;
                        }
                    }
                    // 觉醒终末蓄力：泛光渐强（处决级签名）
                    bloom = System.Math.Max(bloom, attackTimer / 60f * 0.8f);
                    bloomCenter = NPC.Center;
                    if (attackTimer >= 60) {
                        subPhase = 1;
                        attackTimer = 0;
                        FireSegmentLasers();
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                        ACMUtils.AddScreenShake(12f);
                        // 激光帘幕展开的全屏泛光
                        bloom = 1f;
                        bloomCenter = NPC.Center;
                    }
                    break;

                case 1: // 巨型吐息扫射（与体节激光同步，90）
                    NPC.velocity *= 0.97f;
                    if (attackTimer % 5 == 0)
                        FireFinalityBreath();
                    // 终末吐息走廊泛光
                    bloom = System.Math.Max(bloom, 0.5f);
                    bloomCenter = NPC.Center + NPC.rotation.ToRotationVector2() * 50f;
                    if (attackTimer >= 90) {
                        subPhase = 2;
                        attackTimer = 0;
                    }
                    break;

                case 2: // 收尾 → 回到第三幕循环
                    NPC.velocity *= 0.92f;
                    if (attackTimer >= 30) {
                        CurrentState = AIState.ActIII_Vortex;
                        subPhase = 0;
                        attackTimer = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// 觉醒终末的巨型吐息扇形（横扫）。
        /// </summary>
        private void FireFinalityBreath() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float sweep = MathF.Sin(attackTimer * 0.12f) * MathHelper.ToRadians(40f);
            int count = 5;
            float spread = MathHelper.ToRadians(50f);
            for (int i = 0; i < count; i++) {
                float a = sweep - spread / 2 + spread * i / (count - 1);
                Vector2 dir = toPlayer.RotatedBy(a);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 50f, dir * 13f,
                    ModContent.ProjectileType<AwakeningNetherBreath>(), GetProjectileDamage(80), 0f, Main.myPlayer, ai0: 1);
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
        }

        /// <summary>
        /// 同步体节激光：沿龙体取样体节，同时向下喷射激光，形成可读的激光帘幕。
        /// </summary>
        private void FireSegmentLasers() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var segs = GetSegments();
            if (segs.Count == 0)
                return;
            int step = Math.Max(1, segs.Count / 12);
            for (int i = 0; i < segs.Count; i += step) {
                NPC seg = segs[i];
                Projectile.NewProjectile(NPC.GetSource_FromAI(), seg.Center, Vector2.Zero,
                    ModContent.ProjectileType<AwakeningNetherSegmentLaser>(), GetProjectileDamage(110), 0f, Main.myPlayer,
                    ai0: seg.whoAmI, ai1: MathHelper.PiOver2);
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
        }

        // ============================ 体节机制 / 生成辅助 ============================

        /// <summary>
        /// 收集本龙的所有体节（身体 + 尾巴）。
        /// </summary>
        private System.Collections.Generic.List<NPC> GetSegments() {
            var list = new System.Collections.Generic.List<NPC>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.whoAmI != NPC.whoAmI && n.realLife == NPC.whoAmI)
                    list.Add(n);
            }
            return list;
        }

        /// <summary>
        /// 体节作为机制：按幕的节拍从随机体节释放虚空魂雾。
        /// </summary>
        private void EmitSegmentMiasma() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int cadence = act >= 3 ? 50 : (act == 2 ? 70 : 95);
            if (globalTimer % cadence != 0)
                return;
            var segs = GetSegments();
            if (segs.Count == 0)
                return;
            NPC seg = segs[Main.rand.Next(segs.Count)];
            SpawnMiasma(seg.Center, 1f);
        }

        private void SpawnMiasma(Vector2 pos, float size) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<AwakeningNetherMiasma>(), GetProjectileDamage(25), 0f, Main.myPlayer, ai0: size);
        }

        private void SpawnGate(Vector2 pos) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<AwakeningNetherRift>(), GetProjectileDamage(80), 0f, Main.myPlayer,
                ai0: isPhase3 ? 1 : 0);
        }

        private void SpawnSoulSatellites() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int count = isPhase3 ? 6 : 5;
            int who = NPC.target;
            for (int i = 0; i < count; i++) {
                float a = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = Target.Center + a.ToRotationVector2() * 330f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<AwakeningNetherSoulSatellite>(), GetProjectileDamage(85), 0f, Main.myPlayer,
                    ai0: who, ai1: a);
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

            // 屏幕闪烁 + 死亡定格震屏（一次性，纳入统一预算）
            AwakeningNetherHelper.CreateScreenFlash(NPC.Center, AwakeningNetherHelper.AwakeningPurple, 1.5f);
            ACMUtils.AddScreenShake(16f);

            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1.8f }, NPC.Center);
        }

        /// <summary>
        /// 全屏 screenTarget 扭曲 (GenericWarp · rift/void) — 占本帧唯一全屏后处理名额 (§C.4#2)。
        /// 第三幕虚空吞噬优先 (void + 强 uRadialPull 向心吸入)；否则次元裂隙门冲刺 (rift)。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            bool useVoid = voidWarp > 0.04f;
            float intensity = useVoid ? voidWarp : riftWarp;
            if (intensity <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 center = useVoid ? vortexCenter : (riftWarp > 0.04f ? gateEntrance : NPC.Center);
            Vector2 centerUV = (center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            if (useVoid) {
                // 虚空吞噬：强向心吸入 + 中心压暗成黑洞 + 色散（"被吸入"感）
                fx.Parameters["uRadius"]?.SetValue(0.75f);
                fx.Parameters["uWarpScale"]?.SetValue(1.5f);
                fx.Parameters["uChroma"]?.SetValue(0.8f);
                fx.Parameters["uRadialPull"]?.SetValue(0.9f);
                fx.Parameters["uMode"]?.SetValue(4f); // void
                fx.Parameters["uTint"]?.SetValue(new Vector4(AwakeningNetherHelper.VoidDarkPurple.ToVector3(), 0.55f));
            }
            else {
                // 次元裂隙门：中等向心吸入 + 色散裂空
                fx.Parameters["uRadius"]?.SetValue(0.55f);
                fx.Parameters["uWarpScale"]?.SetValue(1.3f);
                fx.Parameters["uChroma"]?.SetValue(0.7f);
                fx.Parameters["uRadialPull"]?.SetValue(0.6f);
                fx.Parameters["uMode"]?.SetValue(3f); // rift
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.5f));
            }

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
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

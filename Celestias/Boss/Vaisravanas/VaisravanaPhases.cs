using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 阶段枚举 + 一阶段（宝塔威光·步战教学）+ 阶段转换演出
    /// </summary>
    internal partial class Vaisravana
    {
        #region 阶段枚举

        public enum BossPhase
        {
            Intro,                      // 出场演出「天王降世」

            // 一阶段 · 宝塔威光：天王步战 + 宝塔充能教学
            Phase1_Hub,                 // 悬浮枢纽，固定可读轮替
            Phase1_KingSteps,           // 天王三步（架步→跨步→震山 ×3）
            Phase1_TowerVolley,         // 宝塔齐射（被赐福→延迟安全光束）
            Phase1_SweepingLight,       // 威光扫射（扫描线预告）
            Phase1_VajraPierce,         // 金刚破军（66f 长架招→静默→一击）
            Phase1_HeavenPillars,       // 天光垂落（波浪光柱阵）

            PhaseTransition_2,          // 一→二「显三面六臂法相」

            // 二阶段 · 天王降临：夜叉四方锚 + 镇压
            Phase2_Hub,                 // 降临枢纽
            Phase2_YakshaSummon,        // 召唤四方夜叉
            Phase2_QuadrantRay,         // 四象射线（夜叉锚定安全道）
            Phase2_ImmortalWave,        // 仙气地波（随地形起伏）
            Phase2_StampFormation,      // 天王踏阵（四步递进，末步震地大踏）
            Phase2_PagodaSuppress,      // 塔光柱镇压（双柱夹击）
            Phase2_GuardianStance,      // 宝伞格挡（守护反击窗口）

            PhaseTransition_3,          // 二→三「库藏开启」

            // 三阶段 · 库藏封印：脚本化 A/B/C 三幕轮替
            Phase3_SealRings,           // A 金环收束（标记安全道）
            Phase3_YakshaMirror,        // B 夜叉镜射（仅反射轴可躲）
            Phase3_UltimateTower,       // C 终极宝塔（70f 蓄力 + 坛城地纹）
            Phase3_SealBeat,            // 幕间宝伞节拍

            Death                       // 死亡演出「金身崩解」
        }

        #endregion

        #region 一阶段轮替状态

        private int p1Index;            // 一阶段攻击轮替索引
        private bool volleyBlessed;     // 本次齐射是否被赐福（安全变体）
        private float volleyCharge;     // 齐射蓄力 0~1（绘制层金线渐亮）
        private float sweepTelegraph;   // 扫射预告进度 0~1（绘制层扫描线）

        #endregion

        #region 一阶段AI · 枢纽

        private void RunPhase1Hub(Player target) {
            // 宝塔威光 - 神圣悬浮，保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -380);
            hoverPos.X += MathF.Sin(globalTime * 1.0f) * 60f;
            hoverPos.Y += MathF.Sin(globalTime * 1.5f) * 20f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.022f, 0.08f);

            towerOrbitSpeed = 0.012f;

            // 枢纽期间从带充能的宝塔点射，提供可读的低压力压制（避免空窗）
            if (AttackTimer % 42 == 0) {
                FireTowerTap(target);
            }

            // 早退出：就位即开打，不为自己的计时器空等
            bool inPosition = toHover.LengthSquared() < 140f * 140f;
            if (PhaseTimer > 80 || (PhaseTimer > 45 && inPosition)) {
                // 固定可读轮替：步战与弹幕交替，压强-呼吸波形；金刚破军押到每轮末尾
                BossPhase[] rotation = {
                    BossPhase.Phase1_KingSteps,
                    BossPhase.Phase1_TowerVolley,
                    BossPhase.Phase1_SweepingLight,
                    BossPhase.Phase1_KingSteps,
                    BossPhase.Phase1_HeavenPillars,
                    BossPhase.Phase1_TowerVolley,
                    BossPhase.Phase1_VajraPierce
                };
                BossPhase next = rotation[p1Index % rotation.Length];
                p1Index++;
                TransitionTo(next);
            }
        }

        /// <summary>枢纽点射：从一座带充能的宝塔射出单发可读慢弹（消耗该塔一点充能）。</summary>
        private void FireTowerTap(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int towerIndex = -1;
            for (int i = 0; i < TowerCount; i++) {
                if (towerCharges[i] > 0) { towerIndex = i; break; }
            }
            if (towerIndex < 0) return; // 充能被偷光 → 不再点射（奖励玩家窃取）

            ConsumeTowerCharge(towerIndex);
            Vector2 towerPos = GetTowerPosition(towerIndex);
            Vector2 toTarget = (target.Center - towerPos).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), towerPos, toTarget * 8f,
                ModContent.ProjectileType<TowerBeam>(), NPC.defDamage / 3, 1f, Main.myPlayer);

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.4f }, towerPos);
        }

        #endregion

        #region 一阶段AI · 天王三步

        /// <summary>
        /// 天王三步：沉腰长架步(首步 50f/后续 30f, 末端 pow8 急吸后坐) → 雷霆跨步(46px/f 仅 7 帧,
        /// 接触伤害仅此窗口) → 落地震山(×0.62/f 硬刹 + 贴地短程地波)。×3 循环。
        /// 公平阀门：落点越过玩家身位 300px；每步发射前固定 24f 金铁声预告。
        /// </summary>
        private void RunPhase1KingSteps(Player target) {
            // 状态机保底出口
            if (AttackTimer > 600) {
                TransitionTo(BossPhase.Phase1_Hub);
                return;
            }

            switch ((int)SubState) {
                case 0: { // 架步（沉腰蓄势）
                    int windup = dashCount == 0 ? 50 : 30;

                    if (PhaseTimer == 1) {
                        stepStart = NPC.Center;
                        PickStepTarget(target);
                    }

                    StepAnticipate(PhaseTimer / (float)windup);

                    // 固定 24f 预告 buffer：金铁声 + 落点坛城微阵（绘制层依 dashTarget 画）
                    if (PhaseTimer == windup - 24) {
                        SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.5f, Volume = 1.1f }, NPC.Center);
                    }

                    if (PhaseTimer >= windup) {
                        SubState = 1;
                        PhaseTimer = 0;
                        StepLaunch();
                    }
                    break;
                }

                case 1: { // 跨步（唯一接触伤害窗口）
                    NPC.damage = NPC.defDamage;

                    bool passed = Vector2.Dot(dashTarget - NPC.Center, stepDir) < 0f;
                    if (PhaseTimer >= stepTravelNeeded || passed) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 2: { // 落地震山（硬刹）
                    if (PhaseTimer == 1)
                        StepLandImpact(spawnShock: true, shockTravel: 620f);

                    NPC.velocity *= 0.62f;

                    if (PhaseTimer >= 22) {
                        dashCount++;
                        if (dashCount >= 3) {
                            SubState = 3;
                        }
                        else {
                            SubState = 0;
                        }
                        PhaseTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }

                case 3: { // 收招沉息
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer >= 28) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
                }
            }
        }

        #endregion

        #region 一阶段AI · 宝塔齐射

        /// <summary>
        /// 宝塔齐射：核心赐福机制示范。
        /// 蓄力期塔体后仰、塔→玩家金线渐亮（绘制层）；释放瞬间塔后座迸发。
        /// 未赐福 → 每座带充能宝塔向玩家喷出扇形光弹（消耗充能）。
        /// 被赐福 → 转化为长预告的延迟安全光束墙，朝玩家方向留出 34° 安全缝。
        /// </summary>
        private void RunPhase1TowerVolley(Player target) {
            switch ((int)SubState) {
                case 0: { // 蓄力 / 预告
                    NPC.velocity *= 0.9f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -360);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        volleyBlessed = ConsumeBlessing();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = volleyBlessed ? 0.1f : 0.6f }, NPC.Center);
                    }

                    int windup = volleyBlessed ? 55 : 40;
                    volleyCharge = MathHelper.Clamp(PhaseTimer / (float)windup, 0f, 1f);

                    if (PhaseTimer >= windup) {
                        SubState = 1;
                        PhaseTimer = 0;
                        volleyCharge = 0f;
                    }
                    break;
                }

                case 1: { // 释放
                    NPC.velocity *= 0.95f;

                    if (PhaseTimer == 1) {
                        if (volleyBlessed) FireBlessedSafeBeam(target);
                        else FireTowerOrbFans(target);
                    }

                    if (PhaseTimer > (volleyBlessed ? 70 : 48)) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
                }
            }
        }

        /// <summary>未赐福变体：每座带充能宝塔瞬发扇形光弹（消耗充能，塔身后座）。</summary>
        private void FireTowerOrbFans(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int fired = 0;
            for (int i = 0; i < TowerCount; i++) {
                if (towerCharges[i] <= 0) continue;
                ConsumeTowerCharge(i);
                fired++;

                Vector2 from = GetTowerPosition(i);
                Vector2 toTarget = (target.Center - from).SafeNormalize(Vector2.UnitY);
                float baseAngle = toTarget.ToRotation();
                int orbCount = Main.expertMode ? 5 : 3;
                float spread = MathHelper.ToRadians(28);
                for (int j = 0; j < orbCount; j++) {
                    float angle = baseAngle + spread * (j - (orbCount - 1) / 2f) / (orbCount - 1);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), from, angle.ToRotationVector2() * 11f,
                        ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 3, 2f, Main.myPlayer);
                }
            }

            if (fired == 0) {
                // 充能被偷光：完全无害（奖励满额窃取）
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.6f }, NPC.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, NPC.Center);
            }
        }

        /// <summary>被赐福变体：延迟安全光束墙，在玩家方向留安全缝。</summary>
        private void FireBlessedSafeBeam(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 在 360° 上发慢速光弹环，但留出朝向玩家当前位置的安全扇区
            float safeAngle = (target.Center - NPC.Center).ToRotation();
            int count = 24;
            float safeHalfWidth = MathHelper.ToRadians(34);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                float delta = MathHelper.WrapAngle(angle - safeAngle);
                if (MathF.Abs(delta) < safeHalfWidth) continue; // 安全缝
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 6.5f,
                    ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 4, 1f, Main.myPlayer);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.9f }, NPC.Center);
        }

        #endregion

        #region 一阶段AI · 威光扫射

        private void RunPhase1SweepingLight(Player target) {
            switch ((int)SubState) {
                case 0: // 架印预告（扫描线由绘制层依 sweepTelegraph 画出）
                    NPC.velocity *= 0.9f;

                    Vector2 sweepHoverPos = target.Center + new Vector2(0, -380);
                    NPC.Center = Vector2.Lerp(NPC.Center, sweepHoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        laserSweepDirection = Main.rand.NextBool() ? 1f : -1f;
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f }, NPC.Center);
                    }

                    sweepTelegraph = MathHelper.Clamp(PhaseTimer / 35f, 0f, 1f);

                    if (PhaseTimer >= 35) {
                        SubState = 1;
                        PhaseTimer = 0;
                        sweepTelegraph = 0f;
                    }
                    break;

                case 1: // 扫射（身体随扫向微倾）
                    float progress = MathHelper.Clamp(PhaseTimer / 72f, 0f, 1f);
                    float startAngle = laserSweepDirection > 0 ? -MathHelper.PiOver4 : MathHelper.PiOver4;
                    float endAngle = laserSweepDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                    float currentAngle = MathHelper.Lerp(startAngle, endAngle, progress) + MathHelper.PiOver2;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, (currentAngle - MathHelper.PiOver2) * 0.14f, 0.2f);

                    if (PhaseTimer % 6 == 0 && PhaseTimer <= 72) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                currentAngle.ToRotationVector2() * 20f,
                                ModContent.ProjectileType<SweepingLightBolt>(), NPC.defDamage / 2, 2f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.6f }, NPC.Center);
                        // 出弹口金闪
                        if (!VaultUtils.isServer) {
                            for (int k = 0; k < 3; k++) {
                                Dust d = Dust.NewDustPerfect(NPC.Center + currentAngle.ToRotationVector2() * 70f,
                                    DustID.GoldFlame, currentAngle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f), 100, default, 1.4f);
                                d.noGravity = true;
                            }
                        }
                    }

                    if (PhaseTimer > 90) {
                        NPC.rotation = 0f;
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
            }
        }

        #endregion

        #region 一阶段AI · 金刚破军

        /// <summary>
        /// 金刚破军（P1 大招）：蓄力 66f（后撤漂移 t²·160px + 汇聚金流·密度∝√t 且 72% 硬切 +
        /// 坛城逐圈点亮 + 震屏 t³ 渐强）→ 静默 12f（爆发前吸气）→ 金矛一击（预判 170px/f）+
        /// 本体反冲 22px/f。总预告 78f（处决级），路径 DrawBeam 预告后半段亮致命红芯。
        /// </summary>
        private void RunPhase1VajraPierce(Player target) {
            switch ((int)SubState) {
                case 0: { // 长架招蓄力 66f
                    const int chargeTime = 66;
                    if (PhaseTimer == 1) {
                        stepStart = NPC.Center;
                        stepDir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX); // 后撤方向
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                    }
                    // 多人客户端可能错过锚点初始化，位置保底防瞬移
                    if (stepStart == Vector2.Zero || Vector2.DistanceSquared(stepStart, NPC.Center) > 500f * 500f) {
                        stepStart = NPC.Center;
                        stepDir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX);
                    }

                    float chargeT = MathHelper.Clamp(PhaseTimer / (float)chargeTime, 0f, 1f);
                    chargeConverge = chargeT;

                    // 后撤漂移：身体离开自己的武器方向（drift-back）
                    NPC.Center = stepStart + stepDir * (chargeT * chargeT * 160f);
                    NPC.velocity = Vector2.Zero;

                    // 44f 前持续预判瞄准，之后锁死（给玩家确定的躲避线）
                    if (PhaseTimer < 44) {
                        Vector2 aim = ACMUtils.LeadTarget(NPC.Center, target.Center, target.velocity, StepSpeed * 3.7f);
                        laserAngle += MathHelper.WrapAngle(aim.ToRotation() - laserAngle) * 0.25f;
                    }

                    // 汇聚金流：密度∝√t，72% 处硬切（最后四分之一是安静的吸气）
                    if (!VaultUtils.isServer && chargeT < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(chargeT) * 0.75f) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 from = NPC.Center + ang.ToRotationVector2() * Main.rand.NextFloat(260f, 430f);
                        Dust d = Dust.NewDustPerfect(from, DustID.GoldFlame,
                            (NPC.Center - from) * 0.085f, 60, default, 2.0f);
                        d.noGravity = true;
                    }

                    // 震屏 t³ 渐强
                    if (!VaultUtils.isServer && PhaseTimer % 8 == 0)
                        ACMScreenShakeSystem.Add(chargeT * chargeT * chargeT * 6f);

                    if (PhaseTimer >= chargeTime) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 1: { // 静默 12f —— 一切汇聚骤停，只剩锁死的矛线
                    NPC.velocity = Vector2.Zero;

                    if (PhaseTimer >= 12) {
                        SubState = 2;
                        PhaseTimer = 0;

                        Vector2 dir = laserAngle.ToRotationVector2();
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 90f, dir * 42.5f,
                                ModContent.ProjectileType<VajraSpear>(), (int)(NPC.defDamage * 1.2f), 4f, Main.myPlayer);
                        }
                        // 出手：本体反冲 + 重锤听觉 + 金爆
                        NPC.velocity = -dir * 22f;
                        bodyFlash = 1f;
                        chargeConverge = 0f;
                        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 1.2f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            ACMScreenShakeSystem.Add(10f);
                            VaisravanaTreasureScreenSystem.PulseBloom(0.8f);
                        }
                    }
                    break;
                }

                case 2: { // 收招 40f
                    NPC.velocity *= 0.86f;
                    if (PhaseTimer >= 40) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
                }
            }
        }

        #endregion

        #region 一阶段AI · 天光垂落

        /// <summary>
        /// 天光垂落：玩家周围铺开一排天光柱（各带 40f 细线预告 → 26f 爆发），
        /// 波浪式从一侧推进——玩家沿波前方向移动即可安全，考验节奏而非反应。
        /// </summary>
        private void RunPhase1HeavenPillars(Player target) {
            switch ((int)SubState) {
                case 0: { // 举印召唤
                    NPC.velocity *= 0.9f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -400);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);
                        bodyFlash = 0.4f;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int count = Main.expertMode ? 9 : 7;
                            float sweepDir = Main.rand.NextBool() ? 1f : -1f;
                            for (int i = 0; i < count; i++) {
                                float xOff = (i - (count - 1) * 0.5f) * 175f + Main.rand.NextFloat(-22f, 22f);
                                int order = sweepDir > 0 ? i : count - 1 - i;
                                Vector2 spawn = new(target.Center.X + xOff, target.Center.Y);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, Vector2.Zero,
                                    ModContent.ProjectileType<VaisravanaLightPillar>(), NPC.defDamage / 2, 0f, Main.myPlayer,
                                    ai0: 0f, ai1: order * 7f);
                            }
                        }
                    }

                    if (PhaseTimer >= 30) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 1: { // 光柱阵运行期：缓慢横移凝视
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        new Vector2(MathF.Sin(globalTime * 0.9f) * 1.6f, MathF.Sin(globalTime * 1.3f) * 0.8f), 0.05f);

                    if (PhaseTimer > 150) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
                }
            }
        }

        /// <summary>沿方向画一条点状预告线（低成本通用 telegraph，保留给次要预警）。</summary>
        private void TelegraphLine(Vector2 from, Vector2 dir, int segments, int dustType) {
            for (int i = 0; i < segments; i++) {
                Vector2 pos = from + dir * (i * 80f);
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 150, default, 0.9f);
                Main.dust[dust].noGravity = true;
            }
        }

        #endregion

        #region 阶段转换演出

        /// <summary>
        /// 一→二「显三面六臂法相」120f：金光内收(吸气) → 法相双影+六臂光轮展开 →
        /// 12f 静默收缩 → 金环冲击波爆发（震屏 12 + 白闪 0.3）。i 帧 + 清弹。
        /// </summary>
        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 1) {
                for (int i = 0; i < TowerCount; i++) towerCharges[i] = MaxTowerCharge;
                pendingBlessing = 0;
                SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
            }

            towerOrbitSpeed = 0.03f + PhaseTimer * 0.0006f;

            // 0~30f: 金光向体内收拢（吸气）
            if (PhaseTimer <= 30 && !VaultUtils.isServer && PhaseTimer % 2 == 0) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(240, 240);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame,
                    (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 9f, 60, default, 2.0f);
                d.noGravity = true;
            }

            // 30~78f: 法相展开
            if (PhaseTimer > 30 && PhaseTimer <= 78) {
                dharmaAura = MathHelper.Clamp((PhaseTimer - 30f) / 48f, 0f, 1f);
                if (PhaseTimer == 40)
                    SoundEngine.PlaySound(SoundID.Item123 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
            }

            // 78~90f: 静默（法相定格，一切粒子停止）

            // 90f: 爆发
            if (PhaseTimer == 90) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(12f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.6f);
                    VaisravanaTreasureScreenSystem.PulseWhiteFlash(0.30f);
                    for (int i = 0; i < 32; i++) {
                        float ang = MathHelper.TwoPi * i / 32f;
                        Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GoldFlame,
                            ang.ToRotationVector2() * Main.rand.NextFloat(7f, 13f), 70, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 120) {
                towerOrbitSpeed = 0.02f;
                TransitionTo(BossPhase.Phase2_YakshaSummon);
            }
        }

        /// <summary>
        /// 二→三「库藏开启」110f：定身收光 → 35f 钟声 → 身后天库坛城之门展开（金雨上升）→
        /// 70f 怒吼 + 金幕拉满。i 帧 + 清弹。
        /// </summary>
        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true;

            towerOrbitSpeed = 0.07f + PhaseTimer * 0.0015f;
            if (PhaseTimer == 1) {
                for (int i = 0; i < TowerCount; i++) towerCharges[i] = MaxTowerCharge;
                pendingBlessing = 0;
                sealCycle = 0;
            }

            if (PhaseTimer == 35) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f }, NPC.Center);
            }

            // 35~70f: 天库之门（坛城由 PublishScreenState 发布）+ 金雨上升粒子
            if (PhaseTimer > 35 && !VaultUtils.isServer && PhaseTimer % 2 == 0) {
                Vector2 dustPos = NPC.Center + new Vector2(Main.rand.NextFloat(-320f, 320f), Main.rand.NextFloat(60f, 300f));
                Dust d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool(4) ? DustID.GoldCoin : DustID.GoldFlame,
                    new Vector2(0, -Main.rand.NextFloat(2f, 5f)), 90, default, 1.7f);
                d.noGravity = true;
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(12f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.8f);
                    VaisravanaTreasureScreenSystem.PulseWhiteFlash(0.25f);
                }
            }

            if (PhaseTimer > 110) {
                towerOrbitSpeed = 0.03f;
                TransitionTo(BossPhase.Phase3_SealRings);
            }
        }

        #endregion
    }
}

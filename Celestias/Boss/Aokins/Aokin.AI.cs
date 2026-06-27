using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            if (divebombCooldown > 0)
                divebombCooldown--;

            // 激活天空背景
            if (!VaultUtils.isServer && AokinSky.name != null) {
                if (!SkyManager.Instance[AokinSky.name].IsActive())
                    SkyManager.Instance.Activate(AokinSky.name, NPC.Center);
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    if (!VaultUtils.isServer && AokinSky.name != null)
                        SkyManager.Instance.Deactivate(AokinSky.name);
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 竞技场半宽随阶段向内收缩（封路龙卷读取）
            float arenaTarget = PhaseRegion == 3 ? 540f : (PhaseRegion == 2 ? 660f : 800f);
            ArenaHalfWidth = MathHelper.Lerp(ArenaHalfWidth, arenaTarget, 0.02f);

            // 阶段转换检测（改规则，非加速）
            CheckPhaseTransitions();

            // 余烬温度系统
            UpdateEmberHeat();

            // 视觉 / 身体
            UpdateVisualEffects();
            UpdateSegments();
            ApplySegmentContactDamage(target);

            attackTimer++;

            switch (CurrentState) {
                case MainState.Intro:
                    RunIntro(target);
                    break;
                case MainState.SummonBarriers:
                    RunSummonBarriers(target);
                    break;
                case MainState.Patrol:
                    RunPatrol(target);
                    break;
                case MainState.PreAttack:
                    RunPreAttack(target);
                    break;
                case MainState.Attacking:
                    RunAttacking(target);
                    break;
                case MainState.PhaseTransition2:
                    RunPhaseTransition2(target);
                    break;
                case MainState.PhaseTransition3:
                    RunPhaseTransition3(target);
                    break;
            }

            UpdateRotation();

            // 火焰光照（温度越高越亮）
            float lightMul = 1f + HeatRatio * 0.6f + (IsPhase3 ? 0.4f : 0f);
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.4f, 0.2f) * glowIntensity * lightMul);

            // V2 热浪屏幕演出（纯本地视觉）
            UpdateHeatScreenFx(target);
        }

        #endregion

        #region 状态切换辅助

        private void TransitionTo(MainState newState) {
            CurrentState = newState;
            attackTimer = 0;
            subState = 0;
            NPC.netUpdate = true;
        }

        private void EnterPatrol() {
            CurrentState = MainState.Patrol;
            attackTimer = 0;
            subState = 0;
            patrolTimer = 0;
            int min = IsPhase3 ? MinPatrolDuration - 25 : (IsPhase2 ? MinPatrolDuration - 15 : MinPatrolDuration);
            int max = IsPhase3 ? MaxPatrolDuration - 35 : (IsPhase2 ? MaxPatrolDuration - 20 : MaxPatrolDuration);
            patrolDuration = Main.rand.Next(Math.Max(50, min), Math.Max(70, max));
            NPC.netUpdate = true;
        }

        #endregion

        #region 余烬温度资源（对位敖顺 StormCharge）

        /// <summary>
        /// 余烬温度：火招累积，满温（过热）→ 下一招强制炼狱茧泄压并清空。
        /// 二/三阶段缓慢被动升温，把互不相关的火招串成一条"你把房间烧热了"的升温曲线。
        /// </summary>
        private void UpdateEmberHeat() {
            // 被动升温（阶段越高越快），炼狱茧期间不升温
            if (CurrentState == MainState.Attacking && CurrentAttack == AttackType.InfernoCocoon)
                return;
            float passive = PhaseRegion switch {
                3 => 0.10f,
                2 => 0.05f,
                _ => 0.02f
            };
            emberHeat = Math.Min(MaxEmberHeat, emberHeat + passive);
        }

        /// <summary>攻击命中节律时累积温度。</summary>
        private void AddHeat(float amount) {
            emberHeat = Math.Min(MaxEmberHeat, emberHeat + amount);
        }

        /// <summary>炼狱茧泄压：清空温度。</summary>
        private void VentHeat() {
            emberHeat = 0f;
        }

        #endregion

        #region 蛇形身体更新

        private void UpdateSegments() {
            if (Main.gamePaused) return;

            int gap = (int)(SegmentGap * NPC.scale);

            for (int i = 0; i < SegmentCount; i++) {
                Vector2 previousSegment;
                float previousRot;
                if (i == 0) {
                    previousSegment = NPC.Center;
                    previousRot = NPC.rotation;
                }
                else {
                    previousSegment = segmentPos[i - 1];
                    previousRot = segmentRot[i - 1];
                }

                Vector2 targetPos = previousSegment - previousRot.ToRotationVector2() * gap;
                segmentPos[i] += (targetPos - segmentPos[i]) * 0.3f;

                Vector2 diff = previousSegment - segmentPos[i];
                if (diff.LengthSquared() > 0.01f) {
                    segmentPos[i] = previousSegment - diff.SafeNormalize(Vector2.Zero) * gap;
                }

                segmentRot[i] = (previousSegment - segmentPos[i]).ToRotation();
            }
        }

        private void ApplySegmentContactDamage(Player target) {
            if (Main.netMode == NetmodeID.Server) return;
            if (NPC.dontTakeDamage && CurrentState == MainState.Attacking && CurrentAttack == AttackType.InfernoCocoon)
                return; // 泄压无敌帧期间躯体不额外造成接触伤害（茧火环负责）

            Rectangle playerBox = new Rectangle(
                (int)target.position.X, (int)target.position.Y,
                target.width, target.height);

            for (int i = 0; i < SegmentCount; i++) {
                Rectangle segBox = new Rectangle(
                    (int)segmentPos[i].X - 20, (int)segmentPos[i].Y - 20, 40, 40);

                if (playerBox.Intersects(segBox)) {
                    int direction = NPC.velocity.X > 0 ? 1 : -1;
                    target.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage / 2, direction);
                    break;
                }
            }
        }

        #endregion

        #region 辅助方法

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateVisualEffects() {
            flameRotation += 0.01f;
            float heat = HeatRatio;

            if (IsPhase2) {
                flameScale = 1.4f + MathF.Sin(globalTime * 3f) * 0.15f + heat * 0.2f;
                glowIntensity = 1.5f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.55f + heat * 0.25f, 0.04f);
            }
            else {
                flameScale = 1f + MathF.Sin(globalTime * 2f) * 0.08f + heat * 0.15f;
                glowIntensity = 1f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.3f + heat * 0.25f, 0.04f);
            }
        }

        #endregion

        #region V2 热浪屏幕演出 — 标量驱动

        /// <summary>
        /// 每帧平滑推进热浪屏幕标量并发布给 <see cref="AokinHeatScreenSystem"/>。
        /// 设计契约: 温度条常驻轻度 ElementalScreenTint(= HeatRatio); 仅炼狱茧泄压 / 相变 / 焚海劫的签名时刻
        /// 拉满昂贵的 GenericWarp(heat) 扭曲(走单一全屏后处理名额, 见 PostDraw)。红色只留给真正致命的火柱/熔岩(弹幕侧)。
        /// </summary>
        private void UpdateHeatScreenFx(Player target) {
            if (Main.dedServ)
                return;

            bool cocoon = CurrentState == MainState.Attacking && CurrentAttack == AttackType.InfernoCocoon;
            bool transition = CurrentState == MainState.PhaseTransition2 || CurrentState == MainState.PhaseTransition3;

            // —— ElementalScreenTint 热浪底色 = 温度条（恒定可读）——
            float tintTarget = 0.08f + HeatRatio * 0.42f;
            if (IsPhase3)
                tintTarget = Math.Max(tintTarget, 0.45f);
            tintTarget = Math.Max(tintTarget, lavaBloom * 0.8f);
            heatTint = MathHelper.Lerp(heatTint, tintTarget, 0.04f);

            // —— GenericWarp(heat) 全屏扭曲: 仅签名时刻 ——
            float warpTarget = 0f;
            if (transition)
                warpTarget = 0.5f;
            if (cocoon)
                warpTarget = Math.Max(warpTarget, 0.25f + MathHelper.Clamp(attackTimer / 120f, 0f, 1f) * 0.55f);
            if (IsPhase3)
                warpTarget = Math.Max(warpTarget, 0.15f + HeatRatio * 0.2f);
            warpTarget = Math.Max(warpTarget, lavaBloom * 0.7f);
            heatWarp = MathHelper.Lerp(heatWarp, warpTarget, warpTarget > heatWarp ? 0.08f : 0.04f);

            // —— 熔岩 / 泄压泛光逐帧衰减 ——
            if (lavaBloom > 0f)
                lavaBloom = Math.Max(0f, lavaBloom - 0.02f);

            // —— ArenaRunic 场地预警地纹: 炼狱茧蓄力期向心收口 / 焚海劫常驻 ——
            float runicTarget = 0f;
            if (cocoon)
                runicTarget = 0.3f + MathHelper.Clamp(attackTimer / 90f, 0f, 1f) * 0.55f;
            if (IsPhase3)
                runicTarget = Math.Max(runicTarget, 0.25f);
            runicTell = MathHelper.Lerp(runicTell, runicTarget, 0.07f);

            AokinHeatScreenSystem.Publish(NPC.Center, heatTint, runicTell, lavaBloom, ArenaHalfWidth, IsPhase3, globalTime);
        }

        #endregion

        #region 阶段转换检测

        private void CheckPhaseTransitions() {
            if (CurrentState == MainState.Intro || CurrentState == MainState.SummonBarriers)
                return;
            if (CurrentState == MainState.PhaseTransition2 || CurrentState == MainState.PhaseTransition3)
                return;

            if (!didPhase3Transition && PhaseRegion >= 3) {
                didPhase2Transition = true; // 跳血直入也补标记
                TransitionTo(MainState.PhaseTransition3);
                return;
            }
            if (!didPhase2Transition && PhaseRegion >= 2) {
                TransitionTo(MainState.PhaseTransition2);
            }
        }

        #endregion

        #region 攻击牌库（加权无重复）

        private static readonly (AttackType type, float weight)[] Phase1Deck = {
            (AttackType.FireBarrage, 1.0f),
            (AttackType.DragonBreath, 1.0f),
            (AttackType.EmberPillars, 1.2f),
            (AttackType.CoilDive, 1.0f),
        };

        private static readonly (AttackType type, float weight)[] Phase2Deck = {
            (AttackType.FireBarrage, 0.7f),
            (AttackType.DragonBreath, 0.6f),
            (AttackType.EmberPillars, 1.2f),
            (AttackType.CoilDive, 1.1f),
            (AttackType.FuryCharge, 1.0f),
            (AttackType.FlameVortex, 0.9f),
            (AttackType.InfernoBreath, 1.0f),
            (AttackType.Divebomb, 0.9f),
        };

        private static readonly (AttackType type, float weight)[] Phase3Deck = {
            (AttackType.EmberPillars, 1.2f),
            (AttackType.CoilDive, 1.1f),
            (AttackType.FuryCharge, 1.0f),
            (AttackType.InfernoBreath, 1.0f),
            (AttackType.MoltenSurge, 1.3f),
            (AttackType.Divebomb, 0.8f),
        };

        private void ChooseNextAttack() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 过热优先泄压（区 >= 2 才解锁炼狱茧）
            if (IsOverheated && PhaseRegion >= 2 && lastAttack != AttackType.InfernoCocoon) {
                BeginAttack(AttackType.InfernoCocoon);
                return;
            }

            var deck = PhaseRegion switch {
                3 => Phase3Deck,
                2 => Phase2Deck,
                _ => Phase1Deck
            };

            AttackType chosen = WeightedPick(deck);
            // 俯冲冷却未好则改选
            int guard = 0;
            while ((chosen == lastAttack || (chosen == AttackType.Divebomb && divebombCooldown > 0)) && guard < 12) {
                chosen = WeightedPick(deck);
                guard++;
            }

            BeginAttack(chosen);
        }

        private AttackType WeightedPick((AttackType type, float weight)[] deck) {
            float total = 0f;
            foreach (var e in deck) total += e.weight;
            float roll = (float)Main.rand.NextDouble() * total;
            foreach (var e in deck) {
                roll -= e.weight;
                if (roll <= 0f) return e.type;
            }
            return deck[0].type;
        }

        private void BeginAttack(AttackType type) {
            CurrentAttack = type;
            lastAttack = type;
            chargeCount = 0;
            coilAngle = 0f;
            coilRadius = 0f;
            CurrentState = MainState.PreAttack;
            attackTimer = 0;
            subState = 0;
            NPC.netUpdate = true;
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(attackTimer / 180f, 0f, 1f);

            Vector2 introOffset = new Vector2(0, 400) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -300) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            if (!VaultUtils.isServer && attackTimer % 2 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(180, 180) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, -2f, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            if (attackTimer == 60) {
                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }

            if (attackTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                lavaBloom = 0.7f;
                AokinHelper.CreateDragonFireBurst(NPC.Center, 200f, 3, 16);
            }

            if (attackTimer > 180) {
                TransitionTo(MainState.SummonBarriers);
            }
        }

        /// <summary>出场后召唤两侧火龙卷封路</summary>
        private void RunSummonBarriers(Player target) {
            NPC.velocity *= 0.9f;

            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            if (attackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                barrierTornadoIds = new int[2];

                int leftTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(-ArenaHalfWidth, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<AokinBarrierTornado>(),
                    NPC.damage / 4, 0f,
                    ai0: NPC.whoAmI, ai1: -1);
                barrierTornadoIds[0] = leftTornado;

                int rightTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(ArenaHalfWidth, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<AokinBarrierTornado>(),
                    NPC.damage / 4, 0f,
                    ai0: NPC.whoAmI, ai1: 1);
                barrierTornadoIds[1] = rightTornado;

                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
            }

            if (!VaultUtils.isServer && attackTimer > 30) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8 + attackTimer * 0.03f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (120 + MathF.Sin(attackTimer * 0.1f) * 30);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }

            if (attackTimer > 90) {
                EnterPatrol();
            }
        }

        #endregion

        #region 状态：巡游喘息 / 预告 / 攻击执行

        /// <summary>强制喘息：每招之间必经的巡游，给玩家恢复窗口（去"无缝刷弹"反模式）。</summary>
        private void RunPatrol(Player target) {
            float orbitSpeed = IsPhase2 ? 0.026f : 0.02f;
            float orbitRadius = 400f;

            NPC.localAI[1] += orbitSpeed;
            if (NPC.localAI[1] > MathHelper.TwoPi)
                NPC.localAI[1] -= MathHelper.TwoPi;

            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.5f - 200f);
            targetPos.Y += MathF.Sin(globalTime * 2f) * 30f;

            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.08f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                AokinHelper.CreateFireTrail(NPC.Center - NPC.velocity * 2f, NPC.velocity, 0.8f);
            }

            patrolTimer++;
            if (patrolTimer >= patrolDuration) {
                ChooseNextAttack();
            }
        }

        /// <summary>攻击前短预告（蓄力光效）。</summary>
        private void RunPreAttack(Player target) {
            NPC.velocity *= 0.92f;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.4f, Volume = 0.7f }, NPC.Center);
            }
            if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                AokinHelper.CreateFlameVortex(NPC.Center, 50f, 0.35f, 7);
            }

            if (attackTimer >= PreAttackDuration) {
                CurrentState = MainState.Attacking;
                attackTimer = 0;
                subState = 0;
                NPC.netUpdate = true;
            }
        }

        private void RunAttacking(Player target) {
            bool finished = CurrentAttack switch {
                AttackType.FireBarrage => RunFireBarrage(target),
                AttackType.DragonBreath => RunDragonBreath(target),
                AttackType.EmberPillars => RunEmberPillars(target),
                AttackType.CoilDive => RunCoilDive(target),
                AttackType.FuryCharge => RunFuryCharge(target),
                AttackType.FlameVortex => RunFlameVortex(target),
                AttackType.InfernoBreath => RunInfernoBreath(target),
                AttackType.Divebomb => RunDivebomb(target),
                AttackType.InfernoCocoon => RunInfernoCocoon(target),
                AttackType.MoltenSurge => RunMoltenSurge(target),
                _ => true
            };

            if (finished) {
                NPC.dontTakeDamage = false;
                EnterPatrol();
            }
        }

        #endregion

        #region 阶段转换：50% — 点燃封路龙卷向内收缩

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12 + attackTimer * 0.05f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (100 + attackTimer);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 8f;
                }
            }

            if (attackTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                lavaBloom = 0.9f;
                AokinHelper.CreateFlameVortex(NPC.Center, 300f, 2f, 60);
            }

            if (attackTimer > 120) {
                didPhase2Transition = true;
                NPC.dontTakeDamage = false;
                emberHeat = Math.Max(emberHeat, MaxEmberHeat * 0.4f);
                EnterPatrol();
            }
        }

        #endregion
    }
}

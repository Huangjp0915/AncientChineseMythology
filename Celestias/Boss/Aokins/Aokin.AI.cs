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
                if ((!target.active || target.dead) && CurrentState != MainState.DeathAnimation) {
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

            // 资源曲线：余烬温度（环境压迫） + 逆鳞怒气（行为压迫）
            UpdateEmberHeat();
            UpdateRage();

            // 本帧默认：接触伤害窗口关闭 / 朝向未锁定（由各状态显式开启）
            bodyContactWindow = false;
            rotationLocked = false;

            // 视觉 / 身体
            UpdateVisualEffects();
            UpdateSegments();

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
                case MainState.RageBurst:
                    RunRageBurst(target);
                    break;
                case MainState.DeathAnimation:
                    RunDeathAnimation(target);
                    break;
            }

            // 伤害窗口 = 视觉攻击窗口（巡游喘息期躯体无害, choreography §6 公平阀门）
            UpdateContactDamage();
            ApplySegmentContactDamage();

            UpdateRotation();

            // 火焰光照（温度越高越亮, 狂暴更烈）
            float lightMul = 1f + HeatRatio * 0.6f + (IsPhase3 ? 0.4f : 0f) + (rageActive ? 0.5f : 0f);
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.4f, 0.2f) * glowIntensity * lightMul);

            // 热浪屏幕演出（纯本地视觉）
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
            NPC.dontTakeDamage = false;
            int min = IsPhase3 ? MinPatrolDuration - 25 : (IsPhase2 ? MinPatrolDuration - 15 : MinPatrolDuration);
            int max = IsPhase3 ? MaxPatrolDuration - 35 : (IsPhase2 ? MaxPatrolDuration - 20 : MaxPatrolDuration);
            if (rageActive) {
                min -= 30;
                max -= 40;
            }
            patrolDuration = Main.rand.Next(Math.Max(45, min), Math.Max(65, max));
            NPC.netUpdate = true;
        }

        #endregion

        #region 余烬温度资源（环境压迫曲线）

        /// <summary>
        /// 余烬温度：火招累积，满温（过热）→ 下一招强制炼狱茧泄压并清空。
        /// 二/三阶段缓慢被动升温，把互不相关的火招串成一条"你把房间烧热了"的升温曲线。
        /// </summary>
        private void UpdateEmberHeat() {
            if (CurrentState == MainState.DeathAnimation)
                return;
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

        #region 逆鳞怒气（行为压迫曲线, P2+）

        /// <summary>
        /// 逆鳞怒气：P2 起受击积怒（服务器权威, 按掉血比例累积）。
        /// 满怒 → 巡游/预告期插入「逆鳞爆气」：清弹 + 无伤冲击（公平阀门）→ 6 秒狂暴。
        /// </summary>
        private void UpdateRage() {
            if (rageActive) {
                rageTimer--;
                if (rageTimer <= 0) {
                    rageActive = false;
                    rageCharge = 0f;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        NPC.netUpdate = true;
                }
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (PhaseRegion < 2 || rageActive || CurrentState == MainState.DeathAnimation) {
                lastLifeSeen = NPC.life;
                return;
            }

            if (lastLifeSeen <= 0)
                lastLifeSeen = NPC.life;
            if (NPC.life < lastLifeSeen) {
                float taken = lastLifeSeen - NPC.life;
                float factor = PhaseRegion >= 3 ? 18f : 14f;
                rageCharge = Math.Min(MaxRage, rageCharge + taken / NPC.lifeMax * 100f * factor);
            }
            lastLifeSeen = NPC.life;
        }

        /// <summary>怒气是否已满且可以插入爆气（仅巡游/预告期打断, 不打断签名演出）。</summary>
        private bool ShouldRageBurst =>
            rageCharge >= MaxRage && !rageActive && PhaseRegion >= 2 &&
            (CurrentState == MainState.Patrol || CurrentState == MainState.PreAttack);

        #endregion

        #region 蛇形身体更新（游动波）

        private void UpdateSegments() {
            if (Main.gamePaused) return;

            int gap = (int)(SegmentGap * NPC.scale);

            // 游动波参数：速度越快甩尾越大, 盘绕/蓄力期收紧（segmentWaveDamp）, 狂暴更暴烈
            float speed = NPC.velocity.Length();
            float waveTarget = CurrentState == MainState.Attacking && CurrentAttack == AttackType.CoilDive ? 0.3f : 1f;
            if (CurrentState == MainState.DeathAnimation && attackTimer > 140)
                waveTarget = 0.05f;
            segmentWaveDamp = MathHelper.Lerp(segmentWaveDamp, waveTarget, 0.05f);
            float waveAmp = (0.10f + Math.Min(speed * 0.011f, 0.20f)) * segmentWaveDamp * (rageActive ? 1.3f : 1f);
            float waveFreq = 3.2f + Math.Min(speed * 0.05f, 1.6f);

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

                // 颈部渐入游动波, 避免头颈错位
                float envelope = Math.Min(i / 4f, 1f);
                float wave = MathF.Sin(globalTime * waveFreq - i * 0.45f) * waveAmp * envelope;

                Vector2 targetPos = previousSegment - (previousRot + wave).ToRotationVector2() * gap;
                segmentPos[i] += (targetPos - segmentPos[i]) * 0.3f;

                Vector2 diff = previousSegment - segmentPos[i];
                if (diff.LengthSquared() > 0.01f) {
                    segmentPos[i] = previousSegment - diff.SafeNormalize(Vector2.Zero) * gap;
                }

                segmentRot[i] = (previousSegment - segmentPos[i]).ToRotation();
            }
        }

        /// <summary>
        /// 接触伤害基准捕获与窗口开关：NPC.damage 只在攻击性窗口（冲刺/盘绕/俯冲）非零,
        /// 巡游喘息、蓄力、演出期躯体与头部均无害（伤害窗口与视觉严格对齐）。
        /// </summary>
        private void UpdateContactDamage() {
            if (contactDamageBase <= 0 && NPC.damage > 0)
                contactDamageBase = NPC.damage;
            NPC.damage = bodyContactWindow ? contactDamageBase : 0;
        }

        /// <summary>
        /// 身体段接触伤害：仅本地玩家自判（多人安全）, 仅攻击性窗口生效。
        /// </summary>
        private void ApplySegmentContactDamage() {
            if (Main.dedServ || !bodyContactWindow || contactDamageBase <= 0)
                return;

            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.ghost || lp.immune)
                return;

            Rectangle playerBox = lp.Hitbox;
            for (int i = 0; i < SegmentCount; i++) {
                Rectangle segBox = new Rectangle(
                    (int)segmentPos[i].X - 20, (int)segmentPos[i].Y - 20, 40, 40);

                if (playerBox.Intersects(segBox)) {
                    int direction = lp.Center.X > segmentPos[i].X ? 1 : -1;
                    lp.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), contactDamageBase / 2, direction);
                    break;
                }
            }
        }

        #endregion

        #region 辅助方法

        private void UpdateRotation() {
            if (rotationLocked)
                return;
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateVisualEffects() {
            flameRotation += 0.01f + (rageActive ? 0.01f : 0f);
            float heat = HeatRatio;

            rageVisual = MathHelper.Lerp(rageVisual, rageActive ? 1f : 0f, rageActive ? 0.12f : 0.05f);

            if (IsPhase2) {
                flameScale = 1.4f + MathF.Sin(globalTime * 3f) * 0.15f + heat * 0.2f;
                glowIntensity = 1.5f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.55f + heat * 0.25f + rageVisual * 0.2f, 0.04f);
            }
            else {
                flameScale = 1f + MathF.Sin(globalTime * 2f) * 0.08f + heat * 0.15f;
                glowIntensity = 1f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.3f + heat * 0.25f, 0.04f);
            }

            // 预警强度自然衰减（各状态每帧主动抬升）
            chargeTelegraphT = Math.Max(0f, chargeTelegraphT - 0.08f);
            diveTelegraphT = Math.Max(0f, diveTelegraphT - 0.08f);
            breathGlow = Math.Max(0f, breathGlow - 0.05f);
        }

        /// <summary>清除本 Boss 的挥发性弹幕（火球/熔金/蒸汽/龙卷等）；includeFieldHazards 时连场地件一起清（死亡）。</summary>
        private void ClearAokinProjectiles(bool includeFieldHazards) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int fireball = ModContent.ProjectileType<AokinFireball>();
            int vortex = ModContent.ProjectileType<AokinFlameVortex>();
            int meteor = ModContent.ProjectileType<AokinMeteor>();
            int glob = ModContent.ProjectileType<AokinMoltenGlob>();
            int pool = ModContent.ProjectileType<AokinFlamePool>();
            int orb = ModContent.ProjectileType<AokinSteamOrb>();
            int shard = ModContent.ProjectileType<AokinSteamShard>();
            int breath = ModContent.ProjectileType<AokinBreathFlame>();
            int dance = ModContent.ProjectileType<AokinFireTornadoProj>();
            int pillar = ModContent.ProjectileType<AokinEmberPillar>();
            int fissure = ModContent.ProjectileType<AokinLavaFissure>();
            int geyser = ModContent.ProjectileType<AokinScaldGeyser>();
            int flood = ModContent.ProjectileType<AokinFlameFlood>();
            int ring = ModContent.ProjectileType<AokinInfernoRing>();

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (!p.hostile)
                    continue;
                int t = p.type;
                bool volatileProj = t == fireball || t == vortex || t == meteor || t == glob
                    || t == pool || t == orb || t == shard || t == breath || t == dance;
                bool fieldProj = t == pillar || t == fissure || t == ring || t == geyser || t == flood;
                if (volatileProj || (includeFieldHazards && fieldProj))
                    p.Kill();
            }
        }

        /// <summary>触发热浪蜃景 vent 冲击环（咆哮/泄压/爆气/死亡冲击的镜头级余波）。</summary>
        private void TriggerVent() {
            ventCenter = NPC.Center;
            ventProgress = 0.02f;
        }

        #endregion

        #region 热浪屏幕演出 — 标量驱动

        /// <summary>
        /// 每帧平滑推进热浪屏幕标量并发布给 <see cref="AokinHeatScreenSystem"/>。
        /// 设计契约: 温度条常驻轻度 ElementalScreenTint(= HeatRatio); 仅炼狱茧泄压 / 相变 / 焚海劫的签名时刻
        /// 拉满昂贵的 GenericWarp(heat) 扭曲(走单一全屏后处理名额, 见 PostDraw)。红色只留给真正致命的火柱/熔岩(弹幕侧),
        /// 逆鳞爆气红脉冲(rageFlash)与死亡白闪/压暗(deathWhite/deathDim)为演出专用通道。
        /// </summary>
        private void UpdateHeatScreenFx(Player target) {
            if (Main.dedServ)
                return;

            bool cocoon = CurrentState == MainState.Attacking && CurrentAttack == AttackType.InfernoCocoon;
            bool transition = CurrentState == MainState.PhaseTransition2 || CurrentState == MainState.PhaseTransition3;
            bool death = CurrentState == MainState.DeathAnimation;

            // —— ElementalScreenTint 热浪底色 = 温度条（恒定可读）——
            float tintTarget = 0.08f + HeatRatio * 0.42f;
            if (IsPhase3)
                tintTarget = Math.Max(tintTarget, 0.45f);
            tintTarget = Math.Max(tintTarget, lavaBloom * 0.8f);
            if (death)
                tintTarget = attackTimer < 90 ? 0.5f : 0.15f;
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
            warpTarget = Math.Max(warpTarget, rageFlash * 0.6f);
            if (death)
                warpTarget = attackTimer > 200 ? 0.9f : warpTarget * 0.5f;
            heatWarp = MathHelper.Lerp(heatWarp, warpTarget, warpTarget > heatWarp ? 0.08f : 0.04f);

            // —— 瞬时通道逐帧衰减 ——
            if (lavaBloom > 0f)
                lavaBloom = Math.Max(0f, lavaBloom - 0.02f);
            if (rageFlash > 0f)
                rageFlash = Math.Max(0f, rageFlash - (rageActive ? 0.008f : 0.03f));
            if (deathWhite > 0f)
                deathWhite = Math.Max(0f, deathWhite - 0.03f);
            if (!death)
                deathDim = Math.Max(0f, deathDim - 0.03f);

            // —— 热浪蜃景 vent 冲击环推进（触发后 ~50f 扫过全屏） ——
            if (ventProgress > 0f) {
                ventProgress += 0.021f;
                if (ventProgress >= 1f)
                    ventProgress = 0f;
            }

            // —— ArenaRunic 场地预警地纹: 炼狱茧蓄力期向心收口 / 焚海劫常驻 ——
            float runicTarget = 0f;
            if (cocoon)
                runicTarget = 0.3f + MathHelper.Clamp(attackTimer / 90f, 0f, 1f) * 0.55f;
            if (IsPhase3 && !death)
                runicTarget = Math.Max(runicTarget, 0.25f);
            runicTell = MathHelper.Lerp(runicTell, runicTarget, 0.07f);

            AokinHeatScreenSystem.Publish(NPC.Center, heatTint, runicTell, lavaBloom, ArenaHalfWidth,
                IsPhase3, globalTime, rageFlash, deathDim, deathWhite);
        }

        /// <summary>热浪蜃景专属全屏后处理的驱动参数（Drawing.PostDraw 读取）。</summary>
        internal (float warp, float ember, float vent, Vector2 ventCenter) HazeParams
            => (heatWarp, MathHelper.Clamp(0.25f + HeatRatio * 0.6f + rageVisual * 0.3f, 0f, 1f), ventProgress, ventCenter);

        #endregion

        #region 阶段转换检测

        private void CheckPhaseTransitions() {
            if (CurrentState == MainState.Intro || CurrentState == MainState.SummonBarriers)
                return;
            if (CurrentState == MainState.PhaseTransition2 || CurrentState == MainState.PhaseTransition3)
                return;
            if (CurrentState == MainState.DeathAnimation)
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
            (AttackType.DragonBreath, 1.1f),
            (AttackType.EmberPillars, 1.2f),
            (AttackType.CoilDive, 1.0f),
            (AttackType.MoltenRain, 1.1f),
        };

        private static readonly (AttackType type, float weight)[] Phase2Deck = {
            (AttackType.FireBarrage, 0.6f),
            (AttackType.DragonBreath, 1.0f),
            (AttackType.EmberPillars, 1.1f),
            (AttackType.CoilDive, 1.0f),
            (AttackType.FuryCharge, 1.2f),
            (AttackType.FlameVortex, 0.7f),
            (AttackType.SteamCannon, 1.0f),
            (AttackType.Divebomb, 0.9f),
            (AttackType.MoltenRain, 0.9f),
        };

        private static readonly (AttackType type, float weight)[] Phase3Deck = {
            (AttackType.EmberPillars, 0.9f),
            (AttackType.CoilDive, 1.0f),
            (AttackType.FuryCharge, 1.1f),
            (AttackType.DragonBreath, 0.8f),
            (AttackType.SteamCannon, 0.9f),
            (AttackType.MoltenSurge, 1.3f),
            (AttackType.Divebomb, 0.8f),
            (AttackType.MoltenRain, 0.9f),
            (AttackType.FlameFlood, 1.2f),
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

        #region 出场演出 —「南海沸腾」

        /// <summary>
        /// 入场脚本：水下蓄势(赤光渐涨) → 破空跃出(单帧 55px/f + 硬刹) → 静止瞪视(70 帧, 龙眼渐亮)
        /// → 咆哮(震屏 + 龙焰环爆 + 封路龙卷落位) → 开战。
        /// menace is mostly stillness — 压迫感主要来自跃出后的静止凝视。
        /// </summary>
        private void RunIntro(Player target) {
            NPC.dontTakeDamage = attackTimer < IntroRoarFrame;

            if (attackTimer < IntroLeapFrame) {
                // —— 水下蓄势：本体潜伏于玩家下方, 海面赤光与上升余烬渐强 ——
                NPC.Center = target.Center + new Vector2(0, 620);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;

                float t = attackTimer / (float)IntroLeapFrame;
                lavaBloom = Math.Max(lavaBloom, t * 0.45f);
                if (attackTimer == 1)
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.6f, Volume = 1.2f }, target.Center);
                if (attackTimer % 14 == 0)
                    ACMUtils.AddScreenShake(t * 3f);

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 pos = target.Center + new Vector2(Main.rand.NextFloat(-520f, 520f), Main.rand.NextFloat(340f, 560f));
                        var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare);
                        d.noGravity = true;
                        d.scale = 1.2f + t * 1.6f;
                        d.velocity = new Vector2(0, -Main.rand.NextFloat(2f, 5f + t * 5f));
                        d.alpha = 100;
                    }
                }
                return;
            }

            if (attackTimer == IntroLeapFrame) {
                // —— 破空跃出：单帧 set（launch is a set, not a ramp）——
                NPC.Opacity = 1f;
                NPC.Center = target.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 620);
                NPC.velocity = new Vector2(0, -55f);
                subState = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.4f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
                if (!VaultUtils.isServer) {
                    AokinHelper.CreateSteamBurst(NPC.Center, 160f, 40);
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 180f, 3, 14);
                }
                // 身体段归位到跃出点下方, 形成破水长龙
                for (int i = 0; i < SegmentCount; i++)
                    segmentPos[i] = NPC.Center + new Vector2(0, SegmentGap * (i + 1));
            }

            if (attackTimer > IntroLeapFrame && attackTimer < IntroRoarFrame) {
                if (subState == 0) {
                    // 冲天段：越过悬停高度即硬刹（×0.62/f = slam into position）
                    if (NPC.Center.Y <= target.Center.Y - 300f || attackTimer > IntroLeapFrame + 22) {
                        subState = 1;
                    }
                    if (!VaultUtils.isServer)
                        AokinHelper.CreateFireTrail(NPC.Center, NPC.velocity, 1.5f);
                }
                else {
                    NPC.velocity *= 0.62f;
                    // 静止瞪视：只余呼吸浮动与龙眼渐亮
                    NPC.velocity.Y += MathF.Sin(globalTime * 2.2f) * 0.05f;
                    introEyeGlow = MathHelper.Clamp((attackTimer - 75f) / 55f, 0f, 1f);
                    rotationLocked = true;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, target.Center.X >= NPC.Center.X ? 0f : MathHelper.Pi, 0.08f);
                    NPC.spriteDirection = target.Center.X >= NPC.Center.X ? 1 : -1;
                }

                // 瞪视中段：封路龙卷自两翼落位
                if (attackTimer == 80)
                    EnsureBarrierTornadoes(target);
            }

            if (attackTimer == IntroRoarFrame) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.15f, Volume = 1.6f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                lavaBloom = 0.85f;
                emberHeat = Math.Max(emberHeat, 8f);
                TriggerVent();
                if (!VaultUtils.isServer)
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 260f, 4, 18);
            }

            if (attackTimer > IntroEndFrame) {
                introEyeGlow = 0f;
                EnterPatrol();
            }
        }

        /// <summary>召唤两侧封路龙卷（幂等）。</summary>
        private void EnsureBarrierTornadoes(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (barrierTornadoIds != null)
                return;

            barrierTornadoIds = new int[2];
            for (int side = 0; side < 2; side++) {
                int dir = side == 0 ? -1 : 1;
                barrierTornadoIds[side] = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(dir * ArenaHalfWidth, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<AokinBarrierTornado>(),
                    Math.Max(contactDamageBase, NPC.defDamage) / 4, 0f,
                    ai0: NPC.whoAmI, ai1: dir);
            }
            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
            ACMUtils.AddScreenShake(6f);
        }

        /// <summary>保底出口：V3 入场已自带龙卷, 此状态仅兜底旧网络状态直接回巡游。</summary>
        private void RunSummonBarriers(Player target) {
            NPC.velocity *= 0.9f;
            if (attackTimer == 1)
                EnsureBarrierTornadoes(target);
            if (attackTimer > 10)
                EnterPatrol();
        }

        #endregion

        #region 状态：巡游喘息 / 预告 / 攻击执行

        /// <summary>强制喘息：每招之间必经的巡游，给玩家恢复窗口（去"无缝刷弹"反模式）。躯体此期无害。</summary>
        private void RunPatrol(Player target) {
            if (ShouldRageBurst) {
                TransitionTo(MainState.RageBurst);
                return;
            }

            float orbitSpeed = (IsPhase2 ? 0.026f : 0.02f) * (rageActive ? 1.3f : 1f);
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

        /// <summary>攻击前短预告（蓄力光效）。狂暴期缩短。</summary>
        private void RunPreAttack(Player target) {
            if (ShouldRageBurst) {
                TransitionTo(MainState.RageBurst);
                return;
            }

            NPC.velocity *= 0.92f;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.4f, Volume = 0.7f }, NPC.Center);
            }
            if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                AokinHelper.CreateFlameVortex(NPC.Center, 50f, 0.35f, 7);
            }

            int duration = rageActive ? PreAttackDuration - 8 : PreAttackDuration;
            if (attackTimer >= duration) {
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
                AttackType.InfernoBreath => RunDragonBreath(target), // 旧枚举映射到重做后的赤炎龙息
                AttackType.Divebomb => RunDivebomb(target),
                AttackType.InfernoCocoon => RunInfernoCocoon(target),
                AttackType.MoltenSurge => RunMoltenSurge(target),
                AttackType.MoltenRain => RunMoltenRain(target),
                AttackType.SteamCannon => RunSteamCannon(target),
                AttackType.FlameFlood => RunFlameFlood(target),
                _ => true
            };

            if (finished) {
                NPC.dontTakeDamage = false;
                EnterPatrol();
            }
        }

        #endregion

        #region 阶段转换：50% —「沸海蒸腾」（清弹 + 蒸汽茧爆发）

        /// <summary>
        /// P2 相变：清弹（公平阀门）→ 盘身聚气（向心蒸汽, sqrt 密度, 72% 截止）→ 静默收缩 →
        /// 蒸汽爆发（无伤冲击环 + 全屏扭曲 + 封路龙卷点燃）→ 舒展入战。
        /// </summary>
        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -320);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.1f);

            if (attackTimer == 1) {
                ClearAokinProjectiles(includeFieldHazards: false);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
            }

            if (attackTimer < 40) {
                // 聚气：向心蒸汽, 密度 ∝ sqrt(t), 螺旋收拢
                float t = attackTimer / 40f;
                if (!VaultUtils.isServer)
                    AokinHelper.CreateConvergingEmbers(NPC.Center, MathF.Sqrt(t) * 0.9f, 300f, 1.2f);
                ACMUtils.AddScreenShake(t * t * 3f);
            }
            else if (attackTimer < 70) {
                // 静默收缩：粒子全停, 光环回收（爆前的吸气）
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.05f, 0.15f);
            }
            else if (attackTimer == 70) {
                // 蒸汽爆发
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(11f);
                lavaBloom = 0.95f;
                heatWarp = Math.Max(heatWarp, 0.85f);
                TriggerVent();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<AokinShockwave>(), 0, 0f, Main.myPlayer, ai0: 1f);
                }
                if (!VaultUtils.isServer) {
                    AokinHelper.CreateSteamBurst(NPC.Center, 240f, 60);
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 320f, 4, 20);
                }
            }

            if (attackTimer > 110) {
                didPhase2Transition = true;
                NPC.dontTakeDamage = false;
                emberHeat = Math.Max(emberHeat, MaxEmberHeat * 0.4f);
                EnterPatrol();
            }
        }

        #endregion

        #region 逆鳞爆气 — 满怒清弹 + 狂暴

        /// <summary>
        /// 逆鳞爆气：45f 聚气（红脉冲渐强 + 低鸣）→ 爆气 1 帧（清挥发弹幕 + 无伤推离波 = 送玩家喘息）
        /// → 6 秒狂暴（攻速提升, 熔鳞泛白）。正反馈的公平阀门。
        /// </summary>
        private void RunRageBurst(Player target) {
            NPC.velocity *= 0.9f;
            rotationLocked = true;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, target.Center.X >= NPC.Center.X ? 0f : MathHelper.Pi, 0.06f);

            if (attackTimer == 1)
                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);

            if (attackTimer < 45) {
                float t = attackTimer / 45f;
                rageFlash = Math.Max(rageFlash, t * 0.75f);
                // 聚气 80% 截止 → 爆前静默
                if (!VaultUtils.isServer && t < 0.8f)
                    AokinHelper.CreateConvergingEmbers(NPC.Center, t, 260f, 1.4f);
                if (t > 0.5f)
                    ACMUtils.AddScreenShake((t - 0.5f) * 6f);
            }

            if (attackTimer == 45) {
                ClearAokinProjectiles(includeFieldHazards: false);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<AokinShockwave>(), 0, 0f, Main.myPlayer, ai0: 0f);
                }
                rageActive = true;
                rageTimer = RageDuration;
                rageFlash = 1f;
                lavaBloom = Math.Max(lavaBloom, 0.8f);
                TriggerVent();
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                if (!VaultUtils.isServer)
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 300f, 4, 22);
                NPC.netUpdate = true;
            }

            if (attackTimer > 65)
                EnterPatrol();
        }

        #endregion

        #region 死亡演出 —「逆鳞崩解」

        /// <summary>
        /// 死亡演出（CheckDead 接管, ~230f）：
        /// 失控乱飞 + 尾→头逐段爆裂(0~90) → 残躯冲天(90~140) → 空中寂静(140~205, 屏幕压暗只余龙眼)
        /// → 金红新星(205, 全场唯一 shake 16 + 白闪 + 清场) → 真死亡(230, 掉落照常)。
        /// </summary>
        private void RunDeathAnimation(Player target) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            DeathProgress = MathHelper.Clamp(attackTimer / (float)DeathDuration, 0f, 1f);

            if (attackTimer == 1) {
                ClearAokinProjectiles(includeFieldHazards: true);
                SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.3f }, NPC.Center);
                NPC.velocity *= 0.5f;
            }

            if (attackTimer < 90) {
                // —— 失控乱飞 + 逐段爆裂（尾→头, 每 3f 一段）——
                Vector2 flail = new Vector2(
                    MathF.Sin(attackTimer * 0.23f) * 9f,
                    MathF.Cos(attackTimer * 0.31f) * 7f - 1.5f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, flail, 0.12f);

                int shouldBurn = Math.Min(SegmentCount, attackTimer / 3);
                while (deathBurntSegments < shouldBurn) {
                    int segIdx = SegmentCount - 1 - deathBurntSegments;
                    if (segIdx >= 0 && !VaultUtils.isServer) {
                        AokinHelper.CreateDragonFireBurst(segmentPos[segIdx], 60f, 2, 8);
                        var d = Dust.NewDustPerfect(segmentPos[segIdx], DustID.Smoke, new Vector2(0, -2f), 120, default, 2.5f);
                        d.noGravity = true;
                    }
                    if (deathBurntSegments % 3 == 0) {
                        SoundEngine.PlaySound(SoundID.Item14 with {
                            Pitch = -0.4f + deathBurntSegments / (float)SegmentCount * 0.8f,
                            Volume = 0.8f
                        }, segmentPos[Math.Max(0, segIdx)]);
                        ACMUtils.AddScreenShake(4f);
                    }
                    deathBurntSegments++;
                }
            }
            else if (attackTimer == 90) {
                // —— 残躯冲天：单帧 set ——
                NPC.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -24f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.4f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
                NPC.netUpdate = true;
            }
            else if (attackTimer < 140) {
                if (attackTimer > 110)
                    NPC.velocity *= 0.94f;
                if (!VaultUtils.isServer && attackTimer % 3 == 0)
                    AokinHelper.CreateFireTrail(NPC.Center, NPC.velocity, 1.3f);
            }
            else if (attackTimer < 205) {
                // —— 空中寂静：粒子全停, 屏幕压暗, 只余龙眼 ——
                NPC.velocity *= 0.86f;
                deathDim = MathHelper.Clamp((attackTimer - 140f) / 65f, 0f, 1f) * 0.65f;
                introEyeGlow = MathHelper.Clamp((attackTimer - 150f) / 40f, 0f, 1f);
            }
            else if (attackTimer == 205) {
                // —— 金红新星：全场唯一的大震 + 白闪 ——
                deathWhite = 1f;
                deathDim = 0f;
                lavaBloom = 1f;
                TriggerVent();
                ACMUtils.AddScreenShake(16f);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 420f, 5, 26);
                    AokinHelper.CreateSteamBurst(NPC.Center, 300f, 50);
                }
            }
            else {
                NPC.velocity *= 0.9f;
            }

            if (attackTimer >= DeathDuration && Main.netMode != NetmodeID.MultiplayerClient) {
                deathAnimationDone = true;
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
                NPC.netUpdate = true;
            }
        }

        #endregion
    }
}

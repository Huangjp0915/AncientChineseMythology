using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    internal partial class Aoyuan
    {
        #region AI主循环 — 状态机架构

        public override bool PreAI() {
            globalTime += 1f / 60f;

            // 激活天空背景
            if (!VaultUtils.isServer && AoyuanSky.name != null) {
                if (!SkyManager.Instance[AoyuanSky.name].IsActive())
                    SkyManager.Instance.Activate(AoyuanSky.name, NPC.Center);
            }

            // 目标与脱战
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (!player.active || player.dead) {
                despawn = true;
            }
            if (despawn) {
                if (!VaultUtils.isServer && AoyuanSky.name != null)
                    SkyManager.Instance.Deactivate(AoyuanSky.name);
                NPC.velocity.Y -= 0.4f;
                NPC.ai[3]++;
                if (NPC.ai[3] >= 300)
                    NPC.active = false;
                return false;
            }

            // 蠕虫身体链生成（首次）
            SpawnWormBody();

            // 出生渐显粒子
            if (NPC.alpha > 0) {
                if (!VaultUtils.isServer) {
                    for (int spawnDust = 0; spawnDust < 2; spawnDust++) {
                        int d = Dust.NewDust(NPC.position, NPC.width, NPC.height,
                            DustID.IceTorch, 0f, 0f, 100, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }
                NPC.alpha -= 12;
                if (NPC.alpha < 0) NPC.alpha = 0;
            }

            // 张嘴动画帧推进
            UpdateMouthAnimation();

            // === 阶段转换检查 ===
            if (IsPhase2 && !didPhase2Transition && CurrentState != AoyuanState.PhaseTransition) {
                TransitionToPhase2();
            }

            // === 永冻地痕：巡游/冷却中铺设 ===
            EmitPermafrostTrail();

            // === 状态机主循环 ===
            switch (CurrentState) {
                case AoyuanState.Intro:
                    RunIntro(player);
                    break;
                case AoyuanState.Patrol:
                    RunPatrol(player);
                    break;
                case AoyuanState.PreAttack:
                    RunPreAttack(player);
                    break;
                case AoyuanState.Attacking:
                    RunAttacking(player);
                    break;
                case AoyuanState.Cooldown:
                    RunCooldown(player);
                    break;
                case AoyuanState.PhaseTransition:
                    RunPhaseTransition(player);
                    break;
            }

            // 蠕虫朝向与旋转
            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;
            NPC.spriteDirection = NPC.velocity.X > 0 ? -1 : 1;

            // 冰霜光照（二阶段更亮）
            float lightMul = IsPhase2 ? 1.5f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.9f) * glowIntensity * lightMul);

            // V2 霜冻屏幕演出（纯本地视觉）
            UpdateFrostScreenFx(player);

            if ((NPC.velocity.X > 0 && NPC.oldVelocity.X < 0 || NPC.velocity.X < 0 && NPC.oldVelocity.X > 0 ||
                 NPC.velocity.Y > 0 && NPC.oldVelocity.Y < 0 || NPC.velocity.Y < 0 && NPC.oldVelocity.Y > 0) && !NPC.justHit)
                NPC.netUpdate = true;

            return false;
        }

        #endregion

        #region V2 霜冻屏幕演出 — 标量驱动（着色器验证层）

        /// <summary>
        /// 每帧平滑推进霜冻屏幕演出标量, 并发布给 <see cref="AoyuanFrostScreenSystem"/>。
        /// 设计契约: 二阶段常驻轻度 ElementalScreenTint 氛围; 仅在绝对零度蓄力→放射冻结 / 浮空破境的
        /// 签名时刻拉满昂贵的 GenericWarp 扭曲(走单一全屏后处理名额)。红色只留给真正致命的完整冻结(见弹幕侧)。
        /// </summary>
        private void UpdateFrostScreenFx(Player player) {
            if (Main.dedServ)
                return;

            bool azActive = CurrentState == AoyuanState.Attacking
                && (AoyuanAttackType)(int)NPC.ai[2] == AoyuanAttackType.AbsoluteZero;
            // WeakPointsExposed 仅在绝对零度吸气蓄力期间为真
            bool azCharging = azActive && WeakPointsExposed;
            float azProgress = azCharging ? MathHelper.Clamp(attackTimer / 180f, 0f, 1f) : 0f;

            // 蓄力末段渐增震屏(处决级预警, 取 max 不累加)
            if (azCharging && azProgress > 0.6f)
                ACMUtils.AddScreenShake((azProgress - 0.6f) / 0.4f * 5f);

            // —— ElementalScreenTint 氛围底色: 二阶段常驻, 蓄力/冻爆加浓 ——
            float tintTarget = IsPhase2 ? 0.5f : 0.08f;
            if (azCharging)
                tintTarget = Math.Max(tintTarget, 0.35f + azProgress * 0.45f);
            tintTarget = Math.Max(tintTarget, freezeBloom * 0.9f);
            frostTint = MathHelper.Lerp(frostTint, tintTarget, 0.04f);

            // —— GenericWarp(frost) 全屏扭曲: 仅签名时刻 ——
            float warpTarget = 0f;
            if (CurrentState == AoyuanState.PhaseTransition)
                warpTarget = 0.45f;
            if (azCharging)
                warpTarget = Math.Max(warpTarget, 0.2f + azProgress * 0.6f);
            warpTarget = Math.Max(warpTarget, freezeBloom * 0.85f);
            frostWarp = MathHelper.Lerp(frostWarp, warpTarget, warpTarget > frostWarp ? 0.08f : 0.04f);

            // —— 冻爆泛光逐帧衰减 ——
            if (freezeBloom > 0f)
                freezeBloom = Math.Max(0f, freezeBloom - 0.025f);

            // —— ArenaRunic 霜冻法阵地纹: 蓄力期向心收口预警 ——
            float runicTarget = azCharging ? 0.35f + azProgress * 0.5f : 0f;
            arenaRunic = MathHelper.Lerp(arenaRunic, runicTarget, 0.07f);

            AoyuanFrostScreenSystem.Publish(NPC.Center, frostTint, freezeBloom, arenaRunic, globalTime);
        }

        #endregion

        #region 张嘴动画

        private void UpdateMouthAnimation() {
            if (fireAttack) {
                attackCounter++;
                if (attackCounter > 8) {
                    attackFrame++;
                    attackCounter = 0;
                }
                if (attackFrame >= HeadFrameCount)
                    attackFrame = HeadFrameCount - 1;
            }
            else {
                attackFrame = 0;
                attackCounter = 0;
            }
        }

        private void OpenMouth() {
            fireAttack = true;
            attackFrame = 0;
            attackCounter = 0;
        }

        private void CloseMouth() {
            fireAttack = false;
            attackFrame = 0;
            attackCounter = 0;
        }

        #endregion

        #region 永冻地痕 — 签名机制

        /// <summary>
        /// 巡游/冷却/转移期间周期性在身后铺设寒冰地痕，玩家站在地痕上叠加冰冻
        /// 二阶段地痕额外令地面打滑
        /// </summary>
        private void EmitPermafrostTrail() {
            if (CurrentState == AoyuanState.PreAttack)
                return;
            if (NPC.velocity.Length() < 2f)
                return;

            trailTimer++;
            int interval = IsPhase2 ? 7 : 10;
            if (trailTimer < interval)
                return;
            trailTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<AoyuanPermafrostTrail>(),
                    0, 0f, Main.myPlayer,
                    ai0: IsPhase2 ? 1f : 0f);
            }
        }

        #endregion

        #region 蠕虫身体生成

        private void SpawnWormBody() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (NPC.ai[0] != 0) return;

            NPC.realLife = NPC.whoAmI;
            int latestNPC = NPC.whoAmI;
            for (int i = 0; i < BodyFrameSequence.Length; ++i) {
                latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(),
                    (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<AoyuanBody>(), NPC.whoAmI, 0, latestNPC);
                Main.npc[latestNPC].realLife = NPC.whoAmI;
                Main.npc[latestNPC].ai[3] = NPC.whoAmI;
                Main.npc[latestNPC].ai[2] = BodyFrameSequence[i];
                Main.npc[latestNPC].netUpdate = true;
            }
            NPC.ai[0] = 1;
            NPC.netUpdate = true;
        }

        #endregion

        #region 蠕虫移动

        /// <summary>
        /// 标准蠕虫移动 - 平滑追踪玩家（沿用原版蠕虫栅格追踪）
        /// </summary>
        private void WormMovement(Player player, float speed, float acceleration) {
            Vector2 npcCenter = new Vector2(NPC.position.X + NPC.width * 0.5f, NPC.position.Y + NPC.height * 0.5f);
            float targetXPos = player.Center.X;
            float targetYPos = player.Center.Y;

            float targetRoundedPosX = (int)(targetXPos / 16.0) * 16;
            float targetRoundedPosY = (int)(targetYPos / 16.0) * 16;
            npcCenter.X = (int)(npcCenter.X / 16.0) * 16;
            npcCenter.Y = (int)(npcCenter.Y / 16.0) * 16;
            float dirX = targetRoundedPosX - npcCenter.X;
            float dirY = targetRoundedPosY - npcCenter.Y;

            float length = (float)Math.Sqrt(dirX * dirX + dirY * dirY);

            if (NPC.soundDelay == 0) {
                float num1 = length / 40f;
                if (num1 < 10.0) num1 = 10f;
                if (num1 > 20.0) num1 = 20f;
                NPC.soundDelay = (int)num1;
            }

            float absDirX = Math.Abs(dirX);
            float absDirY = Math.Abs(dirY);
            if (length == 0f) length = 1f;
            float newSpeed = speed / length;
            dirX *= newSpeed;
            dirY *= newSpeed;

            if ((NPC.velocity.X > 0.0 && dirX > 0.0) || (NPC.velocity.X < 0.0 && dirX < 0.0) ||
                (NPC.velocity.Y > 0.0 && dirY > 0.0) || (NPC.velocity.Y < 0.0 && dirY < 0.0)) {
                if (NPC.velocity.X < dirX) NPC.velocity.X += acceleration;
                else if (NPC.velocity.X > dirX) NPC.velocity.X -= acceleration;
                if (NPC.velocity.Y < dirY) NPC.velocity.Y += acceleration;
                else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= acceleration;

                if (Math.Abs(dirY) < speed * 0.2 && ((NPC.velocity.X > 0.0 && dirX < 0.0) || (NPC.velocity.X < 0.0 && dirX > 0.0))) {
                    if (NPC.velocity.Y > 0.0) NPC.velocity.Y += acceleration * 2f;
                    else NPC.velocity.Y -= acceleration * 2f;
                }
                if (Math.Abs(dirX) < speed * 0.2 && ((NPC.velocity.Y > 0.0 && dirY < 0.0) || (NPC.velocity.Y < 0.0 && dirY > 0.0))) {
                    if (NPC.velocity.X > 0.0) NPC.velocity.X += acceleration * 2f;
                    else NPC.velocity.X -= acceleration * 2f;
                }
            }
            else if (absDirX > absDirY) {
                if (NPC.velocity.X < dirX) NPC.velocity.X += acceleration * 1.1f;
                else if (NPC.velocity.X > dirX) NPC.velocity.X -= acceleration * 1.1f;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5) {
                    if (NPC.velocity.Y > 0.0) NPC.velocity.Y += acceleration;
                    else NPC.velocity.Y -= acceleration;
                }
            }
            else {
                if (NPC.velocity.Y < dirY) NPC.velocity.Y += acceleration * 1.1f;
                else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= acceleration * 1.1f;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5) {
                    if (NPC.velocity.X > 0.0) NPC.velocity.X += acceleration;
                    else NPC.velocity.X -= acceleration;
                }
            }
        }

        /// <summary>
        /// 向指定点平滑移动（用于盘旋/俯冲等特殊机动）
        /// </summary>
        private void WormMoveTo(Vector2 target, float speed, float accel) {
            Vector2 dir = target - NPC.Center;
            float length = dir.Length();
            if (length < 8f) return;
            dir = dir.SafeNormalize(Vector2.UnitY) * speed;
            if (NPC.velocity.X < dir.X) NPC.velocity.X += accel;
            else if (NPC.velocity.X > dir.X) NPC.velocity.X -= accel;
            if (NPC.velocity.Y < dir.Y) NPC.velocity.Y += accel;
            else if (NPC.velocity.Y > dir.Y) NPC.velocity.Y -= accel;
        }

        #endregion

        #region 状态：出场

        private void RunIntro(Player player) {
            attackTimer++;
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, speed, 0.16f);

            if (attackTimer >= 60) {
                CurrentState = AoyuanState.Patrol;
                patrolTimer = 0;
                patrolDuration = Main.rand.Next(MinPatrolDuration, MaxPatrolDuration);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：巡逻

        private void RunPatrol(Player player) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            float accel = IsPhase2 ? 0.18f : 0.13f;

            // 远距离冲锋加速
            float dist = Vector2.Distance(NPC.Center, player.Center);
            if (dist > 700f) {
                speed *= 1.35f;
                accel *= 1.25f;
            }

            WormMovement(player, speed, accel);

            patrolTimer++;
            if (patrolTimer >= patrolDuration) {
                ChooseNextAttack(player);
            }
        }

        #endregion

        #region 状态：预攻击（蓄力电报）

        private void RunPreAttack(Player player) {
            // 减速悬停，蓄力光效
            NPC.velocity *= 0.94f;

            attackTimer++;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.5f, Volume = 0.8f }, NPC.Center);
            }
            if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                AoyuanHelper.CreateFrostVortex(NPC.Center, 50f, 0.4f, 8);
            }

            if (attackTimer >= PreAttackDuration) {
                CurrentState = AoyuanState.Attacking;
                attackTimer = 0;
                veilCount = 0;
                waveCount = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：攻击执行

        private void RunAttacking(Player player) {
            attackTimer++;
            AoyuanAttackType currentAttack = (AoyuanAttackType)(int)NPC.ai[2];

            bool finished = currentAttack switch {
                AoyuanAttackType.GlacialPillarChess => AttackGlacialPillarChess(player),
                AoyuanAttackType.BlizzardVeil => AttackBlizzardVeil(player),
                AoyuanAttackType.FrostBreath => AttackFrostBreath(player),
                AoyuanAttackType.IcicleRainCombo => AttackIcicleRainCombo(player),
                AoyuanAttackType.FrostRingCombo => AttackFrostRingCombo(player),
                AoyuanAttackType.AbsoluteZero => AttackAbsoluteZero(player),
                _ => true
            };

            if (finished) {
                CloseMouth();
                CurrentState = AoyuanState.Cooldown;
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：冷却

        private void RunCooldown(Player player) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, speed, IsPhase2 ? 0.18f : 0.13f);

            attackTimer++;
            int cd = IsPhase2 ? CooldownDuration / 2 : CooldownDuration;
            if (attackTimer >= cd) {
                CurrentState = AoyuanState.Patrol;
                patrolTimer = 0;
                int min = IsPhase2 ? MinPatrolDuration / 2 : MinPatrolDuration;
                int max = IsPhase2 ? MaxPatrolDuration / 2 : MaxPatrolDuration;
                patrolDuration = Main.rand.Next(min, max);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 攻击选择

        private void ChooseNextAttack(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int maxType = IsPhase2 ? Phase2AttackCount : Phase1AttackCount;

            AoyuanAttackType chosen;
            int attempts = 0;
            do {
                chosen = (AoyuanAttackType)Main.rand.Next(maxType);
                attempts++;
            } while (chosen == lastAttack && attempts < 10);

            // 二阶段有概率优先释放绝对零度大招
            if (IsPhase2 && lastAttack != AoyuanAttackType.AbsoluteZero && Main.rand.NextBool(4)) {
                chosen = AoyuanAttackType.AbsoluteZero;
            }

            lastAttack = chosen;
            NPC.ai[2] = (float)chosen;
            attackTimer = 0;
            veilCount = 0;
            waveCount = 0;

            CurrentState = AoyuanState.PreAttack;
            NPC.netUpdate = true;
        }

        #endregion
    }
}

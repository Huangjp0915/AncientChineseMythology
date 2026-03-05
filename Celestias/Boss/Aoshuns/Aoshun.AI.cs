using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region AI主循环 — 状态机架构

        public override bool PreAI() {
            globalTime += 1f / 60f;

            // 目标与脱战
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (!player.active || player.dead) {
                despawn = true;
            }
            if (despawn) {
                NPC.velocity.Y += 0.11f;
                NPC.ai[3]++;
                if (NPC.ai[3] >= 300) {
                    NPC.active = false;

                }
                return false;
            }

            // 蠕虫身体链生成（首次）
            SpawnWormBody();

            // 地形碰撞检测
            bool collision = CheckTileCollision();
            isUnderground = collision;

            // === 风暴蓄电系统 ===
            if (collision && CurrentState == AoshunState.Patrol) {
                StormCharge = Math.Min(StormCharge + ChargePerDigTick, MaxStormCharge);

                // 蓄电粒子
                if (!VaultUtils.isServer && Main.rand.NextBool(4) && StormCharge > 20f) {
                    float chargeRatio = StormCharge / MaxStormCharge;
                    AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, chargeRatio);
                }
            }

            // 近距离判定（用于张嘴动画）
            close = Vector2.Distance(NPC.Center, player.Center) <= 400;

            // === 阶段转换检查 ===
            if (IsPhase2 && !didPhase2Transition && CurrentState != AoshunState.PhaseTransition) {
                TransitionToPhase2();
            }

            // === 状态机主循环 ===
            switch (CurrentState) {
                case AoshunState.Intro:
                    RunIntro(player);
                    break;
                case AoshunState.Patrol:
                    RunPatrol(player, collision);
                    break;
                case AoshunState.PreAttack:
                    RunPreAttack(player, collision);
                    break;
                case AoshunState.Attacking:
                    RunAttacking(player, collision);
                    break;
                case AoshunState.Cooldown:
                    RunCooldown(player, collision);
                    break;
                case AoshunState.Submerge:
                    RunSubmerge(player, collision);
                    break;
                case AoshunState.Emerge:
                    RunEmerge(player, collision);
                    break;
                case AoshunState.PhaseTransition:
                    RunPhaseTransition(player, collision);
                    break;
            }

            // 蠕虫朝向与旋转
            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;
            if (NPC.velocity.X < 0f)
                NPC.spriteDirection = 1;
            else
                NPC.spriteDirection = -1;

            // 碰撞状态同步
            if (collision) {
                if (NPC.localAI[0] != 1) NPC.netUpdate = true;
                NPC.localAI[0] = 1f;
            }
            else {
                if (NPC.localAI[0] != 0f) NPC.netUpdate = true;
                NPC.localAI[0] = 0f;
            }

            if ((NPC.velocity.X > 0 && NPC.oldVelocity.X < 0 || NPC.velocity.X < 0 && NPC.oldVelocity.X > 0 ||
                 NPC.velocity.Y > 0 && NPC.oldVelocity.Y < 0 || NPC.velocity.Y < 0 && NPC.oldVelocity.Y > 0) && !NPC.justHit)
                NPC.netUpdate = true;

            return false;
        }

        #endregion

        #region 蠕虫身体生成

        private void SpawnWormBody() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (NPC.ai[0] != 0) return;

            NPC.realLife = NPC.whoAmI;
            int latestNPC = NPC.whoAmI;
            int randomWormLength = Main.rand.Next(25, 35);
            for (int i = 0; i < randomWormLength; i++) {
                int bodyType = (i % 2 == 0)
                    ? ModContent.NPCType<AoshunArms>()
                    : ModContent.NPCType<AoshunBody>();
                latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(),
                    (int)NPC.position.X + NPC.width / 2,
                    (int)NPC.position.Y + NPC.height / 2,
                    bodyType, NPC.whoAmI, 0, latestNPC);
                Main.npc[latestNPC].realLife = NPC.whoAmI;
                Main.npc[latestNPC].ai[3] = NPC.whoAmI;
            }
            latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(),
                (int)NPC.position.X + NPC.width / 2,
                (int)NPC.position.Y + NPC.height / 2,
                ModContent.NPCType<AoshunTail>(), NPC.whoAmI, 0, latestNPC);
            Main.npc[latestNPC].realLife = NPC.whoAmI;
            Main.npc[latestNPC].ai[3] = NPC.whoAmI;
            NPC.ai[0] = 1;
            NPC.netUpdate = true;
        }

        #endregion

        #region 地形碰撞检测

        private bool CheckTileCollision() {
            int minX = (int)(NPC.position.X / 16f) - 1;
            int maxX = (int)((NPC.position.X + NPC.width) / 16f) + 2;
            int minY = (int)(NPC.position.Y / 16f) - 1;
            int maxY = (int)((NPC.position.Y + NPC.height) / 16f) + 2;
            minX = Math.Max(minX, 0);
            maxX = Math.Min(maxX, Main.maxTilesX);
            minY = Math.Max(minY, 0);
            maxY = Math.Min(maxY, Main.maxTilesY);

            bool col = false;
            for (int i = minX; i < maxX; i++) {
                for (int j = minY; j < maxY; j++) {
                    var tile = Main.tile[i, j];
                    if (tile != null && (tile.HasUnactuatedTile &&
                        (Main.tileSolid[tile.TileType] ||
                         Main.tileSolidTop[tile.TileType] && tile.TileFrameY == 0) ||
                        tile.LiquidAmount > 64)) {
                        float tx = i * 16f;
                        float ty = j * 16f;
                        if (NPC.position.X + NPC.width > tx && NPC.position.X < tx + 16f &&
                            NPC.position.Y + NPC.height > ty && NPC.position.Y < ty + 16f) {
                            col = true;
                            if (Main.rand.NextBool(100) && tile.HasUnactuatedTile)
                                WorldGen.KillTile(i, j, true, true, false);
                        }
                    }
                }
            }
            return col;
        }

        #endregion

        #region 蠕虫移动（保留钻地为主）

        /// <summary>
        /// 标准蠕虫移动 - 钻地追踪玩家
        /// </summary>
        private void WormMovement(Player player, bool collision, float speed, float accel) {
            Vector2 npcCenter = NPC.Center;
            float targetX = player.Center.X;
            float targetY = player.Center.Y;

            // 栅格化位置
            float gridTargetX = (int)(targetX / 16f) * 16;
            float gridTargetY = (int)(targetY / 16f) * 16;
            float gridNpcX = (int)(npcCenter.X / 16f) * 16;
            float gridNpcY = (int)(npcCenter.Y / 16f) * 16;
            float dirX = gridTargetX - gridNpcX;
            float dirY = gridTargetY - gridNpcY;
            float length = MathF.Sqrt(dirX * dirX + dirY * dirY);

            if (!collision) {
                // 空中下坠
                NPC.velocity.Y += 0.11f;
                if (NPC.velocity.Y > speed) NPC.velocity.Y = speed;
                if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.4f) {
                    if (NPC.velocity.X < 0) NPC.velocity.X -= accel * 1.1f;
                    else NPC.velocity.X += accel * 1.1f;
                }
                else if (NPC.velocity.Y == speed) {
                    if (NPC.velocity.X < dirX) NPC.velocity.X += accel;
                    else if (NPC.velocity.X > dirX) NPC.velocity.X -= accel;
                }
                else if (NPC.velocity.Y > 4f) {
                    if (NPC.velocity.X < 0) NPC.velocity.X += accel * 0.9f;
                    else NPC.velocity.X -= accel * 0.9f;
                }
            }
            else {
                // 地下挖掘追踪
                if (NPC.soundDelay == 0) {
                    float delay = Math.Clamp(length / 40f, 10f, 20f);
                    NPC.soundDelay = (int)delay;
                    SoundEngine.PlaySound(SoundID.WormDig, NPC.position);
                }

                float absDirX = Math.Abs(dirX);
                float absDirY = Math.Abs(dirY);
                if (length > 0f) {
                    float ns = speed / length;
                    dirX *= ns;
                    dirY *= ns;
                }

                if ((NPC.velocity.X > 0 && dirX > 0) || (NPC.velocity.X < 0 && dirX < 0) ||
                    (NPC.velocity.Y > 0 && dirY > 0) || (NPC.velocity.Y < 0 && dirY < 0)) {
                    if (NPC.velocity.X < dirX) NPC.velocity.X += accel;
                    else if (NPC.velocity.X > dirX) NPC.velocity.X -= accel;
                    if (NPC.velocity.Y < dirY) NPC.velocity.Y += accel;
                    else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= accel;

                    if (Math.Abs(dirY) < speed * 0.2f && ((NPC.velocity.X > 0 && dirX < 0) || (NPC.velocity.X < 0 && dirX > 0))) {
                        if (NPC.velocity.Y > 0) NPC.velocity.Y += accel * 2f;
                        else NPC.velocity.Y -= accel * 2f;
                    }
                    if (Math.Abs(dirX) < speed * 0.2f && ((NPC.velocity.Y > 0 && dirY < 0) || (NPC.velocity.Y < 0 && dirY > 0))) {
                        if (NPC.velocity.X > 0) NPC.velocity.X += accel * 2f;
                        else NPC.velocity.X -= accel * 2f;
                    }
                }
                else if (absDirX > absDirY) {
                    if (NPC.velocity.X < dirX) NPC.velocity.X += accel * 1.1f;
                    else if (NPC.velocity.X > dirX) NPC.velocity.X -= accel * 1.1f;
                    if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5f) {
                        if (NPC.velocity.Y > 0) NPC.velocity.Y += accel;
                        else NPC.velocity.Y -= accel;
                    }
                }
                else {
                    if (NPC.velocity.Y < dirY) NPC.velocity.Y += accel * 1.1f;
                    else if (NPC.velocity.Y > dirY) NPC.velocity.Y -= accel * 1.1f;
                    if (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y) < speed * 0.5f) {
                        if (NPC.velocity.X > 0) NPC.velocity.X += accel;
                        else NPC.velocity.X -= accel;
                    }
                }
            }
        }

        /// <summary>
        /// 向指定位置钻地（用于深渊伏击等特殊移动）
        /// </summary>
        private void WormMoveTo(Vector2 target, float speed, float accel) {
            Vector2 dir = target - NPC.Center;
            float length = dir.Length();
            if (length < 16f) return;

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
            WormMovement(player, isUnderground, speed, 0.5f);

            if (attackTimer >= 60) {
                CurrentState = AoshunState.Patrol;
                patrolTimer = 0;
                patrolDuration = Main.rand.Next(MinPatrolDuration, MaxPatrolDuration);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：巡逻 — 蠕虫钻地追踪

        private void RunPatrol(Player player, bool collision) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            float accel = IsPhase2 ? 0.7f : 0.5f;

            // 远距离冲锋加速
            float dist = Vector2.Distance(NPC.Center, player.Center);
            if (dist > 600f) {
                speed *= 1.3f;
                accel *= 1.2f;
            }

            WormMovement(player, collision || dist > 500f, speed, accel);

            patrolTimer++;

            // 蓄电满时身体电弧粒子
            if (IsFullyCharged && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(30, 30);
                var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            // 巡逻时间到 → 选择攻击
            if (patrolTimer >= patrolDuration) {
                ChooseNextAttack();
            }
        }

        #endregion

        #region 状态：预攻击（短暂电报）

        private void RunPreAttack(Player player, bool collision) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 * 0.8f : PatrolSpeed * 0.8f;
            WormMovement(player, collision, speed, 0.4f);

            attackTimer++;

            // 预攻击蓄力粒子
            if (!VaultUtils.isServer && attackTimer % 4 == 0) {
                AoshunHelper.CreateThunderVortex(NPC.Center, 40f, 0.3f, 6);
            }

            if (attackTimer >= 40) {
                CurrentState = AoshunState.Attacking;
                attackTimer = 0;
                AttackProgress = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：攻击执行

        private void RunAttacking(Player player, bool collision) {
            attackTimer++;
            AoshunAttackType currentAttack = (AoshunAttackType)(int)NPC.ai[2];

            // 各攻击类型的独立执行逻辑
            bool attackFinished = false;
            switch (currentAttack) {
                case AoshunAttackType.ChainLightning:
                    attackFinished = AttackChainLightning(player, collision);
                    break;
                case AoshunAttackType.AbyssalAmbush:
                    attackFinished = AttackAbyssalAmbush(player, collision);
                    break;
                case AoshunAttackType.DragonScaleStorm:
                    attackFinished = AttackDragonScaleStorm(player, collision);
                    break;
                case AoshunAttackType.TornadoEnsnare:
                    attackFinished = AttackTornadoEnsnare(player, collision);
                    break;
                case AoshunAttackType.ThunderSeal:
                    attackFinished = AttackThunderSeal(player, collision);
                    break;
                case AoshunAttackType.DragonKingRoar:
                    attackFinished = AttackDragonKingRoar(player, collision);
                    break;
                case AoshunAttackType.EyeOfTheStorm:
                    attackFinished = AttackEyeOfTheStorm(player, collision);
                    break;
                case AoshunAttackType.ThunderChainCharge:
                    attackFinished = AttackThunderChainCharge(player, collision);
                    break;
            }

            if (attackFinished) {
                // 蓄电消耗
                StormCharge = Math.Max(0, StormCharge - 30f);

                CurrentState = AoshunState.Cooldown;
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：冷却

        private void RunCooldown(Player player, bool collision) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, collision, speed, 0.5f);

            attackTimer++;
            if (attackTimer >= CooldownDuration) {
                CurrentState = AoshunState.Patrol;
                patrolTimer = 0;
                // 二阶段巡逻更短促
                int min = IsPhase2 ? MinPatrolDuration / 2 : MinPatrolDuration;
                int max = IsPhase2 ? MaxPatrolDuration / 2 : MaxPatrolDuration;
                patrolDuration = Main.rand.Next(min, max);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：深渊潜行（深渊伏击专用）

        private void RunSubmerge(Player player, bool collision) {
            attackTimer++;

            // 急速下钻
            NPC.velocity.Y += 0.8f;
            if (NPC.velocity.Y > 30f) NPC.velocity.Y = 30f;
            NPC.velocity.X *= 0.95f;

            // 潜行足够深后进入等待
            if (attackTimer >= 60) {
                // 记录玩家当前位置作为伏击目标
                ambushTarget = player.Center;
                ambushWarningTimer = 0;
                CurrentState = AoshunState.Emerge;
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 状态：爆出（深渊伏击出击）

        private void RunEmerge(Player player, bool collision) {
            attackTimer++;

            if (attackTimer < 60) {
                // 预警阶段 - 向伏击目标下方移动
                Vector2 belowTarget = ambushTarget + new Vector2(0, 600f);
                WormMoveTo(belowTarget, 25f, 1.2f);

                // 地面预警标记
                if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer == 30) {
                    AoshunAttacks.SpawnThunderSealMarker(NPC, ambushTarget, 90);
                }

                // 预警粒子（从地面冒出的电弧）
                if (!VaultUtils.isServer && attackTimer > 20 && attackTimer % 5 == 0) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 dustPos = ambushTarget + new Vector2(Main.rand.NextFloat(-80, 80), 0);
                        var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                        d.noGravity = true;
                        d.scale = 2f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-1, 1), -Main.rand.NextFloat(3, 8));
                    }
                }
            }
            else if (attackTimer == 60) {
                // 高速上冲爆出
                NPC.velocity = new Vector2(0, -ChargeSpeed);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }
            else if (attackTimer < 100) {
                // 持续上冲
                NPC.velocity.Y = -ChargeSpeed;
                NPC.velocity.X = (ambushTarget.X - NPC.Center.X) * 0.02f;
            }
            else {
                // 出击后释放冲击波
                if (attackTimer == 100 && Main.netMode != NetmodeID.MultiplayerClient) {
                    AoshunAttacks.SpawnShockwave(NPC, 12);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                }

                if (attackTimer >= 120) {
                    CurrentState = AoshunState.Cooldown;
                    attackTimer = 0;
                    StormCharge = Math.Max(0, StormCharge - 30f);
                    NPC.netUpdate = true;
                }
                else {
                    // 减速
                    NPC.velocity *= 0.92f;
                }
            }
        }

        #endregion

        #region 状态：阶段转换

        private void TransitionToPhase2() {
            CurrentState = AoshunState.PhaseTransition;
            attackTimer = 0;
            NPC.netUpdate = true;
        }

        private void RunPhaseTransition(Player player, bool collision) {
            attackTimer++;

            // 转换动画：剧烈放电 + 减速
            NPC.velocity *= 0.96f;

            if (!VaultUtils.isServer) {
                if (attackTimer % 5 == 0) {
                    AoshunHelper.CreateThunderVortex(NPC.Center, 100f + attackTimer, 0.8f, 30);
                }
                if (attackTimer % 10 == 0) {
                    AoshunHelper.CreateThunderBurst(NPC.Center, 150f + attackTimer * 0.5f, 3, 16);
                }
            }

            if (attackTimer == 30) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 2f }, NPC.Center);
            }

            // 全屏闪电效果
            if (attackTimer == 60 && !VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(NPC.Center, 400f, 6, 30);
            }

            if (attackTimer >= 90) {
                didPhase2Transition = true;
                StormCharge = MaxStormCharge; // 满蓄电进入二阶段
                CurrentState = AoshunState.Patrol;
                patrolTimer = 0;
                patrolDuration = Main.rand.Next(MinPatrolDuration / 2, MaxPatrolDuration / 2);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 攻击选择

        private void ChooseNextAttack() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int maxType = IsPhase2 ? Phase2AttackCount : Phase1AttackCount;

            AoshunAttackType chosen;
            int attempts = 0;
            do {
                chosen = (AoshunAttackType)Main.rand.Next(maxType);
                attempts++;
            } while (chosen == lastAttack && attempts < 10);

            // 蓄电满时优先使用消耗电荷的强力攻击
            if (IsFullyCharged && Main.rand.NextBool(3)) {
                if (IsPhase2) {
                    chosen = Main.rand.NextBool()
                        ? AoshunAttackType.ThunderChainCharge
                        : AoshunAttackType.EyeOfTheStorm;
                }
                else {
                    chosen = AoshunAttackType.AbyssalAmbush;
                }
            }

            lastAttack = chosen;
            NPC.ai[2] = (float)chosen;
            attackTimer = 0;
            scaleBarrageTimer = 0;
            chainChargeCount = 0;
            tornadoCount = 0;
            orbitAngle = 0f;

            // 深渊伏击直接进入潜行状态
            if (chosen == AoshunAttackType.AbyssalAmbush) {
                CurrentState = AoshunState.Submerge;
            }
            else {
                CurrentState = AoshunState.PreAttack;
            }
            NPC.netUpdate = true;
        }

        #endregion

        #region 攻击实现

        // ===== 1. 雷链穿刺 =====
        // 在玩家周围生成多个闪电节点，节点间电弧连锁
        // 玩家需要在节点间穿梭躲避
        private bool AttackChainLightning(Player player, bool collision) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, collision, speed, 0.5f);

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            }

            // 分3波释放闪电节点
            if (attackTimer == 20 || attackTimer == 50 || attackTimer == 80) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int nodeCount = Main.expertMode ? 5 : 4;
                    if (IsPhase2) nodeCount += 2;
                    AoshunAttacks.SpawnChainLightningNodes(NPC, player, nodeCount);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f }, NPC.Center);
                }
            }

            return attackTimer >= 120;
        }

        // ===== 2. 深渊伏击 =====
        // 由Submerge→Emerge状态处理，此处备用
        private bool AttackAbyssalAmbush(Player player, bool collision) {
            // 不应被调用（从Submerge/Emerge状态处理）
            return true;
        }

        // ===== 3. 龙鳞风暴 =====
        // 高速移动中身体段向四周抛射带电龙鳞
        // 玩家需要远离蠕虫路径
        private bool AttackDragonScaleStorm(Player player, bool collision) {
            // 突然加速
            float speed = (IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed) * 1.5f;
            float accel = IsPhase2 ? 1.0f : 0.8f;
            WormMovement(player, collision || true, speed, accel); // 强制追踪

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Pitch = -0.2f }, NPC.Center);
            }

            // 每8帧从身体段喷射龙鳞
            if (attackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int scaleCount = Main.expertMode ? 3 : 2;
                if (IsPhase2) scaleCount += 1;
                AoshunAttacks.ShootDragonScales(NPC, scaleCount);
            }

            // 移动中电弧粒子
            if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, 1.5f);
            }

            int duration = IsPhase2 ? 150 : 120;
            return attackTimer >= duration;
        }

        // ===== 4. 龙卷缠绕 =====
        // 绕玩家盘旋，同时在盘旋路径上释放缓慢追踪的龙卷风弹幕
        // 玩家被龙卷风群包围，需要找缝隙逃脱
        private bool AttackTornadoEnsnare(Player player, bool collision) {
            // 盘旋移动（环绕玩家）
            float orbitRadius = IsPhase2 ? 350f : 450f;
            float orbitSpeed = IsPhase2 ? 0.035f : 0.025f;
            orbitAngle += orbitSpeed;

            Vector2 targetPos = player.Center + new Vector2(
                MathF.Cos(orbitAngle) * orbitRadius,
                MathF.Sin(orbitAngle) * orbitRadius * 0.6f
            );

            // 平滑追踪盘旋点
            Vector2 toTarget = targetPos - NPC.Center;
            float dist = toTarget.Length();
            if (dist > 20f) {
                float moveSpeed = Math.Min(dist * 0.08f, IsPhase2 ? 24f : 18f);
                NPC.velocity = toTarget.SafeNormalize(Vector2.UnitY) * moveSpeed;
            }

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
            }

            // 每30帧释放龙卷风
            if (attackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                AoshunAttacks.SpawnTornado(NPC, player);
                tornadoCount++;
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = 0.1f }, NPC.Center);
            }

            int maxTornados = IsPhase2 ? 7 : 5;
            return tornadoCount >= maxTornados;
        }

        // ===== 5. 天雷印 =====
        // 在玩家当前位置落下延迟天雷标记，标记到时间后引爆
        // 连续标记多个位置，玩家需要持续移动
        private bool AttackThunderSeal(Player player, bool collision) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, collision, speed * 0.9f, 0.5f);

            // 每25帧标记玩家位置
            if (attackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int delay = Main.expertMode ? 50 : 70;
                AoshunAttacks.SpawnThunderSealMarker(NPC, player.Center, delay);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 0.8f }, player.Center);
            }

            int marks = IsPhase2 ? 8 : 6;
            return attackTimer >= marks * 25;
        }

        // ===== 6. 龙王怒啸（二阶段）=====
        // 短暂停顿后释放全屏减速debuff波
        // 同时身体放出一圈冲击波弹幕
        private bool AttackDragonKingRoar(Player player, bool collision) {
            if (attackTimer < 30) {
                // 蓄力减速
                NPC.velocity *= 0.92f;

                if (!VaultUtils.isServer && attackTimer % 5 == 0) {
                    AoshunHelper.CreateThunderVortex(NPC.Center, 60f + attackTimer * 2, 0.5f, 15);
                }
            }
            else if (attackTimer == 30) {
                // 释放怒啸
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 2f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 环形冲击波
                    AoshunAttacks.SpawnShockwave(NPC, 16);

                    // 对范围内玩家施加debuff
                    for (int i = 0; i < Main.maxPlayers; i++) {
                        Player p = Main.player[i];
                        if (p.active && !p.dead && Vector2.Distance(NPC.Center, p.Center) < 1500f) {
                            p.AddBuff(BuffID.Slow, 180);
                            p.AddBuff(BuffID.BrokenArmor, 120);
                        }
                    }
                }

                if (!VaultUtils.isServer) {
                    AoshunHelper.CreateThunderBurst(NPC.Center, 300f, 5, 25);
                }
            }
            else {
                // 怒啸后继续移动
                float speed = PatrolSpeedPhase2;
                WormMovement(player, collision, speed, 0.6f);
            }

            return attackTimer >= 80;
        }

        // ===== 7. 风暴之眼（二阶段）=====
        // 以玩家为中心创造一个缩小安全区，区外为持续伤害风暴
        // 安全区逐渐缩小，玩家需要精确站位
        private bool AttackEyeOfTheStorm(Player player, bool collision) {
            float speed = PatrolSpeedPhase2;
            WormMovement(player, collision, speed, 0.6f);

            if (attackTimer == 1) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 生成风暴之眼弹幕（以玩家为中心，持续缩小的安全区）
                    int duration = Main.expertMode ? 300 : 240;
                    AoshunAttacks.SpawnStormEye(NPC, player.Center, duration);
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.8f }, player.Center);
            }

            // 蠕虫在风暴中继续攻击，增加压力
            if (attackTimer > 60 && attackTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int sealDelay = Main.expertMode ? 40 : 55;
                AoshunAttacks.SpawnThunderSealMarker(NPC, player.Center + Main.rand.NextVector2Circular(150, 150), sealDelay);
            }

            int totalDuration = Main.expertMode ? 300 : 240;
            return attackTimer >= totalDuration + 30;
        }

        // ===== 8. 雷霆连环冲（二阶段）=====
        // 多次快速穿越玩家位置，每次穿越留下持续伤害的电痕
        // 逐渐封锁活动空间
        private bool AttackThunderChainCharge(Player player, bool collision) {
            maxChainCharges = Main.expertMode ? 5 : 4;

            if (chainChargeCount >= maxChainCharges) {
                return attackTimer >= 30; // 最后一冲之后等一会
            }

            int chargePhaseTime = 40; // 每次冲刺的间隔

            if (attackTimer % chargePhaseTime == 0) {
                // 选定冲刺方向
                chargeDirection = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
            }

            int phaseTimer = attackTimer % chargePhaseTime;

            if (phaseTimer < 15) {
                // 蓄力 - 稍微远离玩家
                Vector2 retreatTarget = player.Center - chargeDirection * 400f;
                WormMoveTo(retreatTarget, ChargeSpeed * 0.8f, 1.5f);
            }
            else if (phaseTimer < 35) {
                // 冲刺穿越 - 高速向目标方向
                NPC.velocity = chargeDirection * ChargeSpeed;

                // 留下电痕
                if (phaseTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    AoshunAttacks.SpawnElectricTrail(NPC);
                }

                if (!VaultUtils.isServer) {
                    AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, 2f);
                }
            }
            else {
                // 减速准备下一次
                NPC.velocity *= 0.85f;
                chainChargeCount++;
                attackTimer = -1; // 重置（下帧变0）
            }

            return false;
        }

        #endregion
    }
}

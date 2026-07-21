using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region AI主循环

        public override bool PreAI() {
            globalTime += 1f / 60f;
            gestureTimer++;

            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (!player.active || player.dead)
                despawn = true;
            if (despawn) {
                NPC.velocity.Y += 0.2f;
                NPC.velocity.X *= 0.98f;
                NPC.dontTakeDamage = true;
                NPC.EncourageDespawn(180);
                return false;
            }

            SpawnWormBody();

            bool collision = CheckTileCollision();
            isUnderground = collision;

            // 演出期间无敌（战斗状态可打）
            NPC.dontTakeDamage = deathTriggered ||
                CurrentState is AoshunState.Intro or AoshunState.Transition2
                             or AoshunState.Transition3 or AoshunState.Dying;

            // === 风暴蓄电: 连接拍钻地积攒（P3 由风暴本身供能） ===
            if (CurrentState == AoshunState.Regroup) {
                float gain = collision ? ChargePerDigTick : (InPhase3 ? 0.30f : 0.12f);
                StormCharge = Math.Min(StormCharge + gain, MaxStormCharge);

                if (!VaultUtils.isServer && collision && Main.rand.NextBool(4) && StormCharge > 20f)
                    AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, StormCharge / MaxStormCharge);
            }

            close = Vector2.Distance(NPC.Center, player.Center) <= 400;

            // === 阶段转换检查（仅战斗状态触发, 入场/演出中顺延） ===
            if (CurrentState is AoshunState.Regroup or AoshunState.Attacking) {
                if (!didTransition2 && HpFrac < Phase2Threshold)
                    EnterTransition(AoshunState.Transition2);
                else if (didTransition2 && !didTransition3 && HpFrac < Phase3Threshold)
                    EnterTransition(AoshunState.Transition3);
            }

            // === P3 风暴之眼参数推进（各端确定性推导, 服务器权威结算在眼弹幕内） ===
            UpdateEyeArena();

            // === 状态机 ===
            StateTimer++;
            switch (CurrentState) {
                case AoshunState.Intro: RunIntro(player); break;
                case AoshunState.Regroup: RunRegroup(player, collision); break;
                case AoshunState.Attacking: RunAttacking(player, collision); break;
                case AoshunState.Transition2: RunTransition2(player); break;
                case AoshunState.Transition3: RunTransition3(player); break;
                case AoshunState.Dying: RunDying(player); break;
            }

            // 蠕虫朝向
            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;
            NPC.spriteDirection = NPC.velocity.X < 0f ? 1 : -1;

            // 冲刺残影热度: 纯速度门控（快才点燃, 慢则指数熄灭 → 残影只属于真正快的时刻）
            dashVisualHeat = NPC.velocity.Length() > 34f ? 1f : dashVisualHeat * 0.9f;

            // 碰撞状态位（身段贴图入土判断沿用 localAI[0]）
            if (collision) {
                if (NPC.localAI[0] != 1f) NPC.netUpdate = true;
                NPC.localAI[0] = 1f;
            }
            else {
                if (NPC.localAI[0] != 0f) NPC.netUpdate = true;
                NPC.localAI[0] = 0f;
            }

            if (!VaultUtils.isServer)
                UpdateStormScreenFx();

            return false;
        }

        #endregion

        #region 屏幕演出标量（纯本地视觉）

        /// <summary>
        /// 每帧平滑推进风暴屏幕标量并发布给 <see cref="AoshunStormScreenSystem"/>。
        /// 压暗 = 电量底色 + 阶段底线 + 演出加深; 满电边沿触发一次性雷暴临界脉冲;
        /// 风雨强度(stormWeatherFx)供 Aoshun.PostDraw 的全屏 StormWarp 使用。
        /// </summary>
        private void UpdateStormScreenFx() {
            float chargeRatio = MathHelper.Clamp(StormCharge / MaxStormCharge, 0f, 1f);

            // —— 压暗底色 ——
            float tintTarget = 0.10f + chargeRatio * 0.35f;
            if (InPhase2) tintTarget = Math.Max(tintTarget, 0.40f);
            if (InPhase3) tintTarget = Math.Max(tintTarget, 0.46f);
            if (CurrentState == AoshunState.Attacking &&
                CurrentAttack == AoshunAttackType.AbyssBreach && SubState <= 2)
                tintTarget = Math.Max(tintTarget, 0.60f);
            if (CurrentState is AoshunState.Transition2 or AoshunState.Transition3)
                tintTarget = Math.Max(tintTarget, 0.72f);
            if (CurrentState == AoshunState.Dying)
                tintTarget = StateTimer < 150 ? 0.65f : 0.30f;

            stormTintFx = MathHelper.Lerp(stormTintFx, tintTarget, tintTarget > stormTintFx ? 0.06f : 0.03f);

            // —— 风雨强度: T2 落雷后常驻, T3 早段与死亡静默拍退潮 ——
            float weatherTarget = didTransition2 ? 1f : 0f;
            if (CurrentState == AoshunState.Transition2)
                weatherTarget = StateTimer >= 120 ? 1f : 0.15f;
            if (CurrentState == AoshunState.Transition3 && StateTimer < 100)
                weatherTarget = 0.35f;          // 风暴屏息
            if (CurrentState == AoshunState.Dying)
                weatherTarget = StateTimer < 90 ? 0.8f : 0.1f; // 雨止
            stormWeatherFx = MathHelper.Lerp(stormWeatherFx, weatherTarget, 0.035f);

            // —— 满电临界边沿 ——
            bool fullyCharged = IsFullyCharged;
            if (fullyCharged && !stormWasFullyCharged) {
                AoshunStormScreenSystem.PulseBloom(0.85f);
                ACMUtils.AddScreenShake(7f);
            }
            stormWasFullyCharged = fullyCharged;

            AoshunStormScreenSystem.Publish(NPC.Center, stormTintFx, 0f, fullyCharged,
                EyeActive, EyeCenter, EyeRadius, globalTime);

            // 风场粒子: 风向即预警（汇聚=要出手, 爆散=刚炸开, 坍缩=收势/濒死）
            PublishWindField();

            // 死亡演出的天空调光
            if (CurrentState == AoshunState.Dying)
                AoshunSky.SetDeathDim(MathHelper.Clamp((StateTimer - 60f) / 120f, 0f, 1f));
        }

        /// <summary>
        /// 每帧推导风场模式并发布给 <see cref="AoshunWindField"/>（纯本地视觉, 只读同步状态）。
        /// 环境风向与 StormWarp 雨幕倾角同源, 保证"风线-雨幕-扭曲"是同一场风。
        /// </summary>
        private void PublishWindField() {
            Vector2 ambient = new(0.85f + MathF.Sin(globalTime * 0.13f) * 0.15f, 0.22f);
            AoshunWindMode mode = AoshunWindMode.Ambient;
            Vector2 focus = NPC.Center;
            float strength = 0.45f + StormCharge / MaxStormCharge * 0.25f;
            Vector2 windDir = ambient;

            switch (CurrentState) {
                case AoshunState.Intro:
                    if (StateTimer < 46) { strength = 0.3f + StateTimer / 46f * 0.4f; }
                    else if (StateTimer < 60) { mode = AoshunWindMode.Burst; strength = 1f; }
                    else strength = 0.7f;
                    break;
                case AoshunState.Transition2:
                    if (StateTimer < 120) { mode = AoshunWindMode.Converge; strength = StateTimer / 120f; }
                    else if (StateTimer < 145) { mode = AoshunWindMode.Burst; strength = 1f; }
                    else strength = 0.9f;
                    break;
                case AoshunState.Transition3:
                    if (StateTimer < 60) { mode = AoshunWindMode.Collapse; strength = 0.6f; }
                    else { mode = AoshunWindMode.Ring; focus = aimPoint; strength = 0.9f; }
                    break;
                case AoshunState.Dying:
                    if (StateTimer < 90) { mode = AoshunWindMode.Collapse; strength = 0.9f; }
                    else if (StateTimer < 150) { mode = AoshunWindMode.Off; strength = 0f; } // 静默拍: 风也停
                    else { mode = AoshunWindMode.Burst; strength = 1f; }
                    break;
                case AoshunState.Attacking:
                    switch (CurrentAttack) {
                        case AoshunAttackType.GaleCleave: {
                            int wt = StateTimer % 54;
                            if (wt < 26) { mode = AoshunWindMode.Converge; strength = 0.4f + wt / 26f * 0.5f; }
                            else if (wt < 34) { mode = AoshunWindMode.Burst; strength = 0.9f; }
                            break;
                        }
                        case AoshunAttackType.CyclonePalm:
                            if (SubState == 1) { mode = AoshunWindMode.Converge; strength = 0.8f; }
                            else if (SubState == 2) { mode = AoshunWindMode.Burst; strength = 0.9f; }
                            break;
                        case AoshunAttackType.AbyssBreach:
                            if (SubState == 2) { mode = AoshunWindMode.Collapse; focus = aimPoint; strength = 0.75f; }
                            else if (SubState == 3 && StateTimer < 20) { mode = AoshunWindMode.Burst; focus = aimPoint; strength = 1f; }
                            break;
                        case AoshunAttackType.StormNet:
                            mode = AoshunWindMode.Ring; focus = aimPoint; strength = 0.8f;
                            break;
                        case AoshunAttackType.HeavensCall:
                            if (SubState == 1) { mode = AoshunWindMode.Converge; strength = 0.85f; }
                            break;
                        case AoshunAttackType.TempestPierce:
                            if (SubState % 2 == 0 && StateTimer >= 10) { mode = AoshunWindMode.Converge; strength = 0.8f; }
                            else if (SubState % 2 == 1 && StateTimer <= 11) {
                                windDir = NPC.velocity.SafeNormalize(ambient); strength = 1f;
                            }
                            break;
                        case AoshunAttackType.KingRoar:
                            if (StateTimer < 30) { mode = AoshunWindMode.Converge; strength = 0.8f; }
                            else if (StateTimer < 50) { mode = AoshunWindMode.Burst; strength = 1f; }
                            break;
                        case AoshunAttackType.EyePierce:
                        case AoshunAttackType.WallCyclone:
                        case AoshunAttackType.EyeEdgeCall:
                            if (EyeActive) { mode = AoshunWindMode.Ring; focus = EyeCenter; strength = 0.9f; }
                            break;
                    }
                    break;
            }

            // P3 常驻竞技场: 无更强模式时默认绕眼环流
            if (mode == AoshunWindMode.Ambient && EyeActive) {
                mode = AoshunWindMode.Ring;
                focus = EyeCenter;
                strength = MathF.Max(strength, 0.7f);
            }

            AoshunWindField.Publish(mode, focus, strength, windDir);
        }

        #endregion

        #region 蠕虫身体生成 / 地形碰撞

        private void SpawnWormBody() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (NPC.ai[0] != 0) return;

            NPC.realLife = NPC.whoAmI;
            int latestNPC = NPC.whoAmI;
            const int SegmentCount = 28;
            int armIndex = 0;
            for (int i = 0; i < SegmentCount; i++) {
                bool isArm = i % 2 == 0;
                int bodyType = isArm
                    ? ModContent.NPCType<AoshunArms>()
                    : ModContent.NPCType<AoshunBody>();
                latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(),
                    (int)NPC.position.X + NPC.width / 2,
                    (int)NPC.position.Y + NPC.height / 2,
                    bodyType, NPC.whoAmI, 0, latestNPC);
                Main.npc[latestNPC].realLife = NPC.whoAmI;
                Main.npc[latestNPC].ai[3] = NPC.whoAmI;
                if (isArm)
                    Main.npc[latestNPC].ai[2] = armIndex++; // 臂序号: 手势错相用
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
                            if (Main.rand.NextBool(120) && tile.HasUnactuatedTile)
                                WorldGen.KillTile(i, j, true, true, false);
                        }
                    }
                }
            }
            return col;
        }

        #endregion

        #region 移动原语

        /// <summary>标准蠕虫钻地追踪（原版蠕虫手感, 钻地巡逻用）</summary>
        private void WormMovement(Vector2 goal, bool collision, float speed, float accel) {
            Vector2 npcCenter = NPC.Center;
            float dirX = (int)(goal.X / 16f) * 16 - (int)(npcCenter.X / 16f) * 16;
            float dirY = (int)(goal.Y / 16f) * 16 - (int)(npcCenter.Y / 16f) * 16;
            float length = MathF.Sqrt(dirX * dirX + dirY * dirY);

            if (!collision) {
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
                return;
            }

            if (NPC.soundDelay == 0) {
                NPC.soundDelay = (int)Math.Clamp(length / 40f, 10f, 20f);
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

        /// <summary>直接朝目标点加速（无视地形, 空中编排用）</summary>
        private void WormMoveTo(Vector2 target, float speed, float accel) {
            Vector2 dir = (target - NPC.Center).SafeNormalize(Vector2.UnitY) * speed;
            if (NPC.velocity.X < dir.X) NPC.velocity.X += accel;
            else if (NPC.velocity.X > dir.X) NPC.velocity.X -= accel;
            if (NPC.velocity.Y < dir.Y) NPC.velocity.Y += accel;
            else if (NPC.velocity.Y > dir.Y) NPC.velocity.Y -= accel;
        }

        /// <summary>受钳制转向率的空中巡航（大弧线, 展示全身用）</summary>
        private void AirSteer(Vector2 target, float speed, float maxTurn) {
            Vector2 desired = (target - NPC.Center).SafeNormalize(Vector2.UnitX);
            Vector2 cur = NPC.velocity.SafeNormalize(Vector2.UnitX);
            float curAngle = cur.ToRotation();
            float desAngle = desired.ToRotation();
            float diff = MathHelper.WrapAngle(desAngle - curAngle);
            float turn = MathHelper.Clamp(diff, -maxTurn, maxTurn);
            NPC.velocity = (curAngle + turn).ToRotationVector2() * speed;
        }

        #endregion

        #region 入场演出「破土升天」

        private void RunIntro(Player player) {
            int t = StateTimer;

            if (t == 1) {
                // 起点: 玩家侧后方远处地下
                int side = player.Center.X > NPC.Center.X ? -1 : 1;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float gy = AoshunAttacks.FindGroundY(player.Center.X + side * 1300f, player.Center.Y);
                    NPC.Center = new Vector2(player.Center.X + side * 1300f, gy + 320f);
                    aimPoint = new Vector2(player.Center.X + side * 380f, player.Center.Y);
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.4f, Pitch = -0.4f }, player.Center);
            }

            if (t < 40) {
                // 扬尘线逼近: 贴地高速钻行, 震屏渐强
                Vector2 breach = new(aimPoint.X, AoshunAttacks.FindGroundY(aimPoint.X, player.Center.Y) + 260f);
                WormMoveTo(breach, 30f, 2.2f);
                if (!VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(1f + t / 40f * 2f);
                    float gy = AoshunAttacks.FindGroundY(NPC.Center.X, NPC.Center.Y - 400f);
                    for (int i = 0; i < 2; i++) {
                        var d = Dust.NewDustPerfect(new Vector2(NPC.Center.X + Main.rand.NextFloat(-40, 40), gy - 4), DustID.Smoke);
                        d.velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f));
                        d.scale = 1.8f;
                    }
                }
            }
            else if (t < 46) {
                // 一拍静默: 悬停蓄势
                NPC.velocity *= 0.75f;
                SetGesture(ArmGestureKind.Tremor, 60);
            }
            else if (t == 46) {
                // 破土冲天
                NPC.velocity = new Vector2(0f, -46f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(10f);
                    AoshunStormScreenSystem.PulseBloom(0.9f);
                    AoshunHelper.CreateThunderBurst(NPC.Center, 260f, 4, 18);
                    AoshunSky.TriggerBolt(2); // 天空起振: 入场定调雷
                }
            }
            else if (t < 120) {
                // 空中大弧线横越玩家头顶: 转向率钳制展示全身
                float speed = MathHelper.Lerp(44f, 24f, (t - 46) / 74f);
                AirSteer(player.Center + new Vector2(0, -560f), speed, 0.045f);
                SetGesture(ArmGestureKind.SpreadOut, 74);
                if (!VaultUtils.isServer && Main.rand.NextBool(2))
                    AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, 1.2f);
            }
            else if (t < IntroDuration) {
                // 俯冲回土
                Vector2 diveGoal = player.Center + new Vector2(320f, 700f);
                AirSteer(diveGoal, 30f, 0.06f);
                SetGesture(ArmGestureKind.None, 30);
                if (isUnderground && t > 150 && !VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(5f);
                }
            }

            if (t >= IntroDuration) {
                BeginRegroup();
            }
        }

        #endregion

        #region 连接拍「蜷缩重整」+ 巡逻

        private void BeginRegroup() {
            CurrentState = AoshunState.Regroup;
            StateTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private void RunRegroup(Player player, bool collision) {
            float speed = InPhase2 ? PatrolSpeedLate : PatrolSpeed;
            float accel = InPhase2 ? 0.7f : 0.5f;

            float dist = Vector2.Distance(NPC.Center, player.Center);
            if (dist > 600f) { speed *= 1.3f; accel *= 1.2f; }

            if (StateTimer <= RegroupCoilTime) {
                // 蜷缩: 慢速 + 正弦扭转 → S 形紧盘剪影, 臂微颤
                WormMovement(player.Center, collision || dist > 500f, speed * 0.6f, accel * 0.8f);
                NPC.velocity = NPC.velocity.RotatedBy(MathF.Sin(StateTimer * 0.35f) * 0.055f);
                SetGesture(ArmGestureKind.Tremor, RegroupCoilTime);
            }
            else {
                WormMovement(player.Center, collision || dist > 500f, speed, accel);
                SetGesture(ArmGestureKind.None, 30);
            }

            if (IsFullyCharged && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30, 30), DustID.Electric);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            int duration = InPhase3 ? 70 : (InPhase2 ? 75 : 90);
            // 玩家太远时顺延开招（防脱屏放招）, 但设 240f 硬上限
            bool tooFar = dist > 1400f && StateTimer < 240;
            if (StateTimer >= duration + RegroupCoilTime && !tooFar) {
                ChooseNextAttack();
            }
        }

        #endregion

        #region 洗牌袋选招

        private void ChooseNextAttack() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (attackBag.Count == 0)
                RefillBag();

            AoshunAttackType chosen = (AoshunAttackType)attackBag[0];
            attackBag.RemoveAt(0);

            // 满电 → 本次攻击过载强化并清空电量
            Overloaded = IsFullyCharged;
            if (Overloaded)
                StormCharge = 0f;

            CurrentAttack = chosen;
            CurrentState = AoshunState.Attacking;
            StateTimer = 0;
            SubState = 0;
            attackBreachRound = 0;
            NPC.netUpdate = true;
        }

        /// <summary>重压招（相邻则打散, 保证与控场招相间）</summary>
        private static bool IsHeavy(int a) =>
            a == (int)AoshunAttackType.AbyssBreach ||
            a == (int)AoshunAttackType.TempestPierce ||
            a == (int)AoshunAttackType.EyePierce;

        private void RefillBag() {
            attackBag.Clear();
            if (InPhase3) {
                attackBag.AddRange([
                    (int)AoshunAttackType.EyePierce,
                    (int)AoshunAttackType.WallCyclone,
                    (int)AoshunAttackType.EyeEdgeCall,
                    (int)AoshunAttackType.GaleCleave,
                    (int)AoshunAttackType.HeavensCall,
                ]);
            }
            else if (InPhase2) {
                attackBag.AddRange([
                    (int)AoshunAttackType.GaleCleave,
                    (int)AoshunAttackType.CyclonePalm,
                    (int)AoshunAttackType.ThunderSeal,
                    (int)AoshunAttackType.AbyssBreach,
                    (int)AoshunAttackType.StormNet,
                    (int)AoshunAttackType.HeavensCall,
                    (int)AoshunAttackType.TempestPierce,
                ]);
            }
            else {
                attackBag.AddRange([
                    (int)AoshunAttackType.GaleCleave,
                    (int)AoshunAttackType.CyclonePalm,
                    (int)AoshunAttackType.ThunderSeal,
                    (int)AoshunAttackType.AbyssBreach,
                ]);
            }

            // Fisher-Yates 洗牌
            for (int i = attackBag.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
            }
            // 防复读: 袋首 ≠ 上袋尾
            if (attackBag.Count > 1 && attackBag[0] == lastBagTail)
                (attackBag[0], attackBag[^1]) = (attackBag[^1], attackBag[0]);
            // 重压招相间: 相邻重压对后者向后冒泡一位
            for (int i = 0; i + 1 < attackBag.Count; i++) {
                if (IsHeavy(attackBag[i]) && IsHeavy(attackBag[i + 1]) && i + 2 < attackBag.Count)
                    (attackBag[i + 1], attackBag[i + 2]) = (attackBag[i + 2], attackBag[i + 1]);
            }
            lastBagTail = attackBag[^1];
        }

        #endregion

        #region 攻击调度

        private void RunAttacking(Player player, bool collision) {
            bool finished = CurrentAttack switch {
                AoshunAttackType.GaleCleave => AttackGaleCleave(player, collision),
                AoshunAttackType.CyclonePalm => AttackCyclonePalm(player),
                AoshunAttackType.ThunderSeal => AttackThunderSeal(player, collision),
                AoshunAttackType.AbyssBreach => AttackAbyssBreach(player),
                AoshunAttackType.StormNet => AttackStormNet(player, collision),
                AoshunAttackType.HeavensCall => AttackHeavensCall(player),
                AoshunAttackType.TempestPierce => AttackTempestPierce(player),
                AoshunAttackType.KingRoar => AttackKingRoar(player),
                AoshunAttackType.EyePierce => AttackEyePierce(player),
                AoshunAttackType.WallCyclone => AttackWallCyclone(player),
                AoshunAttackType.EyeEdgeCall => AttackEyeEdgeCall(player),
                _ => true,
            };

            // 保底出口: 任何攻击 15s 超时强制回巡逻
            if (finished || StateTimer > 900) {
                attackCounter++;
                Overloaded = false;
                BeginRegroup();
            }
        }

        #endregion

        #region 招式 1: 风刃连斩 GaleCleave

        // 波形: [后仰26f → 挥斩6f(错相) → 波间22f] × 3 (过载 4)
        private bool AttackGaleCleave(Player player, bool collision) {
            int waves = Overloaded ? 4 : 3;
            const int WaveTime = 54;
            int wave = StateTimer / WaveTime;
            int wt = StateTimer % WaveTime;

            // 攻击期间低速游弋（公平: 出招时移动更少）
            WormMovement(player.Center, collision, (InPhase2 ? PatrolSpeedLate : PatrolSpeed) * 0.6f, 0.4f);

            if (wave >= waves)
                return wt >= 20;

            if (wt < 26) {
                SetGesture(ArmGestureKind.ReelBack, 26, 1.5f);
                // 蓄势汇聚尘（在臂段位置, 由臂自己发; 头部只给音效点）
                if (wt == 6)
                    SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.5f, Volume = 0.7f }, NPC.Center);
            }
            else if (wt == 26) {
                SetGesture(ArmGestureKind.Slash, 10, 4f);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.25f, Volume = 1.1f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float speedScale = wave == 0 ? 0.65f : 1f; // 首波降速: 换阶后防冷枪
                    AoshunAttacks.SpawnGaleBlades(NPC, player, wave % 2, speedScale);
                }
                if (!VaultUtils.isServer)
                    ACMUtils.AddScreenShake(3f);
            }
            else if (wt > 40) {
                SetGesture(ArmGestureKind.None, 14);
            }

            return false;
        }

        #endregion

        #region 招式 2: 龙卷压掌 CyclonePalm

        // SubState 0: 侧翼就位(≤40f 提前出)  1: 内拢40f+静默8f→压掌  2: 后仰脱离30f
        private bool AttackCyclonePalm(Player player) {
            if (SubState == 0) {
                int side = attackCounter % 2 == 0 ? 1 : -1;
                Vector2 flank = player.Center + new Vector2(side * 430f, -170f);
                WormMoveTo(flank, 26f, 1.6f);
                SetGesture(ArmGestureKind.None, 20);
                if (Vector2.Distance(NPC.Center, flank) < 90f || StateTimer > 60) {
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.90f;
                SetGesture(ArmGestureKind.FoldIn, 40, 1f);

                // 内拢期汇聚粒子, 最后 8f 静默（爆发前的收声）
                if (StateTimer < 40 && !VaultUtils.isServer && StateTimer % 2 == 0) {
                    Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(140f, 140f);
                    var d = Dust.NewDustPerfect(p, DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * 7f;
                }
                if (StateTimer == 40)
                    SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.8f, Volume = 0.6f }, NPC.Center);

                if (StateTimer >= 48) {
                    // 压掌: 龙卷落地 + 环形冲击波
                    SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.55f, Volume = 1.6f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 0.9f }, NPC.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        float gy = AoshunAttacks.FindGroundY(player.Center.X, player.Center.Y);
                        AoshunAttacks.SpawnCyclone(NPC, new Vector2(player.Center.X, gy), 0);
                        if (Main.expertMode || Overloaded) {
                            float gx2 = player.Center.X + (player.Center.X > NPC.Center.X ? 520f : -520f);
                            AoshunAttacks.SpawnCyclone(NPC, new Vector2(gx2, AoshunAttacks.FindGroundY(gx2, player.Center.Y)), 0);
                        }
                        AoshunAttacks.SpawnShockwave(NPC, 10);
                    }
                    if (!VaultUtils.isServer) {
                        AoshunStormScreenSystem.PulseBloom(0.6f);
                        ACMUtils.AddScreenShake(7f);
                    }
                    SubState = 2;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 后仰脱离
                SetGesture(ArmGestureKind.SpreadOut, 30);
                WormMoveTo(NPC.Center + new Vector2(0, -300f), 16f, 0.9f);
                if (StateTimer >= 30)
                    return true;
            }
            return false;
        }

        #endregion

        #region 招式 3: 天雷印 ThunderSeal

        private bool AttackThunderSeal(Player player, bool collision) {
            float speed = (InPhase2 ? PatrolSpeedLate : PatrolSpeed) * 0.8f;
            WormMovement(player.Center, collision, speed, 0.45f);

            if (StateTimer == 30) {
                SetGesture(ArmGestureKind.SpreadOut, 30, 1f);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 1f }, player.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoshunAttacks.SpawnSealFan(NPC, player, Overloaded ? 7 : 5);
            }
            if (StateTimer == 70)
                SetGesture(ArmGestureKind.None, 20);

            return StateTimer >= 165;
        }

        #endregion

        #region 招式 4: 破渊突袭 AbyssBreach

        // SubState 0: 深潜40f  1: 静默20f(锁定落点)  2: 地裂预警55f  3: 破土+回落, 共2轮
        private bool AttackAbyssBreach(Player player) {
            if (SubState == 0) {
                NPC.velocity.Y += 0.9f;
                if (NPC.velocity.Y > 32f) NPC.velocity.Y = 32f;
                NPC.velocity.X *= 0.95f;
                SetGesture(ArmGestureKind.None, 20);
                if (StateTimer >= 40) {
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.92f;
                if (StateTimer >= 20) {
                    // 静默结束: 锁定落点（带提前量, 之后不再追踪）
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 lead = player.Center + player.velocity * 22f;
                        aimPoint = new Vector2(lead.X, AoshunAttacks.FindGroundY(lead.X, lead.Y));
                        AoshunAttacks.SpawnBreachCrack(NPC, aimPoint, 55);
                        NPC.netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.5f, Pitch = -0.6f }, player.Center);
                    SubState = 2;
                    StateTimer = 0;
                }
            }
            else if (SubState == 2) {
                // 预警期: 移动到落点正下方蓄势
                Vector2 below = aimPoint + new Vector2(0, 620f);
                WormMoveTo(below, 27f, 1.4f);
                SetGesture(ArmGestureKind.Tremor, 55);
                if (StateTimer >= 55) {
                    NPC.Center = new Vector2(aimPoint.X, NPC.Center.Y); // 对轴修正, 防斜穿
                    NPC.velocity = new Vector2(0f, -46f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                    SubState = 3;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 3) {
                SetGesture(ArmGestureKind.SpreadOut, 30, 1f);
                if (StateTimer < 14) {
                    NPC.velocity.Y = -46f; // 持续上冲
                    if (StateTimer == 6) {
                        // 破土瞬间: 冲击波 + 龙鳞喷泉 + 泛光重震
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            AoshunAttacks.SpawnShockwave(NPC, 12);
                            AoshunAttacks.ShootBreachScales(NPC, aimPoint, Overloaded ? 12 : 8);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.4f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            AoshunStormScreenSystem.PulseBloom(0.95f);
                            ACMUtils.AddScreenShake(10f);
                        }
                    }
                }
                else {
                    NPC.velocity *= 0.9f;      // 弧顶硬刹
                    NPC.velocity.Y += 0.5f;    // 回潜
                }

                if (StateTimer >= 55) {
                    // 第 2 轮或收招
                    if (attackBreachRound == 0) {
                        attackBreachRound = 1;
                        SubState = 1;
                        StateTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        attackBreachRound = 0;
                        return true;
                    }
                }
            }
            return false;
        }

        // 破渊轮数（本地推导即可: 由 SubState 循环驱动, 各端一致）
        private int attackBreachRound;

        #endregion

        #region 招式 5: 雷链电网 StormNet (P2+)

        private bool AttackStormNet(Player player, bool collision) {
            if (StateTimer == 1) {
                SetGesture(ArmGestureKind.SpreadOut, 40, 1f);
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    aimPoint = player.Center; // 网锚定施放瞬间
                    AoshunAttacks.SpawnStormNet(NPC, aimPoint, 8);
                    NPC.netUpdate = true;
                }
            }

            // 网存续期间沿环外游弋施压
            float orbitR = 620f;
            float ang = globalTime * 0.9f + attackCounter;
            Vector2 orbitGoal = aimPoint + ang.ToRotationVector2() * orbitR;
            WormMoveTo(orbitGoal, 19f, 0.9f);

            if (StateTimer == 60)
                SetGesture(ArmGestureKind.None, 20);

            return StateTimer >= 280;
        }

        #endregion

        #region 招式 6: 张臂唤雷 HeavensCall (P2+)

        // SubState 0: 上浮就位≤40f  1: 张臂蓄力50f(75%静默)→雷落  2: 反冲收招25f
        private bool AttackHeavensCall(Player player) {
            if (SubState == 0) {
                Vector2 high = player.Center + new Vector2(0, -340f);
                WormMoveTo(high, 24f, 1.3f);
                if (Vector2.Distance(NPC.Center, high) < 110f || StateTimer > 40) {
                    SubState = 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.9f;
                NPC.velocity.Y -= 0.12f; // 蓄力时身体缓慢后飘(反向运动)
                SetGesture(ArmGestureKind.SpreadOut, 50, 1.2f);

                float t = StateTimer / 50f;
                if (!VaultUtils.isServer) {
                    // 汇聚流光: 密度 ∝ √t, 75% 处骤停 → 静默拍
                    if (t < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(t) * 0.9f) {
                        Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(300f, 300f);
                        var d = Dust.NewDustPerfect(p, DustID.Electric);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = (NPC.Center - p) * 0.085f;
                    }
                    ACMUtils.AddScreenShake(t * t * t * 4f);
                }

                if (StateTimer >= 50) {
                    // 唤雷: 错相雷柱 + 躯体受反冲下坠
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.7f }, NPC.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int bolts = Overloaded ? 8 : 6;
                        SpawnCallBolts(player, bolts);
                    }
                    NPC.velocity.Y += 7f; // 反冲
                    if (!VaultUtils.isServer) {
                        AoshunStormScreenSystem.PulseBloom(0.8f);
                        ACMUtils.AddScreenShake(8f);
                    }
                    SubState = 2;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.94f;
                SetGesture(ArmGestureKind.None, 25);
                if (StateTimer >= 25)
                    return true;
            }
            return false;
        }

        /// <summary>玩家周边错相天雷柱: X 间距 ≥190px, 落点一次锁定</summary>
        private void SpawnCallBolts(Player player, int count) {
            float span = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++) {
                float xOff = (i - span) * 205f + Main.rand.NextFloat(-14f, 14f);
                float x = player.Center.X + xOff;
                float gy = AoshunAttacks.FindGroundY(x, player.Center.Y);
                AoshunAttacks.SpawnSkyBolt(NPC, new Vector2(x, gy), 55 + i * 8);
            }
        }

        #endregion

        #region 招式 7: 风暴穿刺 TempestPierce (P2+)

        // 每轮: SubState偶=蓄势30f(10f起后拉+红线)  奇=穿刺11f+硬刹6f+重整24f, ×3轮
        private bool AttackTempestPierce(Player player) {
            int round = SubState / 2;
            int rounds = Overloaded ? 4 : 3;
            if (round >= rounds)
                return true;

            if (SubState % 2 == 0) {
                // 蓄势: 悬于玩家侧方, 后拉 + 冲刺线预告
                int side = (round + attackCounter) % 2 == 0 ? 1 : -1;
                Vector2 hover = player.Center + new Vector2(side * 500f, -60f);

                if (StateTimer < 10) {
                    WormMoveTo(hover, 24f, 1.5f);
                    SetGesture(ArmGestureKind.None, 10);
                }
                else {
                    if (StateTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 锁定穿刺线（带提前量）
                        aimVector = (player.Center + player.velocity * 12f - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item32 with { Pitch = 0.1f, Volume = 0.9f }, NPC.Center);
                    }
                    // 迟滞后吸: pow8 → 最后几帧猛然后缩(吸气)
                    float t = (StateTimer - 10) / 20f;
                    Vector2 reel = -aimVector * MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 8f) * 8f;
                    NPC.velocity = NPC.velocity * 0.85f + reel;
                    SetGesture(ArmGestureKind.ReelBack, 20, 0.8f);
                }

                if (StateTimer >= 30) {
                    // 冲刺: 一帧设速
                    NPC.velocity = aimVector * 52f;
                    SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.3f, Volume = 1.3f }, NPC.Center);
                    if (!VaultUtils.isServer) {
                        ACMUtils.AddScreenShake(6f);
                        AoshunStormScreenSystem.PulseBloom(0.5f);
                    }
                    SubState++;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                SetGesture(ArmGestureKind.Slash, 12, 1f);
                if (StateTimer <= 11) {
                    // 穿刺中: 直线不转向, 沿途电痕
                    if (StateTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                        AoshunAttacks.SpawnElectricTrail(NPC);
                    if (!VaultUtils.isServer)
                        AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, 2f);
                }
                else if (StateTimer <= 17) {
                    NPC.velocity *= 0.7f; // 硬刹
                }
                else {
                    WormMoveTo(player.Center + new Vector2(0, -200f), 16f, 0.8f);
                }

                if (StateTimer >= 41) {
                    SubState++;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            return false;
        }

        #endregion

        #region 招式 8: 龙王怒啸 KingRoar (P3 进场)

        private bool AttackKingRoar(Player player) {
            if (StateTimer < 30) {
                NPC.velocity *= 0.9f;
                SetGesture(ArmGestureKind.FoldIn, 30, 0.8f);
                if (!VaultUtils.isServer && StateTimer % 5 == 0)
                    AoshunHelper.CreateThunderVortex(NPC.Center, 60f + StateTimer * 2, 0.5f, 12);
            }
            else if (StateTimer == 30) {
                SetGesture(ArmGestureKind.SpreadOut, 40, 1.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 2f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    AoshunAttacks.SpawnShockwave(NPC, 14);
                    for (int i = 0; i < Main.maxPlayers; i++) {
                        Player p = Main.player[i];
                        if (p.active && !p.dead && Vector2.Distance(NPC.Center, p.Center) < 900f) {
                            p.AddBuff(BuffID.Slow, 120);
                            p.AddBuff(BuffID.BrokenArmor, 90);
                        }
                    }
                }
                if (!VaultUtils.isServer) {
                    AoshunHelper.CreateThunderBurst(NPC.Center, 300f, 5, 25);
                    AoshunStormScreenSystem.PulseBloom(0.85f);
                    ACMUtils.AddScreenShake(10f);
                }
            }
            else {
                WormMoveTo(EyeActive ? EyeCenter + new Vector2(0, -EyeRadius) : player.Center, 18f, 0.8f);
            }

            return StateTimer >= 80;
        }

        #endregion

        #region 招式 9: 眼弦穿刺 EyePierce (P3)

        // SubState偶=沿眼外圈就位+红线45f  奇=穿弦冲刺+出弦刹车, ×2弦
        private bool AttackEyePierce(Player player) {
            if (!EyeActive)
                return true;
            int chord = SubState / 2;
            if (chord >= 2)
                return true;

            float rimR = EyeRadius + 220f;

            if (SubState % 2 == 0) {
                if (StateTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                    // 选弦: 从玩家相对眼心方位的邻角切入, 弦偏移 ≤200px
                    float baseAng = (player.Center - EyeCenter).ToRotation();
                    float entryAng = baseAng + (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(1.5f, 2.4f);
                    Vector2 entry = EyeCenter + entryAng.ToRotationVector2() * rimR;
                    Vector2 through = EyeCenter + Main.rand.NextVector2Circular(200f, 200f);
                    aimPoint = entry;
                    aimVector = (through - entry).SafeNormalize(Vector2.UnitX);
                    NPC.netUpdate = true;
                }

                // 就位到入弦点
                WormMoveTo(aimPoint, 26f, 1.6f);
                if (StateTimer > 20)
                    SetGesture(ArmGestureKind.ReelBack, 25, 0.8f);
                if (StateTimer == 25)
                    SoundEngine.PlaySound(SoundID.Item32 with { Pitch = 0.15f, Volume = 1f }, NPC.Center);

                if (StateTimer >= 45) {
                    NPC.Center = Vector2.Lerp(NPC.Center, aimPoint, 0.5f); // 离屏矫正
                    NPC.velocity = aimVector * 58f;
                    SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.4f, Volume = 1.4f }, NPC.Center);
                    if (!VaultUtils.isServer) {
                        ACMUtils.AddScreenShake(7f);
                        AoshunStormScreenSystem.PulseBloom(0.55f);
                    }
                    SubState++;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                SetGesture(ArmGestureKind.Slash, 12, 1f);
                float distOut = Vector2.Distance(NPC.Center, EyeCenter);
                if (distOut > rimR + 100f || StateTimer > 60) {
                    NPC.velocity *= 0.7f;
                    if (StateTimer > 8) { // 出弦即刹后短歇
                        SubState++;
                        StateTimer = -40;  // 弦间间隔 ≥130f: 45(预告)+~30(飞行)+40(负计时) ≈ 145
                        NPC.netUpdate = true;
                    }
                }
                else if (!VaultUtils.isServer) {
                    AoshunHelper.CreateLightningTrail(NPC.Center, NPC.velocity, 2f);
                }
            }
            return false;
        }

        #endregion

        #region 招式 10: 沿壁龙卷 WallCyclone (P3)

        private bool AttackWallCyclone(Player player) {
            if (!EyeActive)
                return true;

            if (StateTimer == 20) {
                SetGesture(ArmGestureKind.FoldIn, 30, 1f);
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
            }
            if (StateTimer == 50) {
                SetGesture(ArmGestureKind.Slash, 12, 1.5f);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float dir = attackCounter % 2 == 0 ? 1f : -1f;
                    float ang = (NPC.Center - EyeCenter).ToRotation();
                    Vector2 rimPos = EyeCenter + ang.ToRotationVector2() * (EyeRadius - 60f);
                    AoshunAttacks.SpawnCyclone(NPC, rimPos, 1, dir);
                }
                if (!VaultUtils.isServer)
                    ACMUtils.AddScreenShake(5f);
            }

            // 施放期间沿眼外壁巡游一段
            float orbitAng = globalTime * 0.55f * (attackCounter % 2 == 0 ? 1f : -1f);
            Vector2 goal = EyeCenter + orbitAng.ToRotationVector2() * (EyeRadius + 260f);
            WormMoveTo(goal, 20f, 1f);

            return StateTimer >= 90;
        }

        #endregion

        #region 招式 11: 眼缘落雷 EyeEdgeCall (P3)

        // 雷柱只落眼内边缘环带, 逼离舒适圈; 2轮×4根
        private bool AttackEyeEdgeCall(Player player) {
            if (!EyeActive)
                return true;

            if (StateTimer == 1 || StateTimer == 100) {
                SetGesture(ArmGestureKind.SpreadOut, 45, 1.2f);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.45f, Volume = 1.4f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float baseAng = (player.Center - EyeCenter).ToRotation() + Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 4; i++) {
                        float ang = baseAng + MathHelper.TwoPi * i / 4f;
                        Vector2 pos = EyeCenter + ang.ToRotationVector2() * EyeRadius * 0.82f;
                        AoshunAttacks.SpawnSkyBolt(NPC, pos, 55 + i * 6);
                    }
                }
            }
            if (StateTimer == 70)
                SetGesture(ArmGestureKind.None, 20);

            // 悬于眼顶缘
            WormMoveTo(EyeCenter + new Vector2(0, -EyeRadius - 240f), 17f, 0.8f);

            return StateTimer >= 200;
        }

        #endregion

        #region 换阶段演出 T2「雷暴降临」

        private void EnterTransition(AoshunState transition) {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                AoshunAttacks.ClearHostileProjectiles();
            CurrentState = transition;
            StateTimer = 0;
            SubState = 0;
            Overloaded = false;
            NPC.netUpdate = true;
        }

        private void RunTransition2(Player player) {
            int t = StateTimer;

            if (t < 120) {
                // 盘上中天, 张臂迎雷
                Vector2 stage = player.Center + new Vector2(0, -380f);
                WormMoveTo(stage, 18f, 1f);
                NPC.velocity *= 0.97f;
                SetGesture(ArmGestureKind.SpreadOut, 120, 0.6f);

                float ct = t / 120f;
                if (!VaultUtils.isServer) {
                    // 汇聚流光(75% 骤停) + t³ 渐强震
                    if (ct < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(ct)) {
                        Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(420f, 420f);
                        var d = Dust.NewDustPerfect(p, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 2f;
                        d.velocity = (NPC.Center - p) * 0.07f;
                    }
                    ACMUtils.AddScreenShake(ct * ct * ct * 6f);
                }
                if (t == 100)
                    SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.9f, Volume = 0.8f }, NPC.Center);
            }
            else if (t == 120) {
                // 巨雷贯体: 从此风雨常驻
                didTransition2 = true;
                StormCharge = MaxStormCharge;
                aimPoint = NPC.Center;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.8f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    AoshunStormScreenSystem.PulseBloom(1f);
                    ACMUtils.AddScreenShake(12f);
                    AoshunHelper.CreateThunderBurst(NPC.Center, 420f, 6, 30);
                    AoshunSky.TriggerBolt(4); // 雷暴降临: 天空齐鸣
                }
                NPC.netUpdate = true;
            }
            else {
                // 受雷淬体, 缓慢展开
                NPC.velocity *= 0.96f;
                NPC.velocity.Y += MathF.Sin(t * 0.2f) * 0.08f;
                SetGesture(ArmGestureKind.Tremor, 60);
                if (t == 150)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.6f }, NPC.Center);
            }

            if (t >= Transition2Duration) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    attackBag.Clear(); // 换阶段重开洗牌袋
                    lastBagTail = -1;
                }
                BeginRegroup();
            }
        }

        #endregion

        #region 换阶段演出 T3「坠入眼中」

        private void RunTransition3(Player player) {
            int t = StateTimer;

            if (t < 60) {
                // 深潜消失, 风暴屏息
                NPC.velocity.Y += 0.8f;
                if (NPC.velocity.Y > 30f) NPC.velocity.Y = 30f;
                NPC.velocity.X *= 0.95f;
                SetGesture(ArmGestureKind.None, 30);
                if (t == 30)
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.6f, Pitch = -0.7f }, player.Center);
            }
            else if (t == 60) {
                // 眼锚定 + 生成常驻竞技场
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    aimPoint = player.Center;
                    AoshunAttacks.SpawnStormEyeArena(NPC, aimPoint);
                    NPC.netUpdate = true;
                }
                didTransition3 = true;
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.7f, Volume = 1.8f }, player.Center);
            }
            else if (t < 120) {
                // 眼收拢显形期间, 本体绕远处待命
                Vector2 wait = aimPoint + new Vector2(0, 900f);
                WormMoveTo(wait, 24f, 1.2f);
            }
            else if (t == 120) {
                // 破雨而出沿壁盘旋
                Vector2 rimEntry = aimPoint + new Vector2(EyeStartRadius + 250f, 120f);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.Center = rimEntry;
                    NPC.velocity = new Vector2(-26f, -8f);
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.9f }, aimPoint);
                if (!VaultUtils.isServer) {
                    AoshunStormScreenSystem.PulseBloom(0.85f);
                    ACMUtils.AddScreenShake(9f);
                }
            }
            else {
                // 盘旋入场
                float ang = (NPC.Center - aimPoint).ToRotation() + 0.03f;
                Vector2 goal = aimPoint + ang.ToRotationVector2() * (EyeStartRadius + 180f);
                WormMoveTo(goal, 24f, 1.4f);
                SetGesture(ArmGestureKind.SpreadOut, 60, 0.8f);
            }

            if (t >= Transition3Duration) {
                // P3 开场固定接怒啸
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    attackBag.Clear();
                    lastBagTail = -1;
                }
                CurrentAttack = AoshunAttackType.KingRoar;
                CurrentState = AoshunState.Attacking;
                StateTimer = 0;
                SubState = 0;
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// 每帧从眼弹幕读取眼参数（半径由弹幕自身年龄确定性推导, 各端一致;
        /// 伤害结算也在弹幕内, 头部只做展示与招式锚定）。眼意外丢失时在连接拍补生成。
        /// </summary>
        private void UpdateEyeArena() {
            if (CurrentState == AoshunState.Dying) {
                EyeActive = false;
                return;
            }
            if (!didTransition3)
                return;

            int eyeType = ModContent.ProjectileType<AoshunStormEye>();
            EyeActive = false;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != eyeType)
                    continue;
                EyeActive = true;
                EyeCenter = p.Center;
                if (p.ModProjectile is AoshunStormEye eye)
                    EyeRadius = eye.CurrentRadius;
                break;
            }

            // 眼丢失兜底: 战斗状态下重新展开（以当前目标为锚）
            if (!EyeActive && Main.netMode != NetmodeID.MultiplayerClient &&
                CurrentState == AoshunState.Regroup && StateTimer > 30) {
                Player player = Main.player[NPC.target];
                if (player.active && !player.dead)
                    AoshunAttacks.SpawnStormEyeArena(NPC, player.Center);
            }
        }

        #endregion

        #region 死亡演出「风暴葬礼」

        private void RunDying(Player player) {
            int t = StateTimer;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.97f;

            if (t == 1) {
                SoundEngine.PlaySound(SoundID.NPCDeath60 with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 杀掉眼弹幕（清场）
                    foreach (Projectile p in Main.ActiveProjectiles) {
                        if (p.type == ModContent.ProjectileType<AoshunStormEye>())
                            p.Kill();
                    }
                }
            }

            if (t < 90) {
                // 挣扎攀升: 速度渐衰, 全身震颤, 电火花外泄 ∝ 进度²
                float st = t / 90f;
                NPC.velocity.Y = -6f * (1f - st);
                NPC.velocity.X = MathF.Sin(t * 0.13f) * 4f * (1f - st * 0.5f);
                SetGesture(ArmGestureKind.Tremor, 90);

                if (!VaultUtils.isServer && Main.rand.NextFloat() < st * st * 0.8f) {
                    foreach (NPC seg in Main.ActiveNPCs) {
                        if (seg.realLife != NPC.whoAmI || !Main.rand.NextBool(6))
                            continue;
                        var d = Dust.NewDustPerfect(seg.Center + Main.rand.NextVector2Circular(14, 14), DustID.Electric);
                        d.noGravity = true;
                        d.scale = 1.7f;
                        d.velocity = Main.rand.NextVector2Circular(4, 4);
                    }
                }
            }
            else if (t < 150) {
                // 静默拍: 雨止, 天空调暗, 只剩电网明灭（视觉在 Drawing/Sky）
                NPC.velocity *= 0.9f;
                SetGesture(ArmGestureKind.None, 30);
            }
            else if (t == 150) {
                // 万雷加身: 全战唯一的最大重拍
                aimPoint = NPC.Center;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.85f, Volume = 2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.9f, Volume = 2f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    AoshunStormScreenSystem.PulseBloom(1f);
                    ACMUtils.AddScreenShake(16f);
                    AoshunHelper.CreateThunderBurst(NPC.Center, 500f, 6, 30);
                    AoshunSky.TriggerBolt(5); // 万雷加身: 天空全部起闪
                }
                NPC.netUpdate = true;
            }
            else if (t < 300) {
                // 从尾到头逐段爆裂（每 5f 一段; 各端确定性扫到同一"最深段", 视觉同步）
                NPC.velocity *= 0.92f;
                SetGesture(ArmGestureKind.Tremor, 150);
                if ((t - 150) % 5 == 0)
                    DetonateDeepestSegment();
            }
            else if (t >= DeathDuration) {
                // 真实死亡（deathTriggered=true → CheckDead 放行）
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.life = 0;
                    NPC.checkDead();
                }
            }
        }

        /// <summary>
        /// 死亡演出: 找到链上最深(最靠尾)的存活段, 客户端出雷爆视觉, 服务器将其失活。
        /// 直接置 life=0/active=false 而非 Strike — 段与头共享 realLife 血池, 打击会误伤头。
        /// </summary>
        private void DetonateDeepestSegment() {
            NPC deepest = null;
            int bestDepth = -1;
            foreach (NPC seg in Main.ActiveNPCs) {
                if (seg.realLife != NPC.whoAmI || seg.whoAmI == NPC.whoAmI)
                    continue;
                int depth = 0;
                int cursor = seg.whoAmI;
                while (depth < 64) {
                    int prev = (int)Main.npc[cursor].ai[1];
                    if (prev < 0 || prev >= Main.maxNPCs || prev == NPC.whoAmI || !Main.npc[prev].active)
                        break;
                    cursor = prev;
                    depth++;
                }
                if (depth > bestDepth) {
                    bestDepth = depth;
                    deepest = seg;
                }
            }
            if (deepest == null)
                return;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.8f, Pitch = -0.2f }, deepest.Center);
                AoshunHelper.CreateThunderBurst(deepest.Center, 90f, 2, 10);
                for (int i = 0; i < 8; i++) {
                    var d = Dust.NewDustPerfect(deepest.Center, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(6, 6);
                    d.scale = 2f;
                }
                ACMUtils.AddScreenShake(2.5f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                deepest.life = 0;
                deepest.active = false; // netAlways 每帧同步, 客户端随即看到消失
            }
        }

        #endregion
    }
}

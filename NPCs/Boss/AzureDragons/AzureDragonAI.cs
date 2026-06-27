using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙头部 - AI招式实现
    /// </summary>
    public partial class AzureDragonHead
    {
        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(StateTimer / 180f, 0f, 1f);
            NPC.dontTakeDamage = true;

            // 从天而降，雷光环绕
            Vector2 introOffset = new Vector2(0, -800) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -400) + introOffset;
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            // 青蓝色雷光粒子
            if (!VaultUtils.isServer && StateTimer % 2 == 0) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }

                // 闪电火花
                if (Main.rand.NextBool(3)) {
                    Vector2 sparkPos = NPC.Center + Main.rand.NextVector2Circular(150, 150);
                    int spark = Dust.NewDust(sparkPos, 0, 0, DustID.Electric, 0, 0, 150, default, 1.5f);
                    Main.dust[spark].noGravity = true;
                }
            }

            if (StateTimer == 60) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }

            if (StateTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.8f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(14, 60);

                // 出场雷暴爆发
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 60; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(12, 12);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 80, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                    for (int i = 0; i < 20; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 50, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 180) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase1_Orbit);
            }
        }

        #endregion

        #region 一阶段 - 苍龙出海

        /// <summary>
        /// 盘旋巡弋 - 在玩家周围做8字盘旋，一段时间后切换到攻击招式
        /// </summary>
        private void RunPhase1Orbit(Player target) {
            orbitAngle += orbitSpeed;

            float R = 500f;
            float r = 180f;
            float h = 300f;
            float offsetX = R * MathF.Cos(orbitAngle);
            float offsetY = r * MathF.Sin(orbitAngle * 2f);

            Vector2 desiredPos = target.Center + new Vector2(offsetX, -h + offsetY);
            SmoothOrbit(desiredPos, 80f);

            // 盘旋时青色拖尾粒子
            if (!VaultUtils.isServer && StateTimer % 4 == 0) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, 0, 0, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.2f;
            }

            // 盘旋一定时间后切换攻击
            if (StateTimer > 180) {
                TransitionTo(PickPhase1Attack());
            }
        }

        /// <summary>
        /// 龙息吐息 - 朝玩家方向释放扇形青色能量弹幕
        /// </summary>
        private void RunPhase1DragonBreath(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力阶段
                    NPC.velocity *= 0.92f;

                    // 蓄力粒子向口部汇聚
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                        }
                    }

                    if (AttackTimer >= 60) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 30);
                    }
                    break;

                case 1: // 吐息阶段 - 连续发射能量弹
                    NPC.velocity *= 0.95f;

                    // 缓缓追踪玩家
                    Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity += toTarget * 0.3f;

                    // 每隔一定帧发射龙息弹
                    int fireInterval = Main.expertMode ? 6 : 8;
                    if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        float baseAngle = toTarget.ToRotation();
                        int bulletCount = 3;
                        float spread = MathHelper.ToRadians(25f);

                        for (int i = 0; i < bulletCount; i++) {
                            float angle = baseAngle + MathHelper.Lerp(-spread, spread, i / (float)(bulletCount - 1));
                            Vector2 vel = angle.ToRotationVector2() * 14f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), NPC.Center + toTarget * 40f, vel,
                                ModContent.ProjectileType<AzureBolt>(), NPC.damage / 4, 2f
                            );
                        }
                    }

                    // 吐息时青色烟雾粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustVel = toTarget.RotatedByRandom(0.4f) * Main.rand.NextFloat(4, 10);
                            int dust = Dust.NewDust(NPC.Center + toTarget * 30f, 0, 0, DustID.BlueTorch,
                                dustVel.X, dustVel.Y, 80, default, Main.rand.NextFloat(1.5f, 2.5f));
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 90) {
                        TransitionTo(AIState.Phase1_Orbit);
                    }
                    break;
            }
        }

        /// <summary>
        /// 地面雷柱 (V2 替换原雷球) — 在玩家附近标记若干落点, 约 1.5s 预告后劈下可读雷柱。
        /// 玩家须横向走位躲到未标记的安全列 (toolkit §C.1 AoE 预警三要素)。
        /// </summary>
        private void RunPhase1ThunderRods(Player target) {
            switch ((int)SubState) {
                case 0: // 悬停蓄力
                    Vector2 hoverPos = target.Center + new Vector2(0, -450);
                    SmoothOrbit(hoverPos, 50f);

                    if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 150, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                    }

                    if (AttackTimer >= 45) {
                        SubState = 1;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);

                        // 服务器决定落点(同步): 在玩家两侧标记雷柱, 留出安全缝隙
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int rodCount = Main.expertMode ? 5 : 4;
                            float spacing = 220f;
                            // 整体随机偏移, 使安全缝隙位置每次不同(可读但需要走位)
                            float baseX = target.Center.X + Main.rand.NextFloat(-110f, 110f);
                            for (int i = 0; i < rodCount; i++) {
                                float x = baseX + (i - (rodCount - 1) / 2f) * spacing;
                                Vector2 strikePos = new Vector2(x, target.Center.Y);
                                int telegraph = 90;   // ~1.5s
                                SpawnThunderRod(strikePos, telegraph + i * 6, 16);
                            }
                        }
                    }
                    break;

                case 1: // 等待落雷
                    NPC.velocity *= 0.9f;

                    if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 130) {
                        TransitionTo(AIState.Phase1_Orbit);
                    }
                    break;
            }
        }

        /// <summary>生成一根雷霆落雷柱(服务器权威, 投射物自动同步)。</summary>
        private void SpawnThunderRod(Vector2 strikePos, int telegraphTicks, int strikeActive) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(), strikePos, Vector2.Zero,
                ModContent.ProjectileType<AzureThunderRod>(), NPC.damage / 4, 3f,
                ai0: telegraphTicks, ai1: strikeActive);
        }

        /// <summary>
        /// 苍龙冲刺 - 向玩家高速冲刺，带有青色拖尾
        /// </summary>
        private void RunPhase1Charge(Player target) {
            switch ((int)SubState) {
                case 0: // 锁定准备
                    chargeCount = 0;
                    maxCharges = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 预告
                    NPC.velocity *= 0.85f;

                    // 冲刺方向指示粒子
                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 4; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (40 + i * 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 25) {
                        chargeTarget = target.Center + target.velocity * 10f;
                        chargeDirection = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = Main.expertMode ? 38f : 30f;
                        NPC.velocity = chargeDirection * chargeSpeed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 20);
                    }
                    break;

                case 2: // 冲刺中
                    // 青色冲刺拖尾
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 40f;
                            dustPos += Main.rand.NextVector2Circular(30, 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch,
                                0, 0, 60, default, Main.rand.NextFloat(2f, 3f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                        // 电弧火花
                        if (Main.rand.NextBool(2)) {
                            int spark = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 50, default, 1.5f);
                            Main.dust[spark].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 22) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            TransitionTo(AIState.Phase1_Orbit);
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换演出

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            // 雷暴聚拢特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(250, 250);
                    int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (StateTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0f, Volume = 2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 60);

                // 爆发电弧
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 80; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(15, 15);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                            vel.X, vel.Y, 50, default, Main.rand.NextFloat(2f, 3.5f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 90) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase2_StormChase);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            // 天威降临 - 更强烈的雷暴特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(300, 300);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 50, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 15f;
                }
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 80, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (StateTimer == 40) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 2.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 80);
            }

            if (StateTimer == 70) {
                // 全方位电弧爆发
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 120; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(18, 18);
                        int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, dustType,
                            vel.X, vel.Y, 40, default, Main.rand.NextFloat(2.5f, 4f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 100) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase3_ThunderTribunal);
            }
        }

        #endregion

        #region 律令辅助

        /// <summary>按当前雷霆律令把方向约束到单轴 (横扫=水平 / 纵贯=竖直)。</summary>
        private Vector2 EdictAxisToward(Vector2 from, Vector2 to, float speed) {
            Vector2 d = to - from;
            if (EdictHorizontal)
                return new Vector2(d.X >= 0 ? 1f : -1f, 0f) * speed;
            return new Vector2(0f, d.Y >= 0 ? 1f : -1f) * speed;
        }

        #endregion

        #region 二阶段 - 雷霆震怒

        /// <summary>
        /// 风暴追击 - 快速追踪玩家，同时释放闪电火花
        /// </summary>
        private void RunPhase2StormChase(Player target) {
            // 高速追踪
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float chaseSpeed = Main.expertMode ? 18f : 14f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * chaseSpeed, 0.06f);

            // 追踪时释放电弧粒子
            if (!VaultUtils.isServer && StateTimer % 3 == 0) {
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = Main.rand.NextVector2Circular(40, 40);
                    int dust = Dust.NewDust(NPC.Center + offset, 0, 0, DustID.Electric,
                        0, 0, 80, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -NPC.velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                }
            }

            // 雷霆律令: 弹道严格遵守当前律令轴 (横扫/纵贯), 由天闪提前告知
            int shootInterval = Main.expertMode ? 20 : 30;
            if (StateTimer % shootInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = EdictAxisToward(NPC.Center, target.Center, 12f);
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<AzureBolt>(), NPC.damage / 4, 2f
                );
            }

            if (StateTimer > 200) {
                TransitionTo(PickPhase2Attack());
            }
        }

        /// <summary>
        /// 闪电矩阵 - 在战场上布下电弧网格
        /// </summary>
        private void RunPhase2LightningMatrix(Player target) {
            switch ((int)SubState) {
                case 0: // 升空蓄力
                    Vector2 highPos = target.Center + new Vector2(0, -600);
                    SmoothOrbit(highPos, 40f);

                    if (!VaultUtils.isServer && AttackTimer % 2 == 0) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(80, 80);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 50, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                    }

                    if (AttackTimer >= 50) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 40);
                    }
                    break;

                case 1: // 释放闪电阵
                    NPC.velocity *= 0.9f;

                    if (AttackTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 律令矩阵: 沿当前律令轴的平行弹道阵 (横扫=多排水平 / 纵贯=多列竖直)
                        int lanes = Main.expertMode ? 7 : 5;
                        float laneSpacing = 165f;
                        float speed = 13f;
                        bool horiz = EdictHorizontal;
                        int type = ModContent.ProjectileType<AzureBolt>();
                        for (int i = 0; i < lanes; i++) {
                            float off = (i - (lanes - 1) / 2f) * laneSpacing;
                            Vector2 spawnPos, vel;
                            if (horiz) {
                                int side = i % 2 == 0 ? -1 : 1;
                                spawnPos = new Vector2(target.Center.X - side * 950f, target.Center.Y + off);
                                vel = new Vector2(side, 0f) * speed;
                            }
                            else {
                                spawnPos = new Vector2(target.Center.X + off, target.Center.Y - 950f);
                                vel = new Vector2(0f, 1f) * speed;
                            }
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, type, NPC.damage / 5, 1f);
                        }
                    }

                    // 矩阵闪烁特效
                    if (!VaultUtils.isServer && AttackTimer % 4 == 0) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = target.Center + Main.rand.NextVector2Circular(400, 400);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 80, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 80) {
                        TransitionTo(AIState.Phase2_StormChase);
                    }
                    break;
            }
        }

        /// <summary>
        /// 龙卷风暴 - 高速螺旋盘旋逼近玩家，留下电弧旋风
        /// </summary>
        private void RunPhase2TornadoSweep(Player target) {
            orbitAngle += 0.08f; // 极快的旋转

            float radius = MathHelper.Lerp(500f, 100f, MathHelper.Clamp(StateTimer / 180f, 0, 1));
            Vector2 desiredPos = target.Center + orbitAngle.ToRotationVector2() * radius;
            NPC.velocity = (desiredPos - NPC.Center) * 0.12f;

            // 旋风拖尾
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustVel = (-NPC.velocity).SafeNormalize(Vector2.Zero).RotatedByRandom(1f) * Main.rand.NextFloat(3, 8);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch,
                        dustVel.X, dustVel.Y, 80, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    int spark = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 50, default, 1.5f);
                    Main.dust[spark].noGravity = true;
                }
            }

            // 旋风过程中释放弹幕 (遵守律令轴)
            if (StateTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = EdictAxisToward(NPC.Center, target.Center, 10f);
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f
                );
            }

            if (StateTimer > 200) {
                TransitionTo(AIState.Phase2_StormChase);
            }
        }

        /// <summary>
        /// 急速连冲 - 多次极速冲刺，每次改变方向
        /// </summary>
        private void RunPhase2RapidCharge(Player target) {
            switch ((int)SubState) {
                case 0:
                    chargeCount = 0;
                    maxCharges = Main.expertMode ? 6 : 5;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 短暂锁定
                    NPC.velocity *= 0.8f;

                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (30 + i * 25);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 2.2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 16) {
                        chargeTarget = target.Center + target.velocity * 8f;
                        Vector2 toTargetVec = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float speed = Main.expertMode ? 45f : 36f;
                        NPC.velocity = toTargetVec * speed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 0.9f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 20);
                    }
                    break;

                case 2: // 冲刺
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 50f;
                            dustPos += Main.rand.NextVector2Circular(35, 35);
                            int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType,
                                0, 0, 50, default, Main.rand.NextFloat(2f, 3.5f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.08f;
                        }
                    }

                    if (AttackTimer >= 18) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            TransitionTo(AIState.Phase2_StormChase);
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 三阶段 - 天威降世

        /// <summary>
        /// 网格化雷霆审判庭 (V2 招牌 set-piece) — 替换原"随机落雷洪流"。
        /// 场地划为可读列网格, 每波按图案点亮"危险列"并劈下雷柱, 玩家走"安全列";
        /// 同时风域周期性把玩家横向推动, 走位与雷网联动。波次有限 (TribunalWaveCount),
        /// 满后强制转入移动招式 (消除"加速喷弹"反模式)。落点由服务器决定, 雷柱投射物自动同步。
        /// </summary>
        private void RunPhase3ThunderTribunal(Player target) {
            // 居高临下俯瞰审判庭; 竞技场中心锁定为入场时玩家位置
            if (State == AIState.Phase3_ThunderTribunal && StateTimer == 1) {
                ArenaCenter = target.Center;
                tribunalWave = 0;
            }
            Vector2 highPos = ArenaCenter + new Vector2(MathF.Sin(globalTime * 0.8f) * 180f, -560f);
            SmoothOrbit(highPos, 40f);

            // —— 风域: 周期性横向推力 (纯本地视觉/手感, 由同步的 WindDir 派生, 每端推自己玩家) ——
            ApplyWindField(target);

            const int gridColumns = 11;
            const float colSpacing = 165f;
            int telegraph = Main.expertMode ? 70 : 85;  // ~1.2~1.4s 处决级前摇
            const int strikeActive = 16;
            int wavePeriod = telegraph + strikeActive + 40;

            switch ((int)SubState) {
                case 0: // 蓄力 → 投放一波危险列
                    NPC.velocity *= 0.94f;

                    if (!VaultUtils.isServer && AttackTimer % 4 == 0) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(120, 120);
                        int dt = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                        int d = Dust.NewDust(dp, 0, 0, dt, 0, 0, 60, default, 2.2f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 6f;
                    }

                    if (AttackTimer >= 26) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            SpawnTribunalWave(tribunalWave, gridColumns, colSpacing, telegraph, strikeActive);

                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;

                case 1: // 等待本波雷柱解算
                    NPC.velocity *= 0.92f;

                    if (AttackTimer >= wavePeriod) {
                        tribunalWave++;
                        if (tribunalWave >= TribunalWaveCount) {
                            // 限幅: 强制转入移动招式
                            TransitionTo(AIState.Phase3_CelestialFury);
                        }
                        else {
                            SubState = 0;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>投放一波审判庭危险列 (按波次切换图案: 横扫 / 梳齿 / 向心收束)。</summary>
        private void SpawnTribunalWave(int wave, int columns, float spacing, int telegraph, int strikeActive) {
            float originX = ArenaCenter.X - (columns - 1) / 2f * spacing;
            float strikeY = ArenaCenter.Y;
            int pattern = wave % 3;

            for (int c = 0; c < columns; c++) {
                float x = originX + c * spacing;
                int delay;
                bool strike;
                switch (pattern) {
                    case 0: // 横扫: 从左到右逐列推进, 留 1 列动态安全缝
                        strike = true;
                        delay = c * 9;
                        break;
                    case 1: // 梳齿: 奇偶两小波 (先偶后奇), 安全列在另一组
                        strike = true;
                        delay = (c % 2) * 40;
                        break;
                    default: // 向心收束: 从两端向中央夹击, 中央最后留缝最短
                        strike = c != columns / 2;   // 正中央安全
                        delay = (int)(MathF.Abs(c - (columns - 1) / 2f) * -9 + (columns / 2) * 9);
                        break;
                }
                if (!strike)
                    continue;
                Vector2 strikePos = new Vector2(x, strikeY);
                SpawnThunderRod(strikePos, telegraph + Math.Max(0, delay), strikeActive);
            }
        }

        /// <summary>风域: 周期性横向推动本地玩家, 与雷网走位联动 (MP 安全: 每端推自己玩家)。</summary>
        private void ApplyWindField(Player target) {
            if (Main.dedServ)
                return;
            Player p = Main.LocalPlayer;
            if (!p.active || p.dead)
                return;
            // 仅在审判庭范围内施加, 强度温和保证公平
            if (Vector2.DistanceSquared(p.Center, ArenaCenter) > (ArenaRadius * 1.6f) * (ArenaRadius * 1.6f))
                return;
            float force = WindDir * 0.32f;
            p.velocity.X += force;

            // 风向预告: 顺风方向的青色风线
            if (Main.rand.NextBool(3)) {
                Vector2 dp = p.Center + new Vector2(Main.rand.NextFloat(-500f, 500f), Main.rand.NextFloat(-300f, 300f));
                int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, MathF.Sign(force) * 6f, 0, 120, default, 1.1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = new Vector2(MathF.Sign(force) * Main.rand.NextFloat(4f, 9f), 0f);
            }
        }

        /// <summary>
        /// 天怒龙卷 - 极速螺旋逼近+爆发电弧洪流
        /// </summary>
        private void RunPhase3CelestialFury(Player target) {
            float progress = MathHelper.Clamp(StateTimer / 240f, 0, 1);
            orbitAngle += 0.1f + progress * 0.05f;

            float radius = MathHelper.Lerp(600f, 60f, ACMUtils.SineInOut(progress));
            Vector2 desiredPos = target.Center + orbitAngle.ToRotationVector2() * radius;
            NPC.velocity = (desiredPos - NPC.Center) * 0.15f;

            // 极致旋风特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustVel = (-NPC.velocity).SafeNormalize(Vector2.Zero).RotatedByRandom(1.2f) *
                        Main.rand.NextFloat(4, 12);
                    int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                    int dust = Dust.NewDust(NPC.Center, 0, 0, dustType,
                        dustVel.X, dustVel.Y, 40, default, Main.rand.NextFloat(2f, 3.5f));
                    Main.dust[dust].noGravity = true;
                }
            }

            // 高频弹幕释放
            if (StateTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 12f;
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f
                );
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, -vel,
                    ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f
                );
            }

            // 到达最内圈时爆发
            if (progress > 0.9f && SubState == 0) {
                SubState = 1;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 60);

                // 爆发电弧
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi * i / 12f;
                        Vector2 vel = angle.ToRotationVector2() * 15f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<AzureBolt>(), NPC.damage / 4, 2f
                        );
                    }
                }

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 100; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(20, 20);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                            vel.X, vel.Y, 30, default, Main.rand.NextFloat(2.5f, 4f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 280) {
                TransitionTo(PickPhase3Attack());
            }
        }

        /// <summary>
        /// 龙升天击 - 极速升天后俯冲轰击，沿途留下电弧地带
        /// </summary>
        private void RunPhase3DragonAscent(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力升天
                    NPC.velocity.Y -= 1.5f;
                    NPC.velocity.X *= 0.95f;

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch,
                                Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(2, 6), 80, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 50) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.8f }, NPC.Center);
                    }
                    break;

                case 1: // 锁定俯冲
                    NPC.velocity *= 0.7f;

                    if (AttackTimer >= 15) {
                        chargeTarget = target.Center + target.velocity * 5f;
                        chargeDirection = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = chargeDirection * (Main.expertMode ? 55f : 45f);

                        SubState = 2;
                        AttackTimer = 0;
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 40);
                    }
                    break;

                case 2: // 俯冲轰击
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 60f;
                            dustPos += Main.rand.NextVector2Circular(40, 40);
                            int dustType = Main.rand.Next(3) switch {
                                0 => DustID.Electric,
                                1 => DustID.BlueTorch,
                                _ => DustID.IceTorch
                            };
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType,
                                0, 0, 30, default, Main.rand.NextFloat(2.5f, 4f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.05f;
                        }
                    }

                    // 俯冲路径上释放电弧
                    if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, perpendicular * 8f,
                            ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f
                        );
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, -perpendicular * 8f,
                            ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f
                        );
                    }

                    if (AttackTimer >= 25) {
                        // 着陆冲击
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 60; i++) {
                                Vector2 vel = Main.rand.NextVector2CircularEdge(15, 15);
                                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                                    vel.X, vel.Y, 30, default, Main.rand.NextFloat(2f, 3.5f));
                                Main.dust[dust].noGravity = true;
                            }
                        }
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 30);

                        TransitionTo(PickPhase3Attack());
                    }
                    break;
            }
        }

        #endregion
    }
}

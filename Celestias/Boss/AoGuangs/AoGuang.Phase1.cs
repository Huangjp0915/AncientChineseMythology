using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 潮弓三连 (P1)

        /// <summary>
        /// 潮弓三连 — 侧向蛇游蓄势 45f, 三波扇形潮矢 (波间 22f), 每波带后座反冲。
        /// 公平阀门: 潮矢初速 8 渐升(弹幕自加速), 扇形角距留穿越缝。
        /// </summary>
        private void RunTideBoltVolley(Player target) {
            float side = NPC.Center.X > target.Center.X ? 1f : -1f;
            Vector2 anchor = target.Center + new Vector2(side * 470f, -180f);

            switch ((int)SubState) {
                case 0: // 前摇 45f: 蛇游入位, 龙首聚水
                    SerpentineGlide(anchor, 0.06f, 0.09f, 3f);

                    // 汇聚流光: 水珠被吸向龙首 (预警形状 = 汇聚)
                    if (!VaultUtils.isServer && AttackTimer > 12) {
                        Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 60f;
                        for (int i = 0; i < 2; i++) {
                            Vector2 from = mouth + Main.rand.NextVector2CircularEdge(150f, 150f);
                            Dust d = Dust.NewDustDirect(from, 0, 0, DustID.Water, 0, 0, 100, default, 1.7f);
                            d.noGravity = true;
                            d.velocity = (mouth - from) * 0.085f;
                        }
                    }

                    if (AttackTimer >= 45) { SubState = 1; AttackTimer = 0; chargeCount = 0; }
                    break;

                case 1: // 三波扇形潮矢, 波间 22f
                    SerpentineGlide(anchor, 0.045f, 0.07f, 2f);

                    if (AttackTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 toPlayer = NPC.SafeDirectionTo(target.Center);
                            int count = Main.expertMode ? 7 : 5;
                            float spread = MathHelper.ToRadians(Main.expertMode ? 62f : 50f);
                            for (int i = 0; i < count; i++) {
                                float off = spread * (i / (count - 1f) - 0.5f);
                                // 初速 8, 弹幕自加速到 19 (转场缓速阀门)
                                Vector2 vel = toPlayer.RotatedBy(off) * 8f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    NPC.Center + toPlayer * 60f, vel,
                                    ModContent.ProjectileType<TridentProjectile>(), NPC.damage / 3, 1f);
                            }
                            // 后座反冲: 发射体也要往回顿 (质量=反作用)
                            NPC.velocity -= toPlayer * 9f;
                            NPC.netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.1f, Volume = 1.1f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
                        ACMUtils.AddScreenShake(5f);
                    }

                    if (AttackTimer >= 22) {
                        chargeCount++;
                        AttackTimer = 0;
                        if (chargeCount >= 3) { SubState = 2; }
                    }
                    break;

                case 2: // 收招 30f
                    SerpentineGlide(anchor, 0.03f, 0.06f, 2.4f);
                    if (AttackTimer >= 30) TransitionTo(BossPhase.Cruise);
                    break;
            }
        }

        #endregion

        #region 穿刺巡游 (P1/P2 共用)

        /// <summary>
        /// 穿刺巡游 — 龙形语言核心招: 盘旋入位 → 锁线(转向衰减) → 反吸 → 11f 雷霆穿刺 → 硬刹蛇游。
        /// 速度=对比: 全程只有 11 帧是快的。接触伤害窗口仅在穿刺帧开启。
        /// P1 ×2 @46px/f; P2 ×3 @50px/f。
        /// </summary>
        private void RunPierceRun(Player target) {
            float pierceSpeed = IsPhase2 ? 50f : 46f;
            maxChargeCount = IsPhase2 ? 3 : 2;

            switch ((int)SubState) {
                case 0: // 盘旋入位 (~40f, 到位提前退出)
                    {
                        float side = NPC.Center.X > target.Center.X ? 1f : -1f;
                        Vector2 anchor = target.Center + new Vector2(side * 560f, -140f);
                        SerpentineGlide(anchor, 0.075f, 0.1f, 3.4f);
                        if (AttackTimer >= 40 || (AttackTimer > 14 && NPC.Distance(anchor) < 130f)) {
                            SubState = 1;
                            AttackTimer = 0;
                            // 预告哨音: 固定在发射前 36f (锁线 24 + 反吸 12)
                            SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.4f, Volume = 0.9f }, NPC.Center);
                        }
                    }
                    break;

                case 1: // 锁线 24f: 转向速率衰减, 龙王"咬死"这条线
                    {
                        NPC.velocity *= 0.86f;
                        float t = AttackTimer / 24f;
                        // 前段各端确定性追预测点 (target 已同步), 锁定帧服务器 netUpdate 纠偏;
                        // 后段锁死 (锁定后玩家的位移就是躲闪答案)
                        if (AttackTimer <= 14) {
                            chargeTarget = target.Center + target.velocity * 13f;
                            if (AttackTimer == 14 && Main.netMode != NetmodeID.MultiplayerClient)
                                NPC.netUpdate = true;
                        }
                        Vector2 aimDir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.rotation = NPC.rotation.AngleLerp(aimDir.ToRotation(), MathHelper.Lerp(0.4f, 0.08f, t));
                        NPC.spriteDirection = aimDir.X >= 0 ? 1 : -1;
                        poseRotOverride = NPC.rotation; // 锁姿态, 不被速度旋转覆盖

                        // 致命穿刺线预警 (红=致命, 渐强)
                        if (!VaultUtils.isServer) {
                            int count = 5 + (int)(AttackTimer / 4f);
                            for (int i = 0; i < count; i++) {
                                Vector2 dustPos = NPC.Center + aimDir * (70 + i * 52);
                                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.RedTorch, 0, 0, 100,
                                    TelegraphColors.Lethal, 1.5f);
                                d.noGravity = true;
                                d.velocity = Vector2.Zero;
                            }
                        }

                        if (AttackTimer >= 24) { SubState = 2; AttackTimer = 0; }
                    }
                    break;

                case 2: // 反吸 12f: 后段猛然倒吸 (爆发前的深呼吸)
                    {
                        Vector2 aimDir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                        float t = AttackTimer / 12f;
                        NPC.velocity = -aimDir * MathF.Pow(t, 3f) * 15f;
                        poseRotOverride = aimDir.ToRotation();

                        if (AttackTimer >= 12) {
                            SubState = 3;
                            AttackTimer = 0;
                            // 单帧点火: 速度是"设定"不是"加速"
                            NPC.velocity = aimDir * pierceSpeed;
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.55f, Volume = 0.9f }, NPC.Center);
                            ACMUtils.AddScreenShake(8f);
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case 3: // 穿刺 11f: 无转向, 接触伤害窗口开启
                    contactDamageActive = true;

                    // 动能粒子: 数量/速度 ∝ 速度
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (30 + i * 22)
                                + Main.rand.NextVector2Circular(24, 24);
                            Dust d = Dust.NewDustDirect(dustPos, 0, 0,
                                Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch, 0, 0, 90, default, 2.6f);
                            d.noGravity = true;
                            d.velocity = -NPC.velocity * 0.12f;
                        }
                    }

                    // P2 强化: 穿刺途中甩落慢速水珠 (低初速渐升, 不是弹幕墙)
                    if (IsPhase2 && AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perp = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        int side = AttackTimer % 8 == 0 ? 1 : -1;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                            perp * side * 3.5f, ModContent.ProjectileType<DragonWaterBolt>(),
                            NPC.damage / 4, 1f);
                    }

                    if (AttackTimer >= 11) { SubState = 4; AttackTimer = 0; }
                    break;

                case 4: // 硬刹 + 恢复蛇游 26f (刺间可读窗口)
                    if (AttackTimer <= 10)
                        NPC.velocity *= 0.72f; // 「砸进位置」的急刹
                    else
                        SerpentineGlide(NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitX) * 100f, 0.02f, 0.05f, 3f);

                    if (AttackTimer >= 26) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            TransitionTo(BossPhase.Cruise);
                        }
                        else {
                            SubState = 0;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 潮涌立柱 (P1)

        /// <summary>
        /// 潮涌立柱 — 定身抬首号令 40f, 一排水柱**自一侧向另一侧波浪推进**依次喷发
        /// (间隔 8f, 每根自带 36f 红色警戒柱)。推进方向本身就是可读性: "潮从哪边来"。
        /// 公平阀门: 落点做地面探测, 柱间 170px 必留缝, 依次喷发可顺序躲避。
        /// </summary>
        private void RunSurgePillars(Player target) {
            Vector2 anchor = target.Center + new Vector2(0, -430f);

            switch ((int)SubState) {
                case 0: // 前摇 40f: 升至上方, 抬首蓄势
                    SerpentineGlide(anchor, 0.07f, 0.1f, 2f);
                    if (AttackTimer > 16)
                        poseRotOverride = PoseAngle(-0.35f, NPC.spriteDirection);

                    // 地面泡沫涌动预告 (形状预警先于红色警戒)
                    if (!VaultUtils.isServer && AttackTimer > 10 && AttackTimer % 3 == 0) {
                        Vector2 foamPos = target.Center + new Vector2(Main.rand.NextFloat(-450f, 450f), 60f);
                        Dust d = Dust.NewDustDirect(foamPos, 0, 0, DustID.Wet, 0, -2f, 160, default, 1.4f);
                        d.noGravity = true;
                    }

                    if (AttackTimer == 20)
                        SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.5f, Volume = 1.1f }, NPC.Center);

                    if (AttackTimer >= 40) {
                        SubState = 1;
                        AttackTimer = 0;

                        // 一排立柱自随机一侧起波浪推进 (交错延时由弹幕自身倒计时), 落点做地面探测
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int count = Main.expertMode ? 8 : 7;
                            float spacing = 170f;
                            float dir = Main.rand.NextBool() ? 1f : -1f; // 潮从哪边来
                            float startX = target.Center.X - dir * spacing * (count / 2f);
                            for (int i = 0; i < count; i++) {
                                float x = startX + dir * spacing * i;
                                float groundY = FindGroundY(x, target.Center.Y - 200f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    new Vector2(x, groundY), Vector2.Zero,
                                    ModContent.ProjectileType<WaterSpike>(), NPC.damage / 3, 1f,
                                    ai0: i * 8f); // 波浪推进
                            }
                            NPC.netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.1f }, target.Center);
                    }
                    break;

                case 1: // 立柱波浪推进期间: 龙王缓慢横移压场
                    SerpentineGlide(anchor + new Vector2(MathF.Sin(globalTime * 1.4f) * 160f, 0), 0.045f, 0.07f, 2f);

                    // 末根延时 56f + 柱全程 ~102f → 170f 保底退出
                    if (AttackTimer >= 170)
                        TransitionTo(BossPhase.Cruise);
                    break;
            }
        }

        /// <summary>从 startY 向下寻找地面 (拿不到则回退到 startY+420)。供本 Boss 及其弹幕使用。</summary>
        internal static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = Math.Max(1, (int)(searchStartY / 16f));
            for (int tileY = startTileY; tileY < Math.Min(startTileY + 80, Main.maxTilesY - 1); tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return searchStartY + 420f;
        }

        #endregion
    }
}

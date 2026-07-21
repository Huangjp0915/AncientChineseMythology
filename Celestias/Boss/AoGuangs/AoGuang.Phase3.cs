using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 深渊漩涡 (P3 签名 set-piece)

        /// <summary>
        /// 深渊漩涡 — 定点巨涡 (吸力 4s 渐强, 峰值 0.35, 红环即碰撞边界) +
        /// 龙王沿切向连续穿刺, 穿刺路径穿过绕涡走位的玩家轨道。
        /// 全屏向心折射把"被吸向中心"做成可读压力。
        /// </summary>
        private void RunAbyssalMaw(Player target) {
            switch ((int)SubState) {
                case 0: // 升空入位 40f (到位提前退出)
                    {
                        Vector2 anchor = target.Center + new Vector2(0, -500f);
                        SerpentineGlide(anchor, 0.08f, 0.12f, 2.5f);
                        if (AttackTimer >= 40 || (AttackTimer > 12 && NPC.Distance(anchor) < 110f)) {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;

                case 1: // 落涡 + 成型 60f
                    if (AttackTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            chargeTarget = target.Center + new Vector2(0, 40f); // 漩涡定点 (不追踪)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), chargeTarget, Vector2.Zero,
                                ModContent.ProjectileType<AbyssalVortex>(), NPC.damage / 3, 0f,
                                ai0: NPC.whoAmI);
                            NPC.netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.55f, Volume = 1.5f }, target.Center);
                        ACMUtils.AddScreenShake(11f);
                        waterBloom = 1f;
                    }
                    SerpentineGlide(chargeTarget + new Vector2(0, -560f), 0.05f, 0.08f, 2.5f);
                    if (AttackTimer >= 60) { SubState = 2; AttackTimer = 0; chargeCount = 0; }
                    break;

                case 2: // 切向穿刺循环 ×3: 锁线 22f → 刺 12f → 刹 22f
                    {
                        float cycle = 56f;
                        float inCycle = AttackTimer % cycle;

                        if (inCycle < 22f) { // 锁线: 瞄准漩涡另一侧的切向路径
                            NPC.velocity *= 0.88f;
                            if (inCycle == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                                // 穿刺线 = 过漩涡边缘的切线弦 (逼玩家离开绕涡轨道); 角度经 breathAngle 同步
                                float chordAng = (target.Center - chargeTarget).ToRotation()
                                    + Main.rand.NextFloat(-0.5f, 0.5f);
                                Vector2 chordPt = chargeTarget + chordAng.ToRotationVector2() * 190f;
                                breathAngle = (chordPt - NPC.Center).SafeNormalize(Vector2.UnitY).ToRotation();
                                NPC.netUpdate = true;
                            }
                            float aim = breathAngle;
                            poseRotOverride = aim;
                            NPC.spriteDirection = MathF.Cos(aim) >= 0 ? 1 : -1;

                            if (inCycle == 2)
                                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);

                            if (!VaultUtils.isServer) {
                                Vector2 aimDir = aim.ToRotationVector2();
                                int count = 4 + (int)(inCycle / 4f);
                                for (int i = 0; i < count; i++) {
                                    Dust d = Dust.NewDustDirect(NPC.Center + aimDir * (70 + i * 55), 0, 0,
                                        DustID.RedTorch, 0, 0, 110, TelegraphColors.Lethal, 1.4f);
                                    d.noGravity = true;
                                    d.velocity = Vector2.Zero;
                                }
                            }
                            // 末 6f 反吸
                            if (inCycle >= 16f)
                                NPC.velocity = -aim.ToRotationVector2() * MathF.Pow((inCycle - 16f) / 6f, 3f) * 13f;
                        }
                        else if (inCycle < 34f) { // 穿刺 12f
                            if (inCycle == 22f) {
                                NPC.velocity = breathAngle.ToRotationVector2() * 48f;
                                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.6f, Volume = 0.9f }, NPC.Center);
                                ACMUtils.AddScreenShake(8f);
                            }
                            contactDamageActive = true;
                            if (!VaultUtils.isServer) {
                                for (int i = 0; i < 4; i++) {
                                    Dust d = Dust.NewDustDirect(
                                        NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (30 + i * 26)
                                        + Main.rand.NextVector2Circular(22, 22), 0, 0,
                                        Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch, 0, 0, 90, default, 2.5f);
                                    d.noGravity = true;
                                    d.velocity = -NPC.velocity * 0.1f;
                                }
                            }
                        }
                        else { // 硬刹恢复
                            NPC.velocity *= 0.75f;
                        }

                        if (inCycle >= cycle - 1f) {
                            chargeCount++;
                            if (chargeCount >= 3) { SubState = 3; AttackTimer = 0; }
                        }
                    }
                    break;

                case 3: // 漩涡崩解收尾 (等待漩涡自灭)
                    SerpentineGlide(target.Center + new Vector2(0, -380f), 0.05f, 0.08f, 2.5f);
                    if (AttackTimer >= 70) {
                        waterBloom = MathF.Max(waterBloom, 0.6f);
                        TransitionTo(BossPhase.Cruise);
                    }
                    break;
            }
        }

        #endregion

        #region 终潮天倾 (P3 终极, 30% 以下才见)

        /// <summary>
        /// 终潮天倾 — 压箱底招: 升天 → 半场 Lethal 幕布预警 60f (加速读秒) → 半场天倾巨浪砸落 →
        /// 反向再一次 → 龙王贯场穿刺收尾。被扣到 30% 以下才解锁的"未来承诺"。
        /// </summary>
        private void RunSkyfallTide(Player target) {
            switch ((int)SubState) {
                case 0: // 升空 50f: 龙王冲天而起
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.4f }, NPC.Center);
                        NPC.velocity = new Vector2(0, -26f);
                    }
                    NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -20f), 0.08f);

                    if (!VaultUtils.isServer) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 80), 0, 0,
                            DustID.Water, 0, 5f, 100, default, 2.2f);
                        d.noGravity = false;
                    }

                    if (AttackTimer >= 50) { SubState = 1; AttackTimer = 0; }
                    break;

                case 1: // 第一次半场标记 60f → 天倾
                case 2: // 第二次 (反向)
                    {
                        bool first = (int)SubState == 1;
                        if (AttackTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                            // 危险半场 = 玩家所在半场 (以标记瞬间玩家为准, 逼迫换场)
                            wallDir = first
                                ? (target.Center.X >= NPC.Center.X ? 1f : -1f)
                                : -wallDir;
                            chargeTarget = new Vector2(NPC.Center.X, target.Center.Y); // 分界线 X 锚点
                            NPC.netUpdate = true;

                            // 天倾浪体自带 60f 预警幕布 + 坠落 (分界线 = 生成点 X)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                new Vector2(chargeTarget.X, target.Center.Y - 1100f), Vector2.Zero,
                                ModContent.ProjectileType<AoGuangSkyDeluge>(), NPC.damage / 2, 2f,
                                ai0: wallDir, ai1: target.Center.Y);
                        }

                        // 龙王悬于分界线上空压场
                        Vector2 anchor = new Vector2(chargeTarget.X, target.Center.Y - 620f);
                        SerpentineGlide(anchor, 0.06f, 0.09f, 3f);

                        // 标记期 60f: 加速读秒鼓点 (间隔递减)
                        if (AttackTimer == 6 || AttackTimer == 26 || AttackTimer == 41 ||
                            AttackTimer == 51 || AttackTimer == 57) {
                            float pitch = -0.2f + AttackTimer / 57f * 0.6f;
                            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = pitch, Volume = 1f }, target.Center);
                        }

                        // 坠落瞬间的世界反馈
                        if (AttackTimer == 61) {
                            ACMUtils.AddScreenShake(13f);
                            waterBloom = 1f;
                        }

                        // 落浪后 40f 换场窗口
                        if (AttackTimer >= 100) {
                            if (first) { SubState = 2; AttackTimer = 0; }
                            else { SubState = 3; AttackTimer = 0; }
                        }
                    }
                    break;

                case 3: // 贯场穿刺收尾: 锁线 30f → 全场横贯
                    {
                        if (AttackTimer < 30) {
                            float sideX = NPC.Center.X > target.Center.X ? 1f : -1f;
                            Vector2 anchor = new Vector2(target.Center.X + sideX * 900f, target.Center.Y);
                            SerpentineGlide(anchor, 0.09f, 0.13f, 2f);

                            if (AttackTimer == 6)
                                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.5f, Volume = 1f }, NPC.Center);

                            if (AttackTimer <= 22) {
                                chargeTarget = target.Center + target.velocity * 10f;
                                if (AttackTimer == 22 && Main.netMode != NetmodeID.MultiplayerClient)
                                    NPC.netUpdate = true;
                            }

                            Vector2 aimDir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                            if (AttackTimer > 14) {
                                poseRotOverride = aimDir.ToRotation();
                                NPC.spriteDirection = aimDir.X >= 0 ? 1 : -1;
                            }

                            if (!VaultUtils.isServer && AttackTimer > 10) {
                                for (int i = 0; i < 10; i++) {
                                    Dust d = Dust.NewDustDirect(NPC.Center + aimDir * (80 + i * 90), 0, 0,
                                        DustID.RedTorch, 0, 0, 110, TelegraphColors.Lethal, 1.6f);
                                    d.noGravity = true;
                                }
                            }
                        }
                        else if (AttackTimer == 30) {
                            NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX) * 54f;
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.7f, Volume = 1.1f }, NPC.Center);
                            ACMUtils.AddScreenShake(10f);
                        }
                        else if (AttackTimer <= 46) {
                            contactDamageActive = true;
                            if (!VaultUtils.isServer) {
                                for (int i = 0; i < 6; i++) {
                                    Dust d = Dust.NewDustDirect(
                                        NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (26 + i * 24)
                                        + Main.rand.NextVector2Circular(26, 26), 0, 0,
                                        Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch, 0, 0, 80, default, 2.8f);
                                    d.noGravity = true;
                                    d.velocity = -NPC.velocity * 0.12f;
                                }
                            }
                        }
                        else {
                            NPC.velocity *= 0.78f;
                        }

                        if (AttackTimer >= 74) TransitionTo(BossPhase.Cruise);
                    }
                    break;
            }
        }

        #endregion

        #region 狂龙连刺 (P3)

        /// <summary>
        /// 狂龙连刺 — 三连极速穿刺 (52px/f, 锁线 22f + 反吸 10f + 刺 9f), 第三刺终点浪爆:
        /// 环形潮矢 (初速渐升) + 冲击环。刺间 26f 恢复拍。
        /// </summary>
        private void RunFuryPierce(Player target) {
            switch ((int)SubState) {
                case 0: // 锁线 22f
                    {
                        NPC.velocity *= 0.86f;
                        // 各端确定性追预测点, 锁定帧服务器纠偏
                        if (AttackTimer <= 13) {
                            chargeTarget = target.Center + target.velocity * 9f;
                            if (AttackTimer == 13 && Main.netMode != NetmodeID.MultiplayerClient)
                                NPC.netUpdate = true;
                        }
                        Vector2 aimDir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                        float t = AttackTimer / 22f;
                        NPC.rotation = NPC.rotation.AngleLerp(aimDir.ToRotation(), MathHelper.Lerp(0.45f, 0.1f, t));
                        NPC.spriteDirection = aimDir.X >= 0 ? 1 : -1;
                        poseRotOverride = NPC.rotation;

                        if (AttackTimer == 1)
                            SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.6f, Volume = 0.85f }, NPC.Center);

                        if (!VaultUtils.isServer) {
                            int count = 6 + (int)(AttackTimer / 3f);
                            for (int i = 0; i < count; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + aimDir * (60 + i * 46), 0, 0,
                                    DustID.RedTorch, 0, 0, 100, TelegraphColors.Lethal, 1.6f);
                                d.noGravity = true;
                                d.velocity = Vector2.Zero;
                            }
                        }

                        // 末 10f 反吸
                        if (AttackTimer >= 12)
                            NPC.velocity = -aimDir * MathF.Pow((AttackTimer - 12f) / 10f, 3f) * 16f;

                        if (AttackTimer >= 22) {
                            SubState = 1;
                            AttackTimer = 0;
                            NPC.velocity = aimDir * 52f;
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.7f, Volume = 1f }, NPC.Center);
                            ACMUtils.AddScreenShake(9f);
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case 1: // 穿刺 9f
                    contactDamageActive = true;
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 7; i++) {
                            Dust d = Dust.NewDustDirect(
                                NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (26 + i * 22)
                                + Main.rand.NextVector2Circular(28, 28), 0, 0,
                                Main.rand.Next(3) switch {
                                    0 => DustID.Water,
                                    1 => DustID.BlueTorch,
                                    _ => DustID.Wet
                                }, 0, 0, 80, default, 2.8f);
                            d.noGravity = true;
                            d.velocity = -NPC.velocity * 0.14f;
                        }
                    }

                    if (AttackTimer >= 9) {
                        chargeCount++;
                        SubState = 2;
                        AttackTimer = 0;

                        // 第三刺终点浪爆
                        if (chargeCount >= 3) {
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                int count = Main.expertMode ? 12 : 10;
                                for (int i = 0; i < count; i++) {
                                    float ang = MathHelper.TwoPi * i / count;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                        ang.ToRotationVector2() * 7.5f,
                                        ModContent.ProjectileType<DragonWaterBolt>(), NPC.damage / 4, 1f);
                                }
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<TidalWave>(), NPC.damage / 3, 1f);
                            }
                            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 1.4f }, NPC.Center);
                            ACMUtils.AddScreenShake(11f);
                            waterBloom = 1f;
                            tidalRingVisual = 1f;
                        }
                    }
                    break;

                case 2: // 硬刹 + 刺间恢复 26f
                    if (AttackTimer <= 9)
                        NPC.velocity *= 0.72f;
                    else
                        NPC.velocity *= 0.95f;

                    if (AttackTimer >= 26) {
                        if (chargeCount >= 3) {
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
    }
}

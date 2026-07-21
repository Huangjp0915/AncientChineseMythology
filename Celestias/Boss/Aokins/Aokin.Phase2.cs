using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 狂怒连冲 — 反向拖拽 → 单帧爆发 → 硬刹（V3 重做）

        /// <summary>
        /// 狂怒连冲 V3：每冲一轮 = 30f 反向拖拽蓄势（pow⁸ 后吸 + 冲刺线预警 + 定音）
        /// → 单帧 set 62px/f 直线 10f（零转向, speed is contrast）→ ×0.68/f 硬刹 14f。
        /// 冲刺/刹车窗口才有接触伤害；刹车即玩家的反击窗。
        /// </summary>
        private bool RunFuryCharge(Player target) {
            const int AnticipationTime = 30;
            const int DashTime = 10;
            const int BrakeTime = 14;

            switch (subState) {
                case 0:
                    chargeCount = 0;
                    maxChargeCount = (IsPhase3 ? 4 : (Main.expertMode ? 4 : 3)) + (rageActive ? 1 : 0);
                    subState = 1;
                    attackTimer = 0;
                    break;

                case 1: { // 反向拖拽蓄势
                    float t = attackTimer / (float)AnticipationTime;

                    // 定音: 固定提前量的预警音（玩家可内化节奏）
                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.5f, Volume = 0.75f }, NPC.Center);

                    // 锁定预测点（后段锁死, 给出可读的直线）
                    if (attackTimer <= AnticipationTime - 6)
                        chargeTarget = target.Center + target.velocity * 12f;

                    Vector2 dir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                    // pow⁸ 后吸: 大部分时间静止, 最后几帧猛然向后吸气
                    Vector2 reel = -dir * MathF.Pow(t, 8f) * 130f;
                    Vector2 anchor = NPC.Center + reel;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, reel * 0.25f, 0.2f);
                    NPC.velocity *= 0.88f;

                    rotationLocked = true;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, dir.ToRotation(), 0.2f);
                    NPC.spriteDirection = dir.X >= 0 ? 1 : -1;

                    chargeTelegraphT = Math.Max(chargeTelegraphT, 0.25f + t * 0.75f);

                    if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                        Vector2 mouth = NPC.Center + dir * 55f;
                        AokinHelper.CreateConvergingEmbers(mouth, t * 0.6f, 120f);
                    }

                    if (attackTimer >= AnticipationTime) {
                        // 单帧 set（launch is a set, not a ramp）
                        Vector2 launchDir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float speed = (IsPhase3 ? 66f : 62f) + (rageActive ? 6f : 0f);
                        NPC.velocity = launchDir * speed;
                        subState = 2;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.35f, Volume = 0.9f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                        if (!VaultUtils.isServer)
                            AokinHelper.CreateDragonFireBurst(NPC.Center, 120f, 2, 12);
                        NPC.netUpdate = true;
                    }
                    break;
                }

                case 2: { // 冲刺: 直线零转向
                    bodyContactWindow = true;

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (30f + i * 22f);
                            dustPos += Main.rand.NextVector2Circular(22, 22);
                            int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                            var d = Dust.NewDustPerfect(dustPos, dustType, -NPC.velocity * 0.1f, 100, default, 2.4f);
                            d.noGravity = true;
                        }
                    }

                    // 侧向火幕（冲刺留下的持续威胁, 低速可读）
                    if (attackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                perpendicular * side * 3.6f,
                                ModContent.ProjectileType<AokinFireball>(), contactDamageBase / 4, 1f);
                        }
                    }

                    if (attackTimer >= DashTime) {
                        subState = 3;
                        attackTimer = 0;
                    }
                    break;
                }

                case 3: { // 硬刹（slam into position）
                    if (attackTimer <= 6)
                        bodyContactWindow = true;
                    NPC.velocity *= 0.68f;

                    if (attackTimer >= BrakeTime) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            AddHeat(20f);
                            return true;
                        }
                        subState = 1;
                        attackTimer = 0;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion

        #region 炎龙卷舞 — 环绕漂移火龙卷

        /// <summary>
        /// 炎龙卷舞（原烈焰旋涡重做）：预告后错峰落下 3~4 座着色器火龙卷, 绕玩家极缓公转 + 缓慢收拢。
        /// 龙卷本体大而慢 = 自带预警; Boss 放完即回巡游, 龙卷作为余压场景存留（~5s）。
        /// </summary>
        private bool RunFlameVortex(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            int vortexCount = Main.expertMode ? 4 : 3;

            // 生成点预告（金色收束光, 非致命色）
            if (attackTimer > 10 && attackTimer < 30 && !VaultUtils.isServer) {
                for (int i = 0; i < vortexCount; i++) {
                    float angle = MathHelper.TwoPi * i / vortexCount;
                    Vector2 spawnPos = target.Center + angle.ToRotationVector2() * 260f;
                    if (Main.rand.NextBool(2))
                        AokinHelper.CreateConvergingEmbers(spawnPos, 0.5f, 90f);
                }
            }

            // 错峰落卷（波纹感）
            if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer >= 30 && attackTimer < 30 + vortexCount * 8
                && (attackTimer - 30) % 8 == 0) {
                int i = (attackTimer - 30) / 8;
                float angle = MathHelper.TwoPi * i / vortexCount;
                Vector2 spawnPos = target.Center + angle.ToRotationVector2() * 260f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<AokinFireTornadoProj>(), contactDamageBase / 4, 1f,
                    Main.myPlayer, ai0: angle, ai1: NPC.whoAmI);
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f + i * 0.08f, Volume = 0.8f }, spawnPos);
            }

            // 龙卷落齐后 Boss 即收招回巡游（龙卷自续 ~5s, 无死等）
            if (attackTimer > 110) {
                AddHeat(16f);
                return true;
            }
            return false;
        }

        #endregion

        #region 蒸汽龙炮 — 长聚气单发大弹（V3 新招）

        /// <summary>
        /// 蒸汽龙炮：70f 聚气（向心蒸汽密度=进度条, 72% 处静默 = 爆前吸气）→ 单发大蒸汽熔球
        /// （22px/f, 机体后坐 18px/f）→ 熔球超时/近身爆散 8 向蒸汽弹。
        /// 最小发射距离 260px：贴脸可诱使他憋炮（公平阀门兼策略层）。
        /// </summary>
        private bool RunSteamCannon(Player target) {
            const int ChargeTime = 70;
            const int RecoverTime = 30;

            switch (subState) {
                case 0: { // 聚气
                    float t = attackTimer / (float)ChargeTime;
                    Vector2 toPlayer = target.Center - NPC.Center;

                    // 站位: 侧上方 520px; 玩家贴脸(<300)则缓慢退让维持最小发射距离
                    Vector2 anchor = target.Center + new Vector2(NPC.Center.X < target.Center.X ? -520f : 520f, -180f);
                    float approach = toPlayer.Length() < 300f ? 0.10f : 0.05f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * approach, 0.09f);

                    rotationLocked = true;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, toPlayer.ToRotation(), 0.1f);
                    NPC.spriteDirection = toPlayer.X >= 0 ? 1 : -1;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.8f, Volume = 1.1f }, NPC.Center);
                    if (attackTimer == 42)
                        SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.1f, Volume = 0.9f }, NPC.Center);

                    breathGlow = Math.Max(breathGlow, t);
                    // 聚气密度 = 进度条, 72% 截止 → 最后的静默
                    if (!VaultUtils.isServer && t < 0.72f) {
                        Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 58f;
                        AokinHelper.CreateConvergingEmbers(mouth, MathF.Sqrt(t), 280f, 1.5f);
                    }
                    ACMUtils.AddScreenShake(t * t * 2.6f);

                    if (attackTimer >= ChargeTime) {
                        // 发射: 单帧, 后坐 18px/f（heavy shell launch）
                        Vector2 aim = ACMUtils.LeadTarget(NPC.Center, target.Center, target.velocity, 22f);
                        // 最小发射距离: 贴脸则朝头顶方向抬升出射角, 不给贴脸秒杀
                        if (toPlayer.Length() < 260f)
                            aim = (aim - Vector2.UnitY * 0.8f).SafeNormalize(Vector2.UnitY);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                NPC.Center + aim * 60f, aim * 22f,
                                ModContent.ProjectileType<AokinSteamOrb>(),
                                contactDamageBase / 3, 3f, Main.myPlayer,
                                ai0: contactDamageBase / 4);

                            // 沸海间歇泉齐鸣：脚下 3 座蒸汽柱错峰喷发, 逼玩家在躲弹同时穿缝
                            float geyserBaseY = target.Bottom.Y + 20f;
                            for (int g = -1; g <= 1; g++) {
                                Vector2 pos = new Vector2(target.Center.X + g * 190f + Main.rand.NextFloat(-25f, 25f), geyserBaseY);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    pos, Vector2.Zero,
                                    ModContent.ProjectileType<AokinScaldGeyser>(),
                                    Main.expertMode ? 42 : 55, 2f, Main.myPlayer,
                                    ai0: 42 + (g + 1) * 9, ai1: 1f);
                            }
                        }

                        NPC.velocity = -aim * 18f;
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.3f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(9f);
                        lavaBloom = Math.Max(lavaBloom, 0.45f);
                        if (!VaultUtils.isServer) {
                            AokinHelper.CreateSteamBurst(NPC.Center + aim * 60f, 100f, 30);
                            AokinHelper.CreateDragonFireBurst(NPC.Center + aim * 60f, 90f, 2, 12);
                        }

                        subState = 1;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { // 收招: 后坐衰减
                    NPC.velocity *= 0.92f;
                    if (attackTimer >= RecoverTime) {
                        AddHeat(20f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion

        #region 烈焰俯冲 — 屏内蓄势 + 垂直红线（V3 重做）

        /// <summary>
        /// 烈焰俯冲 V3：升至屏内高点（-520px, 全程可见）→ 18f 悬停震颤 + 垂直致命红线标记落点
        /// → 单帧 set 46px/f 垂直俯冲（线锁定后不再追踪）→ 落地冲击（震屏 + 两侧低空火浪）→ 硬刹收招。
        /// </summary>
        private bool RunDivebomb(Player target) {
            switch (subState) {
                case 0: { // 上升占位（屏内, 玩家全程可见）
                    Vector2 skyTarget = new Vector2(
                        target.Center.X + (NPC.Center.X < target.Center.X ? -170f : 170f),
                        target.Center.Y - 520f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (skyTarget - NPC.Center) * 0.09f, 0.14f);

                    if (Vector2.Distance(NPC.Center, skyTarget) < 60f || attackTimer > 50) {
                        subState = 1;
                        attackTimer = 0;
                        diveTelegraphX = target.Center.X;
                        SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.4f, Volume = 0.9f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { // 悬停震颤 + 红线锁定
                    NPC.velocity *= 0.85f;
                    float t = attackTimer / 18f;
                    diveTelegraphT = Math.Max(diveTelegraphT, 0.3f + t * 0.7f);

                    // 前 12f 缓慢追踪, 之后锁死（给出可读的直线逃逸窗）
                    if (attackTimer <= 12)
                        diveTelegraphX = MathHelper.Lerp(diveTelegraphX, target.Center.X, 0.12f);

                    // 蓄力震颤渐强
                    if (attackTimer > 6)
                        NPC.Center += Main.rand.NextVector2Circular(t * 3f, t * 3f);

                    // 横移对齐红线
                    NPC.velocity.X += MathHelper.Clamp((diveTelegraphX - NPC.Center.X) * 0.06f, -5f, 5f);

                    if (attackTimer >= 18) {
                        // 单帧 set 俯冲
                        NPC.velocity = new Vector2(
                            MathHelper.Clamp((diveTelegraphX - NPC.Center.X) * 0.05f, -6f, 6f),
                            IsPhase3 ? 50f : 46f);
                        subState = 2;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: { // 俯冲
                    bodyContactWindow = true;
                    diveTelegraphT = Math.Max(diveTelegraphT, 0.9f);

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                            var d = Dust.NewDustPerfect(dustPos, DustID.SolarFlare, new Vector2(0, -7f), 100, default, 3f);
                            d.noGravity = true;
                        }
                    }

                    if (NPC.Center.Y > target.Center.Y + 140f || attackTimer > 40) {
                        // 落地冲击: 震屏 + 两侧低空火浪
                        ACMUtils.AddScreenShake(12f);
                        lavaBloom = Math.Max(lavaBloom, 0.6f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            AokinHelper.CreateDragonFireBurst(NPC.Center, 240f, 4, 16);
                            AokinHelper.CreateSteamBurst(NPC.Center, 180f, 24);
                        }
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int side = -1; side <= 1; side += 2) {
                                for (int i = 0; i < 4; i++) {
                                    Vector2 vel = new Vector2(side * (5f + i * 2.4f), -Main.rand.NextFloat(1.5f, 3.5f));
                                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                        NPC.Center + new Vector2(side * 40f, 20f), vel,
                                        ModContent.ProjectileType<AokinFireball>(), contactDamageBase / 4, 1f);
                                    Main.projectile[p].timeLeft = 70;
                                }
                            }
                        }
                        subState = 3;
                        attackTimer = 0;
                    }
                    break;
                }
                case 3: { // 硬刹收招
                    NPC.velocity *= 0.75f;
                    if (attackTimer > 24) {
                        divebombCooldown = 900;
                        AddHeat(18f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion

        #region 炼狱茧 — 满温泄压（无敌帧 + 带缺口扩张火环, 有反制）

        /// <summary>
        /// 炼狱茧 Inferno Cocoon（满温泄压 set-piece）：
        ///   蓄力（runic 向心收口 + 渐强泛光/震屏, 无敌帧）→ 释放一道扩张火环，环上有一道随机缺口
        ///   （AokinShockRing 着色器绘制, 缺口翠玉标示 + 金芒射线提前指向），玩家须朝缺口冲出（反制）。
        ///   释放清空温度。把"你把房间烧热了"的因果收束为一次可读的泄压。
        /// </summary>
        private bool RunInfernoCocoon(Player target) {
            NPC.dontTakeDamage = true; // i-frame beat

            switch (subState) {
                case 0: { // 锚定 + 蓄力
                    Vector2 anchor = target.Center + new Vector2(0, -260);
                    NPC.velocity = (anchor - NPC.Center) * 0.12f;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);

                    // 渐强蓄力泛光 + 震屏（处决级预警）
                    float chargeT = MathHelper.Clamp(attackTimer / 90f, 0f, 1f);
                    lavaBloom = Math.Max(lavaBloom, chargeT * 0.7f);
                    if (chargeT > 0.6f)
                        ACMUtils.AddScreenShake((chargeT - 0.6f) / 0.4f * 7f);

                    if (!VaultUtils.isServer && chargeT < 0.85f)
                        AokinHelper.CreateConvergingEmbers(NPC.Center, chargeT, 320f, 1.3f);

                    if (attackTimer >= 90) {
                        subState = 1;
                        attackTimer = 0;
                        lavaBloom = 1f;
                        TriggerVent();
                        ACMUtils.AddScreenShake(11f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f, Volume = 1.5f }, NPC.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 缺口角度（server 决策并同步）
                            float gapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            int rings = IsPhase3 ? 2 : 1;
                            for (int r = 0; r < rings; r++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<AokinInfernoRing>(),
                                    Main.expertMode ? 55 : 70, 4f, Main.myPlayer,
                                    ai0: gapAngle + r * 0.6f, ai1: r);
                            }
                        }
                        VentHeat();
                    }
                    break;
                }
                case 1: { // 释放余波
                    NPC.velocity *= 0.92f;
                    if (attackTimer > 70) {
                        NPC.dontTakeDamage = false;
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion
    }
}

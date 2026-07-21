using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 火弹扇射 — 三连发组 + 后坐停顿

        /// <summary>
        /// 火弹扇射 V3：三组三连发。每组: 口部闪光预告(8f) → 3 发快射(每发机体后坐) → 14f 停顿走位窗。
        /// 组间停顿即公平阀门（速度对比来自"射-停-射"节律, 非匀速刷弹）。
        /// </summary>
        private bool RunFireBarrage(Player target) {
            const int AimTime = 18;
            const int GroupLen = 34;
            int groupCount = Main.expertMode ? 4 : 3;

            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 120f, -330);
            Vector2 toHover = hoverPos - NPC.Center;

            if (attackTimer <= AimTime) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.05f, 0.1f);
                return false;
            }

            int t = attackTimer - AimTime;
            int group = t / GroupLen;
            int inGroup = t % GroupLen;

            if (group >= groupCount) {
                NPC.velocity *= 0.94f;
                if (t >= groupCount * GroupLen + 20) {
                    AddHeat(12f);
                    return true;
                }
                return false;
            }

            // 组内: 0~8 预告闪光, 8/13/18 三连发, 之后停顿漂移
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            if (inGroup < 8) {
                NPC.velocity *= 0.9f;
                breathGlow = Math.Max(breathGlow, inGroup / 8f * 0.7f);
                if (!VaultUtils.isServer && inGroup % 2 == 0) {
                    Vector2 mouth = NPC.Center + toPlayer * 52f;
                    var d = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.SolarFlare, -toPlayer * 2f, 80, default, 1.6f);
                    d.noGravity = true;
                }
            }
            else if (inGroup == 8 || inGroup == 13 || inGroup == 18) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int bulletCount = (Main.expertMode ? 5 : 3) + (IsPhase2 ? 2 : 0);
                    float spreadAngle = MathHelper.ToRadians(11f);
                    for (int i = -bulletCount / 2; i <= bulletCount / 2; i++) {
                        Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 10.5f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(),
                            NPC.Center + toPlayer * 50f, vel,
                            ModContent.ProjectileType<AokinFireball>(), contactDamageBase / 3, 1f);
                    }
                }
                // 发射后坐（recoil on every emission）
                NPC.velocity -= toPlayer * 5f;
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.8f }, NPC.Center);
                if (!VaultUtils.isServer)
                    AokinHelper.CreateFireTrail(NPC.Center + toPlayer * 50f, toPlayer * 8f, 1.1f);
            }
            else {
                // 停顿走位窗: 轻微漂移换位
                NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.03f, 0.06f);
            }

            return false;
        }

        #endregion

        #region 赤炎龙息 — 锥形火舌波次（V3 重做）

        /// <summary>
        /// 赤炎龙息 V3：仰头反向拖拽蓄势（pow⁴ 后仰 + 口部聚焰）→ 两波锥形火舌（AokinBreathFlame 持械锥）。
        /// 喷息期头部转速钳制 0.024 rad/f（可绕背 = 公平阀门），两波间 18f 逃逸窗。
        /// 狂暴期火舌更长更宽。
        /// </summary>
        private bool RunDragonBreath(Player target) {
            const int AnticipationTime = 34;
            const int BreathTime = 42;
            const int GapTime = 18;
            const int RecoverTime = 22;

            rotationLocked = true;
            Vector2 toPlayer = target.Center - NPC.Center;
            float aimRot = toPlayer.ToRotation();

            switch (subState) {
                case 0: { // 蓄势: 定位 + 后仰反向拖拽
                    Vector2 anchor = target.Center + new Vector2(NPC.Center.X < target.Center.X ? -430f : 430f, -110f);
                    float t = attackTimer / (float)AnticipationTime;

                    // pow⁴ 后仰: 前段几乎不动, 最后几帧猛然后吸（sharp inhale）
                    Vector2 reelBack = -aimRot.ToRotationVector2() * MathF.Pow(t, 4f) * 95f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor + reelBack - NPC.Center) * 0.09f, 0.16f);

                    NPC.rotation = MathHelper.Lerp(NPC.rotation, aimRot, 0.14f);
                    NPC.spriteDirection = toPlayer.X >= 0 ? 1 : -1;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.7f, Volume = 1f }, NPC.Center);
                    if (attackTimer == AnticipationTime - 12)
                        SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);

                    // 口部聚焰（密度随进度, 末段静默一拍）
                    breathGlow = Math.Max(breathGlow, t);
                    if (!VaultUtils.isServer && t < 0.85f) {
                        Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 55f;
                        AokinHelper.CreateConvergingEmbers(mouth, t * 0.9f, 170f, 1.3f);
                    }

                    if (attackTimer >= AnticipationTime) {
                        subState = 1;
                        attackTimer = 0;
                        BeginBreathWave(aimRot);
                    }
                    break;
                }
                case 1:   // 波次 1
                case 3: { // 波次 2
                    // 喷息中: 反推后坐 + 头部转速钳制（可绕背）
                    NPC.velocity = Vector2.Lerp(NPC.velocity, -NPC.rotation.ToRotationVector2() * 2.4f, 0.08f);

                    float diff = MathHelper.WrapAngle(aimRot - NPC.rotation);
                    float turnCap = rageActive ? 0.03f : 0.024f;
                    NPC.rotation += MathHelper.Clamp(diff, -turnCap, turnCap);
                    NPC.spriteDirection = MathF.Cos(NPC.rotation) >= 0 ? 1 : -1;

                    breathGlow = Math.Max(breathGlow, 0.85f);
                    if (!VaultUtils.isServer && attackTimer % 5 == 0)
                        ACMUtils.AddScreenShake(1.6f);

                    if (attackTimer >= BreathTime) {
                        subState++;
                        attackTimer = 0;
                        if (subState == 4)
                            SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.3f, Volume = 0.7f }, NPC.Center);
                    }
                    break;
                }
                case 2: { // 波间逃逸窗: 换位
                    Vector2 anchor = target.Center + new Vector2(NPC.Center.X < target.Center.X ? -400f : 400f, -160f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.07f, 0.12f);
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, aimRot, 0.1f);

                    if (attackTimer >= GapTime) {
                        subState = 3;
                        attackTimer = 0;
                        BeginBreathWave(aimRot);
                    }
                    break;
                }
                case 4: { // 收招: 甩头收势
                    NPC.velocity *= 0.93f;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, aimRot, 0.06f);
                    if (attackTimer >= RecoverTime) {
                        AddHeat(16f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>发射一波锥形龙息（持械锥弹幕负责伤害与绘制）, 附带后坐与顿感。</summary>
        private void BeginBreathWave(float aimRot) {
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
            ACMUtils.AddScreenShake(5f);
            // 点火瞬间后坐（heavy emission recoil）
            NPC.velocity -= aimRot.ToRotationVector2() * 11f;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<AokinBreathFlame>(),
                    contactDamageBase / 3, 2f, Main.myPlayer,
                    ai0: NPC.whoAmI, ai1: rageActive || IsPhase3 ? 1f : 0f);
            }
        }

        #endregion

        #region 劫火印记 — 预告式顺序火柱波

        /// <summary>
        /// 劫火印记：在地面上按顺序（横扫方向）落下一串火柱印记，每柱独立 telegraph→喷发。
        /// V3: Boss 随扫向低空掠行, 每落一印做一次"掌拍"下沉+回弹（把因果画在身上）; 去掉尾部死等。
        /// </summary>
        private bool RunEmberPillars(Player target) {
            int pillarCount = IsPhase2 ? 8 : 6;
            int interval = Main.expertMode ? 18 : 22;
            float span = ArenaHalfWidth * 0.85f;
            float baseY = target.Center.Y + 240f;

            // 扫向（本招开始时确定）
            int dir = (seed + (int)NPC.ai[2]) % 2 == 0 ? 1 : -1;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.4f, Volume = 1.1f }, NPC.Center);
            }

            int index = attackTimer / interval;

            // Boss 掠行: 跟随当前印记 x, 低空压迫
            float sweepT = MathHelper.Clamp(index / (float)Math.Max(1, pillarCount - 1), 0f, 1f);
            float sweepX = target.Center.X + dir * MathHelper.Lerp(-span, span, sweepT);
            Vector2 sweepAnchor = new Vector2(sweepX, target.Center.Y - 310f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (sweepAnchor - NPC.Center) * 0.05f, 0.1f);

            if (attackTimer % interval == 0 && index < pillarCount) {
                // 掌拍: 下沉冲量 + 立即回弹（secondary motion）
                NPC.velocity.Y += 7f;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float t = pillarCount <= 1 ? 0.5f : index / (float)(pillarCount - 1);
                    float x = target.Center.X + dir * MathHelper.Lerp(-span, span, t);
                    Vector2 markPos = new Vector2(x, baseY);

                    int telegraph = Main.expertMode ? 40 : 50;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        markPos, Vector2.Zero,
                        ModContent.ProjectileType<AokinEmberPillar>(),
                        Main.expertMode ? 45 : 60, 2f,
                        Main.myPlayer, ai0: telegraph, ai1: IsPhase2 ? 1f : 0f);
                }
            }

            // 蓄力期向心粒子
            if (!VaultUtils.isServer && attackTimer < 24) {
                AokinHelper.CreateConvergingEmbers(NPC.Center, 0.7f, 180f);
            }

            // 末柱 telegraph+喷发结束即收（无死等）
            int lastTelegraph = Main.expertMode ? 40 : 50;
            int total = (pillarCount - 1) * interval + lastTelegraph + 38 + 24;
            if (attackTimer > total) {
                AddHeat(22f);
                return true;
            }
            return false;
        }

        #endregion

        #region 龙蛇盘绕俯冲 — 收紧的接触伤害螺旋（身体即机制）

        /// <summary>
        /// 龙蛇盘绕俯冲：先预告，再绕玩家高速盘旋并不断收紧半径，
        /// 身体段（UpdateSegments）随之盘成一道收缩的接触伤害螺旋墙；末段甩出。
        /// 接触伤害仅在盘旋/甩出窗口生效（伤害窗口=视觉窗口）。
        /// </summary>
        private bool RunCoilDive(Player target) {
            switch (subState) {
                case 0: { // 预告：飞到玩家上方外圈并标记盘绕方向
                    Vector2 anchor = target.Center + new Vector2(0, -120);
                    NPC.velocity = (anchor - NPC.Center) * 0.06f;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, NPC.Center);
                    if (!VaultUtils.isServer && attackTimer % 3 == 0)
                        AokinHelper.CreateFlameVortex(NPC.Center, 70f + attackTimer, 0.5f, 10);

                    if (attackTimer >= 35) {
                        coilAngle = (NPC.Center - target.Center).ToRotation();
                        coilRadius = 560f;
                        subState = 1;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(6f);
                    }
                    break;
                }
                case 1: { // 收紧盘旋（接触伤害窗口开启）
                    bodyContactWindow = true;
                    float coilSpeed = (IsPhase2 ? 0.13f : 0.10f) * (rageActive ? 1.18f : 1f);
                    coilAngle += coilSpeed;
                    float minRadius = IsPhase3 ? 170f : 200f;
                    coilRadius = MathHelper.Lerp(coilRadius, minRadius, 0.013f);

                    Vector2 desired = target.Center + coilAngle.ToRotationVector2() * coilRadius;
                    NPC.velocity = (desired - NPC.Center) * 0.35f;

                    if (!VaultUtils.isServer && attackTimer % 2 == 0)
                        AokinHelper.CreateFireTrail(NPC.Center, NPC.velocity, 1.1f);

                    // 收到足够紧 / 足够久 → 甩出（早退计时: 目标达成即走）
                    if (coilRadius <= minRadius + 12f || attackTimer > (IsPhase2 ? 220 : 190)) {
                        subState = 2;
                        attackTimer = 0;
                        Vector2 outDir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = outDir * (IsPhase2 ? 34f : 27f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                        if (!VaultUtils.isServer)
                            AokinHelper.CreateDragonFireBurst(NPC.Center, 160f, 3, 12);
                    }
                    break;
                }
                case 2: { // 甩出余波（硬刹）
                    bodyContactWindow = true;
                    NPC.velocity *= 0.93f;
                    if (attackTimer > 26) {
                        AddHeat(18f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion

        #region 熔金雨 — 仰天上抛熔金球（V3 新招）

        /// <summary>
        /// 熔金雨：仰头蓄金光（30f）→ 两波上抛熔金球（抛物线, 落点地面金圈提前标记）→ 收招。
        /// 熔金球落地成灼热熔池, 池间必留缝隙——考走位不考反应。
        /// </summary>
        private bool RunMoltenRain(Player target) {
            const int AnticipationTime = 30;
            const int VolleyGap = 45;
            const int RecoverTime = 24;

            switch (subState) {
                case 0: { // 仰头蓄金光
                    Vector2 anchor = target.Center + new Vector2(0, -360);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.06f, 0.1f);

                    rotationLocked = true;
                    float upRot = NPC.spriteDirection >= 0 ? -MathHelper.PiOver4 * 1.4f : MathHelper.Pi + MathHelper.PiOver4 * 1.4f;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, upRot, 0.1f);

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);

                    float t = attackTimer / (float)AnticipationTime;
                    breathGlow = Math.Max(breathGlow, t * 0.8f);
                    if (!VaultUtils.isServer && t < 0.85f) {
                        Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 55f;
                        AokinHelper.CreateConvergingEmbers(mouth, t * 0.8f, 150f);
                    }

                    if (attackTimer >= AnticipationTime) {
                        subState = 1;
                        attackTimer = 0;
                    }
                    break;
                }
                case 1: { // 两波上抛
                    NPC.velocity *= 0.94f;
                    rotationLocked = true;

                    if (attackTimer == 1 || attackTimer == VolleyGap + 1) {
                        SpitMoltenVolley(target);
                    }

                    if (attackTimer >= VolleyGap + 20) {
                        subState = 2;
                        attackTimer = 0;
                    }
                    break;
                }
                case 2: { // 收招
                    NPC.velocity *= 0.95f;
                    if (attackTimer >= RecoverTime) {
                        AddHeat(18f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>上抛一波熔金球（server）, 附仰喷后坐。</summary>
        private void SpitMoltenVolley(Player target) {
            SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f, Volume = 0.7f }, NPC.Center);
            NPC.velocity.Y += 6f; // 仰喷后坐
            ACMUtils.AddScreenShake(4f);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int globCount = (IsPhase2 ? 9 : 7) + (rageActive ? 2 : 0);
            float landingY = target.Bottom.Y;
            Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 50f;

            for (int i = 0; i < globCount; i++) {
                // 落点横向均布 ± 随机抖动, 保证池间有缝
                float spanT = globCount <= 1 ? 0.5f : i / (float)(globCount - 1);
                float targetX = target.Center.X + MathHelper.Lerp(-ArenaHalfWidth * 0.7f, ArenaHalfWidth * 0.7f, spanT)
                    + Main.rand.NextFloat(-30f, 30f);

                // 由抛物线反解初速: 上抛时间 tUp+tDown 固定区间, 取随机总时长
                float flight = Main.rand.NextFloat(52f, 76f);
                const float gravity = 0.34f;
                float vx = (targetX - mouth.X) / flight;
                float vy = (landingY - mouth.Y) / flight - 0.5f * gravity * flight;

                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    mouth, new Vector2(vx, vy),
                    ModContent.ProjectileType<AokinMoltenGlob>(),
                    contactDamageBase / 4, 1f, Main.myPlayer,
                    ai0: gravity, ai1: landingY);
            }
        }

        #endregion
    }
}

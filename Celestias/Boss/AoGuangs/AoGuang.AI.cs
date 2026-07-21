using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region AI主循环

        // 屏幕演出覆盖 (状态脚本每帧按需设置, <0 = 用默认; 每帧开头复位)
        private float warpOverride = -1f;
        private float tintOverride = -1f;

        public override void AI() {
            globalTime += 1f / 60f;

            // 每帧复位的编排标志 (由状态脚本按需重新设置)
            contactDamageActive = false;
            poseRotOverride = float.NaN;
            warpOverride = -1f;
            tintOverride = -1f;

            // 死亡演出无视目标状态, 一定完整播完
            if (Phase == BossPhase.Death) {
                PhaseTimer++;
                AttackTimer++;
                RunDeath();
                UpdateVisualScalars();
                UpdateScreenFx();
                UpdateRotation();
                Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.7f, 0.9f) * glowIntensity);
                return;
            }

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 血线相变检查 (服务器权威, TransitionTo 自带 netUpdate)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                CheckPhaseTransition();
            }

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Cruise: RunCruise(target); break;
                case BossPhase.TideBoltVolley: RunTideBoltVolley(target); break;
                case BossPhase.PierceRun: RunPierceRun(target); break;
                case BossPhase.SurgePillars: RunSurgePillars(target); break;
                case BossPhase.Transition2: RunTransition2(target); break;
                case BossPhase.TsunamiWaves: RunTsunamiWaves(target); break;
                case BossPhase.TornadoThrow: RunTornadoThrow(target); break;
                case BossPhase.DragonBreath: RunDragonBreath(target); break;
                case BossPhase.Transition3: RunTransition3(target); break;
                case BossPhase.AbyssalMaw: RunAbyssalMaw(target); break;
                case BossPhase.SkyfallTide: RunSkyfallTide(target); break;
                case BossPhase.FuryPierce: RunFuryPierce(target); break;
            }

            // 距离栓绳: 穿刺态以外, 离目标过远时强制回追 (防飞屏绕圈失联)
            if (!contactDamageActive && NPC.Distance(target.Center) > LeashDistance) {
                Vector2 pull = NPC.SafeDirectionTo(target.Center) * 18f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, pull, 0.06f);
            }

            UpdateVisualScalars();
            UpdateScreenFx();
            UpdateRotation();

            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.7f, 0.9f) * glowIntensity);
        }

        #endregion

        #region 朝向 / 移动原语

        private void UpdateRotation() {
            if (!float.IsNaN(poseRotOverride)) {
                NPC.rotation = NPC.rotation.AngleLerp(poseRotOverride, 0.3f);
                return;
            }
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                // 转向速率随速度提升 (高速穿刺锁死直线, 低速巡曳柔和摆首)
                float lerpAmt = MathHelper.Lerp(0.08f, 0.3f, Utils.GetLerpValue(2f, 30f, NPC.velocity.Length(), true));
                NPC.rotation = NPC.rotation.AngleLerp(targetRot, lerpAmt);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        /// <summary>演出姿态角: 面朝右时取 a, 面朝左时镜像 (配合垂直翻转绘制约定)。</summary>
        private static float PoseAngle(float a, int dir) => dir == 1 ? a : MathHelper.Pi - a;

        /// <summary>
        /// 蛇形巡曳: 朝锚点游动 + 垂直于行进方向的正弦摆尾。
        /// 龙王的基础运动语言 —— 慢而有节律, 永不静态。
        /// </summary>
        private void SerpentineGlide(Vector2 anchor, float approach, float lerpAmt, float swayAmp) {
            Vector2 toAnchor = anchor - NPC.Center;
            Vector2 desired = toAnchor * approach;

            Vector2 dir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            desired += perp * MathF.Sin(globalTime * 3.1f) * swayAmp;

            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, lerpAmt);
        }

        #endregion

        #region 视觉标量 / 屏幕演出

        private void UpdateVisualScalars() {
            // fake-Z 默认回落到前景 (入场/相变脚本会主动抬高)
            if (Phase != BossPhase.Intro && Phase != BossPhase.Transition2) {
                visualZ = MathHelper.Lerp(visualZ, 0f, 0.08f);
            }

            // 速度门控能量档: 只有真正快的时刻龙躯才全亮 (速度=对比)
            float speedGlow = Utils.GetLerpValue(13f, 42f, NPC.velocity.Length(), true);
            pierceGlow = MathHelper.Lerp(pierceGlow, speedGlow, 0.22f);

            // 龙眼转红 (三阶段, 相变二脚本会加速)
            eyeRedLerp = MathHelper.Lerp(eyeRedLerp, IsPhase3 ? 1f : 0f, 0.02f);

            // 阶段光强与水环
            if (IsPhase3) {
                glowIntensity = 1.6f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.85f, 0.04f);
            }
            else if (IsPhase2) {
                glowIntensity = 1.3f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.55f, 0.04f);
            }
            else {
                glowIntensity = 1f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.35f, 0.04f);
            }

            // 事件标量自衰减
            if (tidalRingVisual > 0f)
                tidalRingVisual = MathF.Max(0f, tidalRingVisual - 0.022f);
            if (impactFrame > 0f)
                impactFrame = MathF.Max(0f, impactFrame - 1f / 15f);
            stillness = MathHelper.Lerp(stillness, 0f, 0.05f);
        }

        /// <summary>
        /// 「沧海沉浸」屏幕演出标量驱动 (纯本地视觉, 服务端早退)。
        /// 水位叙事 = 危险底色: P1 涨潮 → P2 没顶 → P3 深渊。全屏折射由 PostDraw 走名额契约,
        /// 氛围底色/潮涌泛光发布给 <see cref="AoGuangSubmersionScreenSystem"/>。
        /// </summary>
        private void UpdateScreenFx() {
            if (Main.dedServ)
                return;

            // —— 默认目标 (脚本状态用 override 覆盖) ——
            float tintTarget = IsPhase3 ? 0.5f : (IsPhase2 ? 0.32f : 0.12f);
            float warpTarget = IsPhase3 ? 0.38f : (IsPhase2 ? 0.22f : 0f);
            if (tintOverride >= 0f) tintTarget = tintOverride;
            if (warpOverride >= 0f) warpTarget = warpOverride;

            // —— 深渊漩涡向心吸力 ——
            float inwardTarget = 0f;
            if (Phase == BossPhase.AbyssalMaw && SubState >= 1) {
                warpTarget = MathF.Max(warpTarget, 0.65f);
                tintTarget = MathF.Max(tintTarget, 0.6f);
                inwardTarget = 1f;
            }

            // —— 水位默认目标 (脚本状态直接改 waterLevelTarget / waterLevel) ——
            if (Phase != BossPhase.Intro && Phase != BossPhase.Transition2 &&
                Phase != BossPhase.Transition3 && Phase != BossPhase.Death) {
                waterLevelTarget = IsPhase3 ? 0.30f : (IsPhase2 ? 0.22f : 0.08f);
            }

            tideTint = MathHelper.Lerp(tideTint, tintTarget, 0.03f);
            submersionWarp = MathHelper.Lerp(submersionWarp, warpTarget, warpTarget > submersionWarp ? 0.06f : 0.03f);
            vortexInward = MathHelper.Lerp(vortexInward, inwardTarget, 0.05f);
            waterLevel = MathHelper.Lerp(waterLevel, waterLevelTarget, waterLevel < waterLevelTarget ? 0.035f : 0.02f);

            if (waterBloom > 0f)
                waterBloom = MathF.Max(0f, waterBloom - 0.025f);

            AoGuangSubmersionScreenSystem.Publish(NPC.Center, tideTint, waterBloom, globalTime);
        }

        #endregion

        #region 相变检查 / 状态切换 / 选招洗牌袋

        private void CheckPhaseTransition() {
            bool scripted = Phase == BossPhase.Intro || Phase == BossPhase.Transition2 ||
                            Phase == BossPhase.Transition3 || Phase == BossPhase.Death;
            if (scripted)
                return;

            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                TransitionTo(BossPhase.Transition2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase2Transition = true; // 跳线保护: 直接砸穿 65% 时不再补播相变一
                didPhase3Transition = true;
                TransitionTo(BossPhase.Transition3);
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            chargeCount = 0;
            NPC.netUpdate = true;
        }

        // ===== 洗牌袋 (服务器专用): 每招必现 + 永不连续重复 =====

        private static readonly BossPhase[] Pool1 = {
            BossPhase.TideBoltVolley, BossPhase.PierceRun, BossPhase.SurgePillars
        };
        private static readonly BossPhase[] Pool2 = {
            BossPhase.TsunamiWaves, BossPhase.TornadoThrow, BossPhase.DragonBreath, BossPhase.PierceRun
        };
        private static readonly BossPhase[] Pool3 = {
            BossPhase.AbyssalMaw, BossPhase.SkyfallTide, BossPhase.FuryPierce,
            BossPhase.TsunamiWaves, BossPhase.DragonBreath
        };

        private int bagTier = -1;

        private BossPhase PickNextAttack() {
            BossPhase[] pool = IsPhase3 ? Pool3 : (IsPhase2 ? Pool2 : Pool1);
            int tier = IsPhase3 ? 3 : (IsPhase2 ? 2 : 1);

            if (attackBag == null || bagCursor >= attackBag.Length || bagTier != tier) {
                bagTier = tier;
                attackBag = (BossPhase[])pool.Clone();
                // Fisher-Yates 洗牌
                for (int i = attackBag.Length - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
                }
                // 反连击: 新袋首位不得等于上一招
                if (attackBag.Length > 1 && attackBag[0] == lastAttack) {
                    int swap = Main.rand.Next(1, attackBag.Length);
                    (attackBag[0], attackBag[swap]) = (attackBag[swap], attackBag[0]);
                }
                bagCursor = 0;
            }

            BossPhase next = attackBag[bagCursor++];
            if (next == lastAttack && bagCursor < attackBag.Length)
                next = attackBag[bagCursor++];
            lastAttack = next;
            return next;
        }

        #endregion

        #region 巡曳连接拍

        /// <summary>
        /// 巡曳 — 招与招之间的连接拍 (段落呼吸口): 蛇形缓游到玩家侧上方,
        /// 到位即提前退出 (不为自己的时间表等待), 超时保底退出。结束时服务器选招。
        /// </summary>
        private void RunCruise(Player target) {
            float side = NPC.Center.X > target.Center.X ? 1f : -1f;
            Vector2 anchor = target.Center + new Vector2(side * 430f, -240f);
            SerpentineGlide(anchor, 0.055f, 0.08f, 2.6f);

            // 尾流水珠 (轻)
            if (!VaultUtils.isServer && Main.rand.NextBool(3) && stillness < 0.5f) {
                Dust d = Dust.NewDustDirect(NPC.Center - NPC.velocity * 2f, 0, 0, DustID.Water, 0, 0, 150, default, 1.4f);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.2f;
            }

            // 存在感气泡: 慢速可读, 不构成真压力
            if (PhaseTimer == 18 && Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool()) {
                Vector2 vel = NPC.SafeDirectionTo(target.Center).RotatedByRandom(0.5f) * 5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DragonBubble>(), NPC.damage / 4, 1f);
            }

            // 到位提前退出 / 超时保底 (26f 最短呼吸口)
            bool arrived = PhaseTimer > 26 && NPC.Distance(anchor) < 150f;
            if ((arrived || PhaseTimer > 56) && Main.netMode != NetmodeID.MultiplayerClient) {
                TransitionTo(PickNextAttack());
            }
        }

        #endregion

        #region 入场演出「东海升朝」

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = PhaseTimer < 150;

            // —— 0~35f: 背景冲镜 (fake-Z 6→0, cubed) ——
            if (PhaseTimer <= 35) {
                if (PhaseTimer == 1) {
                    NPC.Center = target.Center + new Vector2(0, -280);
                    NPC.velocity = Vector2.Zero;
                    NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.6f, Volume = 1.2f }, target.Center);
                }
                float t = PhaseTimer / 35f;
                visualZ = 6f * (1f - t * t * t);
                NPC.velocity *= 0.9f;
                poseRotOverride = PoseAngle(0f, NPC.spriteDirection);
            }
            // —— 35~95f: 60 帧纯静止威压 (威严=静止) ——
            else if (PhaseTimer <= 95) {
                visualZ = 0f;
                stillness = 1f;
                NPC.velocity *= 0.85f;
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
                poseRotOverride = PoseAngle(0f, NPC.spriteDirection);

                // 只有水珠从龙躯滴落
                if (!VaultUtils.isServer && PhaseTimer % 5 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(70, 60), 0, 0,
                        DustID.Water, 0, 1.5f, 120, default, 1.3f);
                    d.noGravity = false;
                }
            }
            // —— 95~115f: 举戟后摆 (quad in-out 到 -0.9rad) ——
            else if (PhaseTimer <= 115) {
                float t = (PhaseTimer - 95f) / 20f;
                poseRotOverride = PoseAngle(-0.9f * ACMUtils.QuadInOut(t), NPC.spriteDirection);
                NPC.velocity *= 0.85f;

                // 渐强低鸣
                if (PhaseTimer % 6 == 0)
                    ACMUtils.AddScreenShake((PhaseTimer - 95f) / 20f * 4f);
            }
            // —— 115f: 戟落 (poly 斩拍) + 封路龙卷 ——
            else if (PhaseTimer <= 190) {
                float t = MathHelper.Clamp((PhaseTimer - 115f) / 6f, 0f, 1f);
                float strike = 1f - MathF.Pow(1f - t, 8f); // 高次 ease-out = 一记斩
                poseRotOverride = PoseAngle(MathHelper.Lerp(-0.9f, 0.55f, strike), NPC.spriteDirection);

                if (PhaseTimer == 116) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.6f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                    ACMUtils.AddScreenShake(12f);
                    tidalRingVisual = 1f;
                    waterBloom = 1f;

                    // 封路龙卷 (战场左右边界)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                target.Center + new Vector2(side * 850, 0), Vector2.Zero,
                                ModContent.ProjectileType<BarrierWaterTornado>(),
                                NPC.damage / 4, 0f, ai0: NPC.whoAmI, ai1: side);
                        }
                    }

                    // 戟落水花环
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 46; i++) {
                            float ang = MathHelper.TwoPi * i / 46f;
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Water,
                                MathF.Cos(ang) * 12f, MathF.Sin(ang) * 12f, 90, default, 2.8f);
                            d.noGravity = true;
                        }
                    }
                }

                // 落定后轻微回摆
                if (PhaseTimer > 130)
                    poseRotOverride = PoseAngle(MathHelper.Lerp(0.55f, 0f, (PhaseTimer - 130f) / 60f), NPC.spriteDirection);
                NPC.velocity *= 0.9f;
            }

            if (PhaseTimer > 190) {
                NPC.dontTakeDamage = false;
                TransitionTo(BossPhase.Cruise);
            }
        }

        #endregion

        #region 相变一「没顶」(65%, 可玩演出)

        private void RunTransition2(Player target) {
            NPC.dontTakeDamage = true;
            warpOverride = 0.5f;

            switch ((int)SubState) {
                case 0: // 受创后仰 (30f)
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.NPCHit56 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = 0.2f, Volume = 1.3f }, NPC.Center);
                        NPC.velocity = -NPC.SafeDirectionTo(target.Center) * 13f; // 大反冲
                        waterBloom = 0.6f;
                        ClearHostileProjectiles(keepBarriers: true);
                    }
                    NPC.velocity *= 0.92f;
                    if (AttackTimer >= 30) { SubState = 1; AttackTimer = 0; }
                    break;

                case 1: // 冲天离场 (50f)
                    NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -34f), 0.12f);
                    visualZ = MathHelper.Lerp(visualZ, 3.2f, 0.05f);

                    // 沿途甩落水瀑
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 90), 0, 0,
                                DustID.Water, 0, 6f, 100, default, 2.4f);
                            d.noGravity = false;
                        }
                    }
                    if (AttackTimer >= 50) { SubState = 2; AttackTimer = 0; }
                    break;

                case 2: // 水位吞屏 + 两波穿越浪墙 (190f, 浪从 850px 外 ~113f 抵达 — 都在演出窗口内)
                    visualZ = 3.5f;
                    NPC.Center = Vector2.Lerp(NPC.Center, target.Center + new Vector2(0, -1150), 0.05f);
                    NPC.velocity *= 0.9f;
                    waterLevelTarget = 0.4f;
                    waterLevel = MathHelper.Lerp(waterLevel, waterLevelTarget, 0.045f); // 急涨

                    if ((AttackTimer == 10 || AttackTimer == 60) && Main.netMode != NetmodeID.MultiplayerClient) {
                        float dir = AttackTimer == 10 ? 1f : -1f; // 一左一右
                        SpawnTsunamiWall(target, dir, gapHalf: 150f, speed: 7.5f, spawnDist: 850f);
                    }
                    if (AttackTimer == 10 || AttackTimer == 60) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1.3f }, target.Center);
                        ACMUtils.AddScreenShake(8f);
                    }
                    if (AttackTimer >= 190) { SubState = 3; AttackTimer = 0; }
                    break;

                case 3: // 破水回场 (46f)
                    visualZ = MathHelper.Lerp(visualZ, 0f, 0.12f);
                    SerpentineGlide(target.Center + new Vector2(0, -300), 0.09f, 0.14f, 0f);

                    if (AttackTimer == 18) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.6f }, NPC.Center);
                        ACMUtils.AddScreenShake(12f);
                        waterBloom = 1f;
                        tidalRingVisual = 1f;
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 60; i++) {
                                float ang = MathHelper.TwoPi * i / 60f;
                                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0,
                                    Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch,
                                    MathF.Cos(ang) * Main.rand.NextFloat(6f, 14f),
                                    MathF.Sin(ang) * Main.rand.NextFloat(6f, 14f), 80, default, 3f);
                                d.noGravity = true;
                            }
                        }
                    }
                    if (AttackTimer >= 46) {
                        NPC.dontTakeDamage = false;
                        TransitionTo(BossPhase.Cruise);
                    }
                    break;
            }
        }

        #endregion

        #region 相变二「定海·潮止」(30%)

        private void RunTransition3(Player target) {
            NPC.dontTakeDamage = true;

            switch ((int)SubState) {
                case 0: // 急停定身 (20f)
                    if (AttackTimer == 1)
                        ClearHostileProjectiles(keepBarriers: true);
                    NPC.velocity *= 0.82f;
                    SerpentineGlide(target.Center + new Vector2(0, -300), 0.06f, 0.1f, 0f);
                    if (AttackTimer >= 20) { SubState = 1; AttackTimer = 0; }
                    break;

                case 1: // 潮止 (70f): 海停了 — 全屏水效退干, 无声举戟
                    stillness = 1f;
                    warpOverride = 0f;
                    tintOverride = 0.05f;
                    waterLevelTarget = 0f;
                    waterLevel = MathHelper.Lerp(waterLevel, 0f, 0.08f); // 退潮比涨潮快
                    NPC.velocity *= 0.85f;
                    NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

                    if (AttackTimer > 20) {
                        float t = MathHelper.Clamp((AttackTimer - 20f) / 50f, 0f, 1f);
                        poseRotOverride = PoseAngle(-0.9f * ACMUtils.QuadInOut(t), NPC.spriteDirection);
                    }
                    if (AttackTimer >= 70) { SubState = 2; AttackTimer = 0; }
                    break;

                case 2: // 戟落: impact frame (一场唯一) + 深渊揭幕 (26f)
                    if (AttackTimer == 1) {
                        impactFrame = 1f;
                        waterBloom = 1f;
                        tidalRingVisual = 1f;
                        waterLevel = 0.5f;
                        ACMUtils.AddScreenShake(16f);
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 1.7f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.5f }, NPC.Center);
                    }
                    {
                        float t = MathHelper.Clamp(AttackTimer / 5f, 0f, 1f);
                        float strike = 1f - MathF.Pow(1f - t, 8f);
                        poseRotOverride = PoseAngle(MathHelper.Lerp(-0.9f, 0.55f, strike), NPC.spriteDirection);
                    }
                    eyeRedLerp = MathHelper.Lerp(eyeRedLerp, 1f, 0.08f);
                    waterLevelTarget = 0.3f;
                    NPC.velocity *= 0.9f;
                    if (AttackTimer >= 26) { SubState = 3; AttackTimer = 0; }
                    break;

                case 3: // 落定 + 赤目凝视 (60f)
                    eyeRedLerp = MathHelper.Lerp(eyeRedLerp, 1f, 0.08f);
                    poseRotOverride = PoseAngle(MathHelper.Lerp(0.55f, 0f, MathHelper.Clamp(AttackTimer / 40f, 0f, 1f)), NPC.spriteDirection);
                    NPC.velocity *= 0.92f;
                    if (AttackTimer >= 60) {
                        NPC.dontTakeDamage = false;
                        TransitionTo(BossPhase.Cruise);
                    }
                    break;
            }
        }

        #endregion

        #region 死亡演出「潮退归海」

        // 加速鼓点 (顶点前的读秒, 间隔递减 → 音高递增)
        private static readonly int[] DeathBeepTimes = { 0, 34, 62, 84, 101, 114, 124, 132, 138 };

        private void RunDeath() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            switch ((int)SubState) {
                case 0: // 失控抽搐 (40f)
                    if (AttackTimer % 8 == 1) {
                        NPC.velocity = Main.rand.NextVector2CircularEdge(8f, 8f);
                        SoundEngine.PlaySound(SoundID.NPCHit56 with { Pitch = Main.rand.NextFloat(-0.3f, 0.3f) }, NPC.Center);
                    }
                    NPC.velocity *= 0.9f;
                    dissolveProgress = MathHelper.Lerp(dissolveProgress, 0.1f, 0.05f);
                    if (AttackTimer >= 40) {
                        SubState = 1;
                        AttackTimer = 0;
                        chargeTarget = NPC.Center; // 螺旋中心 (复用同步字段)
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 失控螺旋上升 (150f): 半径渐大 / 转速渐快 / 沿途漏水
                    {
                        float t = AttackTimer;
                        float ang = t * 0.085f + t * t * 0.00042f;
                        float radius = 50f + t * 1.55f;
                        Vector2 spiralPos = chargeTarget + new Vector2(0, -t * 1.7f) + ang.ToRotationVector2() * radius;
                        NPC.velocity = spiralPos - NPC.Center; // 精确跟踪, oldPos 轨迹保持连续

                        dissolveProgress = MathHelper.Lerp(0.1f, 0.55f, t / 150f);

                        // 加速鼓点 (音高随序号上扬)
                        for (int i = 0; i < DeathBeepTimes.Length; i++) {
                            if ((int)t == DeathBeepTimes[i] + 4) {
                                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.3f + i * 0.12f, Volume = 0.9f }, NPC.Center);
                                break;
                            }
                        }
                        if ((int)t % 10 == 0)
                            ACMUtils.AddScreenShake(t / 150f * 6f);

                        // 沿途漏水
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 2; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 50), 0, 0,
                                    DustID.Water, 0, 5f, 100, default, 2.2f);
                                d.noGravity = false;
                            }
                        }

                        if (AttackTimer >= 150) { SubState = 2; AttackTimer = 0; }
                    }
                    break;

                case 2: // 顶点定格 (20f): 万籁俱寂
                    NPC.velocity = Vector2.Zero;
                    stillness = 1f;
                    waterBloom = MathF.Max(waterBloom - 0.06f, 0.12f); // 泛光收缩 — 爆发前的塌缩
                    if (AttackTimer >= 20) { SubState = 3; AttackTimer = 0; }
                    break;

                case 3: // 水爆散身 (26f): 一场最大的一次震屏
                    if (AttackTimer == 1) {
                        ACMUtils.AddScreenShake(19f);
                        waterBloom = 1f;
                        impactFrame = 0.55f;
                        tidalRingVisual = 1f;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 2f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.8f }, NPC.Center);

                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 140; i++) {
                                float angD = MathHelper.TwoPi * i / 140f;
                                float spd = Main.rand.NextFloat(5f, 20f);
                                int dustType = Main.rand.Next(3) switch {
                                    0 => DustID.Water,
                                    1 => DustID.BlueTorch,
                                    _ => DustID.BubbleBlock
                                };
                                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, dustType,
                                    MathF.Cos(angD) * spd, MathF.Sin(angD) * spd, 80, default, 3.2f);
                                d.noGravity = true;
                            }
                        }
                    }
                    NPC.velocity = Vector2.Zero;
                    dissolveProgress = MathF.Min(1f, dissolveProgress + 0.07f);
                    if (AttackTimer >= 26) { SubState = 4; AttackTimer = 0; }
                    break;

                case 4: // 泡沫沉降 (110f) → 真死 (掉落/downed 照常)
                    NPC.velocity = Vector2.Zero;
                    dissolveProgress = 1f;
                    waterLevelTarget = 0f;
                    tintOverride = MathF.Max(0f, 0.4f * (1f - AttackTimer / 110f));
                    warpOverride = 0f;

                    // 余韵: 泡沫缓缓上浮
                    if (!VaultUtils.isServer && AttackTimer % 4 == 0) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(160, 90), 0, 0,
                            DustID.BubbleBlock, 0, -1.2f, 160, default, 1.4f);
                        d.noGravity = true;
                    }

                    if (AttackTimer >= 110 && Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.life = 0;
                        NPC.HitEffect();
                        NPC.checkDead(); // deathAnimStarted 已置位 → 真正死亡
                    }
                    break;
            }
        }

        #endregion

        #region 工具

        /// <summary>
        /// 清除本 Boss 的全部敌对弹幕 (相变/死亡公平阀门)。服务器权威。
        /// </summary>
        private void ClearHostileProjectiles(bool keepBarriers) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int barrierType = ModContent.ProjectileType<BarrierWaterTornado>();
            string ns = typeof(AoGuang).Namespace;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.hostile || p.ModProjectile == null)
                    continue;
                if (p.ModProjectile.GetType().Namespace != ns)
                    continue;
                if (keepBarriers && p.type == barrierType)
                    continue;
                p.Kill();
            }
        }

        /// <summary>
        /// 生成一面整场浪墙 (服务器)。dir=+1 从左往右扫, -1 从右往左; 缺口随机落在玩家 Y 附近。
        /// </summary>
        private void SpawnTsunamiWall(Player target, float dir, float gapHalf, float speed, float spawnDist = 1150f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            float spawnX = target.Center.X - dir * spawnDist;
            float gapY = target.Center.Y + Main.rand.NextFloat(-140f, 140f);
            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                new Vector2(spawnX, target.Center.Y), new Vector2(dir * speed, 0f),
                ModContent.ProjectileType<TsunamiWall>(), NPC.damage / 3, 1f,
                ai0: gapY, ai1: gapHalf);
        }

        #endregion
    }
}

using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 二阶段（天王降临）+ 三阶段（库藏封印）
    /// </summary>
    internal partial class Vaisravana
    {
        #region 二阶段轮替状态

        private int p2Index;        // 二阶段攻击轮替索引
        private float mirrorAxis;   // 镜射轴角度

        // 方向常量：0=北 1=东 2=南 3=西
        private static readonly float[] CardinalAngle = {
            -MathHelper.PiOver2, // 北 (上)
            0f,                  // 东 (右)
            MathHelper.PiOver2,  // 南 (下)
            MathHelper.Pi        // 西 (左)
        };

        private static int Opposite(int dir) => (dir + 2) % 4;

        #endregion

        #region 二阶段AI · 枢纽 / 夜叉召唤

        private void RunPhase2Hub(Player target) {
            // 降临枢纽 — 居于玩家上方稍近处
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 0.8f) * 80f, -300);
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.03f, 0.08f);
            towerOrbitSpeed = 0.018f;

            // 低压力压制：带充能宝塔点射
            if (AttackTimer % 46 == 0) {
                FireTowerTap(target);
            }

            bool inPosition = toHover.LengthSquared() < 160f * 160f;
            if (PhaseTimer > 90 || (PhaseTimer > 50 && inPosition)) {
                // 固定可读轮替：步战镇压与弹幕控场交替，守护姿态作为呼吸拍
                BossPhase[] rotation = {
                    BossPhase.Phase2_StampFormation,
                    BossPhase.Phase2_QuadrantRay,
                    BossPhase.Phase2_PagodaSuppress,
                    BossPhase.Phase2_ImmortalWave,
                    BossPhase.Phase2_GuardianStance,
                    BossPhase.Phase2_StampFormation,
                    BossPhase.Phase2_QuadrantRay,
                    BossPhase.Phase2_PagodaSuppress
                };
                TransitionTo(rotation[p2Index % rotation.Length]);
                p2Index++;
            }
        }

        private void RunPhase2YakshaSummon(Player target) {
            switch ((int)SubState) {
                case 0: // 召唤阶段
                    NPC.velocity *= 0.9f;
                    Vector2 descendPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, descendPos, 0.04f);

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f }, NPC.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            yakshaMinionIds = new int[TowerCount];
                            for (int i = 0; i < TowerCount; i++) {
                                // 四方锚点：方向 i 的夜叉守在该方向
                                Vector2 spawnPos = NPC.Center + CardinalAngle[i].ToRotationVector2() * 240f;
                                int npcId = NPC.NewNPC(NPC.GetSource_FromAI(),
                                    (int)spawnPos.X, (int)spawnPos.Y,
                                    ModContent.NPCType<YakshaMinion>(),
                                    ai0: NPC.whoAmI, ai1: i);
                                yakshaMinionIds[i] = npcId;
                            }
                            NPC.netUpdate = true;
                        }
                    }

                    if (!VaultUtils.isServer && PhaseTimer <= 30) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2.2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Main.rand.NextVector2Circular(4, 4);
                        }
                    }

                    if (PhaseTimer >= 70) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
            }
        }

        #endregion

        #region 二阶段AI · 四象射线（夜叉锚定安全道）

        /// <summary>
        /// 四象射线 — 夜叉锚定安全道。
        /// 每个基本方向射出固定方位激光；某方向夜叉死亡后，其【对侧】方向开出安全道。
        /// （击杀北方夜叉 → 解锁南方安全区，杀序很重要）
        /// </summary>
        private void RunPhase2QuadrantRay(Player target) {
            switch ((int)SubState) {
                case 0: // 预告
                    NPC.velocity *= 0.9f;
                    Vector2 quadHoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, quadHoverPos, 0.04f);

                    if (PhaseTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);

                    // 预告升级：DrawBeam 金带由绘制层依 SubState/PhaseTimer 画出（见 DrawQuadrantTell）

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 发射四象激光（跳过安全道方向）
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int c = 0; c < 4; c++) {
                                bool safe = !YakshaAlive(Opposite(c));
                                if (safe) continue;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<QuadrantRay>(), NPC.defDamage / 2, 0f, Main.myPlayer,
                                    ai0: NPC.whoAmI, ai1: CardinalAngle[c]);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        bodyFlash = 0.6f;
                        if (!VaultUtils.isServer)
                            ACMScreenShakeSystem.Add(12f);
                    }

                    if (PhaseTimer > 100) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
            }
        }

        #endregion

        #region 二阶段AI · 仙气地波

        /// <summary>
        /// 仙气地波 — 随地形起伏的冲击环，吸附地表/平台高度，迫使纵向跳跃走位。
        /// </summary>
        private void RunPhase2ImmortalWave(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力预告
                    NPC.velocity *= 0.92f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);

                    if (PhaseTimer >= 40) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放地波
                    if (PhaseTimer % 45 == 1 && PhaseTimer <= 140) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 左右各一道随地形起伏的冲击波
                            Vector2 spawn = new Vector2(target.Center.X, target.Center.Y - 40);
                            float speed = Main.expertMode ? 9f : 7.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(-speed, 0),
                                ModContent.ProjectileType<ImmortalGroundShock>(), NPC.defDamage / 3, 0f, Main.myPlayer);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(speed, 0),
                                ModContent.ProjectileType<ImmortalGroundShock>(), NPC.defDamage / 3, 0f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.1f }, NPC.Center);
                        if (!VaultUtils.isServer)
                            ACMScreenShakeSystem.Add(8f);
                    }

                    if (PhaseTimer > 175) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
            }
        }

        #endregion

        #region 二阶段AI · 天王踏阵

        /// <summary>
        /// 天王踏阵：四连天王步，架步渐短（26→20f）、步频音阶递升；
        /// 第四步「震地大踏」——落点提前 26f 坛城预告，落地震屏 10 + 双向全程地波 +
        /// 留缝金环。接触伤害仅各步跨步窗口。
        /// </summary>
        private void RunPhase2StampFormation(Player target) {
            // 状态机保底出口
            if (AttackTimer > 700) {
                TransitionTo(BossPhase.Phase2_Hub);
                return;
            }

            bool finalStamp = dashCount >= 3;

            switch ((int)SubState) {
                case 0: { // 架步
                    int windup = finalStamp ? 34 : Math.Max(20, 26 - dashCount * 2);

                    if (PhaseTimer == 1) {
                        stepStart = NPC.Center;
                        PickStepTarget(target);
                        // 步频音阶递升
                        SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.2f + dashCount * 0.18f, Volume = 1f }, NPC.Center);
                    }

                    StepAnticipate(PhaseTimer / (float)windup);

                    if (PhaseTimer >= windup) {
                        SubState = 1;
                        PhaseTimer = 0;
                        StepLaunch();
                    }
                    break;
                }

                case 1: { // 跨步（接触伤害窗口）
                    NPC.damage = NPC.defDamage;

                    bool passed = Vector2.Dot(dashTarget - NPC.Center, stepDir) < 0f;
                    if (PhaseTimer >= stepTravelNeeded || passed) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 2: { // 落地
                    if (PhaseTimer == 1) {
                        if (finalStamp) {
                            // 震地大踏：全程地波 + 留缝金环 + 强震
                            StepLandImpact(spawnShock: true, shockTravel: 2400f, shockDamageDiv: 3);
                            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                            bodyFlash = 0.8f;
                            if (!VaultUtils.isServer) {
                                ACMScreenShakeSystem.Add(10f);
                                VaisravanaTreasureScreenSystem.PulseBloom(0.5f);
                            }
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                float safeAngle = (target.Center - NPC.Center).ToRotation();
                                int count = 18;
                                float safeHalf = MathHelper.ToRadians(40);
                                for (int i = 0; i < count; i++) {
                                    float angle = MathHelper.TwoPi * i / count;
                                    if (MathF.Abs(MathHelper.WrapAngle(angle - safeAngle)) < safeHalf) continue;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 6f,
                                        ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 4, 1f, Main.myPlayer);
                                }
                            }
                        }
                        else {
                            StepLandImpact(spawnShock: dashCount % 2 == 0, shockTravel: 560f);
                        }
                    }

                    NPC.velocity *= 0.62f;

                    int settleTime = finalStamp ? 40 : 16;
                    if (PhaseTimer >= settleTime) {
                        dashCount++;
                        if (dashCount >= 4) {
                            TransitionTo(BossPhase.Phase2_Hub);
                        }
                        else {
                            SubState = 0;
                            PhaseTimer = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
            }
        }

        #endregion

        #region 二阶段AI · 塔光柱镇压

        /// <summary>
        /// 塔光柱镇压（P2 代表招）：两座宝塔飞至玩家两侧上空，塔下天光柱 45f 细线预告 →
        /// 90f 全宽爆发并以 0.55px/f 向中缝夹击。中缝始终 ≥260px（公平阀门），
        /// 逼迫玩家在收窄的金柱走廊里贴塔窃取赐福（风险奖励闭环）。
        /// </summary>
        private void RunPhase2PagodaSuppress(Player target) {
            switch ((int)SubState) {
                case 0: { // 布阵：本体后撤上移，让出舞台
                    Vector2 stagePos = target.Center + new Vector2(0, -430);
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, stagePos, ref dashVelocity, 2.0f, 7f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.1f, Volume = 1.2f }, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 双柱夹击：左右各一根，模式 1（收拢），ai1=收拢方向
                            float halfGap = 480f;
                            for (int s = -1; s <= 1; s += 2) {
                                Vector2 spawn = new(target.Center.X + s * halfGap, target.Center.Y);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, Vector2.Zero,
                                    ModContent.ProjectileType<VaisravanaLightPillar>(), NPC.defDamage / 2, 0f, Main.myPlayer,
                                    ai0: 1f, ai1: -s);
                            }
                            NPC.netUpdate = true;
                        }
                    }

                    if (PhaseTimer >= 30) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 1: { // 镇压运行期：塔口点射维持压力
                    NPC.velocity *= 0.94f;

                    if (PhaseTimer % 55 == 30)
                        FireTowerTap(target);

                    if (PhaseTimer > 165) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
                }
            }
        }

        #endregion

        #region 二阶段AI · 宝伞格挡

        /// <summary>
        /// 宝伞格挡 — 守护者身份窗口。伞盖张开（24f 展开动画）期间无敌 + 劫财反震；
        /// 玩家应停火走位躲避留缝金环。窗口结束伞收，留 20f 破防硬直（可打窗口）。
        /// </summary>
        private void RunPhase2GuardianStance(Player target) {
            switch ((int)SubState) {
                case 0: // 入场钉死 + 伞盖展开
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref dashVelocity, 0.5f, 12f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);

                    if (PhaseTimer >= 24) {
                        SubState = 1;
                        PhaseTimer = 0;
                        guardActive = true;
                        NPC.dontTakeDamage = true;
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                    }
                    break;

                case 1: // 守护窗口：缓速金环逼走位，反震惩罚输出
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref dashVelocity, 0.5f, 12f, 1f / 60f) - NPC.Center;
                    guardActive = true;
                    NPC.dontTakeDamage = true;

                    if (PhaseTimer % 26 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 缓速金环（留缝），玩家需走位
                        float safeAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        int count = 22;
                        float safeHalf = MathHelper.ToRadians(40);
                        for (int i = 0; i < count; i++) {
                            float angle = MathHelper.TwoPi * i / count;
                            if (MathF.Abs(MathHelper.WrapAngle(angle - safeAngle)) < safeHalf) continue;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 5.5f,
                                ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 4, 1f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f }, NPC.Center);
                    }

                    if (PhaseTimer > 130) {
                        SubState = 2;
                        PhaseTimer = 0;
                        guardActive = false;
                        NPC.dontTakeDamage = false;
                        guardVisual = 1.6f; // 破防闪
                        SoundEngine.PlaySound(SoundID.NPCHit42 with { Pitch = 0.3f }, NPC.Center);
                    }
                    break;

                case 2: // 破防硬直（可打窗口）
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer >= 20) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
            }
        }

        #endregion

        #region 三阶段AI · 库藏封印（脚本化 A/B/C 轮替）

        private void RunPhase3SealRings(Player target) {
            // A 幕 · 金环收束：向内收缩的金环，标记安全道
            switch ((int)SubState) {
                case 0: // 预告安全道（Safe 翠玉双线由绘制层画出）
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -260), ref dashVelocity, 2.2f, 6f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        // 安全道角度：避开仍存活夜叉的方向（存活方向被「强化」=危险）
                        laserAngle = ChooseSealSafeAngle();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
                        NPC.netUpdate = true;
                    }

                    if (PhaseTimer >= 55) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放收缩金环（数波）
                    if (PhaseTimer % 40 == 1 && PhaseTimer <= 160) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float startRadius = 780f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<TreasurySealRing>(), NPC.defDamage / 3, 0f, Main.myPlayer,
                                ai0: laserAngle, ai1: startRadius);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                        if (!VaultUtils.isServer)
                            VaisravanaTreasureScreenSystem.PulseBloom(0.32f);
                    }

                    if (PhaseTimer > 200) {
                        AdvanceSealCycle();
                    }
                    break;
            }
        }

        /// <summary>选择金环安全道角度：避开存活夜叉的方向。</summary>
        private float ChooseSealSafeAngle() {
            // 收集没有存活夜叉的方向作为候选安全道
            System.Collections.Generic.List<int> open = new();
            for (int c = 0; c < 4; c++)
                if (!YakshaAlive(c)) open.Add(c);

            if (open.Count == 0)
                return Main.rand.NextFloat(MathHelper.TwoPi); // 夜叉全活：随机缝（更难）

            int pick = open[Main.rand.Next(open.Count)];
            return CardinalAngle[pick];
        }

        /// <summary>
        /// B 幕 · 夜叉镜射：沿镜轴对称发射的镜弹，只有站在反射轴线上才能安全穿过。
        /// </summary>
        private void RunPhase3YakshaMirror(Player target) {
            switch ((int)SubState) {
                case 0: // 选定镜轴 + 预告（金色安全轴由绘制层 DrawMirrorAxisTell 画出）
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -280), ref dashVelocity, 2.2f, 6f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        mirrorAxis = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.7f }, NPC.Center);
                        NPC.netUpdate = true;
                    }

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 镜射波
                    if (PhaseTimer % 18 == 1 && PhaseTimer <= 150) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            FireMirrorVolley(target);
                        }
                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.5f }, NPC.Center);
                    }

                    if (PhaseTimer > 175) {
                        AdvanceSealCycle();
                    }
                    break;
            }
        }

        /// <summary>发射一组沿镜轴对称的镜弹：安全点在反射轴线上。</summary>
        private void FireMirrorVolley(Player target) {
            // 以镜轴为对称轴，向玩家所在侧与其镜像侧各发一发会聚镜弹
            Vector2 axisDir = mirrorAxis.ToRotationVector2();
            Vector2 perp = axisDir.RotatedBy(MathHelper.PiOver2);

            float side = Vector2.Dot(target.Center - NPC.Center, perp);
            float offset = MathHelper.Clamp(MathF.Abs(side), 120f, 360f);

            for (int s = -1; s <= 1; s += 2) {
                Vector2 origin = NPC.Center + perp * offset * s + axisDir * Main.rand.NextFloat(-60f, 60f);
                Vector2 toAxisPoint = (NPC.Center + axisDir * Vector2.Dot(origin - NPC.Center, axisDir)) - origin;
                Vector2 vel = toAxisPoint.SafeNormalize(perp * -s) * 13f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, vel,
                    ModContent.ProjectileType<YakshaMirrorBolt>(), NPC.defDamage / 3, 2f, Main.myPlayer);
            }
        }

        /// <summary>
        /// C 幕 · 终极宝塔：单束终极激光，70f 蓄力（专属坛城地纹逐圈点亮 + 四塔金链汇能）
        /// → 发射瞬间本体反冲 + 白闪 + 金爆。绝不与高频喷射重叠。
        /// </summary>
        private void RunPhase3UltimateTower(Player target) {
            switch ((int)SubState) {
                case 0: // 70f 蓄力 + 地纹预告
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -320), ref dashVelocity, 1.6f, 7f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.6f }, NPC.Center);

                        // 地纹预告：在激光朝向的地面投影处铺设符文
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<TreasurySealRune>(), 0, 0f, Main.myPlayer,
                                ai0: NPC.whoAmI, ai1: laserAngle);
                        }
                    }

                    // 蓄力期持续微调朝向（可读），并震屏渐强
                    laserAngle = MathHelper.Lerp(laserAngle, (target.Center - NPC.Center).ToRotation(), 0.03f);

                    // 汇聚金流（72% 硬切，最后一段静默）
                    if (!VaultUtils.isServer && PhaseTimer < 50 && PhaseTimer % 2 == 0) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(220, 220);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.6f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 13f;
                    }
                    if (!VaultUtils.isServer && PhaseTimer % 8 == 0)
                        ACMScreenShakeSystem.Add(MathHelper.Clamp(PhaseTimer / 10f, 0f, 12f));

                    if (PhaseTimer >= 70) {
                        SubState = 1;
                        PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<TreasureTowerRay>(), (int)(NPC.defDamage * 1.4f), 0f, Main.myPlayer,
                                ai0: NPC.whoAmI, ai1: laserAngle);
                        }
                        // 发射：本体反冲 + 白闪 + 金爆 + 强震
                        NPC.velocity = -laserAngle.ToRotationVector2() * 14f;
                        bodyFlash = 1f;
                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            ACMScreenShakeSystem.Add(12f);
                            VaisravanaTreasureScreenSystem.PulseBloom(1f);
                            VaisravanaTreasureScreenSystem.PulseWhiteFlash(0.35f);
                        }
                    }
                    break;

                case 1: // 激光持续（缓慢扫向玩家，可读）
                    NPC.velocity *= 0.9f;
                    laserAngle = MathHelper.Lerp(laserAngle, (target.Center - NPC.Center).ToRotation(), 0.016f);

                    if (PhaseTimer > 110) {
                        AdvanceSealCycle();
                    }
                    break;
            }
        }

        /// <summary>
        /// 幕间宝伞节拍：短暂守护反击窗口 + 宝塔回充，作为可读性阀门，避免连续高压。
        /// </summary>
        private void RunPhase3SealBeat(Player target) {
            NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -300), ref dashVelocity, 1.2f, 8f, 1f / 60f) - NPC.Center;

            if (PhaseTimer == 1) {
                guardActive = true;
                NPC.dontTakeDamage = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f }, NPC.Center);
                for (int i = 0; i < TowerCount; i++)
                    if (towerCharges[i] < MaxTowerCharge) towerCharges[i]++;
            }

            // 守护期间放一圈缓速金环逼走位
            if (PhaseTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                float safeAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                int count = 20;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    if (MathF.Abs(MathHelper.WrapAngle(angle - safeAngle)) < MathHelper.ToRadians(40)) continue;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 5f,
                        ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 4, 1f, Main.myPlayer);
                }
            }

            if (PhaseTimer > 75) {
                guardActive = false;
                NPC.dontTakeDamage = false;
                guardVisual = 1.4f;
                BossPhase next = sealCycle switch {
                    0 => BossPhase.Phase3_SealRings,
                    1 => BossPhase.Phase3_YakshaMirror,
                    _ => BossPhase.Phase3_UltimateTower
                };
                TransitionTo(next);
            }
        }

        /// <summary>推进库藏封印轮替并进入幕间节拍。</summary>
        private void AdvanceSealCycle() {
            sealCycle = (sealCycle + 1) % 3;
            TransitionTo(BossPhase.Phase3_SealBeat);
        }

        #endregion
    }
}

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

        #region 二阶段AI · 天王降临

        private void RunPhase2Hub(Player target) {
            // 降临枢纽 — 居于玩家上方稍近处
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 0.8f) * 80f, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.08f);
            towerOrbitSpeed = 0.018f;

            // 低压力压制：带充能宝塔点射
            if (AttackTimer % 50 == 0) {
                FireTowerTap(target);
            }

            if (PhaseTimer > 130) {
                // 固定可读轮替：四象射线 → 仙气地波 → 守护姿态
                BossPhase[] rotation = {
                    BossPhase.Phase2_QuadrantRay,
                    BossPhase.Phase2_ImmortalWave,
                    BossPhase.Phase2_GuardianStance,
                    BossPhase.Phase2_QuadrantRay,
                    BossPhase.Phase2_ImmortalWave
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

                    if (!VaultUtils.isServer) {
                        for (int c = 0; c < 4; c++) {
                            bool safe = !YakshaAlive(Opposite(c));
                            if (safe) continue; // 安全道不画危险标线
                            TelegraphLine(NPC.Center, CardinalAngle[c].ToRotationVector2(), 12, DustID.GoldFlame);
                        }
                    }

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
                                    ModContent.ProjectileType<QuadrantRay>(), NPC.damage / 2, 0f, Main.myPlayer,
                                    ai0: NPC.whoAmI, ai1: CardinalAngle[c]);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        if (!VaultUtils.isServer)
                            ACMScreenShakeSystem.Add(12f);
                    }

                    if (PhaseTimer > 100) {
                        TransitionTo(BossPhase.Phase2_Hub);
                    }
                    break;
            }
        }

        /// <summary>
        /// 仙气地波 — 随地形起伏的冲击环，吸附地表/平台高度，迫使纵向跳跃走位。
        /// （区别于观察者的平面环）
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
                                ModContent.ProjectileType<ImmortalGroundShock>(), NPC.damage / 3, 0f, Main.myPlayer);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(speed, 0),
                                ModContent.ProjectileType<ImmortalGroundShock>(), NPC.damage / 3, 0f, Main.myPlayer);
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

        /// <summary>
        /// 守护姿态 — 守护者身份窗口（借鉴玄武 绝对防御）。
        /// Boss 钉死并开启无敌/反震；玩家应停火、靠走位躲避缓速金环，
        /// 而非继续输出（输出会被劫财反震）。窗口结束有破防硬直。
        /// </summary>
        private void RunPhase2GuardianStance(Player target) {
            switch ((int)SubState) {
                case 0: // 入场钉死 + 预告
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref dashVelocity, 0.5f, 12f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);

                    if (PhaseTimer >= 30) {
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
                                ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 4, 1f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f }, NPC.Center);
                    }

                    if (PhaseTimer > 150) {
                        guardActive = false;
                        NPC.dontTakeDamage = false;
                        guardVisual = 1.6f; // 破防闪
                        SoundEngine.PlaySound(SoundID.NPCHit42 with { Pitch = 0.3f }, NPC.Center);
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
                case 0: // 预告安全道
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -260), ref dashVelocity, 2.2f, 6f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        // 安全道角度：避开仍存活夜叉的方向（存活方向被「强化」=危险）
                        laserAngle = ChooseSealSafeAngle();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
                    }

                    if (!VaultUtils.isServer) {
                        Vector2 c = NPC.Center;
                        for (int s = -1; s <= 1; s++) {
                            float a = laserAngle + s * MathHelper.ToRadians(30);
                            TelegraphLine(c, a.ToRotationVector2(), 12, DustID.GoldCoin);
                        }
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
                                ModContent.ProjectileType<TreasurySealRing>(), NPC.damage / 3, 0f, Main.myPlayer,
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
                case 0: // 选定镜轴 + 预告
                    NPC.velocity = ACMUtils.SpringDamp2D(NPC.Center, target.Center + new Vector2(0, -280), ref dashVelocity, 2.2f, 6f, 1f / 60f) - NPC.Center;

                    if (PhaseTimer == 1) {
                        mirrorAxis = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.7f }, NPC.Center);
                    }

                    if (!VaultUtils.isServer) {
                        // 画出镜轴（安全线）
                        Vector2 dir = mirrorAxis.ToRotationVector2();
                        for (int i = -10; i <= 10; i++) {
                            int dust = Dust.NewDust(NPC.Center + dir * (i * 70f), 0, 0, DustID.WhiteTorch, 0, 0, 150, default, 1f);
                            Main.dust[dust].noGravity = true;
                        }
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
                    ModContent.ProjectileType<YakshaMirrorBolt>(), NPC.damage / 3, 2f, Main.myPlayer);
            }
        }

        /// <summary>
        /// C 幕 · 终极宝塔：单束终极激光，约 70 tick 蓄力 + 地纹预告，绝不与 10tick 喷射重叠。
        /// </summary>
        private void RunPhase3UltimateTower(Player target) {
            switch ((int)SubState) {
                case 0: // 70 tick 蓄力 + 地纹预告
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

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 12; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(220, 220);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.6f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 13f;
                        }
                        if (PhaseTimer % 8 == 0)
                            ACMScreenShakeSystem.Add(MathHelper.Clamp(PhaseTimer / 10f, 0f, 12f));
                        // 坛城地纹与本体金爆由 ScreenSystem(逐圈) 与 PreDraw(DrawRadialBloomAt) 演出, 此处仅震屏渐强
                    }

                    if (PhaseTimer >= 70) {
                        SubState = 1;
                        PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<TreasureTowerRay>(), (int)(NPC.damage * 1.4f), 0f, Main.myPlayer,
                                ai0: NPC.whoAmI, ai1: laserAngle);
                        }
                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            // 终极宝塔·财神镇压：金爆泛光 + 强震屏
                            ACMScreenShakeSystem.Add(12f);
                            VaisravanaTreasureScreenSystem.PulseBloom(1f);
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
        /// 幕间守护节拍：短暂守护反击窗口 + 宝塔回充，作为可读性阀门，避免连续高压。
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
                        ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 4, 1f, Main.myPlayer);
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

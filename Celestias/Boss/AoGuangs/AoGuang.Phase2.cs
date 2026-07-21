using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 浪墙层涌 (P2 签名 / P3 强化)

        /// <summary>
        /// 浪墙层涌 — 敖广的签名招: 龙王退至上游举戟, 水被吸走 (负空间预警),
        /// 随后多波整面浪墙横扫战场, 每波留一个 Safe 翠玉描边的穿越缺口 (缺口逐波换位)。
        /// P2: 3 波 / 波间 100f / 缺口 150px; P3: 4 波 / 90f / 130px。
        /// </summary>
        private void RunTsunamiWaves(Player target) {
            int waveCount = IsPhase3 ? 4 : 3;
            int waveGapTime = IsPhase3 ? 90 : 100;
            float gapHalf = IsPhase3 ? 130f : 150f;
            float wallSpeed = IsPhase3 ? 10f : 9f;

            switch ((int)SubState) {
                case 0: // 前摇 60f: 退至上游侧, 举戟, 水流倒吸
                    if (AttackTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        wallDir = Main.rand.NextBool() ? 1f : -1f; // 浪从哪侧来 (服务器决定)
                        NPC.netUpdate = true;
                    }
                    {
                        Vector2 anchor = target.Center + new Vector2(-wallDir * 780f, -260f);
                        SerpentineGlide(anchor, 0.075f, 0.11f, 2f);
                        NPC.spriteDirection = wallDir >= 0 ? 1 : -1;

                        if (AttackTimer > 24) {
                            float t = MathHelper.Clamp((AttackTimer - 24f) / 30f, 0f, 1f);
                            poseRotOverride = PoseAngle(-0.7f * ACMUtils.QuadInOut(t), NPC.spriteDirection);
                        }

                        // 负空间预警: 全场水珠被吸向龙王 (海在退潮)
                        if (!VaultUtils.isServer && AttackTimer > 14) {
                            for (int i = 0; i < 3; i++) {
                                Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(420f, 320f);
                                Dust d = Dust.NewDustDirect(from, 0, 0, DustID.Water, 0, 0, 110, default, 1.8f);
                                d.noGravity = true;
                                d.velocity = (NPC.Center - from) * 0.05f;
                            }
                        }
                        if (AttackTimer == 30)
                            SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);

                        if (AttackTimer >= 60) { SubState = 1; AttackTimer = 0; chargeCount = 0; }
                    }
                    break;

                case 1: // 波次循环: 戟挥落 → 浪墙出闸
                    {
                        // 每波开头挥戟
                        float inWave = AttackTimer % waveGapTime;
                        if (inWave < 8) {
                            float strike = 1f - MathF.Pow(1f - inWave / 8f, 8f);
                            poseRotOverride = PoseAngle(MathHelper.Lerp(-0.7f, 0.4f, strike), NPC.spriteDirection);
                        }

                        if (inWave == 1 && chargeCount < waveCount) {
                            chargeCount++;
                            SpawnTsunamiWall(target, wallDir, gapHalf, wallSpeed);
                            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.25f, Volume = 1.4f }, NPC.Center);
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f, Volume = 1f }, NPC.Center);
                            ACMUtils.AddScreenShake(9f);
                            waterBloom = MathF.Max(waterBloom, 0.5f);
                            // 出浪后座
                            NPC.velocity += new Vector2(-wallDir * 8f, -2f);
                        }

                        // 波间: 平行于浪墙缓移压场 (存在感, 不再叠压力)
                        Vector2 holdAnchor = target.Center + new Vector2(-wallDir * 760f, -240f + MathF.Sin(globalTime * 1.8f) * 60f);
                        SerpentineGlide(holdAnchor, 0.05f, 0.08f, 2.2f);

                        if (chargeCount >= waveCount && inWave >= waveGapTime - 1) {
                            TransitionTo(BossPhase.Cruise);
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 水龙卷投掷 (P2)

        /// <summary>
        /// 水龙卷投掷 — 甩尾蓄势 30f 后掷出一根行走水龙卷 (落点红标, 落地成柱, 缓速平移),
        /// 共 2 次。龙卷慢速可跳越, 同场 ≤2 根 (公平阀门)。
        /// </summary>
        private void RunTornadoThrow(Player target) {
            switch ((int)SubState) {
                case 0: // 甩尾蓄势 30f
                    {
                        float side = NPC.Center.X > target.Center.X ? 1f : -1f;
                        Vector2 anchor = target.Center + new Vector2(side * 480f, -280f);
                        SerpentineGlide(anchor, 0.06f, 0.09f, 4.5f); // 大摆幅 = 尾部卷水

                        // 卷水粒子: 绕龙躯旋转汇聚
                        if (!VaultUtils.isServer) {
                            float ang = AttackTimer * 0.35f;
                            for (int i = 0; i < 2; i++) {
                                Vector2 dustPos = NPC.Center + (ang + i * MathHelper.Pi).ToRotationVector2() * 90f;
                                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Water, 0, 0, 120, default, 2f);
                                d.noGravity = true;
                                d.velocity = (ang + i * MathHelper.Pi + MathHelper.PiOver2).ToRotationVector2() * 6f;
                            }
                        }

                        if (AttackTimer >= 30) {
                            SubState = 1;
                            AttackTimer = 0;

                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                // 同场龙卷 ≤2 (公平阀门)
                                int alive = 0;
                                int spoutType = ModContent.ProjectileType<AoGuangWaterspout>();
                                for (int i = 0; i < Main.maxProjectiles; i++) {
                                    if (Main.projectile[i].active && Main.projectile[i].type == spoutType)
                                        alive++;
                                }
                                if (alive < 2) {
                                    float walkDir = target.Center.X > NPC.Center.X ? 1f : -1f;
                                    float landX = target.Center.X + walkDir * Main.rand.NextFloat(60f, 200f);
                                    float groundY = FindGroundY(landX, target.Center.Y - 200f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                        new Vector2(landX, groundY), new Vector2(walkDir * 2.6f, 0f),
                                        ModContent.ProjectileType<AoGuangWaterspout>(), NPC.damage / 4, 1f);
                                }
                                // 投掷后座
                                NPC.velocity += new Vector2(-side * 7f, -3f);
                                NPC.netUpdate = true;
                            }
                            SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.35f, Volume = 1.2f }, NPC.Center);
                            ACMUtils.AddScreenShake(6f);
                        }
                    }
                    break;

                case 1: // 投掷后间隔 24f, 共 2 掷
                    NPC.velocity *= 0.94f;
                    if (AttackTimer >= 24) {
                        chargeCount++;
                        if (chargeCount >= 2) {
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

        #region 龙息水柱 (P2 / P3 强化)

        /// <summary>
        /// 龙息水柱 — 蓄力 60f (汇聚流光, 72% 后静默收声 — 尖啸前的吸气),
        /// 后释放恒速扫射水束 (P2: DragonBreathBeam 90f / P3: TidalBeam 100f)。
        /// 公平阀门: 蓄力后半段角度锁死, 扫射转速恒定可绕行, Lethal 路径线全程可读。
        /// </summary>
        private void RunDragonBreath(Player target) {
            bool strong = IsPhase3;
            int chargeTime = 60;

            switch ((int)SubState) {
                case 0: // 蓄力 60f
                    {
                        if (AttackTimer == 1) {
                            SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                sweepDir = target.velocity.X >= 0f ? 1f : -1f; // 顺着玩家动向扫 (被读的是走位习惯)
                                NPC.netUpdate = true;
                            }
                        }

                        float charge = AttackTimer / (float)chargeTime;

                        // 身体后漂: 龙王避开自己的武器 (counter-motion)
                        Vector2 hoverPos = target.Center + new Vector2(0, -330f)
                            - NPC.SafeDirectionTo(target.Center) * charge * charge * 160f;
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.09f);

                        // 蓄力前 2/3 各端确定性追踪 (target 已同步), 锁定帧服务器纠偏; 后 1/3 锁死角度 (可读)
                        if (AttackTimer <= chargeTime * 2 / 3) {
                            breathAngle = (target.Center - NPC.Center).ToRotation();
                            if (AttackTimer == chargeTime * 2 / 3 && Main.netMode != NetmodeID.MultiplayerClient)
                                NPC.netUpdate = true;
                        }
                        NPC.spriteDirection = MathF.Cos(breathAngle) >= 0 ? 1 : -1;
                        poseRotOverride = breathAngle;

                        // 汇聚流光 ∝ sqrt(charge), 72% 处硬切 (静默拍)
                        if (!VaultUtils.isServer && charge < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(charge)) {
                            Vector2 mouth = NPC.Center + breathAngle.ToRotationVector2() * 70f;
                            for (int i = 0; i < 3; i++) {
                                Vector2 from = mouth + Main.rand.NextVector2CircularEdge(200f, 200f);
                                Dust d = Dust.NewDustDirect(from, 0, 0,
                                    Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch, 0, 0, 90, default, 2f);
                                d.noGravity = true;
                                d.velocity = (mouth - from) * 0.085f;
                            }
                        }

                        // Lethal 路径预警线
                        if (!VaultUtils.isServer && AttackTimer > 14) {
                            Vector2 beamDir = breathAngle.ToRotationVector2();
                            int count = strong ? 15 : 11;
                            for (int i = 0; i < count; i++) {
                                Vector2 lp = NPC.Center + beamDir * (80 + i * 150);
                                Dust d = Dust.NewDustDirect(lp, 0, 0, DustID.RedTorch, 0, 0, 120,
                                    TelegraphColors.Lethal, 1.4f);
                                d.noGravity = true;
                                d.velocity = beamDir * 2f;
                            }
                        }

                        // 蓄力震屏: charge³ 曲线 (慢起狠收)
                        if (AttackTimer % 8 == 0)
                            ACMUtils.AddScreenShake(charge * charge * charge * (strong ? 12f : 9f));

                        if (AttackTimer >= chargeTime) {
                            SubState = 1;
                            AttackTimer = 0;

                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                int beamType = strong
                                    ? ModContent.ProjectileType<TidalBeam>()
                                    : ModContent.ProjectileType<DragonBreathBeam>();
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    beamType, strong ? NPC.damage : NPC.damage / 2, 0f,
                                    ai0: NPC.whoAmI, ai1: breathAngle);
                            }

                            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                            if (strong)
                                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.3f, Volume = 1.6f }, NPC.Center);
                            ACMUtils.AddScreenShake(strong ? 12f : 10f);
                            waterBloom = 1f;
                            // 龙息后座: 持续被水束推着退
                            NPC.velocity -= breathAngle.ToRotationVector2() * 10f;
                        }
                    }
                    break;

                case 1: // 扫射 (P2 90f / P3 100f): 恒速转动, 玩家绕行即可解
                    {
                        int fireTime = strong ? 100 : 90;
                        NPC.velocity *= 0.92f;
                        // 恒定转速 (不 lerp 追人 — 可预测的压力才是好压力)
                        breathAngle += sweepDir * (strong ? 0.022f : 0.020f);
                        poseRotOverride = breathAngle;
                        NPC.spriteDirection = MathF.Cos(breathAngle) >= 0 ? 1 : -1;

                        // 持续微后座
                        NPC.velocity -= breathAngle.ToRotationVector2() * 0.35f;

                        if (AttackTimer > fireTime) { SubState = 2; AttackTimer = 0; }
                    }
                    break;

                case 2: // 收招 26f
                    NPC.velocity *= 0.94f;
                    if (AttackTimer >= 26) TransitionTo(BossPhase.Cruise);
                    break;
            }
        }

        #endregion
    }
}

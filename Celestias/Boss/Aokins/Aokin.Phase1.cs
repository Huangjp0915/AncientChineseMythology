using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 火弹齐射（小压制）

        /// <summary>火弹扇射 - 向玩家扇形发射火弹。</summary>
        private bool RunFireBarrage(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 100f, -350);
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.04f, 0.1f);

            int fireInterval = Main.expertMode ? 12 : 16;
            if (attackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int bulletCount = Main.expertMode ? 5 : 3;
                if (IsPhase2) bulletCount += 2;
                float spreadAngle = MathHelper.ToRadians(12f);

                for (int i = -bulletCount / 2; i <= bulletCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 10f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        NPC.Center + toPlayer * 50f, vel,
                        ModContent.ProjectileType<AokinFireball>(), NPC.damage / 3, 1f);
                }
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.8f }, NPC.Center);
            }

            if (!VaultUtils.isServer && attackTimer % 5 == 0) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 3; i++) {
                    Vector2 dustVel = toPlayer.RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 6);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, dustVel.X, dustVel.Y, 180, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (attackTimer > 150) {
                AddHeat(12f);
                return true;
            }
            return false;
        }

        #endregion

        #region 龙息喷射

        /// <summary>龙息喷射 - 朝玩家方向持续喷射火焰。</summary>
        private bool RunDragonBreath(Player target) {
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            if (target.Distance(NPC.Center) > 600) {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) / 3f;
                NPC.velocity *= 0.97f;
            }
            else {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) / 30f;
                NPC.velocity *= 0.99f;
            }

            int fireInterval = Main.expertMode ? 6 : 8;
            if (attackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int damage = Main.expertMode ? 35 : 50;
                float speed = Main.expertMode ? 14f : 10f;

                Vector2 direction = NPC.rotation.ToRotationVector2();
                Vector2 vel = direction.RotatedByRandom(MathHelper.ToRadians(8)) * speed;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    NPC.Center + direction * 50f, vel,
                    ModContent.ProjectileType<AokinFireball>(), damage, 1f);
                Main.projectile[p].timeLeft = 100;
            }

            if (!VaultUtils.isServer) {
                Vector2 breathDir = NPC.rotation.ToRotationVector2();
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + breathDir * (30 + i * 10);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, breathDir.X * 8, breathDir.Y * 8, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (attackTimer > 180) {
                AddHeat(16f);
                return true;
            }
            return false;
        }

        #endregion

        #region 劫火印记 — 预告式顺序火柱波（替代陨石刷屏）

        /// <summary>
        /// 劫火印记：在地面上按顺序（横扫方向）落下一串火柱印记，每柱独立 telegraph→喷发。
        /// 替代旧版"随机陨石刷屏"为可读的"读印记节奏走位"机制。
        /// </summary>
        private bool RunEmberPillars(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -380);
            NPC.velocity += (hoverPos - NPC.Center) * 0.0025f;

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
            if (attackTimer % interval == 0 && index < pillarCount && Main.netMode != NetmodeID.MultiplayerClient) {
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

            // 蓄力期向心粒子
            if (!VaultUtils.isServer && attackTimer < 24) {
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            int total = pillarCount * interval + 100;
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
                case 1: { // 收紧盘旋
                    float coilSpeed = IsPhase2 ? 0.13f : 0.10f;
                    coilAngle += coilSpeed;
                    float minRadius = IsPhase3 ? 150f : 200f;
                    coilRadius = MathHelper.Lerp(coilRadius, minRadius, 0.012f);

                    Vector2 desired = target.Center + coilAngle.ToRotationVector2() * coilRadius;
                    NPC.velocity = (desired - NPC.Center) * 0.35f;

                    if (!VaultUtils.isServer && attackTimer % 2 == 0)
                        AokinHelper.CreateFireTrail(NPC.Center, NPC.velocity, 1.1f);

                    // 收到足够紧 / 足够久 → 甩出
                    if (coilRadius <= minRadius + 12f || attackTimer > (IsPhase2 ? 240 : 200)) {
                        subState = 2;
                        attackTimer = 0;
                        Vector2 outDir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = outDir * (IsPhase2 ? 30f : 24f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                    }
                    break;
                }
                case 2: { // 甩出余波
                    NPC.velocity *= 0.95f;
                    if (attackTimer > 30) {
                        AddHeat(18f);
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

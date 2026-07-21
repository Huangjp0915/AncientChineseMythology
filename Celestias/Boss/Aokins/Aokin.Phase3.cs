using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 阶段转换：25% — 焚海劫（熔潮场地改造）

        /// <summary>
        /// 焚海劫 PhaseTransition3：改规则的真三阶段（非更快的二阶段）。
        ///   - 入场即清弹（公平阀门）; 点燃封路龙卷向内收缩（ArenaHalfWidth 已随 PhaseRegion 收缩, 龙卷读取）。
        ///   - runic 场地纹常驻亮起, 铺设第一波熔潮裂隙：地面熔岩柱阵留出安全平台缝隙，
        ///     玩家从此只能在安全缝间走位。
        /// </summary>
        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -360);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            if (attackTimer == 1) {
                ClearAokinProjectiles(includeFieldHazards: false);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
            }

            // 熔潮升温
            emberHeat = Math.Min(MaxEmberHeat, emberHeat + 0.6f);
            heatWarp = Math.Max(heatWarp, MathHelper.Clamp(attackTimer / 80f, 0f, 1f) * 0.6f);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10 + attackTimer * 0.05f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (90 + attackTimer * 1.3f);
                    int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Torch;
                    var d = Dust.NewDustPerfect(dustPos, dustType, (angle + MathHelper.PiOver2).ToRotationVector2() * 9f, 150, default, 3f);
                    d.noGravity = true;
                }
            }

            if (attackTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 2f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                lavaBloom = 1f;
                if (!VaultUtils.isServer)
                    AokinHelper.CreateDragonFireBurst(NPC.Center, 380f, 4, 24);
            }

            // 铺设第一波熔潮裂隙
            if (attackTimer == 80 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnMoltenTideWave(target, 0);
            }

            if (attackTimer > 130) {
                didPhase3Transition = true;
                NPC.dontTakeDamage = false;
                emberHeat = Math.Max(emberHeat, MaxEmberHeat * 0.5f);
                EnterPatrol();
            }
        }

        #endregion

        #region 熔潮涌动 — P3 攻击（再触发熔岩裂隙）

        /// <summary>
        /// 熔潮涌动：焚海劫期间的招牌攻击——盘空后再起两波熔岩裂隙柱阵（交错缺口），
        /// 同时点状落火球施压。缺口位置每波偏移，逼玩家持续在安全平台间迁移。
        /// Boss 随波次向两侧压场（不是原地悬停）。
        /// </summary>
        private bool RunMoltenSurge(Player target) {
            // 随波次左右压场
            float sway = attackTimer < 130 ? -1f : 1f;
            Vector2 hoverPos = target.Center + new Vector2(sway * 200f + MathF.Sin(globalTime) * 60f, -390);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
            }

            // 两波交错裂隙, 每波前有掌拍下沉动作
            if ((attackTimer == 18 || attackTimer == 128) && !VaultUtils.isServer)
                AokinHelper.CreateConvergingEmbers(NPC.Center, 0.8f, 200f);
            if (attackTimer == 20 || attackTimer == 130) {
                NPC.velocity.Y += 8f; // 掌拍冲量
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnMoltenTideWave(target, attackTimer > 100 ? 1 : 0);
            }

            // 间或点火球施压
            if (attackTimer % 40 == 0 && attackTimer > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    NPC.Center + toPlayer * 50f, toPlayer * 9f,
                    ModContent.ProjectileType<AokinFireball>(), contactDamageBase / 3, 1f);
            }

            if (attackTimer > 220) {
                AddHeat(24f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 龙焰洪流（P3 终极压迫）：冲至场边龙口对准玩家所在高度 → 反向拖拽蓄势 →
        /// 喷出横贯火河（前锋 14px/f 推进, 河高 ±95, 上下走位即安全）; 同时脚下两座间歇泉封纵向偷懒位。
        /// 火河推进本身即预警（traveling wave）; Boss 放完短收即回巡游。
        /// </summary>
        private bool RunFlameFlood(Player target) {
            const int AnticipationTime = 44;
            const int HoldTime = 140;
            const int RecoverTime = 24;

            switch (subState) {
                case 0: { // 占位 + 反向拖拽蓄势
                    int side = NPC.Center.X < target.Center.X ? -1 : 1;
                    Vector2 anchor = new Vector2(target.Center.X + side * ArenaHalfWidth * 0.8f, target.Center.Y - 20f);
                    float t = attackTimer / (float)AnticipationTime;

                    Vector2 aimDir = new Vector2(-side, 0f);
                    Vector2 reel = -aimDir * MathF.Pow(t, 4f) * 110f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor + reel - NPC.Center) * 0.1f, 0.15f);

                    rotationLocked = true;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, aimDir.ToRotation(), 0.15f);
                    NPC.spriteDirection = -side;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.75f, Volume = 1.2f }, NPC.Center);
                    if (attackTimer == AnticipationTime - 14)
                        SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);

                    breathGlow = Math.Max(breathGlow, t);
                    if (!VaultUtils.isServer && t < 0.85f) {
                        Vector2 mouth = NPC.Center + aimDir * 55f;
                        AokinHelper.CreateConvergingEmbers(mouth, t, 260f, 1.5f);
                    }
                    ACMUtils.AddScreenShake(t * t * 3f);

                    if (attackTimer >= AnticipationTime) {
                        // 火河点火（单帧）+ 后坐
                        Vector2 mouth = NPC.Center + aimDir * 60f;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                mouth, Vector2.Zero,
                                ModContent.ProjectileType<AokinFlameFlood>(),
                                Main.expertMode ? 55 : 70, 3f, Main.myPlayer,
                                ai0: -side, ai1: NPC.whoAmI);

                            // 两座间歇泉封纵向偷懒位
                            float geyserY = target.Bottom.Y + 20f;
                            for (int g = 0; g < 2; g++) {
                                Vector2 pos = new Vector2(target.Center.X + (g == 0 ? -140f : 140f), geyserY);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    pos, Vector2.Zero,
                                    ModContent.ProjectileType<AokinScaldGeyser>(),
                                    Main.expertMode ? 42 : 55, 2f, Main.myPlayer,
                                    ai0: 50 + g * 12, ai1: 1.1f);
                            }
                        }
                        NPC.velocity = aimDir * -13f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f, Volume = 1.5f }, NPC.Center);
                        ACMUtils.AddScreenShake(9f);
                        lavaBloom = Math.Max(lavaBloom, 0.6f);
                        subState = 1;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { // 持续喷涌: 定身 + 低鸣震颤
                    NPC.velocity *= 0.9f;
                    rotationLocked = true;
                    breathGlow = Math.Max(breathGlow, 0.7f);
                    if (attackTimer % 9 == 0)
                        ACMUtils.AddScreenShake(1.8f);

                    if (attackTimer >= HoldTime) {
                        subState = 2;
                        attackTimer = 0;
                    }
                    break;
                }
                case 2: { // 收招
                    NPC.velocity *= 0.94f;
                    if (attackTimer >= RecoverTime) {
                        AddHeat(26f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// 生成一排熔潮裂隙柱：横跨竞技场，留出 1~2 道安全缝（平台）。
        /// waveParity 控制缺口偏移，使连续波次的安全缝错开。
        /// </summary>
        private void SpawnMoltenTideWave(Player target, int waveParity) {
            int columns = IsPhase3 ? 9 : 7;
            float span = ArenaHalfWidth * 0.9f;
            float baseY = target.Center.Y + 260f;

            // 安全缝索引（1~2 道），随波偏移
            int gapA = 1 + (waveParity + seed) % (columns - 2);
            int gapB = (gapA + columns / 2) % columns;

            for (int i = 0; i < columns; i++) {
                if (i == gapA || i == gapB)
                    continue; // 安全平台缝

                float t = columns <= 1 ? 0.5f : i / (float)(columns - 1);
                float x = target.Center.X + MathHelper.Lerp(-span, span, t);
                Vector2 markPos = new Vector2(x, baseY);

                int telegraph = Main.expertMode ? 45 : 58;
                // 沿波次微错相位, 形成"涌动"观感
                int stagger = (i % 2 == 0 ? 0 : 8) + waveParity * 4;
                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    markPos, Vector2.Zero,
                    ModContent.ProjectileType<AokinLavaFissure>(),
                    Main.expertMode ? 50 : 65, 3f,
                    Main.myPlayer, ai0: telegraph + stagger, ai1: 1f);
            }

            if (!VaultUtils.isServer)
                ACMUtils.AddScreenShake(6f);
        }

        #endregion
    }
}

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
        ///   - 点燃封路龙卷向内收缩（ArenaHalfWidth 已随 PhaseRegion 收缩, 龙卷读取）。
        ///   - 铺设第一波熔潮裂隙：地面熔岩柱阵留出安全平台缝隙，玩家从此只能在安全缝间走位。
        /// </summary>
        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -360);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 熔潮升温
            emberHeat = Math.Min(MaxEmberHeat, emberHeat + 0.6f);
            heatWarp = Math.Max(heatWarp, MathHelper.Clamp(attackTimer / 80f, 0f, 1f) * 0.6f);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 14; i++) {
                    float angle = MathHelper.TwoPi * i / 14 + attackTimer * 0.05f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (90 + attackTimer * 1.3f);
                    int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Torch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 9f;
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
        /// 熔潮涌动：焚海劫期间的招牌攻击——盘空后再起一波熔岩裂隙柱阵（交错缺口），
        /// 同时点状落火球施压。缺口位置每波偏移，逼玩家持续在安全平台间迁移。
        /// </summary>
        private bool RunMoltenSurge(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime) * 120f, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.0025f;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
            }

            // 两波交错裂隙
            if (attackTimer == 20 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnMoltenTideWave(target, 0);
            if (attackTimer == 130 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnMoltenTideWave(target, 1);

            // 间或点火球施压
            if (attackTimer % 40 == 0 && attackTimer > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    NPC.Center + toPlayer * 50f, toPlayer * 9f,
                    ModContent.ProjectileType<AokinFireball>(), NPC.damage / 3, 1f);
            }

            if (attackTimer > 230) {
                AddHeat(24f);
                return true;
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

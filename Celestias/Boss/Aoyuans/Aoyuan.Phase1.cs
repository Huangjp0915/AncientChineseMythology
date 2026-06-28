using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰 一阶段攻击实现（全部带预告 telegraph）
    /// 由 Aoyuan.AI.cs 的 RunAttacking 调度，返回 true 表示攻击结束
    /// </summary>
    internal partial class Aoyuan
    {
        // ===== 1. 冰晶棋局 GlacialPillarChess =====
        // 在玩家上方预告 3x3 幽灵冰柱（地面落点标线），仅其中部分真正落下
        // 玩家需阅读"棋盘"，站到不会落柱的格子
        private bool AttackGlacialPillarChess(Player player) {
            // 缓慢悬停盘旋，保持压迫感
            float speed = IsPhase2 ? PatrolSpeedPhase2 * 0.6f : PatrolSpeed * 0.6f;
            WormMovement(player, speed, 0.1f);

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
            }

            // 布局一批棋盘（一阶段1批，二阶段2批）
            int batches = IsPhase2 ? 2 : 1;
            for (int b = 0; b < batches; b++) {
                if (attackTimer == 12 + b * 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                    AoyuanAttacks.SpawnPillarChess(NPC, player);
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f }, player.Center);
                }
            }

            int duration = IsPhase2 ? 170 : 110;
            return attackTimer >= duration;
        }

        // ===== 2. 暴雪帷幕 BlizzardVeil =====
        // 从一侧推进的雪墙，墙上留一道随机移动的缺口，玩家必须钻缝
        private bool AttackBlizzardVeil(Player player) {
            // 退到玩家另一侧准备推墙
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, speed, 0.13f);

            if (attackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                AoyuanAttacks.SpawnBlizzardVeil(NPC, player);
                veilCount++;
                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
            }

            // 二阶段推第二道（反方向）
            if (IsPhase2 && attackTimer == 90 && Main.netMode != NetmodeID.MultiplayerClient) {
                AoyuanAttacks.SpawnBlizzardVeil(NPC, player);
                veilCount++;
            }

            int duration = IsPhase2 ? 160 : 110;
            return attackTimer >= duration;
        }

        // ===== 3. 寒霜吐息 FrostBreath（专用张嘴动画）=====
        // 张嘴蓄力 → 朝玩家方向锥形吐出冰锥；告别原版 rand.NextBool(20) 随机龙息
        private bool AttackFrostBreath(Player player) {
            if (attackTimer == 1) {
                OpenMouth();
                SoundEngine.PlaySound(SoundID.NPCDeath60 with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
            }

            // 蓄力期：减速对准玩家
            if (attackTimer < 24) {
                NPC.velocity *= 0.9f;
                if (!VaultUtils.isServer && attackTimer % 3 == 0) {
                    Vector2 breathDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 3; i++) {
                        Vector2 dv = breathDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2, 5);
                        int d = Dust.NewDust(NPC.Center + breathDir * 40f, 0, 0, DustID.IceTorch, dv.X, dv.Y, 180, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }
            // 吐息期：持续锥形冰锥
            else if (attackTimer < 90) {
                NPC.velocity *= 0.96f;
                if (!VaultUtils.isServer) {
                    Vector2 breathDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 5; i++) {
                        Vector2 dv = breathDir.RotatedByRandom(0.45f) * Main.rand.NextFloat(4, 9);
                        int d = Dust.NewDust(NPC.Center + breathDir * 40f, 0, 0, DustID.IceTorch, dv.X, dv.Y, 180, default, 2.5f);
                        Main.dust[d].noGravity = true;
                    }
                }
                if (attackTimer % 7 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = Main.expertMode ? 4 : 3;
                    if (IsPhase2) count += 2;
                    AoyuanAttacks.BreathConeAt(NPC, player.Center, count);
                }
            }

            return attackTimer >= 100;
        }

        // ===== 4. 冰柱雨 IcicleRainCombo =====
        // 多波次天降冰柱，落点带短暂预告（冰柱本身高空生成给反应时间）
        private bool AttackIcicleRainCombo(Player player) {
            float speed = IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed;
            WormMovement(player, speed, 0.13f);

            int waveInterval = 22;
            int totalWaves = IsPhase2 ? 6 : 4;

            if (attackTimer % waveInterval == 0 && waveCount < totalWaves && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 9 : 7;
                if (IsPhase2) count += 3;
                for (int i = 0; i < count; i++)
                    AoyuanAttacks.IcicleRain(NPC);
                waveCount++;
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 0.7f }, NPC.Center);
            }

            return waveCount >= totalWaves && attackTimer >= totalWaves * waveInterval + 20;
        }

        // ===== 5. 冰霜环 FrostRingCombo =====
        // 环形冰弹波 + 穿插天降冰柱，玩家需在环缝中走位
        private bool AttackFrostRingCombo(Player player) {
            float speed = (IsPhase2 ? PatrolSpeedPhase2 : PatrolSpeed) * 0.7f;
            WormMovement(player, speed, 0.1f);

            if (attackTimer == 20) {
                int ringCount = IsPhase2 ? 24 : 16;
                AoyuanAttacks.FrostRing(NPC, ringCount, 5f);
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            }
            if (attackTimer == 55) {
                int ringCount = IsPhase2 ? 24 : 16;
                AoyuanAttacks.FrostRing(NPC, ringCount, 7f);
            }
            if (attackTimer >= 30 && attackTimer <= 80 && attackTimer % 12 == 0) {
                AoyuanAttacks.IcicleStorm(NPC, IsPhase2 ? 4 : 2);
            }

            return attackTimer >= (IsPhase2 ? 110 : 95);
        }
    }
}

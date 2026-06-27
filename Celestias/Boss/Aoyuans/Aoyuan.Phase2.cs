using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰 二阶段：50% 浮空破境阶段转换 + 绝对零度大招
    /// 阶段转换"改规则"——解锁打滑地痕与空中俯冲，而非单纯加弹/提速
    /// </summary>
    internal partial class Aoyuan
    {
        #region 阶段转换 — 50% 浮空破境

        private void TransitionToPhase2() {
            CurrentState = AoyuanState.PhaseTransition;
            attackTimer = 0;
            CloseMouth();
            NPC.netUpdate = true;
        }

        private void RunPhaseTransition(Player player) {
            attackTimer++;

            // 转换期间无敌（i-frame 过场节拍）
            NPC.dontTakeDamage = true;

            // 破境上浮 + 急剧减速
            NPC.velocity *= 0.93f;
            NPC.velocity.Y -= 0.25f;

            if (attackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 2f }, NPC.Center);
            }

            if (!VaultUtils.isServer) {
                if (attackTimer % 4 == 0)
                    AoyuanHelper.CreateFrostVortex(NPC.Center, 110f + attackTimer, 0.8f, 30);
                if (attackTimer % 9 == 0)
                    AoyuanHelper.CreateIceBurst(NPC.Center, 160f + attackTimer * 0.5f, 3, 16);
            }

            // 破境冲击：全场冰爆 + 给附近玩家施加打滑（地面结冰）
            if (attackTimer == 50) {
                if (!VaultUtils.isServer) {
                    AoyuanHelper.CreateIceBurst(NPC.Center, 420f, 6, 30);
                    // V2: 相变释放级震屏
                    ACMUtils.AddScreenShake(10f);
                }
                ApplySlipperyField();
            }

            glowIntensity = 1f + (float)Math.Sin(attackTimer * 0.3f) * 0.5f;

            if (attackTimer >= 90) {
                didPhase2Transition = true;
                NPC.dontTakeDamage = false;
                glowIntensity = 1.4f;
                CurrentState = AoyuanState.Patrol;
                patrolTimer = 0;
                patrolDuration = Main.rand.Next(MinPatrolDuration / 2, MaxPatrolDuration / 2);
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        /// <summary>破境后令场地结冰打滑（玩家附着 AoyuanSlippery）</summary>
        private void ApplySlipperyField() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                if (Vector2.Distance(NPC.Center, p.Center) > 2400f) continue;
                p.GetModPlayer<AoyuanFrostPlayer>().slipperyTimer = 1800; // ~30s
            }
        }

        #endregion

        #region 攻击 6. 绝对零度 AbsoluteZero（二阶段大招）

        // 锚定 + 3秒"吸气"蓄力 → 全屏放射冻结
        // 蓄力期间身体段暴露冰晶弱点：击破足够弱点可打断 → 削弱为普通冻结
        // 否则全屏强制冻结
        private bool AttackAbsoluteZero(Player player) {
            const int ChargeTime = 180; // 3秒吸气
            const int ReleaseTime = 60;

            // === 蓄力期 ===
            if (attackTimer <= ChargeTime) {
                NPC.velocity *= 0.85f;

                if (attackTimer == 1) {
                    OpenMouth();
                    WeakPointsExposed = true;
                    WeakPointDamageTaken = 0;
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 1.6f }, NPC.Center);
                    NPC.netUpdate = true;
                }

                // 吸气粒子：向 Boss 中心汇聚
                if (!VaultUtils.isServer && attackTimer % 2 == 0) {
                    for (int i = 0; i < 4; i++) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 200f + Main.rand.NextFloat(250f);
                        Vector2 pos = NPC.Center + ang.ToRotationVector2() * dist;
                        var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * (6f + dist * 0.02f);
                    }
                }

                // 蓄力进度音
                if (attackTimer == ChargeTime - 30)
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);

                // 弱点被击破 → 提前打断
                int breakThreshold = (int)(NPC.lifeMax * 0.025f);
                if (WeakPointDamageTaken >= breakThreshold) {
                    ReleaseAbsoluteZero(player, broken: true);
                    return true;
                }

                Lighting.AddLight(NPC.Center, AoyuanHelper.FrostCyan.ToVector3() * (attackTimer / (float)ChargeTime) * 3f);
                return false;
            }

            // === 释放期 ===
            if (attackTimer == ChargeTime + 1) {
                ReleaseAbsoluteZero(player, broken: false);
            }

            NPC.velocity *= 0.9f;
            return attackTimer >= ChargeTime + ReleaseTime;
        }

        private void ReleaseAbsoluteZero(Player player, bool broken) {
            WeakPointsExposed = false;
            CloseMouth();
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = broken ? 0.2f : -0.3f, Volume = 1.8f }, NPC.Center);
            if (!VaultUtils.isServer) {
                AoyuanHelper.CreateIceBurst(NPC.Center, broken ? 250f : 500f, broken ? 3 : 6, 30);
                // V2: 冻爆泛光 + 处决级一次性震屏（完整冻结更重）
                freezeBloom = 1f;
                ACMUtils.AddScreenShake(broken ? 8f : 12f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // 放射冻结波（弹幕自行对路过玩家施加冻结/冰冻；broken=1 仅减速）
                AoyuanAttacks.SpawnAbsoluteZeroBurst(NPC, broken);
            }
        }

        #endregion
    }
}

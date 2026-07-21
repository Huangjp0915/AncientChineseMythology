using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰 二阶段内容: 时滞破境（50%）、绝对零度（弱点可打断）、镜界瞬狱连突、晶化升天死亡演出
    /// </summary>
    internal partial class Aoyuan
    {
        #region 阶段转换 — 时滞破境（~170f, 清弹 + i-frame）

        private void RunPhaseTransition(Player player) {
            NPC.dontTakeDamage = true;

            float t = StateTimer;

            if (t == 1) {
                CloseMouth();
                telegraphAlpha = 0f;
                if (!VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.8f }, NPC.Center);
            }

            // 移至玩家上空盘蜷
            if (t < 20) {
                GlideTo(player.Center + new Vector2(0f, -400f), 24f, 1.1f);
            }
            // 时间冻结: 全屏去饱和(UpdateScreenFx 驱动), 空中粒子凝滞, 万籁俱寂
            else if (t < 90) {
                CoilAround(player.Center + new Vector2(0f, -400f), 95f, 0.13f, 0.6f);

                if (!VaultUtils.isServer) {
                    // 凝滞悬浮的冰尘 — velocity 为零, 时间仿佛停住
                    if (Main.rand.NextBool(3)) {
                        var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(420, 320),
                            Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                        d.noGravity = true;
                        d.velocity = Vector2.Zero;
                        d.scale = 1.1f + Main.rand.NextFloat(0.7f);
                        d.fadeIn = 1.2f;
                        d.alpha = 120;
                    }
                    // 稀疏冰铃
                    if ((int)t % 22 == 0)
                        AoyuanHelper.PlayChime(NPC.Center, 0.15f + t / 400f, 0.5f);
                }
            }
            // 剑鸣 + 白亮脉冲
            else if (t == 90) {
                if (!VaultUtils.isServer) {
                    AoyuanHelper.PlayChime(NPC.Center, 0.8f, 1.3f);
                    freezeBloom = 0.55f;
                }
            }
            // 碎境: 冲击波 + 场地结冰(规则改变) + 一道诚实的减速寒潮环
            else if (t == 92) {
                if (!VaultUtils.isServer) {
                    AoyuanHelper.PlayShatter(NPC.Center, -0.3f, 1.4f);
                    ACMUtils.AddScreenShake(10f);
                    AoyuanHelper.CreateIceBurst(NPC.Center, 220f, 3, 20);
                    AoyuanHelper.CreateIceBurst(NPC.Center, 430f, 4, 24);
                    AoyuanHelper.CreateMirrorShards(NPC.Center, 1.8f, 40);
                }
                ApplySlipperyField();
                // 破境寒潮环: 仅叠冰冻不伤血(broken 版), 冰蓝非红
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoyuanAttacks.SpawnAbsoluteZeroBurst(NPC, broken: true);
            }
            else if (t > 92) {
                NPC.velocity *= 0.93f;
                glowIntensity = 1f + MathF.Sin((float)t * 0.3f) * 0.4f;
            }

            if (t >= 170) {
                internalAI[2] = 1f;
                NPC.dontTakeDamage = false;
                glowIntensity = 1.4f;
                attackBag.Clear(); // 立即以 P2 招式池重开洗牌袋
                EnterState(AoyuanState.Patrol);
                patrolDuration = PatrolMaxP2;
            }
        }

        /// <summary>破境后令场地结冰打滑（玩家附着 AoyuanSlippery, 保留的规则改变）</summary>
        private void ApplySlipperyField() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                if (Vector2.Distance(NPC.Center, p.Center) > 2400f) continue;
                p.GetModPlayer<AoyuanFrostPlayer>().slipperyTimer = 1800; // ~30s
            }
        }

        #endregion

        #region 攻击 6. 绝对零度 AbsoluteZero（P2 大招, 弱点可打断）

        private const int AZCoilTime = 30;
        private const int AZChargeEnd = AZCoilTime + 190;   // 220
        private const int AZReleaseTick = AZChargeEnd + 12; // 232 (12f 预塌缩静默)
        private const int AZEndTime = AZReleaseTick + 40;

        /// <summary>绝对零度蓄力进度 0~1（绘制层读取: 预警环/塌缩）</summary>
        public float AZChargeProgress =>
            CurrentState == AoyuanState.Attacking
            && (AoyuanAttackType)(int)NPC.ai[2] == AoyuanAttackType.AbsoluteZero
                ? MathHelper.Clamp((StateTimer - AZCoilTime) / (float)(AZChargeEnd - AZCoilTime), 0f, 1f)
                : 0f;

        /// <summary>绝对零度是否处于预塌缩静默（环收缩）</summary>
        public bool AZCollapsing =>
            CurrentState == AoyuanState.Attacking
            && (AoyuanAttackType)(int)NPC.ai[2] == AoyuanAttackType.AbsoluteZero
            && StateTimer > AZChargeEnd && StateTimer < AZReleaseTick && internalAI[1] < 0.5f;

        private bool AttackAbsoluteZero(Player player) {
            float t = StateTimer;
            bool broken = ParamA > 0.5f; // ParamA=1: 已被弱点打断

            // —— 盘成紧螺旋 ——
            if (t < AZCoilTime) {
                Vector2 anchor = player.Center + new Vector2(orbitDir * 180f, -480f);
                if (t == 1)
                    coilAngle = (NPC.Center - anchor).ToRotation();
                CoilAround(anchor, MathHelper.Lerp(150f, 70f, t / AZCoilTime), 0.14f, 0.6f);
                return false;
            }

            // —— 吸气蓄力 190f ——
            if (t <= AZChargeEnd && !broken) {
                NPC.velocity *= 0.85f;
                // 距离栓绳: 玩家逃远则缓缓贴近, 大招不放空
                if (Vector2.Distance(NPC.Center, player.Center) > 950f)
                    GlideTo(player.Center + new Vector2(0f, -450f), 6f, 0.12f);

                float progress = (t - AZCoilTime) / (float)(AZChargeEnd - AZCoilTime);

                if (t == AZCoilTime + 1) {
                    OpenMouth();
                    WeakPointsExposed = true;
                    WeakPointDamageTaken = 0;
                    if (!VaultUtils.isServer)
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 1.6f }, NPC.Center);
                    NPC.netUpdate = true;
                }

                // 吸气流光: 密度 ∝ √charge, 72% 处硬切 → 尖叫前的静默
                if (!VaultUtils.isServer && progress < 0.72f && (int)t % 2 == 0) {
                    int n = 1 + (int)(MathF.Sqrt(progress) * 5f);
                    for (int i = 0; i < n; i++)
                        AoyuanHelper.CreateConvergingStreak(NPC.Center, 200f, 460f, 0.085f);
                }

                // 加速冰铃: 蓄力节拍渐密渐高
                if (!VaultUtils.isServer) {
                    if (t == AZCoilTime + 48 || t == AZCoilTime + 96 || t == AZCoilTime + 133
                        || t == AZCoilTime + 160 || t == AZCoilTime + 178)
                        AoyuanHelper.PlayChime(NPC.Center, -0.3f + progress * 0.9f, 0.9f);
                }

                // 弱点被击破 → 踉跄打断（奖励输出窗）
                int breakThreshold = (int)(NPC.lifeMax * 0.025f);
                if (WeakPointDamageTaken >= breakThreshold && Main.netMode != NetmodeID.MultiplayerClient) {
                    ParamA = 1f;
                    // 主循环先自增再进入本函数, 故设为 释放帧-1, 下帧恰好命中 t == AZReleaseTick
                    StateTimer = AZReleaseTick - 1;
                    NPC.netUpdate = true;
                }

                Lighting.AddLight(NPC.Center, AoyuanHelper.FrostCyan.ToVector3() * progress * 3f);
                return false;
            }

            // —— 预塌缩 12f: 所有粒子熄灭, 预警环收缩 ——
            if (t < AZReleaseTick) {
                NPC.velocity *= 0.8f;
                return false;
            }

            // —— 释放 ——
            if (t == AZReleaseTick) {
                ReleaseAbsoluteZero(broken);
            }

            NPC.velocity *= 0.92f; // 脱力慢滑 — 明确输出窗
            return t >= AZEndTime + (broken ? 30 : 0);
        }

        private void ReleaseAbsoluteZero(bool broken) {
            WeakPointsExposed = false;
            CloseMouth();
            if (broken)
                staggerTimer = 60; // 踉跄易伤窗
            NPC.netUpdate = true;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = broken ? 0.2f : -0.3f, Volume = 1.8f }, NPC.Center);
                if (broken)
                    AoyuanHelper.PlayShatter(NPC.Center, 0.3f, 1.2f);
                AoyuanHelper.CreateIceBurst(NPC.Center, broken ? 250f : 520f, broken ? 3 : 6, 30);
                freezeBloom = 1f;
                ACMUtils.AddScreenShake(broken ? 8f : 12f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // 完整版: 致命快环(红) + 10f 后再一道慢寒潮环(冰蓝, 叠层)
                AoyuanAttacks.SpawnAbsoluteZeroBurst(NPC, broken);
                if (!broken)
                    AoyuanAttacks.SpawnAbsoluteZeroEcho(NPC);
            }
        }

        #endregion

        #region 攻击 7. 镜界·瞬狱连突 MirrorRealm（P2）

        private const int RealmMirrorTick = 6;    // 布镜
        private const int RealmEnterTick = 44;    // 首次入镜
        private const int RealmHopHidden = 40;    // 每跳隐没时长
        private const int RealmHopThrust = 14;    // 出镜贯穿
        private const int RealmHopBrake = 12;     // 贯穿后刹车
        private const int RealmHopLen = RealmHopHidden + RealmHopThrust + RealmHopBrake; // 66
        private const int RealmHopCount = 3;
        private const int RealmFinaleLen = 130;   // 剩镜齐射收尾

        private bool AttackMirrorRealm(Player player) {
            float t = StateTimer;

            // —— 布镜 ——
            if (t == RealmMirrorTick) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoyuanAttacks.SpawnMirrorHex(NPC, player);
                if (!VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.2f }, player.Center);
            }

            // —— 靠近最近镜面 ——
            if (t < RealmEnterTick) {
                if (t > RealmMirrorTick) {
                    Projectile near = AoyuanAttacks.FindNearestRealmMirror(NPC.Center, -1);
                    if (near != null)
                        GlideTo(near.Center, 21f, 0.7f);
                }
                return false;
            }

            // —— 三段瞬狱跳跃 ——
            int hop = (int)ParamB;
            if (hop < RealmHopCount) {
                float ht = t - RealmEnterTick - hop * RealmHopLen;

                // 入镜: 隐没 + 碎入口镜
                if (ht == 0) {
                    BodyHidden = true;
                    NPC.dontTakeDamage = true;
                    NPC.velocity = Vector2.Zero;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile entry = AoyuanAttacks.FindNearestRealmMirror(NPC.Center, -1);
                        if (entry != null) {
                            NPC.Center = entry.Center;
                            entry.ai[2] = 3f; // 入口碎裂指令
                            entry.netUpdate = true;
                        }
                        // 出口镜 = 距入口最远的存活镜（确定性选择）; 白亮预告 + 锁定出射角
                        Projectile exit = AoyuanAttacks.FindFarthestRealmMirror(NPC.Center);
                        if (exit != null) {
                            ParamA = (player.Center + player.velocity * 12f - exit.Center).ToRotation();
                            exit.ai[1] = ParamA;
                            exit.ai[2] = 1f; // 出口白亮指令（镜面自行渐亮 + 预警线）
                            exit.netUpdate = true;
                        }
                        NPC.netUpdate = true;
                    }
                    if (!VaultUtils.isServer)
                        AoyuanHelper.PlayShatter(NPC.Center, 0.1f, 1f);
                }
                // 隐没期: 龙形光斑在镜间流转（镜面绘制层表现）; 固定 36f 预警剑鸣
                else if (ht < RealmHopHidden) {
                    NPC.velocity = Vector2.Zero;
                    if (ht == RealmHopHidden - 36 && !VaultUtils.isServer)
                        AoyuanHelper.PlayChime(player.Center, -0.1f, 1f);
                }
                // 出镜爆刺
                else if (ht == RealmHopHidden) {
                    BodyHidden = false;
                    NPC.dontTakeDamage = false;
                    NPC.alpha = 0;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile exit = AoyuanAttacks.FindWhitenedRealmMirror();
                        if (exit != null) {
                            NPC.Center = exit.Center;
                            exit.ai[2] = 0f; // 白亮解除
                            exit.netUpdate = true;
                        }
                        NPC.netUpdate = true;
                    }
                    LaunchThrust(82f);
                }
                else if (ht <= RealmHopHidden + RealmHopThrust) {
                    EmitThrustWake(34f, 20, 90);
                }
                else {
                    NPC.velocity *= 0.78f;
                }

                // 于 RealmHopLen-1 推进: 下帧 ht 恰好归 0, 保证下一跳的入镜帧(ht==0)不被跳过
                if (ht >= RealmHopLen - 1) {
                    ParamB++;
                    NPC.netUpdate = true;
                }
                return false;
            }

            // —— 终幕: 剩余镜面齐充能 → 折光束齐射（45f 充能全程可见）——
            float ft = t - RealmEnterTick - RealmHopCount * RealmHopLen;
            if (ft == 1 && Main.netMode != NetmodeID.MultiplayerClient)
                AoyuanAttacks.CommandRealmVolley(player);
            // 本体退场上浮旁观
            GlideTo(player.Center + new Vector2(0f, -520f), 11f, 0.28f);

            return ft >= RealmFinaleLen;
        }

        #endregion

        #region 死亡演出 — 晶化升天（~300f）

        private Vector2 deathAnchor;
        private int lastCrystallized;

        private void BeginDeathAnim() {
            CurrentState = AoyuanState.DeathAnim;
            StateTimer = 0;
            ParamB = 0;
            CloseMouth();
            WeakPointsExposed = false;
            telegraphAlpha = 0f;
            BodyHidden = false;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.5f;
            deathAnchor = NPC.Center;
            lastCrystallized = 0;
            CrystallizedSegments = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                AoyuanAttacks.ClearHostileProjectiles();
            NPC.netUpdate = true;
        }

        /// <summary>由 t 推算已晶化段数（尾→头, 节拍加速; 各端确定性同算）</summary>
        private static int CrystallizedCountAt(float t) {
            float clock = 40f;
            float interval = 14f;
            int count = 0;
            while (clock <= t && count < 17) {
                count++;
                clock += interval;
                interval = Math.Max(4f, interval - 0.62f);
            }
            return count;
        }

        private void RunDeathAnim(Player player) {
            NPC.dontTakeDamage = true;
            float t = StateTimer;

            // —— 踉跄 40f ——
            if (t < 40) {
                NPC.velocity *= 0.93f;
                if (!VaultUtils.isServer && (int)t % 9 == 0) {
                    NPC.velocity += Main.rand.NextVector2Circular(2.2f, 2.2f);
                    SoundEngine.PlaySound(SoundID.NPCHit56 with { Pitch = -0.4f, Volume = 0.7f }, NPC.Center);
                }
                if (t == 2 && !VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.85f, Volume = 1.6f }, NPC.Center);
            }
            // —— 螺旋升天 + 尾→头逐段晶化（冰铃音阶加速上行）——
            else if (t < 210) {
                deathAnchor.Y -= 1.8f;
                CoilAround(deathAnchor, 150f, 0.075f, 0.5f);

                CrystallizedSegments = CrystallizedCountAt(t);
                if (CrystallizedSegments > lastCrystallized) {
                    lastCrystallized = CrystallizedSegments;
                    if (!VaultUtils.isServer) {
                        AoyuanHelper.PlayChime(NPC.Center, -0.45f + CrystallizedSegments * 0.075f, 0.85f);
                        ACMUtils.AddScreenShake(1.5f);
                    }
                }
            }
            // —— 顶点全静止 40f: 万籁俱寂 ——
            else if (t < 250) {
                NPC.velocity *= 0.75f;
                CrystallizedSegments = 17;
            }
            // —— 碎裂: 冲击帧 + 全场唯一 shake 16 ——
            else if (t == 250) {
                flashFx = 1f;
                if (!VaultUtils.isServer) {
                    AoyuanHelper.PlayShatter(NPC.Center, -0.4f, 1.6f);
                    SoundEngine.PlaySound(SoundID.NPCDeath62 with { Pitch = -0.3f, Volume = 1.4f }, NPC.Center);
                    ACMUtils.AddScreenShake(16f);
                    AoyuanHelper.CreateMirrorShards(NPC.Center, 2.2f, 60);
                }
                freezeBloom = 1f;
            }
            // —— 身体段尾→头连锁冰爆消散 ——
            else if (t > 250 && t < 285) {
                NPC.velocity = Vector2.Zero;
                if (Main.netMode != NetmodeID.MultiplayerClient && (int)t % 2 == 0)
                    AoyuanAttacks.ShatterOneBodySegment(NPC);
            }
            // —— 真正死亡 → OnKill 掉落 ——
            else if (t >= 285) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    reallyDead = true;
                    NPC.dontTakeDamage = false;
                    NPC.StrikeInstantKill();
                }
            }
        }

        #endregion
    }
}

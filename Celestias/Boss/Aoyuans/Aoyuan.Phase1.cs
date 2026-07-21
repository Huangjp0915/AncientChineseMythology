using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰 入场演出 + 一阶段五招
    /// 所有突刺遵循同一波形: 盘蜷蓄势(anticipation) → 锁线静默(silence) → 刹那贯穿(burst) → 硬刹收剑(recovery)
    /// </summary>
    internal partial class Aoyuan
    {
        #region 入场演出 — 破镜现身（~292f）

        // 时间轴常量（AoyuanSky 剪影与 UpdateScreenFx 也引用）
        public const int IntroSilhouetteStart = 40;   // 天际剪影开始
        public const int IntroMirrorTime = 130;       // 冰镜凝形
        public const int IntroRevealTime = 170;       // 破镜现身
        public const int IntroStareEnd = 218;         // 静止凝视结束
        public const int IntroThrustTime = 258;       // 试剑突刺
        public const int IntroEndTime = 292;

        /// <summary>入场剪影进度（供 AoyuanSky 读取, <0 表示不绘制）</summary>
        public float IntroSilhouetteProgress =>
            CurrentState == AoyuanState.Intro && StateTimer >= IntroSilhouetteStart && StateTimer < IntroMirrorTime
                ? (StateTimer - IntroSilhouetteStart) / (float)(IntroMirrorTime - IntroSilhouetteStart)
                : -1f;

        private Vector2 introAnchor;

        private void RunIntro(Player player) {
            // 现身前隐没（各端由状态+计时器确定, 无需等待同步）
            if (StateTimer < IntroRevealTime) {
                BodyHidden = true;
                NPC.dontTakeDamage = true;
            }

            float t = StateTimer;

            // t2: 挪到玩家上空远处（隐身, 服务器权威）
            if (t == 2 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.Center = player.Center + new Vector2(0f, -820f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }

            // t20: 远方低吼 — 天幕开始结霜（frostEdge 由 UpdateScreenFx 驱动）
            if (t == 20 && !VaultUtils.isServer)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.75f, Volume = 1.4f }, player.Center);

            // t40~130: 天际剪影掠过（AoyuanSky 绘制）, 冰铃点缀
            if (t is > IntroSilhouetteStart and < IntroMirrorTime && !VaultUtils.isServer) {
                if ((int)t % 30 == 0)
                    AoyuanHelper.PlayChime(player.Center, 0.3f + (t - 40f) / 300f, 0.45f);
            }

            // t130: 挪到现身点 + 凝出冰镜
            if (t == IntroMirrorTime) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.Center = player.Center + new Vector2(player.direction >= 0 ? 380f : -380f, -300f);
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                    AoyuanAttacks.SpawnIntroMirror(NPC);
                }
                if (!VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
            }

            // t130~170: 镜后隐身盘蜷成环（现身即是完整的"剑鞘"姿态）
            if (t is >= IntroMirrorTime and < IntroRevealTime) {
                if (t == IntroMirrorTime + 1) {
                    introAnchor = NPC.Center;
                    coilAngle = 0f;
                }
                CoilAround(introAnchor, 115f, 0.16f, 0.7f);
            }

            // t170: 破镜现身 — Shatter + 冰片喷泉 + 60f 完全静止的威压
            if (t == IntroRevealTime) {
                BodyHidden = false;
                NPC.dontTakeDamage = false;
                NPC.alpha = 0;
                if (!VaultUtils.isServer) {
                    AoyuanHelper.PlayShatter(NPC.Center, -0.1f, 1.3f);
                    AoyuanHelper.CreateMirrorShards(NPC.Center, 1.6f, 40);
                    AoyuanHelper.CreateIceBurst(NPC.Center, 180f, 3, 18);
                    ACMUtils.AddScreenShake(4f);
                }
            }

            // t170~218: 静止凝视（威压=静止）
            if (t is > IntroRevealTime and <= IntroStareEnd) {
                NPC.velocity *= 0.86f;
                if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                    // 呼吸寒气自口部缓慢下沉
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30, 30), DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = new Vector2(0, 0.4f);
                    d.alpha = 160;
                }
            }

            // t218~258: 试剑瞄准 — 满额预警线 + 固定 36f 剑鸣
            if (t is > IntroStareEnd and < IntroThrustTime) {
                NPC.velocity *= 0.85f;
                TrackThrustAim(player, (t - IntroStareEnd) / (IntroThrustTime - IntroStareEnd - 8f), lead: 10f);
                telegraphAlpha = Math.Min(1f, telegraphAlpha + 0.06f);
                if (t == IntroThrustTime - 36 && !VaultUtils.isServer)
                    AoyuanHelper.PlayChime(NPC.Center, -0.2f, 1f);
                if (t == IntroThrustTime - 12) {
                    telegraphLock = 1f;
                    if (Main.netMode == NetmodeID.Server)
                        NPC.netUpdate = true;
                }
            }

            // t258: 试剑突刺（首次亮相, 速度稍缓, 无航迹）
            if (t == IntroThrustTime)
                LaunchThrust(80f);

            // 贯穿 12f 后硬刹
            if (t > IntroThrustTime + 12)
                NPC.velocity *= 0.82f;

            if (t >= IntroEndTime) {
                telegraphAlpha = 0f;
                EnterState(AoyuanState.Patrol);
                patrolDuration = PatrolMax;
            }
        }

        #endregion

        #region 突刺共用原语

        /// <summary>
        /// 突刺瞄准: 预测玩家位置, 跟踪率随蓄势进度衰减（看得见地"锁线"）。
        /// progress 0~1; 各端同算, 服务器在锁定帧 netUpdate 校正 ParamA。
        /// </summary>
        private void TrackThrustAim(Player player, float progress, float lead = 14f) {
            Vector2 predicted = player.Center + player.velocity * lead;
            float desired = (predicted - NPC.Center).ToRotation();
            float rate = MathHelper.Lerp(0.15f, 0.02f, MathHelper.Clamp(progress, 0f, 1f));
            if (ParamA == 0f)
                ParamA = desired;
            ParamA = AoyuanHelper.LerpAngle(ParamA, desired, rate);
        }

        /// <summary>
        /// 出剑: 一帧点火（set 不是 ramp）+ 爆闪 + 方向性震屏 + 出剑音
        /// </summary>
        private void LaunchThrust(float speed) {
            NPC.velocity = ParamA.ToRotationVector2() * speed;
            lastWakePos = NPC.Center;
            slashFlash = 1f;
            telegraphAlpha = 0f;
            if (!VaultUtils.isServer) {
                ACMUtils.AddScreenShake(5f);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.45f, Volume = 1.25f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
                AoyuanHelper.CreateIceBurst(NPC.Center, 90f, 2, 12);
            }
            if (Main.netMode == NetmodeID.Server)
                NPC.netUpdate = true;
        }

        #endregion

        #region 1. 刹那·冰封突刺 InstantThrust（签名招）

        // 单循环: 盘蜷44 + 锁线14 → 出剑 → 贯穿12 + 硬刹22 = 92f
        private const int ThrustCoilTime = 44;
        private const int ThrustLockTime = 14;
        private const int ThrustLaunchTick = ThrustCoilTime + ThrustLockTime; // 58
        private const int ThrustActiveTime = 12;
        private const int ThrustBrakeTime = 22;
        private const int ThrustCycleLen = ThrustLaunchTick + ThrustActiveTime + ThrustBrakeTime; // 92

        private bool AttackInstantThrust(Player player) {
            int maxCycles = IsPhase2 || IsDesperation ? 3 : 2;
            int cycle = (int)ParamB;
            float t = StateTimer - cycle * ThrustCycleLen;

            // 锚点: 玩家侧上方 560px, 逐循环换边
            int side = cycle % 2 == 0 ? orbitDir : -orbitDir;
            float anchorAng = -MathHelper.PiOver2 + side * 0.85f;
            Vector2 anchor = player.Center + anchorAng.ToRotationVector2() * 560f;

            // —— 盘蜷蓄势: 螺旋收紧, 龙身盘成剑鞘 ——
            if (t < ThrustCoilTime) {
                if (t == 1) {
                    ParamA = 0f;
                    coilAngle = (NPC.Center - anchor).ToRotation();
                    // 距离栓绳: 开局离玩家太远先瞬时收拢锚点
                    if (Vector2.Distance(NPC.Center, player.Center) > 1250f && Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.Center = Vector2.Lerp(NPC.Center, anchor, 0.55f);
                        NPC.netUpdate = true;
                    }
                }
                float p = t / (float)ThrustCoilTime;
                float radius = MathHelper.Lerp(200f, 95f, AoyuanHelper.QuadOut(p));
                float angSpeed = MathHelper.Lerp(0.055f, 0.125f, p);
                CoilAround(anchor, radius, angSpeed, 0.55f);
                TrackThrustAim(player, p);

                telegraphAlpha = Math.Min(1f, telegraphAlpha + 0.05f);

                // 汇聚寒气 ∝ 进度（锁定前 10f 全部硬切 → 出剑前的静默）
                if (!VaultUtils.isServer && (int)t % 2 == 0) {
                    int n = 1 + (int)(p * 3f);
                    for (int i = 0; i < n; i++)
                        AoyuanHelper.CreateConvergingStreak(NPC.Center, 90f, 320f, 0.10f);
                }

                // 固定 36f 预警剑鸣（launch=58 → t=22）
                if (t == ThrustLaunchTick - 36 && !VaultUtils.isServer)
                    AoyuanHelper.PlayChime(NPC.Center, -0.2f, 1f);
            }
            // —— 锁线静默: 死停, 线转白, 粒子零 ——
            else if (t < ThrustLaunchTick) {
                NPC.velocity *= 0.70f;
                if (t == ThrustCoilTime) {
                    telegraphLock = 1f;
                    if (Main.netMode == NetmodeID.Server)
                        NPC.netUpdate = true;
                }
            }
            // —— 出剑 ——
            else if (t == ThrustLaunchTick) {
                LaunchThrust(95f);
            }
            // —— 贯穿: 直线即速度, 沿途冰封航迹 ——
            else if (t <= ThrustLaunchTick + ThrustActiveTime) {
                EmitThrustWake(30f, 25, 150);
            }
            // —— 硬刹收剑 ——
            else {
                NPC.velocity *= 0.78f;
            }

            // 循环推进
            if (t >= ThrustCycleLen) {
                ParamB++;
                if ((int)ParamB >= maxCycles)
                    return true;
            }
            return false;
        }

        #endregion

        #region 2. 冰镜·折光阵 MirrorArray

        private bool AttackMirrorArray(Player player) {
            int mirrorCount = IsPhase2 ? 5 : 4;

            if (StateTimer == 8) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoyuanAttacks.SpawnMirrorArc(NPC, player, mirrorCount);
                if (!VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.15f, Volume = 1.1f }, player.Center);
            }

            // Boss 退至远侧缓游: 施压靠镜阵, 本体是 presence
            Vector2 away = (NPC.Center - player.Center).SafeNormalize(-Vector2.UnitY);
            GlideTo(player.Center + away * 730f, 9f, 0.22f);

            if (!VaultUtils.isServer && Main.rand.NextBool(10))
                AoyuanHelper.CreateFrostTrail(NPC.Center, NPC.velocity, 0.6f);

            // 时长 = 成形22 + 蓄光45 + 依序间隔 + 束(26警+30束) + 收尾
            int duration = 22 + 45 + (mirrorCount - 1) * 14 + 56 + 24;
            return StateTimer >= duration;
        }

        #endregion

        #region 3. 寒潮·冻土席卷 ColdWave

        private bool AttackColdWave(Player player) {
            const int ImpactTick = 46;

            // —— 俯冲: 中速可读, 提前到位则跳时间（不等自己的计时器）——
            if (StateTimer < ImpactTick) {
                Vector2 diveTarget = player.Center + new Vector2(0f, 250f);
                GlideTo(diveTarget, 30f, 1.3f);
                if (StateTimer == 1 && !VaultUtils.isServer)
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.6f, Volume = 1f }, NPC.Center);
                if (Vector2.Distance(NPC.Center, diveTarget) < 70f && StateTimer < ImpactTick - 1)
                    StateTimer = ImpactTick - 1;
            }
            // —— 触地顿挫 + 寒潮蔓延 ——
            else if (StateTimer == ImpactTick) {
                NPC.velocity = Vector2.Zero;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoyuanAttacks.SpawnColdField(NPC, IsPhase2);
                if (!VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(4f);
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.35f, Volume = 1.3f }, NPC.Center);
                    AoyuanHelper.PlayShatter(NPC.Center, -0.5f, 0.6f);
                    AoyuanHelper.CreateIceBurst(NPC.Center, 220f, 3, 20);
                }
            }
            else if (StateTimer < ImpactTick + 8) {
                NPC.velocity *= 0.5f; // 顿挫定格
            }
            // —— 拔升回空 ——
            else {
                int side = NPC.Center.X < player.Center.X ? -1 : 1;
                GlideTo(player.Center + new Vector2(side * 430f, -390f), 14f, 0.35f);
            }

            return StateTimer >= 150;
        }

        #endregion

        #region 4. 冰封·困龙局 FreezeTrap

        private bool AttackFreezeTrap(Player player) {
            if (StateTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                AoyuanAttacks.SpawnFreezeTraps(NPC, player, IsPhase2);
            }
            if (StateTimer == 10 && !VaultUtils.isServer)
                SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.3f, Volume = 1.1f }, player.Center);

            // 放牧: 绕外圈快游, 缓速压制弹逼动
            orbitAngle += 0.030f * orbitDir;
            Vector2 anchor = player.Center + orbitAngle.ToRotationVector2() * 640f;
            GlideTo(anchor, 17f, 0.45f);

            if (StateTimer >= 30 && (int)StateTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                AoyuanAttacks.SuppressShot(NPC, player);

            int duration = IsPhase2 ? 220 : 155;
            return StateTimer >= duration;
        }

        #endregion

        #region 5. 霜刃·回旋连斩 FrostBlades

        // 短循环: 盘蜷22 + 锁线8 → 贯穿10 + 刹车14 = 54f
        private const int BladeCoilTime = 22;
        private const int BladeLaunchTick = 30;
        private const int BladeCycleLen = 54;

        // 交替角度成 V/X 交叉线
        private static readonly float[] BladeAnchorAngles = [-2.35f, -0.85f, 2.55f, -1.55f];

        private bool AttackFrostBlades(Player player) {
            int maxCycles = IsPhase2 ? 4 : 3;
            int cycle = (int)ParamB;
            float t = StateTimer - cycle * BladeCycleLen;

            float anchorAng = BladeAnchorAngles[cycle % BladeAnchorAngles.Length];
            Vector2 anchor = player.Center + anchorAng.ToRotationVector2() * 410f;

            if (t < BladeCoilTime) {
                if (t == 1) {
                    ParamA = 0f;
                    coilAngle = (NPC.Center - anchor).ToRotation();
                    // 短前摇招式的固定预警: 循环起点高音短鸣（低威胁档的常数音色）
                    if (!VaultUtils.isServer)
                        AoyuanHelper.PlayChime(NPC.Center, 0.35f, 0.7f);
                    if (Vector2.Distance(NPC.Center, player.Center) > 1250f && Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.Center = Vector2.Lerp(NPC.Center, anchor, 0.6f);
                        NPC.netUpdate = true;
                    }
                }
                float p = t / (float)BladeCoilTime;
                CoilAround(anchor, MathHelper.Lerp(150f, 80f, p), MathHelper.Lerp(0.08f, 0.15f, p), 0.6f);
                TrackThrustAim(player, p, lead: 8f);
                telegraphAlpha = Math.Min(0.8f, telegraphAlpha + 0.07f);
            }
            else if (t < BladeLaunchTick) {
                NPC.velocity *= 0.68f;
                if (t == BladeCoilTime) {
                    telegraphLock = 1f;
                    if (Main.netMode == NetmodeID.Server)
                        NPC.netUpdate = true;
                }
            }
            else if (t == BladeLaunchTick) {
                LaunchThrust(58f);
            }
            else if (t <= BladeLaunchTick + 10) {
                EmitThrustWake(40f, 18, 60);
            }
            else {
                NPC.velocity *= 0.76f;
                // 终斩后的 4f 完全定格 — 段落句号
                if (cycle == maxCycles - 1 && t > BladeCycleLen - 6)
                    NPC.velocity = Vector2.Zero;
            }

            if (t >= BladeCycleLen) {
                ParamB++;
                if ((int)ParamB >= maxCycles)
                    return true;
            }
            return false;
        }

        #endregion
    }
}

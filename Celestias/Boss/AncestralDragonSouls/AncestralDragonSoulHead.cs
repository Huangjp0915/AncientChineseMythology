using AncientChineseMythology.Celestias.Boss.AncestralDragonSouls.Items;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂头部 — V3「星海归墟」:
    /// 虚实对比 = 核心读法 (透明可穿行 / 凝实是猎杀线), 全身星尘着色器 (AncestralSoulBody.fx) 由头部合批绘制。
    /// 三大演出: 入场「星海凝形」(星流汇聚+逐节编织+静止凝视) / 半血分裂+残血双魂回拢 (白屏顿帧) / 死亡「归墟」
    /// (尾→头星散波+全静默+终爆)。新招「相位穿行猎杀」; 掠袭冲锋改为 反向蓄力→锁定读线→一帧点火→硬刹 波形。
    /// 阶段结构沿用 V2: 半血真分裂双子 → 残血合体「太初真身」→ 确定性终曲循环 (碎片谜题/阴阳超载/编排终极)。
    /// </summary>
    [AutoloadBossHead]
    public class AncestralDragonSoulHead : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Head;

        public enum AIState : int
        {
            Intro,
            Patrol,
            ScaleBarrage,
            SigilEruption,
            YinYangBind,
            DragonBeam,
            SpiralDive,
            RakingCharge,
            SplitTransition,
            TwinLink,
            TwinCrossfire,
            TwinPressure,
            Recovery,
            EnrageTransition,
            EnrageDaoField,
            EnrageYinYang,
            EnrageUltimate,
            PhaseStrike,
            Death
        }

        public AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        public ref float StateTimer => ref NPC.ai[1];
        public ref float SubTimer => ref NPC.ai[2];
        public ref float StateData => ref NPC.ai[3];

        public const int TwinId = 1;
        public const int MainId = 0;

        private bool didSplit;
        private bool didEnrage;
        private bool merged;
        private bool deathFinished;
        private int enrageStep;
        private int partnerIndex = -1;
        private int lastAttackId = -1;

        // 纯本地表现层标量 (不参与同步)
        private float transientBloom;   // 大节拍泛光 (合体/解锁/释放), 逐帧衰减
        private float warpIntensity;    // GenericWarp(太初雾) 全屏后处理强度
        private float pendingWhiteFlash;// 白屏顿帧待发 (合体/终爆, 由 ScreenSystem 消费)
        private bool showChargeTele;    // 冲刺/穿行蓄力红线预警
        private Vector2 chargeTeleTarget;
        private Vector2 chargeLockedDir = Vector2.UnitX; // 冲锋锁定方向 (各端由同步 Target 确定性推导)

        // 相位穿行拍常量 (windup 虚化蓄力 / strike 凝形猎杀 / recover 收魂)
        private const int PhaseWindup = 30;
        private const int PhaseStrikeDur = 16;
        private const int PhaseRecover = 16;
        private const int PhaseCycle = PhaseWindup + PhaseStrikeDur + PhaseRecover;

        /// <summary>是否已合体为太初真身 (驱动视觉放大, 全客户端同步)。</summary>
        public bool Merged => merged;
        /// <summary>是否处于道之碎片场的"布场谜题"子阶段 (碎片节点据此判断存活)。</summary>
        public bool DaoFieldArming => State == AIState.EnrageDaoField && StateData == 0f;

        /// <summary>
        /// 接触伤害窗口 (虚实读法的机制面): 过场/喘息/死亡/虚化蓄力期一律无接触伤害,
        /// 全部由同步状态确定性推导 — 与龙身透明度严格对齐。
        /// </summary>
        public bool ContactDamageActive {
            get {
                switch (State) {
                    case AIState.Intro:
                    case AIState.SplitTransition:
                    case AIState.EnrageTransition:
                    case AIState.Death:
                    case AIState.Recovery:
                        return false;
                    case AIState.EnrageDaoField:
                        return StateData != 0f; // 谜题窗口无接触, 解锁受创窗口后恢复
                    case AIState.PhaseStrike: {
                        int localT = (int)StateTimer % PhaseCycle;
                        return localT >= PhaseWindup && localT < PhaseWindup + PhaseStrikeDur;
                    }
                    default:
                        return true;
                }
            }
        }

        /// <summary>尾部扫击许可: 只在作战节拍开火, 过场/喘息/谜题布场/死亡一律静默。</summary>
        public bool TailMayAttack =>
            State != AIState.Intro && State != AIState.SplitTransition &&
            State != AIState.EnrageTransition && State != AIState.Recovery &&
            State != AIState.Death && !DaoFieldArming;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true; // 头部合批绘制整条龙, 头出屏时身体仍须渲染
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.value = Item.buyPrice(gold: 10);
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AncestralDragonSoulBody>();
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
            if (!IsTwin) {
                State = AIState.Intro;
                StateTimer = 0;
                SubTimer = 0;
            }
            else {
                State = AIState.Patrol;
                NPC.life = NPC.lifeMax = Main.npc[(int)StateData].life;
            }
            segmentIndex = 0;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(didSplit);
            writer.Write(didEnrage);
            writer.Write(merged);
            writer.Write(IsTwin);
            writer.Write(partnerIndex);
            writer.Write(enrageStep);
            writer.Write(lastAttackId);
            writer.Write(deathFinished);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            didSplit = reader.ReadBoolean();
            didEnrage = reader.ReadBoolean();
            merged = reader.ReadBoolean();
            IsTwin = reader.ReadBoolean();
            partnerIndex = reader.ReadInt32();
            enrageStep = reader.ReadInt32();
            lastAttackId = reader.ReadInt32();
            deathFinished = reader.ReadBoolean();
        }

        public override bool CheckActive() => false;

        public override void AI() {
            base.AI();

            // 死亡演出优先: 不再关心目标/相变触发
            if (State == AIState.Death) {
                NPC.dontTakeDamage = true;
                showChargeTele = false;
                UpdateDeath();
                UpdateGhostLevel();
                transientBloom = MathHelper.Lerp(transientBloom, 0f, 0.05f);
                PublishScreenFx();
                StateTimer++;
                SubTimer++;
                return;
            }

            if (!Target.Alives()) {
                NPC.TargetClosest();
                if (!Target.Alives()) {
                    NPC.velocity.Y -= 0.2f;
                    NPC.EncourageDespawn(10);
                    return;
                }
            }

            // 半血分裂: 只对主龙触发一次
            if (!IsTwin && !didSplit && NPC.life < NPC.lifeMax * 0.5f && State != AIState.Intro) {
                TransitionTo(AIState.SplitTransition);
                didSplit = true;
            }

            // 残血(双龙合计 <25%): 进入双魂回拢终曲。只对主龙触发一次, 真正成为一幕"act"。
            if (!IsTwin && !didEnrage && CombinedFraction() < 0.25f
                && State != AIState.Intro && State != AIState.SplitTransition) {
                didEnrage = true;
                TransitionTo(AIState.EnrageTransition);
                CommandTwin(AIState.EnrageTransition, 0f);
                ClearBossProjectiles(); // 换阶段清弹 (公平阀门)
                transientBloom = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 14f, 8f, 50, 2000f, "AncestralDragonEnrage"));
                    ACMUtils.AddScreenShake(12f);
                    AncestralDragonSky.TriggerFlash(1f);
                }
            }

            // 头部自管无敌: 各状态每帧按需置 true; 默认可被击中。(身体段继承 realLife 头部的 dontTakeDamage)
            NPC.dontTakeDamage = false;
            showChargeTele = false;

            switch (State) {
                case AIState.Intro: UpdateIntro(); break;
                case AIState.Patrol: UpdatePatrol(); break;
                case AIState.ScaleBarrage: UpdateScaleBarrage(); break;
                case AIState.SigilEruption: UpdateSigilEruption(); break;
                case AIState.YinYangBind: UpdateYinYangBind(); break;
                case AIState.DragonBeam: UpdateDragonBeam(); break;
                case AIState.SpiralDive: UpdateSpiralDive(); break;
                case AIState.RakingCharge: UpdateRakingCharge(); break;
                case AIState.SplitTransition: UpdateSplitTransition(); break;
                case AIState.TwinLink: UpdateTwinLink(); break;
                case AIState.TwinCrossfire: UpdateTwinCrossfire(); break;
                case AIState.TwinPressure: UpdateTwinPressure(); break;
                case AIState.Recovery: UpdateRecovery(); break;
                case AIState.EnrageTransition: UpdateEnrageTransition(); break;
                case AIState.EnrageDaoField: UpdateEnrageDaoField(); break;
                case AIState.EnrageYinYang: UpdateEnrageYinYang(); break;
                case AIState.EnrageUltimate: UpdateEnrageUltimate(); break;
                case AIState.PhaseStrike: UpdatePhaseStrike(); break;
            }

            UpdateGhostLevel();
            transientBloom = MathHelper.Lerp(transientBloom, 0f, 0.05f);
            PublishScreenFx();

            StateTimer++;
            SubTimer++;
        }

        private void TransitionTo(AIState newState) {
            State = newState;
            StateTimer = 0;
            SubTimer = 0;
            StateData = 0;
            NPC.netUpdate = true;
        }

        #region 虚实读法

        /// <summary>按状态推导虚化目标 (确定性, 各端一致); 凝形快、虚化缓 — 威胁必须立刻可读。</summary>
        private float GhostTargetByState() {
            switch (State) {
                case AIState.Intro:
                    return StateTimer < 200 ? 0.6f : 0.15f;
                case AIState.Recovery:
                    return 0.55f; // 喘息拍: 半散成星尘 = "现在安全, 打我"
                case AIState.SplitTransition:
                case AIState.EnrageTransition:
                    return 0.85f;
                case AIState.Death:
                    return MathHelper.Clamp(StateTimer / 220f, 0f, 0.9f);
                case AIState.EnrageDaoField:
                    return StateData == 0f ? 0.6f : 0f;
                case AIState.PhaseStrike: {
                    int localT = (int)StateTimer % PhaseCycle;
                    if (localT < PhaseWindup) return 1f;
                    if (localT < PhaseWindup + PhaseStrikeDur) return 0f;
                    return 0.5f;
                }
                default:
                    return 0f;
            }
        }

        private void UpdateGhostLevel() {
            float target = GhostTargetByState();
            GhostLevel = target < GhostLevel
                ? MathHelper.Lerp(GhostLevel, target, 0.4f)   // 凝形要快
                : MathHelper.Lerp(GhostLevel, target, 0.09f); // 虚化缓入
        }

        /// <summary>死亡「归墟」星散波: 尾梢先散, 逐节向头部传导 (段节据此取各自溶解值)。</summary>
        public float DeathDissolveFor(int segIndex) {
            if (State != AIState.Death)
                return 0f;
            float start = 40f + (SummonMax - segIndex) * 2.2f;
            return MathHelper.Clamp((StateTimer - start) / 40f, 0f, 1f);
        }

        /// <summary>体内流光沿脊椎的行波亮度 (绘制时按节取样); 大节拍时全身透亮。</summary>
        public float FlowGlowFor(int segIndex) {
            float wave = MathF.Cos(globalTime * 2.4f - segIndex * 0.22f);
            float pulse = MathF.Pow(MathF.Max(0f, wave), 10f);
            float wave2 = MathF.Cos(globalTime * 1.05f - segIndex * 0.07f + 2.1f);
            pulse += MathF.Pow(MathF.Max(0f, wave2), 14f) * 0.6f;
            return MathHelper.Clamp(pulse * 0.55f + transientBloom * 0.7f, 0f, 1f);
        }

        #endregion

        #region 协同/狂暴 辅助

        private NPC GetTwin() {
            if (IsTwin) return null;
            if (partnerIndex < 0 || partnerIndex >= Main.maxNPCs) return null;
            NPC t = Main.npc[partnerIndex];
            if (t.active && t.type == Type && t.ModNPC is AncestralDragonSoulHead h && h.IsTwin) return t;
            return null;
        }

        /// <summary>双龙合计血量比例 (用于"合计 &lt;25%"判定); 无双子时即自身比例。</summary>
        private float CombinedFraction() {
            float life = NPC.life, max = NPC.lifeMax;
            NPC t = GetTwin();
            if (t != null) { life += t.life; max += t.lifeMax; }
            return max <= 0f ? 0f : life / max;
        }

        /// <summary>(服务器/SP) 命令副本龙切到指定状态, 用于脚本化协同节拍。归墟中的副本龙不再被打断。</summary>
        private void CommandTwin(AIState s, float data) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            NPC t = GetTwin();
            if (t != null && t.ModNPC is AncestralDragonSoulHead th && th.State != AIState.Death) {
                th.State = s;
                th.StateTimer = 0;
                th.SubTimer = 0;
                th.StateData = data;
                t.netUpdate = true;
            }
        }

        private static AIState EnrageStateFor(int step) => (step % 3) switch {
            0 => AIState.EnrageDaoField,
            1 => AIState.EnrageYinYang,
            _ => AIState.EnrageUltimate
        };

        /// <summary>大招收尾统一走喘息拍 (无弹幕), 由 <see cref="UpdateRecovery"/> 决定下一拍。</summary>
        private void BeginRecovery() => TransitionTo(AIState.Recovery);

        /// <summary>清空本 Boss 全部敌对弹幕 (换阶段/死亡的公平阀门)。</summary>
        private static void ClearBossProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int[] types = {
                ModContent.ProjectileType<AncestralMistBolt>(),
                ModContent.ProjectileType<SpiralSoulFragment>(),
                ModContent.ProjectileType<AncestralSoulFragment>(),
                ModContent.ProjectileType<HomingSoulOrb>(),
                ModContent.ProjectileType<TailSweepWave>(),
                ModContent.ProjectileType<AncestralDragonBeam>(),
                ModContent.ProjectileType<DragonScaleShard>(),
                ModContent.ProjectileType<AncestralSoulSigil>(),
                ModContent.ProjectileType<YinYangBinderOrb>(),
                ModContent.ProjectileType<SoulTetherChain>(),
                ModContent.ProjectileType<YinYangOverdrivePulse>()
            };
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (!p.hostile) continue;
                for (int i = 0; i < types.Length; i++) {
                    if (p.type == types[i]) { p.Kill(); break; }
                }
            }
        }

        #endregion

        #region 机动辅助

        private void FlyToward(Vector2 destination, float acceleration, float maxSpeed, float turnAccel = -1f) {
            if (turnAccel < 0) turnAccel = acceleration * 0.6f;
            Vector2 toDest = destination - NPC.Center;
            float dist = toDest.Length();
            if (dist < 1f) return;
            Vector2 desiredDir = toDest / dist;
            Vector2 currentDir = NPC.velocity.SafeNormalize(desiredDir);
            Vector2 newDir = Vector2.Lerp(currentDir, desiredDir, 0.08f).SafeNormalize(desiredDir);
            float curSpeed = NPC.velocity.Length();
            curSpeed += acceleration;
            if (curSpeed > maxSpeed) curSpeed = maxSpeed;
            NPC.velocity = newDir * curSpeed;
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
        }

        private Vector2 LissajousPoint(float phase, float rx, float ry) {
            return new Vector2(rx * MathF.Cos(phase), ry * MathF.Sin(phase * 2f));
        }

        /// <summary>重物三档梯级刹车 (长而顺滑的止动)。</summary>
        private void TieredBrake() {
            float sp = NPC.velocity.Length();
            if (sp > 24f) NPC.velocity *= 0.925f;
            if (sp > 12f) NPC.velocity *= 0.94f;
            if (sp > 4f) NPC.velocity *= 0.965f;
        }

        /// <summary>
        /// 掠袭冲锋通用循环拍: 入位(带反向蓄力后吸) → 锁定读线(近乎静止+红线) → 一帧点火 → 直线 → 硬刹。
        /// cycle = 84f。RakingCharge 与 EnrageUltimate 子拍共用, 只差冲刺速度。
        /// </summary>
        private void RunRakingCycle(int localT, int idx, float dashSpeed) {
            const int approach = 40;
            const int lockDur = 14;
            const int straight = 12;

            if (localT < approach) {
                // 入位: 玩家侧翼; 最后 14f 沿冲刺反方向三次幂后吸 (深吸气)
                float side = (idx % 2 == 0) ? 1f : -1f;
                Vector2 basePos = Target.Center + new Vector2(side * 800f, MathF.Sin(globalTime * 2.3f) * 60f);
                Vector2 aimDir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float reelT = MathHelper.Clamp((localT - 26) / 14f, 0f, 1f);
                Vector2 windPos = basePos - aimDir * (MathF.Pow(reelT, 3f) * 170f);
                FlyToward(windPos, 1.0f, 21f);
            }
            else if (localT < approach + lockDur) {
                if (localT == approach) {
                    // 锁定预判线 (提前量), 之后不再修正 — 可读且有承诺感
                    chargeLockedDir = (Target.Center + Target.velocity * 11f - NPC.Center).SafeNormalize(Vector2.UnitX);
                }
                NPC.velocity *= 0.85f; // 慢启动阀门: 蓄力期近乎静止
                NPC.rotation = chargeLockedDir.ToRotation();
                NPC.spriteDirection = chargeLockedDir.X > 0 ? 1 : -1;
                showChargeTele = true;
                chargeTeleTarget = NPC.Center + chargeLockedDir * 1500f;
            }
            else if (localT == approach + lockDur) {
                // 点火: 一帧 set 速度
                NPC.velocity = chargeLockedDir * dashSpeed;
                NPC.netUpdate = true;
                NPC.rotation = NPC.velocity.ToRotation();
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.2f }, NPC.Center);
                    ACMUtils.AddScreenShake(6f);
                }
            }
            else if (localT < approach + lockDur + straight) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
                if (localT == approach + lockDur + 5 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 6f,
                            ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                    }
                }
            }
            else {
                NPC.velocity *= 0.72f; // 硬刹: 砸停在位, 不飞出屏幕绕圈
                if (NPC.velocity.LengthSquared() > 1f) {
                    NPC.rotation = NPC.velocity.ToRotation();
                    NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
                }
            }
        }

        #endregion

        #region 入场「星海凝形」

        private void UpdateIntro() {
            if (IsTwin) { TransitionTo(AIState.Patrol); return; }

            NPC.dontTakeDamage = true;

            if (StateTimer < 200) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -2.2f), 0.04f);
            }
            else {
                NPC.velocity *= 0.9f; // 静止凝视 — 威压主要来自静止
            }
            NPC.rotation = Utils.AngleLerp(NPC.rotation, -MathHelper.PiOver2, 0.05f);
            mistAlpha = MathHelper.Lerp(mistAlpha, 1.2f, 0.02f);

            // 头部显形: 80 帧星尘编织 (身体各节随生成各自 36f 显形 → 全龙从头到尾自然织成)
            spawnDissolve = MathHelper.Clamp(1f - StateTimer / 80f, 0f, 1f);

            // 星流汇聚 (比例吸入 + 切向涡旋, 蓄力语法); 200f 后全部剪断 = 爆发前的静默
            if (Main.netMode != NetmodeID.Server && StateTimer < 200 && StateTimer % 2 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * Main.rand.NextFloat(260f, 560f);
                Vector2 pull = (NPC.Center - pos) * 0.055f;
                Vector2 swirl = pull.RotatedBy(MathHelper.PiOver2) * 0.5f;
                int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan,
                    0, 0, 110, Color.White, Main.rand.NextFloat(1.2f, 2f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = pull + swirl;
            }

            // 心跳脉冲 ×2 (渐强渐近)
            if (Main.netMode != NetmodeID.Server && ((int)StateTimer == 90 || (int)StateTimer == 150)) {
                bool second = StateTimer > 100;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = second ? -0.5f : -0.7f, Volume = second ? 0.9f : 0.7f }, NPC.Center);
                AncestralDragonSky.TriggerFlash(second ? 0.6f : 0.4f);
            }

            // 开战咆哮
            if ((int)StateTimer == 258) {
                transientBloom = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 10f, 6f, 30, 2000f, "AncestralIntro"));
                    ACMUtils.AddScreenShake(10f);
                    AncestralDragonSky.TriggerFlash(1f);
                    for (int i = 0; i < 60; i++) {
                        float ang = MathHelper.TwoPi * i / 60f;
                        Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(7f, 16f);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud, vel.X, vel.Y, 90, Color.White, 2.6f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 270) {
                TransitionTo(AIState.Patrol);
            }
        }

        #endregion

        #region 基础状态

        private void UpdatePatrol() {
            float phase = globalTime * 0.9f;
            Vector2 orbit = LissajousPoint(phase, 520f, 260f);
            Vector2 anchor = Target.Center + orbit + new Vector2(0, -60f);
            FlyToward(anchor, 0.55f, 16f);

            // 连接拍收短: 真正的喘息由 Recovery 承担, 巡游只是标点
            if (StateTimer > 45) {
                PickNextAttack();
            }
        }

        private void PickNextAttack() {
            AIState[] pool;
            if (didSplit) {
                // 互补角色: 主龙=符文/场地控制; 副龙=交叉火力/机动
                pool = IsTwin
                    ? new[] { AIState.ScaleBarrage, AIState.TwinCrossfire, AIState.SpiralDive, AIState.RakingCharge, AIState.PhaseStrike }
                    : new[] { AIState.SigilEruption, AIState.YinYangBind, AIState.DragonBeam, AIState.TwinPressure, AIState.ScaleBarrage };
            }
            else if (NPC.life < NPC.lifeMax * 0.7f) {
                pool = new[] {
                    AIState.ScaleBarrage, AIState.SigilEruption, AIState.YinYangBind,
                    AIState.DragonBeam, AIState.SpiralDive, AIState.RakingCharge, AIState.PhaseStrike
                };
            }
            else {
                pool = new[] {
                    AIState.ScaleBarrage, AIState.SpiralDive, AIState.RakingCharge, AIState.DragonBeam
                };
            }

            AIState pick;
            do {
                pick = pool[Main.rand.Next(pool.Length)];
            } while ((int)pick == lastAttackId && pool.Length > 1);
            lastAttackId = (int)pick;
            TransitionTo(pick);
        }

        /// <summary>鳞爆弹幕: 三波「汇聚蓄力 → 扇形鳞爆+后座 → 拉开」, 每波有明确呼吸。</summary>
        private void UpdateScaleBarrage() {
            const int lead = 10;
            const int waveLen = 50;
            const int waves = 3;

            Vector2 orbitPos = Target.Center + new Vector2(MathF.Cos(globalTime * 1.1f) * 560f, MathF.Sin(globalTime * 1.5f) * 240f - 60f);
            FlyToward(orbitPos, 0.7f, 17f);

            if (StateTimer < lead)
                return;

            int wi = (int)(StateTimer - lead) / waveLen;
            int wt = (int)(StateTimer - lead) % waveLen;
            if (wi >= waves) {
                BeginRecovery();
                return;
            }

            Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 42f;
            if (wt < 19) {
                // 汇聚星尘: 密度随蓄力升, 72% 处截止 (静默拍)
                if (Main.netMode != NetmodeID.Server && Main.rand.NextFloat() < 0.35f + wt * 0.03f) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = mouth + ang.ToRotationVector2() * Main.rand.NextFloat(60f, 150f);
                    int dust = Dust.NewDust(pos, 0, 0, DustID.WhiteTorch, 0, 0, 120, Color.White, 1.4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (mouth - pos) * 0.09f;
                }
            }
            else if (wt == 26) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                // 发射后座: 头部整体向后一顿 (重量感)
                NPC.velocity -= toPlayer * 7f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = -2; i <= 2; i++) {
                        Vector2 vel = toPlayer.RotatedBy(i * 0.17f) * 6f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), mouth, vel,
                            ModContent.ProjectileType<DragonScaleShard>(), NPC.damage / 3, 1f);
                    }
                    NPC.netUpdate = true;
                }
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item72 with { Pitch = 0.3f, Volume = 0.7f }, NPC.Center);
            }
        }

        private void UpdateSigilEruption() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime * 1.2f) * 300f, -360f);
            FlyToward(hover, 0.5f, 14f);

            int casts = didSplit ? 6 : 4;
            int castInterval = 24;
            if (StateTimer > 40 && StateTimer < 40 + casts * castInterval && (StateTimer - 40) % castInterval == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int idx = (int)((StateTimer - 40) / castInterval);
                    float spread = (idx - (casts - 1) / 2f) * 160f;
                    Vector2 pos = Target.Center + new Vector2(spread + Main.rand.NextFloat(-40f, 40f), 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulSigil>(), NPC.damage / 2, 0f);
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (StateTimer > 40 + casts * castInterval + 160) BeginRecovery();
        }

        private void UpdateYinYangBind() {
            Vector2 hover = Target.Center + new Vector2(0, -420f);
            FlyToward(hover, 0.5f, 13f);

            if (StateTimer > 40 && StateTimer < 220 && (StateTimer - 40) % 55 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 center = NPC.Center + (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 120f;
                    float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
                    int a = Projectile.NewProjectile(NPC.GetSource_FromAI(), center + baseAng.ToRotationVector2() * 40f, Vector2.Zero,
                        ModContent.ProjectileType<YinYangBinderOrb>(), NPC.damage / 3, 0f, -1, -1, 0f);
                    int b = Projectile.NewProjectile(NPC.GetSource_FromAI(), center - baseAng.ToRotationVector2() * 40f, Vector2.Zero,
                        ModContent.ProjectileType<YinYangBinderOrb>(), NPC.damage / 3, 0f, -1, -1, MathHelper.Pi);
                    if (a >= 0 && b >= 0) {
                        Main.projectile[a].ai[0] = b;
                        Main.projectile[b].ai[0] = a;
                        Main.projectile[a].netUpdate = true;
                        Main.projectile[b].netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = -0.3f, Volume = 0.7f }, NPC.Center);
            }

            if (StateTimer > 260) BeginRecovery();
        }

        /// <summary>祖龙吐息: 20f 入位 → 弹幕自带 55f 金白→赤红预警 → 110f 缓扫喷息 (头部持续受后座)。</summary>
        private void UpdateDragonBeam() {
            const int approach = 20;
            const int tele = 55;
            const int beamDur = 110;

            if (StateTimer < approach) {
                Vector2 hover = Target.Center + new Vector2(Target.Center.X > NPC.Center.X ? -620f : 620f, -200f);
                FlyToward(hover, 0.8f, 18f);
            }
            else {
                // 喷息期: 近乎悬停 (慢启动阀门) + 反向后座缓推
                NPC.velocity *= 0.92f;
                if (StateTimer > approach + tele && StateTimer < approach + tele + beamDur) {
                    Vector2 back = (NPC.Center - Target.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity += back * 0.22f;
                    if (NPC.velocity.Length() > 6f)
                        NPC.velocity *= 0.95f;
                }
                // 喷息期面向玩家 (翻转与旋转同步, 避免贴图倒置)
                NPC.rotation = Utils.AngleLerp(NPC.rotation, (Target.Center - NPC.Center).ToRotation(), 0.06f);
                NPC.spriteDirection = Target.Center.X > NPC.Center.X ? 1 : -1;
            }

            if ((int)StateTimer == approach && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<AncestralDragonBeam>(), NPC.damage / 2, 0f, -1, NPC.whoAmI,
                    (Target.Center - NPC.Center).ToRotation());
            }
            if ((int)StateTimer == approach && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);

            // 预警期: 龙口汇聚星尘 (读法冗余: 线 + 汇聚双通道)
            if (Main.netMode != NetmodeID.Server && StateTimer > approach && StateTimer < approach + tele * 0.72f && StateTimer % 2 == 0) {
                Vector2 mouth = NPC.Center + NPC.rotation.ToRotationVector2() * 40f;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = mouth + ang.ToRotationVector2() * Main.rand.NextFloat(50f, 160f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Clentaminator_Cyan, 0, 0, 130, Color.White, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (mouth - pos) * 0.085f;
            }

            if (StateTimer > approach + tele + beamDur + 20) BeginRecovery();
        }

        /// <summary>螺旋俯冲: 快速入轨(提前到位跳表) → 收缩螺旋 → 盘蓄静默 → 切向破圈爆冲 → 硬刹。</summary>
        private void UpdateSpiralDive() {
            if (StateTimer < 26) {
                if ((int)StateTimer == 0)
                    NPC.localAI[0] = (NPC.Center - Target.Center).ToRotation();
                Vector2 entry = Target.Center + NPC.localAI[0].ToRotationVector2() * 600f;
                FlyToward(entry, 1.1f, 26f);
                // 提前到位则跳表 — 不等自己的钟
                if (NPC.Distance(entry) < 90f && StateTimer < 24) {
                    StateTimer = 25;
                    NPC.netUpdate = true;
                }
            }
            else if (StateTimer < 170) {
                float t = (StateTimer - 26) / 144f;
                float radius = MathHelper.Lerp(600f, 150f, t);
                float angularSpeed = MathHelper.Lerp(0.05f, 0.13f, t);
                NPC.localAI[0] += angularSpeed;
                Vector2 orbit = Target.Center + NPC.localAI[0].ToRotationVector2() * radius;
                FlyToward(orbit, 1.2f, 28f);

                if (StateTimer % 10 == 0 && StateTimer > 46 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 5f,
                            ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.5f);
                    }
                }
            }
            else if (StateTimer < 185) {
                NPC.velocity *= 0.8f; // 盘蓄: 缓停 + 静默 (无弹幕)
            }
            else if ((int)StateTimer == 185) {
                // 破圈: 切向一帧爆冲
                Vector2 tangent = (NPC.localAI[0] + MathHelper.PiOver2).ToRotationVector2();
                if (Vector2.Dot(tangent, Target.Center - NPC.Center) < 0f)
                    tangent = -tangent;
                NPC.velocity = tangent * 44f;
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.netUpdate = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 8; i++) {
                        float ang = MathHelper.TwoPi * i / 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, ang.ToRotationVector2() * 6.5f,
                            ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.5f);
                    }
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.35f, Volume = 0.9f }, NPC.Center);
                    ACMUtils.AddScreenShake(7f);
                }
            }
            else if (StateTimer < 215) {
                NPC.velocity *= 0.93f;
                if (NPC.velocity.LengthSquared() > 1f)
                    NPC.rotation = NPC.velocity.ToRotation();
            }
            else {
                BeginRecovery();
            }
        }

        private void UpdateRakingCharge() {
            const int cycleLen = 84;
            const int charges = 3;
            int idx = (int)(StateTimer / cycleLen);
            if (idx >= charges) {
                BeginRecovery();
                return;
            }
            RunRakingCycle((int)StateTimer % cycleLen, idx, 46f);
        }

        /// <summary>
        /// 相位穿行猎杀: 虚化蓄力 (透明无接触, 魂线预警) → 一帧凝形爆冲 (唯一伤害窗口) → 收魂硬刹。
        /// 「透明度即威胁」核心机制招。
        /// </summary>
        private void UpdatePhaseStrike() {
            const int strikes = 2;
            int idx = (int)(StateTimer / PhaseCycle);
            int localT = (int)StateTimer % PhaseCycle;

            if (idx >= strikes) {
                BeginRecovery();
                return;
            }

            if (localT < PhaseWindup) {
                // 虚化蓄力: 大摇大摆漂到玩家侧上方 (可穿过玩家, 无害)
                float side = (idx % 2 == 0) ? 1f : -1f;
                Vector2 windPos = Target.Center + new Vector2(side * 620f, -260f);
                FlyToward(windPos, 0.7f, 17f);

                if (localT == 24) {
                    // 锁定预判方向 — 之后红线冻结, 玩家有 6f 纯读线时间
                    chargeLockedDir = (Target.Center + Target.velocity * 12f - NPC.Center).SafeNormalize(Vector2.UnitX);
                }
                if (localT >= 6) {
                    showChargeTele = true;
                    Vector2 dir = localT >= 24 ? chargeLockedDir
                        : (Target.Center + Target.velocity * 12f - NPC.Center).SafeNormalize(Vector2.UnitX);
                    chargeTeleTarget = NPC.Center + dir * 1500f;
                }
                if (localT == 20 && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.4f, Volume = 0.7f }, NPC.Center);
            }
            else if (localT < PhaseWindup + PhaseStrikeDur) {
                if (localT == PhaseWindup) {
                    // 凝形点火: 透明→实体一帧完成, 伤害窗口与视觉严格对齐
                    GhostLevel = 0f;
                    NPC.velocity = chargeLockedDir * 58f;
                    NPC.rotation = NPC.velocity.ToRotation();
                    NPC.netUpdate = true;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.5f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                    }
                }
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
                if (localT >= PhaseWindup + 10)
                    NPC.velocity *= 0.9f;
                if (localT == PhaseWindup + 12 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        for (int j = 1; j <= 3; j++) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * (3f + j * 1.6f),
                                ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.4f);
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.68f; // 收魂硬刹
                if (NPC.velocity.LengthSquared() > 1f)
                    NPC.rotation = NPC.velocity.ToRotation();
            }
        }

        /// <summary>大招后强制喘息拍: 半散成星尘 (无接触伤害), 缓飞, 给玩家输出/呼吸窗口。狂暴期则推进确定性循环。</summary>
        private void UpdateRecovery() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime) * 220f, -300f);
            FlyToward(hover, 0.35f, 9f);

            float dur = didEnrage ? 70f : 80f;
            if (StateTimer > dur) {
                if (didEnrage) {
                    enrageStep++;
                    TransitionTo(EnrageStateFor(enrageStep));
                }
                else {
                    TransitionTo(AIState.Patrol);
                }
            }
        }

        #endregion

        #region 半血分裂 / 双龙协同

        private void UpdateSplitTransition() {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if ((int)StateTimer == 1) {
                transientBloom = 1f;
                ClearBossProjectiles(); // 换阶段清弹
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.2f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 18f, 10f, 90, 2000f, "AncestralSplit"));
                    ACMUtils.AddScreenShake(12f);
                    AncestralDragonSky.TriggerFlash(0.9f);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                int particles = 8;
                for (int i = 0; i < particles; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * Main.rand.NextFloat(180f, 280f);
                    int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan, 0, 0, 100, Color.White, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if ((int)StateTimer == 120 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnTwinDragon();
            }

            if (StateTimer > 180) {
                NPC.dontTakeDamage = false;
                // 脚本化开场: 主/副同入 双链(intro=1) → 交叉火力
                TransitionTo(AIState.TwinLink);
                StateData = 1f;
                NPC.netUpdate = true;
                CommandTwin(AIState.TwinLink, 1f);
            }
        }

        private void SpawnTwinDragon() {
            int halfLife = Math.Max(1, NPC.life / 2);
            NPC.life = halfLife;
            NPC.lifeMax = halfLife;
            NPC.netUpdate = true;

            Vector2 twinPos = NPC.Center + new Vector2(-400f, -200f);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)twinPos.X, (int)twinPos.Y, Type, NPC.whoAmI + 1);
            if (idx < Main.maxNPCs) {
                NPC twin = Main.npc[idx];
                if (twin.ModNPC is AncestralDragonSoulHead twinHead) {
                    twinHead.IsTwin = true;
                    twinHead.State = AIState.Patrol;
                    twinHead.StateData = NPC.whoAmI;
                    twinHead.didSplit = true;
                    twinHead.partnerIndex = NPC.whoAmI;
                    // 斩断父链: GetSource_FromAI 是 EntitySource_Parent, BasicWorm.OnSpawn 会把副本龙挂进主龙
                    // 蠕虫链 (realLife=主龙 → 副本龙伤害全部并回主龙血池, "真分裂"失效)。这里强制独立。
                    twinHead.FatherWorm = -1;
                    twinHead.SummonCount = 0;
                    twin.realLife = -1;
                    twin.life = twin.lifeMax = halfLife;
                    twin.target = NPC.target;
                    partnerIndex = idx;
                    twin.netUpdate = true;
                    NetMessage.SendData(MessageID.SyncNPC, number: idx);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 80; i++) {
                    float ang = MathHelper.TwoPi * i / 80f;
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud, vel.X, vel.Y, 100, Color.White, 3f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        private void UpdateTwinLink() {
            if (!IsTwin) {
                Vector2 myPos = Target.Center + new Vector2(-500f, -240f + MathF.Sin(globalTime) * 80f);
                FlyToward(myPos, 0.5f, 15f);

                if ((int)StateTimer == 30 && partnerIndex >= 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<SoulTetherChain>(), NPC.damage / 3, 0f, -1, NPC.whoAmI, partnerIndex);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                }
            }
            else {
                Vector2 myPos = Target.Center + new Vector2(500f, -240f + MathF.Sin(globalTime + MathHelper.Pi) * 80f);
                FlyToward(myPos, 0.5f, 15f);
            }

            if (StateTimer > 300) {
                if (StateData == 1f) {
                    // 脚本化开场链 → 交叉火力
                    TransitionTo(AIState.TwinCrossfire);
                    if (!IsTwin)
                        CommandTwin(AIState.TwinCrossfire, 0f);
                }
                else {
                    BeginRecovery();
                }
            }
        }

        private void UpdateTwinCrossfire() {
            float sideSign = IsTwin ? 1f : -1f;
            Vector2 myPos = Target.Center + new Vector2(sideSign * 620f, MathF.Sin(globalTime * 1.5f + (IsTwin ? MathHelper.Pi : 0)) * 160f - 100f);
            FlyToward(myPos, 0.55f, 15f);

            int phaseOffset = IsTwin ? 15 : 0;
            if ((StateTimer + phaseOffset) % 30 == 0 && StateTimer > 40 && StateTimer < 260 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * 0.14f) * 7.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.3f);
                }
            }

            if (StateTimer > 300) BeginRecovery();
        }

        private void UpdateTwinPressure() {
            float dir = IsTwin ? -1f : 1f;
            StateData += 0.07f * dir;
            float radius = MathHelper.Lerp(520f, 200f, MathHelper.Clamp(StateTimer / 180f, 0f, 1f));
            Vector2 orbit = Target.Center + StateData.ToRotationVector2() * radius;
            FlyToward(orbit, 0.9f, 24f);

            if (!IsTwin && (int)StateTimer == 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = -2; i <= 2; i++) {
                    Vector2 pos = Target.Center + new Vector2(i * 180f, 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulSigil>(), NPC.damage / 2, 0f);
                }
            }

            if (StateTimer > 220) BeginRecovery();
        }

        #endregion

        #region 狂暴终曲: 双魂回拢 + 道之碎片场 + 阴阳超载 + 编排终极

        /// <summary>
        /// 双魂回拢过场 (~6s i-frame 镜头拍)。双子向竞技场中心对冲, 灵链处决扫线 (PrimordialRecallBeam X 形),
        /// 合体白屏顿帧 → 太初真身。**i-frame 仅限过场拍 (避免被秒过场); 真正的改变是后续终曲循环, 而非加速。**
        /// 副本龙不存在则单龙直接进入终曲 (不合体)。
        /// </summary>
        private void UpdateEnrageTransition() {
            NPC.dontTakeDamage = true;

            Vector2 arenaCenter = Target.Center + new Vector2(0, -200f);

            if (!IsTwin) {
                // 主龙: 编排回拢
                FlyToward(arenaCenter + new Vector2(-160f, 0f), 0.5f, 14f);

                if ((int)StateTimer == 30 && GetTwin() != null && Main.netMode != NetmodeID.MultiplayerClient) {
                    // X 形处决扫线
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), arenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<PrimordialRecallBeam>(), NPC.damage / 2, 0f, -1, 0f, 1f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), arenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<PrimordialRecallBeam>(), NPC.damage / 2, 0f, -1, MathHelper.PiOver2, -1f);
                }

                if (Main.netMode != NetmodeID.Server && StateTimer % 4 == 0) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = arenaCenter + a.ToRotationVector2() * Main.rand.NextFloat(300f, 480f);
                    int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan, 0, 0, 100, Color.White, 2.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (arenaCenter - pos).SafeNormalize(Vector2.Zero) * 7f;
                }

                // 合体帧: 全场唯一一次白屏顿帧
                if ((int)StateTimer == 210) {
                    MergeDragons();
                    transientBloom = 1f;
                    pendingWhiteFlash = 1.1f;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.3f }, NPC.Center);
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 22f, 12f, 60, 2000f, "AncestralMerge"));
                        ACMUtils.AddScreenShake(16f);
                        AncestralDragonSky.TriggerFlash(1.2f);
                        for (int i = 0; i < 90; i++) {
                            float ang = MathHelper.TwoPi * i / 90f;
                            Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(10f, 22f);
                            int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud, vel.X, vel.Y, 80, Color.White, 3.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                }

                if (StateTimer > 210)
                    FlyToward(arenaCenter, 0.4f, 10f);

                if (StateTimer > 360) {
                    NPC.dontTakeDamage = false;
                    enrageStep = 0;
                    TransitionTo(EnrageStateFor(0));
                }
            }
            else {
                // 副本龙: 向中心回拢 (合体帧由主龙移除)
                FlyToward(arenaCenter + new Vector2(160f, 0f), 0.5f, 14f);
            }
        }

        /// <summary>(服务器/SP) 双子合体: 合计血量为单一血池, 移除副本龙, 标记 merged (视觉放大)。</summary>
        private void MergeDragons() {
            NPC t = GetTwin();
            if (t == null) return;

            NPC.lifeMax += t.lifeMax;
            NPC.life += t.life;
            if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;
            merged = true;

            if (t.ModNPC is AncestralDragonSoulHead th) th.partnerIndex = -1;
            t.life = 0;
            t.active = false; // 段位经 FatherNPC 失活级联消失; 直接 active=false 不触发 OnKill/掉落/旗标

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: partnerIndex);

            partnerIndex = -1;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 道之碎片场 (谜题窗口): 8 颗碎片环绕真身逐颗显形, 每颗须被击中一次方可消解; 全部消解前不吃伤。
        /// 解谜成功 → 受创窗口 (玩家输出); 超时 → 强制引爆成弹幕并开放窗口。
        /// </summary>
        private void UpdateEnrageDaoField() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime * 0.8f) * 140f, -300f);
            FlyToward(hover, 0.35f, 8f);

            if (StateData == 0f) {
                NPC.dontTakeDamage = true;

                if ((int)StateTimer == 1) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) SpawnDaoField();
                    if (Main.netMode != NetmodeID.Server)
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                }

                // 解谜/超时判定 (服务器/SP 权威, netUpdate 同步)
                if (Main.netMode != NetmodeID.MultiplayerClient && StateTimer > 20) {
                    int remaining = CountDaoFragments();
                    if (remaining == 0) {
                        StateData = 1f; SubTimer = 0; transientBloom = 1f; NPC.netUpdate = true;
                    }
                    else if (StateTimer >= 720) {
                        DetonateDaoField();
                        StateData = 1f; SubTimer = 0; NPC.netUpdate = true;
                    }
                }
            }
            else {
                // 受创窗口: 无弹幕, 强泛光提示"现在输出"
                NPC.dontTakeDamage = false;
                if ((int)SubTimer == 1) {
                    transientBloom = 1f;
                    if (Main.netMode != NetmodeID.Server) {
                        AncestralDragonSky.TriggerFlash(0.7f);
                        ACMUtils.AddScreenShake(6f);
                    }
                }
                if (SubTimer > 240) BeginRecovery();
            }
        }

        private void SpawnDaoField() {
            const int n = 8;
            for (int i = 0; i < n; i++) {
                float ang = MathHelper.TwoPi * i / n;
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * 300f;
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                    ModContent.NPCType<AncestralDaoFragment>(), 0, NPC.whoAmI, ang, 300f);
            }
        }

        private int CountDaoFragments() {
            int type = ModContent.NPCType<AncestralDaoFragment>();
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) count++;
            }
            return count;
        }

        private void DetonateDaoField() {
            int type = ModContent.NPCType<AncestralDaoFragment>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.type != type || (int)n.ai[0] != NPC.whoAmI) continue;
                // 超时惩罚: 引爆成可读雾弹环
                for (int j = 0; j < 8; j++) {
                    float a = MathHelper.TwoPi * j / 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), n.Center, a.ToRotationVector2() * 7f,
                        ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                }
                n.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
            }
        }

        /// <summary>阴阳超载 (强制机制): 单控制弹张开阴阳环, 蓄满后全屏魂蚀脉冲; 不在安全缝者被抽走百分比血量。</summary>
        private void UpdateEnrageYinYang() {
            NPC.dontTakeDamage = false;
            Vector2 hover = Target.Center + new Vector2(0, -360f);
            FlyToward(hover, 0.4f, 10f);

            const int windup = 70;
            if ((int)StateTimer == windup && Main.netMode != NetmodeID.MultiplayerClient) {
                float gapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), Target.Center, Vector2.Zero,
                    ModContent.ProjectileType<YinYangOverdrivePulse>(), NPC.damage / 3, 0f, -1, gapAngle);
            }
            if ((int)StateTimer == windup - 1 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0f, Volume = 0.9f }, NPC.Center);

            if (StateTimer > windup + 215) BeginRecovery();
        }

        /// <summary>编排终极: 螺旋俯冲 → 必接双掠袭冲锋 (固定序列, 非 RNG; 冲锋套用锁定读线+硬刹配方)。</summary>
        private void UpdateEnrageUltimate() {
            NPC.dontTakeDamage = false;

            if (StateData == 0f) {
                // —— 子拍0: 螺旋俯冲 ——
                float t = SubTimer / 160f;
                float radius = MathHelper.Lerp(620f, 150f, t);
                float angularSpeed = MathHelper.Lerp(0.045f, 0.13f, t);
                NPC.localAI[0] += angularSpeed;
                Vector2 orbit = Target.Center + NPC.localAI[0].ToRotationVector2() * radius;
                FlyToward(orbit, 1.2f, 28f);

                if (SubTimer % 9 == 0 && SubTimer > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 5.5f,
                            ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.5f);
                    }
                }

                if (SubTimer > 160) { StateData = 1f; SubTimer = 0; NPC.netUpdate = true; }
            }
            else {
                // —— 子拍1: 必接双掠袭冲锋 (更快更狠, 同一读法语言) ——
                const int cycleLen = 84;
                const int charges = 2;
                int idx = (int)(SubTimer / cycleLen);
                if (idx >= charges) {
                    BeginRecovery();
                    return;
                }
                RunRakingCycle((int)SubTimer % cycleLen, idx, 52f);
            }
        }

        #endregion

        #region 死亡「归墟」

        /// <summary>拦截真死亡: 先播 300f 归墟演出, 结束后才结算 (掉落/旗标不变)。副本龙同步归墟但不掉落。</summary>
        public override bool CheckDead() {
            if (!deathFinished) {
                if (State != AIState.Death)
                    BeginDeath();
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.netUpdate = true;
                return false;
            }
            if (IsTwin) {
                NPC.active = false;
                return false;
            }
            return true;
        }

        private void BeginDeath() {
            TransitionTo(AIState.Death);
            GhostLevel = MathF.Max(GhostLevel, 0.2f);
            ClearBossProjectiles();
            // 主龙先死 → 副本龙同步归墟 (双魂同散)
            if (!IsTwin && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC t = GetTwin();
                if (t != null && t.ModNPC is AncestralDragonSoulHead th && th.State != AIState.Death) {
                    th.State = AIState.Death;
                    th.StateTimer = 0;
                    th.SubTimer = 0;
                    th.StateData = 0;
                    t.dontTakeDamage = true;
                    t.netUpdate = true;
                }
            }
        }

        /// <summary>
        /// 归墟脚本: 梯级刹车昂首(0-40) → 尾→头星散波+渐升玻璃音+天幕熄灭(40-240) →
        /// 全静默+辉光坍缩(240-270) → 终爆(271) → 真死亡结算(272)。
        /// </summary>
        private void UpdateDeath() {
            if (StateTimer <= 40f) {
                TieredBrake();
                NPC.rotation = Utils.AngleLerp(NPC.rotation, -MathHelper.PiOver2, 0.03f); // 头颅缓缓昂向天空
                if ((int)StateTimer == 10 && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.9f, Volume = 1.1f }, NPC.Center);
            }
            else {
                NPC.velocity *= 0.97f;
                NPC.velocity.Y -= 0.012f; // 微微上升 — 魂归星海
            }

            // 头颅是最后的残核: 终爆前留一缕闪烁残影, 不提前散尽
            float headDissolve = DeathDissolveFor(0);
            if (StateTimer < 262f)
                headDissolve = MathF.Min(headDissolve, 0.82f);
            deathDissolve = headDissolve;

            // 星散段 (40-240): 渐升玻璃音 + 渐涨低鸣震屏 + 越过消散阈值的体节爆星尘
            if (Main.netMode != NetmodeID.Server && StateTimer > 40 && StateTimer < 240) {
                float progress = (StateTimer - 40f) / 200f;
                if ((int)StateTimer % 16 == 0) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.4f + progress * 0.9f, Volume = 0.6f }, NPC.Center);
                }
                if ((int)StateTimer % 30 == 0)
                    ACMUtils.AddScreenShake(1f + progress * 3f);
                SpawnDeathWaveDust();
            }

            // 240-270: 全静默 (爆前先安静、先变小 — 由绘制层辉光坍缩配合)

            if ((int)StateTimer == 271) {
                // 终爆
                transientBloom = 1.5f;
                pendingWhiteFlash = IsTwin ? 0.35f : 0.85f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.5f, Pitch = -0.3f }, NPC.Center);
                    if (!IsTwin) {
                        ACMUtils.AddScreenShake(16f);
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 18f, 9f, 60, 2400f, "AncestralDeath"));
                        AncestralDragonSky.TriggerFlash(1.5f);
                    }
                    for (int i = 0; i < 120; i++) {
                        float angle = MathHelper.TwoPi * i / 120f;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 20f);
                        int dustType = Main.rand.Next(3) switch {
                            0 => DustID.Cloud,
                            1 => DustID.WhiteTorch,
                            _ => DustID.Clentaminator_Cyan
                        };
                        int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 100, Color.White, 3.2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer >= 272 && Main.netMode != NetmodeID.MultiplayerClient) {
                deathFinished = true;
                NPC.life = 0;
                NPC.checkDead(); // → CheckDead 放行 → 真死亡 (OnKill/掉落)
            }
        }

        /// <summary>星散波扫过的体节爆出小簇星尘 (纯本地视觉)。</summary>
        private void SpawnDeathWaveDust() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.realLife != NPC.whoAmI || n.whoAmI == NPC.whoAmI) continue;
                if (n.ModNPC is not AncestralDragonSoul seg) continue;
                float d = DeathDissolveFor(seg.segmentIndex);
                if (d > 0.42f && d < 0.52f && Main.rand.NextBool(2)) {
                    for (int j = 0; j < 3; j++) {
                        Vector2 vel = Main.rand.NextVector2Circular(3f, 3f) + new Vector2(0, -1.5f);
                        int dust = Dust.NewDust(n.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan,
                            vel.X, vel.Y, 120, Color.White, 1.7f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
        }

        #endregion

        #region 表现层

        // 星尘龙体着色器 (Boss 专属, 静态缓存; 不注册进 ACMShaders)
        private static Asset<Effect> soulBodyRef;

        private static Effect GetSoulBodyEffect() {
            soulBodyRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/AncestralSoulBody",
                AssetRequestMode.ImmediateLoad);
            return soulBodyRef?.Value;
        }

        /// <summary>每帧发布太初屏幕演出标量 (仅主龙, 纯本地视觉) 并平滑 GenericWarp 强度与天幕节拍。</summary>
        private void PublishScreenFx() {
            if (Main.dedServ || IsTwin) return;

            float tint = 0f, runic = 0f;
            switch (State) {
                case AIState.EnrageTransition: tint = 0.9f; break;
                case AIState.EnrageDaoField:
                    tint = 0.7f;
                    runic = StateData == 0f ? 1f : 0.25f;
                    break;
                case AIState.EnrageYinYang: tint = 0.6f; break;
                case AIState.EnrageUltimate: tint = 0.6f; break;
                case AIState.Recovery: tint = didEnrage ? 0.5f : 0f; break;
                case AIState.Death: tint = 0.45f + MathHelper.Clamp(StateTimer / 240f, 0f, 1f) * 0.3f; break;
                default: tint = didSplit ? 0.2f : 0f; break;
            }

            float warpTarget = 0f;
            if (State == AIState.EnrageTransition)
                warpTarget = 0.3f + MathHelper.Clamp(StateTimer / 210f, 0f, 1f) * 0.35f;
            else if (State == AIState.Death)
                warpTarget = 0.25f;
            else if (didEnrage)
                warpTarget = 0.15f;
            warpIntensity = MathHelper.Lerp(warpIntensity, warpTarget, 0.06f);

            // 天幕叙事节拍: 入场星流汇聚 / 分裂后星座龙巡游 / 死亡熄灭
            float converge = State == AIState.Intro
                ? Utils.GetLerpValue(0f, 60f, StateTimer, true) * Utils.GetLerpValue(266f, 210f, StateTimer, true)
                : 0f;
            float swim = didSplit && !merged && State != AIState.Death ? 0.8f : 0f;
            float deathDim = State == AIState.Death ? MathHelper.Clamp((StateTimer - 30f) / 210f, 0f, 1f) : 0f;
            AncestralDragonSky.PublishBeats(converge, swim, deathDim);

            AncestralSoulScreenSystem.Publish(NPC.Center, tint, runic, transientBloom, globalTime, pendingWhiteFlash);
            pendingWhiteFlash = 0f;
        }

        // ===== 整龙合批绘制 =====

        private static readonly List<AncestralDragonSoul> drawChain = new(96);
        private static readonly List<Vector2> spinePoints = new(40);
        private static readonly Comparison<AncestralDragonSoul> tailFirst =
            (a, b) => b.segmentIndex.CompareTo(a.segmentIndex);

        private void CollectDrawChain() {
            drawChain.Clear();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.whoAmI == NPC.whoAmI || n.realLife != NPC.whoAmI) continue;
                if (n.ModNPC is AncestralDragonSoul seg)
                    drawChain.Add(seg);
            }
            drawChain.Sort(tailFirst); // 尾在前, 头压最上
            drawChain.Add(this);
        }

        private static bool OnScreen(Vector2 worldPos) {
            Vector2 sp = worldPos - Main.screenPosition;
            return sp.X > -350f && sp.X < Main.screenWidth + 350f && sp.Y > -350f && sp.Y < Main.screenHeight + 350f;
        }

        private static Color SegmentLightColor(NPC n) {
            Color lc = Lighting.GetColor((int)(n.Center.X / 16f), (int)(n.Center.Y / 16f));
            Color mist = Color.Lerp(lc, new Color(235, 242, 255), 0.5f);
            return Color.Lerp(mist, Color.White, 0.3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPC.IsABestiaryIconDummy) {
                Texture2D bTex = TextureAssets.Npc[Type].Value;
                spriteBatch.Draw(bTex, NPC.Center - screenPos, null, Color.White, NPC.rotation,
                    bTex.Size() / 2f, NPC.scale, SpriteEffects.None, 0f);
                return false;
            }

            // 冲刺/穿行蓄力红线预警 (致命路径, 唯一红)
            if (showChargeTele && !Main.dedServ) {
                ACMShaders.DrawBeam(NPC.Center, chargeTeleTarget, 6f,
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, 0.55f);
            }

            CollectDrawChain();
            DrawSpineRibbon(spriteBatch);
            DrawDragonBatch(spriteBatch, screenPos);
            return false;
        }

        /// <summary>脊线流光: 沿身体每 4 节取样的 BeamGrad 呼吸细带 (幽魂的"魂脉")。</summary>
        private void DrawSpineRibbon(SpriteBatch sb) {
            if (drawChain.Count < 10 || Main.dedServ)
                return;
            Effect fx = ACMShaders.BeamGrad;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            float deathFade = State == AIState.Death ? MathHelper.Clamp(1f - (StateTimer - 40f) / 190f, 0f, 1f) : 1f;
            float intensity = (0.4f + transientBloom * 0.5f) * (1f - GhostLevel * 0.45f) * deathFade;
            if (intensity <= 0.02f)
                return;

            // drawChain 尾在前、头在最后 → 逆序采样得到 头→尾 中心线
            spinePoints.Clear();
            for (int i = drawChain.Count - 1; i >= 0; i -= 4) {
                Vector2 p = drawChain[i].NPC.Center - Main.screenPosition;
                spinePoints.Add(p);
            }
            if (spinePoints.Count < 3)
                return;

            float breath = 0.75f + 0.25f * MathF.Sin(globalTime * 3.1f);
            var verts = ACMUtils.BuildRibbonStrip(spinePoints.ToArray(),
                t => (9f - t * 5f) * breath * MergeScaleMul(),
                _ => Color.White, 0f, 2);
            if (verts.Length < 4)
                return;

            Color core = merged ? new Color(255, 233, 170, 160) : new Color(240, 250, 255, 150);
            Color edge = merged ? new Color(200, 150, 60, 0) : new Color(110, 180, 235, 0);
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uCoreGlow"]?.SetValue(0.55f);
            fx.Parameters["uFlowSpeed"]?.SetValue(1.8f);
            fx.Parameters["uFlowScale"]?.SetValue(2.4f);
            fx.Parameters["uCoreSharp"]?.SetValue(2.0f);
            fx.Parameters["uUseTexture"]?.SetValue(0f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 整龙两段合批: ① Immediate+星尘着色器逐节主体 (溶解/虚化/流光逐节参数);
        /// ② Additive 光晕层 (速度门控残影 + 单层节晕 + 龙眼 + 尾尖)。
        /// </summary>
        private void DrawDragonBatch(SpriteBatch sb, Vector2 screenPos) {
            Effect fx = GetSoulBodyEffect();
            Texture2D noise = ACMShaders.NoiseTexture;
            float mergeGoldVal = merged ? 1f : 0f;
            Color edgeColor = merged ? new Color(255, 214, 130) : new Color(150, 220, 255);
            Color flowColor = merged ? new Color(255, 226, 160) : new Color(210, 240, 255);

            // —— ① 主体批 (Immediate + 星尘着色器: 逐节改参数, 每次 Draw 立即以当前参数冲刷) ——
            bool useShader = fx != null && noise != null;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, useShader ? fx : null,
                Main.GameViewMatrix.TransformationMatrix);

            if (useShader) {
                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                fx.Parameters["uTime"]?.SetValue(globalTime);
                fx.Parameters["uNoiseScale"]?.SetValue(1.8f);
                fx.Parameters["uMergeGold"]?.SetValue(mergeGoldVal);
                fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(edgeColor.ToVector3(), 0.9f));
                fx.Parameters["uFlowColor"]?.SetValue(new Vector4(flowColor.ToVector3(), 0.8f));
            }

            for (int i = 0; i < drawChain.Count; i++) {
                AncestralDragonSoul seg = drawChain[i];
                NPC n = seg.NPC;
                float dissolve = seg.DissolveLevel;
                if (dissolve >= 0.999f || !OnScreen(n.Center))
                    continue;

                Texture2D tex = TextureAssets.Npc[n.type].Value;
                float pulse = (1f + MathF.Sin(soulPulsePhase + seg.segmentIndex * 0.3f) * 0.07f) * MergeScaleMul();
                SpriteEffects flip = n.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                Color col = SegmentLightColor(n) * n.Opacity;

                if (useShader) {
                    fx.Parameters["uDissolve"]?.SetValue(dissolve);
                    fx.Parameters["uGhost"]?.SetValue(seg.GhostLevel);
                    fx.Parameters["uFlowGlow"]?.SetValue(FlowGlowFor(seg.segmentIndex));
                    fx.Parameters["uSeed"]?.SetValue(seg.segmentIndex * 0.137f);
                }

                sb.Draw(tex, n.Center - screenPos, null, col, n.rotation + seg.DrawRotationOffset,
                    tex.Size() / 2f, n.scale * pulse, flip, 0f);
            }

            // —— ② 光晕批 (加性) ——
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 速度门控残影 (仅冲刺时显现 — 修饰跟着速度走)
            float speedGate = Utils.GetLerpValue(16f, 34f, NPC.velocity.Length(), true);
            if (speedGate > 0.05f) {
                Texture2D headTex = TextureAssets.Npc[Type].Value;
                Vector2 hOrigin = headTex.Size() / 2f;
                for (int i = 1; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    float prog = 1f - (float)i / NPC.oldPos.Length;
                    Color tcol = Color.Lerp(Color.White, new Color(160, 215, 255), 1f - prog) * (prog * 0.35f * speedGate);
                    tcol.A = 0;
                    SpriteEffects tflip = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                    sb.Draw(headTex, NPC.oldPos[i] + NPC.Size / 2f - screenPos, null, tcol,
                        NPC.oldRot[i], hOrigin, NPC.scale * prog * 0.95f, tflip, 0f);
                }
            }

            float deathCollapse = State == AIState.Death && StateTimer > 240f
                ? MathHelper.Clamp(1f - (StateTimer - 240f) / 30f * 0.6f, 0.4f, 1f)
                : 1f;

            for (int i = 0; i < drawChain.Count; i++) {
                AncestralDragonSoul seg = drawChain[i];
                NPC n = seg.NPC;
                float dissolve = seg.DissolveLevel;
                if (dissolve >= 0.999f || !OnScreen(n.Center))
                    continue;

                Texture2D tex = TextureAssets.Npc[n.type].Value;
                float glowAlpha = (0.15f + FlowGlowFor(seg.segmentIndex) * 0.22f + transientBloom * 0.25f)
                    * (1f - seg.GhostLevel * 0.5f) * (1f - dissolve) * mistAlpha * deathCollapse;
                if (glowAlpha <= 0.01f)
                    continue;
                Color glowCol = (seg.segmentIndex % 2 == 0 ? new Color(255, 255, 255) : new Color(215, 236, 255)) * glowAlpha;
                glowCol.A = 0;
                float pulse = (1.12f + MathF.Sin(soulPulsePhase * 1.6f + seg.segmentIndex * 0.25f) * 0.04f) * MergeScaleMul();
                SpriteEffects flip = n.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                sb.Draw(tex, n.Center - screenPos, null, glowCol, n.rotation + seg.DrawRotationOffset,
                    tex.Size() / 2f, n.scale * pulse, flip, 0f);

                // 尾尖光点
                if (seg.NPCWormType == WormType.Tail && ACMAsset.LightShot != null) {
                    float tipPulse = (0.6f + MathF.Sin(globalTime * 3f) * 0.3f) * deathCollapse;
                    Color tipColor = new Color(220, 240, 255) * tipPulse * 0.5f * (1f - dissolve);
                    tipColor.A = 0;
                    Vector2 tipPos = n.Center - n.rotation.ToRotationVector2() * 25f - screenPos;
                    sb.Draw(ACMAsset.LightShot, tipPos, null, tipColor, 0f,
                        ACMAsset.LightShot.Size() / 2f, 0.5f * tipPulse, SpriteEffects.None, 0f);
                }
            }

            DrawDragonEyes(sb, screenPos, deathCollapse);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>太初雾全屏扭曲 (GenericWarp · fog 主题), 走单一全屏后处理名额; 强度&lt;0.01 早退。</summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || IsTwin || warpIntensity <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(warpIntensity, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.95f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.1f);
            fx.Parameters["uChroma"]?.SetValue(0.35f);
            fx.Parameters["uRadialPull"]?.SetValue(0.2f);
            fx.Parameters["uMode"]?.SetValue(2f); // fog
            fx.Parameters["uTint"]?.SetValue(new Vector4(new Color(196, 214, 232).ToVector3(), 0.45f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos, float collapse) {
            if (ACMAsset.LightShot == null) return;

            // 入场凝视段龙眼渐亮 (静止中的威压)
            float introFactor = State == AIState.Intro ? Utils.GetLerpValue(190f, 245f, StateTimer, true) : 1f;
            if (introFactor <= 0.01f) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 25f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = (0.8f + MathF.Sin(globalTime * 4f) * 0.2f) * collapse;
            Color eyeColor = (didEnrage ? new Color(255, 200, 220) : new Color(255, 255, 255)) * eyePulse * 0.6f * introFactor;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.6f * eyePulse, SpriteEffects.None, 0f);
        }

        #endregion

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<ArchosaurFerrara>(),
                ModContent.ItemType<ArchosaurBow>(),
                ModContent.ItemType<ArchosaurStaff>()
            ));
        }

        public override void BossLoot(ref string name, ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void OnKill() {
            base.OnKill();

            if (!IsTwin && partnerIndex >= 0 && partnerIndex < Main.maxNPCs) {
                NPC twin = Main.npc[partnerIndex];
                if (twin.active && twin.type == Type) {
                    twin.life = 0;
                    twin.checkDead();
                    twin.active = false;
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, number: partnerIndex);
                    }
                }
            }

            // 终爆已在归墟演出播毕, 这里只留轻量余韵
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 9);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2.2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.2f, Pitch = 0.2f }, NPC.Center);
        }
    }
}

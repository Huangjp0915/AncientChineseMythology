using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙真身·头部 — 状态机骨架与调度中心 (V3 重做)。
    /// 编排文法: 盘(蓄势) → 击(爆发) → 回(收招), 招间必经 Glide 连接段;
    /// 攻击选择为手工编排循环 (压制招与场控招交替), 三大演出节拍齐备。
    /// 一阶段(100%~60%) 苍龙出海 / 二阶段(60%~25%) 雷霆震怒 / 三阶段(25%~0%) 天威降世。
    /// </summary>
    [AutoloadBossHead]
    public partial class AzureDragonHead : AzureDragon
    {
        public override WormType NPCWormType => WormType.Head;

        #region 状态枚举与阈值

        public enum AIState : int
        {
            Intro = 0,
            /// <summary>游弋连接段 — 全阶段共用, 零弹幕保底喘息, 结束时选取下一招。</summary>
            Glide,
            // 一阶段 苍龙出海
            P1_CoilPierce,
            P1_BreathSweep,
            P1_ThunderRods,
            Transition2,
            // 二阶段 雷霆震怒
            P2_ChainPierce,
            P2_LightningLattice,
            P2_StormRing,
            P2_BodyDischarge,
            Transition3,
            // 三阶段 天威降世
            P3_Tribunal,
            P3_SkyDive,
            P3_FurySpiral,
            DeathCinematic,
        }

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.25f;

        #endregion

        #region 同步状态 (ai[] + SendExtraAI)

        public AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float StateTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool deathCinematicDone;

        // 冲刺/俯冲编排
        private int pierceCount;
        private Vector2 chargeDirection = Vector2.UnitX;
        private Vector2 chargeTarget;
        private int diveCount;

        // 盘旋
        private float orbitAngle;
        private int orbitDir = 1;

        // 攻击循环
        private int attackIndexP1;
        private int attackIndexP2;
        private int attackIndexP3;
        private int latticeAxisCounter;

        /// <summary>审判庭竞技场中心 (服务器锁定并同步)。</summary>
        public Vector2 ArenaCenter { get; private set; }
        /// <summary>审判庭竞技场半径 (世界像素)。</summary>
        public float ArenaRadius { get; private set; } = 900f;

        #endregion

        #region 演出状态 (纯客户端视觉, 由已同步状态确定性派生)

        /// <summary>当前活跃苍龙头 whoAmI (StormSystem 单实例查询)。</summary>
        public static int ActiveHead = -1;

        /// <summary>风暴压暗强度 0~1 (按阶段渐变)。</summary>
        public float StormVisual { get; private set; }
        /// <summary>天闪 0~1 (宣告节拍, 衰减)。</summary>
        public float SkyFlash { get; private set; }
        /// <summary>审判庭网格地纹强度 0~1。</summary>
        public float TribunalVisual { get; private set; }
        /// <summary>雨霁暖光 0~1 (死亡演出收尾)。</summary>
        public float DawnVisual { get; private set; }
        /// <summary>整体可见度 (腾云隐身)。</summary>
        public float VisualFade { get; private set; } = 1f;
        /// <summary>假 Z 缩放 (入场云层深处 / 破云俯冲)。</summary>
        public float VisualScale { get; private set; } = 1f;
        /// <summary>是否处于审判庭 set-piece。</summary>
        public bool TribunalActive => State == AIState.P3_Tribunal;
        /// <summary>当前风域水平推力方向 -1~1 (由同步 globalTime 确定性派生)。</summary>
        public float WindDir => MathF.Sin(globalTime * 0.9f);

        // 冲刺增辉 (速度门控 dressing) 与条带过曝
        private float auraIntensity;
        private float dashGlow;
        private float strikeBoost;
        // 蛇形波动与鞭波次级运动
        private float undulationPhase;
        private float undulationAmp = 10f;
        private float undulationAmpTarget = 10f;
        private float whipFront = 999f;
        private float whipImpulse;
        // 龙身放电电荷扫描 (ribbon uChargePos; <0 关闭)
        private float chargeSweep = -1f;
        private float chargeGlow;
        private int dischargeWarnOffset = -1;
        private float dischargeWarn01;
        // 落点泛光与天眼扫描线 (客户端表现)
        private float impactFlash;
        private float clientScanX;

        #endregion

        #region 基础重写

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.ShouldBeCountedAsBoss[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.npcSlots = 30f;
            Music = MusicID.LunarBoss;
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
            State = AIState.Intro;
            StateTimer = 0;
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitDir = Main.rand.NextBool() ? 1 : -1;

            // 入场自云层深处显形: 出生即置于玩家高空侧上方
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int closest = Player.FindClosest(NPC.position, NPC.width, NPC.height);
                if (closest >= 0 && Main.player[closest].active)
                    NPC.Center = Main.player[closest].Center + new Vector2(orbitDir * 520f, -1050f);
                NPC.netUpdate = true;
            }
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AzureDragonBody>();
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(globalTime);
            BitsByte flags = new();
            flags[0] = didPhase2Transition;
            flags[1] = didPhase3Transition;
            flags[2] = deathCinematicDone;
            writer.Write(flags);
            writer.Write((sbyte)orbitDir);
            writer.Write(pierceCount);
            writer.Write(diveCount);
            writer.Write(attackIndexP1);
            writer.Write(attackIndexP2);
            writer.Write(attackIndexP3);
            writer.Write(latticeAxisCounter);
            writer.Write(orbitAngle);
            writer.WriteVector2(chargeDirection);
            writer.WriteVector2(chargeTarget);
            writer.WriteVector2(ArenaCenter);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            globalTime = reader.ReadSingle();
            BitsByte flags = reader.ReadByte();
            didPhase2Transition = flags[0];
            didPhase3Transition = flags[1];
            deathCinematicDone = flags[2];
            orbitDir = reader.ReadSByte();
            pierceCount = reader.ReadInt32();
            diveCount = reader.ReadInt32();
            attackIndexP1 = reader.ReadInt32();
            attackIndexP2 = reader.ReadInt32();
            attackIndexP3 = reader.ReadInt32();
            latticeAxisCounter = reader.ReadInt32();
            orbitAngle = reader.ReadSingle();
            chargeDirection = reader.ReadVector2();
            chargeTarget = reader.ReadVector2();
            ArenaCenter = reader.ReadVector2();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        /// <summary>头部接触伤害速度门控: 盘旋贴脸不啃人, 只有冲刺/俯冲的高速时刻全额结算 (公平阀门)。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if (CinematicNoContact)
                return false;
            return NPC.velocity.Length() > 11f;
        }

        #endregion

        #region 死亡演出拦截

        public override bool CheckDead() {
            if (deathCinematicDone)
                return true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;
            if (State != AIState.DeathCinematic) {
                ClearOwnProjectiles();
                TransitionTo(AIState.DeathCinematic);
            }
            return false;
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            base.AI();
            globalTime += 1f / 60f;
            ActiveHead = NPC.whoAmI;

            if (State != AIState.DeathCinematic) {
                NPC.TargetClosest();
                Player t = Target;
                if (!t.active || t.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.velocity.X *= 0.97f;
                    NPC.EncourageDespawn(30);
                    UpdateVisuals();
                    return;
                }
            }

            Player target = Target;

            // 集中管理无敌帧: 演出节拍不受伤 (入场尾段提前解除)
            NPC.dontTakeDamage = State switch {
                AIState.Intro => StateTimer < 200,
                AIState.Transition2 or AIState.Transition3 or AIState.DeathCinematic => true,
                _ => false,
            };

            CheckPhaseTransition();
            UpdateVisuals();

            StateTimer++;
            AttackTimer++;

            switch (State) {
                case AIState.Intro: RunIntro(target); break;
                case AIState.Glide: RunGlide(target); break;
                case AIState.P1_CoilPierce: RunCoilPierce(target, chained: false); break;
                case AIState.P1_BreathSweep: RunBreathSweep(target); break;
                case AIState.P1_ThunderRods: RunThunderRods(target); break;
                case AIState.Transition2: RunTransition2(target); break;
                case AIState.P2_ChainPierce: RunCoilPierce(target, chained: true); break;
                case AIState.P2_LightningLattice: RunLightningLattice(target); break;
                case AIState.P2_StormRing: RunStormRing(target); break;
                case AIState.P2_BodyDischarge: RunBodyDischarge(target); break;
                case AIState.Transition3: RunTransition3(target); break;
                case AIState.P3_Tribunal: RunTribunal(target); break;
                case AIState.P3_SkyDive: RunSkyDive(target); break;
                case AIState.P3_FurySpiral: RunFurySpiral(target); break;
                case AIState.DeathCinematic: RunDeathCinematic(); break;
            }

            // 头部朝向: 低速悬停时不抖
            if (NPC.velocity.LengthSquared() > 0.64f)
                NPC.rotation = NPC.velocity.ToRotation();

            float lightPulse = 0.8f + 0.3f * MathF.Sin(globalTime * 3f);
            Lighting.AddLight(NPC.Center, DragonCyan.ToVector3() * auraIntensity * lightPulse);
        }

        #endregion

        #region 阶段转换与攻击选择

        private void CheckPhaseTransition() {
            if (State is AIState.Intro or AIState.DeathCinematic or AIState.Transition2 or AIState.Transition3)
                return;

            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                TransitionTo(AIState.Transition2);
                return;
            }

            if (!didPhase3Transition && IsPhase3) {
                didPhase2Transition = true;
                didPhase3Transition = true;
                TransitionTo(AIState.Transition3);
            }
        }

        private void TransitionTo(AIState newState) {
            State = newState;
            StateTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        // 手工编排循环 (PACING §2): 压制招(穿刺)与场控招严格交替, 不会连续两次同招
        private static readonly AIState[] CycleP1 = [
            AIState.P1_CoilPierce, AIState.P1_BreathSweep, AIState.P1_CoilPierce, AIState.P1_ThunderRods,
        ];
        private static readonly AIState[] CycleP2 = [
            AIState.P2_ChainPierce, AIState.P2_LightningLattice, AIState.P2_StormRing,
            AIState.P2_ChainPierce, AIState.P2_BodyDischarge,
        ];
        private static readonly AIState[] CycleP3 = [
            AIState.P3_Tribunal, AIState.P3_SkyDive, AIState.P3_FurySpiral, AIState.P3_SkyDive,
        ];

        private AIState PickNextAttack() {
            if (IsPhase3) {
                AIState s3 = CycleP3[attackIndexP3 % CycleP3.Length];
                attackIndexP3++;
                return s3;
            }
            if (IsPhase2) {
                AIState s2 = CycleP2[attackIndexP2 % CycleP2.Length];
                attackIndexP2++;
                return s2;
            }
            AIState s1 = CycleP1[attackIndexP1 % CycleP1.Length];
            attackIndexP1++;
            return s1;
        }

        #endregion

        #region 演出状态更新

        private void UpdateVisuals() {
            // 风暴压暗: 入场滚入 → P1 薄云 → P2 中 → P3 重 → 死亡退潮
            float stormTarget;
            if (State == AIState.DeathCinematic)
                stormTarget = StateTimer < 190 ? 0.15f : 0f;
            else if (State == AIState.Intro)
                stormTarget = MathHelper.Lerp(0.5f, 0.2f, MathHelper.Clamp(StateTimer / 200f, 0f, 1f));
            else if (IsPhase3)
                stormTarget = 0.72f;
            else if (IsPhase2)
                stormTarget = 0.45f;
            else
                stormTarget = 0.18f;
            StormVisual = MathHelper.Lerp(StormVisual, stormTarget, 0.02f);

            TribunalVisual = MathHelper.Lerp(TribunalVisual, TribunalActive ? 1f : 0f, 0.05f);

            SkyFlash *= 0.9f;
            if (SkyFlash < 0.01f)
                SkyFlash = 0f;

            // 雨霁暖光: 只在死亡演出后半程升起
            float dawnTarget = State == AIState.DeathCinematic && StateTimer >= 190 ? 1f : 0f;
            DawnVisual = MathHelper.Lerp(DawnVisual, dawnTarget, 0.03f);

            float targetAura = IsPhase3 ? 1.5f : (IsPhase2 ? 1.0f : 0.6f);
            if (State == AIState.DeathCinematic)
                targetAura = MathHelper.Lerp(1.5f, 0.2f, MathHelper.Clamp(StateTimer / 214f, 0f, 1f));
            auraIntensity = MathHelper.Lerp(auraIntensity, targetAura, 0.02f);

            // 冲刺增辉: 速度门控, 快才亮 (常开的修饰是噪声)
            dashGlow *= 0.92f;
            if (NPC.velocity.LengthSquared() > 45f * 45f)
                dashGlow = 1f;
            strikeBoost = MathHelper.Lerp(strikeBoost, dashGlow, 0.3f);

            // 蛇形波动: 幅度随状态起伏, 冲刺时身体拉直 (直=快)
            undulationAmp = MathHelper.Lerp(undulationAmp, undulationAmpTarget, 0.08f);
            float speed = NPC.velocity.Length();
            undulationPhase += 0.1f + speed * 0.003f;

            // 鞭波前沿沿身体传播
            whipFront += 1.6f;
            whipImpulse *= 0.94f;

            impactFlash *= 0.88f;
            dischargeWarn01 = MathHelper.Lerp(dischargeWarn01, 0f, 0.1f);

            // 腾云隐身只属于入场/俯冲状态; 其余状态 (含被死亡演出打断时) 自动复原可见度
            if (State is not AIState.P3_SkyDive and not AIState.Intro) {
                VisualFade = MathHelper.Lerp(VisualFade, 1f, 0.12f);
                VisualScale = MathHelper.Lerp(VisualScale, 1f, 0.12f);
            }
        }

        /// <summary>向龙身注入一次鞭波脉冲 (从颈部向尾传播的次级运动)。</summary>
        private void InjectWhip(float strength) {
            whipFront = 0f;
            whipImpulse = strength;
        }

        /// <summary>体节可视化偏移: 蛇形波动 + 鞭波行波 (纯视觉, ≤~20px, 命中盒不动)。</summary>
        public Vector2 SegmentVisualOffset(NPC segment, int summonIndex) {
            Vector2 perp = segment.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float und = MathF.Sin(undulationPhase - summonIndex * 0.42f) * undulationAmp;
            float whip = 0f;
            if (whipImpulse > 0.02f) {
                float d = summonIndex - whipFront;
                whip = MathF.Exp(-d * d / 18f) * whipImpulse * 26f * MathF.Sin(summonIndex * 0.9f);
            }
            return perp * (und + whip);
        }

        #endregion

        #region 辅助

        /// <summary>速度制导转向 (蠕虫不做位置 lerp, 永远靠速度移动)。</summary>
        private void SteerToward(Vector2 goal, float maxSpeed, float accel) {
            Vector2 desired = (goal - NPC.Center).SafeNormalize(Vector2.UnitX) * maxSpeed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, accel);
        }

        /// <summary>沿行进方向的垂直蛇形摆动 (真实运动层, 与视觉波动叠加)。</summary>
        private void SerpentineSway(float strength) {
            Vector2 dir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            NPC.velocity += dir.RotatedBy(MathHelper.PiOver2) * (MathF.Sin(globalTime * 4.6f) * strength);
        }

        /// <summary>清除本 Boss 的全部敌意弹幕 (换阶段/死亡公平阀门; 不动其他 Boss 的弹)。</summary>
        private void ClearOwnProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int bolt = ModContent.ProjectileType<AzureBolt>();
            int rod = ModContent.ProjectileType<AzureThunderRod>();
            int orb = ModContent.ProjectileType<AzureStormOrb>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == bolt || p.type == rod || p.type == orb))
                    p.Kill();
            }
        }

        /// <summary>发出雾涡请求 (客户端演出)。</summary>
        private void EmitMist(Vector2 center, float radiusPx, float intensity, float swirl = 2.2f, Color? color = null) {
            if (Main.dedServ)
                return;
            AzureDragonStormSystem.QueueMist(center, radiusPx, intensity, swirl,
                color ?? new Color(120, 170, 220));
        }

        #endregion
    }
}

using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙头部 - 状态机与AI调度中心
    /// 一阶段(100%~60%)：苍龙出海 - 基础盘旋+龙息+雷球
    /// 二阶段(60%~25%)：雷霆震怒 - 高速冲刺+闪电矩阵+龙卷风暴
    /// 三阶段(25%~0%)：天威降世 - 终极雷暴+连续冲刺+全屏闪电
    /// </summary>
    [AutoloadBossHead]
    public partial class AzureDragonHead : AzureDragon
    {
        public override WormType NPCWormType => WormType.Head;

        #region 状态枚举

        public enum AIState : int
        {
            Intro = 0,
            // 一阶段
            Phase1_Orbit,
            Phase1_DragonBreath,
            Phase1_ThunderOrbs,
            Phase1_Charge,
            // 阶段转换
            PhaseTransition_2,
            // 二阶段
            Phase2_StormChase,
            Phase2_LightningMatrix,
            Phase2_TornadoSweep,
            Phase2_RapidCharge,
            // 阶段转换
            PhaseTransition_3,
            // 三阶段
            Phase3_ThunderJudgment,
            Phase3_CelestialFury,
            Phase3_DragonAscent,
        }

        #endregion

        #region AI属性

        public AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float StateTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        public bool IsPhase2 => NPC.life < NPC.lifeMax * 0.6f;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * 0.25f;

        // 私有状态
        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 冲刺控制
        private Vector2 chargeTarget;
        private Vector2 chargeDirection;
        private int chargeCount;
        private int maxCharges;

        // 移动目标
        private float orbitAngle;
        private float orbitSpeed;

        // 视觉
        private float auraIntensity;
        private float introProgress;

        // 攻击选择
        private int phase1AttackIndex;
        private int phase2AttackIndex;
        private int phase3AttackIndex;

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
            orbitAngle = 0;
            orbitSpeed = 0.02f;
            auraIntensity = 0f;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AzureDragonBody>();
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write((int)State);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(chargeCount);
            writer.Write(orbitAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            State = (AIState)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            chargeCount = reader.ReadInt32();
            orbitAngle = reader.ReadSingle();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        #endregion

        #region AI主循环

        public override void AI() {
            base.AI();
            globalTime += 1f / 60f;

            NPC.TargetClosest();
            Player target = Target;

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.8f;
                NPC.EncourageDespawn(30);
                return;
            }

            // 检查阶段转换
            CheckPhaseTransition();

            StateTimer++;
            AttackTimer++;

            // 更新光环
            float targetAura = IsPhase3 ? 1.5f : (IsPhase2 ? 1.0f : 0.6f);
            auraIntensity = MathHelper.Lerp(auraIntensity, targetAura, 0.02f);

            // 状态机调度
            switch (State) {
                case AIState.Intro:
                    RunIntro(target);
                    break;
                // 一阶段
                case AIState.Phase1_Orbit:
                    RunPhase1Orbit(target);
                    break;
                case AIState.Phase1_DragonBreath:
                    RunPhase1DragonBreath(target);
                    break;
                case AIState.Phase1_ThunderOrbs:
                    RunPhase1ThunderOrbs(target);
                    break;
                case AIState.Phase1_Charge:
                    RunPhase1Charge(target);
                    break;
                // 阶段转换
                case AIState.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                // 二阶段
                case AIState.Phase2_StormChase:
                    RunPhase2StormChase(target);
                    break;
                case AIState.Phase2_LightningMatrix:
                    RunPhase2LightningMatrix(target);
                    break;
                case AIState.Phase2_TornadoSweep:
                    RunPhase2TornadoSweep(target);
                    break;
                case AIState.Phase2_RapidCharge:
                    RunPhase2RapidCharge(target);
                    break;
                // 阶段转换
                case AIState.PhaseTransition_3:
                    RunPhaseTransition3(target);
                    break;
                // 三阶段
                case AIState.Phase3_ThunderJudgment:
                    RunPhase3ThunderJudgment(target);
                    break;
                case AIState.Phase3_CelestialFury:
                    RunPhase3CelestialFury(target);
                    break;
                case AIState.Phase3_DragonAscent:
                    RunPhase3DragonAscent(target);
                    break;
            }

            // 头部朝向 - 仅存储原始速度角度，翻转在PreDraw中处理
            NPC.rotation = NPC.velocity.ToRotation();

            // 增强光照
            float lightPulse = 0.8f + 0.3f * MathF.Sin(globalTime * 3f);
            Lighting.AddLight(NPC.Center, DragonCyan.ToVector3() * auraIntensity * lightPulse);
        }

        #endregion

        #region 阶段转换

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                State != AIState.PhaseTransition_2 && State != AIState.Intro) {
                TransitionTo(AIState.PhaseTransition_2);
                didPhase2Transition = true;
            }

            if (!didPhase3Transition && IsPhase3 &&
                State != AIState.PhaseTransition_3 && State != AIState.PhaseTransition_2 &&
                State != AIState.Intro) {
                TransitionTo(AIState.PhaseTransition_3);
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(AIState newState) {
            State = newState;
            StateTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private AIState PickPhase1Attack() {
            AIState[] attacks = [
                AIState.Phase1_DragonBreath,
                AIState.Phase1_ThunderOrbs,
                AIState.Phase1_Charge,
            ];
            phase1AttackIndex = (phase1AttackIndex + 1) % attacks.Length;
            return attacks[phase1AttackIndex];
        }

        private AIState PickPhase2Attack() {
            AIState[] attacks = [
                AIState.Phase2_LightningMatrix,
                AIState.Phase2_TornadoSweep,
                AIState.Phase2_RapidCharge,
                AIState.Phase2_StormChase,
            ];
            phase2AttackIndex = (phase2AttackIndex + 1) % attacks.Length;
            return attacks[phase2AttackIndex];
        }

        private AIState PickPhase3Attack() {
            AIState[] attacks = [
                AIState.Phase3_ThunderJudgment,
                AIState.Phase3_CelestialFury,
                AIState.Phase3_DragonAscent,
            ];
            phase3AttackIndex = (phase3AttackIndex + 1) % attacks.Length;
            return attacks[phase3AttackIndex];
        }

        #endregion

        #region 辅助方法

        /// <summary>平滑盘旋向目标位置移动</summary>
        private void SmoothOrbit(Vector2 desiredPos, float inertia = 60f) {
            Vector2 toGoal = desiredPos - NPC.Center;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toGoal / 8f) / inertia;
        }

        /// <summary>快速插值移动</summary>
        private void LerpToPosition(Vector2 desiredPos, float speed = 0.08f) {
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, speed);
        }

        #endregion
    }
}

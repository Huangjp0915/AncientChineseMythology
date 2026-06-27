using System;
using System.IO;
using Terraria;
using Terraria.Audio;
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
            Phase1_ThunderOrbs,   // 已重做为「地面雷柱」可读落雷预告
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
            Phase3_ThunderTribunal,   // 招牌 set-piece: 网格化雷霆审判庭 + 风域
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

        // —— V2: 雷霆律令 (P2 规则切换) / 审判庭 (P3) / 风域 ——
        /// <summary>律令周期(秒): 每过一段在「横扫律令」与「纵贯律令」间切换。</summary>
        private const float EdictPeriodSeconds = 5f;
        /// <summary>审判庭波次上限 — 限幅后强制转入移动招式(消除"加速喷弹"反模式)。</summary>
        private const int TribunalWaveCount = 3;
        private int tribunalWave;
        private bool prevEdictHorizontal;

        // —— V2 演出/同步状态 (供 Body 与 StormSystem 读取) ——
        /// <summary>当前活跃苍龙头 whoAmI (StormSystem 单实例查询用)。</summary>
        public static int ActiveHead = -1;
        /// <summary>风暴压暗强度 0~1 (按阶段渐变)。</summary>
        public float StormVisual { get; private set; }
        /// <summary>律令切换天闪 0~1 (衰减)。</summary>
        public float EdictFlash { get; private set; }
        /// <summary>审判庭网格地纹强度 0~1。</summary>
        public float TribunalVisual { get; private set; }
        /// <summary>当前是否处于审判庭 set-piece。</summary>
        public bool TribunalActive => State == AIState.Phase3_ThunderTribunal;
        /// <summary>审判庭竞技场中心(世界)。</summary>
        public Vector2 ArenaCenter { get; private set; }
        /// <summary>审判庭竞技场半径(世界像素)。</summary>
        public float ArenaRadius { get; private set; } = 900f;
        /// <summary>当前风域方向(水平推力, -1~1, 由 globalTime 派生确定性同步)。</summary>
        public float WindDir => MathF.Sin(globalTime * 0.9f);

        /// <summary>
        /// 雷霆律令: 当前是否为「横扫律令」(仅允许水平弹道); 否则为「纵贯律令」(仅竖直/对角)。
        /// 由已同步的 globalTime 确定性派生, 全客户端一致, 无需额外网络字段。
        /// </summary>
        public bool EdictHorizontal => ((int)(globalTime / EdictPeriodSeconds)) % 2 == 0;

        /// <summary>头部正在引导大招(吐息/审判庭蓄力) — Body 据此发出节段同步雷弹。</summary>
        public bool BodyChannelActive {
            get {
                if (State == AIState.Phase1_DragonBreath && SubState >= 1)
                    return true;
                if (State == AIState.Phase3_ThunderTribunal)
                    return true;
                return false;
            }
        }

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
            ActiveHead = NPC.whoAmI;

            NPC.TargetClosest();
            Player target = Target;

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.8f;
                NPC.EncourageDespawn(30);
                return;
            }

            // V2 演出与律令更新 (纯本地视觉 + 确定性律令)
            UpdateV2Visuals();

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
                    RunPhase1ThunderRods(target);
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
                case AIState.Phase3_ThunderTribunal:
                    RunPhase3ThunderTribunal(target);
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
            // 审判庭为招牌 set-piece, 与移动招交替(避免连续静止喷弹)
            AIState[] attacks = [
                AIState.Phase3_ThunderTribunal,
                AIState.Phase3_CelestialFury,
                AIState.Phase3_DragonAscent,
            ];
            phase3AttackIndex = (phase3AttackIndex + 1) % attacks.Length;
            return attacks[phase3AttackIndex];
        }

        #endregion

        #region V2 演出/律令更新

        private void UpdateV2Visuals() {
            // 风暴压暗: 按阶段渐变 (P1 无, P2 中, P3 重)
            float stormTarget = IsPhase3 ? 0.72f : (IsPhase2 ? 0.4f : 0f);
            StormVisual = MathHelper.Lerp(StormVisual, stormTarget, 0.02f);

            // 审判庭网格地纹强度
            TribunalVisual = MathHelper.Lerp(TribunalVisual, TribunalActive ? 1f : 0f, 0.05f);

            // 律令切换天闪 (toolkit §C.1: 规则变化用「天闪」可读, 红只留伤害源)
            EdictFlash *= 0.9f;
            if (EdictFlash < 0.01f)
                EdictFlash = 0f;

            bool inPhase2State = State >= AIState.Phase2_StormChase && State <= AIState.Phase2_RapidCharge;
            bool eh = EdictHorizontal;
            if (eh != prevEdictHorizontal) {
                prevEdictHorizontal = eh;
                if (inPhase2State) {
                    EdictFlash = 1f;
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);
                    ACMUtils.AddScreenShake(4f);
                }
            }
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

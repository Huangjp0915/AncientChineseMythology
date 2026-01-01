using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥青丘狐尾巴 - 幽灵化的尾巴，具有穿透、魂魄拖尾效果
    /// 与九尾狐不同，这些尾巴更加飘渺、灵动，带有幽蓝色调
    /// </summary>
    public class NetherKitsuneTail
    {
        /// <summary>骨骼关节数量</summary>
        public const int JointCount = 14;
        
        /// <summary>每个骨骼段的基础长度</summary>
        public const float BaseSegmentLength = 26f;
        
        /// <summary>幽冥刺击时的最大延展倍率</summary>
        public const float MaxExtensionMultiplier = 4.5f;
        
        /// <summary>当前每个段的实际长度</summary>
        private float[] currentSegmentLengths;
        
        /// <summary>当前延展倍率</summary>
        private float currentExtension = 1.0f;
        
        /// <summary>目标延展倍率</summary>
        private float targetExtension = 1.0f;
        
        /// <summary>尾巴总长度（动态）</summary>
        public float TotalLength => JointCount * BaseSegmentLength * currentExtension;

        /// <summary>尾巴索引（0-8）</summary>
        public int TailIndex { get; private set; }
        
        /// <summary>关节位置数组</summary>
        public Vector2[] Joints { get; private set; }
        
        /// <summary>关节速度</summary>
        public Vector2[] Velocities { get; private set; }
        
        /// <summary>目标位置</summary>
        public Vector2 TargetPosition { get; set; }
        
        /// <summary>根部位置</summary>
        public Vector2 RootPosition { get; set; }
        
        /// <summary>根部基准角度</summary>
        public float BaseAngle { get; set; }
        
        /// <summary>是否处于攻击状态</summary>
        public bool IsAttacking { get; private set; }
        
        /// <summary>攻击计时器</summary>
        public float AttackTimer { get; private set; }
        
        /// <summary>攻击类型</summary>
        public TailAttackType CurrentAttack { get; private set; }
        
        /// <summary>攻击目标位置</summary>
        public Vector2 AttackTargetPos { get; private set; }

        // 物理参数 - 幽冥尾巴更轻盈飘逸
        private float stiffness = 0.10f;
        private float damping = 0.80f;
        private float gravityInfluence = 0.15f; // 幽灵化减少重力
        private float swayAmplitude = 12f;      // 更大的摆动幅度
        private float swaySpeed = 3.0f;
        private float swayPhase;
        
        // 幽冥特效参数
        private float ghostAlpha = 1.0f;        // 幽灵透明度
        private float soulTrailIntensity = 0f;  // 魂魄拖尾强度
        private float phaseShiftTimer = 0f;     // 相位偏移（用于闪烁效果）
        
        // 攻击状态参数
        private Vector2 attackStartPos;
        private float attackDuration;
        private float attackProgress;
        private Vector2[] attackKeyframes;
        
        // 渲染参数
        private float[] segmentWidths;
        private float glowIntensity = 0f;
        
        // 魂魄拖尾记录
        private Vector2[] trailPositions;
        private const int TrailLength = 8;
        
        public enum TailAttackType
        {
            None,
            GhostStab,          // 幽灵刺击 - 穿透性突刺
            SoulSweep,          // 魂魄横扫 - 带魂魄伤害的横扫
            PhaseWhip,          // 相位鞭打 - 忽明忽暗的鞭打
            SpiritDrain,        // 灵魂吸取 - 吸取生命
            NetherCoil,         // 幽冥盘绕 - 束缚敌人
            PhantomSlam,        // 幻影下砸 - 分裂成多个幻影攻击
            VoidPierce          // 虚空穿刺 - 超远距离穿刺
        }
        
        /// <summary>是否显示预判线</summary>
        public bool ShowTelegraph { get; set; }
        
        /// <summary>预判线目标方向</summary>
        public Vector2 TelegraphDirection { get; set; }
        
        /// <summary>预判线长度</summary>
        public float TelegraphLength { get; set; }

        public NetherKitsuneTail(int tailIndex)
        {
            TailIndex = tailIndex;
            Joints = new Vector2[JointCount];
            Velocities = new Vector2[JointCount];
            segmentWidths = new float[JointCount];
            currentSegmentLengths = new float[JointCount];
            attackKeyframes = new Vector2[4];
            trailPositions = new Vector2[TrailLength];
            
            // 每条尾巴的摆动相位不同，幽冥版更加错落
            swayPhase = tailIndex * MathHelper.TwoPi / 9f + Main.rand.NextFloat(0.5f);
            
            // 初始化段宽度（幽冥尾巴更细长飘逸）
            for (int i = 0; i < JointCount; i++)
            {
                float t = i / (float)(JointCount - 1);
                segmentWidths[i] = MathHelper.Lerp(0.9f, 0.2f, EaseOutQuad(t));
                currentSegmentLengths[i] = BaseSegmentLength;
            }
        }

        /// <summary>
        /// 初始化尾巴位置
        /// </summary>
        public void Initialize(Vector2 rootPos, float baseAngle)
        {
            RootPosition = rootPos;
            BaseAngle = baseAngle;
            currentExtension = 1.0f;
            targetExtension = 1.0f;
            
            for (int i = 0; i < JointCount; i++)
            {
                currentSegmentLengths[i] = BaseSegmentLength;
            }
            
            for (int i = 0; i < JointCount; i++)
            {
                float angle = baseAngle + MathF.Sin(i * 0.4f) * 0.25f;
                Joints[i] = rootPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * BaseSegmentLength * i;
                Velocities[i] = Vector2.Zero;
            }
            
            for (int i = 0; i < TrailLength; i++)
            {
                trailPositions[i] = Joints[JointCount - 1];
            }
            
            TargetPosition = Joints[JointCount - 1];
        }

        /// <summary>
        /// 更新尾巴状态
        /// </summary>
        public void Update(Vector2 newRootPos, float newBaseAngle, Vector2 ownerVelocity, float globalTime)
        {
            RootPosition = newRootPos;
            BaseAngle = newBaseAngle;
            
            // 更新相位偏移（幽灵闪烁效果）
            phaseShiftTimer += 0.05f;
            ghostAlpha = 0.7f + 0.3f * MathF.Sin(phaseShiftTimer + TailIndex * 0.5f);
            
            if (IsAttacking)
            {
                UpdateAttack(globalTime);
            }
            else
            {
                UpdateIdleMotion(ownerVelocity, globalTime);
                targetExtension = 1.0f;
            }
            
            // 平滑插值延展系数
            currentExtension = MathHelper.Lerp(currentExtension, targetExtension, 0.18f);
            
            UpdateSegmentLengths();
            SolveFABRIK();
            ApplyPhysics(ownerVelocity);
            UpdateGlow();
            UpdateTrail();
        }
        
        private void UpdateSegmentLengths()
        {
            for (int i = 0; i < JointCount; i++)
            {
                float factor = MathHelper.Lerp(0.5f, 1.5f, (float)i / (JointCount - 1));
                currentSegmentLengths[i] = BaseSegmentLength * (1.0f + (currentExtension - 1.0f) * factor);
            }
        }
        
        private void UpdateTrail()
        {
            // 更新魂魄拖尾
            for (int i = TrailLength - 1; i > 0; i--)
            {
                trailPositions[i] = trailPositions[i - 1];
            }
            trailPositions[0] = Joints[JointCount - 1];
        }

        /// <summary>
        /// 空闲状态的幽灵摆动
        /// </summary>
        private void UpdateIdleMotion(Vector2 ownerVelocity, float globalTime)
        {
            // 幽冥尾巴更加飘逸的摆动
            float swayOffset = MathF.Sin(globalTime * swaySpeed + swayPhase) * swayAmplitude;
            float swayOffset2 = MathF.Sin(globalTime * swaySpeed * 0.6f + swayPhase + 2f) * swayAmplitude * 0.7f;
            float swayOffset3 = MathF.Cos(globalTime * swaySpeed * 0.4f + swayPhase) * swayAmplitude * 0.3f;
            
            Vector2 velocityInfluence = -ownerVelocity * 1.0f;
            
            float targetAngle = BaseAngle + swayOffset * 0.06f + swayOffset2 * 0.04f;
            
            // 幽冥尾巴有轻微的上浮倾向
            Vector2 floatOffset = new Vector2(0, -10f + MathF.Sin(globalTime * 1.5f + TailIndex) * 5f);
            
            TargetPosition = RootPosition + 
                new Vector2(MathF.Cos(targetAngle), MathF.Sin(targetAngle)) * TotalLength * 0.85f +
                velocityInfluence * 3.5f +
                new Vector2(swayOffset + swayOffset3, swayOffset2) +
                floatOffset;
                
            // 更新魂魄拖尾强度
            soulTrailIntensity = MathHelper.Lerp(soulTrailIntensity, 0.3f, 0.05f);
        }

        /// <summary>
        /// 更新攻击动作
        /// </summary>
        private void UpdateAttack(float globalTime)
        {
            AttackTimer += 1f / 60f;
            attackProgress = AttackTimer / attackDuration;
            
            if (attackProgress >= 1f)
            {
                EndAttack();
                return;
            }

            switch (CurrentAttack)
            {
                case TailAttackType.GhostStab:
                    UpdateGhostStabAttack();
                    break;
                case TailAttackType.SoulSweep:
                    UpdateSoulSweepAttack();
                    break;
                case TailAttackType.PhaseWhip:
                    UpdatePhaseWhipAttack();
                    break;
                case TailAttackType.SpiritDrain:
                    UpdateSpiritDrainAttack();
                    break;
                case TailAttackType.NetherCoil:
                    UpdateNetherCoilAttack();
                    break;
                case TailAttackType.PhantomSlam:
                    UpdatePhantomSlamAttack();
                    break;
                case TailAttackType.VoidPierce:
                    UpdateVoidPierceAttack();
                    break;
            }
        }

        #region 攻击动作实现

        /// <summary>
        /// 幽灵刺击 - 穿透性突刺
        /// </summary>
        public void StartGhostStabAttack(Vector2 target, float duration = 0.35f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.GhostStab;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
            
            attackKeyframes[0] = attackStartPos;
            attackKeyframes[1] = RootPosition + (RootPosition - target).SafeNormalize(Vector2.UnitY) * 70f;
            attackKeyframes[2] = target + (target - RootPosition).SafeNormalize(Vector2.UnitY) * 80f;
            attackKeyframes[3] = RootPosition + (target - RootPosition).SafeNormalize(Vector2.UnitY) * TotalLength * 0.7f;
        }

        private void UpdateGhostStabAttack()
        {
            float t = attackProgress;
            
            if (t < 0.2f)
            {
                float localT = EaseOutQuad(t / 0.2f);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.25f;
                ghostAlpha = MathHelper.Lerp(1f, 0.5f, localT); // 蓄力时变淡
            }
            else if (t < 0.45f)
            {
                float localT = EaseInQuad((t - 0.2f) / 0.25f);
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.6f;
                glowIntensity = localT;
                ghostAlpha = MathHelper.Lerp(0.5f, 1f, localT); // 刺出时变实
                soulTrailIntensity = 1f;
            }
            else
            {
                float localT = EaseOutQuad((t - 0.45f) / 0.55f);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.6f, 0.1f, localT);
                glowIntensity = 1f - localT;
                soulTrailIntensity = 1f - localT * 0.7f;
            }
        }

        /// <summary>
        /// 魂魄横扫
        /// </summary>
        public void StartSoulSweepAttack(Vector2 target, float sweepAngle = MathHelper.PiOver2, float duration = 0.55f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.SoulSweep;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
            
            Vector2 toTarget = (target - RootPosition).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();
            float radius = TotalLength * 0.9f;
            
            attackKeyframes[0] = attackStartPos;
            attackKeyframes[1] = RootPosition + (baseAngle - sweepAngle * 0.7f).ToRotationVector2() * radius;
            attackKeyframes[2] = RootPosition + (baseAngle + sweepAngle * 0.7f).ToRotationVector2() * radius;
            attackKeyframes[3] = RootPosition + toTarget * TotalLength * 0.7f;
        }

        private void UpdateSoulSweepAttack()
        {
            float t = attackProgress;
            
            if (t < 0.15f)
            {
                float localT = EaseOutQuad(t / 0.15f);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.2f;
            }
            else if (t < 0.65f)
            {
                float localT = (t - 0.15f) / 0.5f;
                localT = (1f - MathF.Cos(localT * MathF.PI)) * 0.5f;
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.35f;
                glowIntensity = MathF.Sin(localT * MathF.PI);
                soulTrailIntensity = 0.8f + 0.2f * MathF.Sin(localT * MathF.PI);
            }
            else
            {
                float localT = EaseOutQuad((t - 0.65f) / 0.35f);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.35f, 0.1f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 相位鞭打 - 忽明忽暗
        /// </summary>
        public void StartPhaseWhipAttack(Vector2 target, float duration = 0.45f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.PhaseWhip;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdatePhaseWhipAttack()
        {
            float t = attackProgress;
            Vector2 toTarget = (AttackTargetPos - RootPosition).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();
            
            float wavePhase = t * MathF.PI * 3.5f;
            float waveAmplitude = MathF.Sin(t * MathF.PI) * 120f;
            float reach = EaseOutQuad(MathF.Min(t * 2.2f, 1f)) * TotalLength;
            
            Vector2 perpendicular = new Vector2(-toTarget.Y, toTarget.X);
            float sWave = MathF.Sin(wavePhase) * waveAmplitude;
            
            TargetPosition = RootPosition + toTarget * reach + perpendicular * sWave;
            stiffness = 0.15f + 0.35f * MathF.Sin(t * MathF.PI);
            glowIntensity = MathF.Sin(t * MathF.PI);
            
            // 相位闪烁效果
            ghostAlpha = 0.4f + 0.6f * MathF.Abs(MathF.Sin(t * MathF.PI * 5f));
            soulTrailIntensity = MathF.Sin(t * MathF.PI);
        }

        /// <summary>
        /// 灵魂吸取
        /// </summary>
        public void StartSpiritDrainAttack(Vector2 target, float duration = 0.9f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.SpiritDrain;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdateSpiritDrainAttack()
        {
            float t = attackProgress;
            Vector2 toTarget = (AttackTargetPos - RootPosition).SafeNormalize(Vector2.UnitY);
            
            if (t < 0.3f)
            {
                float localT = EaseOutQuad(t / 0.3f);
                TargetPosition = RootPosition + toTarget * TotalLength * localT;
                stiffness = MathHelper.Lerp(0.1f, 0.4f, localT);
                glowIntensity = localT * 0.5f;
            }
            else if (t < 0.8f)
            {
                // 吸取阶段 - 保持在目标位置并脉动
                float localT = (t - 0.3f) / 0.5f;
                float pulse = MathF.Sin(localT * MathF.PI * 4f) * 20f;
                TargetPosition = AttackTargetPos + toTarget * pulse;
                stiffness = 0.5f;
                glowIntensity = 0.5f + 0.5f * MathF.Sin(localT * MathF.PI * 4f);
                soulTrailIntensity = 1f;
            }
            else
            {
                float localT = EaseOutQuad((t - 0.8f) / 0.2f);
                TargetPosition = Vector2.Lerp(AttackTargetPos, RootPosition + toTarget * TotalLength * 0.5f, localT);
                stiffness = MathHelper.Lerp(0.5f, 0.1f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 幽冥盘绕
        /// </summary>
        public void StartNetherCoilAttack(float duration = 1.0f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.NetherCoil;
            AttackTimer = 0f;
            attackDuration = duration;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdateNetherCoilAttack()
        {
            float t = attackProgress;
            
            float coils = 2f + t * 2f;
            float radius = TotalLength * 0.35f * (1f - t * 0.4f);
            float angle = BaseAngle + coils * MathHelper.TwoPi;
            
            // 幽冥盘绕有上升效果
            float rise = t * 50f;
            
            TargetPosition = RootPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle) - rise / radius) * radius;
            stiffness = 0.15f + 0.25f * t;
            glowIntensity = t;
            ghostAlpha = 0.6f + 0.4f * t;
        }

        /// <summary>
        /// 幻影下砸
        /// </summary>
        public void StartPhantomSlamAttack(Vector2 target, float duration = 0.65f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.PhantomSlam;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
            
            attackKeyframes[0] = attackStartPos;
            attackKeyframes[1] = RootPosition + new Vector2(0, -TotalLength * 0.85f);
            attackKeyframes[2] = target;
            attackKeyframes[3] = RootPosition + (target - RootPosition).SafeNormalize(Vector2.UnitY) * TotalLength * 0.55f;
        }

        private void UpdatePhantomSlamAttack()
        {
            float t = attackProgress;
            
            if (t < 0.35f)
            {
                float localT = EaseOutQuad(t / 0.35f);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.25f;
                glowIntensity = localT * 0.4f;
                ghostAlpha = 1f - localT * 0.3f; // 高举时变淡
            }
            else if (t < 0.55f)
            {
                float localT = EaseInQuad((t - 0.35f) / 0.2f);
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.65f;
                glowIntensity = 0.4f + localT * 0.6f;
                ghostAlpha = 0.7f + localT * 0.3f; // 下砸时变实
                soulTrailIntensity = 1f;
            }
            else
            {
                float localT = EaseOutBack((t - 0.55f) / 0.45f);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.65f, 0.1f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 虚空穿刺 - 超远距离攻击
        /// </summary>
        public void StartVoidPierceAttack(Vector2 direction, float telegraphTime = 0.6f, float stabTime = 0.12f, float recoverTime = 0.45f)
        {
            IsAttacking = true;
            CurrentAttack = TailAttackType.VoidPierce;
            AttackTimer = 0f;
            attackDuration = telegraphTime + stabTime + recoverTime;
            
            TelegraphDirection = direction.SafeNormalize(Vector2.UnitX);
            attackStartPos = Joints[JointCount - 1];
            
            float extendedLength = JointCount * BaseSegmentLength * MaxExtensionMultiplier;
            TelegraphLength = extendedLength;
            
            voidPiercePhases[0] = telegraphTime;
            voidPiercePhases[1] = stabTime;
            voidPiercePhases[2] = recoverTime;
            
            ShowTelegraph = true;
        }
        
        private float[] voidPiercePhases = new float[3];
        
        private void UpdateVoidPierceAttack()
        {
            float t = attackProgress;
            float phase1End = voidPiercePhases[0] / attackDuration;
            float phase2End = (voidPiercePhases[0] + voidPiercePhases[1]) / attackDuration;
            
            if (t < phase1End) // 预判阶段
            {
                float localT = t / phase1End;
                
                float coilT = EaseOutQuad(localT);
                TargetPosition = RootPosition + TelegraphDirection * BaseSegmentLength * JointCount * 0.25f * (1f - coilT * 0.6f);
                
                targetExtension = 1.0f;
                stiffness = MathHelper.Lerp(0.1f, 0.45f, localT);
                glowIntensity = localT * 0.7f;
                ghostAlpha = 0.5f + 0.3f * MathF.Sin(localT * MathF.PI * 6f); // 闪烁预警
                ShowTelegraph = true;
            }
            else if (t < phase2End) // 穿刺阶段
            {
                float localT = (t - phase1End) / (phase2End - phase1End);
                
                float extensionT = EaseOutQuad(localT);
                targetExtension = 1.0f + (MaxExtensionMultiplier - 1.0f) * extensionT;
                
                float currentMaxLength = JointCount * BaseSegmentLength * targetExtension;
                TargetPosition = RootPosition + TelegraphDirection * currentMaxLength * 0.95f;
                
                stiffness = 0.85f;
                glowIntensity = 0.7f + extensionT * 0.3f;
                ghostAlpha = 1f;
                soulTrailIntensity = 1f;
                ShowTelegraph = false;
            }
            else // 回收阶段
            {
                float localT = (t - phase2End) / (1f - phase2End);
                float recoverT = EaseOutQuad(localT);
                
                targetExtension = MathHelper.Lerp(MaxExtensionMultiplier, 1.0f, recoverT);
                
                float currentMaxLength = JointCount * BaseSegmentLength * targetExtension;
                TargetPosition = RootPosition + TelegraphDirection * currentMaxLength * 0.55f;
                
                stiffness = MathHelper.Lerp(0.85f, 0.1f, recoverT);
                glowIntensity = 1f - recoverT;
                soulTrailIntensity = 1f - recoverT * 0.8f;
                ShowTelegraph = false;
            }
        }

        private void EndAttack()
        {
            IsAttacking = false;
            CurrentAttack = TailAttackType.None;
            AttackTimer = 0f;
            stiffness = 0.10f;
            glowIntensity = 0f;
            ShowTelegraph = false;
            ghostAlpha = 1f;
        }

        #endregion

        #region FABRIK IK算法

        private void SolveFABRIK()
        {
            const int iterations = 5;
            const float tolerance = 0.5f;
            
            Joints[0] = RootPosition;
            
            for (int iter = 0; iter < iterations; iter++)
            {
                float distToTarget = Vector2.Distance(Joints[JointCount - 1], TargetPosition);
                if (distToTarget < tolerance)
                    break;
                
                Joints[JointCount - 1] = TargetPosition;
                for (int i = JointCount - 2; i >= 0; i--)
                {
                    Vector2 direction = (Joints[i] - Joints[i + 1]).SafeNormalize(Vector2.UnitY);
                    Joints[i] = Joints[i + 1] + direction * currentSegmentLengths[i];
                }
                
                Joints[0] = RootPosition;
                for (int i = 1; i < JointCount; i++)
                {
                    Vector2 direction = (Joints[i] - Joints[i - 1]).SafeNormalize(Vector2.UnitY);
                    
                    // 幽冥尾巴更灵活
                    float maxBendAngle = MathHelper.ToRadians(45f / MathF.Max(1f, currentExtension * 0.4f));
                    
                    if (i > 1)
                    {
                        Vector2 prevDirection = (Joints[i - 1] - Joints[i - 2]).SafeNormalize(Vector2.UnitY);
                        direction = ConstrainAngle(direction, prevDirection, maxBendAngle);
                    }
                    else
                    {
                        Vector2 baseDirection = new Vector2(MathF.Cos(BaseAngle), MathF.Sin(BaseAngle));
                        float maxRootBendAngle = MathHelper.ToRadians(70f);
                        direction = ConstrainAngle(direction, baseDirection, maxRootBendAngle);
                    }
                    
                    Joints[i] = Joints[i - 1] + direction * currentSegmentLengths[i - 1];
                }
            }
        }

        private Vector2 ConstrainAngle(Vector2 direction, Vector2 referenceDirection, float maxAngle)
        {
            float angle = MathF.Acos(MathHelper.Clamp(Vector2.Dot(direction, referenceDirection), -1f, 1f));
            
            if (angle <= maxAngle)
                return direction;
            
            float cross = referenceDirection.X * direction.Y - referenceDirection.Y * direction.X;
            float sign = cross >= 0 ? 1f : -1f;
            float constrainedAngle = referenceDirection.ToRotation() + maxAngle * sign;
            
            return new Vector2(MathF.Cos(constrainedAngle), MathF.Sin(constrainedAngle));
        }

        #endregion

        #region 物理模拟

        private void ApplyPhysics(Vector2 ownerVelocity)
        {
            for (int i = 1; i < JointCount; i++)
            {
                Vector2 prevJoint = Joints[i - 1];
                Vector2 idealDirection = (Joints[i] - prevJoint).SafeNormalize(Vector2.UnitY);
                float segLen = currentSegmentLengths[i - 1];
                Vector2 idealPos = prevJoint + idealDirection * segLen;
                
                Vector2 springForce = (idealPos - Joints[i]) * stiffness;
                Vector2 inertiaForce = -ownerVelocity * (0.12f * i / JointCount);
                
                // 幽冥尾巴有轻微上浮
                float gravityFactor = (float)i / JointCount * gravityInfluence / MathF.Max(1f, currentExtension * 0.5f);
                Vector2 gravityForce = new Vector2(0, gravityFactor * 0.3f - 0.1f); // 轻微上浮
                
                Velocities[i] += springForce + inertiaForce + gravityForce;
                Velocities[i] *= damping;
                
                float maxSpeed = 18f * MathF.Max(1f, currentExtension * 0.5f);
                if (Velocities[i].LengthSquared() > maxSpeed * maxSpeed)
                {
                    Velocities[i] = Velocities[i].SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                
                Joints[i] += Velocities[i];
                
                Vector2 dir = (Joints[i] - prevJoint).SafeNormalize(Vector2.UnitY);
                Joints[i] = prevJoint + dir * segLen;
            }
        }

        private void UpdateGlow()
        {
            if (!IsAttacking && glowIntensity > 0)
            {
                glowIntensity = MathF.Max(0, glowIntensity - 0.025f);
            }
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 绘制尾巴
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color lightColor)
        {
            Texture2D bodyTex = NetherKitsune.NetherMissesBody;
            Texture2D tipTex = NetherKitsune.NetherMissesTop;
            
            if (bodyTex == null || tipTex == null)
                return;
            
            // 先绘制魂魄拖尾
            DrawSoulTrail(spriteBatch, screenPos);
            
            // 绘制所有体节
            for (int i = 0; i < JointCount - 1; i++)
            {
                DrawSegment(spriteBatch, screenPos, bodyTex, i, lightColor);
            }
            
            // 绘制尾尖
            DrawTip(spriteBatch, screenPos, tipTex, lightColor);
            
            // 绘制发光效果
            if (glowIntensity > 0)
            {
                DrawGlow(spriteBatch, screenPos);
            }
        }
        
        /// <summary>
        /// 绘制魂魄拖尾
        /// </summary>
        private void DrawSoulTrail(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (soulTrailIntensity <= 0.1f)
                return;
                
            Texture2D bodyTex = NetherKitsune.NetherMissesBody;
            if (bodyTex == null)
                return;
            
            for (int i = 1; i < TrailLength; i++)
            {
                float progress = 1f - (float)i / TrailLength;
                float alpha = progress * soulTrailIntensity * ghostAlpha * 0.4f;
                
                if (alpha < 0.02f)
                    continue;
                
                Vector2 pos = trailPositions[i];
                Vector2 prevPos = trailPositions[i - 1];
                Vector2 dir = (prevPos - pos).SafeNormalize(Vector2.UnitX);
                float rotation = dir.ToRotation();
                float length = Vector2.Distance(pos, prevPos);
                
                // 幽蓝色拖尾
                Color trailColor = new Color(80, 150, 220) * alpha;
                trailColor.A = 0;
                
                Vector2 scale = new Vector2(length / bodyTex.Width * 1.2f, 0.3f * progress);
                
                spriteBatch.Draw(
                    bodyTex,
                    (pos + prevPos) * 0.5f - screenPos,
                    null,
                    trailColor,
                    rotation,
                    new Vector2(bodyTex.Width * 0.5f, bodyTex.Height * 0.5f),
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        /// <summary>
        /// 绘制预判线
        /// </summary>
        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (!ShowTelegraph || TelegraphLength <= 0)
                return;
            
            float pulseTime = (float)Main.timeForVisualEffects * 0.1f;
            float pulse = 0.5f + 0.5f * MathF.Sin(pulseTime * 10f);
            
            // 幽蓝色预判线
            Color telegraphColor = Color.Lerp(new Color(60, 120, 200), new Color(100, 180, 255), pulse) * (0.25f + pulse * 0.25f);
            telegraphColor.A = 0;
            
            Vector2 startPos = RootPosition;
            Vector2 endPos = RootPosition + TelegraphDirection * TelegraphLength;
            
            int segments = 35;
            float lineWidth = 3f + pulse * 2f;
            
            Texture2D bodyTex = NetherKitsune.NetherMissesBody;
            if (bodyTex == null)
                return;
            
            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;
                
                Vector2 p1 = Vector2.Lerp(startPos, endPos, t1);
                Vector2 p2 = Vector2.Lerp(startPos, endPos, t2);
                
                // 虚线 + 波动效果
                if (i % 3 == 0) continue;
                
                float wave = MathF.Sin(t1 * MathF.PI * 8f + pulseTime * 5f) * 3f;
                Vector2 perpendicular = new Vector2(-TelegraphDirection.Y, TelegraphDirection.X);
                p1 += perpendicular * wave;
                p2 += perpendicular * wave;
                
                float alpha = 1f - t1 * 0.6f;
                Color segColor = telegraphColor * alpha;
                
                Vector2 drawPos = (p1 + p2) * 0.5f - screenPos;
                float segLength = Vector2.Distance(p1, p2);
                float rotation = (p2 - p1).ToRotation();
                
                Vector2 scale = new Vector2(segLength / bodyTex.Width * 1.5f, lineWidth / bodyTex.Height);
                spriteBatch.Draw(
                    bodyTex,
                    drawPos,
                    null,
                    segColor,
                    rotation,
                    new Vector2(bodyTex.Width * 0.5f, bodyTex.Height * 0.5f),
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // 终点警告 - 幽蓝色
            float warningPulse = 1f + 0.4f * MathF.Sin(pulseTime * 15f);
            Color warningColor = new Color(100, 180, 255) * (0.4f + pulse * 0.3f);
            warningColor.A = 0;
            
            Texture2D tipTex = NetherKitsune.NetherMissesTop;
            if (tipTex != null)
            {
                spriteBatch.Draw(
                    tipTex,
                    endPos - screenPos,
                    null,
                    warningColor,
                    TelegraphDirection.ToRotation(),
                    new Vector2(0, tipTex.Height * 0.5f),
                    warningPulse * 0.7f,
                    SpriteEffects.None,
                    0f
                );
            }
            
            Lighting.AddLight(endPos, new Vector3(0.2f, 0.5f, 0.8f) * pulse * 0.4f);
        }

        private void DrawSegment(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D texture, int index, Color lightColor)
        {
            if (index >= JointCount - 1)
                return;
            
            Vector2 start = Joints[index];
            Vector2 end = Joints[index + 1];
            Vector2 direction = end - start;
            float rotation = direction.ToRotation();
            float length = direction.Length();
            
            float widthScale = segmentWidths[index];
            
            // 幽蓝色调
            Color baseColor = Color.Lerp(lightColor, new Color(80, 140, 200), 0.5f);
            Color drawColor = Color.Lerp(baseColor, new Color(120, 200, 255), glowIntensity * 0.6f);
            drawColor *= ghostAlpha;
            
            Vector2 center = (start + end) * 0.5f;
            Vector2 drawPos = center - screenPos;
            
            Vector2 scale = new Vector2(length / texture.Width, widthScale);
            
            // 主体
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                drawColor,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                scale,
                SpriteEffects.None,
                0f
            );
            
            // 幽冥发光层
            if (glowIntensity > 0 || ghostAlpha < 0.9f)
            {
                Color glowColor = new Color(100, 180, 255) * (glowIntensity * 0.5f + (1f - ghostAlpha) * 0.3f);
                glowColor.A = 0;
                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    glowColor,
                    rotation,
                    new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                    scale * 1.25f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTip(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D texture, Color lightColor)
        {
            if (JointCount < 2)
                return;
            
            Vector2 lastJoint = Joints[JointCount - 1];
            Vector2 prevJoint = Joints[JointCount - 2];
            Vector2 direction = (lastJoint - prevJoint).SafeNormalize(Vector2.UnitY);
            float rotation = direction.ToRotation();
            
            // 幽蓝色尾尖
            Color baseColor = Color.Lerp(lightColor, new Color(100, 180, 255), 0.6f);
            Color tipColor = Color.Lerp(baseColor, new Color(150, 220, 255), glowIntensity);
            tipColor *= ghostAlpha;
            
            float tipScale = segmentWidths[JointCount - 1] * 1.1f;
            
            spriteBatch.Draw(
                texture,
                lastJoint - screenPos,
                null,
                tipColor,
                rotation,
                new Vector2(0, texture.Height * 0.5f),
                tipScale,
                SpriteEffects.None,
                0f
            );
            
            // 尾尖发光
            if (glowIntensity > 0)
            {
                Color glowColor = new Color(120, 200, 255) * glowIntensity * 0.6f;
                glowColor.A = 0;
                spriteBatch.Draw(
                    texture,
                    lastJoint - screenPos,
                    null,
                    glowColor,
                    rotation,
                    new Vector2(0, texture.Height * 0.5f),
                    tipScale * 1.4f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawGlow(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            for (int i = JointCount - 4; i < JointCount; i++)
            {
                if (i < 0) continue;
                
                float intensity = glowIntensity * (float)(i - (JointCount - 4) + 1) / 4f;
                
                // 幽蓝色光照
                Lighting.AddLight(Joints[i], new Vector3(0.3f, 0.6f, 0.9f) * intensity);
            }
        }

        #endregion

        #region 工具方法

        public Vector2 GetJointPosition(int index)
        {
            if (index < 0 || index >= JointCount)
                return RootPosition;
            return Joints[index];
        }

        public Vector2 GetTipPosition() => Joints[JointCount - 1];

        public Vector2 GetTipDirection()
        {
            if (JointCount < 2)
                return Vector2.UnitX;
            return (Joints[JointCount - 1] - Joints[JointCount - 2]).SafeNormalize(Vector2.UnitX);
        }

        public bool ShouldFireProjectile()
        {
            return CurrentAttack == TailAttackType.SpiritDrain && 
                   attackProgress >= 0.4f && attackProgress < 0.75f;
        }

        public float GetWidthAtIndex(int index)
        {
            if (index < 0 || index >= JointCount)
                return 1f;
            return segmentWidths[index];
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3) + c1 * MathF.Pow(t - 1f, 2);
        }

        #endregion
    }
}

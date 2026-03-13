using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 九尾狐尾巴 - 使用FABRIK逆运动学算法实现自然的尾巴运动
    /// 每条尾巴由多个骨骼关节组成，支持物理模拟和攻击动作
    /// </summary>
    public class KyuubiTail
    {
        /// <summary>骨骼关节数量</summary>
        public const int JointCount = 12;

        /// <summary>每个骨骼段的基础长度</summary>
        public const float BaseSegmentLength = 24f;

        /// <summary>远距离刺击时的最大延展倍率</summary>
        public const float MaxExtensionMultiplier = 4.0f;

        /// <summary>当前每个段的实际长度</summary>
        private float[] currentSegmentLengths;

        /// <summary>当前延展倍率 (1.0 = 正常, >1.0 = 延展)</summary>
        private float currentExtension = 1.0f;

        /// <summary>目标延展倍率</summary>
        private float targetExtension = 1.0f;

        /// <summary>尾巴总长度（动态）</summary>
        public float TotalLength => JointCount * BaseSegmentLength * currentExtension;

        /// <summary>尾巴索引（0-8）</summary>
        public int TailIndex { get; private set; }

        /// <summary>关节位置数组</summary>
        public Vector2[] Joints { get; private set; }

        /// <summary>关节速度（用于惯性模拟）</summary>
        public Vector2[] Velocities { get; private set; }

        /// <summary>目标位置（IK求解目标）</summary>
        public Vector2 TargetPosition { get; set; }

        /// <summary>根部位置（连接本体）</summary>
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

        // 物理参数
        private float stiffness = 0.15f;      // 刚度系数
        private float damping = 0.85f;         // 阻尼系数
        private float gravityInfluence = 0.3f; // 重力影响
        private float swayAmplitude = 8f;      // 自然摆动幅度
        private float swaySpeed = 2.5f;        // 摆动速度
        private float swayPhase;               // 摆动相位偏移

        // 攻击状态参数
        private Vector2 attackStartPos;
        private float attackDuration;
        private float attackProgress;
        private Vector2[] attackKeyframes;

        // 渲染参数
        private float[] segmentWidths;         // 每个段的宽度（用于渐变）
        private Color tailColor = Color.White;
        private float glowIntensity = 0f;

        public enum TailAttackType
        {
            None,
            Stab,           // 刺击 - 快速直线突刺
            Sweep,          // 横扫 - 弧形扫击
            Whip,           // 鞭打 - S形甩动
            ProjectileFire, // 射弹 - 尾尖发射弹幕
            Coil,           // 缠绕 - 螺旋盘绕准备
            Slam,           // 下砸 - 高举后猛砸
            LongRangeStab   // 远距离刺击 - 大范围远距离突刺
        }

        /// <summary>是否显示预判线</summary>
        public bool ShowTelegraph { get; set; }

        /// <summary>预判线目标方向</summary>
        public Vector2 TelegraphDirection { get; set; }

        /// <summary>预判线长度</summary>
        public float TelegraphLength { get; set; }

        public KyuubiTail(int tailIndex) {
            TailIndex = tailIndex;
            Joints = new Vector2[JointCount];
            Velocities = new Vector2[JointCount];
            segmentWidths = new float[JointCount];
            currentSegmentLengths = new float[JointCount];
            attackKeyframes = new Vector2[4];

            // 初始化相位偏移，让每条尾巴的摆动有差异
            swayPhase = tailIndex * MathHelper.TwoPi / 9f;

            // 初始化段宽度（从粗到细的渐变）
            for (int i = 0; i < JointCount; i++) {
                float t = i / (float)(JointCount - 1);
                // 使用平滑曲线：根部较粗，中间缓慢变细，尖端快速收窄
                segmentWidths[i] = MathHelper.Lerp(1.0f, 0.3f, EaseOutQuad(t));
                currentSegmentLengths[i] = BaseSegmentLength;
            }
        }

        /// <summary>
        /// 初始化尾巴位置
        /// </summary>
        public void Initialize(Vector2 rootPos, float baseAngle) {
            RootPosition = rootPos;
            BaseAngle = baseAngle;
            currentExtension = 1.0f;
            targetExtension = 1.0f;

            // 初始化段长度
            for (int i = 0; i < JointCount; i++) {
                currentSegmentLengths[i] = BaseSegmentLength;
            }

            // 沿着基准角度排列所有关节
            for (int i = 0; i < JointCount; i++) {
                float angle = baseAngle + MathF.Sin(i * 0.3f) * 0.2f; // 轻微弯曲
                Joints[i] = rootPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * BaseSegmentLength * i;
                Velocities[i] = Vector2.Zero;
            }

            TargetPosition = Joints[JointCount - 1];
        }

        /// <summary>
        /// 更新尾巴状态
        /// </summary>
        public void Update(Vector2 newRootPos, float newBaseAngle, Vector2 ownerVelocity, float globalTime) {
            RootPosition = newRootPos;
            BaseAngle = newBaseAngle;

            if (IsAttacking) {
                UpdateAttack(globalTime);
            }
            else {
                UpdateIdleMotion(ownerVelocity, globalTime);
                // 非攻击时恢复正常长度
                targetExtension = 1.0f;
            }

            // 平滑插值当前延展系数
            currentExtension = MathHelper.Lerp(currentExtension, targetExtension, 0.15f);

            // 更新每个段的实际长度
            UpdateSegmentLengths();

            // 应用FABRIK算法求解IK
            SolveFABRIK();

            // 应用物理模拟
            ApplyPhysics(ownerVelocity);

            // 更新发光效果
            UpdateGlow();
        }

        /// <summary>
        /// 更新每个段的实际长度
        /// </summary>
        private void UpdateSegmentLengths() {
            for (int i = 0; i < JointCount; i++) {
                // 末端段延展更多，根部段延展较少，产生自然的拉伸效果
                float segmentExtensionFactor = MathHelper.Lerp(0.5f, 1.5f, (float)i / (JointCount - 1));
                currentSegmentLengths[i] = BaseSegmentLength * (1.0f + (currentExtension - 1.0f) * segmentExtensionFactor);
            }
        }

        /// <summary>
        /// 空闲状态的自然摆动
        /// </summary>
        private void UpdateIdleMotion(Vector2 ownerVelocity, float globalTime) {
            // 计算自然摆动目标
            float swayOffset = MathF.Sin(globalTime * swaySpeed + swayPhase) * swayAmplitude;
            float swayOffset2 = MathF.Sin(globalTime * swaySpeed * 0.7f + swayPhase + 1.5f) * swayAmplitude * 0.5f;

            // 基于速度的拖尾效果
            Vector2 velocityInfluence = -ownerVelocity * 0.8f;

            // 计算自然延伸方向
            float targetAngle = BaseAngle + swayOffset * 0.05f + swayOffset2 * 0.03f;

            // 设置目标位置在尾巴自然延伸的末端
            TargetPosition = RootPosition +
                new Vector2(MathF.Cos(targetAngle), MathF.Sin(targetAngle)) * TotalLength * 0.9f +
                velocityInfluence * 3f +
                new Vector2(swayOffset, swayOffset2);
        }

        /// <summary>
        /// 更新攻击动作
        /// </summary>
        private void UpdateAttack(float globalTime) {
            AttackTimer += 1f / 60f; // 假设60FPS
            attackProgress = AttackTimer / attackDuration;

            if (attackProgress >= 1f) {
                EndAttack();
                return;
            }

            switch (CurrentAttack) {
                case TailAttackType.Stab:
                    UpdateStabAttack();
                    break;
                case TailAttackType.Sweep:
                    UpdateSweepAttack();
                    break;
                case TailAttackType.Whip:
                    UpdateWhipAttack();
                    break;
                case TailAttackType.ProjectileFire:
                    UpdateProjectileAttack();
                    break;
                case TailAttackType.Coil:
                    UpdateCoilAttack();
                    break;
                case TailAttackType.Slam:
                    UpdateSlamAttack();
                    break;
                case TailAttackType.LongRangeStab:
                    UpdateLongRangeStabAttack();
                    break;
            }
        }

        #region 攻击动作实现

        /// <summary>
        /// 开始刺击攻击
        /// </summary>
        public void StartStabAttack(Vector2 target, float duration = 0.4f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.Stab;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];

            // 设置关键帧：蓄力->突刺->回收
            attackKeyframes[0] = attackStartPos; // 起始
            attackKeyframes[1] = RootPosition + (RootPosition - target).SafeNormalize(Vector2.UnitY) * 80f; // 蓄力后撤
            attackKeyframes[2] = target + (target - RootPosition).SafeNormalize(Vector2.UnitY) * 60f; // 刺出超过目标
            attackKeyframes[3] = RootPosition + (target - RootPosition).SafeNormalize(Vector2.UnitY) * TotalLength * 0.7f; // 回收
        }

        private void UpdateStabAttack() {
            float t = attackProgress;

            // 使用贝塞尔曲线和分段时间控制
            if (t < 0.25f) // 蓄力阶段 (0-25%)
            {
                float localT = t / 0.25f;
                localT = EaseOutQuad(localT);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.3f; // 增加刚度表现力量感
            }
            else if (t < 0.5f) // 突刺阶段 (25-50%)
            {
                float localT = (t - 0.25f) / 0.25f;
                localT = EaseInQuad(localT); // 加速刺出
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.5f; // 最高刚度
                glowIntensity = localT; // 发光
            }
            else // 回收阶段 (50-100%)
            {
                float localT = (t - 0.5f) / 0.5f;
                localT = EaseOutQuad(localT);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.5f, 0.15f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 开始横扫攻击
        /// </summary>
        public void StartSweepAttack(Vector2 target, float sweepAngle = MathHelper.PiOver2, float duration = 0.6f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.Sweep;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];

            Vector2 toTarget = (target - RootPosition).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();
            float radius = TotalLength * 0.85f;

            // 横扫弧线的关键点
            attackKeyframes[0] = attackStartPos;
            attackKeyframes[1] = RootPosition + (baseAngle - sweepAngle * 0.6f).ToRotationVector2() * radius;
            attackKeyframes[2] = RootPosition + (baseAngle + sweepAngle * 0.6f).ToRotationVector2() * radius;
            attackKeyframes[3] = RootPosition + toTarget * TotalLength * 0.7f;
        }

        private void UpdateSweepAttack() {
            float t = attackProgress;

            if (t < 0.15f) // 准备
            {
                float localT = EaseOutQuad(t / 0.15f);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.25f;
            }
            else if (t < 0.6f) // 横扫
            {
                float localT = (t - 0.15f) / 0.45f;
                // 使用正弦曲线让扫动更流畅
                localT = (1f - MathF.Cos(localT * MathF.PI)) * 0.5f;
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.4f;
                glowIntensity = MathF.Sin(localT * MathF.PI);
            }
            else // 回收
            {
                float localT = EaseOutQuad((t - 0.6f) / 0.4f);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.4f, 0.15f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 开始鞭打攻击
        /// </summary>
        public void StartWhipAttack(Vector2 target, float duration = 0.5f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.Whip;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdateWhipAttack() {
            float t = attackProgress;
            Vector2 toTarget = (AttackTargetPos - RootPosition).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();

            // S形鞭打轨迹
            float wavePhase = t * MathF.PI * 3f;
            float waveAmplitude = MathF.Sin(t * MathF.PI) * 100f; // 中间最大振幅
            float reach = EaseOutQuad(MathF.Min(t * 2f, 1f)) * TotalLength;

            Vector2 perpendicular = new Vector2(-toTarget.Y, toTarget.X);
            float sWave = MathF.Sin(wavePhase) * waveAmplitude;

            TargetPosition = RootPosition + toTarget * reach + perpendicular * sWave;
            stiffness = 0.2f + 0.3f * MathF.Sin(t * MathF.PI);
            glowIntensity = MathF.Sin(t * MathF.PI);
        }

        /// <summary>
        /// 开始射弹攻击
        /// </summary>
        public void StartProjectileAttack(Vector2 target, float duration = 0.8f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.ProjectileFire;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdateProjectileAttack() {
            float t = attackProgress;
            Vector2 toTarget = (AttackTargetPos - RootPosition).SafeNormalize(Vector2.UnitY);

            if (t < 0.6f) // 蓄力瞄准
            {
                float localT = EaseOutQuad(t / 0.6f);
                // 尾巴指向目标方向
                TargetPosition = RootPosition + toTarget * TotalLength * 0.9f;
                stiffness = MathHelper.Lerp(0.15f, 0.5f, localT);
                glowIntensity = localT;
            }
            else if (t < 0.7f) // 发射时刻
            {
                float localT = (t - 0.6f) / 0.1f;
                // 这里应该触发实际的射弹生成
                TargetPosition = RootPosition + toTarget * TotalLength * (0.9f + localT * 0.1f);
                stiffness = 0.5f;
                glowIntensity = 1f;
            }
            else // 回收
            {
                float localT = EaseOutQuad((t - 0.7f) / 0.3f);
                TargetPosition = RootPosition + toTarget * TotalLength * MathHelper.Lerp(1f, 0.7f, localT);
                stiffness = MathHelper.Lerp(0.5f, 0.15f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 开始缠绕蓄力
        /// </summary>
        public void StartCoilAttack(float duration = 1.0f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.Coil;
            AttackTimer = 0f;
            attackDuration = duration;
            attackStartPos = Joints[JointCount - 1];
        }

        private void UpdateCoilAttack() {
            float t = attackProgress;

            // 螺旋盘绕效果
            float coils = 1.5f + t * 1.5f; // 盘绕圈数随时间增加
            float radius = TotalLength * 0.4f * (1f - t * 0.3f); // 半径逐渐收紧
            float angle = BaseAngle + coils * MathHelper.TwoPi;

            TargetPosition = RootPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            stiffness = 0.2f + 0.2f * t;
            glowIntensity = t;
        }

        /// <summary>
        /// 开始下砸攻击
        /// </summary>
        public void StartSlamAttack(Vector2 target, float duration = 0.7f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.Slam;
            AttackTimer = 0f;
            attackDuration = duration;
            AttackTargetPos = target;
            attackStartPos = Joints[JointCount - 1];

            // 高举位置
            attackKeyframes[0] = attackStartPos;
            attackKeyframes[1] = RootPosition + new Vector2(0, -TotalLength * 0.9f); // 高举
            attackKeyframes[2] = target; // 砸向目标
            attackKeyframes[3] = RootPosition + (target - RootPosition).SafeNormalize(Vector2.UnitY) * TotalLength * 0.6f;
        }

        private void UpdateSlamAttack() {
            float t = attackProgress;

            if (t < 0.4f) // 高举
            {
                float localT = EaseOutQuad(t / 0.4f);
                TargetPosition = Vector2.Lerp(attackKeyframes[0], attackKeyframes[1], localT);
                stiffness = 0.3f;
                glowIntensity = localT * 0.5f;
            }
            else if (t < 0.6f) // 下砸
            {
                float localT = EaseInQuad((t - 0.4f) / 0.2f);
                TargetPosition = Vector2.Lerp(attackKeyframes[1], attackKeyframes[2], localT);
                stiffness = 0.6f;
                glowIntensity = 0.5f + localT * 0.5f;
            }
            else // 回弹
            {
                float localT = EaseOutBack((t - 0.6f) / 0.4f);
                TargetPosition = Vector2.Lerp(attackKeyframes[2], attackKeyframes[3], localT);
                stiffness = MathHelper.Lerp(0.6f, 0.15f, localT);
                glowIntensity = 1f - localT;
            }
        }

        /// <summary>
        /// 开始远距离刺击攻击 - 大范围远距离突刺，尾巴会动态延展
        /// </summary>
        public void StartLongRangeStabAttack(Vector2 direction, float telegraphTime = 0.5f, float stabTime = 0.15f, float recoverTime = 0.4f) {
            IsAttacking = true;
            CurrentAttack = TailAttackType.LongRangeStab;
            AttackTimer = 0f;
            attackDuration = telegraphTime + stabTime + recoverTime;

            // 存储攻击方向
            TelegraphDirection = direction.SafeNormalize(Vector2.UnitX);
            attackStartPos = Joints[JointCount - 1];

            // 计算延展后的总长度
            float extendedLength = JointCount * BaseSegmentLength * MaxExtensionMultiplier;
            TelegraphLength = extendedLength;

            // 存储时间分配
            longRangePhases[0] = telegraphTime;
            longRangePhases[1] = stabTime;
            longRangePhases[2] = recoverTime;

            ShowTelegraph = true;
        }

        // 远距离刺击时间分配
        private float[] longRangePhases = new float[3];

        private void UpdateLongRangeStabAttack() {
            float t = attackProgress;
            float phase1End = longRangePhases[0] / attackDuration;
            float phase2End = (longRangePhases[0] + longRangePhases[1]) / attackDuration;

            if (t < phase1End) // 预判阶段 - 显示预判线并蓄力
            {
                float localT = t / phase1End;

                // 尾巴缓慢收缩到根部附近，准备蓄力
                float coilT = EaseOutQuad(localT);
                TargetPosition = RootPosition + TelegraphDirection * BaseSegmentLength * JointCount * 0.3f * (1f - coilT * 0.5f);

                // 延展系数保持正常
                targetExtension = 1.0f;
                stiffness = MathHelper.Lerp(0.15f, 0.5f, localT);
                glowIntensity = localT * 0.6f;
                ShowTelegraph = true;
            }
            else if (t < phase2End) // 刺出阶段 - 极快速延展刺出
            {
                float localT = (t - phase1End) / (phase2End - phase1End);

                // 快速延展尾巴
                float extensionT = EaseOutQuad(localT); // 快速加速然后减速
                targetExtension = 1.0f + (MaxExtensionMultiplier - 1.0f) * extensionT;

                // 目标位置在延展后的最远点
                float currentMaxLength = JointCount * BaseSegmentLength * targetExtension;
                TargetPosition = RootPosition + TelegraphDirection * currentMaxLength * 0.95f;

                stiffness = 0.8f; // 最高刚度，表现力量感
                glowIntensity = 0.6f + extensionT * 0.4f;
                ShowTelegraph = false;
            }
            else // 回收阶段 - 缓慢收缩
            {
                float localT = (t - phase2End) / (1f - phase2End);
                float recoverT = EaseOutQuad(localT);

                // 缓慢收缩尾巴
                targetExtension = MathHelper.Lerp(MaxExtensionMultiplier, 1.0f, recoverT);

                // 目标位置回到正常状态
                float currentMaxLength = JointCount * BaseSegmentLength * targetExtension;
                TargetPosition = RootPosition + TelegraphDirection * currentMaxLength * 0.6f;

                stiffness = MathHelper.Lerp(0.8f, 0.15f, recoverT);
                glowIntensity = 1f - recoverT;
                ShowTelegraph = false;
            }
        }

        private void EndAttack() {
            IsAttacking = false;
            CurrentAttack = TailAttackType.None;
            AttackTimer = 0f;
            stiffness = 0.15f;
            glowIntensity = 0f;
            ShowTelegraph = false;
        }

        #endregion

        #region FABRIK IK算法

        /// <summary>
        /// FABRIK (Forward And Backward Reaching Inverse Kinematics) 算法
        /// 实现自然的骨骼链IK求解
        /// </summary>
        private void SolveFABRIK() {
            const int iterations = 5; // 迭代次数
            const float tolerance = 0.5f; // 容差

            // 固定根部位置
            Joints[0] = RootPosition;

            for (int iter = 0; iter < iterations; iter++) {
                // 检查是否已经足够接近目标
                float distToTarget = Vector2.Distance(Joints[JointCount - 1], TargetPosition);
                if (distToTarget < tolerance)
                    break;

                // 向前传递 (Forward Reaching) - 从末端到根部
                Joints[JointCount - 1] = TargetPosition;
                for (int i = JointCount - 2; i >= 0; i--) {
                    Vector2 direction = (Joints[i] - Joints[i + 1]).SafeNormalize(Vector2.UnitY);
                    // 使用动态段长度
                    Joints[i] = Joints[i + 1] + direction * currentSegmentLengths[i];
                }

                // 向后传递 (Backward Reaching) - 从根部到末端
                Joints[0] = RootPosition;
                for (int i = 1; i < JointCount; i++) {
                    Vector2 direction = (Joints[i] - Joints[i - 1]).SafeNormalize(Vector2.UnitY);

                    // 应用角度约束（限制相邻骨骼之间的弯曲角度）
                    // 延展时放宽角度约束，让尾巴能更直地刺出
                    float maxBendAngle = MathHelper.ToRadians(35f / MathF.Max(1f, currentExtension * 0.5f));

                    if (i > 1) {
                        Vector2 prevDirection = (Joints[i - 1] - Joints[i - 2]).SafeNormalize(Vector2.UnitY);
                        direction = ConstrainAngle(direction, prevDirection, maxBendAngle);
                    }
                    else {
                        // 第一段相对于基准角度的约束
                        Vector2 baseDirection = new Vector2(MathF.Cos(BaseAngle), MathF.Sin(BaseAngle));
                        float maxRootBendAngle = MathHelper.ToRadians(60f);
                        direction = ConstrainAngle(direction, baseDirection, maxRootBendAngle);
                    }

                    // 使用动态段长度
                    Joints[i] = Joints[i - 1] + direction * currentSegmentLengths[i - 1];
                }
            }
        }

        /// <summary>
        /// 约束骨骼方向在允许的角度范围内
        /// </summary>
        private Vector2 ConstrainAngle(Vector2 direction, Vector2 referenceDirection, float maxAngle) {
            float angle = MathF.Acos(MathHelper.Clamp(Vector2.Dot(direction, referenceDirection), -1f, 1f));

            if (angle <= maxAngle)
                return direction;

            // 计算约束后的方向
            float cross = referenceDirection.X * direction.Y - referenceDirection.Y * direction.X;
            float sign = cross >= 0 ? 1f : -1f;
            float constrainedAngle = referenceDirection.ToRotation() + maxAngle * sign;

            return new Vector2(MathF.Cos(constrainedAngle), MathF.Sin(constrainedAngle));
        }

        #endregion

        #region 物理模拟

        /// <summary>
        /// 应用物理效果（惯性、阻尼、重力）
        /// </summary>
        private void ApplyPhysics(Vector2 ownerVelocity) {
            // 跳过根部
            for (int i = 1; i < JointCount; i++) {
                // 计算到理想位置的偏移
                Vector2 prevJoint = Joints[i - 1];
                Vector2 idealDirection = (Joints[i] - prevJoint).SafeNormalize(Vector2.UnitY);
                float segLen = currentSegmentLengths[i - 1];
                Vector2 idealPos = prevJoint + idealDirection * segLen;

                // 弹性力
                Vector2 springForce = (idealPos - Joints[i]) * stiffness;

                // 惯性（考虑主体速度）
                Vector2 inertiaForce = -ownerVelocity * (0.1f * i / JointCount);

                // 重力影响（末端受影响更大）- 延展时减少重力影响
                float gravityFactor = (float)i / JointCount * gravityInfluence / MathF.Max(1f, currentExtension * 0.5f);
                Vector2 gravityForce = new Vector2(0, gravityFactor * 0.5f);

                // 更新速度
                Velocities[i] += springForce + inertiaForce + gravityForce;
                Velocities[i] *= damping; // 阻尼

                // 限制最大速度 - 延展时允许更快的速度
                float maxSpeed = 15f * MathF.Max(1f, currentExtension * 0.5f);
                if (Velocities[i].LengthSquared() > maxSpeed * maxSpeed) {
                    Velocities[i] = Velocities[i].SafeNormalize(Vector2.Zero) * maxSpeed;
                }

                // 应用速度（但保持骨骼长度约束）
                Joints[i] += Velocities[i];

                // 重新约束长度
                Vector2 dir = (Joints[i] - prevJoint).SafeNormalize(Vector2.UnitY);
                Joints[i] = prevJoint + dir * segLen;
            }
        }

        private void UpdateGlow() {
            // 攻击时自动更新发光已在攻击函数中处理
            // 这里处理自然衰减
            if (!IsAttacking && glowIntensity > 0) {
                glowIntensity = MathF.Max(0, glowIntensity - 0.02f);
            }
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 绘制尾巴
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color lightColor) {
            Texture2D bodyTex = KyuubiKitsune.MissesBody;
            Texture2D tipTex = KyuubiKitsune.MissesTop;

            if (bodyTex == null || tipTex == null)
                return;

            // 绘制所有体节
            for (int i = 0; i < JointCount - 1; i++) {
                DrawSegment(spriteBatch, screenPos, bodyTex, i, lightColor);
            }

            // 绘制尾尖
            DrawTip(spriteBatch, screenPos, tipTex, lightColor);

            // 绘制发光效果
            if (glowIntensity > 0) {
                DrawGlow(spriteBatch, screenPos);
            }
        }

        /// <summary>
        /// 绘制预判线（在尾巴绘制之前调用）
        /// </summary>
        public void DrawTelegraph(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (!ShowTelegraph || TelegraphLength <= 0)
                return;

            // 预判线参数
            float pulseTime = (float)Main.timeForVisualEffects * 0.1f;
            float pulse = 0.5f + 0.5f * MathF.Sin(pulseTime * 8f);
            Color telegraphColor = Color.Lerp(Color.OrangeRed, Color.Gold, pulse) * (0.3f + pulse * 0.3f);
            telegraphColor.A = 0; // 加性混合

            Vector2 startPos = RootPosition;
            Vector2 endPos = RootPosition + TelegraphDirection * TelegraphLength;

            // 绘制预判线（使用多个点连成线）
            int segments = 30;
            float lineWidth = 4f + pulse * 2f;

            for (int i = 0; i < segments; i++) {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;

                Vector2 p1 = Vector2.Lerp(startPos, endPos, t1);
                Vector2 p2 = Vector2.Lerp(startPos, endPos, t2);

                // 虚线效果
                if (i % 3 == 0) continue;

                // 渐变淡出
                float alpha = 1f - t1 * 0.5f;
                Color segColor = telegraphColor * alpha;

                // 绘制线段（简化为点）
                Vector2 drawPos = (p1 + p2) * 0.5f - screenPos;
                float segLength = Vector2.Distance(p1, p2);
                float rotation = (p2 - p1).ToRotation();

                // 使用尾巴体节纹理绘制预判线
                Texture2D bodyTex = KyuubiKitsune.MissesBody;
                if (bodyTex != null) {
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
            }

            // 在终点绘制警告标记
            float warningPulse = 1f + 0.3f * MathF.Sin(pulseTime * 12f);
            Color warningColor = Color.Red * (0.5f + pulse * 0.3f);
            warningColor.A = 0;

            Texture2D tipTex = KyuubiKitsune.MissesTop;
            if (tipTex != null) {
                spriteBatch.Draw(
                    tipTex,
                    endPos - screenPos,
                    null,
                    warningColor,
                    TelegraphDirection.ToRotation(),
                    new Vector2(0, tipTex.Height * 0.5f),
                    warningPulse * 0.8f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 添加光照
            Lighting.AddLight(endPos, new Vector3(1f, 0.3f, 0.1f) * pulse * 0.5f);
        }

        /// <summary>
        /// 绘制单个体节
        /// </summary>
        private void DrawSegment(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D texture, int index, Color lightColor) {
            if (index >= JointCount - 1)
                return;

            Vector2 start = Joints[index];
            Vector2 end = Joints[index + 1];
            Vector2 direction = end - start;
            float rotation = direction.ToRotation();
            float length = direction.Length();

            // 计算宽度缩放
            float widthScale = segmentWidths[index];

            // 混合颜色（考虑发光）
            Color drawColor = Color.Lerp(lightColor, Color.OrangeRed, glowIntensity * 0.5f);
            drawColor = Color.Lerp(drawColor, tailColor, 0.3f);

            // 计算绘制位置（体节中心）
            Vector2 center = (start + end) * 0.5f;
            Vector2 drawPos = center - screenPos;

            // 计算缩放（X轴拉伸以匹配骨骼长度，Y轴根据宽度渐变）
            Vector2 scale = new Vector2(length / texture.Width, widthScale);

            // 绘制体节
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
        }

        /// <summary>
        /// 绘制尾尖
        /// </summary>
        private void DrawTip(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D texture, Color lightColor) {
            if (JointCount < 2)
                return;

            Vector2 lastJoint = Joints[JointCount - 1];
            Vector2 prevJoint = Joints[JointCount - 2];
            Vector2 direction = (lastJoint - prevJoint).SafeNormalize(Vector2.UnitY);
            float rotation = direction.ToRotation();

            // 尾尖颜色，发光时更亮
            Color tipColor = Color.Lerp(lightColor, Color.Gold, glowIntensity);
            tipColor = Color.Lerp(tipColor, tailColor, 0.3f);

            // 尾尖缩放
            float tipScale = segmentWidths[JointCount - 1] * 1.2f;

            // 绘制尾尖
            spriteBatch.Draw(
                texture,
                lastJoint - screenPos,
                null,
                tipColor,
                rotation,
                new Vector2(0, texture.Height * 0.5f), // 原点在左中，使尖端朝外
                tipScale,
                SpriteEffects.None,
                0f
            );
        }

        /// <summary>
        /// 绘制发光效果
        /// </summary>
        private void DrawGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            // 简单的加性发光
            // 在尾尖附近绘制发光点
            for (int i = JointCount - 3; i < JointCount; i++) {
                if (i < 0) continue;

                float intensity = glowIntensity * (float)(i - (JointCount - 3) + 1) / 3f;
                Color glowColor = Color.OrangeRed * intensity * 0.5f;
                glowColor.A = 0; // 加性混合

                // 使用简单的圆形发光（如果有专门的发光纹理可以替换）
                Vector2 pos = Joints[i] - screenPos;
                float glowSize = 20f * intensity;

                // 这里简化处理，实际可以使用专门的发光纹理
                // 添加光照效果
                Lighting.AddLight(Joints[i], new Vector3(1f, 0.5f, 0.2f) * intensity);
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取指定关节的世界位置
        /// </summary>
        public Vector2 GetJointPosition(int index) {
            if (index < 0 || index >= JointCount)
                return RootPosition;
            return Joints[index];
        }

        /// <summary>
        /// 获取尾尖位置
        /// </summary>
        public Vector2 GetTipPosition() => Joints[JointCount - 1];

        /// <summary>
        /// 获取尾尖方向
        /// </summary>
        public Vector2 GetTipDirection() {
            if (JointCount < 2)
                return Vector2.UnitX;
            return (Joints[JointCount - 1] - Joints[JointCount - 2]).SafeNormalize(Vector2.UnitX);
        }

        /// <summary>
        /// 检查射弹发射时机
        /// </summary>
        public bool ShouldFireProjectile() {
            return CurrentAttack == TailAttackType.ProjectileFire &&
                   attackProgress >= 0.6f && attackProgress < 0.65f;
        }

        /// <summary>
        /// 获取当前宽度缩放
        /// </summary>
        public float GetWidthAtIndex(int index) {
            if (index < 0 || index >= JointCount)
                return 1f;
            return segmentWidths[index];
        }

        // 缓动函数
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3) + c1 * MathF.Pow(t - 1f, 2);
        }

        #endregion
    }
}

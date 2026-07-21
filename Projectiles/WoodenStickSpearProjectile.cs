using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 棍系共享挥舞引擎 (如意棍系列重做地基, 系列内部复用)。
    ///
    /// 三段运动波形 (MOTION.md 挥砍解剖): 前摇 ~42% (二次缓动回拉反向蓄势) → 爆发 ~16%
    /// (poly(16) 急停缓出, 几乎全部角位移在前几帧) → 收招 ~42% (五次缓出回落)。
    /// 系列签名: 爆发帧棍身"如意"过冲伸长 (<see cref="Overshoot"/>), 随收招指数衰减。
    ///
    /// 多人安全: 瞄准角取自生成时的 <see cref="Projectile.velocity"/> (随生成包同步, 不在 AI 读鼠标);
    /// 伤害窗口严格对齐爆发段 (<see cref="CanDamage"/>), 前摇无判定。
    /// </summary>
    internal abstract class StickComboSwingBase : ModProjectile
    {
        protected enum MotionFamily
        {
            Swing,  // 角向挥扫
            Spin,   // 以玩家为轴的回环 (可双头判定)
            Thrust  // 轴向戳刺 (可多段连突)
        }

        /// <summary>单个连段步的配置。Arc 对 Swing/Spin 为弧度, 对 Thrust 为伸长倍率。</summary>
        protected struct SwingStep
        {
            public MotionFamily Family;
            public float Arc;
            public float Back;        // 前摇反向回拉量 (弧度 / Thrust 缩回比例)
            public float DamageMul;
            public float TimeMul;
            public float ScaleMul;
            public int SweepSign;     // +1 顺势, -1 反手回扫
            public int Reps;          // Thrust 连突次数
            public bool GroundImpact; // 重段: 爆发结束在棍尖打落点冲击
            public bool DoubleEnded;  // Spin: 两端均有判定

            public static SwingStep Sweep(float arc, float dmg, int sign = 1, float timeMul = 1f, float scaleMul = 1f, bool impact = false)
                => new() { Family = MotionFamily.Swing, Arc = arc, Back = 0.55f, DamageMul = dmg, TimeMul = timeMul, ScaleMul = scaleMul, SweepSign = sign, Reps = 1, GroundImpact = impact };

            public static SwingStep Spin(float rotations, float dmg, float timeMul = 1.4f, float scaleMul = 1.1f, bool doubleEnded = true)
                => new() { Family = MotionFamily.Spin, Arc = rotations * MathHelper.TwoPi, Back = 0.4f, DamageMul = dmg, TimeMul = timeMul, ScaleMul = scaleMul, SweepSign = 1, Reps = 1, DoubleEnded = doubleEnded };

            public static SwingStep Thrust(float reachMul, float dmg, int reps = 1, float timeMul = 1f)
                => new() { Family = MotionFamily.Thrust, Arc = reachMul, Back = 0.25f, DamageMul = dmg, TimeMul = timeMul, ScaleMul = 1f, SweepSign = 1, Reps = Math.Max(reps, 1) };
        }

        // ---- 每件武器覆写的旋钮 ----
        protected abstract SwingStep[] Steps { get; }
        protected virtual int CycleFrames => 22;          // 基准整挥帧数 (受近战攻速缩放)
        protected abstract Color TrailOuter { get; }      // 拖尾外层 (宽暗)
        protected abstract Color TrailInner { get; }      // 拖尾内层 (窄亮)
        protected virtual float TipLength => 100f;        // scale=1 时棍尖到手的长度 (像素)
        protected virtual float BaseScale => 1.15f;
        protected virtual float Overshoot => 0.10f;       // 爆发帧"如意伸长"过冲比例
        protected virtual int BurstTheme => -1;           // ACMWeaponBurst 主题 (-1 = 无)
        protected virtual float HitShake => 1.5f;
        protected virtual int HitDustType => DustID.WoodFurniture;
        protected virtual Vector3 GlowLight => Vector3.Zero;

        protected SwingStep Step => Steps[Math.Clamp((int)Projectile.ai[0], 0, Steps.Length - 1)];
        protected int StepIndex => Math.Clamp((int)Projectile.ai[0], 0, Steps.Length - 1);
        protected ref float AimAngle => ref Projectile.ai[1];
        protected ref float Timer => ref Projectile.ai[2];
        protected Player Owner => Main.player[Projectile.owner];

        // 波形阶段 (帧, 受攻速缩放)
        protected float TotalTime { get; private set; }
        protected float PrepEnd { get; private set; }
        protected float StrikeEnd { get; private set; }
        protected bool InStrike => Timer >= PrepEnd && Timer < StrikeEnd;

        // 视觉状态 (纯本地, 每端从同步的角度/计时器确定性推导)
        private float _lengthPulse = 1f;      // 如意伸长包络
        private float _extension = 1f;        // Thrust 轴向伸缩
        private readonly Vector2[] _tipTrail = new Vector2[12];
        private int _tipCount;
        private Vector2 _impactPoint;
        private float _impactAnim = -1f;      // >=0 时播放落点冲击环
        private bool _struck;                 // 爆发音效只放一次
        private bool _impacted;

        public override string Texture => "AncientChineseMythology/Textures/Projectiles/WoodenStickSpearProjectile";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            // 瞄准角来自 Shoot 的 velocity (owner 端算好, 随生成包同步) — 全端一致, 不读鼠标
            AimAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            Projectile.velocity = Vector2.Zero;

            // 连突段允许每突各命中一次; 回环允许少量多跳
            SwingStep s = Steps[Math.Clamp((int)Projectile.ai[0], 0, Steps.Length - 1)];
            if (s.Family == MotionFamily.Thrust && s.Reps > 1)
                Projectile.localNPCHitCooldown = 6;
            else if (s.Family == MotionFamily.Spin)
                Projectile.localNPCHitCooldown = 18;
        }

        public override bool ShouldUpdatePosition() => false;

        // ---- 缓动 ----
        protected static float QuadInOut(float t) => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
        protected static float QuintOut(float t) => 1f - MathF.Pow(1f - t, 5f);
        protected static float StrikeOut(float t) => 1f - MathF.Pow(1f - t, 16f);

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead || owner.noItems || owner.CCed) {
                Projectile.Kill();
                return;
            }

            float atk = MathF.Max(owner.GetTotalAttackSpeed(Projectile.DamageType), 0.4f);
            SwingStep step = Step;
            TotalTime = CycleFrames * step.TimeMul / atk;
            // 相位分布按运动家族: 横扫 42/16/42 (爆发即一瞬); 回环 25/55/20; 连突 30/45/25 (爆发段持续)
            switch (step.Family) {
                case MotionFamily.Spin:
                    PrepEnd = TotalTime * 0.25f;
                    StrikeEnd = TotalTime * 0.80f;
                    break;
                case MotionFamily.Thrust:
                    PrepEnd = TotalTime * 0.30f;
                    StrikeEnd = TotalTime * 0.75f;
                    break;
                default:
                    PrepEnd = TotalTime * 0.42f;
                    StrikeEnd = TotalTime * 0.58f;
                    break;
            }

            owner.heldProj = Projectile.whoAmI;
            owner.itemAnimation = 2;
            owner.itemTime = 2;
            // spriteDirection 每帧从已同步的 AimAngle 推导 (OnSpawn 不在远端运行)
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            owner.ChangeDir(Projectile.spriteDirection);

            UpdateMotion(step);

            if (GlowLight != Vector3.Zero)
                Lighting.AddLight(TipPosition(), GlowLight);

            // 棍尖轨迹环形缓冲 (拖尾)
            for (int i = _tipTrail.Length - 1; i > 0; i--)
                _tipTrail[i] = _tipTrail[i - 1];
            _tipTrail[0] = TipPosition();
            if (_tipCount < _tipTrail.Length)
                _tipCount++;

            if (_impactAnim >= 0f)
                _impactAnim++;

            Timer++;
            if (Timer >= TotalTime + 2f)
                Projectile.Kill();
        }

        private void UpdateMotion(SwingStep step) {
            float dir = Projectile.spriteDirection;

            // 如意伸长包络: 爆发瞬间冲到 1, 之后指数衰减
            if (InStrike) {
                float t = (Timer - PrepEnd) / MathF.Max(StrikeEnd - PrepEnd, 1f);
                _lengthPulse = 1f + Overshoot * StrikeOut(t);
                if (!_struck) {
                    _struck = true;
                    OnStrikeStart(step);
                }
            }
            else if (Timer >= StrikeEnd) {
                _lengthPulse = 1f + (_lengthPulse - 1f) * 0.88f;
            }

            float angleOff;
            if (step.Family == MotionFamily.Thrust) {
                UpdateThrust(step);
                angleOff = 0f;
            }
            else {
                float arc = step.Arc * step.SweepSign;
                float back = step.Back * step.SweepSign;
                // 回环用 poly(4) (持续旋转), 横扫用 poly(16) (一瞬爆发)
                Func<float, float> strikeEase = step.Family == MotionFamily.Spin
                    ? t => 1f - MathF.Pow(1f - t, 4f)
                    : StrikeOut;
                if (Timer < PrepEnd)
                    angleOff = -back * QuadInOut(Timer / PrepEnd);
                else if (Timer < StrikeEnd)
                    angleOff = MathHelper.Lerp(-back, arc, strikeEase((Timer - PrepEnd) / (StrikeEnd - PrepEnd)));
                else
                    angleOff = arc + 0.07f * arc * QuintOut(MathHelper.Clamp((Timer - StrikeEnd) / (TotalTime - StrikeEnd), 0f, 1f));
                // 挥扫中点对准瞄准角 (回环则以瞄准角为起点)
                float center = step.Family == MotionFamily.Spin ? 0f : 0.55f;
                angleOff -= arc * center;
                _extension = 1f;
            }

            Projectile.rotation = AimAngle + dir * angleOff;

            // 出现/收招缩放包络
            float appear = 1f;
            if (Timer < PrepEnd)
                appear = MathHelper.Lerp(0.85f, 1f, Timer / PrepEnd);
            else if (Timer > TotalTime * 0.8f)
                appear = MathHelper.Lerp(1f, 0.85f, (Timer - TotalTime * 0.8f) / (TotalTime * 0.2f));
            Projectile.scale = BaseScale * step.ScaleMul * appear * Owner.GetAdjustedItemScale(Owner.HeldItem);

            // 手臂与位置
            if (step.Family == MotionFamily.Spin) {
                Projectile.Center = Owner.MountedCenter;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            }
            else {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                Vector2 armPos = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                armPos.Y += Owner.gfxOffY;
                float push = step.Family == MotionFamily.Thrust ? 30f + (_extension - 1f) * TipLength * 0.5f : 30f;
                Projectile.Center = armPos + Projectile.rotation.ToRotationVector2() * push;
            }

            // 重段落点冲击 (爆发结束的第一帧)
            if (step.GroundImpact && !_impacted && Timer >= StrikeEnd) {
                _impacted = true;
                _impactPoint = TipPosition();
                _impactAnim = 0f;
                DoGroundImpact(_impactPoint);
            }
        }

        private void UpdateThrust(SwingStep step) {
            float retract = 1f - step.Back;
            if (Timer < PrepEnd) {
                _extension = MathHelper.Lerp(1f, retract, QuadInOut(Timer / PrepEnd));
            }
            else if (Timer < StrikeEnd) {
                float t = (Timer - PrepEnd) / (StrikeEnd - PrepEnd);
                float rep = t * step.Reps;
                int repIdx = Math.Min((int)rep, step.Reps - 1);
                float tr = rep - repIdx;
                float extend = MathHelper.Lerp(retract, step.Arc, StrikeOut(Math.Min(tr / 0.55f, 1f)));
                if (tr > 0.62f && repIdx < step.Reps - 1)
                    extend = MathHelper.Lerp(extend, retract, QuadInOut((tr - 0.62f) / 0.38f));
                _extension = extend;
                CurrentRep = repIdx;
            }
            else {
                float t = MathHelper.Clamp((Timer - StrikeEnd) / (TotalTime - StrikeEnd), 0f, 1f);
                _extension = MathHelper.Lerp(step.Arc, 0.9f, QuintOut(t));
            }
        }

        /// <summary>Thrust 家族当前突刺序号 (伤害分配用)。</summary>
        protected int CurrentRep { get; private set; }

        /// <summary>如意伸长包络 (1 = 静止; 爆发瞬间冲至 1+Overshoot) — 旗舰件着色器白闪复用。</summary>
        protected float LengthPulse => _lengthPulse;

        protected float EffectiveReach => TipLength * Projectile.scale * _extension * _lengthPulse;

        protected Vector2 TipPosition()
            => Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * EffectiveReach;

        // ---- 判定 ----
        public override bool? CanDamage() {
            float dmgEnd = StrikeEnd + (TotalTime - StrikeEnd) * 0.45f;
            if (Timer >= PrepEnd && Timer < dmgEnd)
                return base.CanDamage();
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 mid = Owner.MountedCenter;
            Vector2 tipVec = Projectile.rotation.ToRotationVector2() * EffectiveReach;
            Vector2 start = Step.Family == MotionFamily.Spin && Step.DoubleEnded ? mid - tipVec : mid;
            Vector2 end = mid + tipVec;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 14f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 mid = Owner.MountedCenter;
            Vector2 tipVec = Projectile.rotation.ToRotationVector2() * EffectiveReach;
            Vector2 start = Step.Family == MotionFamily.Spin && Step.DoubleEnded ? mid - tipVec : mid;
            Utils.PlotTileLine(start, mid + tipVec, 14f * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.FinalDamage *= Step.DamageMul;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中反馈栈: 方向性木屑/火花 + 预算内屏震 + 主题 Burst
            float heavy = Step.DamageMul >= 1.3f ? 1.6f : 1f;
            WeaponVFX.AddScreenShake(target.Center, HitShake * heavy);
            Vector2 away = (target.Center - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, HitDustType,
                    away.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5.5f) * heavy, 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            if (BurstTheme >= 0)
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, BurstTheme,
                    heavy > 1f ? 1.25f : 0.85f, Projectile.owner);
            OnStickHitNPC(target, hit);
        }

        /// <summary>爆发段起点 (挥砍音效等)。</summary>
        protected virtual void OnStrikeStart(SwingStep step) {
            float pitch = -0.12f + StepIndex * 0.09f + Main.rand.NextFloat(-0.08f, 0.08f);
            if (step.DamageMul >= 1.3f || step.Family == MotionFamily.Spin)
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = pitch - 0.1f }, Projectile.Center);
            else
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = pitch }, Projectile.Center);
        }

        protected virtual void OnStickHitNPC(NPC target, NPC.HitInfo hit) { }

        /// <summary>重段落点冲击 (屏震 + 尘 + 低频音; 冲击环在 PreDraw 播)。</summary>
        protected virtual void DoGroundImpact(Vector2 tip) {
            WeaponVFX.AddScreenShake(tip, 3f);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f, Pitch = -0.15f + Main.rand.NextFloat(0.1f) }, tip);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(tip, HitDustType,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4.5f, -1f)), 0, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        // ---- 绘制 ----
        public override bool PreDraw(ref Color lightColor) {
            // 拖尾: 角速度门控 — 只在爆发与收招前 40% 出现 (dressing 门控在快时刻)
            bool trailWindow = Timer >= PrepEnd && Timer < StrikeEnd + (TotalTime - StrikeEnd) * 0.4f;
            if (trailWindow && _tipCount >= 2) {
                var pts = new Vector2[_tipCount];
                Array.Copy(_tipTrail, pts, _tipCount);
                WeaponVFX.DrawRibbonTrail(pts, 7f * Projectile.scale, TrailOuter, TrailInner,
                    uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);
            }

            // 爆发帧棍尖柔光
            if (InStrike)
                WeaponVFX.DrawGlowBurst(TipPosition(), 0.5f * Projectile.scale, TrailInner * 0.8f);

            // 重段落点冲击环 (18 帧扩张衰减)
            if (_impactAnim >= 0f && _impactAnim < 18f) {
                float t = _impactAnim / 18f;
                WeaponVFX.DrawShockwaveRing(_impactPoint, 14f + t * 74f, 9f, (1f - t) * 0.85f, TrailInner, TrailOuter);
            }

            DrawStick(lightColor);
            return false;
        }

        /// <summary>棍身贴图绘制 (虚方法 — 旗舰件覆写接专属着色器)。贴图为 45° 对角朝向。</summary>
        protected virtual void DrawStick(Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            GetDrawParams(tex, out Vector2 origin, out float rotOff, out SpriteEffects fx);
            Main.EntitySpriteDraw(tex, StickDrawCenter() - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation + rotOff, origin, Projectile.scale * _lengthPulse, fx, 0);
        }

        protected void GetDrawParams(Texture2D tex, out Vector2 origin, out float rotOff, out SpriteEffects fx) {
            origin = tex.Size() * 0.5f;
            if (Projectile.spriteDirection > 0) {
                rotOff = MathHelper.ToRadians(45f);
                fx = SpriteEffects.None;
            }
            else {
                rotOff = MathHelper.ToRadians(135f);
                fx = SpriteEffects.FlipHorizontally;
            }
        }

        /// <summary>贴图中心点: Swing/Thrust 挂在手前方半棍处, Spin 居中于玩家。</summary>
        protected Vector2 StickDrawCenter() {
            if (Step.Family == MotionFamily.Spin)
                return Owner.MountedCenter;
            return Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * (EffectiveReach * 0.55f);
        }
    }

    /// <summary>木棍左键: 横扫 → 回扫 两段。朴素木褐, 无 Burst 无着色器 (系列最低档)。</summary>
    internal class WoodenStickSpearProjectile : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/WoodenStickSpearProjectile";

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.6f, 1f),
            SwingStep.Sweep(3.6f, 1.1f, sign: -1),
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 20;
        protected override Color TrailOuter => new(90, 70, 40, 150);
        protected override Color TrailInner => new(170, 140, 90, 200);
        protected override float TipLength => 92f;
        protected override int HitDustType => DustID.WoodFurniture;
    }
}

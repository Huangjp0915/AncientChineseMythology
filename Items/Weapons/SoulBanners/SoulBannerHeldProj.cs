using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡系列共享演出层：专属着色器缓存 + 灵绸布面绘制 + 吸魂漩涡绘制 + 主题色。
    /// 着色器按 WeaponVFX.GetEffect 惰性缓存；缺失时退化为共享原语，保证总有反馈。
    /// </summary>
    internal static class SoulBannerFX
    {
        // 主题色 —— 与 ACMWeaponBurst.Soul(28) 幽紫一致, 深端混入 AbyssPurple(11) 深渊紫
        public static readonly Color SoulLit = new(210, 165, 255);
        public static readonly Color SoulMid = new(180, 120, 255);
        public static readonly Color SoulDeep = new(95, 40, 165);
        public static readonly Color AbyssDeep = new(60, 30, 110);

        /// <summary>
        /// 灵绸布面：沿节点链画 TriangleStrip, 用 SoulBannerCloth 着色器上织纹/符纹/破边/鬼影。
        /// 须在有活动批的阶段调用 (PreDraw)。
        /// </summary>
        /// <param name="worldPoints">布面中心线世界坐标 (锚→尾)。</param>
        /// <param name="baseWidth">布面半宽 (像素)。</param>
        /// <param name="growth">成长比例 0~1 (鬼影面孔显现)。</param>
        /// <param name="flash">大招白闪 0~1。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="seed">实例种子 (错开噪声相位)。</param>
        public static void DrawSpectralCloth(Vector2[] worldPoints, float baseWidth, float growth,
            float flash, float intensity, float seed) {
            if (Main.dedServ || worldPoints == null || worldPoints.Length < 2 || intensity <= 0.01f)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            Effect fx = WeaponVFX.GetEffect("SoulBannerCloth");
            if (fx == null) {
                // 着色器缺失 → 双层 ribbon 兜底
                WeaponVFX.DrawRibbonTrail(worldPoints, baseWidth,
                    SoulDeep * intensity, SoulLit * intensity);
                return;
            }

            int subdivisions = MythologyConfig.Trail == TrailQualityLevel.Med ? 2 : 3;
            Vector2[] pts = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                pts[i] = worldPoints[i] - Main.screenPosition;

            var verts = ACMUtils.BuildRibbonStrip(pts,
                p => baseWidth * (0.7f + 0.3f * p),   // 尾端略张 (幡带飘展)
                _ => Color.White,
                0f, subdivisions);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorDeep"]?.SetValue(SoulDeep.ToVector4());
            fx.Parameters["uColorLit"]?.SetValue(SoulLit.ToVector4());
            fx.Parameters["uGrowth"]?.SetValue(MathHelper.Clamp(growth, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Texture2D noise = ACMShaders.NoiseTexture;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 吸魂漩涡：世界空间 quad, SoulBannerVortex 着色器 (三螺旋臂 + 向心流)。
        /// 须在有活动批的阶段调用 (PreDraw)。
        /// </summary>
        /// <param name="worldCenter">漩涡中心。</param>
        /// <param name="radius">最大半径 (像素)。</param>
        /// <param name="progress">展开进度 0~1。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="spin">旋速。</param>
        /// <param name="seed">实例种子。</param>
        public static void DrawSoulVortex(Vector2 worldCenter, float radius, float progress,
            float intensity, float spin = 2.8f, float seed = 0f) {
            if (Main.dedServ || intensity <= 0.01f || radius < 4f)
                return;

            Effect fx = WeaponVFX.GetEffect("SoulBannerVortex");
            if (fx == null) {
                WeaponVFX.DrawGlowBurst(worldCenter, radius / 32f, SoulMid * (intensity * 0.6f));
                return;
            }

            Texture2D carrier = ACMAsset.SoftGlow;
            if (carrier == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            fx.Parameters["uSpin"]?.SetValue(spin);
            fx.Parameters["uColorCore"]?.SetValue(SoulLit.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(SoulMid.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(seed);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            float diameter = radius * 2f;
            sb.Draw(carrier, worldCenter - Main.screenPosition, null, Color.White, 0f,
                carrier.Size() * 0.5f, new Vector2(diameter / carrier.Width, diameter / carrier.Height),
                SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 灵绸布面模拟 —— Verlet 节点链 (锚点钉死 + 距离约束)。
    /// 锚点被猛甩时布面自然滞后甩鞭 (次级运动); 纯本地视觉, 每客户端各自模拟。
    /// </summary>
    internal sealed class SoulBannerClothSim
    {
        public readonly Vector2[] Pos;
        private readonly Vector2[] prev;
        private readonly float segLen;
        private bool initialized;

        public SoulBannerClothSim(int nodes, float segmentLength) {
            Pos = new Vector2[nodes];
            prev = new Vector2[nodes];
            segLen = segmentLength;
        }

        /// <summary>
        /// 推进一帧。<paramref name="wind"/> 为整体风力 (如吸魂时朝漩涡的引力);
        /// <paramref name="flutterPhase"/> 驱动逐节点正弦微飘。
        /// </summary>
        public void Update(Vector2 anchor, Vector2 wind, float flutterPhase, float damping = 0.9f) {
            int n = Pos.Length;
            if (!initialized) {
                for (int i = 0; i < n; i++) {
                    Pos[i] = anchor + new Vector2(0f, i * segLen);
                    prev[i] = Pos[i];
                }
                initialized = true;
            }

            Pos[0] = anchor;
            prev[0] = anchor;

            for (int i = 1; i < n; i++) {
                Vector2 vel = (Pos[i] - prev[i]) * damping;
                prev[i] = Pos[i];
                float flutter = MathF.Sin(flutterPhase + i * 0.7f) * 0.22f;
                Pos[i] += vel + wind + new Vector2(flutter, 0.42f);
            }

            // 距离约束 (3 轮, 锚点每轮重钉)
            for (int iter = 0; iter < 3; iter++) {
                for (int i = 1; i < n; i++) {
                    Vector2 d = Pos[i] - Pos[i - 1];
                    float len = d.Length();
                    if (len < 0.001f)
                        continue;
                    float err = (len - segLen) / len;
                    if (i == 1) {
                        Pos[i] -= d * err;
                    }
                    else {
                        Pos[i] -= d * (err * 0.5f);
                        Pos[i - 1] += d * (err * 0.5f);
                    }
                }
                Pos[0] = anchor;
            }
        }

        /// <summary>强制重置到锚点 (瞬移/初次显形时避免布面横跨半屏)。</summary>
        public void Snap(Vector2 anchor) {
            for (int i = 0; i < Pos.Length; i++) {
                Pos[i] = anchor + new Vector2(0f, i * segLen);
                prev[i] = Pos[i];
            }
            initialized = true;
        }
    }

    /// <summary>
    /// 万魂幡左键手持弹幕 —— 祭幡法器动作（非挥剑）：
    /// 1. 举幡（Raise）：从身侧提起幡旗, 末段 reel-back 猛地回吸蓄势
    /// 2. 祭幡（Thrust）：poly(10) 急速直刺, 出击帧后坐回挫
    /// 3. 引魂（Channel）：幡旗驻留, 幡尖张开吸魂漩涡 (专属 shader)
    /// 4. 收魂（Retract）：抽回幡旗, 灵魂凝聚爆发
    /// 幡后拖一条灵绸布面 (Verlet 链 + SoulBannerCloth 着色器), 是"幡"身份的核心。
    /// </summary>
    public class SoulBannerHeldProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        // ── 动画参数 ──
        private const float MaxExtend = 90f;       // 最大伸出距离
        private const float StartOffset = -30f;    // 起始偏移（幡旗从身后开始）
        private const float BaseAbsorbRadius = 260f; // 基础引魂漩涡半径

        // 各阶段基准帧数（受攻速影响）
        private const float BaseRaise = 12f;
        private const float BaseThrust = 6f;
        private const float BaseChannel = 20f;
        private const float BaseRetract = 8f;

        private enum BannerPhase { Raise, Thrust, Channel, Retract }

        private Player Owner => Main.player[Projectile.owner];

        // ai slots
        private ref float AimAngle => ref Projectile.ai[0];
        private ref float GlobalTimer => ref Projectile.ai[1];

        // localAI slots
        private BannerPhase CurrentPhase {
            get => (BannerPhase)(int)Projectile.localAI[0];
            set { Projectile.localAI[0] = (int)value; phaseTimer = 0; }
        }

        // 运行时状态（客户端）
        private float phaseTimer;
        private float currentExtend;
        private float bannerScale;
        private bool hasBurstPlayed;
        private float recoilOffset;      // 刺出到位后坐回挫 (指数衰减)
        private float burstRingTimer;    // 收魂冲击环寿命 (帧)

        // 灵绸布面 (9 节点)
        private readonly SoulBannerClothSim cloth = new(9, 11f);

        // 残影系统
        private const int AfterimageLength = 8;
        private Vector2[] afterimagePositions = new Vector2[AfterimageLength];
        private float[] afterimageRotations = new float[AfterimageLength];

        // 受攻速影响的阶段时长
        private float RaiseTime => BaseRaise / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => BaseThrust / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => BaseRetract / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        // 成长系统缓存（每次释放时读取一次）
        private float growthAbsorbRadius;
        private float growthChannelMul;
        private float growthHealMul;
        private float growthRatio; // 0~1, 驱动布面鬼影/亮度/吸魂弧/灵魂脉冲
        private float ChannelTime => BaseChannel * growthChannelMul / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float AbsorbRadius => growthAbsorbRadius;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = 10000;
        }

        public override void OnSpawn(IEntitySource source) {
            AimAngle = Projectile.velocity.ToRotation();
            Projectile.velocity = Vector2.Zero;
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0 ? 1 : -1;
            bannerScale = 0f;
            currentExtend = StartOffset;

            // 缓存成长系统数值
            var sbPlayer = Owner.GetModPlayer<SoulBannerPlayer>();
            growthAbsorbRadius = BaseAbsorbRadius * sbPlayer.AbsorbRadiusMultiplier;
            growthChannelMul = sbPlayer.ChannelTimeMultiplier;
            growthHealMul = sbPlayer.HealMultiplier;
            growthRatio = sbPlayer.GrowthRatio;

            cloth.Snap(Owner.MountedCenter);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((sbyte)Projectile.spriteDirection);
            writer.Write(AimAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.spriteDirection = reader.ReadSByte();
            AimAngle = reader.ReadSingle();
        }

        public override void AI() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.direction = Projectile.spriteDirection;
            GlobalTimer++;
            phaseTimer++;

            switch (CurrentPhase) {
                case BannerPhase.Raise: RaisePhase(); break;
                case BannerPhase.Thrust: ThrustPhase(); break;
                case BannerPhase.Channel: ChannelPhase(); break;
                case BannerPhase.Retract: RetractPhase(); break;
            }

            PositionBanner();
            UpdateCloth();

            if (burstRingTimer > 0)
                burstRingTimer--;
        }

        // ── 举幡：提起幡旗, 末段 reel-back 回吸蓄势 ──
        private void RaisePhase() {
            float t = Math.Clamp(phaseTimer / RaiseTime, 0f, 1f);

            bannerScale = ACMUtils.QuadOut(t);
            float baseExtend = MathHelper.Lerp(StartOffset, 15f, ACMUtils.SineInOut(t));

            // 末段 28% 猛地回吸 (pow 曲线: 前段几乎不动, 最后几帧突然后缩 —— 出刺前的"吸气")
            float reel = 0f;
            if (t > 0.72f) {
                float rt = (t - 0.72f) / 0.28f;
                reel = ACMUtils.QuadIn(rt) * 9f;
            }
            currentExtend = baseExtend - reel;

            // 上升的幽灵粒子
            if (Main.rand.NextBool(3)) {
                Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(24f, 35f);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                    Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3f), 150, default, 0.5f + 0.4f * t);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            // 暗影火焰升腾（阴气汇聚感）
            if (t > 0.3f && Main.rand.NextBool(4)) {
                Vector2 flamePos = Owner.Center + new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(0f, 10f));
                Dust flame = Dust.NewDustDirect(flamePos, 1, 1, DustID.Shadowflame,
                    0f, -Main.rand.NextFloat(1f, 2.5f), 100, default, 0.7f * t);
                flame.noGravity = true;
            }

            if (phaseTimer == 3)
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = -0.3f }, Owner.Center);

            if (phaseTimer >= RaiseTime) {
                for (int i = 0; i < AfterimageLength; i++) {
                    afterimagePositions[i] = Projectile.Center;
                    afterimageRotations[i] = Projectile.rotation;
                }
                CurrentPhase = BannerPhase.Thrust;
            }
        }

        // ── 祭幡：poly(10) 急速直刺 —— 头两帧走完 ~85% 行程, 斩钉截铁 ──
        private void ThrustPhase() {
            float t = Math.Clamp(phaseTimer / ThrustTime, 0f, 1f);

            bannerScale = 1f;
            float p10 = 1f - MathF.Pow(1f - t, 10f);
            currentExtend = MathHelper.Lerp(6f, MaxExtend, p10);

            UpdateAfterimages();

            if (phaseTimer == 1) {
                // 双层音: 高频质感 + 低频挥动
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = -0.4f }, Projectile.Center);
            }

            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 tipPos = Owner.MountedCenter + dir * currentExtend;
            Vector2 perp = new(-dir.Y, dir.X);

            // 双螺旋尾迹粒子 (仅爆发段)
            if (t > 0.1f) {
                float spiralAngle = phaseTimer * 1.2f;
                for (int s = 0; s < 2; s++) {
                    float side = s == 0 ? 1f : -1f;
                    float spiralR = 8f + 10f * t;
                    Vector2 offset = perp * (MathF.Sin(spiralAngle + s * MathF.PI) * spiralR);
                    Vector2 spiralPos = tipPos + offset - dir * Main.rand.NextFloat(5f, 20f);
                    Dust spiral = Dust.NewDustDirect(spiralPos, 1, 1, DustID.PurpleTorch,
                        -dir.X * 2f + perp.X * side * 0.5f, -dir.Y * 2f + perp.Y * side * 0.5f,
                        80, default, 0.8f + 0.5f * t);
                    spiral.noGravity = true;
                }
            }

            // 幡尖前方散射粒子
            if (t > 0.4f && Main.rand.NextBool(2)) {
                Vector2 vel = dir * Main.rand.NextFloat(5f, 9f) + Main.rand.NextVector2Circular(2.5f, 2.5f);
                Dust dust = Dust.NewDustDirect(tipPos, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 60, default, 1.0f + 0.4f * t);
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            if (phaseTimer >= ThrustTime - 1 && recoilOffset <= 0f)
                ThrustImpact();

            if (phaseTimer >= ThrustTime)
                CurrentPhase = BannerPhase.Channel;
        }

        /// <summary>更新残影位置队列</summary>
        private void UpdateAfterimages() {
            for (int i = AfterimageLength - 1; i > 0; i--) {
                afterimagePositions[i] = afterimagePositions[i - 1];
                afterimageRotations[i] = afterimageRotations[i - 1];
            }
            afterimagePositions[0] = Projectile.Center;
            afterimageRotations[0] = Projectile.rotation;
        }

        // ── 引魂：幡旗驻留原地, 幡尖张开吸魂漩涡 ──
        private void ChannelPhase() {
            float t = Math.Clamp(phaseTimer / ChannelTime, 0f, 1f);

            bannerScale = 1f;
            // 后坐回挫 (指数衰减) + 驻留呼吸
            recoilOffset *= 0.78f;
            float breathe = MathF.Sin(phaseTimer * 0.35f) * 4f;
            currentExtend = MaxExtend + breathe - recoilOffset;

            // 吸魂漩涡粒子 (shader 承担形体, dust 只做点缀)
            SpawnSoulVortex(t);

            if (phaseTimer == 4)
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);

            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 tipPos = Owner.MountedCenter + dir * currentExtend;
            Vector2 perp = new(-dir.Y, dir.X);

            // 幡旗末端飘荡粒子
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(
                    tipPos + perp * Main.rand.NextFloat(-25f, 25f),
                    1, 1, DustID.DungeonSpirit,
                    perp.X * Main.rand.NextFloat(-1.5f, 1.5f),
                    -Main.rand.NextFloat(0.8f, 2.5f),
                    100, default, 0.9f + 0.3f * t);
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            if (phaseTimer >= ChannelTime)
                CurrentPhase = BannerPhase.Retract;
        }

        // ── 收魂：收回幡旗, 灵魂凝聚爆发 ──
        private void RetractPhase() {
            float t = Math.Clamp(phaseTimer / RetractTime, 0f, 1f);

            bannerScale = 1f - ACMUtils.QuadIn(t) * 0.3f;
            currentExtend = MathHelper.Lerp(MaxExtend, 0f, ACMUtils.QuadIn(t));

            if (!hasBurstPlayed) {
                hasBurstPlayed = true;
                SoulBurst();
            }

            if (phaseTimer >= RetractTime)
                Projectile.Kill();
        }

        /// <summary>定位幡旗 —— 沿固定瞄准方向的直线延伸</summary>
        private void PositionBanner() {
            Vector2 aimDir = AimAngle.ToRotationVector2();
            float armAngle = AimAngle - MathHelper.PiOver2;

            if (CurrentPhase == BannerPhase.Raise) {
                float raiseT = Math.Clamp(phaseTimer / RaiseTime, 0f, 1f);
                float startOffset = MathHelper.ToRadians(40f) * Projectile.spriteDirection;
                armAngle = MathHelper.Lerp(armAngle + startOffset, armAngle, ACMUtils.SineInOut(raiseT));
            }

            // 引魂阶段：手臂轻微颤抖（灵力涌动）
            if (CurrentPhase == BannerPhase.Channel)
                armAngle += MathF.Sin(phaseTimer * 0.5f) * 0.025f;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            Vector2 handPos = Owner.GetFrontHandPosition(
                Player.CompositeArmStretchAmount.Full, armAngle);
            handPos.Y += Owner.gfxOffY;

            Projectile.Center = handPos + aimDir * Math.Max(currentExtend, 0f);

            float flutter = 0f;
            if (CurrentPhase == BannerPhase.Channel)
                flutter = MathF.Sin(phaseTimer * 0.4f) * 0.06f;
            Projectile.rotation = AimAngle + flutter;

            Projectile.scale = bannerScale * 1.1f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;

            float lightMul = CurrentPhase == BannerPhase.Channel ? 1.5f : 0.5f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.12f, 0.55f) * lightMul);
        }

        /// <summary>灵绸布面推进：锚在幡杆顶端, 引魂时被漩涡吸向幡尖前方</summary>
        private void UpdateCloth() {
            Vector2 aimDir = AimAngle.ToRotationVector2();
            Vector2 anchor = Projectile.Center + aimDir * (52f * Math.Max(Projectile.scale, 0.01f));

            Vector2 wind = Vector2.Zero;
            if (CurrentPhase == BannerPhase.Channel) {
                // 漩涡吸力: 布面朝幡尖前方飘卷
                wind = aimDir * 0.55f;
            }
            else if (CurrentPhase == BannerPhase.Thrust) {
                wind = aimDir * 0.3f;
            }

            cloth.Update(anchor, wind, GlobalTimer * 0.22f);
        }

        /// <summary>刺出到达终点：冲击反馈 (音 + 震 + 环 + 后坐)</summary>
        private void ThrustImpact() {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 impactPos = Owner.MountedCenter + dir * MaxExtend;

            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = 0.3f }, impactPos);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.3f, Pitch = -0.6f }, impactPos);

            WeaponVFX.AddScreenShake(impactPos, 3.5f);
            recoilOffset = 8f; // 出击帧后坐

            // 环形冲击粒子 (收敛数量, 冲击环交给 PreDraw 的 shockwave)
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                Dust dust = Dust.NewDustDirect(impactPos, 1, 1, DustID.PurpleTorch,
                    vel.X, vel.Y, 60, default, 1.5f + Main.rand.NextFloat(0.3f));
                dust.noGravity = true;
                dust.fadeIn = 1.8f;
            }

            // 前向幽灵冲击粒子
            for (int i = 0; i < 8; i++) {
                Vector2 vel = dir * Main.rand.NextFloat(6f, 14f) + Main.rand.NextVector2Circular(3f, 3f);
                Dust ghost = Dust.NewDustDirect(impactPos, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 40, default, 1.6f);
                ghost.noGravity = true;
                ghost.fadeIn = 2.0f;
            }

            // 扇形冲击线
            for (int i = 0; i < 6; i++) {
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-35f, 35f));
                Vector2 vel = (AimAngle + spread).ToRotationVector2() * Main.rand.NextFloat(8f, 16f);
                Dust beam = Dust.NewDustDirect(impactPos, 1, 1, DustID.ShadowbeamStaff,
                    vel.X, vel.Y, 80, default, 1.0f);
                beam.noGravity = true;
            }
        }

        /// <summary>
        /// 引魂漩涡点缀粒子 —— 形体由 SoulBannerVortex 着色器承担,
        /// 这里只保留敌人身上的抽魂流线与内核脉动 (较旧版减量 ~50%)。
        /// </summary>
        private void SpawnSoulVortex(float channelProgress) {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 vortexCenter = Owner.MountedCenter + dir * currentExtend;
            float expandedRadius = AbsorbRadius * ACMUtils.QuadOut(Math.Min(channelProgress * 3f, 1f));

            // ── 从敌人身上抽取灵魂粒子 ──
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this)) continue;

                float dist = Vector2.Distance(npc.Center, vortexCenter);
                if (dist > expandedRadius) continue;

                // 主灵魂流
                if (Main.rand.NextBool(3)) {
                    Vector2 soulPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                    Vector2 toVortex = (vortexCenter - soulPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toVortex.Y, toVortex.X);
                    Vector2 soulVel = toVortex * Main.rand.NextFloat(7f, 13f)
                        + tangent * Main.rand.NextFloat(-4f, 4f);

                    Dust dust = Dust.NewDustDirect(soulPos, 1, 1, DustID.DungeonSpirit,
                        soulVel.X, soulVel.Y, 40, default, 1.4f + 0.3f * channelProgress);
                    dust.noGravity = true;
                    dust.fadeIn = 2.0f;
                }

                // 微光碎片点缀
                if (Main.rand.NextBool(6)) {
                    Vector2 sparkPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.6f, npc.height * 0.6f);
                    Vector2 toV = (vortexCenter - sparkPos).SafeNormalize(Vector2.Zero);
                    Dust spark = Dust.NewDustDirect(sparkPos, 1, 1, DustID.GemAmethyst,
                        toV.X * 8f, toV.Y * 8f, 0, default, 0.6f);
                    spark.noGravity = true;
                }
            }

            // ── 漩涡内核脉动 ──
            float coreIntensity = 0.5f + 0.5f * MathF.Sin(phaseTimer * 0.4f);
            for (int k = 0; k < (int)(2 * coreIntensity + 1); k++) {
                Vector2 corePos = vortexCenter + Main.rand.NextVector2Circular(8f, 8f);
                Dust core = Dust.NewDustDirect(corePos, 1, 1, DustID.PurpleTorch,
                    0f, 0f, 40, default, 1.0f + 0.5f * coreIntensity);
                core.noGravity = true;
                core.velocity *= 0.3f;
            }

            // ── 外圈引气粒子 (稀疏) ──
            if (channelProgress > 0.15f && Main.rand.NextBool(2)) {
                float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float ringR = expandedRadius * Main.rand.NextFloat(0.55f, 1f);
                Vector2 ringPos = vortexCenter + ringAngle.ToRotationVector2() * ringR;
                Vector2 inward = (vortexCenter - ringPos).SafeNormalize(Vector2.Zero);
                Vector2 tangent = new(-inward.Y, inward.X);
                Dust dust = Dust.NewDustPerfect(ringPos, DustID.PurpleTorch, inward * 3f + tangent * 2f,
                    80, default, 0.55f);
                dust.noGravity = true;
            }
        }

        /// <summary>收魂时灵魂凝聚爆发</summary>
        private void SoulBurst() {
            Vector2 burstCenter = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = 0.5f }, burstCenter);
            SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 0.4f, Pitch = -0.3f }, burstCenter);

            WeaponVFX.AddScreenShake(burstCenter, 4f);
            burstRingTimer = 14;

            // 向外爆发的幽灵
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);
                Dust dust = Dust.NewDustDirect(burstCenter, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 30, default, 1.6f + Main.rand.NextFloat(0.4f));
                dust.noGravity = true;
                dust.fadeIn = 2.2f;
            }

            // 暗影火焰环
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + 0.1f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust flame = Dust.NewDustDirect(burstCenter, 1, 1, DustID.Shadowflame,
                    vel.X, vel.Y, 60, default, 1.3f);
                flame.noGravity = true;
            }

            // 向上升腾的残魂
            for (int i = 0; i < 5; i++) {
                Vector2 pos = burstCenter + Main.rand.NextVector2Circular(20f, 10f);
                Dust rising = Dust.NewDustDirect(pos, 1, 1, DustID.DungeonSpirit,
                    Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 6f), 80, default, 1.2f);
                rising.noGravity = true;
                rising.fadeIn = 1.6f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 1.5f);

            // 命中音: 音高随机分层
            SoundEngine.PlaySound(SoundID.NPCHit54 with {
                Volume = 0.3f,
                Pitch = -0.2f + Main.rand.NextFloat(-0.15f, 0.15f)
            }, target.Center);

            // 万魂幡幽紫命中演出 (径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Soul, scale: 0.9f, owner: Projectile.owner);

            // 灵魂从敌人身上飞向幡旗
            for (int i = 0; i < 7; i++) {
                Vector2 toOwner = (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero);
                Vector2 tangent = new(-toOwner.Y, toOwner.X);
                Vector2 vel = toOwner * Main.rand.NextFloat(5f, 11f)
                    + tangent * Main.rand.NextFloat(-2.5f, 2.5f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustDirect(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 50, default, 1.4f);
                dust.noGravity = true;
                dust.fadeIn = 1.8f;
            }

            // 吸取生命（受成长影响）
            if (Main.rand.NextBool(3)) {
                int healAmount = Math.Max(1, (int)(damageDone / 20f * growthHealMul));
                Main.player[Projectile.owner].Heal(healAmount);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool? CanDamage() {
            if (CurrentPhase == BannerPhase.Raise)
                return false;
            return base.CanDamage();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = AimAngle.ToRotationVector2();

            if (CurrentPhase == BannerPhase.Channel) {
                Vector2 vortexCenter = Owner.MountedCenter + dir * currentExtend;
                float checkRadius = AbsorbRadius * 0.45f;
                Vector2 closest = new(
                    MathHelper.Clamp(vortexCenter.X, targetHitbox.Left, targetHitbox.Right),
                    MathHelper.Clamp(vortexCenter.Y, targetHitbox.Top, targetHitbox.Bottom));
                return Vector2.Distance(vortexCenter, closest) < checkRadius;
            }
            else {
                Vector2 start = Owner.MountedCenter;
                Vector2 end = start + dir * Math.Max(currentExtend, 0f);
                float collisionPoint = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    start, end, 25f * Projectile.scale, ref collisionPoint);
            }
        }

        public override void CutTiles() {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + dir * Math.Max(currentExtend, 0f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        // ── 自定义绘制 ──
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(texture.Width / 2f, texture.Height / 2f);
            SpriteEffects effects = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;

            float channelT = CurrentPhase == BannerPhase.Channel
                ? Math.Clamp(phaseTimer / ChannelTime, 0f, 1f) : 0f;

            // ── 1) 灵绸布面 (在本体后面) ──
            if (bannerScale > 0.35f) {
                float clothIntensity = 0.55f + 0.35f * growthRatio
                    + (CurrentPhase == BannerPhase.Channel ? 0.15f : 0f);
                SoulBannerFX.DrawSpectralCloth(cloth.Pos, 13f * Projectile.scale, growthRatio,
                    flash: 0f, intensity: clothIntensity * bannerScale,
                    seed: Projectile.whoAmI * 0.173f);
            }

            // ── 2) 引魂阶段: 幡尖吸魂漩涡 (专属 shader) + 吸魂弧 ──
            if (CurrentPhase == BannerPhase.Channel) {
                Vector2 dir = AimAngle.ToRotationVector2();
                Vector2 vortexCenter = Owner.MountedCenter + dir * currentExtend;
                float vortexRadius = (95f + 55f * growthRatio) * (0.85f + 0.15f * MathF.Sin(phaseTimer * 0.3f));
                SoulBannerFX.DrawSoulVortex(vortexCenter, vortexRadius,
                    progress: ACMUtils.QuadOut(Math.Min(channelT * 2.5f, 1f)),
                    intensity: 0.85f, spin: 3.2f, seed: Projectile.whoAmI * 0.311f);

                // 幽紫吸魂弧 (spectral ribbon 螺旋汇入幡尖)
                int arcs = 3;
                for (int a = 0; a < arcs; a++) {
                    Vector2[] pts = new Vector2[8];
                    float baseA = GlobalTimer * 0.1f + a * MathHelper.TwoPi / arcs;
                    for (int k = 0; k < 8; k++) {
                        float t = k / 7f;
                        float r = MathHelper.Lerp(230f, 12f, t) * (0.6f + 0.4f * growthRatio);
                        float ang = baseA + t * 2.2f;
                        pts[k] = vortexCenter + ang.ToRotationVector2() * r;
                    }
                    WeaponVFX.DrawRibbonTrail(pts, 9f,
                        new Color(95, 40, 165, 150), new Color(210, 165, 255, 200),
                        uvScroll: -GlobalTimer * 0.02f);
                }

                // 满成长: 幽紫灵魂脉冲 (走全屏名额仲裁, 名额满则退化柔光)
                if (growthRatio > 0.95f)
                    WeaponVFX.DrawRadialBloom(vortexCenter, 0.12f, 0.55f,
                        SoulBannerFX.SoulMid, 10f);
            }

            // ── 3) 收魂爆发冲击环 ──
            if (burstRingTimer > 0) {
                float ringT = 1f - burstRingTimer / 14f;
                WeaponVFX.DrawShockwaveRing(Projectile.Center,
                    12f + ringT * 110f, 12f, (1f - ringT) * 0.85f,
                    SoulBannerFX.SoulLit, SoulBannerFX.AbyssDeep);
            }

            // ── 4) 幡身幽紫柔光: 亮度随成长提升, 灵魂归幡时闪烁 ──
            var sbPlayer = Owner.GetModPlayer<SoulBannerPlayer>();
            float absorbFlash = sbPlayer.absorbFlashTimer > 0 ? sbPlayer.absorbFlashTimer / 12f : 0f;
            float growGlow = (0.35f + 0.85f * growthRatio + 0.5f * absorbFlash) * Projectile.scale;
            WeaponVFX.DrawGlowBurst(Projectile.Center, growGlow,
                new Color(150, 60, 255) * (0.4f + 0.5f * growthRatio + 0.3f * absorbFlash));

            // ── 阶段光晕强度 ──
            float glowIntensity = CurrentPhase switch {
                BannerPhase.Channel => 0.6f + 0.3f * MathF.Sin(phaseTimer * 0.3f),
                BannerPhase.Thrust => 0.4f + 0.45f * Math.Clamp(phaseTimer / ThrustTime, 0f, 1f),
                BannerPhase.Retract => 0.7f * (1f - Math.Clamp(phaseTimer / RetractTime, 0f, 1f)),
                _ => 0.2f + 0.12f * MathF.Sin(GlobalTimer * 0.15f),
            };

            // ── 刺出阶段：残影拖尾 (仅 strike act 门控) ──
            if (CurrentPhase == BannerPhase.Thrust) {
                for (int i = AfterimageLength - 1; i >= 1; i--) {
                    float progress = 1f - (float)i / AfterimageLength;
                    float alpha = progress * 0.4f;
                    float scale = Projectile.scale * (0.8f + 0.2f * progress);

                    Color trailColor = Color.Lerp(
                        new Color(80, 20, 160) * alpha,
                        new Color(40, 10, 100) * (alpha * 0.5f),
                        (float)i / AfterimageLength);

                    float trailRotation = afterimageRotations[i] + MathHelper.PiOver2;
                    Main.EntitySpriteDraw(texture,
                        afterimagePositions[i] - Main.screenPosition,
                        null, trailColor, trailRotation, origin,
                        scale, effects, 0);
                }
            }

            // ── 收回阶段：消散残影 ──
            if (CurrentPhase == BannerPhase.Retract) {
                float retractT = Math.Clamp(phaseTimer / RetractTime, 0f, 1f);
                Vector2 aimDir = AimAngle.ToRotationVector2();
                for (int i = 1; i <= 4; i++) {
                    Vector2 trailPos = Projectile.Center + aimDir * i * 16f * (1f - retractT);
                    float alpha = (1f - retractT) * 0.25f * (1f - i / 5f);
                    Color fadeColor = new Color(130, 50, 200) * alpha;
                    Main.EntitySpriteDraw(texture,
                        trailPos - Main.screenPosition,
                        null, fadeColor, drawRotation, origin,
                        Projectile.scale * (0.7f + 0.3f * (1f - i / 5f)), effects, 0);
                }
            }

            // ── 引魂阶段：多层光环（法阵感） ──
            if (CurrentPhase == BannerPhase.Channel) {
                float outerPulse = 1.4f + 0.15f * MathF.Sin(phaseTimer * 0.2f);
                Color outerAura = new Color(120, 40, 220) * (glowIntensity * 0.15f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, outerAura, drawRotation, origin,
                    Projectile.scale * outerPulse, effects, 0);

                Color innerAura = new Color(200, 80, 255) * (glowIntensity * 0.25f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, innerAura, drawRotation, origin,
                    Projectile.scale * 1.08f, effects, 0);
            }

            // ── 通用光晕层（色彩呼吸） ──
            float colorShift = MathF.Sin(GlobalTimer * 0.08f) * 0.5f + 0.5f;
            Color glowColor = Color.Lerp(
                new Color(130, 40, 210),
                new Color(80, 50, 255),
                colorShift) * glowIntensity;

            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, glowColor, drawRotation, origin,
                Projectile.scale * 1.14f, effects, 0);

            // ── 本体 ──
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, lightColor * Projectile.Opacity, drawRotation, origin,
                Projectile.scale, effects, 0);

            return false;
        }
    }

    /// <summary>
    /// 哭嚎亡魂 —— 大招「万魂齐哭」涌出的灵体。
    /// 出生直线爆冲 9 帧 (速度即对比) → 硬刹 → 索敌追踪; ribbon 拖尾 + 柔光头部。
    /// 命中造成 300% 武器伤害 (生成时已计入) 并回复少量生命。
    /// </summary>
    public class SoulWailSpirit : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Age++;

            if (Age <= 9f) {
                // 直线爆冲段: 不转向 (straight reads fast)
            }
            else if (Projectile.velocity.Length() > 7f) {
                Projectile.velocity *= 0.85f; // 硬刹
            }
            else {
                // 索敌追踪
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.085f);
                    // 幽魂游动波
                    Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                    Projectile.position += perp * MathF.Sin(Age * 0.35f + Projectile.whoAmI) * 1.6f;
                }
                else if (Projectile.timeLeft > 24) {
                    Projectile.timeLeft = 24; // 无目标 → 提前消散
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    1, 1, DustID.DungeonSpirit,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, 0.9f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.12f, 0.5f));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this))
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 2f);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with {
                Volume = 0.3f,
                Pitch = 0.3f + Main.rand.NextFloat(-0.15f, 0.15f)
            }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Soul, scale: 1.15f, owner: Projectile.owner);

            // 亡魂反哺: 少量治疗 (受成长回复倍率)
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                float healMul = owner.GetModPlayer<SoulBannerPlayer>().HealMultiplier;
                int heal = Math.Max(1, (int)(damageDone / 30f * healMul));
                owner.Heal(Math.Min(heal, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 80, default, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 双层 ribbon 拖尾 (外深渊紫 / 内幽紫亮)
            WeaponVFX.DrawProjectileTrail(Projectile, 15f,
                new Color(60, 30, 110, 150), new Color(210, 165, 255, 190),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);

            // 头部: 柔光核 + 沿速度拉伸的光斑 (幽魂脸)
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 pos = Projectile.Center - Main.screenPosition;
                float flicker = 0.85f + 0.15f * MathF.Sin(Projectile.localAI[0] * 0.5f + Projectile.whoAmI);
                Color outer = SoulBannerFX.SoulMid * (0.55f * flicker); outer.A = 0;
                Color inner = SoulBannerFX.SoulLit * (0.9f * flicker); inner.A = 0;
                Main.spriteBatch.Draw(glow, pos, null, outer, 0f, glow.Size() * 0.5f,
                    new Vector2(0.62f, 0.45f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, pos, null, inner, Projectile.rotation, glow.Size() * 0.5f,
                    new Vector2(0.4f, 0.22f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 灵魂归幡飞线 —— 击杀收魂的纯视觉弹幕 (damage=0)。
    /// 沿贝塞尔弧线从尸体飞向持幡者, 到达时点亮幡身柔光 + UI 脉冲。同屏 ≤12 (生成端限流)。
    /// </summary>
    public class SoulWispVFX : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float FlightFrames = 22f;

        private ref float StartX => ref Projectile.ai[0];
        private ref float StartY => ref Projectile.ai[1];
        private ref float FlightT => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            FlightT += 1f / FlightFrames;
            float t = ACMUtils.SineInOut(Math.Clamp(FlightT, 0f, 1f));

            Vector2 start = new(StartX, StartY);
            Vector2 end = owner.MountedCenter;
            // 控制点: 中点 + 确定性侧偏 + 上飘 (灵魂袅袅升起再落入幡中)
            float side = (Projectile.whoAmI % 2 == 0 ? 1f : -1f) * (40f + Projectile.whoAmI % 5 * 14f);
            Vector2 mid = (start + end) * 0.5f;
            Vector2 dir = (end - start).SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 control = mid + perp * side + new Vector2(0f, -55f);

            // 二次贝塞尔
            Vector2 p01 = Vector2.Lerp(start, control, t);
            Vector2 p12 = Vector2.Lerp(control, end, t);
            Projectile.Center = Vector2.Lerp(p01, p12, t);
            Projectile.rotation = (p12 - p01).ToRotation();

            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.DungeonSpirit,
                    0f, -0.4f, 130, default, 0.55f);
                dust.noGravity = true;
                dust.velocity *= 0.25f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.18f, 0.07f, 0.3f));

            if (FlightT >= 1f) {
                // 到达: 幡身柔光 + UI 脉冲 (仅本地表现)
                if (Projectile.owner == Main.myPlayer)
                    owner.GetModPlayer<SoulBannerPlayer>().absorbFlashTimer = 12;
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                    Dust dust = Dust.NewDustDirect(owner.MountedCenter, 1, 1, DustID.PurpleTorch,
                        vel.X, vel.Y, 60, default, 0.9f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.18f, Pitch = 0.6f }, owner.Center);
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, 7f,
                new Color(95, 40, 165, 140), new Color(210, 165, 255, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f, subdivisions: 2);

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color c = SoulBannerFX.SoulLit * 0.8f; c.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, c,
                    0f, glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}

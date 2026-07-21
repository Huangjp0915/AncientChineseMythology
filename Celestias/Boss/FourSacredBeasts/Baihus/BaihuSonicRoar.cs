using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎·音波环 Sonic Roar —— 虎啸扩散的环形冲击波（音啸破阵 / 震地踏震波共用）。
    /// V3 重做：
    ///  • 判定窗口与视觉带严格对齐 —— 环带半宽 <see cref="BandHalfWidth"/>（带宽 ~34px），只有波前带内有伤害；
    ///  • 可选<b>旋转安全缺口</b>（ai[2]=初始缺口角，&lt;-100 表示无缺口）——「音啸破阵」的可穿之门，
    ///    缺口角每帧确定性推进（各端同步自增，不依赖随机），伤害判定与着色器渲染共用同一角度；
    ///  • 渲染换 <see cref="BaihuVFX.DrawSonicRing"/>（BaihuSonicRing.fx 环形 TriangleStrip：同心波纹 + 色散微光 + 缺口）。
    /// </summary>
    public class BaihuSonicRoar : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public ref float RingRadius => ref Projectile.ai[0];
        public ref float MaxRadius => ref Projectile.ai[1];
        /// <summary>缺口中心角(世界弧度)。&lt;-100 = 无缺口(震地踏震波)。各端每帧确定性旋转。</summary>
        public ref float GapCenter => ref Projectile.ai[2];

        private const float ExpansionSpeed = 7f;
        /// <summary>判定环带半宽(带宽 ~34px)。视觉带略宽于判定(对玩家宽容)。</summary>
        private const float BandHalfWidth = 17f;
        /// <summary>缺口半宽(弧度, ~36°)。</summary>
        public const float GapHalfArc = 0.63f;
        /// <summary>缺口旋转速度(弧度/帧)。</summary>
        private const float GapRotSpeed = 0.012f;

        private bool HasGap => GapCenter > -100f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (MaxRadius == 0) MaxRadius = 400f;

            RingRadius += ExpansionSpeed;
            Projectile.velocity = Vector2.Zero;

            // 缺口确定性旋转(方向由 identity 奇偶决定, 各端一致)
            if (HasGap)
                GapCenter = MathHelper.WrapAngle(GapCenter + GapRotSpeed * (Projectile.identity % 2 == 0 ? 1f : -1f));

            // 动态碰撞箱跟随环半径
            int newSize = (int)(RingRadius * 2);
            if (newSize > 10)
                Projectile.Resize(newSize, newSize);

            // 波前扰动粒子(避开缺口方向, 音画一致)
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                if (!HasGap || MathF.Abs(MathHelper.WrapAngle(angle - GapCenter)) > GapHalfArc) {
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * RingRadius;
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.Silver, angle.ToRotationVector2() * 2.4f, 150, default, 0.9f);
                    d.noGravity = true;
                }
            }

            if (RingRadius > MaxRadius) Projectile.Kill();

            Lighting.AddLight(Projectile.Center, 0.2f, 0.2f, 0.25f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 环形判定: 只有波前带内造成伤害(与视觉带对齐); 缺口扇区安全
            Vector2 closestPoint = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            Vector2 diff = closestPoint - Projectile.Center;
            float dist = diff.Length();
            if (dist < RingRadius - BandHalfWidth || dist > RingRadius + BandHalfWidth)
                return false;
            if (HasGap && MathF.Abs(MathHelper.WrapAngle(diff.ToRotation() - GapCenter)) < GapHalfArc)
                return false;
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float progress = MathHelper.Clamp(RingRadius / MathF.Max(MaxRadius, 1f), 0f, 1f);
            float fadeIn = MathHelper.Clamp(RingRadius / 60f, 0f, 1f);
            float alpha = (1f - progress * 0.55f) * fadeIn;

            // 主环带(视觉半宽略大于判定半宽 → 对玩家宽容)
            BaihuVFX.DrawSonicRing(Projectile.Center, RingRadius, BandHalfWidth + 6f,
                alpha, HasGap ? GapCenter : -999f, GapHalfArc);

            // 内侧余波(纯装饰, 无判定, 更弱更窄)
            if (RingRadius > 90f)
                BaihuVFX.DrawSonicRing(Projectile.Center, RingRadius - 44f, 8f,
                    alpha * 0.30f, HasGap ? GapCenter : -999f, GapHalfArc);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            BaihuClawMark.Apply(target, 240);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi / 15 * i;
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + angle.ToRotationVector2() * RingRadius * 0.5f,
                    0, 0, DustID.Silver,
                    MathF.Cos(angle) * 3f, MathF.Sin(angle) * 3f, 100, default, 1f);
                d.noGravity = true;
            }
        }

        /// <summary>服务端权威生成一圈音波环。gapCenter &lt;-100 表示无缺口。</summary>
        public static void Spawn(IEntitySource src, Vector2 center, float maxRadius, int damage, float gapCenter = -999f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int p = Projectile.NewProjectile(src, center, Vector2.Zero,
                ModContent.ProjectileType<BaihuSonicRoar>(), damage, 0f, Main.myPlayer,
                ai0: 0f, ai1: maxRadius, ai2: gapCenter);
            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].netUpdate = true;
        }
    }
}

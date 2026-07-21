using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·黑风爪痕 (V3) — P2 风刃弹。V2 时是无人引用的死代码 (随机乱飞的头像图标), 改造为:
    /// 黑风连环冲的段间甩出的弧线风刃 / 黑风怒嚎的环形风暴刃。
    /// 初速由 Boss 传入, 飞行中按 ai[0] 缓慢弯曲 + 轻微减速, 尾段淡出消散 (不落地堆积)。
    /// ai[0] = 每帧弯曲弧度 (±, 0=直线); ai[1] = 计时 (自增)。
    /// </summary>
    public class BlackBear_Proj4 : ModProjectile
    {
        private const int LifeTicks = 90;
        private const int FadeTail = 24; // 尾段淡出

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        private ref float Curve => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTicks;
            Projectile.alpha = 120;
            Projectile.light = 0.25f;
        }

        public override void AI() {
            Timer++;

            // 淡入
            if (Projectile.alpha > 0 && Projectile.timeLeft > FadeTail)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 15);

            // 弧线弯曲 + 轻微空气阻力 (风刃越飞越散)
            if (Math.Abs(Curve) > 0.0001f)
                Projectile.velocity = Projectile.velocity.RotatedBy(Curve);
            Projectile.velocity *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 尾段消散: 淡出且失去伤害 (伤害窗口与视觉严格对齐; 由 timeLeft 推导, 各端确定)
            if (Projectile.timeLeft <= FadeTail) {
                float f = Projectile.timeLeft / (float)FadeTail; // 1 → 0
                Projectile.alpha = (int)(255f * (1f - f));
                if (Projectile.timeLeft <= FadeTail / 2)
                    Projectile.damage = 0;
            }

            // 黑风丝缕 (节流)
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Smoke, -Projectile.velocity * 0.1f, 140, new Color(60, 45, 90), 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 程序化风刃: SlashBurst 放射纹沿速度拉伸 + 柔光垫底; 暗紫主体 + 淡金刃缘
            Texture2D slash = ACMAsset.SlashBurst ?? ACMAsset.SoftGlow;
            Texture2D soft = ACMAsset.SoftGlow;
            if (slash == null || soft == null)
                return false;

            float opacity = Projectile.Opacity;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 slashOrigin = slash.Size() / 2f;
            Vector2 softOrigin = soft.Size() / 2f;
            float rot = Projectile.rotation;

            // 拖尾 (旧位置残影, 暗紫渐隐)
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = new Color(70, 50, 110) * (0.30f * fade * opacity);
                trail.A = 0;
                Main.spriteBatch.Draw(soft, old, null, trail, rot, softOrigin, new Vector2(0.75f, 0.32f) * fade, SpriteEffects.None, 0f);
            }

            // 底层风晕
            Color aura = new Color(52, 36, 88) * (0.75f * opacity);
            aura.A = 0;
            Main.spriteBatch.Draw(soft, drawPos, null, aura, rot, softOrigin, new Vector2(1.05f, 0.42f), SpriteEffects.None, 0f);

            // 主体爪痕 (沿速度方向拉伸的放射纹)
            Color body = new Color(120, 90, 190) * (0.8f * opacity);
            body.A = 0;
            Rectangle src = new(0, 0, slash.Width, slash.Height);
            Main.spriteBatch.Draw(slash, drawPos, src, body, rot, slashOrigin,
                new Vector2(0.22f, 0.075f), SpriteEffects.None, 0f);

            // 淡金刃缘高光
            Color edge = new Color(255, 215, 130) * (0.35f * opacity);
            edge.A = 0;
            Main.spriteBatch.Draw(slash, drawPos, src, edge, rot, slashOrigin,
                new Vector2(0.17f, 0.045f), SpriteEffects.None, 0f);
            return false;
        }
    }
}

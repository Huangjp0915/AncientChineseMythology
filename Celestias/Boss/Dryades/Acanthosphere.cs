using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 刺球弹幕 - 树精Boss的主要攻击弹幕
    /// 带有旋转效果和自然粒子的尖刺球
    /// </summary>
    public class Acanthosphere : ModProjectile
    {
        private float spinRotation;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 旋转
            spinRotation += Projectile.velocity.Length() * 0.04f;
            Projectile.rotation = spinRotation;

            // 轻微受重力影响（抛物线感）
            if (Projectile.velocity.Y < 16f)
                Projectile.velocity.Y += 0.08f;

            // 绿色粒子尾迹
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.JungleGrass,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    100, default, 1.1f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.05f);
        }

        public override void OnKill(int timeLeft) {
            // 消亡时碎片效果
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WoodFurniture, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    120, default, 1.3f);
                d.noGravity = false;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f),
                    80, default, 1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 绘制残影尾迹
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float alpha = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = lightColor * alpha * 0.4f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, trailRot, origin,
                    Projectile.scale * (0.7f + 0.3f * alpha), SpriteEffects.None, 0f);
            }

            // 主体绘制
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}

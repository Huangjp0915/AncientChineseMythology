using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥传送门 - Boss进出的传送门
    /// </summary>
    internal class NetherPortal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float PortalState => ref Projectile.ai[0]; // 0=展开中, 1=稳定, 2=收缩中
        private ref float PortalTimer => ref Projectile.ai[1];
        private ref float LinkedPortalWhoAmI => ref Projectile.ai[2]; // 关联的另一个传送门

        private float scale = 0f;
        private float rotation = 0f;
        private const float MaxScale = 2.5f;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI()
        {
            PortalTimer++;

            // 旋转效果
            rotation += 0.03f;

            switch (PortalState)
            {
                case 0: // 展开中
                    if (scale < MaxScale)
                    {
                        scale += 0.08f;
                        Projectile.alpha = Math.Max(0, 255 - (int)(scale / MaxScale * 255));
                    }
                    else
                    {
                        scale = MaxScale;
                        PortalState = 1;
                        Projectile.alpha = 0;
                    }
                    break;

                case 1: // 稳定状态
                    scale = MaxScale + MathF.Sin(PortalTimer * 0.1f) * 0.1f;
                    
                    // 120帧后开始收缩
                    if (PortalTimer > 120)
                    {
                        PortalState = 2;
                        PortalTimer = 0;
                    }
                    break;

                case 2: // 收缩中
                    if (scale > 0f)
                    {
                        scale -= 0.1f;
                        Projectile.alpha = Math.Min(255, (int)((1f - scale / MaxScale) * 255));
                    }
                    else
                    {
                        Projectile.Kill();
                    }
                    break;
            }

            // 粒子效果
            if (Main.rand.NextBool(2))
            {
                Vector2 offset = Main.rand.NextVector2Circular(60f, 60f) * scale;
                Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * 2f;
                
                int dust = Dust.NewDust(Projectile.Center + offset, 1, 1, DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = velocity;
                Main.dust[dust].fadeIn = 1.2f;
            }

            // 发光效果
            Lighting.AddLight(Projectile.Center, 0.3f * scale, 0.5f * scale, 0.8f * scale);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Underworld.Fog == null)
                return false;

            Texture2D fogTex = Underworld.Fog;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fogTex.Size() * 0.5f;

            Color portalColor = new Color(80, 120, 200);

            // 绘制多层旋转传送门效果
            for (int i = 0; i < 4; i++)
            {
                float layerScale = scale * (1f - i * 0.15f);
                float layerRotation = rotation + i * 0.5f;
                float layerAlpha = 0.4f - i * 0.08f;

                Main.spriteBatch.Draw(
                    fogTex,
                    drawPos,
                    null,
                    portalColor * layerAlpha,
                    layerRotation,
                    origin,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 中心发光核心
            Main.spriteBatch.Draw(
                fogTex,
                drawPos,
                null,
                Color.White * 0.3f,
                rotation * 2f,
                origin,
                scale * 0.4f,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        /// <summary>
        /// 开始收缩传送门
        /// </summary>
        public void StartClosing()
        {
            PortalState = 2;
            PortalTimer = 0;
        }
    }
}

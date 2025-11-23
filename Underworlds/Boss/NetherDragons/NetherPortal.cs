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

        private float scale = 0f;
        private float rotation = 0f;
        private float innerRotation = 0f;
        private const float MaxScale = 3.5f;
        
        // 能量环效果
        private float[] energyRings = new float[3];
        private float pulsePhase = 0f;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 150;
            Projectile.height = 150;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            
            // 初始化能量环
            for (int i = 0; i < energyRings.Length; i++)
            {
                energyRings[i] = i * MathHelper.TwoPi / energyRings.Length;
            }
        }

        public override void AI()
        {
            PortalTimer++;
            pulsePhase += 0.08f;

            // 旋转效果 - 双层反向旋转
            rotation += 0.04f;
            innerRotation -= 0.06f;
            
            // 更新能量环
            for (int i = 0; i < energyRings.Length; i++)
            {
                energyRings[i] += 0.05f;
            }

            switch (PortalState)
            {
                case 0: // 展开中 - 快速展开
                    if (scale < MaxScale)
                    {
                        scale += 0.15f;
                        Projectile.alpha = Math.Max(0, 255 - (int)(scale / MaxScale * 255));
                        
                        // 展开时的爆炸性粒子
                        if (Main.rand.NextBool(2))
                        {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                            int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.BlueTorch, 
                                velocity.X, velocity.Y, 100, Color.Cyan, 2f);
                            Main.dust[dust].noGravity = true;
                        }
                        
                        // 展开音效
                        if (PortalTimer % 3 == 0)
                        {
                            SoundEngine.PlaySound(SoundID.Item9 with { 
                                Volume = 0.3f, 
                                Pitch = scale / MaxScale 
                            }, Projectile.Center);
                        }
                    }
                    else
                    {
                        scale = MaxScale;
                        PortalState = 1;
                        Projectile.alpha = 0;
                        
                        // 完全展开时的爆发
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f }, Projectile.Center);
                        for (int i = 0; i < 30; i++)
                        {
                            float angle = i * MathHelper.TwoPi / 30f;
                            Vector2 velocity = angle.ToRotationVector2() * 6f;
                            int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.BlueTorch, 
                                velocity.X, velocity.Y, 100, Color.Cyan, 2.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    break;

                case 1: // 稳定状态 - 脉动效果
                    float pulse = MathF.Sin(pulsePhase) * 0.15f;
                    scale = MaxScale + pulse;
                    
                    // 持续的能量粒子流
                    if (Main.rand.NextBool(3))
                    {
                        Vector2 offset = Main.rand.NextVector2Circular(80f, 80f) * scale;
                        Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * 3f;
                        
                        int dust = Dust.NewDust(Projectile.Center + offset, 1, 1, DustID.BlueTorch, 
                            velocity.X, velocity.Y, 100, Color.Cyan, 1.8f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].fadeIn = 1.5f;
                    }
                    
                    // 稳定超过140帧后开始收缩
                    if (PortalTimer > 140)
                    {
                        PortalState = 2;
                        PortalTimer = 0;
                    }
                    break;

                case 2: // 收缩中 - 快速内爆
                    if (scale > 0f)
                    {
                        scale -= 0.18f;
                        Projectile.alpha = Math.Min(255, (int)((1f - scale / MaxScale) * 255));
                        
                        // 收缩时向内吸引粒子
                        if (Main.rand.NextBool())
                        {
                            Vector2 offset = Main.rand.NextVector2Circular(100f, 100f);
                            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * 4f;
                            
                            int dust = Dust.NewDust(Projectile.Center + offset, 1, 1, DustID.BlueTorch, 
                                velocity.X, velocity.Y, 100, Color.Cyan, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    else
                    {
                        // 完全收缩，最后的内爆效果
                        if (Main.netMode != NetmodeID.Server)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.BlueTorch, 
                                    velocity.X, velocity.Y, 100, Color.Cyan, 1.2f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f }, Projectile.Center);
                        Projectile.Kill();
                    }
                    break;
            }

            // 强化发光效果
            float lightIntensity = scale / MaxScale;
            Lighting.AddLight(Projectile.Center, 0.4f * lightIntensity, 0.7f * lightIntensity, 1.2f * lightIntensity);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Underworld.Fog == null)
                return false;

            Texture2D fogTex = Underworld.Fog;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fogTex.Size() * 0.5f;

            Color portalColor = new Color(80, 140, 220);
            Color coreColor = new Color(150, 200, 255);

            // 绘制外层光环
            for (int i = 0; i < 3; i++)
            {
                float ringScale = scale * (1.2f + i * 0.3f);
                float ringAlpha = 0.15f - i * 0.04f;
                
                Main.spriteBatch.Draw(
                    fogTex,
                    drawPos,
                    null,
                    portalColor * ringAlpha,
                    rotation + i * 0.3f,
                    origin,
                    ringScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 绘制能量环
            for (int i = 0; i < energyRings.Length; i++)
            {
                float angle = energyRings[i];
                float ringScale = scale * (0.8f + MathF.Sin(angle * 2f) * 0.2f);
                
                Main.spriteBatch.Draw(
                    fogTex,
                    drawPos,
                    null,
                    coreColor * 0.25f,
                    angle,
                    origin,
                    ringScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 绘制主传送门层 - 多层旋转效果
            for (int i = 0; i < 5; i++)
            {
                float layerScale = scale * (1f - i * 0.12f);
                float layerRotation = rotation * (1f + i * 0.2f);
                float layerAlpha = 0.5f - i * 0.08f;

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

            // 内层反向旋转
            for (int i = 0; i < 3; i++)
            {
                float layerScale = scale * (0.6f - i * 0.15f);
                float layerAlpha = 0.4f - i * 0.1f;
                
                Main.spriteBatch.Draw(
                    fogTex,
                    drawPos,
                    null,
                    coreColor * layerAlpha,
                    innerRotation + i * 0.5f,
                    origin,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 中心发光核心 - 脉动
            float corePulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.3f;
            Main.spriteBatch.Draw(
                fogTex,
                drawPos,
                null,
                Color.White * 0.5f,
                rotation * 3f,
                origin,
                scale * 0.3f * corePulse,
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

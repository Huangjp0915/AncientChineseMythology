using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 路径预警特效 - 显示弹幕将要划过的范围
    /// ai[0] = 目标X, ai[1] = 目标Y
    /// </summary>
    public class CelestialPathWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private Vector2 StartPos;
        private Vector2 EndPos;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.localAI[0] = 1;
                StartPos = Projectile.Center;
                EndPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            }

            // 淡入淡出
            if (Projectile.timeLeft > 40)
                Projectile.alpha = (int)MathHelper.Lerp(255, 50, (60 - Projectile.timeLeft) / 20f);
            else
                Projectile.alpha = (int)MathHelper.Lerp(50, 255, (40 - Projectile.timeLeft) / 40f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (StartPos == Vector2.Zero || EndPos == Vector2.Zero) return false;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 direction = EndPos - StartPos;
            float distance = direction.Length();
            float rotation = direction.ToRotation();

            Color color = Color.Gold * (1f - Projectile.alpha / 255f) * 0.5f;
            color.A = 0;

            // 绘制路径线
            int segments = (int)(distance / 30f);
            for (int i = 0; i <= segments; i++)
            {
                float progress = i / (float)segments;
                Vector2 pos = Vector2.Lerp(StartPos, EndPos, progress);

                // 闪烁效果
                float flicker = 0.7f + MathF.Sin((Main.GlobalTimeWrappedHourly * 10f + i * 0.5f)) * 0.3f;

                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, color * flicker,
                    rotation, tex.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            }

            // 在两端绘制更大的标记
            Main.EntitySpriteDraw(tex, StartPos - Main.screenPosition, null, color,
                0, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, EndPos - Main.screenPosition, null, color,
                0, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 闪电预警特效 - 竖直预警线
    /// </summary>
    public class CelestialLightningWarning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 600;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            if (Projectile.timeLeft > 60)
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, (90 - Projectile.timeLeft) / 30f);
            else
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, (60 - Projectile.timeLeft) / 60f);

            if (Projectile.timeLeft == 30 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0, 20f),
                    ModContent.ProjectileType<CelestialLightning>(), 70, 5f, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ACMAsset.LightShot;
            Color color = Color.Red * (1f - Projectile.alpha / 255f) * 0.6f;
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, new Vector2(0.15f, 3f), SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 金色闪电
    /// </summary>
    public class CelestialLightning : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 100;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.localAI[0] = 1;
                SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 8);
                        int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                            DustID.Electric, vel.X, vel.Y, 100, Color.Gold, 1.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            Projectile.alpha = (int)MathHelper.Lerp(100, 255, 1f - Projectile.timeLeft / 60f);

            // 拖尾粒子
            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ACMAsset.GlaciateWave;
            float scale = 0.3f + (1f - Projectile.timeLeft / 60f) * 0.2f;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.velocity.ToRotation(), tex.Size() / 2f, scale, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 金色剑气
    /// </summary>
    public class GoldenSwordAura : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ACMAsset.GlaciateWave;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, 0.2f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            if (!Main.dedServ)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 金色能量弹 - 辐射弹幕
    /// </summary>
    public class GoldenEnergy : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 400;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            Projectile.rotation += 0.1f;

            if (!Main.dedServ)
            {
                Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0f);

                if (Main.rand.NextBool(4))
                {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, 0, 0, 100, default, 0.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.3f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ACMAsset.LightShot;
            Color color = Color.Gold;
            color.A = 0;

            float scale = 0.7f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.1f;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            if (!Main.dedServ)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(2, 2);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 下落的金剑
    /// </summary>
    public class FallingSword : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 100;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.velocity.Y < 24f)
                Projectile.velocity.Y += 0.4f;

            if (!Main.dedServ && Main.rand.NextBool())
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ACMAsset.GlaciateWave;
            Color color = Color.Gold * (1f - Projectile.alpha / 255f);
            color.A = 0;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, new Vector2(tex.Width / 2f, tex.Height * 0.25f), 0.15f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 15; i++)
                {
                    Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }
}

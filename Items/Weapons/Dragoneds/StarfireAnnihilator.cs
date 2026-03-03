using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 星火湮灭狙击枪 —— 超级毕业狙击枪，珊瑚海洋主题，发射高速穿透珊瑚星火十弹
    /// </summary>
    public class StarfireAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 580;
            Item.DamageType = DamageClass.Ranged;
            Item.width  = 100;
            Item.height = 28;
            Item.useTime      = 50;
            Item.useAnimation = 50;
            Item.useStyle     = ItemUseStyleID.Shoot;
            Item.knockBack    = 18;
            Item.crit         = 30;
            Item.value        = Item.buyPrice(gold: 200);
            Item.rare         = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.notAmmo      = true;
            Item.shoot        = ModContent.ProjectileType<StarfireShell>();
            Item.shootSpeed   = 38f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item14, player.position);
            player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8f, 12);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class StarfireShell : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 22;
        }

        public override void SetDefaults() {
            Projectile.width  = 18;
            Projectile.height = 18;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 3;
            Projectile.timeLeft    = 260;
            Projectile.DamageType  = DamageClass.Ranged;
            Projectile.light       = 1.2f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 10;
        }

        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item74, Projectile.position);
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(20f, 30);
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<StarfireExplosion>(), 0, 0f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightShot;
            Texture2D sg  = ACMAsset.SoftGlow;

            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.80f;
                // 青珊瑚层
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(0, 200, 175) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.65f + i * 0.018f, 0.20f), SpriteEffects.None, 0);
                // 珊瑚内层
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(255, 100, 130) * (a * 0.45f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.30f, 0.10f), SpriteEffects.None, 0);
            }
            // 弹头主体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(60, 240, 210),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(1.10f, 0.26f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(0, 215, 185) * 0.92f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.65f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class StarfireExplosion : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/StarfireAnnihilator";

        public override void SetDefaults() {
            Projectile.width     = 10;
            Projectile.height    = 10;
            Projectile.friendly  = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft  = 70;
            Projectile.alpha     = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog  = 1f - Projectile.timeLeft / 70f;
            float alpha = MathHelper.SmoothStep(0.90f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 28f, ACMUtils.QuadOut(prog));
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D star  = ACMAsset.BlankStar;
            Texture2D sg    = ACMAsset.SoftGlow;
            Texture2D spark = ACMAsset.Sparkle;

            for (int k = 0; k < 4; k++) {
                // 青珊瑚层
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(0, 210, 185) * (alpha * 0.68f), k * MathHelper.PiOver2,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.55f, SpriteEffects.None, 0);
                // 珊瑚层
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 100, 130) * (alpha * 0.42f), k * MathHelper.PiOver2 + MathHelper.PiOver4,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.38f, SpriteEffects.None, 0);
            }
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(120, 240, 210) * (alpha * 1.2f),
                (float)Main.timeForVisualEffects * 0.020f,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                scale * 0.55f, SpriteEffects.None, 0);
            sb.Draw(spark, Projectile.Center - Main.screenPosition, null,
                new Color(80, 225, 200) * alpha,
                (float)Main.timeForVisualEffects * 0.015f,
                new Vector2(spark.Width * 0.5f, spark.Height * 0.5f),
                scale * 0.88f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(220, 255, 250) * alpha, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.30f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 暮光流华大弓 —— 超级毕业长弓，珊瑚海洋主题，发射珊瑚海洋之箔穿透敌人
    /// </summary>
    public class TwilightGlazeGreatbow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 515;
            Item.DamageType = DamageClass.Ranged;
            Item.width  = 30;
            Item.height = 80;
            Item.useTime      = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12;
            Item.crit  = 24;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.shoot = ModContent.ProjectileType<TwilightGlazeArrow>();
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item5, player.position);
            // 始终发射定制⧆珊瑚箭
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<TwilightGlazeArrow>(), damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class TwilightGlazeArrow : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width  = 12;
            Projectile.height = 12;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 4;
            Projectile.timeLeft    = 220;
            Projectile.DamageType  = DamageClass.Ranged;
            Projectile.light       = 0.9f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 6;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.velocity.Y += 0.10f; // 轻微重力
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            // 小型珊瑚光爆
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<TwilightGlazeImpact>(), 0, 0f, Projectile.owner);
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
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.68f;
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(0, 215, 185) * a, Projectile.oldRot[i] - MathHelper.PiOver2,
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.14f, 0.55f + i * 0.012f), SpriteEffects.None, 0);
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(255, 100, 130) * (a * 0.38f), Projectile.oldRot[i] - MathHelper.PiOver2,
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.08f, 0.32f), SpriteEffects.None, 0);
            }
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(60, 235, 200),
                Projectile.rotation - MathHelper.PiOver2,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.18f, 1.05f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(0, 220, 190) * 0.85f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.55f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class TwilightGlazeImpact : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/TwilightGlazeGreatbow";

        public override void SetDefaults() {
            Projectile.width     = 10;
            Projectile.height    = 10;
            Projectile.friendly  = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft  = 40;
            Projectile.alpha     = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog  = 1f - Projectile.timeLeft / 40f;
            float alpha = MathHelper.SmoothStep(0.85f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 10f, ACMUtils.QuadOut(prog));
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D sg    = ACMAsset.SoftGlow;
            Texture2D spark = ACMAsset.Sparkle;

            for (int k = 0; k < 4; k++) {
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(0, 210, 185) * (alpha * 0.60f), k * MathHelper.PiOver2 + MathHelper.PiOver4,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.35f, SpriteEffects.None, 0);
            }
            sb.Draw(spark, Projectile.Center - Main.screenPosition, null,
                new Color(80, 235, 205) * alpha,
                (float)Main.timeForVisualEffects * 0.015f,
                new Vector2(spark.Width * 0.5f, spark.Height * 0.5f),
                scale * 0.50f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(200, 255, 245) * alpha, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.18f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

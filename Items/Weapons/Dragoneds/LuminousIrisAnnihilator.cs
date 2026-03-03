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
    /// 耀虹湮灭手炮 —— 超级毕业手炮，金色闪耀主题，发射金色耀虹炮弹，命中引发漫天金光爆發
    /// </summary>
    public class LuminousIrisAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 550;
            Item.DamageType = DamageClass.Ranged;
            Item.width  = 80;
            Item.height = 30;
            Item.useTime      = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 15;
            Item.crit  = 20;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.notAmmo      = true;
            Item.shoot = ModContent.ProjectileType<LuminousIrisShell>();
            Item.shootSpeed = 22f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item92, player.position);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class LuminousIrisShell : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width  = 28;
            Projectile.height = 28;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 6;
            Projectile.timeLeft    = 180;
            Projectile.DamageType  = DamageClass.Ranged;
            Projectile.light       = 1.4f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 8;
        }

        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/NPC_Hit_1") { Volume = 1.4f }, Projectile.position);
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(18f, 28);
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<LuminousIrisExplosion>(), 0, 0f, Projectile.owner);
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
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.75f;
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(255, 200, 30) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.58f + i * 0.014f, 0.24f), SpriteEffects.None, 0);
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(255, 240, 150) * (a * 0.40f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.28f, 0.10f), SpriteEffects.None, 0);
            }
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 215, 60),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.95f, 0.30f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 210, 40) * 0.90f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.78f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class LuminousIrisExplosion : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/LuminousIrisAnnihilator";

        public override void SetDefaults() {
            Projectile.width     = 10;
            Projectile.height    = 10;
            Projectile.friendly  = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft  = 65;
            Projectile.alpha     = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog  = 1f - Projectile.timeLeft / 65f;
            float alpha = MathHelper.SmoothStep(0.92f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 26f, ACMUtils.QuadOut(prog));
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D star  = ACMAsset.BlankStar;
            Texture2D sg    = ACMAsset.SoftGlow;
            Texture2D spark = ACMAsset.Sparkle;
            Texture2D bolt  = ACMAsset.LightningBranch;

            // 四射展开辐射光爆
            for (int k = 0; k < 4; k++) {
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 185, 20) * (alpha * 0.68f), k * MathHelper.PiOver2,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.56f, SpriteEffects.None, 0);
            }
            // 闪电光芒
            for (int k = 0; k < 4; k++) {
                sb.Draw(bolt, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 220, 60) * (alpha * 0.55f), k * MathHelper.PiOver2 + MathHelper.PiOver4,
                    new Vector2(bolt.Width * 0.5f, bolt.Height),
                    new Vector2(0.38f, scale * 0.28f), SpriteEffects.None, 0);
            }
            sb.Draw(spark, Projectile.Center - Main.screenPosition, null,
                new Color(255, 210, 40) * alpha,
                (float)Main.timeForVisualEffects * 0.015f,
                new Vector2(spark.Width * 0.5f, spark.Height * 0.5f),
                scale * 0.82f, SpriteEffects.None, 0);
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(255, 230, 100) * (alpha * 1.15f),
                (float)Main.timeForVisualEffects * 0.020f,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                scale * 0.50f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 255, 200) * alpha, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.32f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

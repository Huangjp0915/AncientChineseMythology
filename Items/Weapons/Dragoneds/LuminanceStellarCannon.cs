using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 星光凝聚炮 —— 超级毕业手炮，白蓝科技主题，发射高速穿透的星光能射束
    /// </summary>
    public class LuminanceStellarCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 545;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 80;
            Item.height = 30;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10;
            Item.crit = 22;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.notAmmo = true;
            Item.shoot = ModContent.ProjectileType<LuminanceStellarShell>();
            Item.shootSpeed = 32f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item92, player.position);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class LuminanceStellarShell : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 200;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.2f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item89, Projectile.position);
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(10f, 18);
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<LuminanceStellarBurst>(), 0, 0f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;

            // 拖尾双层
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.72f;
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(100, 210, 255) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.60f + i * 0.016f, 0.22f), SpriteEffects.None, 0);
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(230, 248, 255) * (a * 0.42f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.32f, 0.10f), SpriteEffects.None, 0);
            }
            // 本体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(180, 235, 255),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(1.0f, 0.28f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(140, 220, 255) * 0.88f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.70f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class LuminanceStellarBurst : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/LuminanceStellarCannon";

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog = 1f - Projectile.timeLeft / 55f;
            float alpha = MathHelper.SmoothStep(0.90f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 18f, ACMUtils.QuadOut(prog));
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D light = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;

            // 8向细长光束放射扩散（交替深蓝/白蓝，基数方向更长）
            for (int k = 0; k < 8; k++) {
                float bAngle = k * MathHelper.Pi / 4f;
                bool cardinal = (k % 2 == 0);
                Color bColor = cardinal ? new Color(80, 195, 255) : new Color(200, 235, 255);
                float bLen = cardinal ? scale * 0.70f : scale * 0.45f;
                sb.Draw(light, Projectile.Center - Main.screenPosition, null,
                    bColor * (alpha * 0.88f),
                    bAngle,
                    new Vector2(light.Width * 0.5f, light.Height),
                    new Vector2(0.17f, bLen), SpriteEffects.None, 0);
            }
            // 外层柔光扩散环（蓝白）
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(100, 210, 255) * (alpha * 0.42f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.65f, SpriteEffects.None, 0);
            // 中心白核闪光（前半段高亮，后段迅速消退）
            float flashAlpha = MathHelper.SmoothStep(1.1f, 0f, prog * 1.5f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(245, 253, 255) * (alpha * flashAlpha), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.25f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

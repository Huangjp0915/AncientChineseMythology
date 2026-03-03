using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 幽穹虚空狙击枪 —— 超级毕业狙击枪，棕色科技主题，射出蚌豁虚空碴
    /// </summary>
    public class VaultshadeVoidshot : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 570;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 100;
            Item.height = 28;
            Item.useTime = 44;
            Item.useAnimation = 44;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 16;
            Item.crit = 26;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.notAmmo = true;
            Item.shoot = ModContent.ProjectileType<VaultshadeVolt>();
            Item.shootSpeed = 42f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item14, player.position);
            player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6f, 10);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class VaultshadeVolt : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 220;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.0f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        private float ScaleMult => MathHelper.Lerp(0.80f, 1.30f, 1f - Projectile.timeLeft / 220f);

        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6, Projectile.position);
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(18f, 25);
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<VaultshadeBlast>(), 0, 0f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            float sm = ScaleMult;
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;

            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.78f;
                float sc = MathHelper.Lerp(0.80f, 1.30f, 1f - (Projectile.timeLeft + i * 2f) / 220f);
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(100, 60, 160) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2((0.58f + i * 0.015f) * sc, 0.20f * sc), SpriteEffects.None, 0);
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(170, 130, 80) * (a * 0.38f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2((0.24f) * sc, 0.10f * sc), SpriteEffects.None, 0);
            }
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(120, 80, 200),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.95f * sm, 0.28f * sm), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(80, 50, 140) * 0.85f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.60f * sm, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    public class VaultshadeBlast : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/VaultshadeVoidshot";

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 65;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog = 1f - Projectile.timeLeft / 65f;
            // 内爆: 初始大 -> 内缩
            float alpha = MathHelper.SmoothStep(0.90f, 0f, prog);
            float scaleOuter = MathHelper.SmoothStep(24f, 2f, ACMUtils.QuadIn(prog));
            float scaleCore = MathHelper.SmoothStep(0f, 14f, ACMUtils.QuadOut(prog) * 0.5f);
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D bolt = ACMAsset.LightningBranch;

            // 紫色虚空履空而来
            for (int k = 0; k < 4; k++) {
                sb.Draw(bolt, Projectile.Center - Main.screenPosition, null,
                    new Color(100, 60, 180) * (alpha * 0.70f), k * MathHelper.PiOver2,
                    new Vector2(bolt.Width * 0.5f, bolt.Height),
                    new Vector2(0.30f, scaleOuter * 0.32f), SpriteEffects.None, 0);
            }
            // 暗色内爆核心
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(50, 20, 100) * (alpha * 1.2f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scaleCore * 0.45f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(160, 120, 240) * (alpha * 0.55f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scaleCore * 0.22f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

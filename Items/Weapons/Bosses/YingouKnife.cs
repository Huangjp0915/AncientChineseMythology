using AncientChineseMythology.NPCs.Boss.Yingous;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    public class YingouKnife : ModItem
    {
        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 342;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = 2000;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.shootSpeed = 8f;
            Item.shoot = ModContent.ProjectileType<SaberHellFriendly>();
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            player.itemLocation = player.GetPlayerStabilityCenter();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, Main.MouseWorld, velocity.GetNormalVector(), type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class SaberHellFriendly : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.velocity = Projectile.velocity.UnitVector();
            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) {
                    Projectile.localAI[1] = 30;
                }
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40) {
                    int num = 1000;
                    int num2 = 36;
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Main.projectile[proj].friendly = true;
                    Projectile.velocity *= -1;
                    proj = Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Main.projectile[proj].friendly = true;
                }
            }
            else {
                if (Projectile.localAI[1] > 0) {
                    Projectile.localAI[1]--;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D back = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            int width = 4400;
            int height = (int)(Projectile.localAI[0] * 3);
            float alpha = Projectile.localAI[1] / 60f;

            Rectangle rect = new Rectangle(-width / 2, -height / 2, width, height);
            Vector2 origin = new Vector2(rect.Width / 2, rect.Height / 2);
            Color drawColor = VaultUtils.MultiStepColorLerp(Projectile.localAI[0] / 40f, Color.Azure, Color.Red);
            Main.spriteBatch.Draw(back, drawPos, rect, drawColor with { A = 155 } * alpha
                , Projectile.velocity.ToRotation(), origin, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }
}

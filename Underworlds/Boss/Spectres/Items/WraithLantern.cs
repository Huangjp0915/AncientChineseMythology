using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres.Items
{
    /// <summary>
    /// 鬼火灯笼 — 怨灵可选掉落，魔法武器
    /// </summary>
    internal class WraithLantern : ModItem
    {
        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 118;
            Item.DamageType = DamageClass.Magic;
            Item.width = 36;
            Item.height = 42;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 18);
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.LostSoulFriendly;
            Item.shootSpeed = 10f;
            Item.mana = 12;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 2; i++) {
                float spread = (i - 0.5f) * 0.15f;
                Vector2 dir = toMouse.RotatedBy(spread);
                Projectile.NewProjectile(source, player.Center + dir * 24f, dir * Item.shootSpeed, type, damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}

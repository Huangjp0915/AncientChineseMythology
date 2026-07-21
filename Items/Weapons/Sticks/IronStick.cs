using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>铁棍: 三段连段 (第三段重抡落点冲击); 右键"撑杆跃" (2.5s 冷却, 无无敌帧)。</summary>
    public class IronStick : StickWeaponItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/IronStick";

        protected override int ComboLength => 3;
        protected override int AltCooldownFrames => 150;

        public override void SetDefaults() {
            Item.damage = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 48);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<IronStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<IronStickSpearProjectile_2>(), damage, knockback, player.whoAmI);
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<WoodenStick>(), 1);
            recipe.AddRecipeGroup(RecipeGroupID.IronBar, 81);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}

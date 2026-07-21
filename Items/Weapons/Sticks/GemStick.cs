using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>宝石棍: 三段连段, 命中绽棱光碎片; 右键"棱光回旋" (1s 冷却)。</summary>
    public class GemStick : StickWeaponItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/GemStick";

        protected override int ComboLength => 3;
        protected override int AltCooldownFrames => 60;

        public override void SetDefaults() {
            Item.damage = 68;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 88);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GemStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<GemStickSpearProjectile_2>(), damage, knockback * 0.7f, player.whoAmI);
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<GoldenStick>(), 1);
            recipe.AddIngredient(ItemID.Ruby, 10);
            recipe.AddIngredient(ItemID.Sapphire, 10);
            recipe.AddIngredient(ItemID.Emerald, 10);
            recipe.AddIngredient(ItemID.Topaz, 10);
            recipe.AddIngredient(ItemID.Amethyst, 10);
            recipe.AddIngredient(ItemID.Diamond, 10);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 20);
            recipe.AddTile(TileID.HeavyWorkBench);
            recipe.Register();
        }
    }
}

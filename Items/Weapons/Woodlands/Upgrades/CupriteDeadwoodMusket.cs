using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class CupriteDeadwoodMusket : ModItem
{
    public override void SetDefaults() {
        Item.damage = 40;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 44;
        Item.height = 20;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item11;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DeadwoodAcornProj>();
        Item.shootSpeed = 10f;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-6, 2);

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<DeadwoodMusket>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

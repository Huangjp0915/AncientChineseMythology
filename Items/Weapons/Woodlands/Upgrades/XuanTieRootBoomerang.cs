using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class XuanTieRootBoomerang : ModItem
{
    public override void SetDefaults() {
        Item.damage = 48;
        Item.crit = 8;
        Item.DamageType = DamageClass.Melee;
        Item.width = 30;
        Item.height = 30;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<RootBoomerangProj>();
        Item.shootSpeed = 12f;
    }

    public override bool CanUseItem(Player player) =>
        player.ownedProjectileCounts[ModContent.ProjectileType<RootBoomerangProj>()] < 1;

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<RootBoomerang>()
            .AddIngredient<XuanTieBar>(15)
            .AddIngredient<YaoQiFragment>(3)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

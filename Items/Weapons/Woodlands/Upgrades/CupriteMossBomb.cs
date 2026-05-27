using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class CupriteMossBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 45;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 24;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<MossBombProj>();
        Item.shootSpeed = 11f;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<MossBomb>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

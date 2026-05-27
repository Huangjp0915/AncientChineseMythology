using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class CupriteEmeraldTwigStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 36;
        Item.crit = 6;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<EmeraldTwigBolt>();
        Item.shootSpeed = 11f;
        Item.mana = 7;
        Item.staff[Type] = true;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<EmeraldTwigStaff>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
            .AddIngredient<YaoQiFragment>(4)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

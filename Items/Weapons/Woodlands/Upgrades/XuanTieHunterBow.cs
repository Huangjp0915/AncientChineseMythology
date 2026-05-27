using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class XuanTieHunterBow : ModItem
{
    public override void SetDefaults() {
        Item.damage = 38;
        Item.crit = 8;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 18;
        Item.height = 52;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ProjectileID.WoodenArrowFriendly;
        Item.shootSpeed = 9f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<VineHunterBow>()
            .AddIngredient<XuanTieBar>(15)
            .AddIngredient<YaoQiFragment>(3)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

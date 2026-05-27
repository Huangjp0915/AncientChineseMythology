using AncientChineseMythology.Items.Materials;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

public class CupriteNatureGrimoire : ModItem
{
    public override void SetDefaults() {
        Item.damage = 35;
        Item.crit = 8;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<NatureGrimoireLeaf>();
        Item.shootSpeed = 9f;
        Item.mana = 9;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        float spreadAngle = MathHelper.ToRadians(20);
        for (int i = -1; i <= 1; i++) {
            Vector2 leafVel = velocity.RotatedBy(spreadAngle * i) * Main.rand.NextFloat(0.9f, 1.1f);
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback, player.whoAmI,
                ai0: Main.rand.NextFloat(MathHelper.TwoPi));
        }
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<NatureGrimoire>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
            .AddIngredient<YaoQiFragment>(4)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

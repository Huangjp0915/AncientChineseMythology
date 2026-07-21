using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>
    /// 如意棍: 三段连段 + 每 5 挥"如意巨大化"横扫 (击落敌弹); 右键按住蓄力"定海神针" (三级, 松开砸落)。
    /// </summary>
    public class RuyiStick : StickWeaponItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/RuyiStick";

        protected override int ComboLength => 3;
        protected override int SpecialEvery => 5;
        protected override int SpecialStepIndex => 3;

        public override void SetDefaults() {
            Item.damage = 120;
            Item.DamageType = DamageClass.Melee;
            Item.width = 78;
            Item.height = 78;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 88);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RuyiStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        public override bool CanUseItem(Player player) {
            // 定海神针蓄力中不可再用
            if (player.ownedProjectileCounts[ModContent.ProjectileType<RuyiStickSpearProjectile_2>()] > 0)
                return false;
            return base.CanUseItem(player);
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<RuyiStickSpearProjectile_2>(), damage, knockback, player.whoAmI, 0f);
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<GemStick>(), 1);
            recipe.AddIngredient(ItemID.HellstoneBar, 81);
            recipe.AddIngredient(ModContent.ItemType<Cuprite.Cuprite>(), 49);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 40);
            recipe.AddTile(TileID.Hellforge);
            recipe.Register();
        }
    }
}

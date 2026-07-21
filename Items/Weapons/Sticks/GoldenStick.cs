using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>金棍: 三段连段 + 每 4 挥金光三连突; 右键掷棍如意 (掷出期间棍不在手, 不可用)。</summary>
    public class GoldenStick : StickWeaponItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/GoldenStick";

        protected override int ComboLength => 3;
        protected override int SpecialEvery => 4;
        protected override int SpecialStepIndex => 3;

        public override void SetDefaults() {
            Item.damage = 48;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 48);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GoldenStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        public override bool CanUseItem(Player player) {
            // 棍已掷出 → 不在手, 左右键都不可用
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GoldenStickSpearProjectile_2>()] > 0)
                return false;
            return base.CanUseItem(player);
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            // 目标距离 owner 端算好经 ai[0] 同步; 掷出瞬间反向后坐
            float dist = MathHelper.Clamp(Vector2.Distance(player.MountedCenter, Main.MouseWorld), 80f, 480f);
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<GoldenStickSpearProjectile_2>(), damage, knockback * 0.6f, player.whoAmI, dist);
            player.velocity -= velocity.SafeNormalize(Vector2.Zero) * 2f;
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<IronStick>(), 1);
            recipe.AddIngredient(ItemID.GoldBar, 81);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 10);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}

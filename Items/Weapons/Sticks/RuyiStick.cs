using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    public class RuyiStick : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Sticks/RuyiStick";
        public int attackType = 0; // 记录当前攻击类型
        public int comboExpireTimer = 0; // 当武器在一定时间内未使用时重置攻击模式
        public override Color? GetAlpha(Color lightColor) { return Color.White; }

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults() {
            // 物品基础属性（这里的值只是“默认”）
            Item.damage = 120;                 // 默认伤害
            Item.DamageType = DamageClass.Melee;
            Item.width = 78;
            Item.height = 78;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.knockBack = 7f;             // 默认击退
            Item.value = Item.buyPrice(gold: 88);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;

            // 默认设定为长矛刺击（左键）
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RuyiStickSpearProjectile>();
            Item.shootSpeed = 1f;
        }

        // 启用右键备用功能
        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void UpdateInventory(Player player) {
            if (comboExpireTimer++ >= 120) // 在库存中存放 120 个 ticks（== 2 秒）后，重置攻击模式
            {
                attackType = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) // 右键射击
            {
                if (comboExpireTimer < 120)
                    Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RuyiStickSpearProjectile_2>(), damage, knockback, Main.myPlayer, attackType);
                else {
                    Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RuyiStickSpearProjectile>(), damage, knockback, Main.myPlayer, attackType);
                    attackType = (attackType + 1) % 2; // 增加攻击类型以确保下一个挥动不同
                    comboExpireTimer = 0; // 每次使用武器时重置计时器，以便组合不会过期
                }

                return false;
            }
            else if (!Main.mouseRight) {
                if (RuyiStickSpearProjectile_2.isA) {
                    Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RuyiStickSpearProjectile_3>(), damage, knockback, Main.myPlayer, attackType);
                    RuyiStickSpearProjectile_2.isA = false;
                }
                else {
                    Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RuyiStickSpearProjectile>(), damage, knockback, Main.myPlayer, attackType);
                    attackType = (attackType + 1) % 3; // 增加攻击类型以确保下一个挥动不同
                    comboExpireTimer = 0; // 每次使用武器时重置计时器，以便组合不会过期
                }

                return false;
            }
            return false; // 返回 false 以防止原始投射物被发射
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

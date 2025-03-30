using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class GemStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults()
        {
            // 物品基础属性（这里的值只是“默认”）
            Item.damage = 25;                 // 默认伤害
            Item.DamageType = DamageClass.Melee;
            Item.width = 40; 
            Item.height = 40;
            Item.useTime = 25; 
            Item.useAnimation = 25;
            Item.knockBack = 8f;             // 默认击退
            Item.value = Item.buyPrice(silver: 10);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;

            // 默认设定为长矛刺击（左键）
            Item.useStyle = ItemUseStyleID.Shoot; 
            Item.noUseGraphic = true;        
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GemStickSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        // 启用右键备用功能
        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        // 根据左键/右键 切换不同的攻击参数
        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键：挥砍（伤害高、击退低）
                Item.useStyle = ItemUseStyleID.Swing;   // 普通近战挥砍
                Item.useTime = 25;
                Item.useAnimation = 25;
                Item.noUseGraphic = false; // 显示物品贴图
                Item.noMelee = false;      // 直接近战判定
                Item.shoot = ProjectileID.None; // 不发射投射物

                // 设置右键的伤害和击退
                Item.damage = 32;
                Item.knockBack = 2f;
            }
            else
            {
                // 左键：长矛刺击（伤害低、击退高）
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.useTime = 25;
                Item.useAnimation = 25;
                Item.noUseGraphic = true;
                Item.noMelee = true;
                Item.shoot = ModContent.ProjectileType<GemStickSpearProjectile>();
                Item.shootSpeed = 3.5f;

                // 设置左键的伤害和击退
                Item.damage = 25;
                Item.knockBack = 8f;
            }
            return base.CanUseItem(player);
        }

        public override void AddRecipes()
        {
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

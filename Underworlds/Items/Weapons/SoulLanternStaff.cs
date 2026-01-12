using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 魂灯杖 - 引导亡魂的灯笼法杖，召唤武器
    /// 肉后初期，召唤幽灵灯笼为你作战
    /// </summary>
    public class SoulLanternStaff : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults() {
            Item.damage = 35; //基础伤害
            Item.DamageType = DamageClass.Summon; //召唤伤害类型
            Item.mana = 10; //魔力消耗
            Item.width = 42; //物品宽度
            Item.height = 42; //物品高度
            Item.useTime = 30; //使用时间
            Item.useAnimation = 30; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 2f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item44; //召唤声音
            Item.autoReuse = false; //不自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ProjectileID.LostSoulFriendly; //召唤友善亡魂
            Item.shootSpeed = 8f; //弹幕速度
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //在鼠标位置召唤
            position = Main.MouseWorld;
            velocity = Vector2.Zero;

            //召唤亡魂
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (proj >= 0) {
                Main.projectile[proj].originalDamage = Item.damage;
            }

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }
}

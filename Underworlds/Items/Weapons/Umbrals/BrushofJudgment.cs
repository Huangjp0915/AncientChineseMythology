using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 判官笔 - 地府判官用于判定生死的神笔，魔法武器
    /// 肉后初期，发射追踪的判官符印弹幕
    /// </summary>
    public class BrushofJudgment : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 48; //基础伤害
            Item.crit = 4; //暴击率
            Item.DamageType = DamageClass.Magic; //魔法伤害类型
            Item.mana = 8; //魔力消耗
            Item.width = 36; //物品宽度
            Item.height = 36; //物品高度
            Item.useTime = 22; //使用时间
            Item.useAnimation = 22; //使用动画时间
            Item.useStyle = ItemUseStyleID.Shoot; //射击风格
            Item.knockBack = 3f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item8; //魔法使用声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ProjectileID.LostSoulFriendly; //发射友善亡魂弹幕
            Item.shootSpeed = 12f; //弹幕速度
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //发射3发散射弹幕
            int numberProjectiles = 3;
            for (int i = 0; i < numberProjectiles; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return false; //不使用默认发射
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //从笔尖位置发射
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 20f;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }
}

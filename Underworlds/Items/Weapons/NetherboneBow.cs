using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons
{
    /// <summary>
    /// 冥骨弓 - 由地府亡灵骨骼制成的弓，远程弓类武器
    /// 肉后初期，发射的箭矢带有冥火效果
    /// </summary>
    public class NetherboneBow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 42; //基础伤害
            Item.crit = 6; //暴击率
            Item.DamageType = DamageClass.Ranged; //远程伤害类型
            Item.width = 24; //物品宽度
            Item.height = 56; //物品高度
            Item.useTime = 22; //使用时间
            Item.useAnimation = 22; //使用动画时间
            Item.useStyle = ItemUseStyleID.Shoot; //射击风格
            Item.knockBack = 2.5f; //击退
            Item.value = Item.buyPrice(gold: 4, silver: 50); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item5; //弓箭声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ProjectileID.WoodenArrowFriendly; //默认发射木箭
            Item.shootSpeed = 10f; //弹幕速度
            Item.useAmmo = AmmoID.Arrow; //使用箭矢弹药
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0); //手持位置微调
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //将普通箭转换为地狱火箭
            if (type == ProjectileID.WoodenArrowFriendly) {
                type = ProjectileID.HellfireArrow;
            }
            //发射时有几率发射额外一支箭
            if (Main.rand.NextBool(3)) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(8));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return true;
        }
    }
}

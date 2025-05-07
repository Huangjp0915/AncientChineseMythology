using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using AncientChineseMythology.Projectiles;
using AncientChineseMythology.Buffs;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    public class BlackBearStaff : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/BlackBearStaff"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults()
        {
            Item.damage = 25; // 基础伤害
            Item.crit = 3; // 暴击率
            Item.DamageType = DamageClass.Summon; // 伤害类型
            Item.width = 40; // 宽度
            Item.height = 28; // 高度
            Item.useTime = 20; // 使用时间
            Item.useAnimation = 20; // 使用动画
            Item.useStyle = ItemUseStyleID.Swing; // 使用方式
            Item.knockBack = 6; // 击退距离
            Item.value = Item.buyPrice(0, 30, 0, 0); // 物品价值
            Item.rare = ItemRarityID.Green; // 稀有度
            Item.UseSound = SoundID.Item100; // 使用音效
            Item.autoReuse = true; // 自动重用
            Item.noUseGraphic = false; // 确保武器图形显示
            Item.mana = 10; // 使用时消耗的魔力值
            Item.noMelee = true; // 无法近战
            Item.shoot = ModContent.ProjectileType<BlackBearStaffProj1>(); // 射击类型
            Item.shootSpeed = 1f; // 射击速度
            Item.buffType = ModContent.BuffType<BuffsBlackBearStaff>(); // 召唤物品的buff类型
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 检查当前召唤物的数量
            int summonCount = player.ownedProjectileCounts[type];
            if (summonCount >= 1)
            {
                return false;
            }

            // 给予玩家BUFF保证召唤物存活
            player.AddBuff(Item.buffType, 3);

            // 召唤物需要设置originalDamage
            var projectile = Projectile.NewProjectileDirect(source, Main.MouseWorld, velocity, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            // 返回false阻止原版发射
            return false;
        }
    }
}

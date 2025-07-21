using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bows
{
    public class BlackBearBow : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Bows/BlackBearBow"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Item.damage = 20;
            Item.crit = 6;
            Item.DamageType = DamageClass.Ranged; // 远程
            Item.width = 22;
            Item.height = 64;
            Item.useTime = 15;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3;
            Item.value = Item.buyPrice(0, 30, 0, 0); // 物品价值
            Item.rare = ItemRarityID.Green; // 稀有度
            Item.noMelee = true; // 无法近战
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true; // 自动使用
            Item.noUseGraphic = false; // 显示使用动画
            Item.shoot = ModContent.ProjectileType<Projectiles.BlackBearBowProj1>(); // 射击类型，发射自定义的弹幕
            Item.shootSpeed = 10f; // 发射速度
            Item.useAmmo = AmmoID.Arrow; // 指定使用的弹药类型（箭）
        }
        public override void HoldItem(Player player) {
            //player.AddBuff(ModContent.BuffType<Buffs.BuffsBoss2_2Gun>(), 30, true);
        }
        public override Vector2? HoldoutOffset() {
            return new Vector2(5, 0); //手持位置偏移
        }
        public override bool CanUseItem(Player player) {
            // 检查是否有足够的弹药
            return player.active && player.HasAmmo(Item); // 直接检查是否有弹药
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //获取射击方向
            // 发射时消耗弹药
            if (player.HasAmmo(Item)) // 检查玩家是否有弹药
            {
                player.ConsumeItem(Item.useAmmo); // 消耗弹药
            }

            // 计算鼠标位置相对于玩家中心的角度
            Vector2 mousePosition = Main.MouseWorld;
            Vector2 direction = mousePosition - player.Center;

            // 计算生成位置在圆上的坐标
            float radius = 20f;
            Vector2 spawnPosition = player.Center + direction.SafeNormalize(Vector2.Zero) * radius + new Vector2(0, 0f);

            // 生成射出的弹幕
            int projectileType = ModContent.ProjectileType<Projectiles.BlackBearBowProj1>();
            Projectile.NewProjectile(source, spawnPosition, velocity, projectileType, damage, knockback, player.whoAmI);

            return true; // 阻止默认发射
        }
    }
}

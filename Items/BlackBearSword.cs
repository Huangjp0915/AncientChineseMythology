using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System.Linq;
using AncientChineseMythology.Projectiles;

namespace AncientChineseMythology.Items
{
    public class BlackBearSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/BlackBearSword"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults()
        {
            Item.damage = 32; // 基础伤害
            Item.crit = 8; // 爆击率
            Item.DamageType = DamageClass.Melee; // 伤害类型
            Item.width = 64; // 物品宽度
            Item.height = 64; // 物品高度
            Item.useTime = 25; // 使用时间
            Item.useAnimation = 25; // 使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; // 使用风格
            Item.knockBack = 6; // 击退
            Item.value = Item.buyPrice(0, 0, 0, 16); // 物品价值
            Item.rare = ItemRarityID.Green; // 稀有度
            Item.UseSound = SoundID.Item1; // 使用声音
            Item.autoReuse = true; // 自动使用
            Item.noUseGraphic = false; // 显示使用图标
            Item.shoot = ModContent.ProjectileType<BlackBearSwordProj1>(); // 射击类型
            Item.shootSpeed = 16;
        }
    }
}

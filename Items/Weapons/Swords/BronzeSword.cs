using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Waapons.Swords
{
    public class BronzeSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/BronzeSword"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Item.damage = 8; // 基础伤害
            Item.crit = 15; // 爆击率
            Item.DamageType = DamageClass.Melee; // 伤害类型
            Item.width = 52; // 物品宽度
            Item.height = 52; // 物品高度
            Item.useTime = 10; // 使用时间
            Item.useAnimation = 10; // 使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; // 使用风格
            Item.knockBack = 0; // 击退
            Item.value = Item.buyPrice(0, 0, 0, 0); // 物品价值
            Item.rare = ItemRarityID.Green; // 稀有度
            //Item.UseSound = SoundID.Item1; // 使用声音
            //Item.useTurn = true; // 自动转向
            Item.autoReuse = true; // 自动使用
            Item.noUseGraphic = false; // 显示使用图标
            Item.shoot = ModContent.ProjectileType<BlankProjectile>(); // 射击类型
            Item.shootSpeed = 1f; // 射击速度
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.Poisoned)) {
                target.AddBuff(BuffID.Poisoned, 180); // 给目标添加中毒状态
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            if (Main.rand.NextBool(100)) {
                // 秒杀 NPC
                target.life = 0;
                target.HitEffect();
                target.checkDead();
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BronzeIngot>(), 18)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

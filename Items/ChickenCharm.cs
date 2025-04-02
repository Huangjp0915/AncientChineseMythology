using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class ChickenCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/ChickenCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 赋予无限飞行时间
            player.wingTime = int.MaxValue;
            // 防止坠落伤害
            player.noFallDmg = true;
            
            // 控制垂直运动
            if (player.controlUp || player.controlJump)
            {
                // 向上飞行：提升速度到 -12f
                player.velocity.Y = -12f;
            }
            else if (player.controlDown)
            {
                // 向下飞行：提升速度到 12f
                player.velocity.Y = 12f;
                // 模拟平台下穿：尝试将玩家位置向下移动 4 像素，但仅在不会碰撞到实心方块的情况下
                Vector2 newPos = player.position + new Vector2(0, 4f);
                if (!Collision.SolidCollision(newPos, player.width, player.height))
                {
                    player.position = newPos;
                }
            }
            else
            {
                // 悬浮时保持垂直速度 0
                player.velocity.Y = 0f;
            }

            // 持续添加鸡符咒专属 Buff（持续2 tick，每帧刷新）
            player.AddBuff(ModContent.BuffType<ChickenCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // 将物品以50%缩放绘制在世界中
            float customScale = 0.5f;
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            Vector2 drawPosition = Item.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            spriteBatch.Draw(texture, drawPosition, null, lightColor, rotation, origin, customScale, SpriteEffects.None, 0f);
            return false;
        }
    }
}

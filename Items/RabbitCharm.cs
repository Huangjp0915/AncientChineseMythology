using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class RabbitCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/RabbitCharm";

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
            // 将最高奔跑速度设为 15
            player.maxRunSpeed = 15f;
            // 提高加速速度，让玩家能更快达到最高速度
            player.runAcceleration += 10f;
            // 增加移动速度倍率（此处增加30%的额外移动速度）
            player.moveSpeed += 0.3f;

            // 当玩家没有按左右方向键时，立即将水平速度归零
            if (!player.controlLeft && !player.controlRight)
            {
                player.velocity.X = 0f;
            }

            player.AddBuff(ModContent.BuffType<RabbitCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 重写该方法以调整物品在世界中的绘制大小
        public override bool PreDrawInWorld(
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            // 让物品在地上时，绘制更小
            float customScale = 0.5f;

            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            Vector2 origin = texture.Size() * 0.5f;

            // 手动绘制
            spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                lightColor,
                rotation,
                origin,
                customScale,
                SpriteEffects.None,
                0f
            );
            
            return false;
        }
    }
}

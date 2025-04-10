using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class SnakeCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/SnakeCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.scale = 0.4f;
            // 此物品作为使用类物品（类似药剂或武器）
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.consumable = false;
            Item.rare = ItemRarityID.Red;
            // 允许右键使用
            Item.autoReuse = false;
        }

        // 允许 alt 功能（右键使用）
        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        // 根据按键使用效果不同
        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键使用：解除隐身（清除对应 Buff）
                player.ClearBuff(ModContent.BuffType<SnakeInvisibilityBuff>());
            }
            else
            {
                // 左键使用：赋予无限隐身，使用 int.MaxValue 作为时长
                player.AddBuff(ModContent.BuffType<SnakeInvisibilityBuff>(), int.MaxValue);
            }
            return true;
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

            // 如果你想手动绘制，可以这样做：
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // 以物品中心为基准进行绘制
            Vector2 drawPosition = Item.Center - Main.screenPosition;

            // 如果需要让它贴得更紧一点，可以手动往下移动
            // 例如：drawPosition.Y += 2f;

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

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }
}

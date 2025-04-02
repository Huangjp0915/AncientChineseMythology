using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class DogCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/DogCharm";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 10);
            // 设置为最高稀有度（红色）
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // Terraria 的生命回复计算方式：lifeRegen 每秒回复的生命为 lifeRegen/2
            // 因此这里增加 100，即每秒回复 50 点生命
            player.lifeRegen += 100;
            
            // 增加魔力回复效果
            player.manaRegenBonus += 50;

            // 刷新 DogCharmBuff（持续2帧刷新，使其不会消失）
            player.AddBuff(ModContent.BuffType<DogCharmBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<StrangeStone>(), 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

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

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }
}

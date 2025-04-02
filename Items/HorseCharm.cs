using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class HorseCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/HorseCharm";

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
            // 移除玩家当前所有的 debuff
            for (int i = player.buffType.Length - 1; i >= 0; i--)
            {
                int buffID = player.buffType[i];
                if (buffID > 0 && Main.debuff[buffID])
                {
                    player.DelBuff(i);
                }
            }
            
            // 设置所有 debuff 类型的免疫标记为 true
            // buffImmune 数组的长度通常覆盖了所有可能的 buff
            for (int i = 0; i < player.buffImmune.Length; i++)
            {
                if (Main.debuff[i])
                {
                    player.buffImmune[i] = true;
                }
            }

            player.AddBuff(ModContent.BuffType<HorseCharmBuff>(), 2);
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

            // 返回 false 表示“我已经手动完成了绘制，不再用默认逻辑绘制”
            return false;
        }
    }
}

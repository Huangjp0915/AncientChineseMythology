using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class CowCharm : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/CowCharm";

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
            // 增加防御力
            player.statDefense += 40;
            // 增加所有伤害80%
            player.GetDamage(DamageClass.Generic) += 0.8f;

            player.AddBuff(ModContent.BuffType<CowCharmBuff>(), 2);
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

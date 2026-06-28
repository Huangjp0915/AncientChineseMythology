using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs.Items
{
    /// <summary>
    /// 傲世神木 - 大椿Boss掉落的材料物品
    /// 悬浮在半空中，自带发光
    /// </summary>
    public class ArrogantDivineSylvan : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Purple;
        }

        public override void PostUpdate() {
            // 悬浮效果
            Item.velocity = Vector2.Zero;
            float sinValue = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 8f);
            Item.velocity.Y = sinValue * 0.1f;

            // 自发光（翠绿偏金色）
            Lighting.AddLight(Item.Center, 0.4f, 0.55f, 0.2f);
        }
    }
}

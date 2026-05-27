using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>四象碑 — 四圣兽召唤物占位（§5.4 Phase 2 stub）。</summary>
    public class FourSymbolsTablet : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 45;
            Item.consumable = false;
            Item.value = Item.buyPrice(platinum: 5);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item4;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.LunarTabletFragment;
    }
}

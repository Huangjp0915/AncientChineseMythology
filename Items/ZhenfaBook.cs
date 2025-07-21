using AncientChineseMythology.Systems;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class ZhenfaBook : ModItem
    {
        // 直接使用 vanilla Book (ID 149) 的贴图
        public override string Texture => "Terraria/Images/Item_149";

        public override void SetStaticDefaults() {
            CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 1;
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 30;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 99, 0, 0);
        }

        public override bool? UseItem(Player player) {
            // 切换 UI
            if (player.whoAmI == Main.myPlayer) {
                ZhenfaUISystem.ToggleBookUI();
            }
            return true;
        }
    }
}

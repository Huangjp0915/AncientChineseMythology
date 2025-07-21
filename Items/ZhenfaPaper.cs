using AncientChineseMythology.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class ZhenfaPaper : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/ZhenfaPaper";

        public override void SetStaticDefaults() {
            CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 25;
        }

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 999;
            Item.rare = ItemRarityID.Blue;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 9, 99, 99);
        }

        public override bool CanUseItem(Player player) {
            // 没有百科全书就不能用
            if (!player.HasItem(ModContent.ItemType<ZhenfaBook>()))
                return false;

            // 若已全部解锁，提示一次并阻止使用
            var modPlr = player.GetModPlayer<ZhenfaPlayer>();
            if (modPlr.DiscoveredRecipes.Count >= ZhenfaRecipeCatalog.AllRecipes.Count) {
                // 只在点击第一帧发送提示，避免刷屏
                if (player.whoAmI == Main.myPlayer && player.itemAnimation == 0)
                    Main.NewText("百科全书中的阵法已经全部解锁！", Color.OrangeRed);

                return false;
            }

            return true;
        }

        public override bool? UseItem(Player player) {
            // 只有能用时才会走到这里，因此必定解锁成功
            player.GetModPlayer<ZhenfaPlayer>().DiscoverRandomRecipe();
            return true; // 正常消耗
        }
    }
}
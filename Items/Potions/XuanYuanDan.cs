using AncientChineseMythology.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Potions
{
    public class XuanYuanDan : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Potions/XuanYuanDan";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 28;
            Item.maxStack = 30;
            Item.useStyle = ItemUseStyleID.EatFood;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.consumable = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 50);
        }

        public override bool? UseItem(Player player) {
            var mp = player.GetModPlayer<MythologyPlayer>();
            // 给固定经验值，也可根据当前 Major/Minor 调整
            const int ExpAmount = 500;
            mp.AddStageExp(ExpAmount);
            Main.NewText($"获得了 {ExpAmount} 点修炼经验", 50, 255, 50);
            return true;
        }
    }
}
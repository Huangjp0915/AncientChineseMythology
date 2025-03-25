using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SubworldLibrary;
using AncientChineseMythology.Subworlds; // 引入子世界所在命名空间

namespace AncientChineseMythology.Items
{
    public class SkyKey : ModItem
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.consumable = false; // 使用后消耗
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Yellow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldenKey;

        public override bool? UseItem(Player player)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                // 进入 ThirtyThreeHeavens 子世界
                SubworldSystem.Enter<ThirtyThreeHeavens>();
            }
            return true;
        }
    }
}
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
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
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

        public override string Texture => "AncientChineseMythology/Textures/Items/SkyKey";
    }
}
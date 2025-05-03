using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class StrangeStone : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/StrangeStone";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 99;
            Item.scale = 0.1f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Green;
        }
    }
}

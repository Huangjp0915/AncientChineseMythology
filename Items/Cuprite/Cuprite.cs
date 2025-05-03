using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class Cuprite : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/Cuprite";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = 1000;
            Item.rare = ItemRarityID.White;
        }
    }
}

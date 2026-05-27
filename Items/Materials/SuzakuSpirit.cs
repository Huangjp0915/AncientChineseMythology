using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class SuzakuSpirit : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 250);
            Item.rare = ItemRarityID.Red;
        }

        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/QingLongSpirit";
    }
}

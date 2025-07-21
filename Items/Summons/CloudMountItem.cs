using AncientChineseMythology.Mounts;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Summons
{

    public class CloudMountItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Summons/CloudMountItem";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 20;
            Item.rare = ItemRarityID.Blue;
            Item.noMelee = true;
            Item.mountType = ModContent.MountType<CloudMount>();
        }
    }
}
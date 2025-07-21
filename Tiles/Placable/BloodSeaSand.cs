using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class BloodSeaSand : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/BloodSeaSand";
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileSand[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;

            AddMapEntry(new Color(180, 20, 20));
            DustType = DustID.Blood;

            // 正确的掉落注册方式
            RegisterItemDrop(ModContent.ItemType<Items.Placable.BloodSeaSand>());
        }
    }
}

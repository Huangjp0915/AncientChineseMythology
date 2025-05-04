using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class FloatingBasalt : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/FloatingBasalt";

        public override void SetStaticDefaults()
        {
            // 完全克隆 Stone 的属性
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            TileID.Sets.Conversion.Stone[Type] = false;  // 受腐化/神圣转换
            DustType = DustID.Stone;
            HitSound = SoundID.Tink;
            RegisterItemDrop(ModContent.ItemType<Items.Placable.FloatingBasalt>());

            AddMapEntry(new Microsoft.Xna.Framework.Color(50, 50, 60), CreateMapEntryName());
        }
    }
}
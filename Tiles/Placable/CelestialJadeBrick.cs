using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class CelestialJadeBrick : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/CelestialJadeBrick";

        public override void SetStaticDefaults() {

            //复制 BlueBrick 的全部属性
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            TileID.Sets.CanBeDugByShovel[Type] = TileID.Sets.CanBeDugByShovel[TileID.AncientBlueBrick];

            //让锤子可以雕刻出多种形状
            DustType = DustID.BlueCrystalShard;
            HitSound = SoundID.Tink;
            RegisterItemDrop(ModContent.ItemType<Items.Placable.CelestialJadeBrick>());
            AddMapEntry(new Microsoft.Xna.Framework.Color(50, 50, 60), CreateMapEntryName());
        }
    }
}
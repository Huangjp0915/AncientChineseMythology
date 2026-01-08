using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Underworlds.Tiles
{
    /// <summary>
    /// 幽冥锭方块
    /// </summary>
    public class NetherBarTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileShine[Type] = 1100;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(100, 150, 255), name);

            DustType = DustID.BlueTorch;
            HitSound = SoundID.Tink;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.15f;
            g = 0.25f;
            b = 0.5f;
        }
    }
}

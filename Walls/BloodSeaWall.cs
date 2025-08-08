using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Walls
{
    public class BloodSeaWall : ModWall
    {
        public override string Texture => "AncientChineseMythology/Textures/Walls/BloodSeaWall";
        public override void SetStaticDefaults() {
            Main.wallHouse[Type] = false;  //不计作可居住
            AddMapEntry(new Color(160, 0, 0));
            DustType = DustID.Blood;
        }
    }
}

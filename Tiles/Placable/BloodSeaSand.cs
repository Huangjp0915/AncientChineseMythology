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
            //刻意不设 tileSand：地下血海盆地的外壳/顶部需要稳定不塌落的固体
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = true;

            AddMapEntry(new Color(180, 20, 20));
            DustType = DustID.Blood;

            //正确的掉落注册方式
            RegisterItemDrop(ModContent.ItemType<Items.Placable.BloodSeaSand>());
        }
    }
}

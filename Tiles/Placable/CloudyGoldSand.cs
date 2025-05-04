using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class CloudyGoldSand : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/CloudyGoldSand";
        public override void SetStaticDefaults()
        {
            // 克隆 Dirt 的基本属性
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlockLight[Type] = false;

            // 允许草生长 / 生态转换
            TileID.Sets.Conversion.Dirt[Type] = true;   // 受神圣 / 腐化 / 真菌转换
            TileID.Sets.Conversion.Grass[Type] = true;  // 草可以爬上来

            DustType = DustID.GoldCoin;
            HitSound = SoundID.Dig;
            RegisterItemDrop(ModContent.ItemType<Items.Placable.CloudyGoldSand>());

            AddMapEntry(new Microsoft.Xna.Framework.Color(190, 160, 80), CreateMapEntryName());
        }
        public override void RandomUpdate(int i, int j)
        {
            // 上方必须为空 & 光照充足
            if (!WorldGen.SolidTile(i, j - 1) &&
                Main.tile[i, j - 1].LiquidAmount == 0 &&
                Lighting.Brightness(i, j) > 0.3f)
            {
                WorldGen.PlaceTile(i, j, ModContent.TileType<CloudyGoldGrass>(),
                                   mute: true, forced: true);
            }
        }
    }
}
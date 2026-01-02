using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Tiles
{
    /// <summary>
    /// 幽冥矿石方块 - 幽冥龙死亡后在地府生成
    /// </summary>
    public class NetherOreTile : ModTile
    {
        public override void SetStaticDefaults() {
            TileID.Sets.Ore[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 800; // 高优先级，类似叶绿矿
            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 975;
            Main.tileMergeDirt[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(80, 120, 200), name);

            DustType = DustID.BlueTorch;
            HitSound = SoundID.Tink;
            MineResist = 4f;
            MinPick = 200; // 需要镐力200以上（斧钻或更高）
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            // 幽冥蓝色微光
            r = 0.1f;
            g = 0.2f;
            b = 0.4f;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
        }
    }
}

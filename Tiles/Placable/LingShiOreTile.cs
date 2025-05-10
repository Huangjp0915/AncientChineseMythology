// AncientChineseMythology/Tiles/Placable/LingShiOreTile.cs
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.DataStructures;
using AncientChineseMythology.Items.Materials;

namespace AncientChineseMythology.Tiles.Placable
{
    public class LingShiOreTile : ModTile
    {
        public override string Texture =>
            "AncientChineseMythology/Textures/Tiles/Placable/LingShiOreTile";

        public override void SetStaticDefaults()
        {
            /* 基础属性 */
            Main.tileSolid[Type]              = true;
            Main.tileMergeDirt[Type]          = true;
            Main.tileSpelunker[Type]          = true;
            Main.tileOreFinderPriority[Type]  = 430;  // 高于赤铜 :contentReference[oaicite:0]{index=0}:contentReference[oaicite:1]{index=1}
            Main.tileShine[Type]              = 1100;
            Main.tileShine2[Type]             = true;
            Main.tileBlockLight[Type]         = true;
            TileID.Sets.Ore[Type]             = true;
            TileID.Sets.FriendlyFairyCanLureTo[Type] = true;

            /* 地图显示 */
            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(150, 200, 255), name);

            /* 音效 / 掉落效果 */
            HitSound  = SoundID.Tink;
            DustType  = DustID.Platinum;

            /* 挖掘参数：需要 >75% 镐力 (Molten 100% 及以上) */
            MineResist = 4.5f;
            MinPick    = 80;
        }

        public override bool IsTileBiomeSightable(int i, int j, ref Color sightColor)
        {
            sightColor = Color.LightBlue;
            return true;
        }

        public override void KillTile(int i, int j,
            ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly && !noItem)
            {
                Item.NewItem(new EntitySource_TileBreak(i, j),
                             i * 16, j * 16, 16, 16,
                             ModContent.ItemType<LingShiOre>());
            }
        }
    }
}

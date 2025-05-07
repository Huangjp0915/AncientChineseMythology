using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.DataStructures;
using AncientChineseMythology.Items.Cuprite;

namespace AncientChineseMythology.Tiles.Placable
{
    public class CupriteOreTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/CupriteOreTile";

        public override void SetStaticDefaults()
        {
            // 基本属性：坚固、能与泥土融合、被探矿器高亮等
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileSpelunker[Type] = true; 
            Main.tileOreFinderPriority[Type] = 410;
            Main.tileShine[Type] = 975;
            Main.tileShine2[Type] = true;
            Main.tileBlockLight[Type] = true;
            // 设置为矿石，使金属探测器可识别
            TileID.Sets.Ore[Type] = true;
            TileID.Sets.FriendlyFairyCanLureTo[Type] = true;

            // 设为非 frameImportant 使矿石可以自动合并
            Main.tileFrameImportant[Type] = false;

            // 贴图敲击时音效、落尘等
            HitSound = SoundID.Tink;
            // 设置 DustType
            DustType = DustID.Iron;

            // 地图显示名称及颜色
            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(200, 100, 50), name);

            // 需要挖掘要求与挖掘抗性
            MineResist = 4f;
            MinPick = 65;
        }

        public override bool IsTileBiomeSightable(int i, int j, ref Color sightColor) {
			sightColor = Color.Red;
			return true;
		}

        // 当该矿石 Tile 被挖掘时，生成掉落物品
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            // 如果挖掘成功且不只显示特效且允许掉落物品，则生成掉落
            if (!fail && !effectOnly && !noItem)
            {
                // 在(i,j)处生成赤铜矿物品（ChitongOre）
                Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 16, 16, ModContent.ItemType<CupriteOre>());
            }
        }
        
    }
}

using AncientChineseMythology.Content.Items.Placeables;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles
{
    public class TaiguWeaponWorkshop : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/TaiguWeaponWorkshop";

        public override void SetStaticDefaults() {
            // 基本属性
            Main.tileFrameImportant[Type] = true;
            Main.tileTable[Type] = true;              // 让房屋系统把它当桌子
            Main.tileLavaDeath[Type] = true;

            // 3×3 尺寸与锚点
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);  // 关键行！
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 }; // 三行各 16px
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 3, 0);
            TileObjectData.addTile(Type);                              // 必须最后调用

            // 地图名称与颜色
            AddMapEntry(new Color(150, 120, 90), CreateMapEntryName());

            // 让它在配方里拥有“工作台”功能
            AdjTiles = new int[] { TileID.WorkBenches };

            // 掉落物
            RegisterItemDrop(ModContent.ItemType<TaiguWeaponWorkshopItem>());
        }
    }
}

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles.Placable
{
    public class ElixirFurnaceTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/ElixirFurnaceTile";

        public override void SetStaticDefaults()
        {
            // 基础设置
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = false; // 允许无背景墙放置
            Main.tileLavaDeath[Type] = true;
            
            // 3x3布局（48px = 3格x16px）
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 }; // 总高度48px
            TileObjectData.newTile.CoordinateWidth = 16; // 每格宽度16px
            TileObjectData.newTile.Origin = new Point16(1, 2); // 锚点在第3行
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile, // 仅需实心地面
                TileObjectData.newTile.Width,
                0
            );
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);

            // 地图标记
            AddMapEntry(new Color(150, 120, 80), CreateMapEntryName());
            
            // 动画设置（若需要）
            AnimationFrameHeight = 48;
        }

        // 发光效果
        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            r = 0.8f; g = 0.6f; b = 0.4f;
        }

        // 精准掉落控制
        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            int left = i - frameX / 16; // 每格16px
            int top = j - frameY / 16;
            if (left < 0 || top < 0) return;
        }
    }
}
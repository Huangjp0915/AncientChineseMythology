using AncientChineseMythology.Items;
using AncientChineseMythology.Subworlds;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles
{
    public class TeleportationTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            // 标记为重要 Tile，便于交互
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;

            // 定义此 Tile 为多格物件：占用 4×4 个格子（4*16 = 64 像素宽高，与贴图尺寸一致）
            TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch); // 使用基础风格作为起点
            TileObjectData.newTile.Width = 4;   // 4 格宽（64 像素）
            TileObjectData.newTile.Height = 4;  // 4 格高（64 像素）
            // 原点设为左下角，即摆放时传入的坐标对应物件底部左侧
            TileObjectData.newTile.Origin = new Point16(0, 3);
            // 允许悬空放置：取消对底部锚点的要求
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            // 这里告诉引擎如何分割贴图：由于贴图尺寸为 64×64，
            // 我们将其分为 4 行，每行 16 像素，高度数组如下：
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16, 16 };
            TileObjectData.newTile.CoordinatePadding = 0;
            TileObjectData.addTile(Type);

            // 使用 Localization 获取地图显示名称（请确保语言文件中有对应键）
            var tileName = Language.GetText("漩涡之门");
            AddMapEntry(new Color(200, 200, 200), tileName);

            DustType = DustID.Stone;
        }

        public override bool RightClick(int i, int j)
        {
            Player player = Main.LocalPlayer;
            // 计算当前 Tile 在世界中的像素位置（Tile 大小 16px）
            Vector2 tileWorldPos = new Vector2(i * 16, j * 16);
            // 如果玩家距离此物件在 64 像素内，则触发右键交互
            if (Vector2.Distance(player.Center, tileWorldPos) <= 64f)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // 请确保 UnderworldSubworld 类已实现并注册
                    SubworldLibrary.SubworldSystem.Enter<UnderworldSubworld>();
                }
            }
            return true;
        }

        // 当鼠标悬停在 Tile 上时，显示图标和名称
        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2; // 防止物品被扔出
            player.cursorItemIconEnabled = true;
            // 设置鼠标旁边显示的小图标，使用与此 Tile 关联的物品（这里假设 TeleportationItem 具有传送门贴图）
            player.cursorItemIconID = ModContent.ItemType<TeleportationItem>();
            player.cursorItemIconText = Language.GetTextValue("漩涡之门");
        }

        // 当鼠标远离时也要处理
        public override void MouseOverFar(int i, int j)
        {
            MouseOver(i, j);
        }

        // 防止爆炸破坏
        public override bool CanExplode(int i, int j)
        {
            return false;
        }

        // 防止其他方式破坏 Tile
        public override bool CanKillTile(int i, int j, ref bool blockDamaged)
        {
            return false;
        }
    }
}

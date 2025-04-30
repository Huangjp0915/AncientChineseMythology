using AncientChineseMythology.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Content.Tiles
{
	public class ShengZhuStatueTile : ModTile
	{
        public override string Texture => "AncientChineseMythology/Textures/Tiles/ShengZhuStatueTile";

		public override void SetStaticDefaults() {
			Main.tileFrameImportant[Type] = true;
			Main.tileObsidianKill[Type]   = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX); // 2×3
			TileObjectData.addTile(Type);

			DustType = DustID.Gold;
			AddMapEntry(new Color(0xEF,0xD9,0xA6),
				Language.GetText("Mods.AncientChineseMythology.MapObject.ShengZhuStatue"));
		}

		// —— 单机右键交互 ——  
		public override bool RightClick(int i, int j) {
            Player pl = Main.LocalPlayer;

            // 必须手持任意 *Charm 物品
            if (pl.HeldItem?.ModItem == null ||
                !pl.HeldItem.ModItem.GetType().Name.Contains("RatCharm"))
                return false;

            // 无网络，直接调用
            ModContent.GetInstance<Content.Systems.ShengZhuStatueSystem>()
                    .TriggerStatue(new Point16(i, j), pl.whoAmI);

            AncientChineseMythologySystem.triggeredShengZhuStatue = true;
            
            return true;
        }

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2; // 防止物品被扔出
            player.cursorItemIconEnabled = true;
            // 设置鼠标旁边显示的小图标，使用与此 Tile 关联的物品（这里假设 TeleportationItem 具有传送门贴图）
            player.cursorItemIconID = ModContent.ItemType<RatCharm>();
            player.cursorItemIconText = Language.GetTextValue("圣主雕像");
        }
	}
}

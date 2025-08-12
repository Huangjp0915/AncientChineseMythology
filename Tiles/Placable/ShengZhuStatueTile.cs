using AncientChineseMythology.Items.Weapons;
using AncientChineseMythology.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles.Placable
{
    public class ShengZhuStatueTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/ShengZhuStatueTile";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;

            //改用 Style2x2 并自定义成 2×3
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.addTile(Type);

            DustType = DustID.Gold;

            AddMapEntry(new Color(0xEF, 0xD9, 0xA6),
                Language.GetText("Mods.AncientChineseMythology.MapObject.ShengZhuStatue"));
        }

        public override bool RightClick(int i, int j) {
            Player pl = Main.LocalPlayer;
            int ratCharmType = ModContent.ItemType<RatCharm>();

            //条件 A：真正手持鼠符咒  
            if (pl.HeldItem.type != ratCharmType)
                return false;

            //条件 B：鼠标上不拿着鼠符咒  
            if (Main.mouseItem.type == ratCharmType)
                return false;

            //满足上述条件，才进行召唤逻辑
            ModContent
                .GetInstance<ShengZhuStatueSystem>()
                .TriggerStatue(new Point16(i, j), pl.whoAmI);

            AncientChineseMythologySystem.triggeredShengZhuStatue = true;
            return true;
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<RatCharm>();
            player.cursorItemIconText = Language.GetTextValue("圣主雕像");
        }
    }
}

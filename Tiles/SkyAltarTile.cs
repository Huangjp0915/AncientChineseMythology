using System.Linq;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles
{
    public class SkyAltarTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/SkyAltarTile";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(170, 190, 255), CreateMapEntryName());
            DustType = DustID.Firefly;
        }

        private static bool PlayerHasKey(Player p) =>
            p.inventory.Any(item => item.type == ModContent.ItemType<Items.SkyKey>());

        public override bool RightClick(int i, int j)
        {
            Player pl = Main.LocalPlayer;
            if (!BrokenHeavenIslandSystem.unlockedSkyIsland)
            {
                if (PlayerHasKey(pl))        // 只要身上有钥匙就能发动
                {
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        AncientChineseMythology.SendSkyKeyUnlock(pl.whoAmI);  // ⇦ 传自己 id
                    else
                        BrokenHeavenIslandSystem.OpenSkyIsland(pl.whoAmI);
                }
                else
                    Main.NewText("需要持有『天钥』才能启动祭坛！");
            }
            return true;
        }

        private static bool ConsumeSkyKey(Player p)
        {
            for (int n = 0; n < p.inventory.Length; n++)
                if (p.inventory[n].type == ModContent.ItemType<Items.SkyKey>())
                {
                    p.inventory[n].stack--;
                    if (p.inventory[n].stack <= 0) p.inventory[n] = new Item();
                    return true;
                }
            return false;
        }
    }
}

using AncientChineseMythology.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles
{
    enum BloodLingzhiStage : byte { Seed, Growing, Mature }

    public class BloodLingzhiHerbTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/BloodLingzhiHerbTile";
        private const int FrameW = 18;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileCut[Type]            = true;
            Main.tileNoFail[Type]         = true;
            Main.tileObsidianKill[Type]   = true;
            TileID.Sets.ReplaceTileBreakUp[Type] = true;
            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

            AddMapEntry(new Color(180, 30, 50));   // 深血色

            // StyleAlch：允许种在任何实心方块表面
            TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
            TileObjectData.newTile.AnchorValidTiles = null;           // 不限制基底类型
            TileObjectData.addTile(Type);

            HitSound = SoundID.Grass;
            DustType = DustID.Blood;
        }

        public override bool CanPlace(int i, int j)
        {
            // 检查玩家所在位置是否猩红
            Player pl = Main.LocalPlayer;
            if (pl == null || !pl.ZoneCrimson) return false;

            // 地面须为实心方块
            Tile ground = Framing.GetTileSafely(i, j + 1);
            return ground.HasTile && Main.tileSolid[ground.TileType];
        }

        public override bool CanDrop(int i,int j) =>
            GetStage(i,j) == BloodLingzhiStage.Mature;

        public override IEnumerable<Item> GetItemDrops(int i,int j)
        {
            Player pl = Main.player[Player.FindClosest(new Vector2(i,j).ToWorldCoordinates(),16,16)];
            bool reg = pl.active &&
                (pl.HeldItem.type == ItemID.StaffofRegrowth || pl.HeldItem.type == ItemID.AcornAxe);

            int herb = reg ? Main.rand.Next(1,3) : 1;
            int seed = reg ? Main.rand.Next(1,6) : Main.rand.Next(1,4);

            yield return new Item(ModContent.ItemType<BloodLingzhi>(), herb);
            yield return new Item(ModContent.ItemType<BloodLingzhiSeeds>(), seed);
        }

        public override void RandomUpdate(int i,int j)
        {
            Tile t = Framing.GetTileSafely(i,j);
            if (GetStage(t) != BloodLingzhiStage.Mature)
            {
                t.TileFrameX += FrameW;
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendTileSquare(-1,i,j,1);
            }
        }

        public override bool IsTileSpelunkable(int i,int j) =>
            GetStage(i,j) == BloodLingzhiStage.Mature;

        public override void SetSpriteEffects(int i,int j,ref SpriteEffects e) =>
            e = (i & 1)==0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        /* 辅助 */
        private static BloodLingzhiStage GetStage(Tile t) => (BloodLingzhiStage)(t.TileFrameX / FrameW);
        private static BloodLingzhiStage GetStage(int i,int j) => GetStage(Framing.GetTileSafely(i,j));
    }
}

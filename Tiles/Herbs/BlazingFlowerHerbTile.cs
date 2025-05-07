using AncientChineseMythology.Items.Herbs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles.Herbs
{
    enum BlazingStage : byte { Seed, Growing, Bloom }

    public class BlazingFlowerHerbTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Herbs/BlazingFlowerHerbTile";
        private const int FrameW = 18;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileCut[Type]            = true;
            Main.tileNoFail[Type]         = true;
            Main.tileObsidianKill[Type]   = true;
            TileID.Sets.ReplaceTileBreakUp[Type]      = true;
            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

            AddMapEntry(new Color(255, 90, 40));

            TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
            TileObjectData.newTile.AnchorValidTiles = new int[] { TileID.Ash };   // 只允许灰烬块
            TileObjectData.addTile(Type);

            HitSound = SoundID.Grass;
            DustType = DustID.Torch;
        }

        public override bool CanDrop(int i,int j) =>
            GetStage(i,j) == BlazingStage.Bloom &&
            IsEvening();                       // 仅傍晚盛开时可掉落

        public override IEnumerable<Item> GetItemDrops(int i,int j)
        {
            if (!IsEvening()) yield break;

            Player pl = Main.player[Player.FindClosest(new Vector2(i,j).ToWorldCoordinates(),16,16)];
            bool reg = pl.active &&
                (pl.HeldItem.type == ItemID.StaffofRegrowth || pl.HeldItem.type == ItemID.AcornAxe);

            int herb = reg ? Main.rand.Next(1,3) : 1;
            int seed = reg ? Main.rand.Next(1,6) : Main.rand.Next(1,4);

            yield return new Item(ModContent.ItemType<BlazingFlower>(), herb);
            yield return new Item(ModContent.ItemType<BlazingFlowerSeeds>(), seed);
        }

        public override void RandomUpdate(int i,int j)
        {
            Tile t = Framing.GetTileSafely(i,j);
            BlazingStage stage = GetStage(t);

            bool evening = IsEvening();
            if (evening && stage == BlazingStage.Growing) {
                t.TileFrameX += FrameW;                 // 傍晚盛开
            }
            else if (!evening && stage == BlazingStage.Bloom) {
                t.TileFrameX -= FrameW;                 // 其余时间闭合
            }
            else if (stage == BlazingStage.Seed) {      // 种子任何时间 → Growing
                t.TileFrameX += FrameW;
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendTileSquare(-1,i,j,1);
        }

        public override bool IsTileSpelunkable(int i,int j) =>
            IsEvening() && GetStage(i,j)==BlazingStage.Bloom;

        public override void SetSpriteEffects(int i,int j,ref SpriteEffects e) =>
            e = (i & 1)==0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        private static bool IsEvening() =>
            Main.dayTime && Main.time >= 27000.0;   // 18:00~19:30

        private static BlazingStage GetStage(Tile t)=> (BlazingStage)(t.TileFrameX / FrameW);
        private static BlazingStage GetStage(int i,int j)=> GetStage(Framing.GetTileSafely(i,j));
    }
}

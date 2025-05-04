using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Herbs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Metadata;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles.Herbs {
	public enum StarflowerStage : byte { Seed, Growing, Bloom }

	public class StarflowerHerbTile : ModTile {

		public override string Texture => "AncientChineseMythology/Textures/Tiles/Herbs/StarflowerHerbTile";
		private const int FrameWidth = 18;

		public override void SetStaticDefaults() {
			Main.tileFrameImportant[Type] = true;
			Main.tileCut[Type]            = true;
			Main.tileNoFail[Type]         = true;
			Main.tileObsidianKill[Type]   = true;
			TileID.Sets.ReplaceTileBreakUp[Type]      = true;  // 方便重新种植 :contentReference[oaicite:1]{index=1}
			TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

			TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);  // 高尔夫物理 :contentReference[oaicite:2]{index=2}

			LocalizedText name = CreateMapEntryName();
			AddMapEntry(new Color(100, 158, 255), name);

			TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
			TileObjectData.newTile.AnchorValidTiles = new int[] {
				TileID.Cloud, TileID.RainCloud        // 只允许种在云块上
			};
			TileObjectData.addTile(Type);

			DustType = DustID.BlueTorch;  // 蓝色星光尘
			HitSound = SoundID.Grass;
		}

		public override bool CanPlace(int i, int j) {
			// 允许重新种在已完全绽放的星辰花上 :contentReference[oaicite:3]{index=3}
			Tile tile = Framing.GetTileSafely(i, j);
			if (tile.HasTile && tile.TileType == Type)
				return GetStage(tile) == StarflowerStage.Bloom;
			return base.CanPlace(i, j);
		}

		// 夜晚时才判定为可掉落
		public override bool CanDrop(int i, int j) =>
			GetStage(i, j) == StarflowerStage.Bloom && !Main.dayTime;

		public override IEnumerable<Item> GetItemDrops(int i, int j) {
			StarflowerStage stage = GetStage(i, j);
			if (stage != StarflowerStage.Bloom || Main.dayTime)
				yield break;

			Player p = Main.player[Player.FindClosest(new Vector2(i, j).ToWorldCoordinates(), 16, 16)];
			bool regrowth = p.active &&
				(p.HeldItem.type == ItemID.StaffofRegrowth || p.HeldItem.type == ItemID.AcornAxe);

			int flowerAmt = regrowth ? Main.rand.Next(2, 4) : Main.rand.Next(1, 2);
			int seedAmt   = regrowth ? Main.rand.Next(2, 7) : Main.rand.Next(1, 4);

			yield return new Item(ModContent.ItemType<Starflower>(), flowerAmt);
			yield return new Item(ModContent.ItemType<StarflowerSeeds>(), seedAmt);
		}

		// 控制生长：白天→Growing，夜晚→Bloom
		public override void RandomUpdate(int i, int j) {
			Tile tile = Framing.GetTileSafely(i, j);
			StarflowerStage stage = GetStage(tile);

			if (Main.dayTime && stage == StarflowerStage.Bloom) {
				// 白天闭合
				tile.TileFrameX -= FrameWidth;
			}
			else if (!Main.dayTime && stage == StarflowerStage.Growing) {
				// 夜晚开放
				tile.TileFrameX += FrameWidth;
			}
			else if (stage == StarflowerStage.Seed) {
				// 任何时间都能从种子→Growing
				tile.TileFrameX += FrameWidth;
			}
			if (Main.netMode != NetmodeID.SinglePlayer)
				NetMessage.SendTileSquare(-1, i, j, 1);
		}

		public override bool IsTileSpelunkable(int i, int j) =>
			!Main.dayTime && GetStage(i, j) == StarflowerStage.Bloom;  // 夜晚才闪光

		public override void SetSpriteEffects(int i, int j, ref SpriteEffects effects) {
			if (i % 2 == 0) effects = SpriteEffects.FlipHorizontally;
		}

		private static StarflowerStage GetStage(int i, int j) => GetStage(Framing.GetTileSafely(i, j));
		private static StarflowerStage GetStage(Tile tile)    => (StarflowerStage)(tile.TileFrameX / FrameWidth);
	}
}

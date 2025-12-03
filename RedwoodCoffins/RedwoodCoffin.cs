using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.RedwoodCoffins
{
    internal class RedwoodCoffin : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 0, 0, 30);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<RedwoodCoffinTile>();
        }
    }

    internal class RedwoodCoffinTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(167, 72, 81), VaultUtils.GetLocalizedItemName<RedwoodCoffin>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 9;
            TileObjectData.newTile.Height = 12;
            TileObjectData.newTile.Origin = new Point16(5, 11);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile<RedwoodCoffin>();

        public override bool RightClick(int i, int j) {
            if (TileProcessorLoader.AutoPositionGetTP<RedwoodCoffinTP>(i, j, out var coffinTP)) {
                coffinTP.open = !coffinTP.open;
            }
            return base.RightClick(i, j);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out RedwoodCoffinTP coffinTP)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += coffinTP.frame * 18 * 12;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class RedwoodCoffinTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<RedwoodCoffinTile>();
        public int frame;
        private int frameCunter;
        public bool open;
        public override void Update() {
            if (open) {
                if (++frameCunter > 6) {
                    frameCunter = 0;
                    if (frame < 4) {
                        frame++;
                    }
                    else {
                        frame = 4;
                    }
                }
            }
            else {
                if (++frameCunter > 6) {
                    frameCunter = 0;
                    if (frame > 0) {
                        frame--;
                    }
                }
            }
        }
    }
}

using AncientChineseMythology.NPCs.Boss.Hanbas;
using AncientChineseMythology.NPCs.Boss.Hoqings;
using AncientChineseMythology.NPCs.Boss.Jiangcens;
using AncientChineseMythology.NPCs.Boss.Yingous;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
                // 只有在关闭状态下才能触发Boss召唤
                if (!coffinTP.open && !coffinTP.hasBeenOpened) {
                    coffinTP.open = true;
                    coffinTP.hasBeenOpened = true;

                    // 播放打开音效
                    SoundEngine.PlaySound(SoundID.DoorOpen, new Vector2(i * 16, j * 16));
                }
                else if (coffinTP.open && coffinTP.hasBeenOpened) {
                    // 已经打开过的棺材可以关闭，但不会再召唤Boss
                    coffinTP.open = false;
                    SoundEngine.PlaySound(SoundID.DoorClosed, new Vector2(i * 16, j * 16));
                }
            }
            return true;
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
        public bool hasBeenOpened; // 标记是否已经被打开过

        public override void Update() {
            if (open) {
                if (++frameCunter > 6) {
                    frameCunter = 0;
                    if (frame < 4) {
                        frame++;
                    }
                    else {
                        frame = 4;
                        SpawnRandomBoss(Position.X, Position.Y);
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

        private void SpawnRandomBoss(int i, int j) {
            // Boss类型列表（只包括月球领主后的Boss）
            List<int> bossTypes = new List<int>
            {
                ModContent.NPCType<Hanba>(),
                ModContent.NPCType<Hoqing>(),
                ModContent.NPCType<Jiangcen>(),
                ModContent.NPCType<Yingou>()
            };

            // 随机选择一个Boss
            int bossType = Main.rand.Next(bossTypes);

            // 计算生成位置（棺材中心上方）
            Vector2 spawnPos = new Vector2(i * 16, (j - 6) * 16);

            // 生成Boss
            int npcIndex = NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, bossType);

            if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                NPC boss = Main.npc[npcIndex];

                // 播放Boss生成音效
                SoundEngine.PlaySound(SoundID.Roar, spawnPos);

                // 创建粒子效果
                for (int k = 0; k < 50; k++) {
                    Vector2 speed = Main.rand.NextVector2Circular(8f, 8f);
                    Dust dust = Dust.NewDustPerfect(spawnPos, DustID.Smoke, speed, 100, Color.DarkRed, 2f);
                    dust.noGravity = true;
                }

                // 显示提示文字
                if (Main.netMode == NetmodeID.SinglePlayer) {
                    Main.NewText($"红木棺材中苏醒了 {boss.ModNPC.Name}！", 175, 75, 255);
                }
                else if (Main.netMode == NetmodeID.Server) {
                    Terraria.Chat.ChatHelper.BroadcastChatMessage(
                        Terraria.Localization.NetworkText.FromLiteral($"红木棺材中苏醒了 {boss.ModNPC.Name}！"),
                        new Color(175, 75, 255)
                    );
                }
                WorldGen.KillTile(i, j);
            }
        }
    }
}

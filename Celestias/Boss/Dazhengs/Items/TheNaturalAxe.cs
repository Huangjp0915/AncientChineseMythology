using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs.Items
{
    /// <summary>
    /// 自然之斧 - 大椿Boss必定掉落的工具兼武器
    /// 左键：高伤害斧头砍伐
    /// 右键：批量在鼠标附近地面种植树种
    /// </summary>
    public class TheNaturalAxe : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 48;
            Item.height = 48;
            Item.damage = 200;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = 8;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.axe = 55; // 实际斧力 = 55 * 5 = 275%
            Item.tileBoost = 4;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键种树模式
                Item.noMelee = true;
                Item.noUseGraphic = true;
                Item.useStyle = ItemUseStyleID.HoldUp;
                Item.useTime = 20;
                Item.useAnimation = 20;
                Item.UseSound = SoundID.Grass;
            }
            else
            {
                // 左键斧头模式
                Item.noMelee = false;
                Item.noUseGraphic = false;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.useTime = 8;
                Item.useAnimation = 12;
                Item.UseSound = SoundID.Item1;
            }

            return base.CanUseItem(player);
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                PlantTreesNearCursor(player);
                return true;
            }

            return base.UseItem(player);
        }

        private void PlantTreesNearCursor(Player player)
        {
            Point cursorTile = Main.MouseWorld.ToTileCoordinates();
            int radius = 6;
            int planted = 0;
            int maxPlant = 5;

            for (int x = cursorTile.X - radius; x <= cursorTile.X + radius && planted < maxPlant; x++)
            {
                for (int y = cursorTile.Y - radius; y <= cursorTile.Y + radius && planted < maxPlant; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    if (Main.tile[x, y].HasTile)
                        continue;

                    Tile below = Main.tile[x, y + 1];
                    if (!below.HasTile || !Main.tileSolid[below.TileType])
                        continue;

                    bool validSoil = below.TileType == TileID.Grass
                        || below.TileType == TileID.HallowedGrass
                        || below.TileType == TileID.JungleGrass
                        || below.TileType == TileID.MushroomGrass
                        || below.TileType == TileID.CorruptGrass
                        || below.TileType == TileID.CrimsonGrass
                        || below.TileType == TileID.AshGrass;

                    if (!validSoil)
                        continue;

                    if (y - 1 < 0 || Main.tile[x, y - 1].HasTile)
                        continue;

                    if (WorldGen.PlaceTile(x, y, TileID.Saplings, mute: false, forced: false, player.whoAmI))
                    {
                        planted++;
                        if (Main.netMode != NetmodeID.SinglePlayer)
                            NetMessage.SendTileSquare(-1, x, y);
                    }
                }
            }

            if (planted > 0)
            {
                for (int i = 0; i < planted * 4; i++)
                {
                    Vector2 dustPos = Main.MouseWorld + Main.rand.NextVector2Circular(96f, 64f);
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Grass, 0f, -2f, 100, default, 1.2f);
                    dust.noGravity = true;
                    dust.velocity *= 0.5f;
                }
            }
        }
    }
}

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class CloudyGoldGrass : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/CloudyGoldGrass";

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type]        = true;
            Main.tileMergeDirt[Type]    = true;
            Main.tileBlockLight[Type]   = false;

            // 草->沙 退化 & 受环境转换
            TileID.Sets.Conversion.Grass[Type] = true;
            TileID.Sets.Grass[Type]            = true;

            DustType = DustID.GoldCoin;
            HitSound = SoundID.Grass;
            // 掉落还是云霞金沙
            RegisterItemDrop(ModContent.ItemType<Items.Placable.CloudyGoldSand>());

            AddMapEntry(new Color(150, 200, 120), CreateMapEntryName());
        }

        /* —— 草皮被锄头、镰刀、火等破坏时退回沙块 —— */
        public override void KillTile(int i, int j,
                                  ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            noItem = true; // 不掉“草皮”自身
            WorldGen.PlaceTile(i, j,
                ModContent.TileType<CloudyGoldSand>(),
                mute: true, forced: true);
        }

        /* —— 周围方块缺光或被液体覆盖时自动退化 —— */
        public override void RandomUpdate(int i, int j)
        {
            if (WorldGen.SolidTile(i, j - 1) ||
                Main.tile[i, j - 1].LiquidAmount > 0)
            {
                WorldGen.PlaceTile(i, j, ModContent.TileType<CloudyGoldSand>(),
                                   mute: true, forced: true);
            }
        }
    }
}

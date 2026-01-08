using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles
{
    /// <summary>
    /// 天极锭放置物块
    /// </summary>
    public class EmpyriteBarTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileShine[Type] = 1100;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileLighted[Type] = true;

            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

            AddMapEntry(new Color(230, 210, 140), CreateMapEntryName());

            DustType = DustID.GoldCoin;
            HitSound = SoundID.Tink;

            // 使用金属锭的物块数据
            Terraria.ObjectData.TileObjectData.newTile.CopyFrom(Terraria.ObjectData.TileObjectData.Style1x1);
            Terraria.ObjectData.TileObjectData.addTile(Type);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = MathF.Sin(Main.GameUpdateCount * 0.04f + i * 0.15f) * 0.2f + 0.8f;
            r = 0.9f * pulse;
            g = 0.85f * pulse;
            b = 0.5f * pulse;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
        }

        public override void NearbyEffects(int i, int j, bool closer) {
            if (closer && Main.rand.NextBool(80)) {
                Vector2 dustPos = new Vector2(i * 16 + 8, j * 16 + 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -0.8f, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

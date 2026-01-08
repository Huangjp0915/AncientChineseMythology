using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles
{
    /// <summary>
    /// 天极矿物块 - 天柱周围生成的神圣矿物
    /// 金色+青色主题，发光效果
    /// </summary>
    public class EmpyriteOreTile : ModTile
    {
        public override void SetStaticDefaults() {
            TileID.Sets.Ore[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 900; // 高优先级显示
            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 1000;
            Main.tileMergeDirt[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(220, 200, 120), name);

            DustType = DustID.GoldCoin;
            HitSound = SoundID.Tink;
            MineResist = 4f;
            MinPick = 225; // 需要月后镐力
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            // 金色+青色发光
            float pulse = MathF.Sin(Main.GameUpdateCount * 0.03f + i * 0.1f + j * 0.1f) * 0.15f + 0.85f;
            r = 0.8f * pulse;
            g = 0.75f * pulse;
            b = 0.4f * pulse;
        }

        public override void RandomUpdate(int i, int j) {
            // 随机生成光粒子
            if (Main.rand.NextBool(30)) {
                Vector2 dustPos = new Vector2(i * 16 + 8, j * 16 + 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1f, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }
        }

        public override void NearbyEffects(int i, int j, bool closer) {
            // 靠近时产生粒子
            if (closer && Main.rand.NextBool(60)) {
                Vector2 dustPos = new Vector2(i * 16 + Main.rand.Next(16), j * 16 + Main.rand.Next(16));
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -0.5f, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
            return true;
        }
    }
}

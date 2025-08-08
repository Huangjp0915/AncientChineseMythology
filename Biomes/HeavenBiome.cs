using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Biomes
{
    public class HeavenBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.Event;

        public override void SetStaticDefaults() {
        }

        //public override ModWaterStyle WaterStyle => ModContent.GetInstance<HeavenWaterStyle>();

        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle
            => ModContent.GetInstance<Backgrounds.HeavenSurfaceBGStyle>();

        public override int Music
            => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/HeavenTheme");


        public override bool IsBiomeActive(Player player) {
            if (!NPC.downedMoonlord)
                return false;

            /* scan parameters */
            const int scanRadius = 180;   //tiles
            const int needCloud = 2000;  //clouds required

            int cloudCnt = 0;

            int cx = (int)(player.Center.X / 16);
            int cy = (int)(player.Center.Y / 16);

            for (int x = cx - scanRadius; x <= cx + scanRadius &&
                    (cloudCnt < needCloud); x++) {
                if (x <= 10 || x >= Main.maxTilesX - 10) continue;

                for (int y = cy - scanRadius; y <= cy + scanRadius &&
                        (cloudCnt < needCloud); y++) {
                    if (y <= 10 || y >= Main.maxTilesY - 10) continue;

                    Tile t = Main.tile[x, y];
                    if (!t.HasTile) continue;

                    ushort type = t.TileType;

                    /* cloud types – 150: Cloud, 153: Rain Cloud */
                    if (type == TileID.Cloud) {
                        cloudCnt++;
                    }
                }
            }

            return cloudCnt >= needCloud;
        }

        /*public override void SpecialVisuals(Player player, bool isActive)
        {
            //离开天庭 → 不刷雾
            if (!isActive)
                return;

            /* ───── 1. 计算雾层数 ───── */
        /*int heavenTiles = CountHeavenTilesAround(player);
        int mistLayers  = Math.Clamp((heavenTiles - NeedTiles) / 10 + 1, 1, 10);

        /* ───── 2. 每帧尝试生成 mistLayers 个粒子 ───── */
        /*for (int i = 0; i < mistLayers; i++)
        {
            //2.1 以玩家为中心随机一个 tile 点
            int tryX = (int)(player.Center.X / 16) + Main.rand.Next(-RangeTiles, RangeTiles + 1);
            int tryY = (int)(player.Center.Y / 16) + Main.rand.Next(-RangeTiles / 2, RangeTiles / 2 + 1);

            //世界边界保护
            if (!WorldGen.InWorld(tryX, tryY, 5))
                continue;

            //2.2 寻找离该点最近的“地表”——向下搜最多 20 格，直到找到实心块
            int groundY = tryY;
            while (groundY < Main.maxTilesY - 10 &&
                !Main.tile[tryX, groundY].HasTile)
                groundY++;

            if (groundY >= Main.maxTilesY - 10) //找不到地面
                continue;

            //2.3 把生成位置放到地表上方 3–5 格处，让雾刚好贴地
            int spawnY = groundY - Main.rand.Next(3, 6);
            if (spawnY <= 10)
                continue;

            Vector2 spawnPos = new Vector2(tryX * 16 + 8, spawnY * 16 + 8);

            /* ───── 3. 生成 DustID.EctoMist (173) ───── */
        /*int dustIndex = Dust.NewDust(
            spawnPos,            //位置（像素）
            0, 0,                //宽高 0 = 精确点
            DustID.Smoke,     //173
            Main.rand.NextFloat(-0.15f, 0.15f),   //横向微漂
            Main.rand.NextFloat(-0.05f, -0.25f),  //慢速上升
            100,                 //Alpha
            default,             //颜色留默认 → 灰白
            0.9f + mistLayers * 0.05f); //尺寸随层数略增

        Dust d = Main.dust[dustIndex];
        d.noGravity = true;
        d.fadeIn    = 1f + Main.rand.NextFloat(0.5f); //先淡入再淡出
    }
}

/* ───── 辅助：统计玩家周围天庭方块，用于雾浓度 ───── */
        /*private static int CountHeavenTilesAround(Player player)
        {
            int px = (int)(player.Center.X / 16);
            int py = (int)(player.Center.Y / 16);
            int sx = px - RangeTiles, ex = px + RangeTiles;
            int sy = py - RangeTiles, ey = py + RangeTiles;

            int cnt = 0;
            for (int x = sx; x <= ex; x++)
            {
                if (x <= 10 || x >= Main.maxTilesX - 10)
                    continue;

                for (int y = sy; y <= ey; y++)
                {
                    if (y <= 10 || y >= Main.maxTilesY - 10)
                        continue;

                    Tile t = Main.tile[x, y];
                    if (t.HasTile && HeavenTileIDs.Contains(t.TileType))
                        cnt++;
                }
            }
            return cnt;
        }*/
    }
}

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace AncientChineseMythology.Projectiles
{
    public class DragonCharmExplosion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/DragonCharmExplosion";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Projectile.width = 200;  // 增大爆炸视觉效果
            Projectile.height = 200; // 增大爆炸视觉效果
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2; // 爆炸效果只存在短暂时间
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void OnKill(int timeLeft)
        {
            // 播放爆炸音效
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            // 设定爆炸破坏范围，单位为瓷砖；此处以半径5为例
            int explosionRadius = 12;
            Vector2 explosionCenter = Projectile.Center;
            int tileX = (int)(explosionCenter.X / 16f);
            int tileY = (int)(explosionCenter.Y / 16f);

            for (int x = tileX - explosionRadius; x <= tileX + explosionRadius; x++)
            {
                for (int y = tileY - explosionRadius; y <= tileY + explosionRadius; y++)
                {
                    float diffX = x - tileX;
                    float diffY = y - tileY;
                    if (diffX * diffX + diffY * diffY < explosionRadius * explosionRadius)
                    {
                        if (WorldGen.InWorld(x, y, 1))
                        {
                            // 破坏前景瓷砖（排除地牢砖等关键砖块）
                            if (Main.tile[x, y] != null && Main.tile[x, y].HasTile &&
                                !Main.tileDungeon[Main.tile[x, y].TileType])
                            {
                                WorldGen.KillTile(x, y, false, false, false);
                                if (Main.netMode == NetmodeID.MultiplayerClient)
                                {
                                    NetMessage.SendTileSquare(-1, x, y, 1);
                                }
                            }
                            // 额外破坏背景墙（如果存在且可破坏）
                            if (Main.tile[x, y] != null && Main.tile[x, y].WallType > 0)
                            {
                                WorldGen.KillWall(x, y, false);
                                if (Main.netMode == NetmodeID.MultiplayerClient)
                                {
                                    NetMessage.SendTileSquare(-1, x, y, 1);
                                }
                            }
                        }
                    }
                }
            }

            // 生成烟尘效果
            for (int i = 0; i < 30; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke,
                    Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3));
            }
        }
    }
}

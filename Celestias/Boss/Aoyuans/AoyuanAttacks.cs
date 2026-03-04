using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// °½Èò¹¥»÷¸¨Öú - ±ùÖùÓêºÍ±ù±¬¹¥»÷µÄÉú³ÉÂß¼­
    /// </summary>
    public static class AoyuanAttacks
    {
        /// <summary>
        /// ´Ó¿ÕÖĞÕÙ»½±ùÖùÓê - ±ùÖù´ÓÍæ¼ÒÉÏ·½Ëæ»úÎ»ÖÃ½µÂä
        /// </summary>
        public static void IcicleRain(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            float speed = 10f;

            Vector2 spawnPos = new Vector2(
                player.Center.X + Main.rand.Next(-700, 700),
                player.MountedCenter.Y - 600f - 100 * Main.rand.Next(3)
            );

            float dirY = player.Center.Y - spawnPos.Y;
            if (dirY < 20f) dirY = 20f;

            float length = MathF.Sqrt(dirY * dirY);
            float normalizedY = speed / length * dirY + Main.rand.Next(41) * 0.02f;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                new Vector2(0, normalizedY * 1.5f),
                ModContent.ProjectileType<AoyuanIcicle>(),
                npc.damage / 4,
                0f
            );
        }

        /// <summary>
        /// ±ù¾§É¢Éä - ´ÓBossÎ»ÖÃÏòÍâ·¢ÉäÉÈĞÎ±ùµ¯
        /// </summary>
        public static void IceBurst(NPC npc, int count, float spreadDegrees = 70f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float spread = spreadDegrees * 0.0174f;
            float baseSpeed = npc.velocity.Length();
            if (baseSpeed < 2f) baseSpeed = 2f;

            double startAngle = Math.Atan2(npc.velocity.X, npc.velocity.Y) - spread / 2;
            double deltaAngle = spread / Math.Max(count - 1, 1);

            for (int i = 0; i < count; i++) {
                double offsetAngle = startAngle + deltaAngle * i;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    new Vector2(
                        baseSpeed * (float)Math.Sin(offsetAngle) * 2,
                        baseSpeed * (float)Math.Cos(offsetAngle) * 2
                    ),
                    ModContent.ProjectileType<AoyuanIceball>(),
                    npc.damage / 4,
                    3f
                );
            }
        }
    }
}
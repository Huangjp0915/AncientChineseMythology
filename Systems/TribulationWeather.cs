using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;

namespace AncientChineseMythology.Systems
{
    public static class TribulationWeather
    {
        private const string SkyKey = "AncientChineseMythology:TribSky";

        public static void Start()
        {
            /* 服务器（含单机主机）控制降雨 */
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Main.StartRain();
                Main.maxRaining = 1f;
                Main.rainTime   = 3600 * 4;
            }

            /* 客户端激活天空滤镜 */
            if (Main.netMode != NetmodeID.Server)
            {
                var sky = SkyManager.Instance[SkyKey];
                sky?.Activate(Vector2.Zero);          // 索引器可能返回 null，判空即可
            }
        }

        public static void Stop()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                Main.StopRain();

            if (Main.netMode != NetmodeID.Server)
            {
                var sky = SkyManager.Instance[SkyKey];
                sky?.Deactivate();                    // 同理
            }
        }
    }
}

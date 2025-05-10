/*using Terraria;
using Terraria.ModLoader;
using SubworldLibrary;
using AncientChineseMythology.Subworlds;

namespace AncientChineseMythology
{
    public class UnderworldGraveyardSystem : ModSystem
    {
        public override void PostUpdatePlayers()
        {
            if (SubworldSystem.IsActive<UnderworldSubworld>())
            {
                // 确保视觉效果
                Main.GraveyardVisualIntensity = 1f;
                
                // 更新所有玩家的区域状态
                foreach (Player player in Main.player)
                {
                    if (player.active && !player.dead)
                    {
                        player.ZoneGraveyard = true;
                    }
                }
            }
        }
    }
}*/
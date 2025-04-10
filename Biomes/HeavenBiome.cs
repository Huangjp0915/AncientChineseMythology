using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Biomes
{
    public class HeavenBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;
        
        public override void SetStaticDefaults()
        {
        }
        
        // 使用新的背景设置方法
        public override string BackgroundPath => "Terraria/Images/Background_0"; // 森林背景
        
        public override string MapBackground => "Terraria/Images/MapBackground_0"; // 小地图背景
        
        public override void OnEnter(Player player)
        {
            // 使用原版森林背景
            Main.background = 0;
        }
        
        public override void OnLeave(Player player)
        {
            // 不需要特殊处理
        }
        
        public override bool IsBiomeActive(Player player)
        {
            // 返回false，因为我们使用SceneEffect检测
            return false;
        }
    }
}
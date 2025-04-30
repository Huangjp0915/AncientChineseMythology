using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class MingCrowMinionBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/MingCrowMinionBuff";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;      // 退出游戏保存
            Main.buffNoTimeDisplay[Type] = true;
        }

        // 只要冥鸦还活着就不断刷新 buff 时间
        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.Minions.MingCrowMinion>()] > 0)
            {
                player.buffTime[buffIndex] = 2; // 5 分钟相当于“永久”
            }
        }
    }
}

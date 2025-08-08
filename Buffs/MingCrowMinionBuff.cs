using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class MingCrowMinionBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/MingCrowMinionBuff";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;      //退出游戏保存
            Main.buffNoTimeDisplay[Type] = true;
        }

        //只要冥鸦还活着就不断刷新 buff 时间
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.Minions.MingCrowMinion>()] > 0) {
                player.buffTime[buffIndex] = 2; //5 分钟相当于“永久”
            }
        }

        public override bool RightClick(int buffIndex) {
            //获取本地玩家
            Player player = Main.LocalPlayer;
            //冥鸦 Minion 类型
            int mcType = ModContent.ProjectileType<Projectiles.Minions.MingCrowMinion>();

            //遍历并清除所有该玩家的冥鸦投射物
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI
                    && p.type == mcType && p.minion) {
                    p.Kill();
                }
            }

            return true; //允许 tML 按默认流程取消 Buff
        }
    }
}

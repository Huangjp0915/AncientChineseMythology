using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class BuffsBlackBearStaff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BuffsBlackBearStaff";

        public override void SetStaticDefaults() {
            //Main.buffNoTimeDisplay[Type] = false;// 设置为false，表示这是一个持续时间的buff
            Main.debuff[Type] = false; // 设置为false，表示这是一个增益buff
            Main.buffNoSave[Type] = true; // 设置为true，退出世界后不会保留该buff
            Main.buffNoTimeDisplay[Type] = true; // 设置为true，在屏幕上不会显示时间
        }

        public override void Update(Player player, ref int buffIndex) {
            // 如果召唤物存在，那么延长buff时间，否则删除BUFF
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.BlackBearStaffProj1>()] > 0)//检测玩家持有的弹幕数量
            {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}

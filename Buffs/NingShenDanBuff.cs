using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class NingShenDanBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/NingShenDanBuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 增加最大魔力值 100 点
            player.statManaMax2 += 100;
            // 同时将魔力回满
            player.statMana = player.statManaMax2;

            // 当 Buff 即将结束时（只剩 1 tick）添加后遗症 Debuff，持续 30 秒（1800 ticks）
            if (player.buffTime[buffIndex] == 1) {
                player.AddBuff(ModContent.BuffType<NingShenDanDebuff>(), 1800);
            }
        }
    }
}

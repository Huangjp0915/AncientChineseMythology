using AncientChineseMythology.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class ChengYingBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/ChengYingBuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.vanityPet[Type] = false;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (!player.mount.Active || player.mount.Type != ModContent.MountType<ChengYingMount>())
                player.mount.SetMount(ModContent.MountType<ChengYingMount>(), player); // 保持激活
            player.buffTime[buffIndex] = 2; // 始终刷新
        }
    }
}

using AncientChineseMythology.Mounts;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{

    public class CloudMountBuff : ModBuff {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/CloudMountBuff";

        public override void SetStaticDefaults() {
            Main.vanityPet[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.mount.SetMount(ModContent.MountType<CloudMount>(), player);
            player.buffTime[buffIndex] = 10; // 永续
        }
    }
}
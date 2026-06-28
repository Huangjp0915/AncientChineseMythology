using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    public class SoulBannerMinionBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SoulBannerMinion>()] > 0) {
                player.buffTime[buffIndex] = 2;
            }
        }

        public override bool RightClick(int buffIndex) {
            Player player = Main.LocalPlayer;
            int bannerType = ModContent.ProjectileType<SoulBannerMinion>();

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.type == bannerType && p.minion) {
                    p.Kill();
                }
            }

            return true;
        }
    }
}

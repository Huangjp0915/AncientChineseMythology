using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class ShenxianGuanglunBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/ShenxianGuanglunBuff";
        public override void SetStaticDefaults() {
            // 装备栏里显示为“光宠”
            Main.lightPet[Type] = true;  // 标记为光宠 Buff 
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 保活 + 召唤弹幕
            if (player.buffTime[buffIndex] > 1)
                player.buffTime[buffIndex] = 18000;

            player.GetModPlayer<ACMPlayer>().shenxianLightPet = true;

            if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[ModContent.ProjectileType<ShenxianGuanglunPet>()] <= 0) {
                Projectile.NewProjectile(
                    player.GetSource_Buff(buffIndex),
                    player.Center,
                    Microsoft.Xna.Framework.Vector2.Zero,
                    ModContent.ProjectileType<ShenxianGuanglunPet>(),
                    0, 0, player.whoAmI);
            }
        }
    }
}

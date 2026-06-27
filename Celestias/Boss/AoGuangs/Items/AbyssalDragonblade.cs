using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs.Items
{
    /// <summary>
    /// 深渊龙刀 - 敖广掉落的大刀类近战武器
    /// 挥砍时释放水龙斩波，蓄力可释放小型水龙卷
    /// </summary>
    public class AbyssalDragonblade : ModItem
    {
        private int slashCount = 0;
        private float tidalPower = 0f;
        private const float MaxTidalPower = 100f;

        public override void SetDefaults() {
            Item.damage = 380;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DragonTidalSlash>();
            Item.shootSpeed = 14f;
            Item.crit = 12;
        }

        public override void HoldItem(Player player) {
            // 水光环效果
            if (tidalPower > 50f && Main.rand.NextBool(6)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(50, 50);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, -1f, 150, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }

            // 满潮时强化效果
            if (tidalPower >= MaxTidalPower && Main.rand.NextBool(3)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(60, 60);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, -2f, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 潮力自然衰减
            if (tidalPower > 0f) {
                tidalPower -= 0.08f;
                if (tidalPower < 0f) tidalPower = 0f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            slashCount++;
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 普通斩击弹幕
            Projectile.NewProjectile(source, player.Center, direction * 16f,
                type, damage, knockback, player.whoAmI);

            // 每三刀释放水龙斩波
            if (slashCount >= 3) {
                slashCount = 0;
                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f, Volume = 1f }, player.Center);

                Projectile.NewProjectile(source, player.Center, direction * 18f,
                    ModContent.ProjectileType<DragonTidalSlash>(), (int)(damage * 1.4f), knockback * 1.5f, player.whoAmI);

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
                }
            }

            // 满潮时释放水龙卷
            if (tidalPower >= MaxTidalPower) {
                tidalPower = 0f;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1.2f }, player.Center);

                // 在前方生成小型水龙卷
                Vector2 tornadoPos = player.Center + direction * 200f;
                Projectile.NewProjectile(source, tornadoPos, Vector2.Zero,
                    ModContent.ProjectileType<MiniWaterTornado>(), damage * 2, knockback, player.whoAmI);

                // 视觉爆发
                for (int i = 0; i < 25; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(player.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 25);
                }
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 积蓄潮力
            tidalPower += 6f;
            if (hit.Crit) tidalPower += 10f;
            if (tidalPower > MaxTidalPower) tidalPower = MaxTidalPower;

            // 水花击中效果
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // 东海冰蓝命中演出 (径向辉光 + 冲击环) — 满潮时放大
            float burstScale = tidalPower >= MaxTidalPower ? 1.6f : 1.1f;
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, burstScale, player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "以东海龙王之鳞铸成的深渊巨刃"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "每三刀释放水龙斩波"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "命中敌人积蓄潮力，满潮时释放水龙卷"));
        }
    }
}

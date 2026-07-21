using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bows
{
    /// <summary>
    /// 金金弓 — 黑熊精掉落弓 (黑风山大王的"黑风 + 蜂蜜"双意象)。
    /// 普通射击: 任何箭化为黑风箭 (墨紫风尾, 30 帧后极微风导)。
    /// 每第 5 射: 蜂蜜重矢 (琥珀大箭 ×1.7, 重弹道, 命中迟缓并炸出蜜滴)。
    /// </summary>
    public class BlackBearBow : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Bows/BlackBearBow";

        /// <summary>射击计数 (每第 5 射蜜矢; owner 端物品实例状态, TidecallersDecree 同款模式)。</summary>
        private int _shotCount;

        private bool HoneyReady => _shotCount >= 4;

        public override void SetDefaults() {
            Item.damage = 28;
            Item.crit = 6;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 22;
            Item.height = 64;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3;
            Item.value = Item.buyPrice(0, 30, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item5; // 基础弓声走广播; 蜜矢附加层在 Shoot 叠加
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<Projectiles.BlackBearBowProj1>();
            Item.shootSpeed = 11f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override void HoldItem(Player player) {
            // 蜜矢就绪: 持弓手滴落金蜜微粒 (纯视觉读条)
            if (HoneyReady && !Main.dedServ && Main.rand.NextBool(5)) {
                Vector2 hand = player.MountedCenter + new Vector2(player.direction * 12f, -4f);
                Dust d = Dust.NewDustPerfect(hand, DustID.Honey2,
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.4f)), 100, default, Main.rand.NextFloat(0.8f, 1.15f));
                d.noGravity = false;
            }
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(5, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _shotCount++;
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 spawnPos = player.Center + dir * 20f;
            int projType = ModContent.ProjectileType<Projectiles.BlackBearBowProj1>();

            if (_shotCount >= 5) {
                // —— 蜂蜜重矢 (第 5 射峰值) ——
                _shotCount = 0;
                Projectile.NewProjectile(source, spawnPos, velocity * 1.1f, projType,
                    (int)(damage * 1.7f), knockback * 1.6f, player.whoAmI, ai0: 1f);

                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.45f, Pitch = -0.1f }, player.Center);

                // 出膛蜜花 + 微后坐 (重矢的份量)
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(spawnPos, DustID.Honey2,
                        dir.RotatedByRandom(0.5) * Main.rand.NextFloat(1.5f, 4f), 80, default, 1.2f);
                    d.noGravity = true;
                }
                player.velocity -= dir * 1.5f;
            }
            else {
                // —— 黑风箭 ——
                Projectile.NewProjectile(source, spawnPos, velocity, projType, damage, knockback, player.whoAmI, ai0: 0f);

                // 出膛黑风微尘
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(spawnPos, DustID.Smoke,
                        dir * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        140, new Color(60, 44, 92), 1.1f);
                    d.noGravity = true;
                }
            }

            return false; // 弹幕已手动生成 (弹药消耗由 tML 标准流程处理, 不再手动重复消耗)
        }
    }
}

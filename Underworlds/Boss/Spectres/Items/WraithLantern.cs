using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres.Items
{
    /// <summary>
    /// 鬼火灯笼 — 怨灵可选掉落，魔法武器
    /// 释放双鬼火灯笼，怨灵锁链在灯笼间灼烧敌人。
    /// </summary>
    public class WraithLantern : ModItem
    {
        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 118;
            Item.DamageType = DamageClass.Magic;
            Item.width = 36;
            Item.height = 42;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 18);
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<WraithLanternGhost>();
            Item.shootSpeed = 10f;
            Item.mana = 12;
            Item.noMelee = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ClearOwnedLanternSet(player);

            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitY);
            int ghostType = ModContent.ProjectileType<WraithLanternGhost>();
            int tetherType = ModContent.ProjectileType<WraithLanternTether>();

            int left = Projectile.NewProjectile(source, player.Center, aim * Item.shootSpeed, ghostType, damage, knockback, player.whoAmI, 0f);
            int right = Projectile.NewProjectile(source, player.Center, aim * Item.shootSpeed, ghostType, damage, knockback, player.whoAmI, 1f);

            if (left < 0 || right < 0) return false;

            Projectile projLeft = Main.projectile[left];
            Projectile projRight = Main.projectile[right];
            projLeft.ai[1] = right;
            projRight.ai[1] = left;
            projLeft.originalDamage = Item.damage;
            projRight.originalDamage = Item.damage;

            int tetherDamage = (int)(damage * 0.42f);
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, tetherType, tetherDamage, 0f, player.whoAmI, left, right);

            // 放灯瞬间的青黄魂火点燃演出 (一次性, 走 ACMWeaponBurst 安全反馈)
            ACMWeaponBurst.Spawn(source, player.Center, ACMWeaponBurst.SoulFire, scale: 0.7f, owner: player.whoAmI);

            return false;
        }

        private static void ClearOwnedLanternSet(Player player) {
            int ghostType = ModContent.ProjectileType<WraithLanternGhost>();
            int tetherType = ModContent.ProjectileType<WraithLanternTether>();

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI) continue;
                if (proj.type == ghostType || proj.type == tetherType) {
                    proj.Kill();
                }
            }
        }
    }
}

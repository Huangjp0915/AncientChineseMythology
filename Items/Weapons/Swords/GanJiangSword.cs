using AncientChineseMythology.Projectiles;
using System;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    public class GanJiangSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";
        public int attackType = 0;
        public int comboExpireTimer = 0;
        private int Counter = 0;

        public override void SetDefaults() {
            Item.damage = 84;
            Item.crit = 24;
            Item.DamageType = DamageClass.Melee;
            Item.width = 68;
            Item.height = 68;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 0, 1, 4);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GanJiangSwordProj>();
        }

        public override void HoldItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                bool flag = Main.projectile.Any(proj => proj.active && proj.type == ModContent.ProjectileType<GanJiangSwordProj_2>() && proj.owner == player.whoAmI);
                bool flag1 = Main.projectile.Any(proj => proj.active && proj.type == ModContent.ProjectileType<GanJiangSwordProj>() && proj.owner == player.whoAmI);

                if (Main.mouseRight && !flag && !flag1 && !player.mouseInterface) {
                    Vector2 direction = Vector2.Normalize(player.DirectionTo(Main.MouseWorld));
                    FireProjectile(player, player.Center, direction, ModContent.ProjectileType<GanJiangSwordProj_2>(), (int)(Item.damage * 0.8f), Item.knockBack);
                    if (Counter < 2)
                        Counter++;
                    else
                        Counter = 0;
                    if (Counter == 2) FireProjectile(player, player.Center, direction, ModContent.ProjectileType<GanJiangSwordProj_2>(), (int)(Item.damage * 0.8f), Item.knockBack);
                }
            }

            base.HoldItem(player);
        }
        public override bool CanUseItem(Player player) {
            if (Counter < 2)
                Counter++;
            else
                Counter = 0;
            if (Counter == 2) {
                Vector2 direction = Vector2.Normalize(player.DirectionTo(Main.MouseWorld));
                FireProjectile(player, player.Center, direction, ModContent.ProjectileType<GanJiangSwordProj>(), Item.damage, Item.knockBack);
            }
            return base.CanUseItem(player);
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Main.mouseRight) {
                FireProjectile(player, position, velocity, ModContent.ProjectileType<GanJiangSwordProj_2>(), (int)(Item.damage * 0.8f), knockback);
                return false;
            }
            else {
                FireProjectile(player, position, velocity, ModContent.ProjectileType<GanJiangSwordProj>(), damage, knockback);
                return false;
            }
        }

        private void FireProjectile(Player player, Vector2 position, Vector2 velocity, int projectileType, int damage, float knockback) {
            Projectile.NewProjectile(player.GetSource_ItemUse(Item), position, velocity, projectileType, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 2;
            comboExpireTimer = 0;
        }

        public override void UpdateInventory(Player player) {
            comboExpireTimer = Math.Min(comboExpireTimer + 1, 120);
            if (comboExpireTimer >= 120) {
                attackType = 0;
                Counter = -1;
            }
        }
    }
}
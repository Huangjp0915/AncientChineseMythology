using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    public class SoulBanner : ModItem
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.damage = 52;
            Item.DamageType = DamageClass.Summon;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightPurple;
            Item.mana = 15;
            Item.autoReuse = true;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulBannerHeldProj>();
            Item.shootSpeed = 3.5f;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键：召唤悬浮幡（冷却更长，魔力消耗更高）
                Item.useTime = 36;
                Item.useAnimation = 36;
                Item.mana = 30;

                // 已有一个幡旗时不能再次释放
                int minionType = ModContent.ProjectileType<SoulBannerMinion>();
                if (player.ownedProjectileCounts[minionType] >= 1)
                    return false;
            }
            else
            {
                // 左键：手持弹幕挥舞吸魂
                Item.useTime = 30;
                Item.useAnimation = 30;
                Item.mana = 15;
            }

            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                // 右键：召唤万魂幡悬浮体
                player.AddBuff(ModContent.BuffType<SoulBannerMinionBuff>(), 2);
                var proj = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<SoulBannerMinion>(), damage, knockback, player.whoAmI);
                proj.originalDamage = Item.damage;
            }
            else
            {
                // 左键：释放手持挥舞弹幕
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<SoulBannerHeldProj>(), damage, knockback, player.whoAmI);
            }

            return false;
        }
    }
}

using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Waapons.Swords
{
    public class YuChangSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/YuChangSword";

        private int attackCounter = 0; // 攻击计数器
        private int cooldownTimer = 0; // 冷却计时器

        public override void SetDefaults() {
            Item.damage = 35; //基础伤害
            Item.crit = 80; //暴击率
            Item.DamageType = DamageClass.Melee; //伤害类型
            Item.width = 50; //物品宽度
            Item.height = 50; //物品高度
            Item.useTime = 14; //使用时间
            Item.useAnimation = 14; //使用动画时间
            Item.knockBack = 4; //击退
            Item.useStyle = ItemUseStyleID.Rapier; //使用风格
            Item.value = Item.buyPrice(0, 100, 0, 0); //物品价值
            Item.rare = ItemRarityID.Orange; //稀有度
            Item.autoReuse = true; // 自动使用
            Item.noUseGraphic = true; // 隐藏默认挥动贴图
            Item.shoot = ModContent.ProjectileType<YuChangSwordProjectile>(); //绑定自定义投射物
            Item.shootSpeed = 1f; //射弹速度 
            Item.noMelee = true; // 禁用近战碰撞框

        }

        // 使能右键
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 右键技能：检查冷却
                if (cooldownTimer > 0)
                    return false;

                // 设置右键使用参数
                Item.useStyle = ItemUseStyleID.HoldUp;
                Item.useTime = 10;
                Item.useAnimation = 10;
                Item.shoot = ModContent.ProjectileType<YuChangSkillProjectile>();
                Item.shootSpeed = 20f;
                Item.noMelee = true;
                return true;
            }
            else {
                // 左键保持原逻辑
                Item.useStyle = ItemUseStyleID.Rapier;
                Item.useTime = 14;
                Item.useAnimation = 14;
                Item.shoot = ModContent.ProjectileType<YuChangSwordProjectile>();
                Item.shootSpeed = 1f;
                Item.noMelee = true;
                return player.ownedProjectileCounts[Item.shoot] < 1;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 pos, Vector2 vel, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                // 右键：发射主动技能投射物，伤害 300%，无限穿透
                Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
                Projectile.NewProjectile(
                    source,
                    player.Center,
                    direction * Item.shootSpeed,
                    type,
                    (int)(damage * 3f),
                    knockback,
                    player.whoAmI
                );
                // 重置 20 秒冷却（20*60 帧）
                cooldownTimer = 20 * 60;
                return false;
            }
            else {
                // 左键：原有主弹幕 + 每4次连击发射射弹
                Projectile.NewProjectile(source, pos, vel, type, damage, knockback, player.whoAmI);
                if (++attackCounter >= 4) {
                    attackCounter = 0;
                    Projectile.NewProjectile(source, player.Center, vel * 20f,
                        ModContent.ProjectileType<YuChangSwordBeanProjectile>(),
                        damage, knockback, player.whoAmI, ai0: MathHelper.ToRadians(45));
                }
                return false;
            }
        }

        public override void UpdateInventory(Player player) {
            // 每帧减少冷却
            if (cooldownTimer > 0)
                cooldownTimer--;
        }



    }
    public class YuChangSwordFishingPlayer : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            // 1% 几率钓到鱼肠剑
            if (Main.rand.NextFloat() < 0.01f) {
                itemDrop = ModContent.ItemType<YuChangSword>();
            }
        }
    }
}
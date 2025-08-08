using AncientChineseMythology.Buffs;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Summons
{
    public class ShenxianGuanglunItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Summons/ShenxianGuanglunItem";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults() {
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.width = 28;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<ShenxianGuanglunBuff>(); // 给予 Buff
            Item.shoot = ModContent.ProjectileType<ShenxianGuanglunPet>(); // 直接生成弹幕
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                // 1. 给 buff（会在 Buff.Update 里被续到 18000）
                player.AddBuff(Item.buffType, 3600);

                // 2. 先删除玩家已有的同类光宠，防止重复
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active &&
                        proj.owner == player.whoAmI &&
                        proj.type == ModContent.ProjectileType<ShenxianGuanglunPet>()) {
                        proj.Kill();
                    }
                }

                // 3. 立即生成新宠物
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<ShenxianGuanglunPet>(),
                    0, 0, player.whoAmI);
            }
            return true;
        }
    }
}

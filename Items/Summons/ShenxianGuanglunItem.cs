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
            player.AddBuff(Item.buffType, 2); // 给自己上 Buff
            return true;
        }
    }
}

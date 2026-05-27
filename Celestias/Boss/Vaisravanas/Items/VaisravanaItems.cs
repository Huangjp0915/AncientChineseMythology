using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas.Items
{
    // PLACEHOLDER: see docs/PLACEHOLDER_CONTENT_REGISTRY.md
    /// <summary>毗沙门主题武器/饰品占位 — Phase 2 掉落绑定。</summary>
    public class TreasurePagodaStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1320;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 18;
            Item.shoot = ProjectileID.RainbowRodBullet;
            Item.shootSpeed = 12f;
            Item.staff[Item.type] = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.RainbowRod;
    }

    public class VaultshadeVoidshot : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1280;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 24f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.SniperRifle;
    }

    public class CelestialCircletScepter : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1300;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 16;
            Item.shoot = ProjectileID.HallowStar;
            Item.shootSpeed = 14f;
            Item.staff[Item.type] = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.StaffofRegrowth;
    }

    public class TreasurePagodaCharm : ModItem
    {
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 32;
            Item.value = Item.sellPrice(platinum: 1);
            Item.rare = ItemRarityID.Red;
            Item.accessory = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsShield;
    }
}

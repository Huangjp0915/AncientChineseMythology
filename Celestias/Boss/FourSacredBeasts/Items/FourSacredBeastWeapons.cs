using AncientChineseMythology.Celestias.PillarofTheHeavenes.Items;
using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>四圣兽主题武器占位 — Phase 2 掉落绑定；灵材合成桥待完善。</summary>
    public class AzureTorrentBlades : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1480;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 40;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<QingLongSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;
    }

    public class WindserpentDao : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1520;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    public class ThunderclapLongbow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1550;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 46;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PulseBow;
    }

    public class AurelianCataclysmSmasher : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1580;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 12f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsHammer;
    }

    public class ArgentPulseObliterator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1450;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 48;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 6;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    public class WhiteTigerClaws : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1500;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.FeralClaws;
    }

    public class StarfireAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1520;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    public class SolarisEternalVerdict : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1600;
            Item.DamageType = DamageClass.Summon;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.mana = 10;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.OpticStaff;
    }

    public class PhoenixFlameStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1480;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
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

    public class GeocrystalShatterblade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1450;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    public class GeoarchonRupturer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1500;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 20;
            Item.shoot = ProjectileID.RockGolemRock;
            Item.shootSpeed = 10f;
            Item.staff[Item.type] = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.StaffofEarth;
    }

    public class BlackTortoiseShield : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1550;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shieldSlot = 1;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AnkhShield;
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans.Items
{
    /// <summary>敖闰主题武器占位 — Phase 1 掉落绑定，完整机制待 Phase 2。</summary>
    public class GlacialDragonblade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 355;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.IceSickle;
    }

    public class PermafrostTrident : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 360;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Trident;
    }

    public class VortexPrimordialStain : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 365;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.mana = 12;
            Item.shoot = ProjectileID.WaterBolt;
            Item.shootSpeed = 14f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BookofSkulls;
    }

    public class InkscaledFlowFan : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 370;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.mana = 10;
            Item.shoot = ProjectileID.WaterBolt;
            Item.shootSpeed = 12f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicMirror;
    }

    public class BlizzardPiercer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 375;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.IceBow;
    }
}

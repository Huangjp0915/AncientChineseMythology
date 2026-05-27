using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns.Items
{
    /// <summary>敖顺主题武器占位 — Phase 1 掉落绑定，完整机制待 Phase 2。</summary>
    public class ThunderlordHalberd : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 370;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Gungnir;
    }

    public class StormchainWhip : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 375;
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.width = Item.height = 30;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item152;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.DD2SquireBetsySword;
    }

    public class TempestRepeater : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 380;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VenusMagnum;
    }

    public class LightningEdictTome : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 385;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.mana = 12;
            Item.shoot = ProjectileID.CultistBossLightningOrb;
            Item.shootSpeed = 12f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BookofSkulls;
    }

    public class AzureRuinBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 420;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }
}

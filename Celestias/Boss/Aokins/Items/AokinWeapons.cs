using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins.Items
{
    /// <summary>敖钦主题武器占位 — Phase 1 掉落绑定，完整机制待 Phase 2。</summary>
    public class InfernoDragonSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 350;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Gungnir;
    }

    public class FlamecoilChakram : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 355;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.LightDisc;
    }

    public class CrimsonMaelstromBow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 360;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Marrow;
    }

    public class DraconicEmber : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 340;
            Item.DamageType = DamageClass.Summon;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.mana = 10;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PygmyStaff;
    }

    public class MeteorCallerStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 365;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.mana = 12;
            Item.shoot = ProjectileID.Meteor1;
            Item.shootSpeed = 12f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AmberStaff;
    }
}

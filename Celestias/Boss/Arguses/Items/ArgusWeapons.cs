using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses.Items
{
    /// <summary>百目主题武器占位 — Phase 2 掉落绑定，完整机制待后续。</summary>
    public class SoulPiercingArc : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1150;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PulseBow;
    }

    public class LuminanceStellarCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1200;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    public class LuminousIrisAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1180;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 48;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Handgun;
    }
}

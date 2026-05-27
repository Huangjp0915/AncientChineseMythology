using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.NiuMa
{
    public class SoulHookWhip : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<SoulHookWhipProjectile>(), 28, 2f, 2f, 20);
            Item.damage = 52;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ThornWhip;
    }

    public class SoulHookWhipProjectile : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.ownerHitCheckDistance = 700f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornWhip;
    }
}

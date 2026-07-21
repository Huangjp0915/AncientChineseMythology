using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 赤铜剑 — 火淬猩红。重做:
    /// 左键点燃挥砍不变; 右键"入炉蓄力"由纯随机 ×2~×8 (不可读) 改为可读三级蓄力 —
    /// 按住右键入炉 (45/105/165 帧升 L1/L2/L3, 每级白热闪 + 升调音), 松手出炉挥出对应等级重斩,
    /// L2/L3 追加熔火溅珠。蓄力状态走 player.channel 原生同步 (修复旧版直读 Main.mouseRight 的多人失步)。
    /// </summary>
    public class CrimsonbronzeSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/CrimsonbronzeSword";

        public override void SetDefaults() {
            Item.damage = 59;
            Item.crit = 2;
            Item.DamageType = DamageClass.Melee;
            Item.width = 74;
            Item.height = 82;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 10, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.autoReuse = true;
            Item.noUseGraphic = false;
            Item.noMelee = false;
            Item.shoot = ModContent.ProjectileType<BlankProjectile>();
            Item.shootSpeed = 1f;
        }

        // 左键挥砍火色尘 + 暖光 (纯表现)
        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Torch);
                d.noGravity = true;
                d.velocity *= 0.45f;
                d.scale = Main.rand.NextFloat(0.9f, 1.4f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.5f, 0.2f, 0.05f);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.OnFire)) {
                target.AddBuff(BuffID.OnFire, 180);
            }
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Crimson, scale: 0.9f, owner: player.whoAmI);
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 入炉蓄力: 手持弹幕接管, channel 原生同步蓄力状态
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.noMelee = true;
                Item.noUseGraphic = true;
                Item.channel = true;
                Item.UseSound = null;
                return player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonbronzeSwordProj1>()] == 0;
            }

            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = false;
            Item.noUseGraphic = false;
            Item.channel = false;
            Item.UseSound = null; // 左键音由 BlankProjectile 生成时播放 (沿用原行为)
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<CrimsonbronzeSwordProj1>(), damage, knockback, player.whoAmI);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<BlankProjectile>(), 0, 0f, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Cuprite.Cuprite>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}

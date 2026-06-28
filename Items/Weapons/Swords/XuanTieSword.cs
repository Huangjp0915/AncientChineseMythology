using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 玄铁剑 — 可见质变 (纯表现): 挥砍时刃身暗红 SoftGlow 渐强, 命中触发
    /// <see cref="ACMWeaponBurst"/> 玄铁流血爆发。流血机制/伤害不变。
    /// </summary>
    public class XuanTieSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/XuanTieSword";

        public override void SetDefaults() {
            Item.damage = 13;                     //铁阔剑 12 +1 :contentReference[oaicite:4]{index=4}
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;                    //与铁阔剑一致
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.value = Terraria.Item.buyPrice(silver: 75);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<XuanTie.XuanTieBar>(), 10)
                .AddTile(TileID.Anvils)
                .Register();
        }

        // 刃身暗红辉光: 随挥砍进度渐强 (纯表现)
        public override void MeleeEffects(Player player, Microsoft.Xna.Framework.Rectangle hitbox) {
            float swing = player.itemAnimationMax > 0
                ? 1f - player.itemAnimation / (float)player.itemAnimationMax
                : 1f;
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.RedTorch);
                d.noGravity = true;
                d.velocity *= 0.35f;
                d.scale = Main.rand.NextFloat(0.7f, 1.1f);
            }
            Lighting.AddLight(hitbox.Center.ToVector2(), 0.18f + 0.22f * swing, 0.03f, 0.05f);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage) {
                target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3); //3 秒 :contentReference[oaicite:5]{index=5}
            }
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.XuanTieBleed, scale: 1f, owner: player.whoAmI);
        }
    }
}
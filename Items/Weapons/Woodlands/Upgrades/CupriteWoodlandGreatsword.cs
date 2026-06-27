using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜林地巨剑 — 林地巨剑的赤铜升级。
/// 相对基础版的可见质变: 挥砍带"毒+灼"双 DoT 视觉混合 (绿尘 + 橙焰 ember), 命中触发
/// <see cref="ACMWeaponBurst"/> 赤铜灼烧演出 (径向辉光 + 冲击环) + 灼烧 DoT + 轻度屏震。
/// </summary>
public class CupriteWoodlandGreatsword : ModItem
{
    public override void SetDefaults() {
        Item.damage = 42;
        Item.crit = 6;
        Item.DamageType = DamageClass.Melee;
        Item.width = 48;
        Item.height = 48;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.scale = 1.15f;
    }

    public override void MeleeEffects(Player player, Rectangle hitbox) {
        // 毒 + 灼 双 DoT 视觉混合: 多数橙焰, 少量残留绿尘
        if (Main.rand.NextBool(2)) {
            int type = Main.rand.NextBool(4) ? DustID.GreenTorch : DustID.Torch;
            Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, type);
            d.noGravity = true;
            d.velocity *= 0.5f;
            d.scale = Main.rand.NextFloat(0.9f, 1.4f);
        }
        Lighting.AddLight(hitbox.Center.ToVector2(), 0.5f, 0.25f, 0.05f);
    }

    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
        // 保留原版毒, 叠加赤铜灼烧 DoT (视觉与机制都体现"毒+灼")
        if (Main.rand.NextBool(3))
            target.AddBuff(BuffID.Poisoned, 150);
        target.AddBuff(BuffID.OnFire, 120);

        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch,
                Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, Main.rand.NextFloat(1f, 1.6f));
            d.noGravity = true;
        }

        ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.CupriteBurn, scale: 1.2f, owner: player.whoAmI);
        WeaponVFX.AddScreenShake(target.Center, 2f);
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<WoodlandGreatsword>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

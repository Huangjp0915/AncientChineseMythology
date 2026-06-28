using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 林地巨剑 - 战士巨剑类武器
/// 前期近战大剑，挥砍范围大，速度较慢，命中敌人有概率造成中毒
/// </summary>
public class WoodlandGreatsword : ModItem
{
    public override void SetDefaults() {
        Item.damage = 16;
        Item.crit = 4;
        Item.DamageType = DamageClass.Melee;
        Item.width = 48;
        Item.height = 48;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.scale = 1.1f;
    }

    public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
        if (Main.rand.NextBool(4)) {
            target.AddBuff(BuffID.Poisoned, 120);
        }

        // 命中翠绿演出 (径向辉光 + 冲击环) + 飞散叶尘
        for (int i = 0; i < 6; i++) {
            int type = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(target.Center, type,
                Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, Main.rand.NextFloat(1f, 1.5f));
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Nature, scale: 1f, owner: player.whoAmI);
    }

    public override void MeleeEffects(Player player, Rectangle hitbox) {
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Grass);
            d.noGravity = true;
            d.velocity *= 0.5f;
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 20)
            .AddIngredient(ItemID.Vine, 3)
            .AddIngredient(ItemID.JungleSpores, 5)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

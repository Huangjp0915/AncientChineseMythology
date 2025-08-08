using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
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

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage) {
                target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3); //3 秒 :contentReference[oaicite:5]{index=5}
            }
        }
    }
}
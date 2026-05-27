using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Armor.XuanTie
{
    [AutoloadEquip(EquipType.Body)]
    public class XuanTieBreastplate : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteBreastplate";

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 4);
            Item.rare = ItemRarityID.Pink;
            Item.defense = 14;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<XuanTieBar>(20)
                .AddIngredient<QingLongSpirit>(3)
                .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
                .AddIngredient<BronzeIngot>(10)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedMechBossAny)
                .Register();
        }
    }
}

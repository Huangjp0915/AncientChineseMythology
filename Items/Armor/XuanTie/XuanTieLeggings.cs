using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Armor.XuanTie
{
    [AutoloadEquip(EquipType.Legs)]
    public class XuanTieLeggings : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteLeggings";

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 3);
            Item.rare = ItemRarityID.Pink;
            Item.defense = 12;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<XuanTieBar>(16)
                .AddIngredient<QingLongSpirit>(2)
                .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
                .AddIngredient<BronzeIngot>(8)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedMechBossAny)
                .Register();
        }
    }
}

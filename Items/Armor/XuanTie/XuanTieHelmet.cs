using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using AncientChineseMythology.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Armor.XuanTie
{
    [AutoloadEquip(EquipType.Head)]
    public class XuanTieHelmet : ModItem
    {
        public static LocalizedText SetBonusText { get; private set; }

        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteHelmet";

        public override void SetStaticDefaults() {
            SetBonusText = this.GetLocalization("SetBonus");
        }

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 3);
            Item.rare = ItemRarityID.Pink;
            Item.defense = 11;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) =>
            body.type == ModContent.ItemType<XuanTieBreastplate>()
            && legs.type == ModContent.ItemType<XuanTieLeggings>();

        public override void UpdateArmorSet(Player player) {
            player.setBonus = SetBonusText.Value;
            player.GetModPlayer<XuanTieSetBonusPlayer>().xuanTieSet = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<XuanTieBar>(18)
                .AddIngredient<QingLongSpirit>(2)
                .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
                .AddIngredient<BronzeIngot>(8)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedMechBossAny)
                .Register();
        }
    }
}

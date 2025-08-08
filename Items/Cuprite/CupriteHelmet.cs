//Content/Items/Armor/CupriteHelmet.cs
using AncientChineseMythology.Items.Bronze;
using AncientChineseMythology.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Cuprite
{

    [AutoloadEquip(EquipType.Head)]
    public class CupriteHelmet : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteHelmet";
        public static LocalizedText SetBonusText { get; private set; }

        public override void SetStaticDefaults() {
            //本地化键：Mods.AncientChineseMythology.Items.CupriteHelmet.SetBonus
            SetBonusText = this.GetLocalization("SetBonus").WithFormatArgs(
                (int)(CupriteArmorConstants.BurnChance * 100),
                CupriteArmorConstants.BurnDurationTicks / 60);
        }

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(silver: 80);
            Item.rare = ItemRarityID.Green;
            Item.defense = 9;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) =>
            body.type == ModContent.ItemType<CupriteBreastplate>() &&
            legs.type == ModContent.ItemType<CupriteLeggings>();

        public override void UpdateArmorSet(Player player) {
            player.setBonus = SetBonusText.Value;
            player.GetModPlayer<CupriteSetBonusPlayer>().cupriteSet = true;
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<Cuprite>(), 15);
            recipe.AddIngredient(ModContent.ItemType<BronzeIngot>(), 10);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}
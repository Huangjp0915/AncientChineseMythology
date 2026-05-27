using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.NPCs.Boss.NiutouMamian;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Summons
{
    /// <summary>冥途双引符 — WoF 后于恶魔祭坛召唤牛头马面（§4.1.1）。</summary>
    public class UnderworldPairSummons : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.maxStack = 20;
            Item.useTime = Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 2);
        }

        public override bool CanUseItem(Player player) {
            return Main.hardMode
                && !NPC.AnyNPCs(ModContent.NPCType<NiuTou>())
                && !NPC.AnyNPCs(ModContent.NPCType<MaMian>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }
            SpoawnProj.CreatNPC(player.Center);
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<YaoQiFragment>(10)
                .AddIngredient(ItemID.Bone, 5)
                .AddIngredient(ItemID.SoulofNight, 2)
                .AddIngredient(ItemID.SoulofLight, 2)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}

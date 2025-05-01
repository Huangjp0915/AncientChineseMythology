using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Players;

namespace AncientChineseMythology.Items.Potions;

public class PoJingDan : ModItem
{
    public override string Texture => "AncientChineseMythology/Textures/Items/Potions/PoJingDan";

    public override void SetStaticDefaults()
    {
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 28;
        Item.maxStack = 10;
        Item.useStyle = ItemUseStyleID.EatFood;
        Item.useAnimation = 17;
        Item.useTime = 17;
        Item.useTurn = true;
        Item.UseSound = SoundID.Item3;
        Item.consumable = true;
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.buyPrice(gold: 1);
    }

    public override bool? UseItem(Player player)
    {
        var mp = player.GetModPlayer<MythologyPlayer>();
        if (mp.ForceMajorAdvance())
        {
            Main.NewText("破境成功，直接晋升至下一大境界！", 255, 200, 50);
        }
        else
        {
            Main.NewText("当前尚未满足破境条件（需小境界与经验皆已达到大圆满）", 200, 50, 50);
        }
        return true;
    }
}

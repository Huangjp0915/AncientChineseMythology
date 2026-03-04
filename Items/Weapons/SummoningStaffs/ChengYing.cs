using AncientChineseMythology.Buffs;
using AncientChineseMythology.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SummoningStaffs
{
    public class ChengYingReins : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/ChengYing";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 20;
            Item.UseSound = SoundID.Item79;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.noMelee = true;
            Item.mountType = ModContent.MountType<ChengYingMount>();
        }

        public override bool? UseItem(Player player) {
            player.AddBuff(ModContent.BuffType<ChengYingBuff>(), 2); //2 tick，Mount 会自动刷新
            return true;
        }
    }
}

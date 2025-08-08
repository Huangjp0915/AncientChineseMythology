using AncientChineseMythology.NPCs.Boss.BlackBear;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Summons
{
    public class JiaSha : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Summons/JiaSha";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 30;
            //使用举起使用风格，类似其他召唤物品
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            //消耗品：使用后物品消失
            Item.consumable = true;
            //物品稀有度和价值可根据需求调整
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(gold: 5);
        }

        //限制使用条件：只能在丛林生物群落中且处于地表
        public override bool CanUseItem(Player player) {
            //如果该 Boss 已存在，则不能再次使用
            if (NPC.AnyNPCs(ModContent.NPCType<BlackBear>())) {
                if (Main.myPlayer == player.whoAmI) {
                    Main.NewText("BlackBear已经存在了!", Color.Red);
                }
                return false;
            }
            //检查是否在丛林区域
            if (!player.ZoneJungle) {
                if (Main.myPlayer == player.whoAmI) {
                    Main.NewText("你必须要在丛林使用！", Color.Red);
                }
                return false;
            }
            //检查是否在地表（Overworld高度）
            if (!player.ZoneOverworldHeight) {
                if (Main.myPlayer == player.whoAmI) {
                    Main.NewText("这个必须要在地表才行！", Color.Red);
                }
                return false;
            }
            return true;
        }

        //使用物品时召唤 Boss
        public override bool? UseItem(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                //在玩家位置召唤 Boss，召唤后显示提示信息
                NPC.SpawnBoss((int)player.Center.X, (int)player.Center.Y, ModContent.NPCType<BlackBear>(), player.whoAmI);

                Main.NewText("黑熊金盯上了你的袈裟！", Color.OrangeRed);
            }
            return true;
        }
    }
}

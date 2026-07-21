using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>
    /// 四象碑 —— 举碑启动「四象归位」仪式：青龙(东)→朱雀(南)→白虎(西)→玄武(北) 依次降临的组曲挑战。
    /// 仪式编排由 <see cref="FourSymbolsRiteSystem"/> 服务器权威驱动；本物品只负责触发与重复召唤拦截。
    /// 不可消耗：仪式可反复挑战。
    /// </summary>
    public class FourSymbolsTablet : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 45;
            Item.consumable = false;
            Item.value = Item.buyPrice(platinum: 5);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item4;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.LunarTabletFragment;

        public override bool CanUseItem(Player player) {
            // 任一四圣兽在场或仪式进行中不可重复举碑（AnySacredBeastAlive 客户端可安全查询）
            return !FourSymbolsRiteSystem.RiteActive && !FourSymbolsRiteSystem.AnySacredBeastAlive;
        }

        public override bool? UseItem(Player player) {
            // 仪式启动为服务器权威；客户端只播放举碑动作
            if (Main.netMode != NetmodeID.MultiplayerClient)
                FourSymbolsRiteSystem.TryStartRite(player);

            if (player.whoAmI == Main.myPlayer)
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.8f, Pitch = -0.4f }, player.Center);
            return true;
        }
    }
}

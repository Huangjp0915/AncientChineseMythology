//using System.Collections.Generic;
//using Microsoft.Xna.Framework;
//using Terraria;
//using Terraria.ModLoader;
//using AncientChineseMythology.Players;

//namespace AncientChineseMythology.Items
//{
//    public abstract class GrowthWeapon : ModItem
//    {
//        // 标记该武器是否参与成长系统，默认为 true
//        public virtual bool IsGrowthWeapon => true;

//        // 重写 ModifyTooltips 方法，在武器描述中追加当前成长加成信息
//        public override void ModifyTooltips(List<TooltipLine> tooltips)
//        {
//            Player player = Main.LocalPlayer;
//            if (player != null)
//            {
//                GrowthPlayer modPlayer = player.GetModPlayer<GrowthPlayer>();
//                float bonus = modPlayer.growthBonus;
//                TooltipLine line = new TooltipLine(Mod, "GrowthBonus", $"当前成长加成：{(bonus * 100f):F0}%");
//                line.OverrideColor = Color.LimeGreen;
//                tooltips.Add(line);
//            }
//        }
//    }
//}

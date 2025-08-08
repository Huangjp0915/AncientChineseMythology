using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class YaoQiFragment : ModItem
    {

        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/YaoQiFragment";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 50; //允许在旅程模式研究
        }

        public override void SetDefaults() {
            //基础属性
            Item.width = 24;          //物品宽度
            Item.height = 24;         //物品高度
            Item.maxStack = 999;      //堆叠上限
            Item.value = Item.buyPrice(silver: 5); //价值（可自行调整）
            Item.rare = ItemRarityID.Blue;         //稀有度
        }

        public override void PostUpdate() {
            //强制把物品的速度清零，让它停留在原地
            Item.velocity = Microsoft.Xna.Framework.Vector2.Zero;

            //让物品围绕一个小范围上下浮动
            //这里用正弦函数产生周期性移动，可自行调整速度和幅度
            float floatSpeed = 8f; //上下浮动的速度
            float floatRange = 0.08f; //上下浮动的幅度

            //计算一个正弦值（随时间变化）
            float sinValue = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * floatSpeed);

            //为了让浮动更平滑，可以在物品的垂直速度上加上一个小量
            Item.velocity.Y = sinValue * floatRange;
        }
    }
}

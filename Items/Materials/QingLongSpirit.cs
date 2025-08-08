using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class QingLongSpirit : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/QingLongSpirit";

        public override void SetDefaults() {
            //属性
            Item.width = 32;           //宽度
            Item.height = 32;          //高度
            Item.maxStack = 999;       //堆叠数
            Item.value = Item.buyPrice(silver: 250);   //价值
            Item.rare = ItemRarityID.LightRed;        //稀有度
        }

        public override void PostUpdate() {
            //强制把物品的速度清零，让它停留在原地
            Item.velocity = Microsoft.Xna.Framework.Vector2.Zero;

            //让物品围绕一个小范围上下浮动
            //这里用正弦函数产生周期性移动，可自行调整速度和幅度
            float floatSpeed = 10f;//上下浮动的速度
            float floatRange = 0.1f;//上下浮动的幅度

            //计算一个正弦值（随时间变化）
            float sinValue = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * floatSpeed);

            //为了让浮动更平滑，可以在物品的垂直速度上加上一个小量
            Item.velocity.Y = sinValue * floatRange;
        }
    }
}
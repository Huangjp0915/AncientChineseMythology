using Microsoft.Xna.Framework.Graphics;

namespace AncientChineseMythology
{
    [VaultLoaden("AncientChineseMythology/Textures/Masking")]
    internal class ACMAsset
    {
        /// <summary>
        /// 大小126*126，一个星星灰度图，灰底白光，通常用于复合一些星星类型的特效或者视觉效果，一般来讲，AlphaBlend绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D BlankStar;
        /// <summary>
        /// 大小512*512，一个剑气灰度图，白底，正面朝向右侧，通常用于复合一些剑气类型的特效或者视觉效果
        /// </summary>
        public static Texture2D GlaciateWave;
        /// <summary>
        /// 大小64*64，一个光弹灰度图，黑底，通常用于复合一些光弹类型的特效或者视觉效果，或者复合叠加出一下圆形的光效，一般来讲，AlphaBlend绘制时颜色的A值通道需要设置为0
        /// 同时我不得不说，此纹理正面朝向于右侧，是一个‘--》’形状，如果要绘制注意旋转角
        /// </summary>
        public static Texture2D LightShot;
        /// <summary>
        /// 总大小1024*1024，一个黑底的烟雾灰度图，帧数为16帧4行4列，通常用于复合一些烟雾类型的特效或者视觉效果，一般来讲，AlphaBlend绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D Smoke;
        /// <summary>
        /// 大小64*64，一个黑底的圆点灰度图，通常用于复合一些柔和光效类型的特效或者视觉效果，一般来讲，AlphaBlend绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D SoftGlow;
        /// <summary>
        /// 大小128*128，一个爆炸的线条效果灰度图，黑底白线，通常用于复合一些爆炸类型的特效或者视觉效果，一般来讲，AlphaBlend绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D Sparkle;
        /// <summary>
        /// 大小512*512（或实际尺寸），包含9个不规则碎片形状的纹理集
        /// 图像呈现为白色不规则多边形核心，边缘带有橙色柔和发光感
        /// 常用于制作：受击火花、物体破碎后的飞散残骸、或是魔法能量溢出时的颗粒特效
        /// 建议：在着色器中可以通过采样不同的UV区域来获得多样的碎片形态
        /// </summary>
        public static Texture2D EmberShards;
        /// <summary>
        /// 大小512*1024（或实际尺寸），一个黑底白色的放射状线条纹理
        /// 视觉上表现为从底部中心向顶部发散的尖锐光束，具有极强的垂直向速度感
        /// 常用于制作：向上喷发的剑气、地面崩裂时的能量溢出、打击瞬间的闪光（Hit Spark）或者作为角色突进时的拖尾特效
        /// 绘制建议：由于具有明显的单向发散性，适合配合缩放动画（Y轴拉长）来模拟瞬时的爆发力
        /// </summary>
        public static Texture2D SlashBurst;
        /// <summary>
        /// 大小512*1024，一个具有柔和外发光的雷电分叉灰度图
        /// 纹理主体为白色的不规则折线，带有半透明的晕染边缘，呈现垂直走向
        /// 常用于制作：闪电落下的特效、法术释放时的电弧、或是作为武器附魔时的流窜电火花
        /// </summary>
        public static Texture2D LightningBranch;
        /// <summary>
        /// 垂直排列的四组横向闪电电弧序列
        /// 每一组电弧都具有极高的亮度核心和剧烈的折线波动，充满不稳定的能量感
        /// 常用于制作：武器上的附魔电流、身体周围缠绕的电圈、或是法术蓄力时的粒子扰动
        /// 提示：可通过偏移UV坐标来随机获取其中一段电弧，以增加视觉多样性
        /// </summary>
        public static Texture2D ElectricArcSheet;
    }
}

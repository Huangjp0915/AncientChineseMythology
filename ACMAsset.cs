using Microsoft.Xna.Framework.Graphics;

namespace AncientChineseMythology
{
    [VaultLoaden("AncientChineseMythology/Textures/Masking")]
    internal class ACMAsset
    {
        /// <summary>
        /// 大小126*126，一个星星灰度图，灰底白光，通常用于复合一些星星类型的特效或者视觉效果，一般来讲，绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D BlankStar;
        /// <summary>
        /// 大小512*512，一个剑气灰度图，白底，正面朝向右侧，通常用于复合一些剑气类型的特效或者视觉效果
        /// </summary>
        public static Texture2D GlaciateWave;
        /// <summary>
        /// 大小64*64，一个光弹灰度图，黑底，通常用于复合一些光弹类型的特效或者视觉效果，或者复合叠加出一下圆形的光效，一般来讲，绘制时颜色的A值通道需要设置为0
        /// 同时我不得不说，此纹理正面朝向于右侧，是一个‘--》’形状，如果要绘制注意旋转角
        /// </summary>
        public static Texture2D LightShot;
        /// <summary>
        /// 总大小1024*1024，一个黑底的烟雾灰度图，帧数为16帧4行4列，通常用于复合一些烟雾类型的特效或者视觉效果，一般来讲，绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D Smoke;
        /// <summary>
        /// 大小128*128，一个爆炸的线条效果灰度图，黑底白线，通常用于复合一些爆炸类型的特效或者视觉效果，一般来讲，绘制时颜色的A值通道需要设置为0
        /// </summary>
        public static Texture2D Sparkle;
    }
}

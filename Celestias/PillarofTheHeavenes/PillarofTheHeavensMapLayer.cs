using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria.DataStructures;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱地图图层 - 在地图上显示四根天柱的图标
    /// </summary>
    internal class PillarofTheHeavensMapLayer : ModMapLayer
    {
        /// <summary>图标纹理路径</summary>
        private const string IconTexturePath = "AncientChineseMythology/Celestias/PillarofTheHeavenes/PillarofTheHeavensIcon";

        /// <summary>图标纹理缓存</summary>
        private static Asset<Texture2D> iconTexture;

        /// <summary>图标缩放比例</summary>
        private const float IconScale = 1f;

        /// <summary>悬停时图标缩放</summary>
        private const float HoverIconScale = 1.2f;

        /// <summary>图标脉冲动画速度</summary>
        private const float PulseSpeed = 0.03f;

        /// <summary>脉冲计时器</summary>
        private static float pulseTimer = 0f;

        /// <summary>四个天柱的方位名称</summary>
        private static readonly string[] PillarNames = ["东方天柱", "南方天柱", "西方天柱", "北方天柱"];

        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            // 检查天柱是否已降临
            if (!HeavenPillarSystem.PillarsDescended) return;

            // 加载图标纹理
            iconTexture ??= ModContent.Request<Texture2D>(IconTexturePath, AssetRequestMode.ImmediateLoad);
            if (iconTexture == null || !iconTexture.IsLoaded) return;

            Texture2D tex = iconTexture.Value;

            // 更新脉冲动画
            pulseTimer += PulseSpeed;

            // 遍历四根天柱绘制图标
            for (int i = 0; i < HeavenPillarSystem.PillarCount; i++) {
                Vector2 pillarPos = HeavenPillarSystem.PillarPositions[i];
                if (pillarPos == Vector2.Zero) continue;

                // 将世界坐标转换为地图坐标（tile坐标）
                Vector2 tilePos = pillarPos / 16f;

                // 计算脉冲效果（每个天柱有相位偏移）
                float pulse = 1f + MathF.Sin(pulseTimer + i * MathHelper.PiOver2) * 0.08f;
                float currentScale = IconScale * pulse;

                // 绘制图标
                MapOverlayDrawContext.DrawResult result = context.Draw(
                    tex,
                    tilePos,
                    Color.White,
                    new SpriteFrame(1, 1, 0, 0),
                    currentScale,
                    currentScale,
                    Alignment.Center
                );

                // 检查悬停状态
                if (result.IsMouseOver) {
                    // 设置悬停文本
                    text = PillarNames[i];

                    // 重新绘制放大的图标
                    context.Draw(
                        tex,
                        tilePos,
                        Color.White * 1.2f,
                        new SpriteFrame(1, 1, 0, 0),
                        HoverIconScale * pulse,
                        HoverIconScale * pulse,
                        Alignment.Center
                    );
                }
            }
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂(地表) 雷暴氛围演出层 — 风暴压暗 (ElementalScreenTint), 破绽窗口时转金白骤亮。
    /// 纯本地视觉 (toolkit §C.4 服务端零绘制 / §C.5 不占全屏后处理名额: ElementalScreenTint 只画占位像素)。
    /// 逻辑由 ArchosaurHead AI 服务器权威驱动; 本系统仅读取其同步态衍生的视觉标量。
    /// </summary>
    public class ArchosaurStormSystem : ModSystem
    {
        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            int who = ArchosaurHead.ActiveHead;
            if (who < 0 || who >= Main.maxNPCs)
                return;
            NPC npc = Main.npc[who];
            if (!npc.active || npc.ModNPC is not ArchosaurHead head)
                return;

            float storm = head.StormVisual;
            float window = head.WindowVisual;
            float intensity = MathHelper.Clamp(storm, 0f, 1f);
            if (intensity <= 0.02f)
                return;

            Effect tint = ACMShaders.ElementalScreenTint;
            if (tint == null)
                return;

            // 常态: 玄青雷暴压暗; 破绽窗口: 整体提亮泛金白 (输出时机的明确信号)
            Color top = Color.Lerp(new Color(26, 44, 78), new Color(255, 246, 200), window);
            Color low = Color.Lerp(new Color(10, 20, 44), new Color(120, 150, 210), window);
            ACMShaders.SetCommonParams(tint, npc.Center, intensity);
            tint.Parameters["uTint"]?.SetValue(new Vector4(top.R / 255f, top.G / 255f, top.B / 255f, 0.45f + 0.3f * window));
            tint.Parameters["uTint2"]?.SetValue(low.ToVector4());
            tint.Parameters["uVignette"]?.SetValue(0.42f - 0.18f * window);
            tint.Parameters["uFogScale"]?.SetValue(2.8f);
            ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
        }
    }
}

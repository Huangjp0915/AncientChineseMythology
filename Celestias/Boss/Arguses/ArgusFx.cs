using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 百目共用绘制原语: 凝视语法配色 (游移紫 → 收敛橙 → 锁死红) 与 A=0 加性视线绘制。
    /// 供悬停箭 (<see cref="StarSightArrows"/>) 等在默认批内画瞄准线, 零批切换。
    /// </summary>
    internal static class ArgusFx
    {
        public static readonly Color GazePurple = new(185, 105, 255);
        public static readonly Color ConvergeOrange = new(255, 170, 70);

        /// <summary>凝视语法取色: converge 0=游移紫 → 1=收敛橙; locked=锁死红。</summary>
        public static Color GazeColor(float converge, bool locked) =>
            locked ? TelegraphColors.Lethal : Color.Lerp(GazePurple, ConvergeOrange, ACMUtils.Clamp01(converge));

        /// <summary>
        /// 在**默认活动批** (AlphaBlend) 中用 A=0 加性 LightShot 画一条凝视视线 (零批切换)。
        /// 双层: 宽淡辉光 + 细亮芯。LightShot 为 64² 右向图。
        /// </summary>
        public static void DrawSightLine(SpriteBatch sb, Vector2 worldStart, Vector2 dir, float length,
            Color color, float alpha, float coreWidth = 3f) {
            Texture2D tex = ACMAsset.LightShot;
            if (tex == null || alpha <= 0.01f)
                return;

            float rot = dir.ToRotation();
            Vector2 pos = worldStart - Main.screenPosition;
            Vector2 origin = new(2f, tex.Height / 2f);
            Vector2 scale = new(length / (tex.Width - 4f), coreWidth * 3f / tex.Height);

            Color glow = color * (alpha * 0.35f);
            glow.A = 0;
            sb.Draw(tex, pos, null, glow, rot, origin, scale, SpriteEffects.None, 0f);

            Color core = Color.Lerp(color, Color.White, 0.35f) * alpha;
            core.A = 0;
            sb.Draw(tex, pos, null, core, rot, origin, new Vector2(scale.X, coreWidth / tex.Height), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// "被看见"走廊检测: 本地玩家是否处于 (from, dir) 视线走廊内; 命中则向
        /// <see cref="ArgusScreenSystem.ReportGazeThreat"/> 汇报屏幕边缘警示 (纯本地视觉)。
        /// </summary>
        public static void ReportIfLocalPlayerSighted(Vector2 from, Vector2 dir, float maxLen,
            float corridorHalfWidth, float amount) {
            if (Main.dedServ)
                return;
            Player lp = Main.LocalPlayer;
            if (lp == null || !lp.active || lp.dead)
                return;
            Vector2 toP = lp.Center - from;
            float along = Vector2.Dot(toP, dir);
            if (along < 0f || along > maxLen)
                return;
            float lateral = System.MathF.Abs(toP.X * dir.Y - toP.Y * dir.X);
            if (lateral < corridorHalfWidth)
                ArgusScreenSystem.ReportGazeThreat(amount);
        }
    }
}

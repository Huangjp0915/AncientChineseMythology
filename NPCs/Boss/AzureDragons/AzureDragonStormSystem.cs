using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙雷暴演出层 — 风暴压暗 (ElementalScreenTint) + 律令天闪 + 审判庭网格地纹 (ArenaRunic)。
    /// 纯本地视觉 (toolkit §C.4 服务端零绘制 / §C.5 全屏后处理单层)。逻辑由 Boss AI 服务器权威驱动。
    /// 在 PostDrawTiles (无活动批) 阶段绘制, 用 DrawFullscreenOverlay / DrawScreenSpaceDecalStandalone。
    /// </summary>
    public class AzureDragonStormSystem : ModSystem
    {
        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            int who = AzureDragonHead.ActiveHead;
            if (who < 0 || who >= Main.maxNPCs)
                return;
            NPC npc = Main.npc[who];
            if (!npc.active || npc.ModNPC is not AzureDragonHead head)
                return;

            float storm = head.StormVisual;
            float flash = head.EdictFlash;
            float intensity = MathHelper.Clamp(storm + flash * 0.6f, 0f, 1f);
            if (intensity <= 0.01f)
                return;

            // —— 风暴压暗氛围 (含律令切换的天闪: flash 时整体提亮泛白) ——
            Effect tint = ACMShaders.ElementalScreenTint;
            if (tint != null) {
                Color top = Color.Lerp(new Color(18, 40, 70), new Color(150, 210, 255), flash);
                Color low = Color.Lerp(new Color(8, 18, 40), new Color(60, 120, 200), flash);
                ACMShaders.SetCommonParams(tint, npc.Center, intensity);
                tint.Parameters["uTint"]?.SetValue(new Vector4(top.R / 255f, top.G / 255f, top.B / 255f, 0.5f + 0.35f * flash));
                tint.Parameters["uTint2"]?.SetValue(low.ToVector4());
                tint.Parameters["uVignette"]?.SetValue(0.45f);
                tint.Parameters["uFogScale"]?.SetValue(2.6f);
                ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
            }

            // —— 审判庭网格地纹 (单层全屏 SDF, 仅 P3 set-piece) ——
            if (head.TribunalActive) {
                Effect runic = ACMShaders.ArenaRunic;
                if (runic != null) {
                    ACMShaders.WorldDecalParams(head.ArenaCenter, head.ArenaRadius,
                        out Vector2 uvCenter, out float radiusFrac, out float aspect);
                    runic.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    runic.Parameters["uCenter"]?.SetValue(uvCenter);
                    runic.Parameters["uRadius"]?.SetValue(radiusFrac);
                    runic.Parameters["uAspect"]?.SetValue(aspect);
                    runic.Parameters["uIntensity"]?.SetValue(0.6f * head.TribunalVisual);
                    runic.Parameters["uColorPrimary"]?.SetValue(AzureDragon.DragonCyan.ToVector4());
                    runic.Parameters["uColorSecondary"]?.SetValue(AzureDragon.DragonLightning.ToVector4());
                    runic.Parameters["uRuneFreq"]?.SetValue(12f);
                    runic.Parameters["uMode"]?.SetValue(0f);
                    runic.Parameters["uShape"]?.SetValue(0f);
                    ACMShaders.DrawScreenSpaceDecalStandalone(runic, BlendState.Additive);
                }
            }
        }
    }
}

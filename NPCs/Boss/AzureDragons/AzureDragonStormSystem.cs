using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙雷暴演出层 — 风暴压暗(ElementalScreenTint) + 天闪 + 审判庭地纹(ArenaRunic)
    /// + 云雾涡旋(AzureDragonMist, 队列消费) + 天雷条带(LightningBranch) + 雨霁暖光。
    /// 纯本地视觉, 服务端零绘制; 逻辑均由 Boss AI 的已同步状态派生。
    /// 在 PostDrawTiles (无活动批) 绘制 — 雾与地纹在实体层之下, 龙行雾上。
    /// </summary>
    public class AzureDragonStormSystem : ModSystem
    {
        #region 雾涡请求队列 (头部客户端路径发布, 本系统逐帧消费)

        public struct MistPuff
        {
            public Vector2 Center;
            public float Radius;
            public float Intensity;
            public float Swirl;
            public Color Color;
        }

        private static readonly List<MistPuff> MistQueue = [];

        /// <summary>入队一个雾涡 (客户端调用; 每帧至多绘制 6 个)。</summary>
        public static void QueueMist(Vector2 center, float radiusPx, float intensity, float swirl, Color color) {
            if (MistQueue.Count < 24)
                MistQueue.Add(new MistPuff {
                    Center = center, Radius = radiusPx, Intensity = intensity, Swirl = swirl, Color = color,
                });
        }

        #endregion

        #region 天雷条带 (LightningBranch 视觉落雷)

        private struct SkyBolt
        {
            public Vector2 Top;
            public float Life;
            public float Rotation;
            public float Scale;
        }

        private readonly List<SkyBolt> skyBolts = [];
        private float prevFlash;

        #endregion

        private static Asset<Effect> mistRef;

        private static Effect MistEffect {
            get {
                if (Main.dedServ)
                    return null;
                mistRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AzureDragonMist", AssetRequestMode.ImmediateLoad);
                return mistRef?.Value;
            }
        }

        public override void OnWorldUnload() {
            MistQueue.Clear();
            skyBolts.Clear();
        }

        public override void PostDrawTiles() {
            try {
                if (Main.dedServ || Main.gameMenu)
                    return;

                int who = AzureDragonHead.ActiveHead;
                if (who < 0 || who >= Main.maxNPCs)
                    return;
                NPC npc = Main.npc[who];
                if (!npc.active || npc.ModNPC is not AzureDragonHead head)
                    return;

                UpdateSkyBolts(head);

                if (!MythologyConfig.FullscreenShadersEnabled) {
                    DrawSkyBolts();
                    return;
                }

                DrawStormTint(npc, head);
                DrawTribunalGrid(head);
                DrawMistQueue();
                DrawSkyBolts();
            }
            finally {
                MistQueue.Clear();
            }
        }

        #region 风暴压暗 + 天闪 + 雨霁

        private void DrawStormTint(NPC npc, AzureDragonHead head) {
            float storm = head.StormVisual;
            float flash = head.SkyFlash;
            float dawn = head.DawnVisual;
            float intensity = MathHelper.Clamp(storm + flash * 0.6f + dawn * 0.55f, 0f, 1f);
            if (intensity <= 0.01f)
                return;

            Effect tint = ACMShaders.ElementalScreenTint;
            if (tint == null)
                return;

            // 风暴深蓝 → 天闪时提亮泛白 → 雨霁转暖金 (死亡收尾的诗意换色)
            Color top = Color.Lerp(new Color(18, 40, 70), new Color(150, 210, 255), flash);
            Color low = Color.Lerp(new Color(8, 18, 40), new Color(60, 120, 200), flash);
            top = Color.Lerp(top, new Color(255, 196, 130), dawn);
            low = Color.Lerp(low, new Color(190, 120, 90), dawn);

            ACMShaders.SetCommonParams(tint, npc.Center, intensity);
            tint.Parameters["uTint"]?.SetValue(new Vector4(top.R / 255f, top.G / 255f, top.B / 255f, 0.5f + 0.35f * flash));
            tint.Parameters["uTint2"]?.SetValue(low.ToVector4());
            tint.Parameters["uVignette"]?.SetValue(0.45f * (1f - dawn));
            tint.Parameters["uFogScale"]?.SetValue(2.6f);
            ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
        }

        #endregion

        #region 审判庭网格地纹

        private void DrawTribunalGrid(AzureDragonHead head) {
            if (!head.TribunalActive && head.TribunalVisual <= 0.02f)
                return;
            Effect runic = ACMShaders.ArenaRunic;
            if (runic == null)
                return;

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

        #endregion

        #region 云雾涡旋

        private void DrawMistQueue() {
            if (MistQueue.Count == 0)
                return;
            Effect fx = MistEffect;
            if (fx == null)
                return;

            int drawn = 0;
            foreach (MistPuff puff in MistQueue) {
                if (drawn >= 6)
                    break;
                if (puff.Intensity <= 0.02f)
                    continue;

                ACMShaders.WorldDecalParams(puff.Center, puff.Radius,
                    out Vector2 uvCenter, out float radiusFrac, out float aspect);
                // 完全离屏的雾涡直接跳过
                if (uvCenter.X < -0.4f || uvCenter.X > 1.4f || uvCenter.Y < -0.4f || uvCenter.Y > 1.4f)
                    continue;

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(puff.Intensity, 0f, 1f));
                fx.Parameters["uCenter"]?.SetValue(uvCenter);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColor"]?.SetValue(new Vector4(puff.Color.ToVector3(), 0.7f));
                fx.Parameters["uSwirl"]?.SetValue(puff.Swirl);
                fx.Parameters["uSoftEdge"]?.SetValue(0.55f);
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
                drawn++;
            }
        }

        #endregion

        #region 天雷条带

        private void UpdateSkyBolts(AzureDragonHead head) {
            // 天闪跃升沿: 劈 2~3 道视觉天雷
            float flash = head.SkyFlash;
            if (flash > 0.55f && prevFlash <= 0.55f) {
                int count = Main.rand.Next(2, 4);
                for (int i = 0; i < count; i++)
                    SpawnSkyBolt();
            }
            prevFlash = flash;

            // P3 重风暴下的零星环境落雷
            if (head.StormVisual > 0.6f && Main.rand.NextBool(210))
                SpawnSkyBolt();

            for (int i = skyBolts.Count - 1; i >= 0; i--) {
                SkyBolt b = skyBolts[i];
                b.Life -= 1f / 14f;
                skyBolts[i] = b;
                if (b.Life <= 0f)
                    skyBolts.RemoveAt(i);
            }
        }

        private void SpawnSkyBolt() {
            if (skyBolts.Count >= 5)
                return;
            skyBolts.Add(new SkyBolt {
                Top = Main.screenPosition + new Vector2(Main.rand.NextFloat(0.1f, 0.9f) * Main.screenWidth, -80f),
                Life = 1f,
                Rotation = Main.rand.NextFloat(-0.16f, 0.16f),
                Scale = Main.rand.NextFloat(1.1f, 1.7f),
            });
        }

        private void DrawSkyBolts() {
            if (skyBolts.Count == 0 || ACMAsset.LightningBranch == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightningBranch;
            foreach (SkyBolt b in skyBolts) {
                float a = b.Life * b.Life;
                Color col = AzureDragon.DragonLightning * (0.75f * a);
                col.A = 0;
                Vector2 pos = b.Top - Main.screenPosition;
                sb.Draw(tex, pos, null, col, b.Rotation, new Vector2(tex.Width / 2f, 0f),
                    new Vector2(b.Scale * 0.7f, b.Scale), SpriteEffects.None, 0f);
            }

            sb.End();
        }

        #endregion
    }
}

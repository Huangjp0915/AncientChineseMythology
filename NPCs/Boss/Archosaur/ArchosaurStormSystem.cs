using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂 V3 雷暴演出层 — 专属 <c>ArchosaurStormSky</c> 天幕 (云海压暗 + 雨丝 + 白闪 + 破绽金化)
    /// 与天空闪电事件队列。纯本地视觉: 标量由 <see cref="ArchosaurHead"/> 每帧发布, 闪电由 AI 在确定性
    /// 时刻投递; overlay 为占位像素全屏绘制, 不读 screenTarget、不占全屏后处理名额。
    /// </summary>
    public class ArchosaurStormSystem : ModSystem
    {
        // ===== 视觉标量 (客户端) =====
        private static float storm;          // 雷暴强度 (慢趋近)
        private static float stormTarget;
        private static float window;         // 破绽金化 (慢趋近)
        private static float windowTarget;
        private static float flash;          // 天空白闪 (事件驱动, 快衰减)
        private static ulong lastPublishFrame;

        // ===== 天空闪电事件 =====
        private struct SkyBolt
        {
            public Vector2 Strike;   // 落点(世界坐标)
            public float Lean;       // 顶端横向倾斜(px)
            public float Seed;
            public float Width;      // 半宽(px)
            public int Life;
            public int MaxLife;
        }

        private const int MaxBolts = 10;
        private static readonly List<SkyBolt> bolts = new(MaxBolts);

        /// <summary>头部 AI 每帧发布雷暴/破绽目标强度 (仅客户端调用生效)。</summary>
        public static void Publish(float stormLevel, float windowLevel) {
            if (Main.dedServ)
                return;
            stormTarget = MathHelper.Clamp(stormLevel, 0f, 1f);
            windowTarget = MathHelper.Clamp(windowLevel, 0f, 1f);
            lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>叠加一次天空白闪 (0~1)。</summary>
        public static void AddFlash(float amount) {
            if (!Main.dedServ)
                flash = Math.Max(flash, MathHelper.Clamp(amount, 0f, 1f));
        }

        /// <summary>
        /// 投一道天空闪电 (从屏幕上方劈到 <paramref name="strikePos"/>)。附带白闪与雷声。
        /// 仅客户端生效; AI 侧请在确定性时刻调用并以 !Main.dedServ 守卫。
        /// </summary>
        public static void AddSkyBolt(Vector2 strikePos, float widthScale = 1f, float flashAmount = 0.45f, bool thunder = true) {
            if (Main.dedServ)
                return;
            if (bolts.Count >= MaxBolts)
                bolts.RemoveAt(0);
            bolts.Add(new SkyBolt {
                Strike = strikePos,
                Lean = Main.rand.NextFloat(-260f, 260f),
                Seed = Main.rand.NextFloat(0.05f, 0.95f),
                Width = 46f * widthScale,
                Life = 18,
                MaxLife = 18,
            });
            AddFlash(flashAmount);
            if (thunder)
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.75f, Pitch = Main.rand.NextFloat(-0.25f, 0.15f) }, strikePos);
        }

        /// <summary>战斗结束/Boss 消失时的收尾 (标量自然衰减, 闪电清空)。</summary>
        public static void ClearBolts() => bolts.Clear();

        public override void ClearWorld() {
            bolts.Clear();
            storm = stormTarget = window = windowTarget = flash = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;

            // 标量演化 (无发布方时自动回落)
            if (Main.GameUpdateCount - lastPublishFrame > 8) {
                stormTarget = 0f;
                windowTarget = 0f;
            }
            storm = MathHelper.Lerp(storm, stormTarget, 0.025f);
            window = MathHelper.Lerp(window, windowTarget, 0.05f);
            flash *= 0.88f;
            if (flash < 0.01f)
                flash = 0f;

            if (!MythologyConfig.FullscreenShadersEnabled) {
                bolts.Clear();
                return;
            }

            DrawOverlay();
            DrawSkyBolts();
        }

        private static void DrawOverlay() {
            if (storm + flash < 0.015f)
                return;
            Effect fx = ArchosaurVFX.StormSky;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(storm);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uWindow"]?.SetValue(window);
            fx.Parameters["uRain"]?.SetValue(MathHelper.Clamp(storm * 1.15f - 0.18f, 0f, 1f));
            fx.Parameters["uVignette"]?.SetValue(0.40f - 0.20f * window);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        private static void DrawSkyBolts() {
            if (bolts.Count == 0)
                return;
            for (int i = bolts.Count - 1; i >= 0; i--) {
                SkyBolt b = bolts[i];
                float p = b.Life / (float)b.MaxLife;
                float intensity = MathF.Pow(p, 0.6f);
                Vector2 top = new(b.Strike.X + b.Lean, Main.screenPosition.Y - 300f);
                ArchosaurVFX.DrawLightningStrip(top, b.Strike, b.Width,
                    ArchosaurVFX.BoltCore, TelegraphColors.Lightning, intensity, b.Seed,
                    jagAmp: 0.62f, flicker: 0.25f, hasActiveBatch: false);
                b.Life--;
                if (b.Life <= 0)
                    bolts.RemoveAt(i);
                else
                    bolts[i] = b;
            }
        }
    }
}

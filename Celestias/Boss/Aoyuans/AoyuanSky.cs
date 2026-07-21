using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 西海龙王敖闰天空 - 静渊寒夜
    /// 深海夜幕 + 三层极光帘 + 程序化飘雪 + 入场天际剪影; 强度随战斗状态驱动
    /// （时滞破境/死亡演出时天幕额外压暗, 读 Aoyuan.StillFxFactor）
    /// </summary>
    internal class AoyuanSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.7f;
        private Color skyColor;
        private float pulsePhase;
        private float stillDarken;   // 时滞压暗（读 Boss StillFxFactor 平滑）

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:AoyuanSky";
            SkyManager.Instance[name] = new AoyuanSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) => active = false;
        public override void Reset() { active = false; intensity = 0.01f; }
        public override bool IsActive() => active;

        public override void Update(GameTime gameTime) {
            pulsePhase += 0.03f;

            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                // 冰霜色阶：深海蓝 -> 暗青 -> 极夜黑
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(8, 20, 60),    // 深海蓝（最压迫）
                    new Color(15, 40, 80),   // 暗青蓝
                    new Color(10, 15, 30));   // 极夜黑（远距离）

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                // 二阶段天空更暗更冷; 时滞演出额外压暗
                float lifePercent = (float)boss.life / boss.lifeMax;
                if (lifePercent < 0.5f) {
                    maxIntensity = 0.85f;
                    skyColor = Color.Lerp(skyColor, new Color(5, 10, 40), 0.3f);
                }
                float stillTarget = boss.ModNPC is Aoyuan a ? a.StillFxFactor : 0f;
                stillDarken = MathHelper.Lerp(stillDarken, stillTarget, 0.08f);

                active = true;
            }
            else {
                stillDarken = MathHelper.Lerp(stillDarken, 0f, 0.1f);
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0f) return;
            if (!(maxDepth >= 0 && minDepth < 0)) return;

            DrawBackdrop(spriteBatch);
            DrawAurora(spriteBatch);
            DrawSnow(spriteBatch);
            DrawIntroSilhouette(spriteBatch);
        }

        #region 层1 — 静渊夜幕

        private void DrawBackdrop(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            // 天幕底色（时滞演出时额外压暗）
            float darken = 1f + stillDarken * 0.5f;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                skyColor * (intensity * darken));

            // 深蓝呼吸叠加层
            float breathAlpha = (0.5f + MathF.Sin(pulsePhase * 1.5f) * 0.5f) * intensity * 0.15f;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(20, 50, 120) * breathAlpha);

            // 时滞: 全屏冷灰罩（时间冻结的窒息感）
            if (stillDarken > 0.02f) {
                sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    new Color(8, 14, 28) * (stillDarken * 0.55f));
            }
        }

        #endregion

        #region 层2 — 三层极光帘

        private void DrawAurora(SpriteBatch sb) {
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave == null) return;

            Vector2 origin = wave.Size() / 2f;
            for (int layer = 0; layer < 3; layer++) {
                float phase = pulsePhase * (0.5f + layer * 0.22f) + layer * 2.1f;
                float y = Main.screenHeight * (0.10f + layer * 0.075f) + MathF.Sin(phase * 0.7f) * 26f;
                int bands = 4;
                for (int b = 0; b <= bands; b++) {
                    float x = Main.screenWidth * b / (float)bands + MathF.Sin(phase + b * 1.7f) * 60f;
                    float sway = MathF.Sin(phase * 1.3f + b * 2.3f) * 0.22f;
                    Color c = Color.Lerp(AoyuanHelper.WestSeaTeal, AoyuanHelper.FrostCyan, (layer + b % 2) / 3.5f);
                    float alpha = (0.05f + 0.035f * MathF.Sin(phase * 0.9f + b)) * intensity * (1f - stillDarken * 0.6f);
                    if (alpha <= 0.004f) continue;
                    c *= alpha;
                    c.A = 0;
                    sb.Draw(wave, new Vector2(x, y), null, c, sway,
                        origin, new Vector2(1.7f, 0.55f - layer * 0.1f), SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层3 — 程序化飘雪（确定性伪随机, 无状态）

        private void DrawSnow(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;

            Vector2 origin = glow.Size() / 2f;
            float time = pulsePhase * 33f; // 帧级时间
            const int flakes = 46;
            for (int i = 0; i < flakes; i++) {
                // 每片雪的确定性参数
                float seedA = (i * 0.618034f) % 1f;
                float seedB = (i * 0.381966f + 0.5f) % 1f;
                float fall = 0.55f + seedA * 1.1f;
                float drift = MathF.Sin(time * 0.02f + i * 1.37f) * 34f;

                float x = (seedA * Main.screenWidth + drift + time * (0.2f + seedB * 0.3f)) % Main.screenWidth;
                if (x < 0) x += Main.screenWidth;
                float y = (seedB * Main.screenHeight + time * fall) % Main.screenHeight;

                float tw = 0.5f + MathF.Sin(time * 0.09f + i * 2.3f) * 0.5f;
                Color c = AoyuanHelper.IceCrystalWhite * ((0.10f + tw * 0.14f) * intensity);
                c.A = 0;
                sb.Draw(glow, new Vector2(x, y), null, c, 0f, origin,
                    0.05f + seedB * 0.07f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 入场天际剪影（龙影两次掠过）

        private void DrawIntroSilhouette(SpriteBatch sb) {
            NPC boss = GetBoss();
            if (boss?.ModNPC is not Aoyuan aoyuan) return;
            float progress = aoyuan.IntroSilhouetteProgress;
            if (progress < 0f) return;

            Texture2D headTex = TextureAssets.Npc[boss.type].Value;
            Texture2D bodyTex = TextureAssets.Npc[ModContent.NPCType<AoyuanBody>()].Value;
            if (headTex == null || bodyTex == null) return;

            // 两段掠过: [0,0.5) 远景右→左偏高; [0.5,1) 近景左→右偏低
            bool farPass = progress < 0.5f;
            float p = farPass ? progress / 0.5f : (progress - 0.5f) / 0.5f;
            float ease = AoyuanHelper.SineInOut(p);

            float scale = farPass ? 0.5f : 0.85f;
            float y0 = Main.screenHeight * (farPass ? 0.22f : 0.34f);
            float xStart = farPass ? Main.screenWidth + 220f : -220f;
            float xEnd = farPass ? -220f : Main.screenWidth + 220f;
            float x = MathHelper.Lerp(xStart, xEnd, ease);
            int dir = farPass ? -1 : 1;

            // 渐入渐出
            float alpha = MathF.Sin(p * MathF.PI);
            Color silCol = new Color(16, 34, 62) * (alpha * intensity * 1.2f);

            // 沿正弦路径排列身体段（头在前）
            const int segs = 12;
            float segGap = 34f * scale;
            Rectangle headFrame = new(0, 0, headTex.Width, headTex.Height / 3);
            int bodyFrameH = bodyTex.Height / 5;
            Rectangle bodyFrame = new(0, bodyFrameH, bodyTex.Width, bodyFrameH);

            for (int s = segs - 1; s >= 0; s--) {
                float sx = x - dir * s * segGap;
                float sy = y0 + MathF.Sin(pulsePhase * 2.2f + s * 0.55f) * 26f * scale;
                Vector2 pos = new(sx, sy);
                float rot = MathF.Atan2(
                    MathF.Cos(pulsePhase * 2.2f + s * 0.55f) * 26f * scale * 0.55f,
                    dir * segGap) + MathHelper.PiOver2 + (dir < 0 ? MathHelper.Pi : 0f);

                if (s == 0) {
                    sb.Draw(headTex, pos, headFrame, silCol, rot,
                        headFrame.Size() / 2f, scale * 0.8f, SpriteEffects.None, 0f);
                }
                else {
                    sb.Draw(bodyTex, pos, bodyFrame, silCol * 0.92f, rot,
                        bodyFrame.Size() / 2f, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        public override Color OnTileColor(Color inColor) {
            // 所有地表颜色偏蓝/变暗; 时滞时进一步失色
            Color desaturated = Color.Lerp(inColor, new Color(30, 50, 80), 0.3f);
            Color result = Color.Lerp(inColor, desaturated, intensity);
            if (stillDarken > 0.02f)
                result = Color.Lerp(result, new Color(40, 55, 85), stillDarken * 0.4f);
            return result;
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.6f;
        }

        private NPC GetBoss() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<Aoyuan>())
                    return npc;
            }
            return null;
        }
    }
}

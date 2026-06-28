using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    internal class AncestralDragonEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<AncestralDragonSoulHead>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(AncestralDragonSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 祖龙残魂天空 — 程序化HLSL天幕
    /// 域扭曲fbm云海 + 龙鳞光轮 + Voronoi星辰, 随Boss阶段切换玄青/紫芒/赤金色调
    /// </summary>
    public class AncestralDragonSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:AncestralDragonSky";

        private bool active;
        private float intensity;
        private float globalTime;
        private float phase;// 0=常态 0.5=二阶段 1.0=暴怒
        private float pulsePhase;
        private float bossHealthPercent = 1f;
        private float flash;

        private const float FadeInSpeed = 0.012f;
        private const float FadeOutSpeed = 0.02f;

        private static Asset<Effect> skyEffectRef;

        // 分裂/回拢/解锁等大节拍触发的天幕亮拍 (静态待发, 由实例消费)
        private static float pendingFlash;

        /// <summary>由 Boss 在分裂/双魂回拢/碎片解锁等节拍触发一次天幕亮拍 (纯本地视觉)。</summary>
        public static void TriggerFlash(float amount) {
            if (amount > pendingFlash) pendingFlash = amount;
        }

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            // 保留一个轻度屏幕滤镜压暗场景, 让shader天幕更突出
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.0f, 0.0f, 0.0f)
                .UseOpacity(0.35f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() => active || intensity > 0.01f;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            globalTime += delta;
            pulsePhase += delta * MathHelper.Lerp(1.6f, 4.0f, phase);

            // 消费待发亮拍并衰减
            if (pendingFlash > flash) flash = pendingFlash;
            pendingFlash = 0f;
            flash = MathHelper.Lerp(flash, 0f, 0.06f);

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive) {
                if (!active) Activate(Vector2.Zero);

                bossHealthPercent = (float)boss.life / boss.lifeMax;

                // 阶段过渡映射血量
                float targetPhase;
                if (bossHealthPercent > 0.6f) targetPhase = 0f;
                else if (bossHealthPercent > 0.3f) targetPhase = MathHelper.Lerp(0f, 0.5f, (0.6f - bossHealthPercent) / 0.3f);
                else targetPhase = MathHelper.Lerp(0.5f, 1.0f, MathHelper.Clamp((0.3f - bossHealthPercent) / 0.2f, 0f, 1f));
                phase = MathHelper.Lerp(phase, targetPhase, 0.02f);

                intensity = MathHelper.Lerp(intensity, 1f, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) {
                    intensity = 0f;
                    if (active) Deactivate();
                }
            }
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<AncestralDragonSoulHead>() && npc.active) {
                    return npc;
                }
            }
            return null;
        }

        public static bool IsBossActive() => FindBoss() != null;

        private static Effect GetSkyEffect() {
            skyEffectRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/AncestralDragonSky",
                AssetRequestMode.ImmediateLoad);
            return skyEffectRef?.Value;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            // 仅在最远背景深度层绘制
            if (!(maxDepth >= 0 && minDepth < 0)) return;
            if (intensity <= 0.01f) return;

            Effect fx = GetSkyEffect();
            Texture2D dummy = ACMAsset.BlankStar;
            if (fx == null || dummy == null) return;

            // Boss屏幕归一化坐标
            Vector2 bossUV = new Vector2(0.5f, 0.35f);
            NPC boss = FindBoss();
            if (boss != null) {
                Vector2 sp = boss.Center - Main.screenPosition;
                bossUV = new Vector2(
                    MathHelper.Clamp(sp.X / Main.screenWidth, -0.5f, 1.5f),
                    MathHelper.Clamp(sp.Y / Main.screenHeight, -0.5f, 1.5f)
                );
            }

            float aspect = Main.screenWidth / (float)Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uPhase"]?.SetValue(MathHelper.Clamp(phase, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uResolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            fx.Parameters["uBossUV"]?.SetValue(bossUV);
            fx.Parameters["uPulse"]?.SetValue(pulsePhase);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1.5f));

            // 切换SpriteBatch为带shader的Immediate模式, 全屏绘制后恢复
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);

            spriteBatch.Draw(dummy, new Rectangle(0, 0, Main.screenWidth * 2, Main.screenHeight * 2), Color.White);

            spriteBatch.End();
            // 恢复默认
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        }

        public override Color OnTileColor(Color inColor) {
            // 随阶段调整瓦片染色
            Color phaseTint = Color.Lerp(
                new Color(200, 215, 240),
                new Color(230, 170, 230),
                MathHelper.Clamp(phase * 2f, 0f, 1f));
            phaseTint = Color.Lerp(phaseTint,
                new Color(255, 180, 140),
                MathHelper.Clamp((phase - 0.5f) * 2f, 0f, 1f));

            Color tint = Color.Lerp(Color.White, phaseTint, intensity * 0.35f);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.9f;
        }
    }
}

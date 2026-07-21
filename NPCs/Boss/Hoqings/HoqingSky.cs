using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    /// <summary>
    /// 后卿天幕 V3 —— 程序化疠雾 + 冥月 (HoqingPlagueMiasma 专属着色器)。
    /// 冥月随战斗推进"睁眼"：幕一病绿淡月 → 幕二月晕扩大 → 幕三渗血环（血月）。
    /// <see cref="TriggerFlash"/> 供入场怒吼/进幕三/死亡爆点打一次天幕亮拍。
    /// 注册名 <see cref="name"/> 与 <see cref="LoadInstance"/> 由 ACMMod.Load 调用, 不可变。
    /// </summary>
    internal class HoqingSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float MaxIntensity = 0.8f;
        private Color skyColor;
        private float globalTime;
        private float moonPhase;   //0 幕一 → 0.5 幕二 → 1 幕三 (月晕/亮度)
        private float moonBlood;   //0~1 冥月渗血 (幕三/死亡)
        private float flash;       //天幕亮拍

        internal static string name;

        //大节拍亮拍 (静态待发, 由实例消费; 各端本地视觉)
        private static float pendingFlash;

        private static Asset<Effect> miasmaRef;
        private static Effect MiasmaFX {
            get {
                if (Main.dedServ) {
                    return null;
                }
                miasmaRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/HoqingPlagueMiasma", AssetRequestMode.ImmediateLoad);
                return miasmaRef?.Value;
            }
        }

        public static void LoadInstance() {
            name = "AncientChineseMythology:HoqingSky";
            SkyManager.Instance[name] = new HoqingSky();
        }

        /// <summary>由 Boss 在入场怒吼/进幕三/死亡爆点触发一次天幕亮拍 (纯本地视觉)。</summary>
        public static void TriggerFlash(float amount) {
            if (amount > pendingFlash) {
                pendingFlash = amount;
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override bool IsActive() => active;

        private static NPC FindBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Hoqing>()) {
                    return npc;
                }
            }
            return null;
        }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //消费待发亮拍并衰减
            if (pendingFlash > flash) {
                flash = pendingFlash;
            }
            pendingFlash = 0f;
            flash = MathHelper.Lerp(flash, 0f, 0.055f);

            NPC boss = FindBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                //瘟疫亡灵色阶：腐绿 -> 病黄绿 -> 尸绿魂光（越近越病态）
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(18, 34, 22),    //腐暗绿（最压迫）
                    new Color(46, 78, 40),    //病黄绿
                    new Color(96, 170, 90));  //尸绿魂光（近Boss时）

                //冥月睁眼: 由阶段推导 (ai[0] 为同步状态, 各端一致)
                var phase = (Hoqing.BossPhase)(int)boss.ai[0];
                bool inP3 = phase is Hoqing.BossPhase.P3_AltarRush or Hoqing.BossPhase.P3_AltarChannel
                    or Hoqing.BossPhase.P3_AltarRelease or Hoqing.BossPhase.P3_GhostGate
                    or Hoqing.BossPhase.DeathThroes;
                bool inP2 = phase is Hoqing.BossPhase.P2_Hover or Hoqing.BossPhase.P2_SputumRain
                    or Hoqing.BossPhase.P2_CorpseChain or Hoqing.BossPhase.P2_PlagueCorridor
                    or Hoqing.BossPhase.P2_LanternRing or Hoqing.BossPhase.P2_PhantomSweep;
                float targetMoon = inP3 ? 1f : inP2 ? 0.5f : 0.2f;
                moonPhase = MathHelper.Lerp(moonPhase, targetMoon, 0.015f);
                moonBlood = MathHelper.Lerp(moonBlood, inP3 ? 1f : 0f, 0.02f);

                if (intensity < MaxIntensity) {
                    intensity += 0.01f;
                }
                active = true;
            }
            else {
                moonBlood = MathHelper.Lerp(moonBlood, 0f, 0.03f);
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //仅在最远背景深度层绘制一次
            if (!(maxDepth >= 0 && minDepth < 0)) {
                return;
            }
            if (intensity <= 0.01f) {
                return;
            }

            //底色: 病态压迫的整幕染色 (轻微幽颤)
            Vector2 shake = Main.rand.NextVector2Circular(1.5f * intensity, 1.5f * intensity);
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            //程序化疠雾 + 冥月 (专属着色器)
            Effect fx = MiasmaFX;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null) {
                return;
            }

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity * (0.6f + 0.4f * moonPhase), 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uMoonPos"]?.SetValue(new Vector2(0.68f, 0.20f));
            fx.Parameters["uMoonRadius"]?.SetValue(0.06f + 0.035f * moonPhase);
            fx.Parameters["uMoonBlood"]?.SetValue(MathHelper.Clamp(moonBlood, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uColorMistA"]?.SetValue(new Color(24, 52, 30).ToVector4());
            fx.Parameters["uColorMistB"]?.SetValue(new Color(74, 120, 56).ToVector4());
            fx.Parameters["uColorMoon"]?.SetValue(new Color(150, 235, 140).ToVector4());

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            spriteBatch.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        }

        public override Color OnTileColor(Color inColor) {
            //所有地表颜色染上病态尸绿/失色; 血月期再偏红一分
            Color desaturated = Color.Lerp(inColor, new Color(60, 90, 55), 0.45f);
            desaturated = Color.Lerp(desaturated, new Color(96, 62, 52), 0.25f * moonBlood);
            Color result = Color.Lerp(inColor, desaturated, intensity);
            //天幕亮拍瞬间提亮地表 (死亡白闪的地面回应)
            return Color.Lerp(result, Color.White, flash * 0.55f);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.85f;
        }
    }
}

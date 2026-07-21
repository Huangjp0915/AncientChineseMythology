using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天御金龙 V3 屏幕氛围系统。由 <see cref="CelestialDragons"/> 头部每帧 <see cref="Publish"/> 驱动:
    ///   ● <b>CelestialDragonCloudSea</b>(专属) —— 云海层: 三层漂流云卷 + 破云 punch 涟漪 (俯冲穿透点云被冲开)。
    ///   ● <b>ElementalScreenTint</b>(共享) —— 常驻金芒底色 (随幕加浓, 过场脉冲)。
    ///   ● <b>ArenaRunic</b>(共享) —— 敕令幕天规法阵地纹。
    ///   ● 全屏白金闪 (<see cref="PublishFlash"/>) —— 天罚顿帧/升天爆发, 一场战斗只用于最重的节拍。
    /// 绘制位于 <see cref="PostDrawTiles"/>(实体之下) → 不遮挡需躲避的弹幕。纯本地视觉, 服务端零绘制,
    /// 受 MythologyConfig 降级; 云海为 decal overlay, 不占全屏后处理名额 (与 ArenaRunic 同类)。
    /// </summary>
    public class CelestialScreenSystem : ModSystem
    {
        private static float _tint;
        private static float _runic;
        private static float _runicRadius;
        private static Vector2 _arenaCenter;
        private static float _time;
        private static ulong _lastPublishFrame;

        // ===== V3: 云海 / 破云 punch / 全屏闪 =====
        private static float _cloud;      // 发布目标
        private static float _cloudCur;   // 平滑当前值
        private static float _flash;      // 白金闪脉冲 (每帧 ×0.86 衰减)

        private struct CloudPunch
        {
            public Vector2 World;
            public float Age;      // 0 新 → 1 消散
            public float Strength;
        }

        private static readonly CloudPunch[] _punches = new CloudPunch[3];
        private static int _punchNext;

        /// <summary>由金龙头部每帧调用, 发布当前氛围标量 (纯本地视觉)。</summary>
        public static void Publish(Vector2 arenaCenter, float tint, float runic, float runicRadius, float cloud, float time) {
            _arenaCenter = arenaCenter;
            _tint = tint;
            _runic = runic;
            _runicRadius = runicRadius;
            _cloud = cloud;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>破云涟漪: 在世界点冲开云层 (俯冲穿透/入场破云), 最多 3 个并存, 复用最旧槽位。</summary>
        public static void PublishCloudPunch(Vector2 worldPos, float strength = 1f) {
            if (Main.dedServ)
                return;
            _punches[_punchNext] = new CloudPunch { World = worldPos, Age = 0f, Strength = strength };
            _punchNext = (_punchNext + 1) % _punches.Length;
        }

        /// <summary>全屏白金闪 (0~1)。只用于天罚顿帧与升天爆发两类最重节拍。</summary>
        public static void PublishFlash(float strength) {
            if (Main.dedServ)
                return;
            _flash = MathHelper.Max(_flash, MathHelper.Clamp(strength, 0f, 1f));
        }

        public override void OnWorldUnload() {
            _tint = _runic = _cloud = _cloudCur = _flash = 0f;
            for (int i = 0; i < _punches.Length; i++)
                _punches[i].Age = 1f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
                _cloud = MathHelper.Lerp(_cloud, 0f, 0.06f);
            }

            _cloudCur = MathHelper.Lerp(_cloudCur, _cloud, 0.05f);
            for (int i = 0; i < _punches.Length; i++) {
                if (_punches[i].Age < 1f)
                    _punches[i].Age += 1f / 42f;
            }

            DrawCloudSea();
            DrawTint();
            DrawEdictRunic();
            DrawFlash();
        }

        // ===== CelestialDragonCloudSea: 云海层 (专属 decal) =====
        private static void DrawCloudSea() {
            if (_cloudCur <= 0.02f)
                return;
            Effect fx = CelestialDragonVFX.CloudSea;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            float zoom = Main.GameViewMatrix.Zoom.X;
            float halfH = Main.screenHeight * 0.5f;
            // 云海上缘: 场地中心下方 420px (世界), 换算成屏幕 UV (缩放感知)
            float worldY = _arenaCenter.Y + 420f;
            float screenY = (worldY - Main.screenPosition.Y - halfH) * zoom + halfH;
            float cloudLevel = MathHelper.Clamp(screenY / Main.screenHeight, 0.30f, 1.40f);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_cloudCur, 0f, 1f) * 0.8f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uCloudLevel"]?.SetValue(cloudLevel);
            // 世界锚定漂流: 屏幕滚动时云缓慢反向掠过 (微系数防平铺重复感)
            fx.Parameters["uScroll"]?.SetValue(Main.screenPosition * 0.00016f);
            for (int i = 0; i < _punches.Length; i++) {
                Vector2 uv = WorldToScreenUV(_punches[i].World);
                Vector4 packed = new(uv.X, uv.Y, MathHelper.Clamp(_punches[i].Age, 0f, 1f),
                    _punches[i].Age >= 1f ? 0f : _punches[i].Strength);
                fx.Parameters["uPunch" + i]?.SetValue(packed);
            }
            fx.Parameters["uColorLit"]?.SetValue(new Vector4(1f, 0.88f, 0.62f, 0.75f));
            fx.Parameters["uColorShadow"]?.SetValue(new Vector4(0.42f, 0.34f, 0.30f, 0.55f));

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
        }

        private static Vector2 WorldToScreenUV(Vector2 world) {
            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 screen = (world - Main.screenPosition - halfScreen) * zoom + halfScreen;
            return screen / new Vector2(Main.screenWidth, Main.screenHeight);
        }

        // ===== ElementalScreenTint: 金芒氛围底色 =====
        private static void DrawTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=暖金, 下=琥珀压暗; 覆盖度保守, 始终看得清弹幕
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3() * 0.45f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.4f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 敕令幕天规法阵地纹 =====
        private static void DrawEdictRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_arenaCenter, _runicRadius, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Gold.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(255, 150, 60).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.NonPremultiplied);
        }

        // ===== 全屏白金闪 (顿帧节拍) =====
        private static void DrawFlash() {
            if (_flash <= 0.02f) {
                _flash = 0f;
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color c = new Color(255, 248, 224) * _flash;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), c);
            sb.End();

            _flash *= 0.86f;
        }
    }
}

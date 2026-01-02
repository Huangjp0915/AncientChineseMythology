using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds
{
    /// <summary>
    /// 地府场景效果
    /// </summary>
    internal class UnderworldSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => UnderworldFogEffect.IsActive(player);
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(UnderworldFogSky.Name, isActive);
            }
        }
    }

    /// <summary>
    /// 地府迷雾天空效果
    /// </summary>
    internal class UnderworldFogSky : CustomSky, IACMLoader
    {
        internal static string Name => "ACM:UnderworldFog";
        private bool active;
        private float intensity;

        // 雾气效果参数
        private float fogPulseTimer = 0f;
        private float soulDriftTimer = 0f;

        // 迷雾层
        private readonly GhostlyFog[] fogs = new GhostlyFog[120];

        // 幽魂效果
        private readonly WanderingSoul[] souls = new WanderingSoul[20];

        // 地府特有颜色 - 阴暗的青灰色调
        private readonly Color[] underworldColors = new Color[]
        {
            new Color(40, 50, 60),      // 深青灰
            new Color(50, 60, 70),      // 灰蓝
            new Color(35, 45, 55),      // 暗青
            new Color(45, 55, 65),      // 阴灰
            new Color(30, 40, 50),      // 幽暗
            new Color(55, 65, 75),      // 冷灰
        };

        void IACMLoader.LoadData() {
            SkyManager.Instance[Name] = this;
            //创建魔法紫色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.2f, 0.1f, 0.3f)//紫色魔法调
                .UseOpacity(0.5f), EffectPriority.High);

            // 初始化迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i] = new GhostlyFog();
            }

            // 初始化幽魂
            for (int i = 0; i < souls.Length; i++) {
                souls[i] = new WanderingSoul();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            fogPulseTimer = 0f;
            soulDriftTimer = 0f;

            // 重置迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i].Reset();
            }

            // 重置幽魂
            for (int i = 0; i < souls.Length; i++) {
                souls[i].Reset();
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || Underworld.Fog == null) {
                return;
            }

            // 阴暗的背景色
            Color bgColor = new Color(20, 25, 30);
            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            // 使用MagicPixel作为纯色纹理
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(
                pixel,
                screenRect,
                new Rectangle(0, 0, 1, 1),
                bgColor * intensity * 0.6f
            );

            // 绘制幽冥迷雾层
            DrawGhostlyFogsSky(spriteBatch);

            // 绘制游荡幽魂
            DrawWanderingSoulsSky(spriteBatch);

            // 绘制雾气涟漪
            DrawFogRipplesSky(spriteBatch, pixel);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            // 检查玩家是否在地府区域
            bool shouldBeActive = false;
            foreach (Player player in Main.ActivePlayers) {
                if (UnderworldFogEffect.IsInUnderworldZone(player)) {
                    shouldBeActive = true;
                    break;
                }
            }

            // 强度变化
            if (shouldBeActive) {
                if (intensity < 1f) {
                    intensity += 0.01f;
                }
                if (!active) {
                    Activate(Vector2.Zero);
                }
            }
            else {
                intensity -= 0.008f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }

            // 更新雾气脉动
            fogPulseTimer += 0.015f;
            if (fogPulseTimer > MathHelper.TwoPi) {
                fogPulseTimer -= MathHelper.TwoPi;
            }

            // 更新幽魂漂移
            soulDriftTimer += 0.01f;
            if (soulDriftTimer > MathHelper.TwoPi * 2) {
                soulDriftTimer -= MathHelper.TwoPi * 2;
            }

            // 更新迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i].Update();
            }

            // 更新幽魂
            for (int i = 0; i < souls.Length; i++) {

                souls[i].Update();
            }
        }

        public override Color OnTileColor(Color inColor) {
            // 应用阴冷的青灰色调
            if (intensity > 0.1f) {
                float coldR = 0.85f;
                float coldG = 0.90f;
                float coldB = 0.95f;

                Color tintedColor = new Color(
                    (int)(inColor.R * coldR),
                    (int)(inColor.G * coldG),
                    (int)(inColor.B * coldB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }
            return inColor;
        }

        #region 绘制特效方法
        private void DrawGhostlyFogsSky(SpriteBatch sb) {
            if (Underworld.Fog == null) {
                return;
            }

            Texture2D fogTex = Underworld.Fog;

            for (int i = 0; i < fogs.Length; i++) {
                GhostlyFog fog = fogs[i];
                if (!fog.IsActive) {
                    continue;
                }

                Vector2 drawPos = fog.Position - Main.screenPosition;

                // 缓慢变换的阴冷色调
                int colorIndex = (int)(soulDriftTimer * 1.5f + i * 0.3f) % underworldColors.Length;
                Color fogColor = Color.Lerp(
                    underworldColors[colorIndex],
                    underworldColors[(colorIndex + 1) % underworldColors.Length],
                    (float)Math.Sin(soulDriftTimer + i * 0.5f) * 0.5f + 0.5f
                );

                float alpha = (float)Math.Sin(fog.AnimProgress * MathHelper.Pi) * intensity * 0.4f;

                // 主雾气层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha,
                    fog.Rotation,
                    fogTex.Size() * 0.5f,
                    fog.Scale,
                    SpriteEffects.None,
                    0f
                );

                // 幽暗光晕层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha * 0.3f,
                    fog.Rotation * 0.7f,
                    fogTex.Size() * 0.5f,
                    fog.Scale * 1.5f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawWanderingSoulsSky(SpriteBatch sb) {
            if (Underworld.Fog == null) {
                return;
            }

            Texture2D soulTex = Underworld.Fog;

            for (int i = 0; i < souls.Length; i++) {
                WanderingSoul soul = souls[i];
                if (!soul.IsActive) {
                    continue;
                }

                Vector2 drawPos = soul.Position - Main.screenPosition;

                // 幽魂的苍白色
                Color soulColor = new Color(200, 220, 230);
                float alpha = (float)Math.Sin(soul.AnimProgress * MathHelper.Pi) * intensity * 0.25f;

                // 主体
                sb.Draw(
                    soulTex,
                    drawPos,
                    null,
                    soulColor * alpha,
                    soul.Rotation,
                    soulTex.Size() * 0.5f,
                    soul.Scale * 0.6f,
                    SpriteEffects.None,
                    0f
                );

                // 幽光
                for (int j = 0; j < 3; j++) {
                    float offset = j * 0.3f;
                    sb.Draw(
                        soulTex,
                        drawPos,
                        null,
                        soulColor * alpha * 0.15f,
                        soul.Rotation + offset,
                        soulTex.Size() * 0.5f,
                        soul.Scale * (0.8f + j * 0.2f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawFogRipplesSky(SpriteBatch sb, Texture2D pixel) {
            // 绘制雾气涟漪效果
            int rippleCount = 2;
            for (int i = 0; i < rippleCount; i++) {
                float phase = (fogPulseTimer + i * MathHelper.Pi) % MathHelper.TwoPi;
                float rippleAlpha = (float)Math.Sin(phase) * 0.5f + 0.5f;

                int colorIndex = (int)(phase * 2f) % underworldColors.Length;
                Color rippleColor = underworldColors[colorIndex];

                // 垂直雾带
                int y = (int)(Main.screenHeight * (0.2f + i * 0.3f));
                sb.Draw(
                    pixel,
                    new Rectangle(0, y, Main.screenWidth, 3),
                    new Rectangle(0, 0, 1, 1),
                    rippleColor * (rippleAlpha * intensity * 0.1f)
                );
            }
        }
        #endregion

        #region 幽冥迷雾类
        private class GhostlyFog
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public GhostlyFog() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 100);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) {
                        Activate();
                    }
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Rotation += 0.001f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.002f, 0.006f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-300, Main.screenWidth + 300),
                    Main.screenPosition.Y + Main.rand.Next(-300, Main.screenHeight + 300)
                );

                Velocity = Main.rand.NextVector2Circular(0.3f, 0.3f);
                Scale = Main.rand.NextFloat(2f, 4f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 游荡幽魂类
        private class WanderingSoul
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public WanderingSoul() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(100, 300);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) {
                        Activate();
                    }
                    return;
                }

                AnimProgress += AnimSpeed;

                // 飘忽不定的移动
                float wave = (float)Math.Sin(AnimProgress * MathHelper.TwoPi * 3f);
                Velocity.X += wave * 0.02f;
                Velocity.Y += (float)Math.Cos(AnimProgress * MathHelper.TwoPi * 2f) * 0.02f;

                Position += Velocity;
                Rotation += 0.005f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.007f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(100, Main.screenWidth - 100),
                    Main.screenPosition.Y + Main.rand.Next(100, Main.screenHeight - 100)
                );

                Velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
                Scale = Main.rand.NextFloat(0.8f, 1.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    internal class UnderworldFogSystem : ModSystem
    {
        // 雾气效果参数
        private float fogPulseTimer = 0f;
        private float soulDriftTimer = 0f;
        private float intensity = 0f;

        // 迷雾层
        private readonly GhostlyFog[] fogs = new GhostlyFog[80];

        // 幽魂效果
        private readonly WanderingSoul[] souls = new WanderingSoul[15];

        // 地府特有颜色 - 阴暗的青灰色调
        private readonly Color[] underworldColors = new Color[]
        {
            new Color(40, 50, 60),      // 深青灰
            new Color(50, 60, 70),      // 灰蓝
            new Color(35, 45, 55),      // 暗青
            new Color(45, 55, 65),      // 阴灰
            new Color(30, 40, 50),      // 幽暗
            new Color(55, 65, 75),      // 冷灰
        };

        public override void OnModLoad() {
            // 初始化迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i] = new GhostlyFog();
            }

            // 初始化幽魂
            for (int i = 0; i < souls.Length; i++) {
                souls[i] = new WanderingSoul();
            }
        }

        public override void PostUpdateEverything() {
            // 检查玩家是否在地府区域
            bool shouldBeActive = false;
            foreach (Player player in Main.ActivePlayers) {
                if (UnderworldFogEffect.IsInUnderworldZone(player)) {
                    shouldBeActive = true;
                    break;
                }
            }

            // 强度变化
            if (shouldBeActive) {
                if (intensity < 1f) {
                    intensity += 0.01f;
                }
            }
            else {
                intensity -= 0.008f;
                if (intensity < 0f) {
                    intensity = 0f;
                }
            }

            if (intensity <= 0.01f)
                return;

            // 更新雾气脉动
            fogPulseTimer += 0.015f;
            if (fogPulseTimer > MathHelper.TwoPi) {
                fogPulseTimer -= MathHelper.TwoPi;
            }

            // 更新幽魂漂移
            soulDriftTimer += 0.01f;
            if (soulDriftTimer > MathHelper.TwoPi * 2) {
                soulDriftTimer -= MathHelper.TwoPi * 2;
            }

            // 更新迷雾
            for (int i = 0; i < fogs.Length; i++) {
                fogs[i].Update();
            }

            // 更新幽魂
            for (int i = 0; i < souls.Length; i++) {
                souls[i].Update();
            }
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu || intensity <= 0.01f || Underworld.Fog == null)
                return;

            // 检查当前玩家是否在地府区域
            if (!UnderworldFogEffect.IsInUnderworldZone(Main.LocalPlayer))
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 绘制阴暗背景层
            DrawDarkOverlay(spriteBatch);

            // 绘制幽冥迷雾层
            DrawGhostlyFogs(spriteBatch);

            // 绘制游荡幽魂
            DrawWanderingSouls(spriteBatch);

            spriteBatch.End();
        }

        #region 绘制方法

        private void DrawDarkOverlay(SpriteBatch sb) {
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Color bgColor = new Color(15, 20, 25);
            Rectangle screenRect = new Rectangle(
                (int)Main.screenPosition.X,
                (int)Main.screenPosition.Y,
                Main.screenWidth,
                Main.screenHeight
            );

            sb.Draw(
                pixel,
                screenRect,
                new Rectangle(0, 0, 1, 1),
                bgColor * intensity * 0.3f
            );
        }

        private void DrawGhostlyFogs(SpriteBatch sb) {
            if (Underworld.Fog == null)
                return;

            Texture2D fogTex = Underworld.Fog;

            for (int i = 0; i < fogs.Length; i++) {
                GhostlyFog fog = fogs[i];
                if (!fog.IsActive)
                    continue;

                Vector2 drawPos = fog.Position - Main.screenPosition;

                // 只绘制在屏幕范围内的雾气
                if (drawPos.X < -200 || drawPos.X > Main.screenWidth + 200 ||
                    drawPos.Y < -200 || drawPos.Y > Main.screenHeight + 200)
                    continue;

                // 缓慢变换的阴冷色调
                int colorIndex = (int)(soulDriftTimer * 1.5f + i * 0.3f) % underworldColors.Length;
                Color fogColor = Color.Lerp(
                    underworldColors[colorIndex],
                    underworldColors[(colorIndex + 1) % underworldColors.Length],
                    (float)Math.Sin(soulDriftTimer + i * 0.5f) * 0.5f + 0.5f
                );

                float alpha = (float)Math.Sin(fog.AnimProgress * MathHelper.Pi) * intensity * 0.5f;

                // 主雾气层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha,
                    fog.Rotation,
                    fogTex.Size() * 0.5f,
                    fog.Scale * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );

                // 幽暗光晕层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha * 0.3f,
                    fog.Rotation * 0.7f,
                    fogTex.Size() * 0.5f,
                    fog.Scale * 1.5f * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawWanderingSouls(SpriteBatch sb) {
            if (Underworld.Fog == null)
                return;

            Texture2D soulTex = Underworld.Fog;

            for (int i = 0; i < souls.Length; i++) {
                WanderingSoul soul = souls[i];
                if (!soul.IsActive)
                    continue;

                Vector2 drawPos = soul.Position - Main.screenPosition;

                // 只绘制在屏幕范围内的幽魂
                if (drawPos.X < -200 || drawPos.X > Main.screenWidth + 200 ||
                    drawPos.Y < -200 || drawPos.Y > Main.screenHeight + 200)
                    continue;

                // 幽魂的苍白色
                Color soulColor = new Color(200, 220, 230);
                float alpha = (float)Math.Sin(soul.AnimProgress * MathHelper.Pi) * intensity * 0.35f;

                // 主体
                sb.Draw(
                    soulTex,
                    drawPos,
                    null,
                    soulColor * alpha,
                    soul.Rotation,
                    soulTex.Size() * 0.5f,
                    soul.Scale * 0.6f * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );

                // 幽光
                for (int j = 0; j < 3; j++) {
                    float offset = j * 0.3f;
                    sb.Draw(
                        soulTex,
                        drawPos,
                        null,
                        soulColor * alpha * 0.2f,
                        soul.Rotation + offset,
                        soulTex.Size() * 0.5f,
                        soul.Scale * (0.8f + j * 0.2f) * Main.GameViewMatrix.Zoom.X,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        #endregion

        #region 幽冥迷雾类
        private class GhostlyFog
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public GhostlyFog() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 100);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) {
                        Activate();
                    }
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Rotation += 0.001f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.002f, 0.006f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-300, Main.screenWidth + 300),
                    Main.screenPosition.Y + Main.rand.Next(-300, Main.screenHeight + 300)
                );

                Velocity = Main.rand.NextVector2Circular(0.3f, 0.3f);
                Scale = Main.rand.NextFloat(2f, 4f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 游荡幽魂类
        private class WanderingSoul
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;

            private int cooldown;

            public WanderingSoul() {
                Reset();
            }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(100, 300);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) {
                        Activate();
                    }
                    return;
                }

                AnimProgress += AnimSpeed;

                // 飘忽不定的移动
                float wave = (float)Math.Sin(AnimProgress * MathHelper.TwoPi * 3f);
                Velocity.X += wave * 0.02f;
                Velocity.Y += (float)Math.Cos(AnimProgress * MathHelper.TwoPi * 2f) * 0.02f;

                Position += Velocity;
                Rotation += 0.005f;

                if (AnimProgress >= 1f) {
                    Reset();
                }
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.007f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(100, Main.screenWidth - 100),
                    Main.screenPosition.Y + Main.rand.Next(100, Main.screenHeight - 100)
                );

                Velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
                Scale = Main.rand.NextFloat(0.8f, 1.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    /// <summary>
    /// 地府雾气效果管理器
    /// </summary>
    public static class UnderworldFogEffect
    {
        /// <summary>
        /// 检查玩家是否在地府区域
        /// </summary>
        public static bool IsInUnderworldZone(Player player) {
            if (UnderworldPlayer.UnderworldEffect) {
                return true;
            }
            // 检查是否在地狱层或岩石层（地府范围）
            int tileX = (int)(player.Center.X / 16f);
            int tileY = (int)(player.Center.Y / 16f);

            // 检查是否在地图右半边
            bool isRightSide = tileX > Main.maxTilesX / 2;

            // 检查是否在地府高度范围（岩石层到地图底部）
            bool isInDepth = tileY > Main.rockLayer && tileY < Main.maxTilesY;

            // 检查周围是否有幽冥石或灵魂沙
            bool hasUnderworldTiles = false;
            int checkRadius = 20;

            for (int i = -checkRadius; i <= checkRadius; i += 5) {
                for (int j = -checkRadius; j <= checkRadius; j += 5) {
                    int checkX = tileX + i;
                    int checkY = tileY + j;

                    if (checkX >= 0 && checkX < Main.maxTilesX &&
                        checkY >= 0 && checkY < Main.maxTilesY) {
                        Tile tile = Main.tile[checkX, checkY];
                        if (tile.HasTile &&
                            (tile.TileType == ModContent.TileType<Tiles.UmbralStone>() ||
                             tile.TileType == ModContent.TileType<Tiles.NetherSand>())) {
                            hasUnderworldTiles = true;
                            break;
                        }
                    }
                }
                if (hasUnderworldTiles) break;
            }

            return isRightSide && isInDepth && hasUnderworldTiles;
        }

        /// <summary>
        /// 检查效果是否应该激活
        /// </summary>
        public static bool IsActive(Player player) {
            if (Main.gameMenu)
                return false;

            return IsInUnderworldZone(player);
        }
    }
}

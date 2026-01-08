using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱区域场景效果
    /// </summary>
    internal class HeavenPillarSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Heaven");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => HeavenlyEffectManager.IsActive(player);
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(HeavenlySky.Name, isActive);
            }
        }
    }

    /// <summary>
    /// 天庭仙气天空效果
    /// </summary>
    internal class HeavenlySky : CustomSky, IACMLoader
    {
        internal static string Name => "ACM:HeavenlyPillar";
        private bool active;
        private float intensity;

        // 仙气效果参数
        private float divinePulseTimer = 0f;
        private float cloudDriftTimer = 0f;
        private float lightRayTimer = 0f;

        // 祥云层
        private readonly AuspiciousCloud[] clouds = new AuspiciousCloud[80];

        // 神光粒子
        private readonly DivineLightParticle[] divineParticles = new DivineLightParticle[40];

        // 飘落的金色花瓣
        private readonly GoldenPetal[] petals = new GoldenPetal[30];

        // 天庭色彩 - 神圣的金白翠绿色调
        private readonly Color[] heavenlyColors =
        [
            new Color(255, 250, 220),   // 神圣金白
            new Color(255, 245, 200),   // 暖金
            new Color(220, 255, 230),   // 翠玉绿
            new Color(240, 255, 250),   // 仙白
            new Color(255, 240, 180),   // 金黄
            new Color(200, 240, 255),   // 天青
        ];

        void IACMLoader.LoadData() {
            SkyManager.Instance[Name] = this;
            // 创建神圣金色滤镜
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.95f, 0.9f, 0.7f) // 神圣金色调
                .UseOpacity(0.3f), EffectPriority.High);

            // 初始化祥云
            for (int i = 0; i < clouds.Length; i++) {
                clouds[i] = new AuspiciousCloud();
            }

            // 初始化神光粒子
            for (int i = 0; i < divineParticles.Length; i++) {
                divineParticles[i] = new DivineLightParticle();
            }

            // 初始化花瓣
            for (int i = 0; i < petals.Length; i++) {
                petals[i] = new GoldenPetal();
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            divinePulseTimer = 0f;
            cloudDriftTimer = 0f;
            lightRayTimer = 0f;

            for (int i = 0; i < clouds.Length; i++) clouds[i].Reset();
            for (int i = 0; i < divineParticles.Length; i++) divineParticles[i].Reset();
            for (int i = 0; i < petals.Length; i++) petals[i].Reset();
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f) return;

            // 神圣的背景光晕
            Color bgColor = new Color(255, 252, 240);
            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(pixel, screenRect, new Rectangle(0, 0, 1, 1), bgColor * intensity * 0.15f);

            // 绘制祥云层
            DrawAuspiciousCloudsSky(spriteBatch);

            // 绘制神光粒子
            DrawDivineLightSky(spriteBatch);

            // 绘制金色花瓣
            DrawGoldenPetalsSky(spriteBatch);

            // 绘制光柱效果
            DrawLightPillarsSky(spriteBatch, pixel);
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            bool shouldBeActive = false;
            foreach (Player player in Main.ActivePlayers) {
                if (HeavenlyEffectManager.IsInPillarZone(player)) {
                    shouldBeActive = true;
                    break;
                }
            }

            if (shouldBeActive) {
                if (intensity < 1f) intensity += 0.015f;
                if (!active) Activate(Vector2.Zero);
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0) Deactivate();
            }

            divinePulseTimer += 0.02f;
            cloudDriftTimer += 0.008f;
            lightRayTimer += 0.015f;

            for (int i = 0; i < clouds.Length; i++) clouds[i].Update();
            for (int i = 0; i < divineParticles.Length; i++) divineParticles[i].Update();
            for (int i = 0; i < petals.Length; i++) petals[i].Update();
        }

        public override Color OnTileColor(Color inColor) {
            if (intensity > 0.1f) {
                // 应用神圣的暖金色调
                float warmR = 1.05f;
                float warmG = 1.02f;
                float warmB = 0.95f;

                Color tintedColor = new Color(
                    (int)Math.Min(255, inColor.R * warmR),
                    (int)Math.Min(255, inColor.G * warmG),
                    (int)(inColor.B * warmB),
                    inColor.A
                );

                return Color.Lerp(inColor, tintedColor, intensity * 0.4f);
            }
            return inColor;
        }

        #region 绘制方法
        private void DrawAuspiciousCloudsSky(SpriteBatch sb) {
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex == null) return;

            int frameSize = smokeTex.Width / 4;

            for (int i = 0; i < clouds.Length; i++) {
                AuspiciousCloud cloud = clouds[i];
                if (!cloud.IsActive) continue;

                Vector2 drawPos = cloud.Position - Main.screenPosition;

                int colorIndex = (int)(cloudDriftTimer * 1.5f + i * 0.3f) % heavenlyColors.Length;
                Color cloudColor = Color.Lerp(
                    heavenlyColors[colorIndex],
                    heavenlyColors[(colorIndex + 1) % heavenlyColors.Length],
                    (float)Math.Sin(cloudDriftTimer + i * 0.5f) * 0.5f + 0.5f
                );

                float alpha = (float)Math.Sin(cloud.AnimProgress * MathHelper.Pi) * intensity * 0.5f;
                cloudColor *= alpha;
                cloudColor.A = 0;

                int frameX = i % 4;
                int frameY = (i / 4) % 4;
                Rectangle smokeRect = new Rectangle(frameX * frameSize, frameY * frameSize, frameSize, frameSize);

                sb.Draw(smokeTex, drawPos, smokeRect, cloudColor, cloud.Rotation, new Vector2(frameSize / 2), cloud.Scale, SpriteEffects.None, 0f);

                // 光晕层
                Color glowColor = cloudColor;
                glowColor.A = 0;
                sb.Draw(smokeTex, drawPos, smokeRect, glowColor * 0.3f, cloud.Rotation * 0.8f, new Vector2(frameSize / 2), cloud.Scale * 1.4f, SpriteEffects.None, 0f);
            }
        }

        private void DrawDivineLightSky(SpriteBatch sb) {
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex == null) return;

            for (int i = 0; i < divineParticles.Length; i++) {
                DivineLightParticle particle = divineParticles[i];
                if (!particle.IsActive) continue;

                Vector2 drawPos = particle.Position - Main.screenPosition;

                Color particleColor = heavenlyColors[i % heavenlyColors.Length];
                float alpha = (float)Math.Sin(particle.AnimProgress * MathHelper.Pi) * intensity * 0.6f;
                particleColor *= alpha;
                particleColor.A = 0;

                sb.Draw(glowTex, drawPos, null, particleColor, MathHelper.PiOver2, glowTex.Size() / 2, particle.Scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawGoldenPetalsSky(SpriteBatch sb) {
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex == null) return;

            for (int i = 0; i < petals.Length; i++) {
                GoldenPetal petal = petals[i];
                if (!petal.IsActive) continue;

                Vector2 drawPos = petal.Position - Main.screenPosition;

                Color petalColor = new Color(255, 240, 180, 0);
                float alpha = (float)Math.Sin(petal.AnimProgress * MathHelper.Pi) * intensity * 0.7f;
                petalColor *= alpha;

                sb.Draw(starTex, drawPos, null, petalColor, petal.Rotation, starTex.Size() / 2, petal.Scale * 0.3f, SpriteEffects.None, 0f);
            }
        }

        private void DrawLightPillarsSky(SpriteBatch sb, Texture2D pixel) {
            // 绘制从天降下的神光柱
            int pillarCount = 4;
            for (int i = 0; i < pillarCount; i++) {
                float phase = (lightRayTimer + i * MathHelper.PiOver2) % MathHelper.TwoPi;
                float pillarAlpha = (float)Math.Sin(phase) * 0.5f + 0.5f;

                int x = (int)(Main.screenWidth * (0.15f + i * 0.25f));
                Color pillarColor = heavenlyColors[i % heavenlyColors.Length];
                pillarColor *= pillarAlpha * intensity * 0.08f;
                pillarColor.A = 0;

                // 渐变光柱
                for (int j = 0; j < 20; j++) {
                    float yFactor = j / 20f;
                    int width = (int)(30 + (1f - yFactor) * 50);
                    Color gradientColor = pillarColor * (1f - yFactor * 0.7f);

                    sb.Draw(pixel, new Rectangle(x - width / 2, (int)(Main.screenHeight * yFactor * 0.6f), width, 30), new Rectangle(0, 0, 1, 1), gradientColor);
                }
            }
        }
        #endregion

        #region 祥云类
        private class AuspiciousCloud
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public AuspiciousCloud() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(10, 60);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Rotation += 0.0005f;

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.001f, 0.004f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-200, Main.screenHeight + 200)
                );

                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.3f, 0.3f));
                Scale = Main.rand.NextFloat(2.5f, 5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion

        #region 神光粒子类
        private class DivineLightParticle
        {
            public Vector2 Position;
            public float Scale;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public DivineLightParticle() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(30, 150);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.Y -= 0.01f; // 缓慢上升

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.005f, 0.012f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(50, Main.screenWidth - 50),
                    Main.screenPosition.Y + Main.rand.Next(Main.screenHeight / 2, Main.screenHeight)
                );

                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1f, -0.3f));
                Scale = Main.rand.NextFloat(1f, 2.5f);
            }
        }
        #endregion

        #region 金色花瓣类
        private class GoldenPetal
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public GoldenPetal() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(50, 200);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;

                // 飘落的摇摆
                float wave = (float)Math.Sin(AnimProgress * MathHelper.TwoPi * 4f);
                Velocity.X += wave * 0.03f;

                Position += Velocity;
                Rotation += 0.02f;

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.008f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y - 50
                );

                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 1.5f));
                Scale = Main.rand.NextFloat(0.8f, 1.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
        #endregion
    }

    /// <summary>
    /// 天庭视觉效果处理器 - 玩家层面的效果
    /// </summary>
    public class HeavenlyEffect : ModPlayer
    {
        #region 效果状态
        public float PillarInfluence { get; private set; }
        private float effectFade;
        private float divineGlowTimer;
        private int cloudParticleTimer;
        private float lightRayTimer;
        private int ambientSoundCooldown;
        public bool IsInPillarZone { get; private set; }
        private int nearestPillarIndex = -1;
        #endregion

        public override void ResetEffects() {
            PillarInfluence = 0f;
            IsInPillarZone = false;
            nearestPillarIndex = -1;
        }

        public override void PostUpdate() {
            if (!HeavenPillarSystem.PillarsDescended) {
                effectFade = MathHelper.Lerp(effectFade, 0f, 0.05f);
                return;
            }

            PillarInfluence = HeavenPillarSystem.GetPillarInfluence(Player.Center);
            IsInPillarZone = PillarInfluence > 0f;
            nearestPillarIndex = HeavenPillarSystem.GetNearestPillarIndex(Player.Center);

            float targetFade = IsInPillarZone ? PillarInfluence : 0f;
            effectFade = MathHelper.Lerp(effectFade, targetFade, 0.06f);

            divineGlowTimer += 0.02f * (1f + effectFade);
            lightRayTimer += 0.015f;
            cloudParticleTimer++;

            if (ambientSoundCooldown > 0) ambientSoundCooldown--;

            if (effectFade > 0.05f) {
                ApplyHeavenlyEffects();
            }
        }

        private void ApplyHeavenlyEffects() {
            if (Main.dedServ) return;

            SpawnDivineParticles();
            SpawnCloudEffects();
            SpawnLightRayEffects();
            SpawnJadeParticles();
            ApplyDivineLighting();
            PlayAmbientSounds();
        }

        private void SpawnDivineParticles() {
            if (cloudParticleTimer % 2 != 0) return;

            int particleCount = (int)(5 * effectFade);
            for (int i = 0; i < particleCount; i++) {
                Vector2 spawnPos = Player.Center + Main.rand.NextVector2Circular(500f, 350f) * effectFade;

                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.WhiteTorch;
                int dust = Dust.NewDust(spawnPos, 0, 0, dustType, 0, -2.5f, 150, default, Main.rand.NextFloat(1.2f, 2.2f) * effectFade);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-4f, -1.5f));
                Main.dust[dust].fadeIn = 1.5f;
            }
        }

        private void SpawnJadeParticles() {
            if (cloudParticleTimer % 6 != 0) return;
            if (effectFade < 0.4f) return;

            int jadeCount = (int)(3 * effectFade);
            for (int i = 0; i < jadeCount; i++) {
                Vector2 spawnPos = Player.Center + Main.rand.NextVector2Circular(400f, 300f);

                int jadeDust = Dust.NewDust(spawnPos, 0, 0, DustID.JungleGrass, 0, -2f, 100, default, Main.rand.NextFloat(1.5f, 2.5f) * effectFade);
                Main.dust[jadeDust].noGravity = true;
                Main.dust[jadeDust].velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1f));
                Main.dust[jadeDust].fadeIn = 1.3f;
            }
        }

        private void SpawnCloudEffects() {
            if (cloudParticleTimer % 3 != 0) return;
            if (effectFade < 0.25f) return;

            int cloudCount = (int)(4 * effectFade);
            for (int i = 0; i < cloudCount; i++) {
                Vector2 cloudPos = Player.Center + new Vector2(
                    Main.rand.NextFloat(-600f, 600f) * effectFade,
                    Main.rand.NextFloat(-150f, 300f)
                );

                int cloud = Dust.NewDust(cloudPos, 0, 0, DustID.Cloud,
                    Main.rand.NextFloat(-0.8f, 0.8f),
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    200,
                    default,
                    Main.rand.NextFloat(3f, 6f) * effectFade);
                Main.dust[cloud].noGravity = true;
                Main.dust[cloud].velocity *= 0.4f;
            }
        }

        private void SpawnLightRayEffects() {
            if (effectFade < 0.4f) return;
            if (cloudParticleTimer % 5 != 0) return;

            float rayAngle = MathF.Sin(lightRayTimer + Main.rand.NextFloat(-0.3f, 0.3f)) * 0.4f;
            Vector2 rayStart = Player.Center + new Vector2(Main.rand.NextFloat(-350f, 350f), -600f);
            Vector2 rayDir = new Vector2(MathF.Sin(rayAngle), MathF.Cos(rayAngle));

            for (int i = 0; i < 12; i++) {
                Vector2 dustPos = rayStart + rayDir * (i * 60);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, rayDir.X * 3, rayDir.Y * 3, 100, default, Main.rand.NextFloat(1.5f, 2.8f) * effectFade);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 2f;
            }
        }

        private void ApplyDivineLighting() {
            if (effectFade < 0.1f) return;

            float pulseIntensity = 0.6f + MathF.Sin(divineGlowTimer) * 0.2f;
            Vector3 divineLight = new Vector3(1f, 0.95f, 0.85f) * effectFade * pulseIntensity * 0.5f;
            Vector3 jadeLight = new Vector3(0.7f, 1f, 0.85f) * effectFade * pulseIntensity * 0.3f;

            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6 + divineGlowTimer * 0.4f;
                Vector2 lightPos = Player.Center + angle.ToRotationVector2() * 150f;
                Lighting.AddLight(lightPos, divineLight);
            }

            Lighting.AddLight(Player.Center + new Vector2(0, -80), divineLight * 1.5f);

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4 + divineGlowTimer * 0.6f + MathHelper.PiOver4;
                Vector2 lightPos = Player.Center + angle.ToRotationVector2() * 100f;
                Lighting.AddLight(lightPos, jadeLight);
            }
        }

        private void PlayAmbientSounds() {
            if (ambientSoundCooldown > 0) return;
            if (effectFade < 0.5f) return;

            if (Main.rand.NextBool(120)) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.4f * effectFade,
                    Pitch = 0.6f + Main.rand.NextFloat(-0.1f, 0.2f),
                    MaxInstances = 2
                }, Player.Center);
                ambientSoundCooldown = 100;
            }
        }
    }

    /// <summary>
    /// 天柱区域世界层面的绘制系统
    /// </summary>
    public class HeavenlyWorldDrawSystem : ModSystem
    {
        private float intensity = 0f;
        private float divinePulseTimer = 0f;
        private float cloudDriftTimer = 0f;

        private readonly HeavenCloud[] worldClouds = new HeavenCloud[60];
        private readonly HeavenLightOrb[] lightOrbs = new HeavenLightOrb[25];

        public override void OnModLoad() {
            for (int i = 0; i < worldClouds.Length; i++) worldClouds[i] = new HeavenCloud();
            for (int i = 0; i < lightOrbs.Length; i++) lightOrbs[i] = new HeavenLightOrb();
        }

        public override void PostUpdateWorld() {
            bool shouldBeActive = false;
            foreach (Player player in Main.ActivePlayers) {
                if (HeavenlyEffectManager.IsInPillarZone(player)) {
                    shouldBeActive = true;
                    break;
                }
            }

            if (shouldBeActive) {
                if (intensity < 1f) intensity += 0.012f;
            }
            else {
                intensity -= 0.008f;
                if (intensity < 0f) intensity = 0f;
            }

            if (intensity <= 0.01f) return;

            divinePulseTimer += 0.018f;
            cloudDriftTimer += 0.01f;

            for (int i = 0; i < worldClouds.Length; i++) worldClouds[i].Update();
            for (int i = 0; i < lightOrbs.Length; i++) lightOrbs[i].Update();
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu || intensity <= 0.01f) return;
            if (!HeavenlyEffectManager.IsInPillarZone(Main.LocalPlayer)) return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //DrawDivineOverlay(spriteBatch);
            //DrawWorldClouds(spriteBatch);
            //DrawLightOrbs(spriteBatch);

            spriteBatch.End();
        }

        private void DrawDivineOverlay(SpriteBatch sb) {
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Color bgColor = new Color(255, 252, 235);
            Rectangle screenRect = new Rectangle(
                (int)Main.screenPosition.X,
                (int)Main.screenPosition.Y,
                Main.screenWidth,
                Main.screenHeight
            );

            sb.Draw(pixel, screenRect, new Rectangle(0, 0, 1, 1), bgColor * intensity * 0.12f);
        }

        private void DrawWorldClouds(SpriteBatch sb) {
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex == null) return;

            int frameSize = smokeTex.Width / 4;

            for (int i = 0; i < worldClouds.Length; i++) {
                HeavenCloud cloud = worldClouds[i];
                if (!cloud.IsActive) continue;

                Vector2 drawPos = cloud.Position - Main.screenPosition;

                if (drawPos.X < -300 || drawPos.X > Main.screenWidth + 300 ||
                    drawPos.Y < -300 || drawPos.Y > Main.screenHeight + 300)
                    continue;

                Color cloudColor = Color.Lerp(new Color(255, 250, 220), new Color(220, 255, 235), 
                    (float)Math.Sin(cloudDriftTimer + i * 0.5f) * 0.5f + 0.5f);
                float alpha = (float)Math.Sin(cloud.AnimProgress * MathHelper.Pi) * intensity * 0.55f;
                cloudColor *= alpha;
                cloudColor.A = (byte)(alpha * 100);

                int frameX = i % 4;
                int frameY = (i / 4) % 4;
                Rectangle smokeRect = new Rectangle(frameX * frameSize, frameY * frameSize, frameSize, frameSize);

                sb.Draw(smokeTex, drawPos, smokeRect, cloudColor, cloud.Rotation, new Vector2(frameSize / 2), 
                    cloud.Scale * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);

                Color glowColor = cloudColor;
                glowColor.A = 0;
                sb.Draw(smokeTex, drawPos, smokeRect, glowColor * 0.4f, cloud.Rotation * 0.8f, 
                    new Vector2(frameSize / 2), cloud.Scale * 1.5f * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);
            }
        }

        private void DrawLightOrbs(SpriteBatch sb) {
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex == null) return;

            for (int i = 0; i < lightOrbs.Length; i++) {
                HeavenLightOrb orb = lightOrbs[i];
                if (!orb.IsActive) continue;

                Vector2 drawPos = orb.Position - Main.screenPosition;

                if (drawPos.X < -100 || drawPos.X > Main.screenWidth + 100 ||
                    drawPos.Y < -100 || drawPos.Y > Main.screenHeight + 100)
                    continue;

                Color orbColor = Color.Lerp(new Color(255, 245, 180), new Color(180, 255, 220), 
                    (float)Math.Sin(divinePulseTimer + i) * 0.5f + 0.5f);
                float alpha = (float)Math.Sin(orb.AnimProgress * MathHelper.Pi) * intensity * 0.7f;
                orbColor *= alpha;
                orbColor.A = 0;

                sb.Draw(glowTex, drawPos, null, orbColor, 0f, glowTex.Size() / 2, 
                    orb.Scale * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);
            }
        }

        private class HeavenCloud
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public HeavenCloud() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(15, 80);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Rotation += 0.0008f;

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0015f, 0.005f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-300, Main.screenHeight + 300)
                );

                Velocity = Main.rand.NextVector2Circular(0.4f, 0.4f);
                Scale = Main.rand.NextFloat(2.5f, 5.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class HeavenLightOrb
        {
            public Vector2 Position;
            public float Scale;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public HeavenLightOrb() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(40, 180);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.Y -= 0.015f;

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.004f, 0.01f);

                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(80, Main.screenWidth - 80),
                    Main.screenPosition.Y + Main.rand.Next(Main.screenHeight / 2, Main.screenHeight + 100)
                );

                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.2f, -0.4f));
                Scale = Main.rand.NextFloat(1.2f, 3f);
            }
        }
    }

    /// <summary>
    /// 天庭效果管理器
    /// </summary>
    public static class HeavenlyEffectManager
    {
        public static bool IsInPillarZone(Player player) {
            if (!HeavenPillarSystem.PillarsDescended) return false;
            return HeavenPillarSystem.IsInPillarRange(player.Center);
        }

        public static bool IsActive(Player player) {
            if (Main.gameMenu) return false;
            return IsInPillarZone(player);
        }
    }

    /// <summary>
    /// 天柱区域的全局背景效果
    /// </summary>
    public class HeavenlyBackgroundEffect : ModSystem
    {
        private static float skyOverlayAlpha = 0f;

        public override void PostUpdateWorld() {
            if (!HeavenPillarSystem.PillarsDescended) {
                skyOverlayAlpha = MathHelper.Lerp(skyOverlayAlpha, 0f, 0.02f);
                return;
            }

            float maxInfluence = 0f;
            foreach (Player player in Main.ActivePlayers) {
                float influence = HeavenPillarSystem.GetPillarInfluence(player.Center);
                if (influence > maxInfluence) {
                    maxInfluence = influence;
                }
            }

            float targetAlpha = maxInfluence * 0.7f;
            skyOverlayAlpha = MathHelper.Lerp(skyOverlayAlpha, targetAlpha, 0.03f);
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (skyOverlayAlpha < 0.05f) return;

            Color divineColor = new Color(255, 252, 235);
            backgroundColor = Color.Lerp(backgroundColor, divineColor, skyOverlayAlpha * 0.35f);

            tileColor = Color.Lerp(tileColor, Color.White, skyOverlayAlpha * 0.2f);
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (skyOverlayAlpha > 0.1f) {
                scale += skyOverlayAlpha * 0.15f;
            }
        }
    }
}

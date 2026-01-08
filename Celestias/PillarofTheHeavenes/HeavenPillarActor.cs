using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱Actor - 神圣的天庭之柱实体
    /// 四根天柱代表四方神圣，支撑天穹
    /// 纹理为横向四帧排列，每帧代表一根柱子样式
    /// </summary>
    public class HeavenPillarActor : Actor
    {
        #region 常量
        /// <summary>天柱总数</summary>
        public const int PillarCount = 4;
        /// <summary>天柱影响范围（玩家靠近此距离时触发效果）- 扩大到2400像素</summary>
        public const float EffectRadius = 2400f;
        /// <summary>天柱核心光效范围</summary>
        public const float CoreLightRadius = 1200f;
        /// <summary>天柱缩放倍数</summary>
        public const float PillarScale = 3f;
        #endregion

        #region 状态属性
        /// <summary>柱子样式索引（0-3，对应四帧纹理）</summary>
        [SyncVar]
        public int PillarStyleIndex;

        /// <summary>天柱是否已完全降临</summary>
        [SyncVar]
        public bool HasDescended;

        /// <summary>降临动画进度（0-1）</summary>
        public float DescendProgress;

        /// <summary>光效脉冲计时器</summary>
        private float glowPulseTimer;

        /// <summary>浮动动画计时器</summary>
        private float floatTimer;

        /// <summary>粒子生成计时器</summary>
        private int particleTimer;

        /// <summary>神圣光环旋转角度</summary>
        private float haloRotation;

        /// <summary>当前光效强度</summary>
        private float currentGlowIntensity;

        /// <summary>目标光效强度</summary>
        private float targetGlowIntensity;

        /// <summary>仙气光柱计时器</summary>
        private float divinePillarTimer;

        /// <summary>祥云漂浮计时器</summary>
        private float cloudDriftTimer;

        /// <summary>神圣光环扩散计时器</summary>
        private float haloExpandTimer;
        #endregion

        #region 纹理缓存
        private static Texture2D pillarTexture;
        private static int frameWidth;
        private static int frameHeight;
        #endregion

        public override void OnSpawn(params object[] args) {
            // 初始化天柱属性 - 放大三倍
            Width = frameWidth > 0 ? (int)(frameWidth * PillarScale) : 384;
            Height = frameHeight > 0 ? (int)(frameHeight * PillarScale) : 1536;
            Scale = PillarScale;
            DrawLayer = ActorDrawLayer.BeforePlayers;
            DrawExtendMode = 1800; // 大幅扩大绘制范围以容纳巨型天柱

            // 初始化动画状态
            DescendProgress = 0f;
            HasDescended = false;
            glowPulseTimer = 0f;
            floatTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            particleTimer = 0;
            haloRotation = 0f;
            currentGlowIntensity = 0f;
            targetGlowIntensity = 1f;
            divinePillarTimer = 0f;
            cloudDriftTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            haloExpandTimer = 0f;

            // 如果传入了样式索引参数
            if (args != null && args.Length > 0 && args[0] is int styleIndex) {
                PillarStyleIndex = (int)MathHelper.Clamp(styleIndex, 0, PillarCount - 1);
            }
        }

        public override void AI() {
            // 更新降临动画
            if (!HasDescended) {
                DescendProgress = MathHelper.Clamp(DescendProgress + 0.006f, 0f, 1f);
                if (DescendProgress >= 1f) {
                    HasDescended = true;
                    NetUpdate = true;
                }
            }

            // 更新动画计时器
            glowPulseTimer += 0.025f;
            floatTimer += 0.015f;
            haloRotation += 0.003f;
            particleTimer++;
            divinePillarTimer += 0.02f;
            cloudDriftTimer += 0.008f;
            haloExpandTimer += 0.018f;

            // 更新光效强度
            currentGlowIntensity = MathHelper.Lerp(currentGlowIntensity, targetGlowIntensity, 0.04f);

            // 检测附近玩家并触发效果
            CheckNearbyPlayers();

            // 生成粒子效果
            if (!VaultUtils.isServer) {
                SpawnAmbientParticles();
                SpawnDivineAuraParticles();
                SpawnAscendingLightParticles();
            }

            // 添加光照
            AddLighting();
        }

        /// <summary>
        /// 检测附近玩家并调整光效
        /// </summary>
        private void CheckNearbyPlayers() {
            float closestDistance = float.MaxValue;

            foreach (Player player in Main.ActivePlayers) {
                float distance = Vector2.Distance(Center, player.Center);
                if (distance < closestDistance) {
                    closestDistance = distance;
                }
            }

            // 根据距离调整光效强度
            if (closestDistance < EffectRadius) {
                float proximityFactor = 1f - (closestDistance / EffectRadius);
                targetGlowIntensity = 0.5f + proximityFactor * 0.5f;
            }
            else {
                targetGlowIntensity = 0.3f;
            }
        }

        /// <summary>
        /// 生成环境粒子效果
        /// </summary>
        private void SpawnAmbientParticles() {
            if (!HasDescended || Main.dedServ) return;

            float scaledWidth = Width;
            float scaledHeight = Height;

            // 神圣光粒（每隔几帧生成）- 增加密度
            if (particleTimer % 4 == 0) {
                for (int j = 0; j < 3; j++) {
                    Vector2 particlePos = Center + Main.rand.NextVector2Circular(scaledWidth * 0.8f, scaledHeight * 0.5f);
                    int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.WhiteTorch;
                    int dust = Dust.NewDust(particlePos, 0, 0, dustType, 0, -2f, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-3f, -1f));
                    Main.dust[dust].fadeIn = 1.5f;
                }
            }

            // 神圣光环粒子（当玩家靠近时更密集）
            if (particleTimer % 8 == 0 && currentGlowIntensity > 0.4f) {
                for (int j = 0; j < 4; j++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = CoreLightRadius * Main.rand.NextFloat(0.2f, 0.9f);
                    Vector2 particlePos = Center + angle.ToRotationVector2() * radius;

                    int dust = Dust.NewDust(particlePos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.8f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (Center - particlePos).SafeNormalize(Vector2.Zero) * 3f;
                    Main.dust[dust].fadeIn = 1.8f;
                }
            }

            // 祥云飘动效果 - 更多更大的云
            if (particleTimer % 20 == 0) {
                for (int j = 0; j < 2; j++) {
                    Vector2 cloudPos = Position + new Vector2(
                        Main.rand.NextFloat(-scaledWidth * 0.5f, scaledWidth * 1.5f),
                        Main.rand.NextFloat(scaledHeight * 0.2f, scaledHeight * 0.8f)
                    );
                    int dust = Dust.NewDust(cloudPos, 0, 0, DustID.Cloud,
                        Main.rand.NextFloat(-1.5f, 1.5f),
                        Main.rand.NextFloat(-0.8f, 0.8f),
                        200,
                        default,
                        Main.rand.NextFloat(3f, 5f));
                    Main.dust[dust].noGravity = true;
                }
            }

            // 翠绿玉光粒子（天柱特色）
            if (particleTimer % 12 == 0) {
                Vector2 jadePos = Center + Main.rand.NextVector2Circular(scaledWidth * 0.6f, scaledHeight * 0.4f);
                int jade = Dust.NewDust(jadePos, 0, 0, DustID.JungleGrass, 0, -1.8f, 120, default, Main.rand.NextFloat(1.4f, 2.4f));
                Main.dust[jade].noGravity = true;
                Main.dust[jade].velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2.5f, -0.8f));
            }
        }

        /// <summary>
        /// 生成神圣光环粒子
        /// </summary>
        private void SpawnDivineAuraParticles() {
            if (!HasDescended || Main.dedServ) return;
            if (currentGlowIntensity < 0.3f) return;

            // 环绕天柱的光环粒子
            if (particleTimer % 6 == 0) {
                float orbitAngle = haloRotation * 3f + particleTimer * 0.05f;
                for (int i = 0; i < 2; i++) {
                    float angle = orbitAngle + i * MathHelper.Pi;
                    float radius = 200f + MathF.Sin(divinePillarTimer + i) * 50f;
                    Vector2 orbitPos = Center + angle.ToRotationVector2() * radius;

                    int dust = Dust.NewDust(orbitPos, 0, 0, DustID.GoldFlame, 0, 0, 80, default, Main.rand.NextFloat(1.8f, 2.5f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;
                }
            }

            // 底部神圣光圈
            if (particleTimer % 10 == 0) {
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float baseRadius = Width * 0.6f + Main.rand.NextFloat(-30f, 30f);
                Vector2 basePos = Position + new Vector2(Width / 2, Height) + baseAngle.ToRotationVector2() * baseRadius;

                int dust = Dust.NewDust(basePos, 0, 0, DustID.WhiteTorch, 0, -0.5f, 100, default, Main.rand.NextFloat(2f, 3f));
                Main.dust[dust].noGravity = true;
            }
        }

        /// <summary>
        /// 生成上升光柱粒子
        /// </summary>
        private void SpawnAscendingLightParticles() {
            if (!HasDescended || Main.dedServ) return;
            if (currentGlowIntensity < 0.5f) return;

            // 从天柱中心向上升腾的光柱
            if (particleTimer % 3 == 0) {
                float xOffset = Main.rand.NextFloat(-Width * 0.3f, Width * 0.3f);
                Vector2 lightPos = Position + new Vector2(Width / 2 + xOffset, Height * 0.8f);

                for (int i = 0; i < 2; i++) {
                    int dust = Dust.NewDust(lightPos, 0, 0, DustID.GoldFlame, 0, -4f - i * 2f, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity.X = Main.rand.NextFloat(-0.3f, 0.3f);
                    Main.dust[dust].fadeIn = 2f;
                }
            }

            // 顶部光芒扩散
            if (particleTimer % 15 == 0) {
                Vector2 topCenter = Position + new Vector2(Width / 2, -50);
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6 + haloRotation;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);
                    vel.Y -= 2f;

                    int dust = Dust.NewDust(topCenter, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 80, default, Main.rand.NextFloat(2f, 3f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].fadeIn = 1.5f;
                }
            }
        }

        /// <summary>
        /// 添加光照效果
        /// </summary>
        private void AddLighting() {
            if (!HasDescended) return;

            // 主体光照 - 神圣金白色，大幅增强
            float pulseIntensity = 0.85f + MathF.Sin(glowPulseTimer) * 0.15f;
            Vector3 mainLight = new Vector3(1f, 0.95f, 0.85f) * currentGlowIntensity * pulseIntensity;
            Vector3 jadeLight = new Vector3(0.7f, 1f, 0.8f) * currentGlowIntensity * pulseIntensity * 0.5f;

            // 天柱多点光源 - 增加光源数量和强度
            int lightPoints = 12;
            for (int i = 0; i < lightPoints; i++) {
                float yOffset = Height * i / (lightPoints - 1);
                Vector2 lightPos = Position + new Vector2(Width / 2, yOffset);
                float intensity = 0.7f + 0.3f * (1f - (float)i / lightPoints);
                Lighting.AddLight(lightPos, mainLight * intensity * 1.5f);

                // 两侧辅助光源
                Lighting.AddLight(lightPos + new Vector2(-Width * 0.4f, 0), mainLight * intensity * 0.6f);
                Lighting.AddLight(lightPos + new Vector2(Width * 0.4f, 0), mainLight * intensity * 0.6f);
            }

            // 顶部强光 - 更强的光芒
            Vector2 topPos = Position + new Vector2(Width / 2, -100);
            Lighting.AddLight(topPos, mainLight * 2f);
            Lighting.AddLight(topPos + new Vector2(0, -50), mainLight * 1.5f);

            // 底部光圈
            Vector2 bottomPos = Position + new Vector2(Width / 2, Height + 50);
            Lighting.AddLight(bottomPos, mainLight * 1.2f);

            // 环绕光源
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8 + haloRotation;
                float radius = CoreLightRadius * 0.4f;
                Vector2 orbitPos = Center + angle.ToRotationVector2() * radius;
                Lighting.AddLight(orbitPos, jadeLight * 0.8f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            pillarTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/PillarofTheHeavenes/PillarofTheHeavens").Value;
            frameWidth = pillarTexture.Width / PillarCount;
            frameHeight = pillarTexture.Height;
            if (pillarTexture == null) return false;

            // 计算帧矩形
            Rectangle sourceRect = new Rectangle(
                PillarStyleIndex * frameWidth,
                0,
                frameWidth,
                frameHeight
            );

            // 降临动画偏移 - 更长的降临距离
            float descendOffset = HasDescended ? 0f : (1f - ACMUtils.QuadOut(DescendProgress)) * 1500f;

            // 浮动偏移 - 轻微的上下浮动
            float floatOffset = HasDescended ? MathF.Sin(floatTimer) * 5f : 0f;

            Vector2 drawPos = Position - Main.screenPosition + new Vector2(0, -descendOffset + floatOffset);
            Vector2 origin = Vector2.Zero;

            // 降临时的透明度渐变
            float alpha = HasDescended ? 1f : ACMUtils.SineInOut(DescendProgress);

            // 绘制外层神圣光晕（最底层）
            DrawOuterDivineGlow(spriteBatch, drawPos, alpha);

            // 绘制光柱效果
            DrawLightPillarEffect(spriteBatch, drawPos, alpha);

            // 绘制光晕背景
            if (currentGlowIntensity > 0.2f) {
                DrawGlowEffect(spriteBatch, drawPos, alpha);
            }

            // 绘制环绕祥云
            DrawSurroundingClouds(spriteBatch, drawPos, alpha);

            // 绘制主体天柱
            Color pillarColor = drawColor * alpha;
            pillarColor = Color.Lerp(pillarColor, Color.White, 0.4f); // 天柱自发光效果增强

            spriteBatch.Draw(
                pillarTexture,
                drawPos,
                sourceRect,
                pillarColor,
                Rotation,
                origin,
                Scale,
                SpriteEffects.None,
                0f
            );

            // 绘制多层高光叠加
            if (HasDescended && currentGlowIntensity > 0.3f) {
                // 第一层金色高光
                Color glowColor1 = new Color(255, 245, 200, 0) * (currentGlowIntensity - 0.3f) * 0.5f;
                spriteBatch.Draw(
                    pillarTexture,
                    drawPos,
                    sourceRect,
                    glowColor1,
                    Rotation,
                    origin,
                    Scale,
                    SpriteEffects.None,
                    0f
                );

                // 第二层白色高光
                if (currentGlowIntensity > 0.5f) {
                    Color glowColor2 = new Color(255, 255, 255, 0) * (currentGlowIntensity - 0.5f) * 0.3f;
                    spriteBatch.Draw(
                        pillarTexture,
                        drawPos,
                        sourceRect,
                        glowColor2,
                        Rotation,
                        origin,
                        Scale * 1.02f,
                        SpriteEffects.None,
                        0f
                    );
                }

                // 第三层翠绿玉光
                if (currentGlowIntensity > 0.6f) {
                    float jadePulse = MathF.Sin(glowPulseTimer * 2f) * 0.5f + 0.5f;
                    Color glowColor3 = new Color(180, 255, 200, 0) * (currentGlowIntensity - 0.6f) * 0.25f * jadePulse;
                    spriteBatch.Draw(
                        pillarTexture,
                        drawPos,
                        sourceRect,
                        glowColor3,
                        Rotation,
                        origin,
                        Scale * 1.04f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false; // 阻止默认绘制
        }

        /// <summary>
        /// 绘制外层神圣光晕
        /// </summary>
        private void DrawOuterDivineGlow(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex == null) return;

            Vector2 glowCenter = drawPos + new Vector2(frameWidth * Scale / 2, frameHeight * Scale / 2);
            float baseAlpha = alpha * currentGlowIntensity * 0.4f;

            // 多层大范围光晕
            for (int i = 0; i < 5; i++) {
                float layerScale = 15f + i * 5f;
                float layerAlpha = baseAlpha * (0.3f - i * 0.05f);
                float pulse = MathF.Sin(glowPulseTimer + i * 0.5f) * 0.1f + 0.9f;

                Color layerColor = Color.Lerp(
                    new Color(255, 240, 180),
                    new Color(180, 220, 255),
                    i * 0.15f
                );
                layerColor.A = 0;
                layerColor *= layerAlpha * pulse;

                spriteBatch.Draw(
                    glowTex,
                    glowCenter,
                    null,
                    layerColor,
                    haloRotation * (1f + i * 0.2f),
                    glowTex.Size() / 2,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制光柱效果
        /// </summary>
        private void DrawLightPillarEffect(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex == null || !HasDescended) return;

            Vector2 pillarCenter = drawPos + new Vector2(frameWidth * Scale / 2, 0);
            float beamAlpha = alpha * currentGlowIntensity * 0.3f;

            // 从顶部向上延伸的光柱
            for (int i = 0; i < 8; i++) {
                float yOffset = -i * 80f;
                float fadeOut = 1f - i * 0.1f;
                float pulse = MathF.Sin(divinePillarTimer + i * 0.3f) * 0.2f + 0.8f;

                Color beamColor = new Color(255, 250, 220, 0) * beamAlpha * fadeOut * pulse;

                spriteBatch.Draw(
                    glowTex,
                    pillarCenter + new Vector2(0, yOffset),
                    null,
                    beamColor,
                    0f,
                    glowTex.Size() / 2,
                    new Vector2(3f, 1.5f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制环绕祥云
        /// </summary>
        private void DrawSurroundingClouds(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex == null || !HasDescended) return;

            int smokeFrameSize = smokeTex.Width / 4; // 4x4 帧
            Vector2 pillarCenter = drawPos + new Vector2(frameWidth * Scale / 2, frameHeight * Scale / 2);

            // 环绕的祥云
            for (int i = 0; i < 6; i++) {
                float angle = cloudDriftTimer + i * MathHelper.TwoPi / 6;
                float radius = 250f + MathF.Sin(cloudDriftTimer * 2f + i) * 50f;
                float yOffset = MathF.Sin(cloudDriftTimer + i * 0.8f) * 100f;

                Vector2 cloudPos = pillarCenter + new Vector2(MathF.Cos(angle) * radius, yOffset);

                int frameX = (i + (int)(cloudDriftTimer * 2f)) % 4;
                int frameY = (i * 2) % 4;
                Rectangle smokeRect = new Rectangle(frameX * smokeFrameSize, frameY * smokeFrameSize, smokeFrameSize, smokeFrameSize);

                Color cloudColor = new Color(255, 255, 255, 0) * alpha * currentGlowIntensity * 0.4f;
                float cloudScale = 1.5f + MathF.Sin(cloudDriftTimer + i) * 0.3f;

                spriteBatch.Draw(
                    smokeTex,
                    cloudPos,
                    smokeRect,
                    cloudColor,
                    cloudDriftTimer * 0.1f + i,
                    new Vector2(smokeFrameSize / 2),
                    cloudScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制神圣光效
        /// </summary>
        private void DrawGlowEffect(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            Texture2D glowTex = ACMAsset.Sparkle;
            if (glowTex == null) return;

            float glowAlpha = (currentGlowIntensity - 0.2f) * alpha;
            Vector2 glowCenter = drawPos + new Vector2(frameWidth * Scale / 2, frameHeight * Scale / 2);

            // 多层光晕 - 更多层次
            for (int i = 0; i < 5; i++) {
                float layerScale = 8f + i * 3f;
                float layerAlpha = glowAlpha * (0.5f - i * 0.08f);
                float rotSpeed = 1f + i * 0.3f;

                Color layerColor = Color.Lerp(
                    new Color(255, 235, 180),
                    new Color(180, 255, 220),
                    i * 0.2f
                );
                layerColor.A = 0;
                layerColor *= layerAlpha;

                spriteBatch.Draw(
                    glowTex,
                    glowCenter,
                    null,
                    layerColor,
                    haloRotation * rotSpeed + i * 0.5f,
                    glowTex.Size() / 2,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 添加星芒效果
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex != null && currentGlowIntensity > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float angle = haloRotation * 2f + i * MathHelper.PiOver2;
                    float dist = 100f + MathF.Sin(glowPulseTimer + i) * 30f;
                    Vector2 starPos = glowCenter + angle.ToRotationVector2() * dist;

                    float starAlpha = (currentGlowIntensity - 0.5f) * alpha * 0.6f;
                    Color starColor = new Color(255, 250, 220, 0) * starAlpha;

                    spriteBatch.Draw(
                        starTex,
                        starPos,
                        null,
                        starColor,
                        angle + glowPulseTimer,
                        starTex.Size() / 2,
                        0.6f + MathF.Sin(glowPulseTimer * 2f + i) * 0.2f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            // 绘制顶部符文光效
            if (HasDescended && currentGlowIntensity > 0.3f) {
                DrawTopRune(spriteBatch);
                DrawDivineHalo(spriteBatch);
            }
        }

        /// <summary>
        /// 绘制顶部符文
        /// </summary>
        private void DrawTopRune(SpriteBatch spriteBatch) {
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex == null) return;

            float floatOffset = MathF.Sin(floatTimer * 1.5f) * 8f;
            Vector2 topPos = Position - Main.screenPosition + new Vector2(frameWidth * Scale / 2, -80 + floatOffset);

            // 多层符文效果
            for (int i = 0; i < 3; i++) {
                float runeScale = (1.2f - i * 0.2f) + MathF.Sin(glowPulseTimer * 2f + i) * 0.15f;
                float runeAlpha = currentGlowIntensity * (0.9f - i * 0.2f);

                Color runeColor = Color.Lerp(
                    new Color(255, 240, 180, 0),
                    new Color(180, 255, 220, 0),
                    i * 0.3f
                ) * runeAlpha;

                spriteBatch.Draw(
                    starTex,
                    topPos,
                    null,
                    runeColor,
                    haloRotation * (2f + i * 0.5f),
                    starTex.Size() / 2,
                    runeScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制神圣光环
        /// </summary>
        private void DrawDivineHalo(SpriteBatch spriteBatch) {
            Texture2D glowTex = ACMAsset.LightShot;
            if (glowTex == null) return;

            // 在天柱周围绘制扩散的光环
            Vector2 center = Position - Main.screenPosition + new Vector2(frameWidth * Scale / 2, frameHeight * Scale / 2);

            // 扩散光环
            float expandPhase = (haloExpandTimer % MathHelper.TwoPi) / MathHelper.TwoPi;
            float expandScale = 5f + expandPhase * 15f;
            float expandAlpha = (1f - expandPhase) * currentGlowIntensity * 0.3f;

            Color expandColor = new Color(255, 250, 220, 0) * expandAlpha;

            spriteBatch.Draw(
                glowTex,
                center,
                null,
                expandColor,
                0f,
                glowTex.Size() / 2,
                expandScale,
                SpriteEffects.None,
                0f
            );

            // 第二层扩散（错开相位）
            float expandPhase2 = ((haloExpandTimer + MathHelper.Pi) % MathHelper.TwoPi) / MathHelper.TwoPi;
            float expandScale2 = 5f + expandPhase2 * 15f;
            float expandAlpha2 = (1f - expandPhase2) * currentGlowIntensity * 0.2f;

            Color expandColor2 = new Color(200, 255, 230, 0) * expandAlpha2;

            spriteBatch.Draw(
                glowTex,
                center,
                null,
                expandColor2,
                0f,
                glowTex.Size() / 2,
                expandScale2,
                SpriteEffects.None,
                0f
            );
        }

        /// <summary>
        /// 获取天柱的方位名称（东南西北）
        /// </summary>
        public string GetDirectionName() {
            return PillarStyleIndex switch {
                0 => "东方天柱",
                1 => "南方天柱",
                2 => "西方天柱",
                3 => "北方天柱",
                _ => "天柱"
            };
        }
    }
}

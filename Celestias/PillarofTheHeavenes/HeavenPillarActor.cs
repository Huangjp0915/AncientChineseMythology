using InnoVault.Actors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
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
        /// <summary>天柱影响范围（玩家靠近此距离时触发效果）</summary>
        public const float EffectRadius = 800f;
        /// <summary>天柱核心光效范围</summary>
        public const float CoreLightRadius = 400f;
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
        #endregion

        #region 纹理缓存
        private static Texture2D pillarTexture;
        private static int frameWidth;
        private static int frameHeight;
        #endregion

        public override void SetStaticDefaults() {
            // 预加载纹理并计算帧尺寸
            if (!Main.dedServ) {
                pillarTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/PillarofTheHeavenes/PillarofTheHeavens").Value;
                frameWidth = pillarTexture.Width / PillarCount;
                frameHeight = pillarTexture.Height;
            }
        }

        public override void OnSpawn(params object[] args) {
            // 初始化天柱属性
            Width = frameWidth > 0 ? frameWidth : 128;
            Height = frameHeight > 0 ? frameHeight : 512;
            DrawLayer = ActorDrawLayer.BeforePlayers;
            DrawExtendMode = 600; // 扩大绘制范围以容纳大型天柱

            // 初始化动画状态
            DescendProgress = 0f;
            HasDescended = false;
            glowPulseTimer = 0f;
            floatTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            particleTimer = 0;
            haloRotation = 0f;
            currentGlowIntensity = 0f;
            targetGlowIntensity = 1f;

            // 如果传入了样式索引参数
            if (args != null && args.Length > 0 && args[0] is int styleIndex) {
                PillarStyleIndex = (int)MathHelper.Clamp(styleIndex, 0, PillarCount - 1);
            }
        }

        public override void AI() {
            // 更新降临动画
            if (!HasDescended) {
                DescendProgress = MathHelper.Clamp(DescendProgress + 0.008f, 0f, 1f);
                if (DescendProgress >= 1f) {
                    HasDescended = true;
                    NetUpdate = true;
                }
            }

            // 更新动画计时器
            glowPulseTimer += 0.03f;
            floatTimer += 0.02f;
            haloRotation += 0.005f;
            particleTimer++;

            // 轻微浮动效果
            if (HasDescended) {
                float floatOffset = MathF.Sin(floatTimer) * 3f;
                // 位置微调由绘制时处理，保持逻辑位置不变
            }

            // 更新光效强度
            currentGlowIntensity = MathHelper.Lerp(currentGlowIntensity, targetGlowIntensity, 0.05f);

            // 检测附近玩家并触发效果
            CheckNearbyPlayers();

            // 生成粒子效果
            if (!VaultUtils.isServer) {
                SpawnAmbientParticles();
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

            // 神圣光粒（每隔几帧生成）
            if (particleTimer % 8 == 0) {
                Vector2 particlePos = Center + Main.rand.NextVector2Circular(Width * 0.6f, Height * 0.4f);
                int dustType = Main.rand.NextBool(3) ? Terraria.ID.DustID.GoldFlame : Terraria.ID.DustID.WhiteTorch;
                int dust = Dust.NewDust(particlePos, 0, 0, dustType, 0, -1.5f, 150, default, Main.rand.NextFloat(0.8f, 1.4f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -0.5f));
            }

            // 神圣光环粒子（当玩家靠近时更密集）
            if (particleTimer % 15 == 0 && currentGlowIntensity > 0.5f) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = CoreLightRadius * Main.rand.NextFloat(0.3f, 0.8f);
                Vector2 particlePos = Center + angle.ToRotationVector2() * radius;

                int dust = Dust.NewDust(particlePos, 0, 0, Terraria.ID.DustID.GoldFlame, 0, 0, 100, default, Main.rand.NextFloat(1.2f, 2f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Center - particlePos).SafeNormalize(Vector2.Zero) * 2f;
            }

            // 云雾飘动效果
            if (particleTimer % 40 == 0) {
                Vector2 cloudPos = Position + new Vector2(Main.rand.NextFloat(Width), Main.rand.NextFloat(Height * 0.3f, Height * 0.7f));
                int dust = Dust.NewDust(cloudPos, 0, 0, Terraria.ID.DustID.Cloud, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-0.5f, 0.5f), 200, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dust].noGravity = true;
            }
        }

        /// <summary>
        /// 添加光照效果
        /// </summary>
        private void AddLighting() {
            if (!HasDescended) return;

            // 主体光照 - 神圣金白色
            float pulseIntensity = 0.8f + MathF.Sin(glowPulseTimer) * 0.2f;
            Vector3 mainLight = new Vector3(1f, 0.95f, 0.8f) * currentGlowIntensity * pulseIntensity;

            // 天柱多点光源
            int lightPoints = 5;
            for (int i = 0; i < lightPoints; i++) {
                float yOffset = Height * i / (lightPoints - 1);
                Vector2 lightPos = Position + new Vector2(Width / 2, yOffset);
                Lighting.AddLight(lightPos, mainLight * (0.6f + 0.4f * (1f - (float)i / lightPoints)));
            }

            // 顶部强光
            Lighting.AddLight(Position + new Vector2(Width / 2, 0), mainLight * 1.2f);
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

            // 降临动画偏移
            float descendOffset = HasDescended ? 0f : (1f - ACMUtils.QuadOut(DescendProgress)) * 800f;

            // 浮动偏移
            float floatOffset = HasDescended ? MathF.Sin(floatTimer) * 3f : 0f;

            Vector2 drawPos = Position - Main.screenPosition + new Vector2(0, -descendOffset + floatOffset);
            Vector2 origin = Vector2.Zero;

            // 降临时的透明度渐变
            float alpha = HasDescended ? 1f : ACMUtils.SineInOut(DescendProgress);

            // 绘制光晕背景（玩家靠近时更明显）
            if (currentGlowIntensity > 0.3f) {
                DrawGlowEffect(spriteBatch, drawPos, alpha);
            }

            // 绘制主体天柱
            Color pillarColor = drawColor * alpha;
            pillarColor = Color.Lerp(pillarColor, Color.White, 0.3f); // 天柱自发光效果

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

            // 绘制高光叠加
            if (HasDescended && currentGlowIntensity > 0.5f) {
                Color glowColor = new Color(255, 250, 220, 0) * (currentGlowIntensity - 0.5f) * 0.6f;
                spriteBatch.Draw(
                    pillarTexture,
                    drawPos,
                    sourceRect,
                    glowColor,
                    Rotation,
                    origin,
                    Scale,
                    SpriteEffects.None,
                    0f
                );
            }

            return false; // 阻止默认绘制
        }

        /// <summary>
        /// 绘制神圣光效
        /// </summary>
        private void DrawGlowEffect(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            // 使用ACMAsset中的光效纹理（如果可用）
            Texture2D glowTex = ACMAsset.Sparkle;
            if (glowTex == null) return;

            float glowAlpha = (currentGlowIntensity - 0.3f) * alpha;
            Vector2 glowCenter = drawPos + new Vector2(frameWidth / 2, frameHeight / 2);

            // 多层光晕
            for (int i = 0; i < 3; i++) {
                float layerScale = 6f + i * 2f;
                float layerAlpha = glowAlpha * (0.4f - i * 0.1f);
                Color layerColor = Color.Lerp(new Color(255, 230, 180), new Color(200, 180, 255), i * 0.3f);
                layerColor.A = 0;
                layerColor *= layerAlpha;

                spriteBatch.Draw(
                    glowTex,
                    glowCenter,
                    null,
                    layerColor,
                    haloRotation + i * 0.5f,
                    glowTex.Size() / 2,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            // 绘制顶部符文光效
            if (HasDescended && currentGlowIntensity > 0.4f) {
                DrawTopRune(spriteBatch);
            }
        }

        /// <summary>
        /// 绘制顶部符文
        /// </summary>
        private void DrawTopRune(SpriteBatch spriteBatch) {
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex == null) return;

            Vector2 topPos = Position - Main.screenPosition + new Vector2(frameWidth / 2, -30);
            float floatOffset = MathF.Sin(floatTimer * 1.5f) * 5f;
            topPos.Y += floatOffset;

            float runeScale = 0.8f + MathF.Sin(glowPulseTimer * 2f) * 0.1f;
            Color runeColor = new Color(255, 240, 200, 0) * currentGlowIntensity * 0.8f;

            spriteBatch.Draw(
                starTex,
                topPos,
                null,
                runeColor,
                haloRotation * 2f,
                starTex.Size() / 2,
                runeScale,
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

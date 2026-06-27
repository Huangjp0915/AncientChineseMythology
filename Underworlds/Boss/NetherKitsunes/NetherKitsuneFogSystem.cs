using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥青丘狐战斗迷雾系统 - 创造诡异的地府氛围
    /// 与幽冥龙的迷雾系统类似但有独特的狐妖风格
    /// </summary>
    internal class NetherKitsuneFogSystem : ModSystem
    {
        private static bool isActive = false;
        private static float intensity = 0f;
        private static int bossNPCIndex = -1;

        // 幽魂迷雾层
        private static readonly List<SoulFogLayer> soulFogs = new();
        private const int MaxSoulFogs = 50;

        // 狐火游离粒子
        private static readonly List<FoxfireWisp> wisps = new();
        private const int MaxWisps = 25;

        // 魂魄涟漪效果
        private static readonly List<SoulRipple> ripples = new();
        private const int MaxRipples = 12;

        private static float globalTimer = 0f;
        private static Vector2 bossLastPosition = Vector2.Zero;
        private static Vector2 battlefieldCenter = Vector2.Zero;
        private static float battlefieldRadius = 1000f;

        // ===== V2 演出发布通道 (由 NetherKitsune AI 每帧写入, 本系统在 PostDrawTiles 绘制) =====
        // 魂火泛光 RadialBloom (大刺/相变/裁决) —— 加性 overlay, 不读 screenTarget。
        private static float soulBloom;
        private static Vector2 bloomCenter;
        private static Color bloomColor = new Color(130, 210, 255);
        // 尾巴/虚空九刺地纹法阵 ArenaRunic (可读落点/真身锚) —— 屏幕空间 SDF。
        private static float runic;
        private static Vector2 runicCenter;
        private static float runicRadius = 360f;
        private static bool runicLethal;

        public static bool IsActive => isActive;
        public static float Intensity => intensity;

        /// <summary>由 Boss AI 发布魂火径向泛光 (世界中心 / 0~1 强度 / 颜色)。</summary>
        public static void PublishBloom(Vector2 center, float strength, Color color) {
            soulBloom = MathHelper.Clamp(strength, 0f, 1f);
            bloomCenter = center;
            bloomColor = color;
        }

        /// <summary>由 Boss AI 发布法阵预警 (世界中心 / 世界半径 / 0~1 强度 / 是否致命转红)。</summary>
        public static void PublishRunic(Vector2 center, float worldRadius, float strength, bool lethal) {
            runic = MathHelper.Clamp(strength, 0f, 1f);
            runicCenter = center;
            runicRadius = worldRadius;
            runicLethal = lethal;
        }

        /// <summary>
        /// 激活迷雾效果
        /// </summary>
        public static void Activate(int bossIndex) {
            if (bossIndex < 0 || bossIndex >= Main.maxNPCs)
                return;

            isActive = true;
            bossNPCIndex = bossIndex;
            intensity = 0f;

            NPC boss = Main.npc[bossIndex];
            battlefieldCenter = boss.Center;
            bossLastPosition = boss.Center;

            // 初始化幽魂迷雾
            soulFogs.Clear();
            for (int i = 0; i < MaxSoulFogs; i++) {
                soulFogs.Add(new SoulFogLayer(battlefieldCenter, battlefieldRadius));
            }

            wisps.Clear();
            ripples.Clear();
        }

        /// <summary>
        /// 停用迷雾效果
        /// </summary>
        public static void Deactivate() {
            isActive = false;
            bossNPCIndex = -1;
            soulBloom = 0f;
            runic = 0f;
        }

        /// <summary>
        /// 创建魂魄涟漪（Boss攻击时调用）
        /// </summary>
        public static void CreateRipple(Vector2 position, float strength = 1f) {
            if (ripples.Count < MaxRipples) {
                ripples.Add(new SoulRipple(position, strength));
            }
        }

        /// <summary>
        /// 创建狐火游离
        /// </summary>
        private static void CreateWisp(Vector2 position, Vector2 velocity) {
            if (wisps.Count < MaxWisps) {
                wisps.Add(new FoxfireWisp(position, velocity));
            }
        }

        public override void PostUpdateEverything() {
            if (!isActive) {
                if (intensity > 0f) {
                    intensity -= 0.015f;
                    if (intensity <= 0f) {
                        intensity = 0f;
                        soulFogs.Clear();
                        wisps.Clear();
                        ripples.Clear();
                    }
                }
                return;
            }

            // 验证Boss是否存活
            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs ||
                !Main.npc[bossNPCIndex].active || Main.npc[bossNPCIndex].type != ModContent.NPCType<NetherKitsune>()) {
                Deactivate();
                return;
            }

            NPC boss = Main.npc[bossNPCIndex];

            // 强度渐入
            if (intensity < 1f) {
                intensity = Math.Min(intensity + 0.006f, 1f);
            }

            globalTimer += 0.016f;
            if (globalTimer > MathHelper.TwoPi * 10f) {
                globalTimer -= MathHelper.TwoPi * 10f;
            }

            // 更新战场中心
            battlefieldCenter = Vector2.Lerp(battlefieldCenter, boss.Center, 0.015f);

            // Boss移动时产生狐火
            Vector2 bossVelocity = boss.Center - bossLastPosition;
            float bossSpeed = bossVelocity.Length();

            if (bossSpeed > 8f && Main.rand.NextBool(3)) {
                Vector2 wispPos = boss.Center - bossVelocity.SafeNormalize(Vector2.Zero) * 60f;
                wispPos += Main.rand.NextVector2Circular(30f, 30f);
                CreateWisp(wispPos, -bossVelocity * 0.1f + Main.rand.NextVector2Circular(2f, 2f));
            }

            bossLastPosition = boss.Center;

            // 更新幽魂迷雾
            foreach (var fog in soulFogs) {
                fog.Update(boss, battlefieldCenter, globalTimer);
            }

            // 更新狐火
            for (int i = wisps.Count - 1; i >= 0; i--) {
                wisps[i].Update();
                if (wisps[i].IsDead) {
                    wisps.RemoveAt(i);
                }
            }

            // 更新涟漪
            for (int i = ripples.Count - 1; i >= 0; i--) {
                ripples[i].Update();
                if (ripples[i].IsDead) {
                    ripples.RemoveAt(i);
                }
            }
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu)
                return;

            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs || !Main.npc[bossNPCIndex].active)
                return;

            // 迷雾精灵层 (需 Underworld.Fog 与可见强度)
            if (Underworld.Fog != null && intensity > 0.01f) {
                SpriteBatch spriteBatch = Main.spriteBatch;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                DrawSoulFogs(spriteBatch);
                DrawWisps(spriteBatch);
                DrawRipples(spriteBatch);

                spriteBatch.End();
            }

            // V2 演出层 (各自管理批次): 法阵预警 → 魂火泛光
            DrawArenaRunic();
            DrawSoulBloom();
        }

        // ===== V2: ArenaRunic 法阵预警 (尾巴落点 / 真身锚 / 虚空九刺收口) =====
        private static void DrawArenaRunic() {
            if (runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(runicCenter, runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            Color primary = runicLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color secondary = runicLethal ? TelegraphColors.Execution : TelegraphColors.GhostGreen;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(9f);
            fx.Parameters["uMode"]?.SetValue(0f);   // 法阵
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== V2: RadialBloom 魂火泛光 (大刺/相变/真身裁决) — 加性 overlay, 不占全屏 screenTarget 名额 =====
        private static void DrawSoulBloom() {
            if (soulBloom <= 0.01f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (bloomCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(soulBloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.24f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uColor"]?.SetValue(bloomColor.ToVector4());
            fx.Parameters["uRayCount"]?.SetValue(9f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }

        private static void DrawSoulFogs(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var fog in soulFogs) {
                Vector2 drawPos = fog.Position - Main.screenPosition;

                if (drawPos.X < -400 || drawPos.X > Main.screenWidth + 400 ||
                    drawPos.Y < -400 || drawPos.Y > Main.screenHeight + 400)
                    continue;

                Color fogColor = fog.GetColor();
                float alpha = fog.GetAlpha() * intensity;

                // 主迷雾层
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

                // 幽光层
                Color glowColor = new Color(100, 180, 255) * alpha * 0.2f;
                glowColor.A = 0;
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    glowColor,
                    fog.Rotation * 0.6f,
                    fogTex.Size() * 0.5f,
                    fog.Scale * 1.3f * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void DrawWisps(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var wisp in wisps) {
                Vector2 drawPos = wisp.Position - Main.screenPosition;

                // 狐火颜色 - 幽蓝带白
                Color wispColor = Color.Lerp(new Color(80, 160, 220), new Color(150, 200, 255), wisp.GetPulse());
                float alpha = wisp.GetAlpha() * intensity;
                wispColor.A = 0;

                // 内核
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    wispColor * alpha,
                    wisp.Rotation,
                    fogTex.Size() * 0.5f,
                    wisp.Scale * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );

                // 外晕
                Color outerColor = new Color(60, 120, 180) * alpha * 0.4f;
                outerColor.A = 0;
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    outerColor,
                    wisp.Rotation * 0.8f,
                    fogTex.Size() * 0.5f,
                    wisp.Scale * 2f * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void DrawRipples(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var ripple in ripples) {
                Vector2 drawPos = ripple.Position - Main.screenPosition;

                // 魂魄涟漪 - 幽蓝色
                Color rippleColor = new Color(90, 170, 240);
                float alpha = ripple.GetAlpha() * intensity;

                for (int i = 0; i < 3; i++) {
                    float scaleOffset = i * 0.5f;
                    float alphaOffset = 1f - i * 0.3f;

                    Color layerColor = rippleColor * alpha * alphaOffset * 0.4f;
                    layerColor.A = 0;

                    sb.Draw(
                        fogTex,
                        drawPos,
                        null,
                        layerColor,
                        ripple.Rotation + i * 0.2f,
                        fogTex.Size() * 0.5f,
                        ripple.Scale * (1f + scaleOffset) * Main.GameViewMatrix.Zoom.X,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        /// <summary>
        /// 获取指定位置的迷雾密度
        /// </summary>
        public static float GetFogDensityAt(Vector2 position) {
            if (!isActive || intensity <= 0f)
                return 0f;

            float distanceFromCenter = Vector2.Distance(position, battlefieldCenter);
            float baseDensity = 1f - Math.Clamp(distanceFromCenter / battlefieldRadius, 0f, 1f);

            float layerDensity = 0f;
            int nearbyCount = 0;

            foreach (var fog in soulFogs) {
                float distance = Vector2.Distance(fog.Position, position);
                if (distance < 250f) {
                    nearbyCount++;
                    layerDensity += (1f - distance / 250f) * fog.GetAlpha();
                }
            }

            if (nearbyCount > 0)
                layerDensity /= nearbyCount;

            return Math.Min((baseDensity * 0.5f + layerDensity * 0.5f) * intensity, 1f);
        }
    }

    /// <summary>
    /// 幽魂迷雾层 - 飘渺的魂魄气息
    /// </summary>
    internal class SoulFogLayer
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float PulsePhase;
        public bool IsNearBoss;

        private Color baseColor;
        private Vector2 battlefieldCenter;
        private float battlefieldRadius;

        private const float BossInfluenceRadius = 200f;

        public SoulFogLayer(Vector2 center, float radius) {
            battlefieldCenter = center;
            battlefieldRadius = radius;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(0f, radius * 0.85f);
            Position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            Velocity = Main.rand.NextVector2Circular(0.3f, 0.3f);
            Scale = Main.rand.NextFloat(2.5f, 5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotationSpeed = Main.rand.NextFloat(-0.003f, 0.003f);
            PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi);

            // 幽蓝色调
            Color[] colors = new Color[]
            {
                new Color(30, 50, 70),
                new Color(35, 55, 80),
                new Color(25, 45, 65),
                new Color(40, 60, 85),
            };
            baseColor = colors[Main.rand.Next(colors.Length)];
        }

        public void Update(NPC boss, Vector2 center, float timer) {
            battlefieldCenter = center;

            Position += Velocity;
            Rotation += RotationSpeed;
            PulsePhase += 0.012f;

            // 保持在战场范围内
            Vector2 toCenter = battlefieldCenter - Position;
            float distanceFromCenter = toCenter.Length();
            if (distanceFromCenter > battlefieldRadius * 0.75f) {
                Velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.04f;
            }

            if (Velocity.Length() > 0.6f) {
                Velocity = Vector2.Normalize(Velocity) * 0.6f;
            }

            // Boss影响
            Vector2 toBoss = boss.Center - Position;
            float distanceToBoss = toBoss.Length();

            IsNearBoss = distanceToBoss < BossInfluenceRadius;

            if (IsNearBoss) {
                // 被Boss吸引
                float attractStrength = (1f - distanceToBoss / BossInfluenceRadius) * 0.8f;
                Velocity += toBoss.SafeNormalize(Vector2.Zero) * attractStrength * 0.05f;

                // 跟随Boss移动
                Velocity += boss.velocity * 0.02f;
            }
        }

        public Color GetColor() {
            if (IsNearBoss) {
                return Color.Lerp(baseColor, new Color(80, 140, 200), 0.5f);
            }
            return baseColor;
        }

        public float GetAlpha() {
            float pulse = MathF.Sin(PulsePhase) * 0.2f + 0.8f;
            float baseAlpha = 0.55f;

            if (IsNearBoss) {
                baseAlpha *= 1.2f;
            }

            return baseAlpha * pulse;
        }
    }

    /// <summary>
    /// 狐火游离 - 幽蓝色的鬼火
    /// </summary>
    internal class FoxfireWisp
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Progress;
        public bool IsDead => Progress >= 1f;

        private float pulsePhase;

        public FoxfireWisp(Vector2 position, Vector2 velocity) {
            Position = position;
            Velocity = velocity;
            Scale = Main.rand.NextFloat(0.3f, 0.6f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Progress = 0f;
            pulsePhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Progress += 0.008f;
            Position += Velocity;
            Velocity *= 0.97f;

            // 轻微上浮
            Velocity.Y -= 0.03f;

            // 随机飘动
            Velocity += Main.rand.NextVector2Circular(0.1f, 0.1f);

            Rotation += 0.02f;
            pulsePhase += 0.15f;

            Scale = MathHelper.Lerp(0.3f, 0.8f, MathF.Sin(Progress * MathF.PI));
        }

        public float GetAlpha() {
            return MathF.Sin(Progress * MathF.PI) * 0.8f;
        }

        public float GetPulse() {
            return 0.5f + 0.5f * MathF.Sin(pulsePhase);
        }
    }

    /// <summary>
    /// 魂魄涟漪 - Boss攻击时的波动效果
    /// </summary>
    internal class SoulRipple
    {
        public Vector2 Position;
        public float Scale;
        public float Rotation;
        public float Progress;
        public float Strength;
        public bool IsDead => Progress >= 1f;

        public SoulRipple(Vector2 position, float strength) {
            Position = position;
            Scale = 0.3f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Progress = 0f;
            Strength = Math.Clamp(strength, 0.5f, 2.5f);
        }

        public void Update() {
            Progress += 0.01f * Strength;
            Scale = MathHelper.Lerp(0.3f, 6f, Progress);
            Rotation += 0.006f;
        }

        public float GetAlpha() {
            return MathF.Sin(Progress * MathF.PI) * Strength * 0.6f;
        }
    }
}

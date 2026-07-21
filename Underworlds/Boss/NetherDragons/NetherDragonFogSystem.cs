using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙战斗雾气系统 — 环境限视冥雾 + 涟漪冲击语言。
    ///
    /// V3 瘦身 (性能契约):
    ///   ● 环境雾 60 → 28 层, 隔层单绘 (draw call 120 → ~42);
    ///   ● 删除涡流层 (贡献趋近于零的第三层);
    ///   ● <see cref="GetFogDensityAt"/> 由遍历全部雾层改为 O(1) 中心距离推导
    ///     (V2 被每节身段每帧调用, 峰值 ~2000 距离计算/帧)。
    /// 涟漪保留 — 它是 Boss 冲击动作的世界层反馈语言 (破门/吐息/换阶段)。
    /// </summary>
    internal class NetherDragonFogSystem : ModSystem
    {
        private static bool isActive = false;
        private static float intensity = 0f;
        private static int bossNPCIndex = -1;

        // 环境雾气层 (大范围、缓慢移动的背景雾)
        private static readonly List<AmbientFogLayer> ambientFogs = new();
        private const int MaxAmbientFogs = 28;

        // 雾气涟漪 (Boss 冲击动作)
        private static readonly List<FogRipple> ripples = new();
        private const int MaxRipples = 10;

        private static float globalTimer = 0f;

        private static Vector2 battlefieldCenter = Vector2.Zero;
        private static float battlefieldRadius = 1200f;

        public static bool IsActive => isActive;

        /// <summary>激活雾气效果。</summary>
        public static void Activate(int bossIndex) {
            if (bossIndex < 0 || bossIndex >= Main.maxNPCs)
                return;

            isActive = true;
            bossNPCIndex = bossIndex;
            intensity = 0f;

            NPC boss = Main.npc[bossIndex];
            battlefieldCenter = boss.Center;

            ambientFogs.Clear();
            for (int i = 0; i < MaxAmbientFogs; i++) {
                ambientFogs.Add(new AmbientFogLayer(battlefieldCenter, battlefieldRadius));
            }

            ripples.Clear();
        }

        /// <summary>停用雾气效果。</summary>
        public static void Deactivate() {
            isActive = false;
            bossNPCIndex = -1;
        }

        /// <summary>创建雾气涟漪 (Boss 冲击动作的世界层反馈)。</summary>
        public static void CreateRipple(Vector2 position, float strength = 1f) {
            if (ripples.Count < MaxRipples) {
                ripples.Add(new FogRipple(position, strength));
            }
        }

        public override void PostUpdateEverything() {
            if (!isActive) {
                if (intensity > 0f) {
                    intensity -= 0.01f;
                    if (intensity <= 0f) {
                        intensity = 0f;
                        ambientFogs.Clear();
                        ripples.Clear();
                    }
                }
                // 残余涟漪继续播完
                for (int i = ripples.Count - 1; i >= 0; i--) {
                    ripples[i].Update();
                    if (ripples[i].IsDead)
                        ripples.RemoveAt(i);
                }
                return;
            }

            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs ||
                !Main.npc[bossNPCIndex].active || Main.npc[bossNPCIndex].type != ModContent.NPCType<NetherDragonHead>()) {
                Deactivate();
                return;
            }

            NPC boss = Main.npc[bossNPCIndex];

            if (intensity < 1f) {
                intensity = Math.Min(intensity + 0.008f, 1f);
            }

            globalTimer += 0.016f;
            if (globalTimer > MathHelper.TwoPi * 10f) {
                globalTimer -= MathHelper.TwoPi * 10f;
            }

            // 战场中心跟随 Boss 缓慢移动
            battlefieldCenter = Vector2.Lerp(battlefieldCenter, boss.Center, 0.01f);

            foreach (var fog in ambientFogs) {
                fog.Update(boss, battlefieldCenter);
            }

            for (int i = ripples.Count - 1; i >= 0; i--) {
                ripples[i].Update();
                if (ripples[i].IsDead)
                    ripples.RemoveAt(i);
            }
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu || intensity <= 0.01f || Underworld.Fog == null)
                return;

            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs || !Main.npc[bossNPCIndex].active)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            DrawAmbientFogs(spriteBatch);
            DrawRipples(spriteBatch);

            spriteBatch.End();
        }

        private static void DrawAmbientFogs(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            for (int i = 0; i < ambientFogs.Count; i++) {
                AmbientFogLayer fog = ambientFogs[i];
                Vector2 drawPos = fog.Position - Main.screenPosition;

                // 屏幕剔除
                if (drawPos.X < -500 || drawPos.X > Main.screenWidth + 500 ||
                    drawPos.Y < -500 || drawPos.Y > Main.screenHeight + 500)
                    continue;

                Color fogColor = fog.GetColor();
                float alpha = fog.GetAlpha() * intensity;

                sb.Draw(fogTex, drawPos, null, fogColor * alpha, fog.Rotation,
                    fogTex.Size() * 0.5f, fog.Scale * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);

                // 柔和光晕层只画偶数层 (draw call 减半, 观感几乎无损)
                if (i % 2 == 0) {
                    sb.Draw(fogTex, drawPos, null, fogColor * alpha * 0.3f, fog.Rotation * 0.7f,
                        fogTex.Size() * 0.5f, fog.Scale * 1.4f * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawRipples(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var ripple in ripples) {
                Vector2 drawPos = ripple.Position - Main.screenPosition;

                Color rippleColor = new Color(100, 150, 220);
                float alpha = ripple.GetAlpha() * MathF.Max(intensity, 0.4f);

                for (int i = 0; i < 3; i++) {
                    sb.Draw(fogTex, drawPos, null, rippleColor * alpha * (1f - i * 0.25f) * 0.5f,
                        ripple.Rotation + i * 0.3f, fogTex.Size() * 0.5f,
                        ripple.Scale * (1f + i * 0.4f) * Main.GameViewMatrix.Zoom.X, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>指定位置雾密度 — O(1) 中心距离推导 (供体节/绘制大量调用)。</summary>
        public static float GetFogDensityAt(Vector2 position) {
            if (!isActive || intensity <= 0f)
                return 0f;

            float distanceFromCenter = Vector2.Distance(position, battlefieldCenter);
            float baseDensity = 1f - Math.Clamp(distanceFromCenter / battlefieldRadius, 0f, 1f);
            return Math.Min(baseDensity * intensity, 1f);
        }
    }

    /// <summary>
    /// 环境雾气层 - 大范围的缓慢移动背景雾
    /// </summary>
    internal class AmbientFogLayer
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float RotationSpeed;
        public float PulsePhase;
        public bool IsRepelledByBoss;

        private readonly Color baseColor;
        private Vector2 battlefieldCenter;
        private readonly float battlefieldRadius;

        // Boss 体积碰撞参数
        private const float BossRepelRadius = 180f;
        private const float BossRepelStrength = 3f;

        public AmbientFogLayer(Vector2 center, float radius) {
            battlefieldCenter = center;
            battlefieldRadius = radius;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(0f, radius * 0.9f);
            Position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            Velocity = Main.rand.NextVector2Circular(0.2f, 0.2f);
            Scale = Main.rand.NextFloat(3f, 6f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotationSpeed = Main.rand.NextFloat(-0.002f, 0.002f);
            PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi);

            // 阴暗的地府色调
            Color[] underworldColors = new Color[]
            {
                new Color(35, 45, 55),
                new Color(40, 50, 60),
                new Color(45, 55, 65),
                new Color(30, 40, 50),
            };
            baseColor = underworldColors[Main.rand.Next(underworldColors.Length)];
        }

        public void Update(NPC boss, Vector2 center) {
            battlefieldCenter = center;

            Position += Velocity;
            Rotation += RotationSpeed;
            PulsePhase += 0.01f;

            // 保持在战场范围内
            Vector2 toCenter = battlefieldCenter - Position;
            if (toCenter.Length() > battlefieldRadius * 0.8f) {
                Velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.05f;
            }

            if (Velocity.Length() > 0.5f) {
                Velocity = Vector2.Normalize(Velocity) * 0.5f;
            }

            // Boss 体积碰撞 - 雾气被推开 + 高速拖拽
            Vector2 toBoss = boss.Center - Position;
            float distanceToBoss = toBoss.Length();

            IsRepelledByBoss = false;

            if (distanceToBoss < BossRepelRadius) {
                IsRepelledByBoss = true;

                float repelStrength = (1f - distanceToBoss / BossRepelRadius) * BossRepelStrength;
                Vector2 repelDirection = -toBoss.SafeNormalize(Vector2.Zero);
                Velocity += repelDirection * repelStrength * 0.1f;

                float dragFactor = (1f - distanceToBoss / BossRepelRadius) * 0.2f;
                if (Vector2.Dot(boss.velocity, -repelDirection) > 0) {
                    Velocity += boss.velocity * dragFactor * 0.3f;
                }
            }
        }

        public Color GetColor() {
            if (IsRepelledByBoss) {
                return Color.Lerp(baseColor, new Color(80, 120, 180), 0.4f);
            }
            return baseColor;
        }

        public float GetAlpha() {
            float pulse = MathF.Sin(PulsePhase) * 0.15f + 0.85f;
            float baseAlpha = 0.6f;
            if (IsRepelledByBoss) {
                baseAlpha *= 0.7f;
            }
            return baseAlpha * pulse;
        }
    }

    /// <summary>
    /// 雾气涟漪 — Boss 冲击动作 (破门/吐息/换阶段) 的扩散环
    /// </summary>
    internal class FogRipple
    {
        public Vector2 Position;
        public float Scale;
        public float Rotation;
        public float Progress;
        public float Strength;
        public bool IsDead => Progress >= 1f;

        private const float MaxScale = 5f;
        private const float ExpandSpeed = 0.012f;

        public FogRipple(Vector2 position, float strength) {
            Position = position;
            Scale = 0.5f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Progress = 0f;
            Strength = Math.Clamp(strength, 0.5f, 3f);
        }

        public void Update() {
            Progress += ExpandSpeed * Strength;
            Scale = MathHelper.Lerp(0.5f, MaxScale, Progress);
            Rotation += 0.008f;
        }

        public float GetAlpha() {
            return MathF.Sin(Progress * MathHelper.Pi) * Strength * 0.5f;
        }
    }
}

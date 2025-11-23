using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙战斗雾气系统 - 环境雾气 + Boss体积交互
    /// </summary>
    internal class NetherDragonFogSystem : ModSystem
    {
        private static bool isActive = false;
        private static float intensity = 0f;
        private static int bossNPCIndex = -1;

        // 环境雾气层（大范围、缓慢移动的背景雾）
        private static readonly List<AmbientFogLayer> ambientFogs = new();
        private const int MaxAmbientFogs = 60;

        // Boss扰动产生的动态雾气涡流
        private static readonly List<FogVortex> vortexes = new();
        private const int MaxVortexes = 30;

        // 雾气涟漪效果（Boss冲刺等动作）
        private static readonly List<FogRipple> ripples = new();
        private const int MaxRipples = 15;

        // 时间计数器
        private static float globalTimer = 0f;

        // Boss相关参数
        private static Vector2 bossLastPosition = Vector2.Zero;
        private static Vector2 bossVelocity = Vector2.Zero;

        // 战场中心和范围
        private static Vector2 battlefieldCenter = Vector2.Zero;
        private static float battlefieldRadius = 1200f;

        public static bool IsActive => isActive;

        /// <summary>
        /// 激活雾气效果
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

            // 初始化环境雾气
            ambientFogs.Clear();
            for (int i = 0; i < MaxAmbientFogs; i++) {
                ambientFogs.Add(new AmbientFogLayer(battlefieldCenter, battlefieldRadius));
            }

            vortexes.Clear();
            ripples.Clear();
        }

        /// <summary>
        /// 停用雾气效果
        /// </summary>
        public static void Deactivate() {
            isActive = false;
            bossNPCIndex = -1;
        }

        /// <summary>
        /// 创建雾气涟漪（仅在Boss特殊攻击时使用）
        /// </summary>
        public static void CreateRipple(Vector2 position, float strength = 1f) {
            if (ripples.Count < MaxRipples) {
                ripples.Add(new FogRipple(position, strength));
            }
        }

        /// <summary>
        /// 创建雾气涡流（Boss快速移动时产生）
        /// </summary>
        private static void CreateVortex(Vector2 position, Vector2 direction, float strength) {
            if (vortexes.Count < MaxVortexes) {
                vortexes.Add(new FogVortex(position, direction, strength));
            }
        }

        public override void PostUpdateEverything() {
            if (!isActive) {
                if (intensity > 0f) {
                    intensity -= 0.01f;
                    if (intensity <= 0f) {
                        intensity = 0f;
                        ambientFogs.Clear();
                        vortexes.Clear();
                        ripples.Clear();
                    }
                }
                return;
            }

            // 检查Boss是否存活
            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs ||
                !Main.npc[bossNPCIndex].active || Main.npc[bossNPCIndex].type != ModContent.NPCType<NetherDragonHead>()) {
                Deactivate();
                return;
            }

            NPC boss = Main.npc[bossNPCIndex];

            // 强度渐变
            if (intensity < 1f) {
                intensity = Math.Min(intensity + 0.008f, 1f);
            }

            // 更新全局计时器
            globalTimer += 0.016f;
            if (globalTimer > MathHelper.TwoPi * 10f) {
                globalTimer -= MathHelper.TwoPi * 10f;
            }

            // 更新战场中心（跟随Boss缓慢移动）
            battlefieldCenter = Vector2.Lerp(battlefieldCenter, boss.Center, 0.01f);

            // 计算Boss速度
            bossVelocity = boss.Center - bossLastPosition;
            float bossSpeed = bossVelocity.Length();

            // Boss快速移动时创建体积雾气扰动
            if (bossSpeed > 5f && Main.rand.NextBool(2)) {
                // 在Boss后方创建涡流
                Vector2 vortexPos = boss.Center - bossVelocity.SafeNormalize(Vector2.Zero) * 80f;
                CreateVortex(vortexPos, bossVelocity, bossSpeed / 15f);
            }

            bossLastPosition = boss.Center;

            // 更新环境雾气
            foreach (var fog in ambientFogs) {
                fog.Update(boss, battlefieldCenter, globalTimer);
            }

            // 更新涡流
            for (int i = vortexes.Count - 1; i >= 0; i--) {
                vortexes[i].Update();
                if (vortexes[i].IsDead) {
                    vortexes.RemoveAt(i);
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
            if (Main.gameMenu || intensity <= 0.01f || Underworld.Fog == null)
                return;

            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs || !Main.npc[bossNPCIndex].active)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 先绘制环境雾气（背景层）
            DrawAmbientFogs(spriteBatch);

            // 再绘制涡流效果（中层）
            DrawVortexes(spriteBatch);

            // 最后绘制涟漪效果（前景层）
            DrawRipples(spriteBatch);

            spriteBatch.End();
        }

        private static void DrawAmbientFogs(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var fog in ambientFogs) {
                Vector2 drawPos = fog.Position - Main.screenPosition;

                // 屏幕剔除
                if (drawPos.X < -500 || drawPos.X > Main.screenWidth + 500 ||
                    drawPos.Y < -500 || drawPos.Y > Main.screenHeight + 500)
                    continue;

                Color fogColor = fog.GetColor();
                float alpha = fog.GetAlpha() * intensity;

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

                // 柔和光晕层
                sb.Draw(
                    fogTex,
                    drawPos,
                    null,
                    fogColor * alpha * 0.3f,
                    fog.Rotation * 0.7f,
                    fogTex.Size() * 0.5f,
                    fog.Scale * 1.4f * Main.GameViewMatrix.Zoom.X,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void DrawVortexes(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var vortex in vortexes) {
                Vector2 drawPos = vortex.Position - Main.screenPosition;

                Color vortexColor = new Color(80, 120, 180);
                float alpha = vortex.GetAlpha() * intensity;

                // 绘制旋转的涡流
                for (int i = 0; i < 2; i++) {
                    float rotOffset = i * MathHelper.Pi;
                    sb.Draw(
                        fogTex,
                        drawPos,
                        null,
                        vortexColor * alpha * 0.5f,
                        vortex.Rotation + rotOffset,
                        fogTex.Size() * 0.5f,
                        vortex.Scale * (1f + i * 0.3f) * Main.GameViewMatrix.Zoom.X,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private static void DrawRipples(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;

            foreach (var ripple in ripples) {
                Vector2 drawPos = ripple.Position - Main.screenPosition;

                Color rippleColor = new Color(100, 150, 220);
                float alpha = ripple.GetAlpha() * intensity;

                // 绘制多层涟漪环
                for (int i = 0; i < 3; i++) {
                    float scaleOffset = i * 0.4f;
                    float alphaOffset = 1f - i * 0.25f;

                    sb.Draw(
                        fogTex,
                        drawPos,
                        null,
                        rippleColor * alpha * alphaOffset * 0.5f,
                        ripple.Rotation + i * 0.3f,
                        fogTex.Size() * 0.5f,
                        ripple.Scale * (1f + scaleOffset) * Main.GameViewMatrix.Zoom.X,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        /// <summary>
        /// 获取指定位置的雾气密度（用于AI视觉效果）
        /// </summary>
        public static float GetFogDensityAt(Vector2 position) {
            if (!isActive || intensity <= 0f)
                return 0f;

            // 基于距离战场中心的距离计算基础雾气密度
            float distanceFromCenter = Vector2.Distance(position, battlefieldCenter);
            float baseDensity = 1f - Math.Clamp(distanceFromCenter / battlefieldRadius, 0f, 1f);

            // 叠加附近雾层的影响
            float layerDensity = 0f;
            int nearbyCount = 0;

            foreach (var fog in ambientFogs) {
                float distance = Vector2.Distance(fog.Position, position);
                if (distance < 300f) {
                    nearbyCount++;
                    layerDensity += (1f - distance / 300f) * fog.GetAlpha();
                }
            }

            if (nearbyCount > 0)
                layerDensity /= nearbyCount;

            return Math.Min((baseDensity * 0.6f + layerDensity * 0.4f) * intensity, 1f);
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

        private Color baseColor;
        private Vector2 originalPosition;
        private Vector2 battlefieldCenter;
        private float battlefieldRadius;

        // Boss碰撞参数
        private const float BossRepelRadius = 180f;
        private const float BossRepelStrength = 3f;

        public AmbientFogLayer(Vector2 center, float radius) {
            battlefieldCenter = center;
            battlefieldRadius = radius;

            // 在战场范围内随机分布
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(0f, radius * 0.9f);
            Position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            originalPosition = Position;

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

        public void Update(NPC boss, Vector2 center, float timer) {
            battlefieldCenter = center;

            // 基础缓慢漂移
            Position += Velocity;
            Rotation += RotationSpeed;
            PulsePhase += 0.01f;

            // 保持在战场范围内
            Vector2 toCenter = battlefieldCenter - Position;
            float distanceFromCenter = toCenter.Length();
            if (distanceFromCenter > battlefieldRadius * 0.8f) {
                Velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.05f;
            }

            // 限制速度
            if (Velocity.Length() > 0.5f) {
                Velocity = Vector2.Normalize(Velocity) * 0.5f;
            }

            // Boss体积碰撞 - 雾气被推开
            Vector2 toBoss = boss.Center - Position;
            float distanceToBoss = toBoss.Length();

            IsRepelledByBoss = false;

            if (distanceToBoss < BossRepelRadius) {
                IsRepelledByBoss = true;

                // 计算排斥力
                float repelStrength = (1f - distanceToBoss / BossRepelRadius) * BossRepelStrength;
                Vector2 repelDirection = -toBoss.SafeNormalize(Vector2.Zero);

                // 应用排斥力
                Velocity += repelDirection * repelStrength * 0.1f;

                // Boss运动产生的拖拽效果
                Vector2 bossVelocity = boss.velocity;
                float dragFactor = (1f - distanceToBoss / BossRepelRadius) * 0.2f;

                // 只有当Boss远离雾气时才拖拽
                if (Vector2.Dot(bossVelocity, -repelDirection) > 0) {
                    Velocity += bossVelocity * dragFactor * 0.3f;
                }
            }
        }

        public Color GetColor() {
            if (IsRepelledByBoss) {
                // 被Boss扰动时颜色变蓝
                return Color.Lerp(baseColor, new Color(80, 120, 180), 0.4f);
            }
            return baseColor;
        }

        public float GetAlpha() {
            float pulse = MathF.Sin(PulsePhase) * 0.15f + 0.85f;
            float baseAlpha = 0.6f;

            // 被Boss排斥时透明度降低
            if (IsRepelledByBoss) {
                baseAlpha *= 0.7f;
            }

            return baseAlpha * pulse;
        }
    }

    /// <summary>
    /// 雾气涡流 - Boss快速移动产生的扰动
    /// </summary>
    internal class FogVortex
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Progress;
        public float Strength;
        public bool IsDead => Progress >= 1f;

        private const float MaxScale = 2.5f;
        private const float LifeTime = 0.012f;
        private const float RotationSpeed = 0.08f;

        public FogVortex(Vector2 position, Vector2 direction, float strength) {
            Position = position;
            Velocity = direction.SafeNormalize(Vector2.Zero) * 0.5f;
            Scale = 0.8f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Progress = 0f;
            Strength = Math.Clamp(strength, 0.5f, 2f);
        }

        public void Update() {
            Progress += LifeTime * Strength;
            Position += Velocity;
            Velocity *= 0.95f; // 逐渐减速

            Scale = MathHelper.Lerp(0.8f, MaxScale, Progress);
            Rotation += RotationSpeed * Strength;
        }

        public float GetAlpha() {
            return MathF.Sin(Progress * MathHelper.Pi) * Strength * 0.4f;
        }
    }

    /// <summary>
    /// 雾气涟漪效果 - 特殊攻击时产生
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
            Strength = Math.Clamp(strength, 0.5f, 2f);
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

using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥妖狐战斗迷雾系统 —— V3「雾祟」核心机制载体。
    ///
    /// 职责:
    ///  1. 冥雾呼吸通道: Boss AI 每帧 <see cref="PublishMist"/> 目标密度, 本系统平滑 + 脉冲衰减;
    ///     浓雾=蓄势 / 骤清=爆发, 全屏 Mist 后处理由 Boss.PostDraw 读 <see cref="MistDensity"/> 绘制。
    ///  2. 雾中狐眼池: <see cref="SpawnEye"/> 生成 SDF 狐眼 telegraph (FadeIn→凝视(可眨眼)→瞳缩→白闪→散),
    ///     瞳缩时长即扑袭倒计时, 眨眼即真身读数线索。纯客户端视觉, 时序由各端同步的 AI 状态推导。
    ///  3. 魂焰批量绘制: <see cref="DrawSoulflameBatch"/> 程序化撕裂鬼火 (尾尖火/怨火/死亡九火通用)。
    ///  4. 保留 V2 通道: 魂火泛光 (RadialBloom) 与法阵预警 (ArenaRunic) 发布绘制、雾精灵/鬼火/涟漪氛围层。
    /// </summary>
    internal class NetherKitsuneFogSystem : ModSystem
    {
        private static bool isActive = false;
        private static float intensity = 0f;
        private static int bossNPCIndex = -1;

        // 幽魂迷雾层 (近景视差氛围, Mist 后处理接管主体积感后调低)
        private static readonly List<SoulFogLayer> soulFogs = new();
        private const int MaxSoulFogs = 40;

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

        // ===== 专属着色器缓存 (ps_3_0, 不注册 ACMShaders; 参考 Xuanwu 写法) =====
        private static Asset<Effect> _mistFx;
        private static Asset<Effect> _eyeFx;
        private static Asset<Effect> _flameFx;

        /// <summary>冥雾全屏后处理 (s0=screenTarget, s1=共享噪声)。</summary>
        public static Effect MistFx => GetFx(ref _mistFx, "NetherKitsuneMist");
        /// <summary>雾中狐眼 SDF telegraph。</summary>
        public static Effect EyeFx => GetFx(ref _eyeFx, "NetherKitsuneEye");
        /// <summary>程序化撕裂魂焰 sprite。</summary>
        public static Effect SoulflameFx => GetFx(ref _flameFx, "NetherKitsuneSoulflame");

        private static Effect GetFx(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/" + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        // ===== 冥雾呼吸通道 (AI 每帧发布, 本系统平滑) =====
        private static float mistDensity;       // 平滑后的当前密度
        private static float mistTarget;        // AI 发布的目标密度
        private static float mistPulse;         // 事件脉冲 (加法, ×0.9/f 衰减; 可为负=骤清)
        private static float mistGhost;         // 0=冥蓝 1=鬼绿 (平滑)
        private static float mistGhostTarget;
        private static float mistFreeze;        // 雾冻结 (死亡顿帧, 直接设置)
        private static Vector2 mistWind;

        /// <summary>当前冥雾可见密度 (含脉冲, 供 Boss.PostDraw 的 Mist 后处理读取)。</summary>
        public static float MistDensity => MathHelper.Clamp(mistDensity + mistPulse, 0f, 1.15f) * intensity;
        public static float MistGhost => mistGhost;
        public static float MistFreeze => mistFreeze;
        public static Vector2 MistWind => mistWind;

        /// <summary>由 Boss AI 每帧发布冥雾状态 (目标密度 / 鬼绿度 / 冻结 / 风向)。</summary>
        public static void PublishMist(float targetDensity, float ghost, float freeze, Vector2 wind) {
            mistTarget = MathHelper.Clamp(targetDensity, 0f, 1.15f);
            mistGhostTarget = MathHelper.Clamp(ghost, 0f, 1f);
            mistFreeze = MathHelper.Clamp(freeze, 0f, 1f);
            mistWind = wind;
        }

        /// <summary>冥雾瞬时脉冲: 正=涌浓 (蓄势), 负=骤清 (爆发拍呼气)。</summary>
        public static void MistPulseAdd(float delta) {
            mistPulse = MathHelper.Clamp(mistPulse + delta, -0.9f, 0.9f);
        }

        // ===== 死亡演出: 雾收束吸入 =====
        private static bool gatherMode;
        private static Vector2 gatherPoint;

        /// <summary>开/关雾收束模式 (死亡演出: 全场雾被吸入狐身)。</summary>
        public static void SetGather(bool on, Vector2 point) {
            gatherMode = on;
            gatherPoint = point;
        }

        // ===== V2 保留: 泛光 / 法阵发布通道 =====
        private static float soulBloom;
        private static Vector2 bloomCenter;
        private static Color bloomColor = new Color(130, 210, 255);
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

        // ===== 地面魂焰累积通道 (怨火地灾/散景鬼火 — 每帧由弹幕 AI 提交, PostDrawTiles 一批画完) =====
        private static readonly List<SoulflameSpec> groundFlames = new();
        private const int MaxGroundFlames = 48;

        /// <summary>提交一朵地面魂焰 (本帧有效, PostDrawTiles 统一绘制后清空)。</summary>
        public static void RequestGroundFlame(in SoulflameSpec spec) {
            if (Main.dedServ || groundFlames.Count >= MaxGroundFlames)
                return;
            groundFlames.Add(spec);
        }

        // ===== 雾中狐眼池 =====
        private static readonly List<FoxEye> eyes = new();
        private const int MaxEyes = 12;

        /// <summary>
        /// 生成一对雾中狐眼 (纯客户端视觉; 时序参数由各端一致的 AI 状态推导保证同步观感)。
        /// 生命周期: fadeIn → 凝视 stare (blinkAt≥0 时在该帧眨眼一次 = 真身线索) → 瞳缩 squint (扑袭倒计时)
        /// → 白闪 6f → 淡出 12f。widthPx 为眼对总宽 (世界像素)。
        /// </summary>
        public static void SpawnEye(Vector2 worldPos, int fadeIn, int stare, int squint, float widthPx, Color color, int blinkAt = -1) {
            if (Main.dedServ || eyes.Count >= MaxEyes)
                return;
            eyes.Add(new FoxEye(worldPos, fadeIn, stare, squint, widthPx, color, blinkAt));
        }

        /// <summary>立即清除所有狐眼 (中断/换拍)。</summary>
        public static void ClearEyes() => eyes.Clear();

        /// <summary>
        /// 激活迷雾效果
        /// </summary>
        public static void Activate(int bossIndex) {
            if (bossIndex < 0 || bossIndex >= Main.maxNPCs)
                return;

            isActive = true;
            bossNPCIndex = bossIndex;
            intensity = 0f;
            mistDensity = 0f;
            mistTarget = 0f;
            mistPulse = 0f;
            mistGhost = 0f;
            mistGhostTarget = 0f;
            mistFreeze = 0f;
            gatherMode = false;

            NPC boss = Main.npc[bossIndex];
            battlefieldCenter = boss.Center;
            bossLastPosition = boss.Center;

            soulFogs.Clear();
            for (int i = 0; i < MaxSoulFogs; i++) {
                soulFogs.Add(new SoulFogLayer(battlefieldCenter, battlefieldRadius));
            }

            wisps.Clear();
            ripples.Clear();
            eyes.Clear();
        }

        /// <summary>
        /// 停用迷雾效果
        /// </summary>
        public static void Deactivate() {
            isActive = false;
            bossNPCIndex = -1;
            soulBloom = 0f;
            runic = 0f;
            mistTarget = 0f;
            gatherMode = false;
            eyes.Clear();
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

        public override void Unload() {
            _mistFx = null;
            _eyeFx = null;
            _flameFx = null;
        }

        public override void PostUpdateEverything() {
            // 雾密度/换色平滑 (无论激活与否都推进, 保证战后余雾自然散尽)
            float rate = mistTarget > mistDensity ? 0.045f : 0.06f;
            mistDensity = MathHelper.Lerp(mistDensity, mistTarget, rate);
            mistPulse *= 0.90f;
            mistGhost = MathHelper.Lerp(mistGhost, mistGhostTarget, 0.05f);

            if (!isActive) {
                if (intensity > 0f) {
                    intensity -= 0.015f;
                    if (intensity <= 0f) {
                        intensity = 0f;
                        soulFogs.Clear();
                        wisps.Clear();
                        ripples.Clear();
                        eyes.Clear();
                    }
                }
                UpdateEyes();
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
                intensity = Math.Min(intensity + 0.008f, 1f);
            }

            // 雾冻结时氛围粒子也滞住 (死亡顿帧)
            float timeScale = 1f - mistFreeze * 0.95f;
            globalTimer += 0.016f * timeScale;
            if (globalTimer > MathHelper.TwoPi * 10f) {
                globalTimer -= MathHelper.TwoPi * 10f;
            }

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

            // 更新幽魂迷雾 (收束模式: 全部吸向 gatherPoint)
            foreach (var fog in soulFogs) {
                if (gatherMode) {
                    Vector2 pull = (gatherPoint - fog.Position);
                    fog.Velocity += pull.SafeNormalize(Vector2.Zero) * 0.5f;
                    if (fog.Velocity.Length() > 14f)
                        fog.Velocity = fog.Velocity.SafeNormalize(Vector2.Zero) * 14f;
                    fog.Position += fog.Velocity;
                    fog.Scale = MathF.Max(0.3f, fog.Scale - 0.03f);
                }
                else {
                    fog.Update(boss, battlefieldCenter, globalTimer);
                }
            }

            for (int i = wisps.Count - 1; i >= 0; i--) {
                if (gatherMode) {
                    wisps[i].Velocity += (gatherPoint - wisps[i].Position).SafeNormalize(Vector2.Zero) * 0.8f;
                }
                wisps[i].Update();
                if (wisps[i].IsDead) {
                    wisps.RemoveAt(i);
                }
            }

            for (int i = ripples.Count - 1; i >= 0; i--) {
                ripples[i].Update();
                if (ripples[i].IsDead) {
                    ripples.RemoveAt(i);
                }
            }

            UpdateEyes();
        }

        private static void UpdateEyes() {
            for (int i = eyes.Count - 1; i >= 0; i--) {
                eyes[i].Update();
                if (eyes[i].IsDead)
                    eyes.RemoveAt(i);
            }
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu)
                return;

            bool bossAlive = bossNPCIndex >= 0 && bossNPCIndex < Main.maxNPCs && Main.npc[bossNPCIndex].active;
            if (!bossAlive && intensity <= 0.01f && eyes.Count == 0)
                return;

            // 迷雾精灵层 (近景视差; Mist 后处理接管主体积感, 此层调低)
            if (Underworld.Fog != null && intensity > 0.01f) {
                SpriteBatch spriteBatch = Main.spriteBatch;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                DrawSoulFogs(spriteBatch);
                DrawWisps(spriteBatch);
                DrawRipples(spriteBatch);

                spriteBatch.End();
            }

            // 演出层 (各自管理批次): 地面魂焰 → 法阵预警 → 狐眼 → 魂火泛光
            DrawGroundFlames();
            DrawArenaRunic();
            DrawEyes();
            DrawSoulBloom();
        }

        // ===== 地面魂焰 (自开合批; 无活动批阶段专用) =====
        private static void DrawGroundFlames() {
            if (groundFlames.Count == 0)
                return;
            Effect fx = SoulflameFx;
            Texture2D carrier = ACMShaders.NoiseTexture;
            if (fx == null || carrier == null) {
                groundFlames.Clear();
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uStretch"]?.SetValue(1f);
            for (int i = 0; i < groundFlames.Count; i++) {
                SoulflameSpec s = groundFlames[i];
                if (s.Intensity <= 0.02f)
                    continue;
                fx.Parameters["uSeed"]?.SetValue(s.Seed);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(s.Intensity, 0f, 1f));
                fx.Parameters["uGhost"]?.SetValue(MathHelper.Clamp(s.Ghost, 0f, 1f));
                fx.Parameters["uCoreColor"]?.SetValue(s.Core.ToVector4());
                fx.Parameters["uEdgeColor"]?.SetValue(s.Edge.ToVector4());
                fx.CurrentTechnique.Passes[0].Apply();

                Vector2 scale = new Vector2(s.WidthPx / carrier.Width, s.HeightPx / carrier.Height);
                sb.Draw(carrier, s.WorldPos - Main.screenPosition, null, Color.White,
                    s.Rotation, new Vector2(carrier.Width * 0.5f, carrier.Height), scale, SpriteEffects.None, 0f);
            }

            sb.End();
            groundFlames.Clear();
        }

        // ===== ArenaRunic 法阵预警 (九刺收口 / 真身锚 / 下砸落点) =====
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

        // ===== 雾中狐眼 (Immediate + Additive, 每对眼单独设参 Apply) =====
        private static void DrawEyes() {
            if (eyes.Count == 0)
                return;
            Effect fx = EyeFx;
            Texture2D carrier = ACMShaders.NoiseTexture;
            if (fx == null || carrier == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            foreach (var eye in eyes) {
                fx.Parameters["uOpen"]?.SetValue(eye.Open);
                fx.Parameters["uPupil"]?.SetValue(eye.Pupil);
                fx.Parameters["uGlow"]?.SetValue(eye.Glow);
                fx.Parameters["uFlash"]?.SetValue(eye.Flash);
                fx.Parameters["uColor"]?.SetValue(eye.IrisColor.ToVector4());
                fx.Parameters["uSeed"]?.SetValue(eye.Seed);
                fx.CurrentTechnique.Passes[0].Apply();

                Vector2 scale = new Vector2(eye.WidthPx / carrier.Width, eye.WidthPx * 0.46f / carrier.Height);
                sb.Draw(carrier, eye.WorldPos - Main.screenPosition, null, Color.White,
                    0f, carrier.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }

            sb.End();
        }

        // ===== RadialBloom 魂火泛光 (加性 overlay, 不占全屏 screenTarget 名额) =====
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

        // ===== 魂焰批量绘制 (尾尖火 / 怨火 / 死亡九火通用) =====

        /// <summary>单朵魂焰绘制规格。</summary>
        public struct SoulflameSpec
        {
            public Vector2 WorldPos;   // 焰根世界坐标
            public float WidthPx;      // 宽 (世界像素)
            public float HeightPx;     // 高 (世界像素)
            public float Intensity;    // 0~1
            public float Ghost;        // 0=冥蓝 1=鬼绿
            public float Rotation;     // 绕焰根旋转 (0=焰尖朝上)
            public float Seed;         // 相位差
            public Color Core;
            public Color Edge;
        }

        /// <summary>
        /// 批量绘制程序化魂焰 (一次开合批)。**须在已有活动批的阶段调用** (NPC/弹幕 PreDraw):
        /// 内部 End 当前批 → Immediate+Additive+Soulflame → 逐朵设参绘制 → 恢复默认批。
        /// </summary>
        public static void DrawSoulflameBatch(IReadOnlyList<SoulflameSpec> specs) {
            if (Main.dedServ || specs == null || specs.Count == 0)
                return;
            Effect fx = SoulflameFx;
            Texture2D carrier = ACMShaders.NoiseTexture;
            if (fx == null || carrier == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uStretch"]?.SetValue(1f);
            for (int i = 0; i < specs.Count; i++) {
                SoulflameSpec s = specs[i];
                if (s.Intensity <= 0.02f)
                    continue;
                fx.Parameters["uSeed"]?.SetValue(s.Seed);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(s.Intensity, 0f, 1f));
                fx.Parameters["uGhost"]?.SetValue(MathHelper.Clamp(s.Ghost, 0f, 1f));
                fx.Parameters["uCoreColor"]?.SetValue(s.Core.ToVector4());
                fx.Parameters["uEdgeColor"]?.SetValue(s.Edge.ToVector4());
                fx.CurrentTechnique.Passes[0].Apply();

                Vector2 scale = new Vector2(s.WidthPx / carrier.Width, s.HeightPx / carrier.Height);
                // origin = 底部中点 → 焰绕根部旋转, 焰尖朝 -rotation 方向
                sb.Draw(carrier, s.WorldPos - Main.screenPosition, null, Color.White,
                    s.Rotation, new Vector2(carrier.Width * 0.5f, carrier.Height), scale, SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        private static void DrawSoulFogs(SpriteBatch sb) {
            Texture2D fogTex = Underworld.Fog;
            // 换色: 冥蓝 <-> 鬼绿 (随 MistGhost)
            Color nearTint = Color.Lerp(new Color(80, 140, 200), new Color(70, 190, 120), mistGhost);

            foreach (var fog in soulFogs) {
                Vector2 drawPos = fog.Position - Main.screenPosition;

                if (drawPos.X < -400 || drawPos.X > Main.screenWidth + 400 ||
                    drawPos.Y < -400 || drawPos.Y > Main.screenHeight + 400)
                    continue;

                Color fogColor = fog.IsNearBoss ? Color.Lerp(fog.GetColor(), nearTint, 0.4f) : fog.GetColor();
                float alpha = fog.GetAlpha() * intensity * 0.7f;

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

                Color glowColor = Color.Lerp(new Color(100, 180, 255), new Color(110, 235, 155), mistGhost) * alpha * 0.18f;
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
            Color wispBase = Color.Lerp(new Color(80, 160, 220), new Color(90, 220, 140), mistGhost);
            Color wispBright = Color.Lerp(new Color(150, 200, 255), new Color(160, 250, 190), mistGhost);

            foreach (var wisp in wisps) {
                Vector2 drawPos = wisp.Position - Main.screenPosition;

                Color wispColor = Color.Lerp(wispBase, wispBright, wisp.GetPulse());
                float alpha = wisp.GetAlpha() * intensity;
                wispColor.A = 0;

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
    /// 雾中狐眼实例 —— FadeIn → 凝视(可眨眼=真身线索) → 瞳缩(扑袭倒计时) → 白闪 → 淡出。
    /// </summary>
    internal class FoxEye
    {
        public Vector2 WorldPos;
        public float WidthPx;
        public Color IrisColor;
        public float Seed;

        private readonly int fadeIn;
        private readonly int stare;
        private readonly int squint;
        private readonly int blinkAt;   // 凝视段内的眨眼起始帧 (-1 = 不眨)
        private const int BlinkLen = 12;
        private const int FlashLen = 6;
        private const int FadeOutLen = 12;

        private int timer;

        public FoxEye(Vector2 pos, int fadeIn, int stare, int squint, float widthPx, Color color, int blinkAt) {
            WorldPos = pos;
            this.fadeIn = Math.Max(1, fadeIn);
            this.stare = Math.Max(0, stare);
            this.squint = Math.Max(1, squint);
            this.blinkAt = blinkAt;
            WidthPx = widthPx;
            IrisColor = color;
            Seed = Main.rand.NextFloat(10f);
        }

        public bool IsDead => timer > fadeIn + stare + squint + FlashLen + FadeOutLen;

        public void Update() => timer++;

        /// <summary>眼睑开度 0~1。</summary>
        public float Open {
            get {
                if (timer < fadeIn)
                    return ACMUtils.QuadOut(timer / (float)fadeIn);
                int t = timer - fadeIn;
                // 凝视段: 眨眼一次 (合-开, 真身读数线索)
                if (t < stare) {
                    if (blinkAt >= 0 && t >= blinkAt && t < blinkAt + BlinkLen) {
                        float bt = (t - blinkAt) / (float)BlinkLen;
                        return MathF.Abs(bt * 2f - 1f); // 1→0→1
                    }
                    return 1f;
                }
                t -= stare;
                if (t < squint + FlashLen)
                    return 1f;
                t -= squint + FlashLen;
                return 1f - ACMUtils.Clamp01(t / (float)FadeOutLen);
            }
        }

        /// <summary>瞳形 1=圆瞳 → 0=竖线 (瞳缩段线性收缩 = 可内化的倒计时)。</summary>
        public float Pupil {
            get {
                int t = timer - fadeIn - stare;
                if (t <= 0)
                    return 1f;
                return 1f - ACMUtils.Clamp01(t / (float)squint);
            }
        }

        /// <summary>扑袭白闪 0~1。</summary>
        public float Flash {
            get {
                int t = timer - fadeIn - stare - squint;
                if (t < 0 || t >= FlashLen)
                    return 0f;
                return 1f - t / (float)FlashLen;
            }
        }

        public float Glow {
            get {
                int total = fadeIn + stare + squint + FlashLen + FadeOutLen;
                if (timer > total - FadeOutLen)
                    return 1f - ACMUtils.Clamp01((timer - (total - FadeOutLen)) / (float)FadeOutLen);
                return 1f;
            }
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
                float attractStrength = (1f - distanceToBoss / BossInfluenceRadius) * 0.8f;
                Velocity += toBoss.SafeNormalize(Vector2.Zero) * attractStrength * 0.05f;
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

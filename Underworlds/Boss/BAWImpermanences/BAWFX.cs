using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常 V3 演出层 (纯视觉, 服务端零绘制)。
    ///
    /// 专属着色器 (BAW 前缀, ps_3_0, 本类静态缓存, 不注册 ACMShaders):
    ///  1. BAWYinYangSplit —— 阴阳勾魂全屏分屏 (水墨波动缝 + 双域魂点), 走唯一全屏名额。
    ///     驱动: 双使各自按状态给出包络 (<see cref="BlackImpermanence.SplitDriveTarget"/> 等), 取 max;
    ///     协同分屏沿两使连线, 孤使/死亡节拍沿幸存者中心的缓旋轴。
    ///  2. BAWSoulFlame —— 世界空间程序魂火 quad (体表魂焰罩 / 引魂灯 / 死亡魂柱)。
    ///
    /// 复用共享件: DissolveBurn (魂凝/消散) 经 <see cref="DrawDissolveSprite"/>。
    /// </summary>
    public static class BAWFX
    {
        // —— 阴阳配色 (与双使发光取色一致) ——
        /// <summary>阴侧 (黑无常): 幽蓝紫。</summary>
        public static readonly Color YinColor = TelegraphColors.NetherViolet;
        /// <summary>阳侧 (白无常): 暖白幽光。</summary>
        public static readonly Color YangColor = new(255, 244, 220);

        /// <summary>黑无常溶解灼烧边: 紫魂。</summary>
        public static readonly Color BlackDissolveEdge = new(150, 110, 230);
        /// <summary>白无常溶解灼烧边: 青白魂。</summary>
        public static readonly Color WhiteDissolveEdge = new(205, 222, 255);

        // ===================================================================
        //  专属着色器缓存 (Xuanwu 写法: 惰性 ImmediateLoad, 静态 Asset)
        // ===================================================================

        private static Asset<Effect> _yinYangSplit;
        private static Asset<Effect> _soulFlame;

        private static Effect YinYangSplitFX {
            get {
                if (Main.dedServ) return null;
                _yinYangSplit ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BAWYinYangSplit", AssetRequestMode.ImmediateLoad);
                return _yinYangSplit?.Value;
            }
        }

        private static Effect SoulFlameFX {
            get {
                if (Main.dedServ) return null;
                _soulFlame ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BAWSoulFlame", AssetRequestMode.ImmediateLoad);
                return _soulFlame?.Value;
            }
        }

        // ===================================================================
        //  1) 魂魄消融 DissolveBurn (Boss 贴图单 pass, 共享着色器)
        // ===================================================================

        /// <summary>
        /// 用 DissolveBurn 着色器绘制一张 Boss 贴图 (出场/复活/孤使/死亡的"魂↔实体")。
        /// <paramref name="threshold"/> 0=完整实体, 1=完全消散为魂。
        /// 着色器缺失 / 服务端时返回 false, 调用方应回退到普通 <c>sb.Draw</c>。
        /// </summary>
        public static bool DrawDissolveSprite(SpriteBatch sb, Texture2D tex, Vector2 drawPos, Rectangle? frame,
            Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects,
            float threshold, Color edgeColor) {
            if (Main.dedServ || sb == null || tex == null)
                return false;

            Effect fx = ACMShaders.DissolveBurn;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return false;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(threshold, 0f, 1f));
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.5f);
            fx.Parameters["uEdgeColor"]?.SetValue(edgeColor.ToVector4());
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f)); // 自下而上消融
            fx.Parameters["uSweepStrength"]?.SetValue(0.25f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawPos, frame, color, rotation, origin, scale, effects, 0f);
            sb.End();

            ACMShaders.RestoreDefaultBatch(sb);
            return true;
        }

        // ===================================================================
        //  2) 阴阳勾魂分屏 (BAWYinYangSplit 全屏后处理)
        // ===================================================================

        private static float _intensity;        // 平滑后的当前分屏强度
        private static ulong _intensityFrame;   // 强度推进帧守卫 (双使同帧两次 PostDraw 只推进一次)

        /// <summary>
        /// 阴阳分屏总入口。两使的任一 <c>PostDraw</c> 每帧调用即可 —— 内部帧守卫去重, 经
        /// <see cref="ACMShaders.RequestFullscreenSlot"/> 保证每帧仅一层全屏后处理。
        /// 目标强度 = 双使包络 max, 并随怨念账 (<see cref="UnderworldField"/>) 上探。
        /// </summary>
        public static void DrawYinYangSplit(SpriteBatch sb) {
            if (Main.dedServ || sb == null || Main.gameMenu)
                return;

            NPC black = FindTwin(ModContent.NPCType<BlackImpermanence>());
            NPC white = FindTwin(ModContent.NPCType<WhiteImpermanence>());
            if (black == null && white == null)
                return;

            // 同帧只推进一次强度 (避免双使各推一次导致速度翻倍)
            if (_intensityFrame != Main.GameUpdateCount) {
                _intensityFrame = Main.GameUpdateCount;
                _intensity = MathHelper.Lerp(_intensity, TargetIntensity(black, white), 0.08f);
            }

            if (_intensity < 0.01f)
                return;

            if (!ACMShaders.RequestFullscreenSlot())
                return; // 本帧名额已被占用 / 全屏 shader 被配置关闭

            Effect fx = YinYangSplitFX;
            if (fx == null)
                return;

            // 分屏轴: 双使在场沿连线; 仅一使 (孤使/死亡演出) 用穿过其中心的缓旋轴
            Vector2 mid;
            Vector2 dir;
            if (black != null && white != null) {
                mid = (black.Center + white.Center) * 0.5f;
                dir = white.Center - black.Center;
                if (dir.LengthSquared() < 1f)
                    dir = Vector2.UnitX;
                dir.Normalize();
            }
            else {
                NPC solo = black ?? white;
                mid = solo.Center;
                dir = (Main.GlobalTimeWrappedHourly * 0.35f).ToRotationVector2();
                if (black == null)
                    dir = -dir; // 白孤使: 阳域朝向翻转, 使其身侧恒为阳
            }

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            Vector2 midUV = (mid - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            // aspect 空间方向 ∝ 屏幕方向, 故 uSplitDir 直接用归一化屏幕方向
            float proj = midUV.X * aspect * dir.X + midUV.Y * dir.Y;
            float splitPos = proj / ((1f + aspect) * 0.5f);

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(_intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uSplitDir"]?.SetValue(dir);
            fx.Parameters["uSplitPos"]?.SetValue(splitPos);
            fx.Parameters["uYinColor"]?.SetValue(new Vector4(YinColor.ToVector3(), 1f));
            fx.Parameters["uYangColor"]?.SetValue(new Vector4(YangColor.ToVector3(), 1f));

            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: true);
        }

        /// <summary>双使包络 max + 怨念账上探 (0~0.85 封顶)。</summary>
        private static float TargetIntensity(NPC black, NPC white) {
            float t = 0f;
            if (black?.ModNPC is BlackImpermanence b)
                t = Math.Max(t, b.SplitDriveTarget);
            if (white?.ModNPC is WhiteImpermanence w)
                t = Math.Max(t, w.SplitDriveTarget);
            if (t <= 0f)
                return 0f;

            float grudge = Math.Max(
                black != null ? UnderworldField.GetGrudgeNormalized(black) : 0f,
                white != null ? UnderworldField.GetGrudgeNormalized(white) : 0f);
            return MathHelper.Clamp(t + grudge * 0.15f, 0f, 0.85f);
        }

        private static NPC FindTwin(int type) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n != null && n.active && n.type == type)
                    return n;
            }
            return null;
        }

        // ===================================================================
        //  3) 程序魂火 quad (BAWSoulFlame)
        // ===================================================================

        /// <summary>
        /// 在世界坐标画一团程序魂火 (焰尖朝 <paramref name="rotation"/>=0 时的正上方)。
        /// 内部 End→Begin(Immediate, Additive)→恢复默认批; 调用点每帧 ≤8 以控开合批成本。
        /// 着色器缺失时静默跳过 (调用方通常另有 CPU 光层兜底)。
        /// </summary>
        /// <param name="size">quad 尺寸(像素, 宽×高)。</param>
        /// <param name="stretch">纵向拉伸 (1=圆焰, 2~3=焰柱)。</param>
        public static void DrawSoulFlame(SpriteBatch sb, Vector2 worldCenter, Vector2 size,
            Color coreColor, Color edgeColor, float seed, float intensity, float rotation = 0f, float stretch = 1f) {
            if (Main.dedServ || sb == null || intensity <= 0.01f)
                return;

            Effect fx = SoulFlameFX;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uCoreColor"]?.SetValue(coreColor.ToVector4());
            fx.Parameters["uEdgeColor"]?.SetValue(edgeColor.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uStretch"]?.SetValue(stretch);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(noise, worldCenter - Main.screenPosition, null, Color.White, rotation,
                noise.Size() * 0.5f, size / noise.Size(), SpriteEffects.None, 0f);
            sb.End();

            ACMShaders.RestoreDefaultBatch(sb);
        }

        // ===================================================================
        //  4) 战场辅助
        // ===================================================================

        /// <summary>
        /// 清除双使名下全部敌对弹幕 (换阶段/演出的公平阀门)。服务端权威, 客户端由同步收尾。
        /// </summary>
        public static void ClearBAWProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int t1 = ModContent.ProjectileType<ChainProjectile>();
            int t2 = ModContent.ProjectileType<ChainSweepProjectile>();
            int t3 = ModContent.ProjectileType<ChainPullProjectile>();
            int t4 = ModContent.ProjectileType<SoulChainProjectile>();
            int t5 = ModContent.ProjectileType<GhostProjectile>();
            int t6 = ModContent.ProjectileType<SpiritCircleProjectile>();
            int t7 = ModContent.ProjectileType<GhostWaveProjectile>();
            int t8 = ModContent.ProjectileType<SoulDrainProjectile>();

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p == null || !p.active || !p.hostile)
                    continue;
                int t = p.type;
                if (t == t1 || t == t2 || t == t3 || t == t4 || t == t5 || t == t6 || t == t7 || t == t8)
                    p.Kill();
            }
        }
    }
}

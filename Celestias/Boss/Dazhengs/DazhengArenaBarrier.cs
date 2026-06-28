using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿Boss限制圈弹幕
    /// 在Boss周围生成一个圆形自然屏障，限制玩家战斗区域
    /// 使用自定义着色器 + 噪声纹理渲染动态藤蔓纹路
    /// </summary>
    public class DazhengArenaBarrier : ModProjectile
    {
        #region 常量

        /// <summary>一阶段战场半径（世界像素）</summary>
        public const float Phase1Radius = 1500f;
        /// <summary>二阶段战场半径（收缩）</summary>
        public const float Phase2Radius = 1200f;
        /// <summary>界外伤害间隔（帧）</summary>
        private const int DamageInterval = 30;
        /// <summary>推力开始生效的半径百分比</summary>
        private const float PushStartPercent = 0.92f;

        // 着色器颜色
        private static readonly Vector4 ColorPrimary = new(0.10f, 0.35f, 0.12f, 1f);   // 深林绿
        private static readonly Vector4 ColorSecondary = new(0.78f, 0.68f, 0.20f, 1f);  // 古金色

        #endregion

        #region 静态资源

        private static Texture2D noiseTexture;
        private static Asset<Effect> arenaEffect;

        #endregion

        #region 实例状态

        /// <summary>关联的Boss NPC索引</summary>
        private int BossIndex => (int)Projectile.ai[0];
        /// <summary>目标半径（用于平滑过渡）</summary>
        private float TargetRadius { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }

        private float currentRadius;
        private float fadeProgress;
        private float animTime;
        private int damageTimer;

        // 季节换色 (向当前主导季节平滑过渡)
        private Vector4 curPrimary = ColorPrimary;
        private Vector4 curSecondary = ColorSecondary;

        #endregion

        #region ModProjectile 重写

        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.damage = 0;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 10;
            Projectile.alpha = 255;
            Projectile.hide = true;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCs.Add(index);
        }

        public override void Unload() {
            noiseTexture?.Dispose();
            noiseTexture = null;
            arenaEffect = null;
        }

        #endregion

        #region AI

        public override void AI() {
            // 检查Boss是否存活
            if (!IsBossAlive()) {
                Projectile.Kill();
                return;
            }

            NPC boss = Main.npc[BossIndex];
            Projectile.Center = boss.Center;
            Projectile.timeLeft = 10;

            // 平滑半径过渡
            if (currentRadius <= 0)
                currentRadius = TargetRadius;
            currentRadius = MathHelper.Lerp(currentRadius, TargetRadius, 0.025f);

            // 淡入
            fadeProgress = MathHelper.Clamp(fadeProgress + 0.015f, 0f, 1f);

            // 动画时间
            animTime += 1f / 30f;

            // 季节换色: 屏障主/辅色向当前主导季节平滑过渡
            if (boss.ModNPC is Dazheng dz) {
                int s = dz.CurrentSeason;
                Vector4 tgtP = new(DazhengSeasons.Tint(s).ToVector3() * 0.45f, 1f);
                Vector4 tgtS = new(DazhengSeasons.Accent(s).ToVector3() * 0.85f, 1f);
                curPrimary = Vector4.Lerp(curPrimary, tgtP, 0.02f);
                curSecondary = Vector4.Lerp(curSecondary, tgtS, 0.02f);
            }

            // 服务端：界外伤害
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                damageTimer++;
                if (damageTimer >= DamageInterval) {
                    damageTimer = 0;
                    ApplyOutOfBoundsDamage();
                }
            }

            // 客户端：推力 + 边缘粒子
            if (Main.netMode != NetmodeID.Server) {
                ApplyPushForce();
                SpawnEdgeParticles();
            }

            // 光照
            float glow = 0.4f + MathF.Sin(animTime * 2f) * 0.1f;
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi / 8 * i + animTime * 0.3f;
                Vector2 lightPos = Projectile.Center + new Vector2(
                    MathF.Cos(angle) * currentRadius,
                    MathF.Sin(angle) * currentRadius);
                Lighting.AddLight(lightPos, new Vector3(0.1f, 0.25f, 0.08f) * glow);
            }
        }

        private bool IsBossAlive() {
            int idx = BossIndex;
            return idx >= 0 && idx < Main.maxNPCs &&
                   Main.npc[idx].active &&
                   Main.npc[idx].type == ModContent.NPCType<Dazheng>();
        }

        private void ApplyOutOfBoundsDamage() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                float dist = Vector2.Distance(p.Center, Projectile.Center);
                if (dist > currentRadius) {
                    int dmg = 50;
                    if (Main.expertMode) dmg = 75;
                    if (Main.masterMode) dmg = 100;

                    p.Hurt(PlayerDeathReason.ByCustomReason(
                        Terraria.Localization.NetworkText.FromLiteral(
                            p.name + " 被大椿的自然之力吞噬了")), dmg, 0);
                }
            }
        }

        private void ApplyPushForce() {
            Player local = Main.LocalPlayer;
            if (!local.active || local.dead) return;

            float dist = Vector2.Distance(local.Center, Projectile.Center);
            float warnDist = currentRadius * PushStartPercent;

            if (dist > warnDist) {
                Vector2 pushDir = (Projectile.Center - local.Center).SafeNormalize(Vector2.Zero);
                float excess = (dist - warnDist) / (currentRadius * (1f - PushStartPercent));
                excess = MathHelper.Clamp(excess, 0f, 3f);
                float strength = excess * excess * 0.5f;
                local.velocity += pushDir * strength;
            }
        }

        private void SpawnEdgeParticles() {
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = currentRadius + Main.rand.NextFloat(-40f, 40f);
                Vector2 pos = Projectile.Center + new Vector2(
                    MathF.Cos(angle), MathF.Sin(angle)) * radius;

                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.JungleGrass,
                    MathF.Cos(angle + MathHelper.PiOver2) * 1.5f,
                    MathF.Sin(angle + MathHelper.PiOver2) * 1.5f,
                    120, default, 1.6f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            // 偶尔的金色亮点
            if (Main.rand.NextBool(8)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + new Vector2(
                    MathF.Cos(angle), MathF.Sin(angle)) * currentRadius;

                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.GoldFlame,
                    0, -1f, 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || fadeProgress <= 0.01f)
                return false;

            EnsureNoiseTexture();
            Effect effect = GetEffect();
            if (effect == null || noiseTexture == null)
                return false;

            SpriteBatch sb = Main.spriteBatch;

            // 计算屏幕空间参数
            Vector2 worldOffset = Projectile.Center - Main.screenPosition;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float zoom = Main.GameViewMatrix.Zoom.X;

            Vector2 screenPos = (worldOffset - halfScreen) * zoom + halfScreen;
            float screenRadius = currentRadius * zoom;

            Vector2 centerUV = screenPos / new Vector2(Main.screenWidth, Main.screenHeight);
            float radiusUV = screenRadius / Main.screenHeight;
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            // 设置着色器参数
            effect.Parameters["uTime"]?.SetValue(animTime);
            effect.Parameters["uCenter"]?.SetValue(centerUV);
            effect.Parameters["uRadius"]?.SetValue(radiusUV);
            effect.Parameters["uIntensity"]?.SetValue(fadeProgress);
            effect.Parameters["uAspect"]?.SetValue(aspect);
            effect.Parameters["uColorPrimary"]?.SetValue(curPrimary);
            effect.Parameters["uColorSecondary"]?.SetValue(curSecondary);

            // 切换SpriteBatch到着色器模式
            sb.End();

            sb.Begin(
                SpriteSortMode.Immediate,
                BlendState.NonPremultiplied,
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect,
                Matrix.Identity);

            // 全屏绘制噪声纹理（Immediate模式下SpriteBatch会自动Apply着色器Pass）
            sb.Draw(noiseTexture,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.White);

            sb.End();

            // 恢复原始SpriteBatch状态（与项目其他PreDraw一致使用PointClamp）
            sb.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        #endregion

        #region 资源管理

        private static void EnsureNoiseTexture() {
            if (noiseTexture == null || noiseTexture.IsDisposed)
                noiseTexture = GenerateNoiseTexture(Main.graphics.GraphicsDevice);
        }

        private static Effect GetEffect() {
            arenaEffect ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/DazhengArenaCircle",
                AssetRequestMode.ImmediateLoad);
            return arenaEffect?.Value;
        }

        /// <summary>
        /// 生成可平铺的三通道分形噪声纹理
        /// R/G/B 各通道为独立噪声，着色器中分别采样以获得丰富的有机纹路
        /// </summary>
        private static Texture2D GenerateNoiseTexture(GraphicsDevice device, int size = 256) {
            Color[] pixels = new Color[size * size];
            byte[][] channels = new byte[3][];

            for (int c = 0; c < 3; c++) {
                channels[c] = new byte[size * size];
                float[,] noise = GenerateTileableFBM(size, octaves: 5, seed: 42 + c * 173);

                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        channels[c][y * size + x] = (byte)(noise[x, y] * 255);
            }

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(channels[0][i], channels[1][i], channels[2][i], (byte)255);

            Texture2D tex = new(device, size, size, false, SurfaceFormat.Color);
            tex.SetData(pixels);
            return tex;
        }

        /// <summary>
        /// 可平铺的分形布朗运动 (FBM) 噪声
        /// 多八度值噪声叠加，边缘无缝衔接
        /// </summary>
        private static float[,] GenerateTileableFBM(int size, int octaves, int seed) {
            float[,] result = new float[size, size];
            Random rng = new(seed);

            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int oct = 0; oct < octaves; oct++) {
                int grid = Math.Max(2, (int)(4 * frequency));

                // 生成格点随机值
                float[] lattice = new float[(grid + 1) * (grid + 1)];
                for (int i = 0; i < lattice.Length; i++)
                    lattice[i] = (float)rng.NextDouble();

                // 平铺：右边缘 = 左边缘，下边缘 = 上边缘
                for (int i = 0; i <= grid; i++) {
                    lattice[i * (grid + 1) + grid] = lattice[i * (grid + 1)];
                    lattice[grid * (grid + 1) + i] = lattice[i];
                }
                lattice[grid * (grid + 1) + grid] = lattice[0];

                // 双线性插值 + smoothstep
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        float fx = (float)x / size * grid;
                        float fy = (float)y / size * grid;
                        int ix = Math.Min((int)fx, grid - 1);
                        int iy = Math.Min((int)fy, grid - 1);

                        float tx = fx - ix;
                        float ty = fy - iy;
                        tx = tx * tx * (3 - 2 * tx); // smoothstep
                        ty = ty * ty * (3 - 2 * ty);

                        float v00 = lattice[iy * (grid + 1) + ix];
                        float v10 = lattice[iy * (grid + 1) + ix + 1];
                        float v01 = lattice[(iy + 1) * (grid + 1) + ix];
                        float v11 = lattice[(iy + 1) * (grid + 1) + ix + 1];

                        float vx0 = v00 + (v10 - v00) * tx;
                        float vx1 = v01 + (v11 - v01) * tx;
                        float v = vx0 + (vx1 - vx0) * ty;

                        result[x, y] += v * amplitude;
                    }
                }

                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            // 归一化到 0~1
            if (maxValue > 0) {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        result[x, y] /= maxValue;
            }

            return result;
        }

        #endregion
    }
}

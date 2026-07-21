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
    /// 使用自定义着色器 + 噪声纹理渲染动态藤蔓纹路。
    ///
    /// V3 阶段化演变: 裂纹 (uCrack) 由大椿血量/阶段驱动 — P2 起浮现、濒死加深;
    /// 换阶段收缩瞬间白热闪光 (uFlash); 死亡演出中按 <see cref="Dazheng.DeathBarrierShatterTick"/>
    /// 执行碎裂时间轴 (闪光 → 崩解淡出 → 自灭), 死亡期间停用界外伤害与推力 (战场规则先死)。
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

        // V3 阶段化演变 (本地视觉)
        private float crack;         // 裂纹强度 (向 Boss 授权值平滑)
        private float flash;         // 白热闪光 (脉冲后衰减)
        private bool bossDying;      // 死亡演出中 (停界外伤害/推力)
        private int shatterTimer;    // 碎裂时间轴 (>0 已开始)

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

            // 季节换色 + 阶段化演变: 从大椿读取裂纹授权/闪光节拍/死亡时刻
            if (boss.ModNPC is Dazheng dz) {
                int s = dz.CurrentSeason;
                Vector4 tgtP = new(DazhengSeasons.Tint(s).ToVector3() * 0.45f, 1f);
                Vector4 tgtS = new(DazhengSeasons.Accent(s).ToVector3() * 0.85f, 1f);
                curPrimary = Vector4.Lerp(curPrimary, tgtP, 0.02f);
                curSecondary = Vector4.Lerp(curSecondary, tgtS, 0.02f);

                // 裂纹向授权值平滑 (P2 跳变 0.35 / 濒死 0.85 / 死亡 → 1)
                crack = MathHelper.Lerp(crack, dz.BarrierCrack, 0.045f);
                bossDying = dz.IsDying;

                // 换阶段爆发帧: 白热闪光脉冲
                if (dz.Phase == Dazheng.BossPhase.PhaseTransition_2 &&
                    (int)boss.ai[1] == Dazheng.Transition2BurstTick) {
                    flash = 1f;
                }

                // 死亡碎裂时间轴
                if (bossDying && boss.ai[1] >= Dazheng.DeathBarrierShatterTick) {
                    if (shatterTimer == 0)
                        BeginShatter();
                    shatterTimer++;
                    crack = 1f;
                    // 崩解: 40t 内 alpha 塌缩, 之后自灭 (战场规则先于树神死去)
                    fadeProgress = MathHelper.Clamp(fadeProgress - 0.028f, 0f, 1f);
                    if (fadeProgress <= 0f) {
                        Projectile.Kill();
                        return;
                    }
                }
            }
            flash = MathHelper.Lerp(flash, 0f, 0.08f);

            // 服务端：界外伤害 (死亡演出中停用 — 结界已死)
            if (Main.netMode != NetmodeID.MultiplayerClient && !bossDying) {
                damageTimer++;
                if (damageTimer >= DamageInterval) {
                    damageTimer = 0;
                    ApplyOutOfBoundsDamage();
                }
            }

            // 客户端：推力 + 边缘粒子 (碎裂后停止)
            if (Main.netMode != NetmodeID.Server && shatterTimer == 0) {
                if (!bossDying)
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
                Lighting.AddLight(lightPos, new Vector3(0.1f, 0.25f, 0.08f) * glow * fadeProgress);
            }
        }

        /// <summary>碎裂起点: 白热闪光 + 沿圆周崩解尘环 (一次性; 声画节拍由大椿死亡时间轴配)。</summary>
        private void BeginShatter() {
            flash = 1f;
            if (Main.netMode == NetmodeID.Server)
                return;
            for (int i = 0; i < 64; i++) {
                float angle = MathHelper.TwoPi / 64 * i;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * currentRadius;
                Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.JungleGrass : DustID.GoldFlame,
                    new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(1f, 4f) +
                    new Vector2(0, Main.rand.NextFloat(1f, 3f)), 90, default, Main.rand.NextFloat(1.4f, 2.2f));
                d.noGravity = Main.rand.NextBool();
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

            Effect effect = GetEffect();
            Texture2D noise = ACMShaders.NoiseTexture; // V3: 共享噪声件, 弃用私有重复生成器
            if (effect == null || noise == null)
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
            effect.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(crack, 0f, 1f));
            effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));

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
            sb.Draw(noise,
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

        private static Effect GetEffect() {
            arenaEffect ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/DazhengArenaCircle",
                AssetRequestMode.ImmediateLoad);
            return arenaEffect?.Value;
        }

        #endregion
    }
}

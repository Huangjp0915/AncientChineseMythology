using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿黄金幻象弹幕 — 金色的树神虚影，具有追踪能力
    /// 使用 SoftGlow + GlaciateWave 复合绘制金色幽灵效果
    /// 先缓慢漂浮，随后加速追踪玩家。
    ///
    /// V3 公平阀门: 追踪期转向率封顶 (0.045 rad/f, 可绕圈甩掉) + 速度 6→13 渐升;
    /// 剩余寿命 &lt; 120t 进入「熄火」— 停止追踪、拉直轨迹、金光冷却为暗琥珀并淡出 (可读的威胁解除)。
    /// </summary>
    public class DazhengGoldenPhantom : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int BurnoutTicks = 120;  // 熄火阶段时长 (寿命尾段)
        private const float MaxTurnRate = 0.045f;

        private float glowPhase;
        private bool activated; // 是否已激活追踪

        /// <summary>熄火进度 0(全力)~1(完全冷却)。</summary>
        private float Burnout => 1f - MathHelper.Clamp((Projectile.timeLeft - 40) / (float)(BurnoutTicks - 40), 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            glowPhase += 0.1f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 前60帧慢速漂浮，之后激活追踪
            if (Projectile.timeLeft < 300 && !activated) {
                activated = true;
            }

            bool burnedOut = Projectile.timeLeft < BurnoutTicks;
            // 伤害窗口与视觉对齐: 熄火(冷却变暗)即无害化
            Projectile.hostile = !burnedOut;

            if (activated && !burnedOut) {
                // 追踪最近的玩家 — 转向率封顶 (公平阀门: 高速横移/绕圈可甩掉)
                Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                if (target.active && !target.dead) {
                    // 速度渐升 6→13 (激活后 ~90t 达满速, 给逃逸窗口)
                    float ramp = MathHelper.Clamp((300 - Projectile.timeLeft) / 90f, 0f, 1f);
                    float speed = MathHelper.Lerp(6f, 13f, ramp);

                    float curAngle = Projectile.velocity.ToRotation();
                    float wantAngle = (target.Center - Projectile.Center).ToRotation();
                    float newAngle = curAngle + MathHelper.Clamp(
                        MathHelper.WrapAngle(wantAngle - curAngle), -MaxTurnRate, MaxTurnRate);
                    Projectile.velocity = newAngle.ToRotationVector2() * speed;
                }
            }
            else if (burnedOut) {
                // 熄火: 停止追踪, 轨迹拉直并缓慢减速 — 威胁解除的可读信号
                Projectile.velocity *= 0.985f;
            }
            else {
                // 缓慢漂浮+脉动
                Projectile.velocity *= 0.98f;
                float driftAngle = glowPhase * 0.5f;
                Projectile.velocity += new Vector2(MathF.Cos(driftAngle), MathF.Sin(driftAngle)) * 0.1f;
            }

            // 金色粒子 (熄火后转稀疏暗琥珀)
            if (Main.rand.NextBool(burnedOut ? 5 : 2)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                    0, 0, DustID.GoldFlame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, burnedOut ? 1f : 1.5f);
                d.noGravity = true;
                d.fadeIn = 1.5f;
            }

            // 偶尔释放树叶粒子
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                    0, 0, DustID.GrassBlades,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    150, default, 1.2f);
                d.noGravity = false;
            }

            float lightDim = 1f - Burnout * 0.7f;
            Lighting.AddLight(Projectile.Center, 0.5f * lightDim, 0.4f * lightDim, 0.1f * lightDim);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            // 熄火视觉: 金光冷却为暗琥珀 + 整体淡出 (脉动也随之平息)
            float cool = 1f - Burnout * 0.75f;
            float pulse = (1f + MathF.Sin(glowPhase * 3f) * 0.15f * cool) * MathHelper.Lerp(1f, 0.7f, Burnout);

            // === 1. 残影拖尾 ===
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float t = (float)i / Projectile.oldPos.Length;
                float alpha = 0.5f * (1f - t);
                Color trailColor = Color.Lerp(new Color(255, 220, 80), new Color(255, 160, 30), t) * alpha;
                trailColor.A = 0;

                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 origin = glowTex.Size() / 2f;
                    float trailScale = (1.2f - t * 0.6f) * pulse;
                    sb.Draw(glowTex, trailPos, null, trailColor, 0f, origin, trailScale, SpriteEffects.None, 0f);
                }
            }

            // === 2. 主体绘制 (Additive) ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                // 外层金色大光晕 (熄火时向暗琥珀冷却)
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float burnout = Burnout;

                    Color outerGlow = (Color.Lerp(new Color(255, 200, 50), new Color(140, 80, 30), burnout) with { A = 0 }) * (0.4f * pulse * cool);
                    sb.Draw(glowTex, drawPos, null, outerGlow, 0f, glowOrigin, 2.0f * pulse, SpriteEffects.None, 0f);

                    Color mainGlow = (Color.Lerp(new Color(255, 220, 100), new Color(160, 100, 45), burnout) with { A = 0 }) * (0.6f * pulse * cool);
                    sb.Draw(glowTex, drawPos, null, mainGlow, 0f, glowOrigin, 1.2f * pulse, SpriteEffects.None, 0f);

                    Color coreGlow = new Color(255, 250, 200, 0) * (0.5f * cool);
                    sb.Draw(glowTex, drawPos, null, coreGlow, 0f, glowOrigin, 0.5f, SpriteEffects.None, 0f);
                }

                // 使用 GlaciateWave 绘制树的虚影形状
                Texture2D waveTex = ACMAsset.GlaciateWave;
                if (waveTex != null) {
                    Vector2 waveOrigin = waveTex.Size() / 2f;

                    // 旋转的金色树影
                    float rotA = glowPhase * 0.5f;
                    float rotB = -glowPhase * 0.3f;

                    Color phantomA = new Color(255, 200, 60, 0) * (0.35f * pulse);
                    sb.Draw(waveTex, drawPos, null, phantomA, rotA, waveOrigin, new Vector2(0.12f, 0.06f), SpriteEffects.None, 0f);

                    Color phantomB = new Color(255, 180, 40, 0) * (0.25f * pulse);
                    sb.Draw(waveTex, drawPos, null, phantomB, rotB, waveOrigin, new Vector2(0.10f, 0.05f), SpriteEffects.None, 0f);

                    // 交叉层
                    Color phantomC = new Color(255, 230, 120, 0) * (0.2f * pulse);
                    sb.Draw(waveTex, drawPos, null, phantomC, rotA + MathHelper.PiOver2, waveOrigin, new Vector2(0.08f, 0.04f), SpriteEffects.None, 0f);
                }

                // 使用 BlankStar 绘制中心星辉
                Texture2D starTex = ACMAsset.BlankStar;
                if (starTex != null) {
                    Vector2 starOrigin = starTex.Size() / 2f;
                    float starRot = glowPhase * 0.8f;
                    Color starColor = new Color(255, 240, 180, 0) * (0.4f * pulse);
                    sb.Draw(starTex, drawPos, null, starColor, starRot, starOrigin, 0.3f * pulse, SpriteEffects.None, 0f);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 金色爆发粒子
            for (int i = 0; i < 15; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5),
                    80, default, 2f);
                d.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GrassBlades, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4),
                    100, default, 1.5f);
                d.noGravity = false;
            }
        }
    }
}

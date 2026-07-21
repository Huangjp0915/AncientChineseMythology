using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「根刺阵」— 春季签名的地面破土根矛 (V3 新增, 给静态树神一个有重量感的"点名"招)。
    ///
    /// 四段时间轴 (skill: 长前摇 / 急爆发 / 持留 / 收招):
    ///  ● 预警 42t: 竖直 Lethal 红光柱 + 基部根须聚集尘 (位置在生成瞬间锁定, 不追踪);
    ///  ● 破土 6t: poly(8) 急出全高 (速度对比: 前摇越静, 破土越猛);
    ///  ● 持留 30t: 全高伫立, 尖端渗金光;
    ///  ● 回缩 24t: 平滑没入土中。
    /// 伤害窗口与视觉严格对齐: 仅破土+持留期 hostile。
    ///
    /// ai[0] = 全高 (像素, 生成时定); ai[1] = 内部计时 (确定性推进, 多端一致)。
    /// 弹幕位置 = 根部基点 (地表)。绘制 client-only。
    /// </summary>
    public class DazhengRootSpear : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int WarnTicks = 42;
        private const int EruptTicks = 6;
        private const int HoldTicks = 30;
        private const int RetractTicks = 24;
        private const int TotalTicks = WarnTicks + EruptTicks + HoldTicks + RetractTicks;

        private float FullHeight => Projectile.ai[0] > 0 ? Projectile.ai[0] : 400f;
        private ref float Timer => ref Projectile.ai[1];

        private float wavePhase;

        public override void SetStaticDefaults() {
            // 根矛比 hitbox 高得多, 放宽屏外绘制裁剪
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 24;
            Projectile.hostile = false; // 预警期无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalTicks + 10;
            Projectile.ignoreWater = true;
        }

        /// <summary>当前伸出高度 (由确定性计时推导, 多端一致)。</summary>
        private float CurrentHeight() {
            float t = Timer;
            if (t <= WarnTicks)
                return 0f;
            if (t <= WarnTicks + EruptTicks) {
                // poly(8) 急出: 几乎全部高度在最后几帧完成 → "破土一瞬"
                float p = (t - WarnTicks) / EruptTicks;
                return FullHeight * (1f - MathF.Pow(1f - p, 8f));
            }
            if (t <= WarnTicks + EruptTicks + HoldTicks)
                return FullHeight;
            float r = (t - WarnTicks - EruptTicks - HoldTicks) / RetractTicks;
            return FullHeight * (1f - r * r); // 平滑没入
        }

        private bool DamageActive => Timer > WarnTicks && Timer <= WarnTicks + EruptTicks + HoldTicks;

        public override void AI() {
            Timer++;
            wavePhase += 0.18f;
            Projectile.velocity = Vector2.Zero;
            Projectile.hostile = DamageActive;

            if (Timer > TotalTicks) {
                Projectile.Kill();
                return;
            }

            // 破土瞬间: 一次性冲击反馈 (音效 + 碎土 + 轻震)
            if ((int)Timer == WarnTicks + 1 && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 1.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
                ACMUtils.AddScreenShake(3.5f);
                for (int i = 0; i < 18; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), 4f),
                        DustID.WoodFurniture, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -3f)),
                        100, default, Main.rand.NextFloat(1.4f, 2.4f));
                    d.noGravity = false;
                }
            }

            // 预警期: 基部根须聚集尘 (越临近破土越密)
            if (Timer <= WarnTicks && Main.netMode != NetmodeID.Server) {
                float urgency = Timer / (float)WarnTicks;
                if (Main.rand.NextFloat() < 0.3f + urgency * 0.5f) {
                    Vector2 p = Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-4f, 6f));
                    Dust d = Dust.NewDustPerfect(p, DustID.JungleGrass,
                        new Vector2(0, -Main.rand.NextFloat(0.5f, 2f) * (0.5f + urgency)), 120, default, 1.2f + urgency * 0.6f);
                    d.noGravity = true;
                }
            }

            // 伫立期: 尖端渗金光粒子
            float h = CurrentHeight();
            if (h > 20f && Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - new Vector2(Main.rand.NextFloat(-10f, 10f), h - 8f),
                    DustID.GoldFlame, new Vector2(0, -0.8f), 100, default, 1.1f);
                d.noGravity = true;
            }

            if (h > 10f)
                Lighting.AddLight(Projectile.Center - new Vector2(0, h * 0.6f), 0.25f, 0.4f, 0.1f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伤害窗口与可见实体严格对齐: 竖直矩形 = 根部 → 当前高度
            if (!DamageActive)
                return false;
            float h = CurrentHeight();
            if (h < 8f)
                return false;
            Rectangle body = new((int)(Projectile.Center.X - 20), (int)(Projectile.Center.Y - h), 40, (int)h);
            return body.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 basePos = Projectile.Center;
            float h = CurrentHeight();

            // —— 预警: 竖直 Lethal 光柱 (细→亮, 命中前 0.7s 可读) ——
            if (Timer <= WarnTicks) {
                float p = Timer / (float)WarnTicks;
                float w = MathHelper.Lerp(2.5f, 6f, p);
                Color core = TelegraphColors.Lethal;
                Color edge = TelegraphColors.Lethal * 0.4f;
                ACMShaders.DrawBeam(basePos, basePos - new Vector2(0, FullHeight), w,
                    core, edge, 0.35f + p * 0.5f, flowSpeed: 2.2f, flowScale: 3f, coreSharp: 2.6f);
                return false;
            }

            if (h < 6f)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D chainTex = TextureAssets.Chain.Value;

            // —— 根矛主体: 链条纹理堆叠 + 双股拧绞 (与 DazhengVine 语汇一致) ——
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                float segLen = chainTex.Height;
                int segs = (int)MathF.Ceiling(h / segLen) + 1;
                Vector2 chainOrigin = new(chainTex.Width / 2f, chainTex.Height / 2f);
                for (int i = 0; i < segs; i++) {
                    float yTop = i * segLen;
                    if (yTop > h) break;
                    float tFrac = yTop / MathF.Max(h, 1f);
                    // 越接近尖端越细; 双股左右拧绞
                    float taper = MathHelper.Lerp(1.25f, 0.45f, tFrac);
                    float twist = MathF.Sin(i * 1.4f + wavePhase) * 5f * (1f - tFrac);
                    Vector2 dp = basePos + new Vector2(twist, -yTop) - Main.screenPosition;
                    Color c = Color.Lerp(new Color(95, 150, 55), new Color(190, 170, 70), tFrac) *
                              MathHelper.Lerp(1f, 0.75f, tFrac);
                    sb.Draw(chainTex, dp, null, c, 0f, chainOrigin, new Vector2(taper, 1.05f), SpriteEffects.None, 0f);
                    sb.Draw(chainTex, dp + new Vector2(-twist * 1.6f, 0), null, c * 0.55f, 0f, chainOrigin,
                        new Vector2(taper * 0.7f, 1.05f), SpriteEffects.None, 0f);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                // 尖端金芒 + 基部辉光
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    Vector2 go = glow.Size() / 2f;
                    Vector2 tip = basePos - new Vector2(0, h) - Main.screenPosition;
                    float pulse = 1f + MathF.Sin(wavePhase * 2f) * 0.15f;
                    sb.Draw(glow, tip, null, new Color(255, 220, 110, 0) * 0.55f, 0f, go, 0.5f * pulse, SpriteEffects.None, 0f);
                    sb.Draw(glow, basePos - Main.screenPosition, null, new Color(80, 180, 60, 0) * 0.4f, 0f, go, 0.8f, SpriteEffects.None, 0f);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0),
                    DustID.JungleGrass, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)),
                    100, default, 1.3f);
                d.noGravity = true;
            }
        }
    }
}

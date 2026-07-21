using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 旋转星系球 — 轨道运动学星阵弹幕 (ArgusGalaxy 着色器单批绘制)。
    /// 速度旋转运动学: 生成时给切向初速, 每帧速度旋转 ω → 圆轨道由运动自洽产生, 无需锚点同步;
    /// drift 每帧缩放速度 → 螺旋收缩/扩张 (轨道半径 = v/ω 随之变化)。
    /// ai[0]=ω (rad/tick, 符号=旋向); ai[1]=drift (每 tick 速度缩放增量, 负=收缩);
    /// ai[2]=成形时长 chargeTicks (幽灵态淡入, 无伤害 — 公平阀门: 星阵成形延迟可读)。
    /// </summary>
    public class SpinningGalacticOrbs : ModProjectile
    {
        private ref float Omega => ref Projectile.ai[0];
        private ref float Drift => ref Projectile.ai[1];
        private ref float ChargeTicks => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        private float spinPhase;

        private static Asset<Effect> galaxyRef;

        private static Effect GalaxyEffect {
            get {
                if (Main.dedServ)
                    return null;
                galaxyRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/ArgusGalaxy", AssetRequestMode.ImmediateLoad);
                return galaxyRef?.Value;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            //首球代绘全体 → 出屏也要绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2200;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        private float EffCharge => ChargeTicks > 1f ? ChargeTicks : 24f;

        /// <summary>成形进度 0~1 (亮度/伤害门)。</summary>
        private float Charge => ACMUtils.Clamp01(Age / EffCharge);

        //幽灵态无伤害 — 伤害窗口与满亮视觉严格对齐
        public override bool? CanDamage() => Charge >= 1f ? null : false;

        //轨道半径栓绳 (px): 防止收缩碾压玩家 / 展开飞出视野 (经 |v| = ω·r 换算约束速度)
        private const float MinOrbitRadius = 170f;
        private const float MaxOrbitRadius = 820f;

        public override void AI() {
            Age++;
            spinPhase += 0.11f + MathF.Abs(Omega) * 2f;

            //轨道运动学: 速度旋转 ω + drift 径向螺旋
            if (Omega != 0f)
                Projectile.velocity = Projectile.velocity.RotatedBy(Omega);
            if (Drift != 0f)
                Projectile.velocity *= 1f + Drift;

            //半径栓绳 + 离心散场: 生命末段解除轨道沿切线甩出 (星阵收拍可读)
            if (Omega != 0f) {
                float omega = MathF.Abs(Omega);
                float speed = Projectile.velocity.Length();
                float minSpeed = omega * MinOrbitRadius;
                float maxSpeed = omega * MaxOrbitRadius;
                if (speed < minSpeed)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * minSpeed;
                else if (speed > maxSpeed)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * maxSpeed;

                if (Projectile.timeLeft < 45) {
                    Omega = 0f;
                    Drift = 0f;
                }
            }

            Projectile.rotation += 0.1f * MathF.Sign(Omega == 0f ? 1f : Omega);

            //成形瞬间的提亮迸星
            if ((int)Age == (int)EffCharge && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center,
                        i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch,
                        Main.rand.NextVector2Circular(2.5f, 2.5f), 90, default, 1.3f);
                    d.noGravity = true;
                }
            }

            //星系旋转粒子 (幽灵态减量)
            if (Main.rand.NextBool(Charge >= 1f ? 4 : 8)) {
                float angle = spinPhase * 2f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * 10f;
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, dustType, 0, 0, 130, default, 1.1f);
                d.noGravity = true;
                d.velocity = new Vector2(-offset.Y, offset.X) * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.2f, 0.55f) * (0.35f + 0.65f * Charge));
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.3f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }

        // ===== 绘制: 首球一次切批 (ArgusGalaxy), 逐球仅换顶点色/旋转, 零切批开销 =====

        private bool IsLeadOrb() {
            for (int i = 0; i < Projectile.whoAmI; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == Type)
                    return false;
            }
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!IsLeadOrb())
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Effect fx = GalaxyEffect;
            Texture2D glow = ACMAsset.SoftGlow;

            if (fx == null || glow == null) {
                DrawFallback(sb);
                return false;
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            int type = Type;
            float scaleBase = 108f / glow.Width;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type)
                    continue;
                var orb = (SpinningGalacticOrbs)p.ModProjectile;
                float charge = orb.Charge;
                float pulse = 1f + MathF.Sin(orb.spinPhase * 1.6f) * 0.07f;
                //顶点色: rgb=色调(紫/蓝按标识交替), a=成形进度兼透明度 (着色器契约)
                Color tint = (p.identity % 2 == 0 ? new Color(185, 105, 255) : new Color(105, 150, 255))
                    * (0.55f + 0.45f * charge);
                tint.A = (byte)(255 * MathHelper.Clamp(0.35f + 0.65f * charge, 0f, 1f));
                sb.Draw(glow, p.Center - Main.screenPosition, null, tint,
                    p.rotation + orb.spinPhase * 0.3f, glow.Size() * 0.5f,
                    scaleBase * p.scale * pulse * (0.65f + 0.35f * charge), SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }

        /// <summary>着色器不可用时的退化绘制 (原贴图)。</summary>
        private void DrawFallback(SpriteBatch sb) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            int type = Type;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type)
                    continue;
                var orb = (SpinningGalacticOrbs)p.ModProjectile;
                float charge = orb.Charge;
                float pulse = 1f + MathF.Sin(orb.spinPhase * 4f) * 0.12f;
                Color mainColor = Color.Lerp(new Color(180, 100, 240), new Color(80, 120, 255),
                    MathF.Sin(orb.spinPhase) * 0.5f + 0.5f) * (0.35f + 0.65f * charge);
                sb.Draw(texture, p.Center - Main.screenPosition, null, mainColor, p.rotation, origin,
                    p.scale * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}

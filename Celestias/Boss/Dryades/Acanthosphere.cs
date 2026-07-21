using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 刺球 — 树精的多模态弹幕 (V2)。
    ///  ai[0] 模式:
    ///   0 = 普通 (轻重力 + 弹跳, 旧行为)。
    ///   1 = 陷阱 (落地扎入 ~3s → 引爆放射根须; 空间封锁, 非直接投掷)。
    ///   2 = 环刺 (无重力直线向心, 刺球领域收缩环用; 撞地即灭)。
    /// </summary>
    public class Acanthosphere : ModProjectile
    {
        private float spinRotation;
        private int bounceCount;

        private const int TrapPlantLife = 180; // 陷阱扎地引爆计时 (~3s)
        private const int TrapWarnWindow = 36;  // 引爆前转赤红预警

        private float Mode => Projectile.ai[0];
        private bool planted;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 480;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            spinRotation += Projectile.velocity.Length() * 0.04f + 0.02f;
            Projectile.rotation = spinRotation;

            switch ((int)Mode) {
                case 1: TrapAI(); break;
                case 2: RingAI(); break;
                default: NormalAI(); break;
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.05f);
        }

        // ===== 模式 0: 普通 (轻重力 + 弹跳) =====
        private void NormalAI() {
            if (Projectile.velocity.Y < 16f)
                Projectile.velocity.Y += 0.15f;
            EmitTrailDust();
        }

        // ===== 模式 2: 环刺 (无重力直线向心, 刺球领域) =====
        private void RingAI() {
            Projectile.penetrate = -1;
            // 只做一次性上限压缩 (旧版每帧重置 timeLeft 导致永不超时)
            if (Projectile.timeLeft > 300)
                Projectile.timeLeft = 300;
            // 接近中心后自然消亡, 避免在中心堆积
            if (Projectile.velocity.Length() < 0.5f)
                Projectile.Kill();
            EmitTrailDust();
        }

        // ===== 模式 1: 陷阱 (扎地 → 引爆) =====
        private void TrapAI() {
            Projectile.penetrate = -1;
            Projectile.tileCollide = !planted;

            if (!planted) {
                // 下落扎地
                if (Projectile.velocity.Y < 14f)
                    Projectile.velocity.Y += 0.35f;
                Projectile.timeLeft = 600;

                // 落地或撞地由 OnTileCollide 触发 Plant; 超时未落地则强制扎住
                Projectile.ai[1]++;
                if (Projectile.ai[1] > 90f)
                    Plant();
                EmitTrailDust();
                return;
            }

            // 已扎地: 计时引爆
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[1]++;
            int t = (int)Projectile.ai[1];

            float warnFrac = MathHelper.Clamp((t - (TrapPlantLife - TrapWarnWindow)) / (float)TrapWarnWindow, 0f, 1f);

            if (Main.netMode != NetmodeID.Server) {
                // 待机绿尘脉冲; 临爆赤红 (TelegraphColors.Lethal)
                if (Main.rand.NextBool(warnFrac > 0.01f ? 2 : 5)) {
                    int dustType = warnFrac > 0.4f ? DustID.RedTorch : DustID.GreenTorch;
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(14, 14), 28, 28, dustType,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.5f, -0.5f),
                        80, default, 1f + warnFrac);
                    d.noGravity = true;
                }
            }

            if (t >= TrapPlantLife)
                Detonate();
        }

        private void Plant() {
            if (planted)
                return;
            planted = true;
            Projectile.ai[1] = 0f;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.4f, Volume = 0.5f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.WoodFurniture,
                        Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 1f), 130, default, 1.2f);
                    d.noGravity = false;
                }
            }
        }

        private void Detonate() {
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item62 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                ACMUtils.AddScreenShake(4f);
                for (int i = 0; i < 16; i++) {
                    float a = MathHelper.TwoPi / 16 * i;
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                        MathF.Cos(a) * 5f, MathF.Sin(a) * 5f, 80, default, 1.6f);
                    d.noGravity = true;
                }
            }

            // 引爆放射根须 (server 权威)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int shards = 8;
                for (int i = 0; i < shards; i++) {
                    float a = MathHelper.TwoPi / shards * i + MathHelper.PiOver4 * 0.5f;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 7.5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<DryadsVine>(), Projectile.damage, 1f, Projectile.owner);
                }
            }
            Projectile.Kill();
        }

        private void EmitTrailDust() {
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.JungleGrass,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    100, default, 1.1f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            // 陷阱: 撞地即扎入
            if ((int)Mode == 1) {
                Plant();
                return false;
            }
            // 环刺: 撞地即灭
            if ((int)Mode == 2) {
                Projectile.Kill();
                return false;
            }

            // 普通: 弹跳 (有限次)
            bounceCount++;
            if (bounceCount >= 5) {
                Projectile.Kill();
                return false;
            }

            if (MathF.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
                Projectile.velocity.X = -oldVelocity.X * 0.85f;
            if (MathF.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;

            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.WoodFurniture,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f), 130, default, 1f);
                d.noGravity = false;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WoodFurniture, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    120, default, 1.3f);
                d.noGravity = false;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f),
                    80, default, 1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 辉光核: 加性 SoftGlow 底光 (弹幕在暗色地形背景上可读)
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                float corePulse = 0.85f + 0.15f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI);
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    new Color(110, 220, 70, 0) * (0.55f * corePulse), 0f, glow.Size() / 2f,
                    0.5f * Projectile.scale, SpriteEffects.None, 0f);
                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 残影尾迹 (扎地陷阱不画残影)
            if (!planted) {
                for (int i = 1; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float alpha = 1f - (float)i / Projectile.oldPos.Length;
                    Color trailColor = lightColor * alpha * 0.4f;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float trailRot = Projectile.oldRot[i];
                    Main.spriteBatch.Draw(texture, trailPos, null, trailColor, trailRot, origin,
                        Projectile.scale * (0.7f + 0.3f * alpha), SpriteEffects.None, 0f);
                }
            }

            // 陷阱临爆: 主体染赤红脉冲, 让"即将致命"可读
            Color body = lightColor;
            float bodyScale = Projectile.scale;
            if ((int)Mode == 1 && planted) {
                float warnFrac = MathHelper.Clamp((Projectile.ai[1] - (TrapPlantLife - TrapWarnWindow)) / TrapWarnWindow, 0f, 1f);
                float pulse = 0.5f + 0.5f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * (8f + warnFrac * 18f));
                body = Color.Lerp(lightColor, TelegraphColors.Lethal, warnFrac * pulse);
                bodyScale *= 1f + warnFrac * 0.25f * pulse;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, drawPos, null, body, Projectile.rotation, origin,
                bodyScale, SpriteEffects.None, 0f);

            return false;
        }
    }
}

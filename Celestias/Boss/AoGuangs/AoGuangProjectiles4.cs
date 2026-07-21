using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region 深渊漩涡

    /// <summary>
    /// 深渊漩涡 (V3) - 三阶段签名 set-piece 的定点巨涡。
    /// 公平阀门: 拉力 4s 渐强至峰值 0.35 (可对抗), 红环 (ArenaRunic) 即碰撞边界,
    /// 全屏向心折射由 Boss 侧驱动。存活 300f。
    /// </summary>
    public class AbyssalVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];

        private float vortexRotation;
        private float vortexAlpha = 0f;
        private float vortexRadius = 50f;
        private const float MaxRadius = 280f;
        private const int LifeTime = 300;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 300;
            Projectile.height = 300;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            float age = LifeTime - Projectile.timeLeft;
            vortexRotation += 0.2f + age * 0.0006f; // 转速缓升

            // 成型 / 崩解
            if (Projectile.timeLeft > 40) {
                vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.035f);
                vortexRadius = MathHelper.Lerp(vortexRadius, MaxRadius, 0.03f);
            }
            else {
                vortexAlpha = Projectile.timeLeft / 40f;
                vortexRadius = MathHelper.Lerp(vortexRadius, 60f, 0.08f);
            }

            // 吸引玩家: 峰值 0.35, 前 240f 线性渐强 (给足对抗与学习时间)
            float pullRamp = MathHelper.Clamp(age / 240f, 0f, 1f) * vortexAlpha;
            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;

                float distance = Vector2.Distance(player.Center, Projectile.Center);
                if (distance < 640f && distance > 40f) {
                    Vector2 pullDir = (Projectile.Center - player.Center).SafeNormalize(Vector2.Zero);
                    float pullStrength = (1f - distance / 640f) * 0.35f * pullRamp;
                    player.velocity += pullDir * pullStrength;

                    // 轻微旋转拉扯 (氛围, 不足以改变走位)
                    player.velocity += pullDir.RotatedBy(MathHelper.PiOver2) * pullStrength * 0.25f;
                }
            }

            // 螺旋吸入粒子: 从外圈螺旋卷入 (吸力的可读形状)
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    if (Main.rand.NextBool(2)) continue;
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = Main.rand.NextFloat(vortexRadius * 0.9f, vortexRadius * 1.9f);
                    Vector2 dustPos = Projectile.Center + ang.ToRotationVector2() * r;
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 140, default, 2.2f);
                    d.noGravity = true;
                    // 切向 + 向心的螺旋速度
                    d.velocity = (ang + MathHelper.PiOver2 + 0.5f).ToRotationVector2() * 7f
                               - ang.ToRotationVector2() * 3f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DeepSeaBlue.ToVector3() * vortexAlpha * 1.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 碰撞边界 = 红环半径 (视觉与伤害严格一致)
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return vortexAlpha > 0.6f && distance < vortexRadius * 0.8f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 多层旋涡 (24 层, 控 overdraw): 内亮外暗, 相邻层反向
            int layers = 24;
            for (int layer = layers - 1; layer >= 0; layer--) {
                float p = layer / (float)(layers - 1);
                float layerScale = MathHelper.Lerp(0.35f, 1.55f, p);
                float layerRot = vortexRotation * (1f + p * 0.8f) * (layer % 2 == 0 ? 1 : -1);
                float layerAlpha = vortexAlpha * MathHelper.Lerp(0.55f, 0.1f, p);

                Color layerColor = Color.Lerp(AoGuangHelper.WaterGlow, AoGuangHelper.DeepSeaBlue, MathF.Sqrt(p));
                layerColor *= layerAlpha;
                layerColor.A = 0;

                sb.Draw(tornadoTex, screenPos, null, layerColor, layerRot, origin,
                    layerScale * (vortexRadius / MaxRadius), SpriteEffects.None, 0f);
            }

            // 中心深渊核心 (吞光的暗心 + 微亮瞳)
            if (ACMAsset.LightShot != null) {
                Color coreColor = AoGuangHelper.DeepSeaBlue * vortexAlpha * 0.85f;
                coreColor.A = 0;
                sb.Draw(ACMAsset.LightShot, screenPos, null, coreColor, vortexRotation * 2f,
                    ACMAsset.LightShot.Size() / 2f, 1.4f * vortexAlpha, SpriteEffects.None, 0f);
                Color pupil = AoGuangHelper.WaterGlow * vortexAlpha * 0.4f;
                pupil.A = 0;
                sb.Draw(ACMAsset.LightShot, screenPos, null, pupil, -vortexRotation * 1.5f,
                    ACMAsset.LightShot.Size() / 2f, 0.4f * vortexAlpha, SpriteEffects.None, 0f);
            }

            DrawAbyssRing();

            return false;
        }

        /// <summary>
        /// 深渊漩涡致命半径环描边 (ArenaRunic 法阵模式)。环内为致命死区(碰撞 = vortexRadius*0.8),
        /// 环用红=致命描边明示"被吸向中心的死亡边界"。客户端纯视觉, 配合 Boss 全屏折射的向心吸入。
        /// </summary>
        private void DrawAbyssRing() {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled || vortexAlpha <= 0.05f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(Projectile.Center, vortexRadius * 0.8f,
                out Vector2 uvCenter, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(vortexAlpha, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Lethal.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(AoGuangHelper.DeepSeaBlue.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 爆发粒子
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * i / 60;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8, 15);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Water,
                    1 => DustID.BlueTorch,
                    _ => DustID.Wet
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1.2f }, Projectile.Center);
            ACMUtils.AddScreenShake(10f);
        }
    }

    #endregion

    #region 落水矛

    /// <summary>
    /// 落水矛 - 从天而降的水矛
    /// </summary>
    public class FallingWaterSpear : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float spearPhase;
        private bool hasLanded = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            spearPhase += 0.1f;

            // 加速下落
            if (Projectile.velocity.Y < 25f) {
                Projectile.velocity.Y += 0.3f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 水矛拖尾粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    dustPos += Main.rand.NextVector2Circular(8, 8);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.5f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!hasLanded) {
                hasLanded = true;

                // 着地爆发
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-8, -2));
                        int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                        int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }

                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);
                ACMUtils.AddScreenShake(3f);
            }

            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            // 注意：GlaciateWave朝右，需要旋转使其朝下
            Vector2 origin = new Vector2(tex.Width * 0.1f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoGuangHelper.OceanTeal * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 scale = new Vector2(0.4f * progress, 0.08f * progress);
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            }

            // 主体水矛
            Color mainColor = AoGuangHelper.WaterGlow * 0.9f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.5f, 0.1f), SpriteEffects.None, 0f);

            // 矛尖高光
            Color tipColor = AoGuangHelper.PureWhite * 0.7f;
            tipColor.A = 0;
            Vector2 tipOffset = Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;
            if (ACMAsset.LightShot != null) {
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos + tipOffset, null, tipColor,
                    Projectile.rotation, ACMAsset.LightShot.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion
}

using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region 深渊漩涡

    /// <summary>
    /// 深渊漩涡 - 三阶段的巨型漩涡，使用原版龙卷纹理
    /// </summary>
    public class AbyssalVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];

        private float vortexRotation;
        private float vortexAlpha = 0f;
        private float vortexRadius = 50f;
        private const float MaxRadius = 300f;

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
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            vortexRotation += 0.25f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.03f);
            vortexRadius = MathHelper.Lerp(vortexRadius, MaxRadius, 0.04f);

            // 强力吸引玩家
            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;

                float distance = Vector2.Distance(player.Center, Projectile.Center);
                if (distance < 600f && distance > 50f) {
                    Vector2 pullDir = (Projectile.Center - player.Center).SafeNormalize(Vector2.Zero);
                    float pullStrength = (1f - distance / 600f) * 1.5f;
                    player.velocity += pullDir * pullStrength;

                    // 旋转拉扯
                    Vector2 tangent = pullDir.RotatedBy(MathHelper.PiOver2);
                    player.velocity += tangent * pullStrength * 0.3f;
                }
            }

            // 深渊粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int ring = 0; ring < 3; ring++) {
                    float ringRadius = vortexRadius * (0.4f + ring * 0.3f);
                    int particleCount = 6 + ring * 2;

                    for (int i = 0; i < particleCount; i++) {
                        if (Main.rand.NextBool(3)) continue;

                        float angle = vortexRotation * (1.2f - ring * 0.2f) + MathHelper.TwoPi * i / particleCount;
                        Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                        int dustType = Main.rand.Next(3) switch {
                            0 => DustID.Water,
                            1 => DustID.BlueTorch,
                            _ => DustID.Wet
                        };
                        int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2.5f - ring * 0.3f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * (10f - ring * 2f);
                    }
                }

                // 中心深渊粒子
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(30, 30);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 200, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(2, 2);
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DeepSeaBlue.ToVector3() * vortexAlpha * 1.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < vortexRadius * 0.8f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            // 使用原版龙卷风纹理绘制深渊
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 绘制多层旋转深渊
            int layers = 118;
            for (int layer = layers - 1; layer >= 0; layer--) {
                float layerScale = 0.5f + layer * 0.2f;
                float layerRot = vortexRotation * (1f + layer * 0.15f) * (layer % 2 == 0 ? 1 : -1);
                float layerAlpha = vortexAlpha * (0.8f - layer * 0.08f);

                // 颜色从外到内渐变
                Color layerColor;
                if (layer < 3) {
                    layerColor = Color.Lerp(AoGuangHelper.DeepSeaBlue, AoGuangHelper.WaterGlow, layer / 3f);
                }
                else if (layer < 6) {
                    layerColor = Color.Lerp(AoGuangHelper.DragonBlue, AoGuangHelper.OceanTeal, (layer - 3) / 3f);
                }
                else {
                    layerColor = AoGuangHelper.DeepSeaBlue;
                }

                layerColor *= layerAlpha;
                layerColor.A = 0;

                sb.Draw(tornadoTex, screenPos, null, layerColor, layerRot, origin, layerScale * (vortexRadius / MaxRadius), SpriteEffects.None, 0f);
            }

            // 中心深渊核心
            if (ACMAsset.LightShot != null) {
                Color coreColor = AoGuangHelper.DeepSeaBlue * vortexAlpha * 0.8f;
                coreColor.A = 0;
                sb.Draw(ACMAsset.LightShot, screenPos, null, coreColor, vortexRotation * 2f,
                    ACMAsset.LightShot.Size() / 2f, 1.5f * vortexAlpha, SpriteEffects.None, 0f);
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

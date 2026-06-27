using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region 龙息光束

    /// <summary>
    /// 龙息光束 - 水柱激光
    /// </summary>
    public class DragonBreathBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2000f;
        private const int LaserDuration = 90;

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];
        private float laserWidth = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = 35;
            Projectile.height = 35;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LaserDuration;
        }

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 从Boss获取角度
            if (owner.ModNPC is AoGuang dragon) {
                LaserAngle = dragon.breathAngle;
            }

            Projectile.rotation = LaserAngle;

            // 激光宽度动画
            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            if (progress < 0.1f) {
                laserWidth = MathHelper.Lerp(0f, 1f, progress / 0.1f);
            }
            else if (progress > 0.85f) {
                laserWidth = MathHelper.Lerp(1f, 0f, (progress - 0.85f) / 0.15f);
            }
            else {
                laserWidth = 1f;
            }

            // 水花粒子
            if (Main.netMode != NetmodeID.Server) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 6; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(20 * laserWidth, 20 * laserWidth);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 2f * laserWidth);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 3f;
                }
            }

            // 光照
            for (int i = 0; i < 10; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, AoGuangHelper.DragonBlue.ToVector3() * 1.5f * laserWidth);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f * laserWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            // 多层激光
            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = (0.25f + layer * 0.15f) * laserWidth;
                float layerAlpha = 0.9f - layer * 0.2f;

                Color layerColor = layer switch {
                    0 => AoGuangHelper.WaterGlow,
                    1 => AoGuangHelper.DragonBlue,
                    2 => AoGuangHelper.OceanTeal,
                    _ => new Color(100, 180, 220)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 scale = new Vector2(LaserLength / laserTex.Width, layerWidth);
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);
            }

            // 起点光球
            if (ACMAsset.LightShot != null) {
                Color orbColor = AoGuangHelper.WaterGlow * laserWidth * 0.8f;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 2.5f * laserWidth, SpriteEffects.None, 0f);
            }

            // V2: 共享 BeamGrad 原语叠一道流动光芯, 强化龙息水柱辨识度
            if (laserWidth > 0.05f) {
                Vector2 start = Projectile.Center;
                Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
                ACMShaders.DrawBeam(start, end, 34f * laserWidth,
                    AoGuangHelper.WaterGlow, AoGuangHelper.OceanTeal, laserWidth,
                    flowSpeed: 1.6f, flowScale: 2.2f);
            }

            return false;
        }
    }

    #endregion

    #region 潮汐激光

    /// <summary>
    /// 潮汐激光 - 三阶段强力激光
    /// </summary>
    public class TidalBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2800f;
        private const int LaserDuration = 100;

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];
        private float laserWidth = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3500;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LaserDuration;
        }

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 从Boss获取角度
            if (owner.ModNPC is AoGuang dragon) {
                LaserAngle = dragon.breathAngle;
            }

            Projectile.rotation = LaserAngle;

            // 更粗的激光动画
            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            if (progress < 0.08f) {
                laserWidth = MathHelper.Lerp(0f, 1f, progress / 0.08f);
            }
            else if (progress > 0.9f) {
                laserWidth = MathHelper.Lerp(1f, 0f, (progress - 0.9f) / 0.1f);
            }
            else {
                laserWidth = 1f;
            }

            // 更密集的水花
            if (Main.netMode != NetmodeID.Server) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 10; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(30 * laserWidth, 30 * laserWidth);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f * laserWidth);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 4f + Main.rand.NextVector2Circular(2, 2);
                }
            }

            // 更强的光照
            for (int i = 0; i < 14; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, AoGuangHelper.DragonBlue.ToVector3() * 2f * laserWidth);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 60f * laserWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            // 多层更粗的激光
            for (int layer = 4; layer >= 0; layer--) {
                float layerWidth = (0.35f + layer * 0.2f) * laserWidth;
                float layerAlpha = 0.95f - layer * 0.18f;

                Color layerColor = layer switch {
                    0 => AoGuangHelper.PureWhite,
                    1 => AoGuangHelper.WaterGlow,
                    2 => AoGuangHelper.DragonBlue,
                    3 => AoGuangHelper.OceanTeal,
                    _ => new Color(80, 150, 200)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 scale = new Vector2(LaserLength / laserTex.Width, layerWidth);
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);
            }

            // 起点爆发
            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = AoGuangHelper.WaterGlow * laserWidth * 0.7f;
                sparkleColor.A = 0;
                float sparkleRot = (float)Main.GameUpdateCount * 0.12f;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, sparkleRot,
                    ACMAsset.Sparkle.Size() / 2f, 3f * laserWidth, SpriteEffects.None, 0f);
            }

            if (ACMAsset.LightShot != null) {
                Color orbColor = AoGuangHelper.PureWhite * laserWidth * 0.9f;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 3.5f * laserWidth, SpriteEffects.None, 0f);
            }

            // V2: 共享 BeamGrad 原语叠一道流动光芯, 提升潮汐激光的辨识度与质感
            if (laserWidth > 0.05f) {
                Vector2 start = Projectile.Center;
                Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
                ACMShaders.DrawBeam(start, end, 50f * laserWidth,
                    AoGuangHelper.PureWhite, AoGuangHelper.DragonBlue, laserWidth,
                    flowSpeed: 2.0f, flowScale: 2.4f, coreSharp: 2.4f);
            }

            return false;
        }
    }

    #endregion

    #region 巨型漩涡

    /// <summary>
    /// 巨型漩涡 - 吸引玩家的大型漩涡
    /// </summary>
    public class GiantWhirlpool : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float vortexAngle;
        private float vortexAlpha = 0f;
        private float vortexRadius = 50f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1500;
        }

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            vortexAngle += 0.2f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.03f);

            // 漩涡扩大
            float targetRadius = 180f;
            vortexRadius = MathHelper.Lerp(vortexRadius, targetRadius, 0.05f);

            // 吸引附近玩家
            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;

                float distance = Vector2.Distance(player.Center, Projectile.Center);
                if (distance < 400f && distance > 50f) {
                    Vector2 pullDir = (Projectile.Center - player.Center).SafeNormalize(Vector2.Zero);
                    float pullStrength = (1f - distance / 400f) * 0.8f;
                    player.velocity += pullDir * pullStrength;
                }
            }

            // 大型漩涡粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 8;
                    float radius = vortexRadius * (0.8f + MathF.Sin(vortexAngle * 2f + i) * 0.2f);
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 8f;
                }

                // 中心粒子
                for (int i = 0; i < 3; i++) {
                    Vector2 dustVel = Main.rand.NextVector2Circular(3, 3);
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, dustVel.X, dustVel.Y, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.OceanTeal.ToVector3() * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < vortexRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制大型漩涡
            AoGuangHelper.DrawGiantWhirlpool(sb, Projectile.Center, vortexRadius, vortexAngle, vortexAlpha);

            DrawWhirlRing();

            return false;
        }

        /// <summary>
        /// 巨型漩涡致命核心半径环描边 (ArenaRunic 法阵)。环内 = 致命碰撞区(distance &lt; vortexRadius),
        /// 红=致命描边明示伤害边界, 与吸力方向(粒子)共同构成可读 tell。客户端纯视觉。
        /// </summary>
        private void DrawWhirlRing() {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled || vortexAlpha <= 0.05f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(Projectile.Center, vortexRadius,
                out Vector2 uvCenter, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(vortexAlpha * 0.9f, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Lethal.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(AoGuangHelper.OceanTeal.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 爆发粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40;
                Vector2 vel = angle.ToRotationVector2() * 8f;
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.4f }, Projectile.Center);
        }
    }

    #endregion

    #region 龙王仆从

    /// <summary>
    /// 龙王仆从 - 虾兵蟹将
    /// </summary>
    public class DragonMinion : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private float attackTimer = 0;
        private float minionPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 1;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            minionPhase += 0.05f;
            attackTimer++;

            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            // 追踪玩家
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float distance = toTarget.Length();

                if (distance > 100f) {
                    // 追踪
                    float speed = 6f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.08f);
                }
                else {
                    // 靠近时减速
                    Projectile.velocity *= 0.95f;
                }

                // 定期攻击
                if (attackTimer >= 90 && Main.netMode != NetmodeID.MultiplayerClient) {
                    attackTimer = 0;
                    Vector2 shotDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        shotDir * 8f,
                        ModContent.ProjectileType<DragonWaterBolt>(),
                        Projectile.damage / 2,
                        1f
                    );

                    SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.OceanTeal.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(minionPhase * 2f) * 0.1f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoGuangHelper.OceanTeal * progress * 0.3f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.6f * progress, SpriteEffects.None, 0f);
            }

            // 外光
            Color outerColor = AoGuangHelper.DragonBlue * 0.5f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoGuangHelper.WaterGlow * 0.7f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.6f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion
}

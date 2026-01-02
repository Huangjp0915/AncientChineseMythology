using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥激光束 - 横向扫射的持续激光
    /// </summary>
    internal class NetherLaserBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float LaserDirection => ref Projectile.ai[0]; // 激光方向角度
        private ref float LaserTimer => ref Projectile.ai[1];
        private ref float MaxLength => ref Projectile.ai[2]; // 最大长度

        private float currentLength = 0f;
        private const float TargetLength = 1500f;
        private const float BeamWidth = 30f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            LaserTimer++;

            // 激光长度变化
            if (LaserTimer < 10f) {
                // 快速伸展
                currentLength = MathHelper.Lerp(0f, TargetLength, LaserTimer / 10f);
            }
            else if (Projectile.timeLeft < 20) {
                // 收缩
                currentLength = MathHelper.Lerp(TargetLength, 0f, 1f - Projectile.timeLeft / 20f);
            }
            else {
                currentLength = TargetLength;
            }

            // 碰撞检测长度（考虑墙壁）
            MaxLength = GetLaserLength();

            // 旋转效果（微小摆动）
            LaserDirection += MathF.Sin(LaserTimer * 0.1f) * 0.002f;

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                Vector2 laserEnd = Projectile.Center + new Vector2(MathF.Cos(LaserDirection), MathF.Sin(LaserDirection)) * MaxLength;
                Vector2 dustPos = Vector2.Lerp(Projectile.Center, laserEnd, Main.rand.NextFloat(0.2f, 1f));

                int dust = Dust.NewDust(dustPos, 1, 1, DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            // 发光
            Lighting.AddLight(Projectile.Center, 0.4f, 0.6f, 1f);

            // 音效
            if (LaserTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item33, Projectile.Center);
            }
        }

        private float GetLaserLength() {
            float length = 50f;
            Vector2 direction = new Vector2(MathF.Cos(LaserDirection), MathF.Sin(LaserDirection));

            while (length <= currentLength) {
                Vector2 testPoint = Projectile.Center + direction * length;

                if (!Collision.CanHit(Projectile.Center, 1, 1, testPoint, 1, 1)) {
                    return length - 20f;
                }

                length += 20f;
            }

            return currentLength;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 激光束碰撞检测
            Vector2 start = Projectile.Center;
            Vector2 end = start + new Vector2(MathF.Cos(LaserDirection), MathF.Sin(LaserDirection)) * MaxLength;

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, BeamWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Underworld.Fog == null)
                return false;

            DrawLaser();
            return false;
        }

        private void DrawLaser() {
            Texture2D beamTexture = Underworld.Fog;
            Vector2 start = Projectile.Center - Main.screenPosition;
            Vector2 direction = new Vector2(MathF.Cos(LaserDirection), MathF.Sin(LaserDirection));

            float drawLength = MaxLength;
            int segments = (int)(drawLength / 20f);

            Color beamColor = new Color(100, 150, 255);

            // 绘制激光段
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 segmentPos = start + direction * (i * 20f);

                // 淡入淡出效果
                float alpha = 0.6f;
                if (progress < 0.1f)
                    alpha *= progress / 0.1f;
                if (progress > 0.9f)
                    alpha *= (1f - progress) / 0.1f;

                float scale = BeamWidth / beamTexture.Width * 0.03f;

                // 主光束
                Main.spriteBatch.Draw(
                    beamTexture,
                    segmentPos,
                    null,
                    beamColor * alpha,
                    LaserDirection,
                    beamTexture.Size() * 0.5f,
                    new Vector2(0.4f, scale),
                    SpriteEffects.None,
                    0f
                );

                // 外层光晕
                Main.spriteBatch.Draw(
                    beamTexture,
                    segmentPos,
                    null,
                    beamColor * alpha * 0.3f,
                    LaserDirection,
                    beamTexture.Size() * 0.5f,
                    new Vector2(0.5f, scale * 1.5f),
                    SpriteEffects.None,
                    0f
                );
            }

            // 绘制激光起点
            DrawLaserStart(start);

            // 绘制激光终点
            Vector2 endPos = start + direction * drawLength;
            DrawLaserEnd(endPos);
        }

        private void DrawLaserStart(Vector2 position) {
            Texture2D texture = Underworld.Fog;
            Color color = new Color(150, 200, 255);

            float pulseScale = 1f + MathF.Sin(LaserTimer * 0.2f) * 0.2f;

            Main.spriteBatch.Draw(
                texture,
                position,
                null,
                color * 0.8f,
                LaserDirection + rotation,
                texture.Size() * 0.5f,
                0.8f * pulseScale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawLaserEnd(Vector2 position) {
            Texture2D texture = Underworld.Fog;
            Color color = new Color(120, 180, 255);

            float pulseScale = 1f + MathF.Sin(LaserTimer * 0.15f) * 0.3f;

            // 爆裂效果
            for (int i = 0; i < 3; i++) {
                float rotation = this.rotation + i * MathHelper.TwoPi / 3f;
                Main.spriteBatch.Draw(
                    texture,
                    position,
                    null,
                    color * 0.4f,
                    rotation,
                    texture.Size() * 0.5f,
                    (0.6f + i * 0.2f) * pulseScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private float rotation = 0f;
    }
}

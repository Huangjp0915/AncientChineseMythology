using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 符文能量球 — 金色和蓝色符文构成的球体，核心发光
    /// </summary>
    public class RunicEnergyOrbs : ModProjectile
    {
        private float spinPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // ai[0]==1: 符文封印模式——固定位置,ai[1]帧后引爆
            if (Projectile.ai[0] == 1f) {
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation += 0.06f;
                spinPhase += 0.1f;

                Projectile.ai[1]--;

                // 封印地面符文粒子——脉冲圆环
                float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 150f, 0f, 1f);
                if (Main.rand.NextBool(2)) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 20f + urgency * 15f;
                    Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, DustID.GoldFlame, 0, 0, 80, default, 1f + urgency);
                    d.noGravity = true;
                    d.velocity = (Projectile.Center - d.position).SafeNormalize(Vector2.Zero) * 2f;
                }
                if (urgency > 0.6f && Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 60, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(2, 2);
                }

                Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.45f, 0.15f) * (0.5f + urgency * 0.8f));

                // 倒计时结束——引爆
                if (Projectile.ai[1] <= 0) {
                    Projectile.Kill();
                }
                return;
            }

            // 默认: 飞行符文球
            Projectile.rotation += 0.1f;
            spinPhase += 0.15f;

            // 旋转符文粒子环绕
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, dustType, 0, 0, 120, default, 1.2f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.2f);
        }

        public override void OnKill(int timeLeft) {
            bool isSeal = Projectile.ai[0] == 1f;
            int count = isSeal ? 20 : 10;
            float scale = isSeal ? 2.5f : 1.5f;
            float speed = isSeal ? 8f : 5f;

            for (int i = 0; i < count; i++) {
                int dustType = i % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, scale);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(speed, speed);
            }

            // 封印引爆: 释放环状小弹幕
            if (isSeal && Main.netMode != NetmodeID.MultiplayerClient) {
                int projCount = 8;
                for (int i = 0; i < projCount; i++) {
                    float angle = MathHelper.TwoPi / projCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        Type, Projectile.damage / 2, 0f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 符印封锁模式: 引爆将近时由暗转亮的可读地纹光环 (暖金=危险预警)
            if (Projectile.ai[0] == 1f) {
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float urgency = MathHelper.Clamp(1f - Projectile.ai[1] / 150f, 0f, 1f);
                    Vector2 gPos = Projectile.Center - Main.screenPosition;
                    Vector2 gOrigin = glow.Size() / 2f;
                    float ringPulse = 0.5f + MathF.Sin(spinPhase * 3f) * 0.5f;

                    // 外环: 随引爆将近收缩+提亮
                    float outerScale = (0.55f - urgency * 0.18f + ringPulse * 0.06f) * 0.6f;
                    Color outer = Color.Lerp(new Color(180, 130, 40), new Color(255, 210, 90), urgency) * (0.35f + urgency * 0.5f);
                    sb.Draw(glow, gPos, null, outer, 0f, gOrigin, outerScale, SpriteEffects.None, 0f);

                    // 内核: 引爆临界白金闪
                    if (urgency > 0.6f) {
                        Color core = Color.Lerp(new Color(255, 220, 120), Color.White, (urgency - 0.6f) / 0.4f) * (urgency * 0.6f);
                        sb.Draw(glow, gPos, null, core, 0f, gOrigin, outerScale * 0.5f * ringPulse, SpriteEffects.None, 0f);
                    }
                }
            }

            float pulse = 1f + MathF.Sin(spinPhase * 4f) * 0.15f;

            // 残影
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(255, 200, 80), new Color(80, 130, 220), t) * (0.4f * (1f - t));
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.3f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = Color.Lerp(new Color(255, 215, 100), new Color(100, 150, 255), MathF.Sin(spinPhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }
}

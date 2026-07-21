using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 符文横扫光刃 — 大型弧形金蓝符文剑气 (新月刃)。
    /// ai[0] = 变体: 0=标准; 1=巨型断罪刃 (1.6x 尺寸, 更亮更重)
    /// ai[1] = 减速率: >0 时每帧 velocity *= ai[1] (刃墙缓速用)
    /// </summary>
    public class RunicCleaveWaves : ModProjectile
    {
        private float glowPhase;

        private bool IsGiant => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            glowPhase += 0.12f;

            if (IsGiant) {
                Projectile.scale = 1.6f;
                // 巨刃越飞越快 (复利加速 — 冲出去而非漂出去)
                if (Projectile.velocity.Length() < 26f)
                    Projectile.velocity *= 1.012f;
            }

            if (Projectile.ai[1] > 0f)
                Projectile.velocity *= Projectile.ai[1];

            // 拖尾粒子 ∝ 速度 (速度门控: 快弹更闹, 慢弹安静)
            float speed = Projectile.velocity.Length();
            int dustChance = speed > 14f ? 1 : 2;
            if (Main.rand.NextBool(dustChance)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, IsGiant ? 1.8f : 1.4f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.45f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(glowPhase * 3f) * 0.1f;
            float speed = Projectile.velocity.Length();
            float speedT = MathHelper.Clamp((speed - 6f) / 16f, 0f, 1f); // 残影透明度速度门控

            // 残影 — 只有快刃才拉出长尾
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(255, 200, 60), new Color(60, 120, 255), t)
                    * (0.5f * (1f - t) * (0.35f + speedT * 0.65f));
                trailColor.A = 0;
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.3f) * pulse, SpriteEffects.None, 0f);
            }

            // 前缘白热光晕 — 沿速度方向偏前, 强度随速度
            if (glow != null && speedT > 0.05f) {
                Vector2 headOffset = Projectile.velocity.SafeNormalize(Vector2.Zero) * 14f * Projectile.scale;
                Color heat = new Color(255, 245, 205) * (0.4f * speedT * (IsGiant ? 1.4f : 1f));
                heat.A = 0;
                sb.Draw(glow, drawPos + headOffset, null, heat, 0f, glow.Size() / 2f,
                    (IsGiant ? 1.3f : 0.85f) * pulse, SpriteEffects.None, 0f);
            }

            // 主体 — 巨刃叠一层白金过曝芯
            Color mainColor = Color.Lerp(new Color(255, 220, 100), new Color(100, 160, 255), MathF.Sin(glowPhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);
            if (IsGiant) {
                Color core = new Color(255, 245, 200, 0) * 0.55f;
                sb.Draw(texture, drawPos, null, core, Projectile.rotation, origin, Projectile.scale * pulse * 0.82f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}

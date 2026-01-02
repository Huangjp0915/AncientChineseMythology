using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨念弹 - 怨灵的主要追踪弹幕
    /// </summary>
    public class SpectreWraithBolt : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase = 0f;
        private float homingStrength = 0.03f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Projectile.scale = 0.3f;
            pulsePhase += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微追踪
            Player target = FindTarget();
            if (target != null && Projectile.timeLeft > 200) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            }

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), dustType);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 100;
            }

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreCyan.ToVector3() * 0.3f);
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 600f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(SpectreHelper.SpectreDeepCyan, SpectreHelper.SpectreCyan, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.5f + progress * 0.5f), SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;
            Color mainColor = SpectreHelper.SpectreCyan;

            // 光晕
            Color glowColor = mainColor;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.4f,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.3f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 120);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 30f, 1, 8);
        }
    }

    /// <summary>
    /// 灵魂链条 - 连接攻击弹幕
    /// </summary>
    public class SpectreSoulChain : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private float pulsePhase = 0f;
        private Vector2 targetPos;
        private bool initialized = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 100;
        }

        public override void AI() {
            if (!initialized) {
                targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                initialized = true;
            }

            pulsePhase += 0.15f;

            // 向目标位置移动
            Vector2 toTarget = (targetPos - Projectile.Center).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(Projectile.Center, targetPos);

            if (dist > 30f) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.08f);
            }
            else {
                // 到达目标后消散
                Projectile.velocity *= 0.9f;
                Projectile.alpha += 5;
                if (Projectile.alpha > 255) {
                    Projectile.Kill();
                }
            }

            Projectile.rotation += 0.1f;

            // 链条粒子
            SpectreHelper.CreateSoulChainParticles(Projectile.Center, targetPos, 0.5f);

            Lighting.AddLight(Projectile.Center, SpectreHelper.SpectreYellow.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制链条
            if (Projectile.oldPos.Length > 5 && Projectile.oldPos[5] != Vector2.Zero) {
                Vector2 chainStart = Projectile.oldPos[Math.Min(10, Projectile.oldPos.Length - 1)] + Projectile.Size / 2;
                SpectreHelper.DrawSoulChain(sb, chainStart, Projectile.Center,
                    SpectreHelper.SpectreYellow, 6f, pulsePhase * 60f);
            }

            // 绘制核心
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            SpectreHelper.DrawSpectreCore(sb, Projectile.Center,
                SpectreHelper.SpectreYellow, SpectreHelper.SpectreGold,
                pulse, pulsePhase);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slow, 90);
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 40f, 2, 10);
        }
    }

    /// <summary>
    /// 灵魂球 - 环形弹幕
    /// </summary>
    public class SpectreSoulOrb : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        private float pulsePhase = 0f;
        private int ColorType => (int)Projectile.ai[0]; // 0=青色, 1=黄色

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 100;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            Projectile.rotation += 0.08f;

            // 轻微加速
            if (Projectile.velocity.Length() < 12f) {
                Projectile.velocity *= 1.01f;
            }

            // 粒子
            if (Main.rand.NextBool(3)) {
                int dustType = ColorType == 0 ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.08f;
            }

            Color lightColor = ColorType == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.25f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            Color orbColor = ColorType == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = orbColor * progress * 0.4f;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.4f + progress * 0.6f), SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;

            // 光晕
            Color glowColor = orbColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.5f,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.4f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, orbColor,
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (ColorType == 0) {
                target.AddBuff(BuffID.Frostburn, 90);
            }
            else {
                target.AddBuff(BuffID.OnFire, 90);
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                int dustType = ColorType == 0 ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 哀嚎波 - 大范围扩散弹幕
    /// </summary>
    public class SpectreWailingWave : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreWave";

        private float pulsePhase = 0f;
        private float growthScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 200;
            Projectile.alpha = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            growthScale = MathHelper.Lerp(growthScale, 1.5f, 0.02f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 逐渐加速
            if (Projectile.velocity.Length() < 14f) {
                Projectile.velocity *= 1.015f;
            }

            // 波形运动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(pulsePhase * 0.8f) * 3f;
            Projectile.position += perpendicular * wave;

            // 粒子
            SpectreHelper.CreateSpectreTrail(Projectile.Center, Projectile.velocity, growthScale);

            // 发光
            Color lightColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.4f * growthScale);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 多层拖尾
            for (int layer = 0; layer < 2; layer++) {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float progress = 1f - i / (float)Projectile.oldPos.Length;
                    float layerAlpha = progress * (layer == 0 ? 0.25f : 0.4f);

                    Color trailColor = layer == 0
                        ? SpectreHelper.SpectreDeepCyan
                        : Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, progress);
                    trailColor *= layerAlpha;
                    trailColor.A = 0;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                    float trailScale = growthScale * (0.4f + progress * 0.6f) * (layer == 0 ? 1.5f : 1f);

                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Color mainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.4f);

            // 光晕
            Color glowColor = mainColor;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor * 0.4f,
                Projectile.rotation, origin, growthScale * pulse * 1.5f, SpriteEffects.None, 0);

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, growthScale * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Confused, 60);
            target.AddBuff(BuffID.Chilled, 120);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.2f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 50f * growthScale, 2, 12);
        }
    }
}

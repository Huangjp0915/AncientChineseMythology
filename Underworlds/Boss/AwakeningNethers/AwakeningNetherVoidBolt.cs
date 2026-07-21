using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒幽冥龙 - 虚空弹 (V3 公平版)
    /// 公平阀门三件套: 出膛 wind-up (前 30f 速度 40%→100%, 杀 telefrag)、
    /// 追踪 210f 硬截止、近身 180px 内放弃追踪直线掠过 (奖励贴身走位)。
    /// 配色收敛: 觉醒紫主体 + 鬼绿芯。
    /// </summary>
    public class AwakeningNetherVoidBolt : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        public const int HomingCutoff = 210; // 追踪硬截止 (帧)
        private const int WindupTime = 30;

        private float pulsePhase;
        private int age;

        // 是否为强化版本 (狂暴)
        private bool IsEnhanced => Projectile.ai[0] > 0;
        // 追踪强度等级
        private int TrackingLevel => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 330;
            Projectile.alpha = 50;
        }

        public override void AI() {
            age++;
            pulsePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 出膛 wind-up: 前 30f 由 40% 速度爬升到 100% (转阶段/齐射瞬间必然可逃)
            float windup = MathHelper.Lerp(0.4f, 1f, MathHelper.Clamp(age / (float)WindupTime, 0f, 1f));
            float targetSpeed = (IsEnhanced ? 17f : 13f) * windup;

            Player target = FindTarget();
            bool mayHome = age < HomingCutoff && target != null
                && target.Distance(Projectile.Center) > 180f; // 近身放弃追踪 — 直线掠过
            if (mayHome) {
                float homing = (0.035f + TrackingLevel * 0.012f) * windup;
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * targetSpeed, homing);
            }
            else {
                // 截止后保持直线, 只做速度整定
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * MathHelper.Lerp(
                    Projectile.velocity.Length(), targetSpeed, 0.06f);
            }

            if (!Main.dedServ) {
                if (Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = -Projectile.velocity * 0.15f;
                    d.alpha = 80;
                }
                if (Main.rand.NextBool(4)) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch);
                    d.noGravity = true;
                    d.scale = 0.9f;
                    d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                }
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.5f);
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 1200f;
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
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;
            Vector2 origin = glow.Size() / 2f;

            // 单层拖尾 (紫)
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color tc = AwakeningNetherHelper.AwakeningPurple * (progress * 0.4f);
                tc.A = 0;
                sb.Draw(glow, Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition, null, tc,
                    0f, origin, (0.5f + progress * 0.6f) * (IsEnhanced ? 1.3f : 1f), SpriteEffects.None, 0);
            }

            // 紫壳 + 鬼绿芯 + 白点 (统一深渊色语言)
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;
            float scaleMod = IsEnhanced ? 1.35f : 1.05f;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                AwakeningNetherHelper.AwakeningPurple with { A = 0 } * 0.8f,
                0f, origin, 1.35f * pulse * scaleMod, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                TelegraphColors.GhostGreen with { A = 0 } * 0.75f,
                0f, origin, 0.8f * pulse * scaleMod, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * 0.55f,
                0f, origin, 0.36f * pulse * scaleMod, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(IsEnhanced ? 3 : 2);
            target.AddBuff(BuffID.Darkness, 180);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustID.CursedTorch : DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                d.alpha = 50;
            }
        }
    }

    /// <summary>
    /// 觉醒幽冥龙 - 灵魂弹 (V3)
    /// 环形一次性爆发的灵魂弹幕。出膛 wind-up 30f (40%→100%), 配色收敛为紫壳鬼绿芯。
    /// ai[0]: 0=直线 1=螺旋。
    /// </summary>
    public class AwakeningNetherSoulOrb : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float pulsePhase;
        private float spiralAngle;
        private int age;

        private int SpreadMode => (int)Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 80;
        }

        public override void AI() {
            age++;
            pulsePhase += 0.12f;
            spiralAngle += 0.05f;

            // 出膛 wind-up (公平阀门): 风暴爆发瞬间必然可逃
            float windup = MathHelper.Lerp(0.4f, 1f, MathHelper.Clamp(age / 30f, 0f, 1f));

            if (SpreadMode == 1) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Projectile.position += perpendicular * MathF.Sin(spiralAngle) * 3f * windup;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            float speed = Projectile.velocity.Length();
            float targetSpeed = 13f * windup;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY)
                * MathHelper.Lerp(speed, targetSpeed, 0.05f);

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.CursedTorch);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;
            Vector2 origin = glow.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color tc = AwakeningNetherHelper.AwakeningPurple * (progress * 0.35f);
                tc.A = 0;
                sb.Draw(glow, Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition, null, tc,
                    0f, origin, 0.4f + progress * 0.5f, SpriteEffects.None, 0);
            }

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                AwakeningNetherHelper.AwakeningPurple with { A = 0 } * 0.8f,
                0f, origin, 1.1f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                TelegraphColors.GhostGreen with { A = 0 } * 0.7f,
                0f, origin, 0.62f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                Color.White with { A = 0 } * 0.45f,
                0f, origin, 0.3f * pulse, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(1);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
            }
        }
    }
}

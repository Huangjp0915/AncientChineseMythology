using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙雾气弹 - 基础攻击弹幕
    /// 白色迷幻的雾气弹，具有轻微追踪
    /// </summary>
    public class AncestralMistBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float mistPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.alpha = 0;
        }

        public override void AI() {
            mistPhase += 0.15f;

            // 轻微追踪
            if (Projectile.timeLeft > 180) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.015f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 雾气飘动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(mistPhase * 2f) * 0.8f;
            Projectile.position += perpendicular * drift;

            // 雾气粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 200, new Color(240, 248, 255), 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.5f, 0.5f);
                Main.dust[dust].fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.85f, 0.95f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(mistPhase * 2f) * 0.2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(200, 220, 255), 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.5f * progress, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerColor = new Color(220, 235, 255) * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.8f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 255) * 0.7f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 螺旋龙魂碎片 - 螺旋下降时发射的弹幕
    /// </summary>
    public class SpiralSoulFragment : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float spiralPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            spiralPhase += 0.12f;
            Projectile.rotation += 0.15f;

            // 螺旋运动
            float spiralOffset = MathF.Sin(spiralPhase) * 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * spiralOffset;

            // 龙魂粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Clentaminator_Cyan, 0, 0, 150, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.8f, 0.9f) * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(200, 230, 255) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, 0.35f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            float pulse = 1f + MathF.Sin(spiralPhase * 2f) * 0.15f;
            Color mainColor = new Color(230, 245, 255);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, 0.4f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 150, Color.White, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 祖龙灵魂碎片 - 灵魂风暴召唤的环绕攻击
    /// </summary>
    public class AncestralSoulFragment : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float OrbitAngle => ref Projectile.ai[1];
        private float orbitRadius = 150f;
        private float pulsePhase;
        private int attackTimer = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            attackTimer++;

            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            // 环绕运动
            OrbitAngle += 0.04f;
            orbitRadius = MathHelper.Lerp(orbitRadius, 200f + MathF.Sin(pulsePhase) * 30f, 0.02f);

            Vector2 targetPos = owner.Center + OrbitAngle.ToRotationVector2() * orbitRadius;
            Projectile.velocity = (targetPos - Projectile.Center) * 0.15f;

            Projectile.rotation = OrbitAngle + MathHelper.PiOver2;

            // 周期性向玩家发射
            if (attackTimer % 90 == 0 && attackTimer > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toPlayer = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        toPlayer * 8f,
                        ModContent.ProjectileType<AncestralMistBolt>(),
                        Projectile.damage / 2,
                        1f
                    );

                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                }
            }

            // 龙魂粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Cloud, 0, 0, 180, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.85f, 0.95f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 235, 255) * progress * 0.3f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, 0.4f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 光晕
            Color glowColor = new Color(200, 220, 255) * 0.4f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glowColor, Projectile.rotation, origin, 0.6f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 255) * 0.7f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin, 0.4f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 追踪龙魂球 - 盘龙缠绕时发射的追踪弹
    /// </summary>
    public class HomingSoulOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float orbPhase;
        private bool isHoming = true;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            orbPhase += 0.12f;

            // 强追踪
            if (isHoming && Projectile.timeLeft > 100) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float turnSpeed = Main.expertMode ? 0.06f : 0.04f;
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, turnSpeed);

                    float speed = Projectile.velocity.Length();
                    if (speed < 12f) speed += 0.1f;

                    Projectile.velocity = newAngle.ToRotationVector2() * speed;

                    // 接近目标时停止追踪
                    if (toTarget.Length() < 50f) {
                        isHoming = false;
                    }
                }
            }

            Projectile.rotation += 0.1f;

            // 龙魂光粒
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 150, Color.White, 0.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.9f, 1f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(orbPhase * 3f) * 0.15f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(180, 210, 255), 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.4f * progress, SpriteEffects.None, 0f);
            }

            // 外层
            Color outerColor = new Color(200, 225, 255) * 0.5f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.6f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 255) * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.35f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 120, Color.White, 1.3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 尾部扫击波 - 龙尾攻击发射的波浪
    /// </summary>
    public class TailSweepWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wavePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            wavePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 波浪飘动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(wavePhase * 2f) * 2f;
            Projectile.position += perpendicular * wave * 0.5f;

            // 渐渐消散
            if (Projectile.timeLeft < 30) {
                Projectile.scale *= 0.97f;
            }

            // 仙气粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 200, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1, 1);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.85f, 0.95f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(wavePhase * 2f) * 0.1f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 240, 255) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(0.6f * progress, 0.15f * progress * Projectile.scale), SpriteEffects.None, 0f);
            }

            // 外层
            Color outerColor = new Color(200, 230, 255) * 0.5f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, Projectile.rotation, origin,
                new Vector2(0.8f, 0.25f * Projectile.scale) * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 255) * 0.7f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin,
                new Vector2(0.6f, 0.12f * Projectile.scale) * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Cloud, vel.X, vel.Y, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 祖龙吐息激光 - 大型追踪激光
    /// </summary>
    public class AncestralDragonBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2500f;
        private const int LaserDuration = 120;

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];
        private float laserWidth = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3500;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
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

            // 缓慢追踪
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                float targetAngle = (target.Center - Projectile.Center).ToRotation();
                float turnSpeed = Main.expertMode ? 0.02f : 0.015f;
                LaserAngle = MathHelper.Lerp(LaserAngle, targetAngle, turnSpeed);
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

            // 激光粒子
            if (Main.netMode != NetmodeID.Server) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 8; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(20 * laserWidth, 20 * laserWidth);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Cloud,
                        1 => DustID.WhiteTorch,
                        _ => DustID.Clentaminator_Cyan
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, Color.White, 2f * laserWidth);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 3f;
                }
            }

            // 发光
            for (int i = 0; i < 12; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(0.9f, 0.95f, 1f) * 1.5f * laserWidth);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 50f * laserWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            // 多层激光
            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = (0.3f + layer * 0.2f) * laserWidth;
                float layerAlpha = 0.9f - layer * 0.2f;

                // 白色到淡青的渐变
                Color layerColor = layer switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(240, 250, 255),
                    2 => new Color(220, 240, 255),
                    _ => new Color(200, 225, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 scale = new Vector2(LaserLength / laserTex.Width, layerWidth);
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);
            }

            // 起点光球
            if (ACMAsset.LightShot != null) {
                Color orbColor = new Color(255, 255, 255) * laserWidth * 0.8f;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 3f * laserWidth, SpriteEffects.None, 0f);
            }

            // 星光效果
            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = new Color(230, 245, 255) * laserWidth * 0.6f;
                sparkleColor.A = 0;
                float sparkleRot = (float)Main.GameUpdateCount * 0.1f;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, sparkleRot,
                    ACMAsset.Sparkle.Size() / 2f, 2.5f * laserWidth, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 消散粒子
            Vector2 laserDir = LaserAngle.ToRotationVector2();
            for (int i = 0; i < 30; i++) {
                float dist = i * LaserLength / 30f;
                Vector2 dustPos = Projectile.Center + laserDir * dist;
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

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
            Texture2D laserTex = ACMAsset.SlashBurst;
            if (laserTex == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            // SlashBurst纹理朝向上方,底部为起点,需要原点放底边中央并加PiOver2
            Vector2 origin = new Vector2(laserTex.Width / 2f, laserTex.Height);
            float drawRot = LaserAngle + MathHelper.PiOver2;

            // 多层激光:内核白色,外层淡青,越外层越宽越淡
            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = (0.25f + layer * 0.22f) * laserWidth;
                float layerAlpha = 0.9f - layer * 0.2f;

                Color layerColor = layer switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(240, 250, 255),
                    2 => new Color(210, 235, 255),
                    _ => new Color(180, 215, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Vector2 scale = new Vector2(layerWidth, LaserLength / laserTex.Height);
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, drawRot, origin, scale, SpriteEffects.None, 0f);
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

    /// <summary>
    /// 龙鳞爆裂弹 - 缓慢旋转飞行,延时后分裂为6道雾气弹
    /// 用于制造空间封锁,迫使玩家及时走位
    /// </summary>
    public class DragonScaleShard : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int FuseTime = 90;
        private ref float SpinPhase => ref Projectile.ai[0];
        private ref float FuseTimer => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FuseTime + 30;
        }

        public override void AI() {
            SpinPhase += 0.18f;
            FuseTimer++;

            // 速度衰减营造悬停感
            Projectile.velocity *= 0.985f;
            Projectile.rotation = SpinPhase;

            // 即将爆裂时收缩并闪烁
            float fuseRatio = FuseTimer / FuseTime;
            Projectile.scale = 1f + MathF.Sin(fuseRatio * MathHelper.TwoPi * 3f) * 0.15f * fuseRatio;

            if (Main.netMode != NetmodeID.Server) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Frost, 0, 0, 180, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = SpinPhase.ToRotationVector2() * 2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.75f, 0.85f, 1f) * (0.3f + fuseRatio * 0.5f));

            // 到时爆裂
            if (FuseTimer >= FuseTime && Main.netMode != NetmodeID.MultiplayerClient) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.6f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + SpinPhase;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        angle.ToRotationVector2() * 9f,
                        ModContent.ProjectileType<AncestralMistBolt>(),
                        Projectile.damage,
                        0.5f
                    );
                }
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            if (tex == null) return false;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float fuseRatio = FuseTimer / FuseTime;
            Color outerColor = Color.Lerp(new Color(210, 235, 255), new Color(255, 200, 220), fuseRatio) * 0.7f;
            outerColor.A = 0;

            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, Projectile.rotation, origin, 0.9f * Projectile.scale, SpriteEffects.None, 0f);

            Color coreColor = new Color(255, 255, 255) * (0.7f + fuseRatio * 0.3f);
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, -Projectile.rotation, origin, 0.55f * Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Frost, vel.X, vel.Y, 150, Color.White, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 祖龙符文 - 悬浮的符文,预警后向上爆发能量柱
    /// 用于制造地面封锁区域,奖励预判走位
    /// </summary>
    public class AncestralSoulSigil : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WarnTime = 75;
        private const int EruptTime = 60;
        private const float PillarHeight = 700f;
        private const float PillarWidth = 90f;

        private ref float Timer => ref Projectile.ai[0];
        private ref float Stage => ref Projectile.ai[1];
        private float rotPhase;

        public override void SetStaticDefaults() { }

        public override void SetDefaults() {
            Projectile.width = (int)PillarWidth;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + EruptTime + 5;
        }

        public override void AI() {
            Timer++;
            rotPhase += 0.08f;

            if (Stage == 0f) {
                // 预警阶段:符文凝聚
                if (Timer >= WarnTime) {
                    Stage = 1f;
                    Timer = 0f;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f }, Projectile.Center);
                        for (int i = 0; i < 40; i++) {
                            float ang = MathHelper.TwoPi * i / 40f;
                            Vector2 vel = new Vector2(MathF.Cos(ang) * 3f, -Main.rand.NextFloat(4f, 10f));
                            int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 100, Color.White, 2.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                }

                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 offset = ang.ToRotationVector2() * PillarWidth * 0.6f;
                    int dust = Dust.NewDust(Projectile.Center + offset, 0, 0, DustID.Clentaminator_Cyan, 0, 0, 180, Color.White, 1.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -offset.SafeNormalize(Vector2.Zero) * 2f;
                }
            }
            else {
                // 爆发阶段:垂直能量柱
                if (Timer >= EruptTime) {
                    Projectile.Kill();
                    return;
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 pillarPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-PillarWidth * 0.5f, PillarWidth * 0.5f), -Main.rand.NextFloat(PillarHeight));
                        int dust = Dust.NewDust(pillarPos, 0, 0, DustID.WhiteTorch, 0, -Main.rand.NextFloat(3f, 8f), 80, Color.White, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }

                Lighting.AddLight(Projectile.Center + new Vector2(0, -PillarHeight / 2f), new Vector3(1f, 1f, 1f) * 1.2f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.95f, 1f) * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Stage == 0f) {
                return false;
            }
            Rectangle pillarRect = new Rectangle(
                (int)(Projectile.Center.X - PillarWidth / 2f),
                (int)(Projectile.Center.Y - PillarHeight),
                (int)PillarWidth,
                (int)PillarHeight);
            return pillarRect.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = ACMAsset.SoftGlow ?? ACMAsset.LightShot;
            Texture2D sparkle = ACMAsset.Sparkle;
            if (glow == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (Stage == 0f) {
                // 预警符文
                float warnRatio = Timer / WarnTime;
                Color warnColor = new Color(220, 240, 255) * (0.4f + warnRatio * 0.5f);
                warnColor.A = 0;
                Main.spriteBatch.Draw(glow, drawPos, null, warnColor, 0f, glow.Size() / 2f, 1.5f * warnRatio, SpriteEffects.None, 0f);

                if (sparkle != null) {
                    Color runeColor = new Color(255, 255, 255) * warnRatio * 0.8f;
                    runeColor.A = 0;
                    Main.spriteBatch.Draw(sparkle, drawPos, null, runeColor, rotPhase, sparkle.Size() / 2f, 0.8f * (1f + MathF.Sin(warnRatio * MathHelper.Pi * 4f) * 0.15f), SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(sparkle, drawPos, null, runeColor * 0.7f, -rotPhase * 1.3f, sparkle.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
                }
            }
            else {
                // 能量柱
                float eruptRatio = Timer / EruptTime;
                float alpha = eruptRatio < 0.15f ? eruptRatio / 0.15f : (eruptRatio > 0.8f ? (1f - eruptRatio) / 0.2f : 1f);

                Texture2D pixel = ACMAsset.BlankStar ?? Main.Assets.Request<Texture2D>("Images/MagicPixel").Value;
                Vector2 pillarOrigin = new Vector2(pixel.Width / 2f, pixel.Height);
                Vector2 scale = new Vector2(PillarWidth / pixel.Width, PillarHeight / pixel.Height);

                // 三层能量柱
                for (int layer = 2; layer >= 0; layer--) {
                    float layerScale = 1f - layer * 0.25f;
                    Color layerColor = layer switch {
                        0 => new Color(255, 255, 255),
                        1 => new Color(220, 240, 255),
                        _ => new Color(180, 210, 255)
                    };
                    layerColor *= alpha * (0.9f - layer * 0.25f);
                    layerColor.A = 0;
                    Vector2 layerScaleFinal = new Vector2(scale.X * layerScale, scale.Y);
                    Main.spriteBatch.Draw(pixel, drawPos, null, layerColor, 0f, pillarOrigin, layerScaleFinal, SpriteEffects.None, 0f);
                }

                // 底部辉光
                Color baseGlow = new Color(255, 255, 255) * alpha;
                baseGlow.A = 0;
                Main.spriteBatch.Draw(glow, drawPos, null, baseGlow, 0f, glow.Size() / 2f, 2f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 阴阳双珠 - 两颗绕共同中心旋转的灵珠,结束后分离追踪玩家
    /// 在旋转期玩家需预判分离方向,可读性与压力兼具
    /// </summary>
    public class YinYangBinderOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        // ai[0]: 配对的另一颗的whoAmI; ai[1]: 自身相位偏移(0或PI)
        private ref float PartnerIndex => ref Projectile.ai[0];
        private ref float PhaseOffset => ref Projectile.ai[1];

        private const int BindTime = 100;
        private int homingStartedAt = -1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 500;
        }

        public override void AI() {
            float age = 500 - Projectile.timeLeft;

            if (age < BindTime) {
                // 绑定期:围绕中心点旋转
                if (PartnerIndex >= 0 && PartnerIndex < Main.maxProjectiles) {
                    Projectile partner = Main.projectile[(int)PartnerIndex];
                    if (partner.active && partner.type == Type) {
                        Vector2 mid = (Projectile.Center + partner.Center) / 2f;
                        float angle = age * 0.12f + PhaseOffset;
                        float radius = 60f + age * 1.2f;
                        Vector2 desired = mid + angle.ToRotationVector2() * radius;
                        Projectile.velocity = (desired - Projectile.Center) * 0.35f;
                    }
                }
                Projectile.rotation += 0.2f;
            }
            else {
                // 分离期:缓慢追踪玩家
                if (homingStartedAt < 0) {
                    homingStartedAt = (int)age;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);
                        for (int i = 0; i < 16; i++) {
                            float ang = MathHelper.TwoPi * i / 16f;
                            Vector2 vel = ang.ToRotationVector2() * 5f;
                            int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 120, Color.White, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    // 分离期速度初始化
                    if (Projectile.velocity.Length() < 3f) {
                        Projectile.velocity = Projectile.rotation.ToRotationVector2() * 6f;
                    }
                }

                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float currentAngle = Projectile.velocity.ToRotation();
                    float targetAngle = toTarget.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.025f);
                    float speed = Math.Min(Projectile.velocity.Length() + 0.12f, 14f);
                    Projectile.velocity = newAngle.ToRotationVector2() * speed;
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dustType = PhaseOffset < 1f ? DustID.WhiteTorch : DustID.Clentaminator_Cyan;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 150, Color.White, 1.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.9f, 1f) * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 白/青阴阳配色
            Color coreCol = PhaseOffset < 1f ? new Color(255, 255, 255) : new Color(200, 230, 255);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = coreCol * progress * 0.35f;
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.55f * progress, SpriteEffects.None, 0f);
            }

            Color outerColor = coreCol * 0.5f;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.9f, SpriteEffects.None, 0f);

            Color inner = new Color(255, 255, 255) * 0.9f;
            inner.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, inner, 0f, origin, 0.45f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 150, Color.White, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 双龙灵链 - 连接两颗头部的电链状激光,接触造成伤害
    /// 双龙协同专用攻击,迫使玩家在两龙之间的通道中穿梭
    /// </summary>
    public class SoulTetherChain : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float HeadAIndex => ref Projectile.ai[0];
        private ref float HeadBIndex => ref Projectile.ai[1];

        private const int Duration = 360;

        public override void SetStaticDefaults() { }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Duration;
        }

        private bool TryGetHeads(out NPC a, out NPC b) {
            a = b = null;
            int ia = (int)HeadAIndex;
            int ib = (int)HeadBIndex;
            if (ia < 0 || ia >= Main.maxNPCs || ib < 0 || ib >= Main.maxNPCs) return false;
            a = Main.npc[ia];
            b = Main.npc[ib];
            return a.active && b.active;
        }

        public override void AI() {
            if (!TryGetHeads(out NPC a, out NPC b)) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = (a.Center + b.Center) / 2f;

            // 沿链产生粒子
            if (Main.netMode != NetmodeID.Server) {
                Vector2 dir = (b.Center - a.Center);
                float len = dir.Length();
                dir /= len;
                int count = (int)(len / 60f);
                for (int i = 0; i < count; i++) {
                    if (!Main.rand.NextBool(3)) continue;
                    Vector2 pos = a.Center + dir * (i * 60f + Main.rand.NextFloat(60f));
                    int dustType = Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan;
                    int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 150, Color.White, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloatDirection() * 2f;
                }
                // 链条光源
                for (int i = 1; i < count; i++) {
                    Lighting.AddLight(a.Center + dir * (i * 60f), new Vector3(0.8f, 0.85f, 1f) * 0.8f);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!TryGetHeads(out NPC a, out NPC b)) return false;
            float alpha = GetAlpha();
            if (alpha < 0.4f) return false;
            float point = 0f;
            float width = 28f * MathHelper.Clamp(alpha, 0f, 1f);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a.Center, b.Center, width, ref point);
        }

        private float GetAlpha() {
            int age = Duration - Projectile.timeLeft;
            if (age < 30) return age / 30f;
            if (Projectile.timeLeft < 30) return Projectile.timeLeft / 30f;
            return 1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!TryGetHeads(out NPC a, out NPC b)) return false;
            Texture2D tex = ACMAsset.LightningBranch;
            if (tex == null) return false;

            float alpha = GetAlpha();
            Vector2 from = a.Center - Main.screenPosition;
            Vector2 to = b.Center - Main.screenPosition;
            Vector2 delta = to - from;
            float len = delta.Length();
            float rot = delta.ToRotation() + MathHelper.PiOver2;
            // LightningBranch纹理朝上,以底部中心为起点沿着AB方向延伸
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height);

            // 轻微抖动模拟电弧不稳定感
            float jitter = MathF.Sin((float)Main.GameUpdateCount * 0.4f + Projectile.whoAmI) * 0.03f;

            // 多层电链
            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = (0.18f + layer * 0.16f) * alpha;
                float layerAlpha = (0.9f - layer * 0.2f) * alpha;
                Color layerColor = layer switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(240, 250, 255),
                    2 => new Color(200, 225, 255),
                    _ => new Color(160, 195, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;
                Vector2 scale = new Vector2(layerWidth + jitter * layer * 0.1f, len / tex.Height);
                Main.spriteBatch.Draw(tex, from, null, layerColor, rot, origin, scale, SpriteEffects.None, 0f);
            }

            // 端点光球
            if (ACMAsset.LightShot != null) {
                Color endColor = new Color(255, 255, 255) * alpha * 0.8f;
                endColor.A = 0;
                Vector2 orbOrigin = ACMAsset.LightShot.Size() / 2f;
                Main.spriteBatch.Draw(ACMAsset.LightShot, from, null, endColor, 0f, orbOrigin, 1.5f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(ACMAsset.LightShot, to, null, endColor, 0f, orbOrigin, 1.5f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 阴阳超载脉冲 (Yin-Yang Overdrive) — 狂暴终曲强制机制控制弹。
    /// 围绕竞技场中心张开一圈阴阳灵珠环 (留一道缓慢旋转的"安全缝", 翠玉色); 蓄力满后环身转赤红并
    /// 释放一次**全屏魂蚀脉冲**: 此刻**不在安全缝内**的玩家被抽走当前/上限血量的固定百分比 (失败惩罚)。
    /// 红色只在致命释放帧出现; 蓄力期为主题青白 + 翠玉安全缝。每客户端只对本地玩家结算 (MP 安全)。
    /// </summary>
    public class YinYangOverdrivePulse : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int TelegraphDur = 150;
        private const int LethalFrame = TelegraphDur;
        private const int FadeDur = 26;
        private const float RingRadius = 540f;
        private const float GapHalfWidth = 0.62f;   // 安全缝半角(弧度)
        private const int OrbCount = 22;

        private ref float GapBaseAngle => ref Projectile.ai[0];
        private bool hasPulsed;

        public override void SetStaticDefaults() { }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;     // 不靠接触造成伤害, 仅在释放帧手动结算
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TelegraphDur + LethalFrame + FadeDur;
        }

        private float Age => (TelegraphDur + LethalFrame + FadeDur) - Projectile.timeLeft;
        private float GapCenter => GapBaseAngle + Age * 0.012f;

        public override void AI() {
            float age = Age;

            // 蓄力期: 向心粒子 + 音效渐强
            if (Main.netMode != NetmodeID.Server && age < TelegraphDur && age % 6 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * RingRadius;
                int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan,
                    0, 0, 120, Color.White, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
            }

            if (Main.netMode != NetmodeID.Server && (int)age == TelegraphDur - 30) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);
            }

            // 致命释放: 全屏魂蚀脉冲 (每客户端结算本地玩家)
            if (age >= LethalFrame && !hasPulsed) {
                hasPulsed = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f, Volume = 1.1f }, Projectile.Center);
                    ACMUtils.AddScreenShake(9f);
                    for (int i = 0; i < 50; i++) {
                        float a = MathHelper.TwoPi * i / 50f;
                        Vector2 vel = a.ToRotationVector2() * Main.rand.NextFloat(6f, 16f);
                        int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Clentaminator_Cyan, vel.X, vel.Y, 80, Color.White, 2f);
                        Main.dust[dust].noGravity = true;
                    }

                    Player p = Main.LocalPlayer;
                    if (p != null && p.active && !p.dead && !p.creativeGodMode) {
                        float ang = (p.Center - Projectile.Center).ToRotation();
                        float diff = MathHelper.WrapAngle(ang - GapCenter);
                        if (Math.Abs(diff) > GapHalfWidth) {
                            int drain = Math.Max(60, (int)(p.statLifeMax2 * 0.28f));
                            p.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                                Terraria.Localization.NetworkText.FromLiteral(p.name + " was drained by the Ancestral Dragon Soul.")),
                                drain, 0, dodgeable: false);
                        }
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.7f, 0.95f) * 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = ACMAsset.LightShot ?? ACMAsset.SoftGlow;
            if (glow == null)
                return false;

            float age = Age;
            float charge = MathHelper.Clamp(age / TelegraphDur, 0f, 1f);
            bool lethal = age >= LethalFrame;
            float fade = age >= LethalFrame ? MathHelper.Clamp(1f - (age - LethalFrame) / FadeDur, 0f, 1f) : 1f;

            Vector2 center = Projectile.Center - Main.screenPosition;
            float gapCenter = GapCenter;

            for (int i = 0; i < OrbCount; i++) {
                float a = MathHelper.TwoPi * i / OrbCount;
                float diff = MathHelper.WrapAngle(a - gapCenter);
                bool inGap = Math.Abs(diff) < GapHalfWidth;

                Vector2 pos = center + a.ToRotationVector2() * RingRadius;
                float orbPulse = 0.8f + MathF.Sin(age * 0.18f + i) * 0.2f;

                Color col;
                if (inGap) {
                    col = TelegraphColors.Safe;      // 安全缝: 翠玉
                }
                else if (lethal) {
                    col = TelegraphColors.Lethal;    // 致命: 纯红 (仅释放帧)
                }
                else {
                    // 阴阳: 白/青交替, 蓄力越满越亮
                    col = (i % 2 == 0) ? Color.White : new Color(150, 215, 255);
                    col = Color.Lerp(col, new Color(255, 150, 170), charge * 0.4f);
                }
                col *= fade * (0.6f + charge * 0.4f);
                col.A = 0;
                spriteBatch_DrawOrb(glow, pos, col, (inGap ? 0.7f : 0.95f) * orbPulse);
            }

            // 安全缝指示弧 (翠玉光带)
            if (!lethal) {
                Texture2D spk = ACMAsset.Sparkle;
                if (spk != null) {
                    Vector2 gapPos = center + gapCenter.ToRotationVector2() * RingRadius;
                    Color sc = TelegraphColors.Safe * (0.4f + charge * 0.5f);
                    sc.A = 0;
                    Main.spriteBatch.Draw(spk, gapPos, null, sc, age * 0.05f, spk.Size() / 2f, 1.4f, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        private static void spriteBatch_DrawOrb(Texture2D tex, Vector2 pos, Color col, float scale) {
            Main.spriteBatch.Draw(tex, pos, null, col, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
            Color core = Color.White * (col.A == 0 ? 0.5f : 0.5f);
            core.A = 0;
            Main.spriteBatch.Draw(tex, pos, null, core * 0.5f, 0f, tex.Size() / 2f, scale * 0.55f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 太初回拢扫线 (Primordial Recall Beam) — 双魂回拢 i-frame 过场中的处决扫线。
    /// 绕竞技场中心缓慢旋转的一条贯穿直径; 先以金白 (Holy) 拉直预警, 蓄满后转赤红 (Lethal) 横扫造成伤害。
    /// 两条 90° 错位 = X 形处决线。Boss 此刻无敌, 但玩家须读线走位。
    /// </summary>
    public class PrimordialRecallBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float BeamLength = 1700f;
        private const int TelegraphDur = 80;
        private const int LethalDur = 130;

        private ref float AngleOffset => ref Projectile.ai[0];   // X 错位 (0 / PiOver2)
        private ref float SpinSign => ref Projectile.ai[1];      // 旋转方向

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TelegraphDur + LethalDur;
        }

        private float Age => (TelegraphDur + LethalDur) - Projectile.timeLeft;
        private bool Lethal => Age >= TelegraphDur;
        private float CurrentAngle => AngleOffset + Age * 0.010f * (SpinSign >= 0 ? 1f : -1f);

        public override void AI() {
            // 中心由 spawn 时设定, 保持不动 (竞技场中心)
            Projectile.velocity = Vector2.Zero;

            if (Lethal && Main.netMode != NetmodeID.Server) {
                Vector2 dir = CurrentAngle.ToRotationVector2();
                for (int s = -1; s <= 1; s += 2) {
                    for (int i = 0; i < 6; i++) {
                        float dist = Main.rand.NextFloat(BeamLength);
                        Vector2 pos = Projectile.Center + dir * dist * s;
                        int dust = Dust.NewDust(pos, 0, 0, DustID.RedTorch, 0, 0, 120, Color.White, 1.6f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloatDirection() * 2f;
                    }
                }
            }

            Vector2 ldir = CurrentAngle.ToRotationVector2();
            for (int i = -6; i <= 6; i++) {
                Lighting.AddLight(Projectile.Center + ldir * (i * 130), new Vector3(1f, Lethal ? 0.4f : 0.85f, Lethal ? 0.4f : 0.7f) * 0.8f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Lethal)
                return false;
            float point = 0f;
            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 a = Projectile.Center - dir * BeamLength;
            Vector2 b = Projectile.Center + dir * BeamLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 44f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SlashBurst;
            if (tex == null)
                return false;

            float age = Age;
            float grow = Lethal ? 1f : MathHelper.Clamp(age / TelegraphDur, 0f, 1f);
            float fade = Projectile.timeLeft < 24 ? Projectile.timeLeft / 24f : 1f;
            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 center = Projectile.Center - Main.screenPosition;
            float drawRot = CurrentAngle + MathHelper.PiOver2;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height);

            Color baseCol = Lethal ? TelegraphColors.Lethal : TelegraphColors.Holy;

            // 双向直径: 各画一条半线
            for (int s = -1; s <= 1; s += 2) {
                float halfRot = drawRot + (s < 0 ? MathHelper.Pi : 0f);
                for (int layer = 3; layer >= 0; layer--) {
                    float lw = (0.20f + layer * 0.16f) * grow * (Lethal ? 1f : 0.4f);
                    float la = (0.85f - layer * 0.2f) * fade;
                    Color lc = Color.Lerp(Color.White, baseCol, layer / 3f) * la;
                    lc.A = 0;
                    Vector2 scale = new Vector2(lw, BeamLength / tex.Height);
                    Main.spriteBatch.Draw(tex, center, null, lc, halfRot, origin, scale, SpriteEffects.None, 0f);
                }
            }

            // 中心枢纽光球
            if (ACMAsset.LightShot != null) {
                Color hub = baseCol * fade * 0.9f;
                hub.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, center, null, hub, 0f, ACMAsset.LightShot.Size() / 2f, 2.4f * grow, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}


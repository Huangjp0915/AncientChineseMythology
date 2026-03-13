using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    #region 天眼仆从NPC

    /// <summary>
    /// 天眼仆从 - 环绕Boss并发射攻击
    /// </summary>
    internal class CelestialEyeMinion : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        [VaultLoaden("{@namespace}/")]
        public static Texture2D CelestialOverseerEye;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 48;
            NPC.height = 48;
            NPC.damage = 80;
            NPC.defense = 40;
            NPC.lifeMax = 50000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.dontTakeDamage = false;

            if (Main.expertMode) {
                NPC.lifeMax = 75000;
            }
            if (Main.masterMode) {
                NPC.lifeMax = 100000;
            }
        }

        private ref float OwnerIndex => ref NPC.ai[0];
        private ref float MinionIndex => ref NPC.ai[1];
        private ref float LaserMode => ref NPC.ai[2];
        private ref float AttackTimer => ref NPC.ai[3];

        private float orbitAngle;
        private float orbitRadius = 250f;
        private float globalTime;

        public override void AI() {
            globalTime += 1f / 60f;

            // 获取主人
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<CelestialOverseer>()) {
                NPC.life = 0;
                NPC.checkDead();
                return;
            }

            // 获取目标
            Player target = Main.player[owner.target];
            if (!target.active || target.dead) {
                NPC.velocity *= 0.95f;
                return;
            }

            // 轨道运动
            orbitAngle += 0.02f + MinionIndex * 0.005f;
            float targetRadius = 250f + MathF.Sin(globalTime * 2f + MinionIndex) * 30f;
            orbitRadius = MathHelper.Lerp(orbitRadius, targetRadius, 0.05f);

            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * orbitRadius;
            Vector2 targetPos = owner.Center + orbitOffset;

            // 平滑移动
            NPC.velocity = (targetPos - NPC.Center) * 0.1f;

            // 面向玩家
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            AttackTimer++;

            // 攻击模式
            if (LaserMode == 1) {
                // 激光模式由Boss控制
                LaserMode = 0;
            }
            else {
                // 普通攻击
                float attackCooldown = Main.expertMode ? 80f : 100f;
                if (AttackTimer >= attackCooldown) {
                    AttackTimer = 0;
                    FireAtTarget(target);
                }
            }

            // 发光
            Lighting.AddLight(NPC.Center, new Vector3(0.8f, 0.9f, 1f) * 0.6f);
        }

        private void FireAtTarget(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);

            // 随机选择攻击类型
            int attackType = Main.rand.Next(3);
            switch (attackType) {
                case 0: // 单发追踪弹
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        toTarget * 10f,
                        ModContent.ProjectileType<CelestialEyeBeam>(),
                        NPC.damage / 2,
                        1f,
                        Main.myPlayer
                    );
                    break;

                case 1: // 三连发
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = toTarget.RotatedBy(MathHelper.ToRadians(15 * i)) * 8f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            vel,
                            ModContent.ProjectileType<HolyOrb>(),
                            NPC.damage / 3,
                            1f,
                            Main.myPlayer
                        );
                    }
                    break;

                case 2: // 星辰弹
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        toTarget * 12f,
                        ModContent.ProjectileType<CelestialStar>(),
                        NPC.damage / 2,
                        2f,
                        Main.myPlayer
                    );
                    break;
            }

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f }, NPC.Center);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = CelestialOverseerEye ?? TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = texture.Size() / 2f;

            // 拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = new Color(200, 220, 255) * progress * 0.3f;
                trailColor.A = 0;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                spriteBatch.Draw(texture, trailPos, null, trailColor, NPC.oldRot[i], origin, NPC.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color glowColor = new Color(200, 220, 255) * 0.5f;
            glowColor.A = 0;
            spriteBatch.Draw(texture, drawPos, null, glowColor, NPC.rotation, origin, NPC.scale * 1.3f, SpriteEffects.None, 0f);

            // 主体
            spriteBatch.Draw(texture, drawPos, null, Color.White, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);

            // 瞳孔高光
            Color coreColor = new Color(255, 255, 220);
            coreColor.A = 0;
            spriteBatch.Draw(texture, drawPos, null, coreColor * 0.5f, NPC.rotation, origin, NPC.scale * 0.8f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill() {
            // 死亡粒子
            for (int i = 0; i < 20; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion
    /// <summary>
    /// 神圣光弹 - 基础追踪弹幕
    /// </summary>
    internal class HolyOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            // 轻微追踪
            if (Projectile.ai[0] == 0) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.02f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 发光粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.7f) * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar;
            Vector2 origin = tex.Size() / 2f;

            // 使用 ACMAsset.LightShot 绘制光效
            if (ACMAsset.LightShot != null) {
                Color glowColor = new Color(255, 240, 180) * 0.6f;
                glowColor.A = 0;

                Main.spriteBatch.Draw(
                    ACMAsset.LightShot,
                    Projectile.Center - Main.screenPosition,
                    null,
                    glowColor,
                    Projectile.rotation - MathHelper.PiOver2,
                    ACMAsset.LightShot.Size() / 2f,
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 拖尾
            Color trailColor = new Color(255, 230, 150);
            trailColor.A = 0;
            float trailOpacity = 0.4f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = trailOpacity * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor * fade, Projectile.rotation, origin, Projectile.scale * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            // 主体
            Color mainColor = new Color(255, 245, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            // 消散粒子
            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天眼光束 - 从天眼发射的追踪光束
    /// </summary>
    internal class CelestialEyeBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            // 更强的追踪
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead && Projectile.timeLeft > 120) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float turnSpeed = Main.expertMode ? 0.06f : 0.04f;
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, turnSpeed);
                Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 光束粒子
            if (!VaultUtils.isServer) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 100, new Color(200, 220, 255), 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.85f, 1f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 使用剑气灰度图绘制光束效果
            if (ACMAsset.GlaciateWave != null) {
                Texture2D beamTex = ACMAsset.GlaciateWave;
                Vector2 origin = new Vector2(0, beamTex.Height / 2f);

                Color beamColor = new Color(180, 220, 255) * 0.7f;
                beamColor.A = 0;

                float length = Projectile.velocity.Length() * 3f;
                Vector2 scale = new Vector2(length / beamTex.Width, 0.15f);

                // 多层光束
                for (int i = 0; i < 3; i++) {
                    float layerAlpha = 0.5f - i * 0.15f;
                    float layerScale = 1f + i * 0.3f;
                    Main.spriteBatch.Draw(
                        beamTex,
                        Projectile.Center - Main.screenPosition,
                        null,
                        beamColor * layerAlpha,
                        Projectile.rotation,
                        origin,
                        scale * layerScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // 核心光点
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(220, 240, 255);
                coreColor.A = 0;
                Main.spriteBatch.Draw(
                    ACMAsset.LightShot,
                    Projectile.Center - Main.screenPosition,
                    null,
                    coreColor,
                    0f,
                    ACMAsset.LightShot.Size() / 2f,
                    0.4f,
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }

    /// <summary>
    /// 神圣光柱 - 从天而降的审判光柱
    /// </summary>
    internal class DivineLightPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 800;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            Projectile.ai[0]++;

            // 光柱粒子
            if (!VaultUtils.isServer && Projectile.ai[0] % 2 == 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), Main.rand.NextFloat(-400, 400));
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, -2f, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 强烈发光
            for (int i = 0; i < 5; i++) {
                Vector2 lightPos = Projectile.Center + new Vector2(0, -300 + i * 150);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 1.5f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 自定义碰撞检测 - 细长的光柱
            Rectangle pillarBox = new Rectangle(
                (int)Projectile.Center.X - 30,
                (int)Projectile.Center.Y - 400,
                60,
                800
            );
            return pillarBox.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float progress = Projectile.ai[0] / 60f;
            float alpha = progress < 0.2f ? progress / 0.2f : (progress > 0.8f ? (1f - progress) / 0.2f : 1f);

            // 使用 GlaciateWave 绘制光柱
            if (ACMAsset.GlaciateWave != null) {
                Texture2D pillarTex = ACMAsset.GlaciateWave;

                Color pillarColor = new Color(255, 240, 180) * alpha * 0.8f;
                pillarColor.A = 0;

                // 旋转90度使其垂直
                float rotation = MathHelper.PiOver2;
                Vector2 scale = new Vector2(1600f / pillarTex.Width, 0.25f);

                // 多层光柱
                for (int i = 0; i < 3; i++) {
                    float layerAlpha = 0.6f - i * 0.15f;
                    float layerScale = 1f + i * 0.4f;
                    Main.spriteBatch.Draw(
                        pillarTex,
                        drawPos,
                        null,
                        pillarColor * layerAlpha,
                        rotation,
                        pillarTex.Size() / 2f,
                        scale * new Vector2(1f, layerScale),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // 中心高亮
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(255, 255, 220) * alpha;
                coreColor.A = 0;

                for (int i = 0; i < 5; i++) {
                    Vector2 corePos = drawPos + new Vector2(0, -300 + i * 150);
                    Main.spriteBatch.Draw(
                        ACMAsset.LightShot,
                        corePos,
                        null,
                        coreColor * 0.5f,
                        0f,
                        ACMAsset.LightShot.Size() / 2f,
                        1.5f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            // 消散光效
            for (int i = 0; i < 20; i++) {
                Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), Main.rand.NextFloat(-400, 400));
                Vector2 dustVel = Main.rand.NextVector2Circular(5, 5);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 星辰弹幕 - 从远处飞来的星辰
    /// </summary>
    internal class CelestialStar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            Projectile.rotation += 0.15f;

            // 加速
            if (Projectile.velocity.Length() < 18f) {
                Projectile.velocity *= 1.02f;
            }

            // 星光粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.YellowStarDust, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.6f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 使用 BlankStar 绘制星辰
            if (ACMAsset.BlankStar != null) {
                Texture2D starTex = ACMAsset.BlankStar;
                Vector2 origin = starTex.Size() / 2f;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;

                // 拖尾
                Color trailColor = new Color(255, 230, 150) * 0.5f;
                trailColor.A = 0;

                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float scale = 0.5f * (1f - i * 0.06f);
                    Main.spriteBatch.Draw(starTex, pos, null, trailColor * fade, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
                }

                // 外层光晕
                Color glowColor = new Color(255, 245, 180) * 0.6f;
                glowColor.A = 0;
                Main.spriteBatch.Draw(starTex, drawPos, null, glowColor, Projectile.rotation * 0.5f, origin, 0.7f, SpriteEffects.None, 0f);

                // 核心
                Color coreColor = new Color(255, 255, 220);
                coreColor.A = 0;
                Main.spriteBatch.Draw(starTex, drawPos, null, coreColor, Projectile.rotation, origin, 0.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            // 星辰爆炸效果
            for (int i = 0; i < 15; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.YellowStarDust, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
        }
    }

    /// <summary>
    /// 神圣光环 - 旋转的光环弹幕
    /// </summary>
    internal class HolyHaloRing : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            // 旋转运动
            Projectile.ai[0] += 0.03f;
            float currentAngle = Projectile.ai[0];
            float speed = Projectile.velocity.Length();
            Projectile.velocity = currentAngle.ToRotationVector2() * speed;

            Projectile.rotation = currentAngle + MathHelper.PiOver2;

            // 光环粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.6f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.LightShot != null) {
                Texture2D tex = ACMAsset.LightShot;
                Vector2 origin = tex.Size() / 2f;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;

                // 拖尾
                Color trailColor = new Color(255, 220, 150) * 0.4f;
                trailColor.A = 0;

                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = 0.4f * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.spriteBatch.Draw(tex, pos, null, trailColor * fade, 0f, origin, 0.4f * (1f - i * 0.05f), SpriteEffects.None, 0);
                }

                // 主体
                Color mainColor = new Color(255, 240, 180);
                mainColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, mainColor, 0f, origin, 0.5f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #region 大激光弹幕

    /// <summary>
    /// 神圣死光 - 追踪玩家的大激光
    /// </summary>
    internal class DivineDeathRay : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2000f;
        private const int LaserDuration = 90;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LaserDuration;
        }

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];

        public override void AI() {
            // 跟随Boss
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 追踪玩家
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                float targetAngle = (target.Center - Projectile.Center).ToRotation();
                float turnSpeed = 0.025f;
                LaserAngle = MathHelper.Lerp(LaserAngle, targetAngle, turnSpeed);
            }

            Projectile.rotation = LaserAngle;

            // 激光粒子
            if (!VaultUtils.isServer) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 5; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(15, 15);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 2f;
                }
            }

            // 发光
            for (int i = 0; i < 10; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 1.5f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            float alpha = progress < 0.1f ? progress / 0.1f : (progress > 0.8f ? (1f - progress) / 0.2f : 1f);
            float width = 0.4f * alpha;

            Vector2 scale = new Vector2(LaserLength / laserTex.Width, width);

            // 核心
            Color coreColor = new Color(255, 245, 200) * alpha;
            coreColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, coreColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);

            // 外层光晕
            Color glowColor = new Color(255, 220, 150) * alpha * 0.6f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, glowColor, LaserAngle, origin, scale * new Vector2(1f, 1.5f), SpriteEffects.None, 0f);

            // 最外层
            Color outerColor = new Color(255, 200, 100) * alpha * 0.3f;
            outerColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, outerColor, LaserAngle, origin, scale * new Vector2(1f, 2f), SpriteEffects.None, 0f);

            // 起点光球
            if (ACMAsset.LightShot != null) {
                Color orbColor = new Color(255, 250, 200) * alpha;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f, ACMAsset.LightShot.Size() / 2f, 2f * alpha, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 终极天光 - 超大激光
    /// </summary>
    internal class OmegaCelestialLaser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 3000f;
        private const int LaserDuration = 150;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LaserDuration;
        }

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 更强的追踪
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                float targetAngle = (target.Center - Projectile.Center).ToRotation();
                float turnSpeed = Main.expertMode ? 0.035f : 0.025f;
                LaserAngle = MathHelper.Lerp(LaserAngle, targetAngle, turnSpeed);
            }

            Projectile.rotation = LaserAngle;

            // 大量粒子
            if (!VaultUtils.isServer) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 10; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(30, 30);
                    int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 3f;
                }
            }

            // 强烈发光
            for (int i = 0; i < 15; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 2f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 80f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            float alpha = progress < 0.1f ? progress / 0.1f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);
            float width = 0.8f * alpha;

            Vector2 scale = new Vector2(LaserLength / laserTex.Width, width);

            // 多层绘制
            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = 1f + layer * 0.5f;
                float layerAlpha = 1f - layer * 0.2f;
                Color layerColor = Color.Lerp(new Color(255, 250, 220), new Color(255, 200, 100), layer / 3f) * alpha * layerAlpha;
                layerColor.A = 0;
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, LaserAngle, origin, scale * new Vector2(1f, layerWidth), SpriteEffects.None, 0f);
            }

            // 起点爆发
            if (ACMAsset.Sparkle != null) {
                Color burstColor = new Color(255, 250, 200) * alpha;
                burstColor.A = 0;
                float burstRot = (float)Main.GameUpdateCount * 0.1f;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, burstColor, burstRot, ACMAsset.Sparkle.Size() / 2f, 3f * alpha, SpriteEffects.None, 0f);
            }

            if (ACMAsset.LightShot != null) {
                Color orbColor = new Color(255, 255, 220) * alpha;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f, ACMAsset.LightShot.Size() / 2f, 4f * alpha, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 交叉激光 - 旋转的固定激光
    /// </summary>
    internal class CrossLaserBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 1500f;
        private const int LaserDuration = 120;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LaserDuration;
        }

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 缓慢旋转
            LaserAngle += 0.015f;
            Projectile.rotation = LaserAngle;

            // 粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                float dist = Main.rand.NextFloat(LaserLength);
                Vector2 dustPos = Projectile.Center + laserDir * dist;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            // 发光
            for (int i = 0; i < 8; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 180);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 1f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 25f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            float alpha = progress < 0.15f ? progress / 0.15f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);

            Vector2 scale = new Vector2(LaserLength / laserTex.Width, 0.2f * alpha);

            Color beamColor = new Color(255, 240, 180) * alpha;
            beamColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, beamColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);

            Color glowColor = new Color(255, 220, 150) * alpha * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, glowColor, LaserAngle, origin, scale * new Vector2(1f, 1.8f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 扫射激光弹 - 快速直线激光弹
    /// </summary>
    internal class SweepingLaserBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.7f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(255, 230, 150) * fade;
                trailColor.A = 0;
                float trailScale = 0.08f * (1f - i * 0.03f);
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(0.3f, trailScale), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 245, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.4f, 0.1f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 仆从同步激光 - 仆从发射的激光弹
    /// </summary>
    internal class MinionSyncLaser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 100, new Color(200, 220, 255), 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.9f, 1f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D tex = ACMAsset.GlaciateWave;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.6f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(180, 220, 255) * fade;
                trailColor.A = 0;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(0.5f, 0.12f * (1f - i * 0.04f)), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(220, 240, 255);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.6f, 0.15f), SpriteEffects.None, 0f);

            // 核心光点
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(255, 255, 255);
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor, 0f, ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion
}

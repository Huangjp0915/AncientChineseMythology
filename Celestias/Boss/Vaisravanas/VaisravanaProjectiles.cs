using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    #region 宝塔神光弹

    /// <summary>
    /// 宝塔神光弹 - 毗沙门天王的基础弹幕
    /// </summary>
    internal class TreasureTowerOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float pulsePhase = 0f;
        private float orbAlpha = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            orbAlpha = MathHelper.Lerp(orbAlpha, 1f, 0.08f);
            pulsePhase += 0.1f;

            // 轻微追踪
            if (Projectile.ai[0] == 0 && Projectile.timeLeft > 200) {
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

            // 仙气粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 10);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            // 白色仙光
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.95f) * 0.7f * orbAlpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 使用 VaisravanaHelper 绘制仙气光球
            VaisravanaHelper.DrawImmortalOrb(sb, Projectile.Center,
                VaisravanaHelper.PureWhite * orbAlpha,
                VaisravanaHelper.ImmortalGold,
                0.6f, pulsePhase);

            // 拖尾
            if (ACMAsset.LightShot != null) {
                Color trailColor = VaisravanaHelper.SpiritSilver * 0.4f;
                trailColor.A = 0;

                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = orbAlpha * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float scale = 0.4f * (1f - i * 0.05f);
                    sb.Draw(ACMAsset.LightShot, pos, null, trailColor * fade, 0f,
                        ACMAsset.LightShot.Size() / 2f, scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(5, 5);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 宝塔光束

    /// <summary>
    /// 宝塔光束 - 从宝塔发射的追踪光束
    /// </summary>
    internal class TowerBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            // 追踪
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead && Projectile.timeLeft > 100) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float turnSpeed = Main.expertMode ? 0.05f : 0.035f;
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, turnSpeed);
                Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 粒子
            if (!VaultUtils.isServer) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 0.9f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.95f, 1f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D beamTex = ACMAsset.GlaciateWave;
            Vector2 origin = new Vector2(0, beamTex.Height / 2f);

            Color beamColor = VaisravanaHelper.CelestialAzure * 0.7f;
            beamColor.A = 0;

            float length = Projectile.velocity.Length() * 4f;
            Vector2 scale = new Vector2(length / beamTex.Width, 0.12f);

            for (int i = 0; i < 3; i++) {
                float layerAlpha = 0.6f - i * 0.15f;
                float layerScale = 1f + i * 0.4f;
                Main.spriteBatch.Draw(beamTex, Projectile.Center - Main.screenPosition, null, beamColor * layerAlpha,
                    Projectile.rotation, origin, scale * new Vector2(1f, layerScale), SpriteEffects.None, 0f);
            }

            // 核心光点
            if (ACMAsset.LightShot != null) {
                Color coreColor = VaisravanaHelper.PureWhite;
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, Projectile.Center - Main.screenPosition, null, coreColor,
                    0f, ACMAsset.LightShot.Size() / 2f, 0.35f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 天王星辰

    /// <summary>
    /// 天王星辰 - 天王召唤的神圣星辰
    /// </summary>
    internal class VaisravanaStar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float starRotation = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            starRotation += 0.12f;
            Projectile.rotation = starRotation;

            // 加速
            if (Projectile.velocity.Length() < 20f) {
                Projectile.velocity *= 1.025f;
            }

            // 星光粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WhiteTorch, 0, 0, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.08f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.92f) * 0.9f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.BlankStar == null) return false;

            Texture2D starTex = ACMAsset.BlankStar;
            Vector2 origin = starTex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            Color trailColor = VaisravanaHelper.ImmortalGold * 0.5f;
            trailColor.A = 0;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float scale = 0.45f * (1f - i * 0.06f);
                Main.spriteBatch.Draw(starTex, pos, null, trailColor * fade, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
            }

            // 外层光晕
            Color glowColor = VaisravanaHelper.SpiritSilver * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(starTex, drawPos, null, glowColor, starRotation * 0.5f, origin, 0.65f, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = VaisravanaHelper.PureWhite;
            coreColor.A = 0;
            Main.spriteBatch.Draw(starTex, drawPos, null, coreColor, starRotation, origin, 0.45f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            for (int i = 0; i < 15; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f }, Projectile.Center);
        }
    }

    #endregion

    #region 仙气波

    /// <summary>
    /// 仙气波 - 环形扩散的仙气波动
    /// </summary>
    internal class ImmortalWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float waveRadius = 0f;
        private float waveAlpha = 1f;

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
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            // 扩散
            float maxRadius = 600f;
            float progress = 1f - (float)Projectile.timeLeft / 60f;
            waveRadius = maxRadius * VaisravanaHelper.QuadOut(progress);
            waveAlpha = 1f - progress;

            // 粒子效果
            if (!VaultUtils.isServer && Projectile.timeLeft % 3 == 0) {
                int particleCount = 8;
                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * waveRadius;
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 2f;
                }
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.95f) * waveAlpha * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 环形碰撞
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            float ringWidth = 40f;
            return distance > waveRadius - ringWidth && distance < waveRadius + ringWidth;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            // 绘制扩散环
            VaisravanaHelper.DrawDivineCircle(sb, Projectile.Center, waveRadius,
                VaisravanaHelper.PureWhite, Main.GameUpdateCount * 0.02f, waveAlpha);

            // 内环
            VaisravanaHelper.DrawImmortalHalo(sb, Projectile.Center, waveRadius * 0.85f,
                VaisravanaHelper.CelestialAzure, -Main.GameUpdateCount * 0.015f, waveAlpha * 0.7f);

            return false;
        }
    }

    #endregion

    #region 大激光

    /// <summary>
    /// 宝塔圣光 - 毗沙门天王的大激光
    /// </summary>
    internal class TreasureTowerRay : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2500f;
        private const int LaserDuration = 100;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
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

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float LaserAngle => ref Projectile.ai[1];

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 追踪
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (target.active && !target.dead) {
                float targetAngle = (target.Center - Projectile.Center).ToRotation();
                float turnSpeed = 0.02f;
                LaserAngle = MathHelper.Lerp(LaserAngle, targetAngle, turnSpeed);
            }

            Projectile.rotation = LaserAngle;

            // 粒子（金色库藏屑）
            if (!VaultUtils.isServer) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 6; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(20, 20);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.6f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 2f;
                }
            }

            // 光照（暖金）
            for (int i = 0; i < 12; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.9f, 0.6f) * 1.8f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 50f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            float alpha = progress < 0.1f ? progress / 0.1f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);

            // 终极宝塔·贯穿全屏金柱：BeamGrad 金带（暖金核 + 金边）。"财神镇压"戏剧顶点。
            Vector2 dir = LaserAngle.ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + dir * LaserLength;
            float halfWidth = 70f * alpha;
            Color core = TelegraphColors.Holy; core.A = 255;       // 暖白金芯（过曝）
            Color edge = TelegraphColors.Gold; edge.A = 150;       // 琉璃金边
            ACMShaders.DrawBeam(start, end, halfWidth, core, edge, alpha,
                flowSpeed: 2.0f, flowScale: 2.2f, coreSharp: 2.0f, coreGlow: 1.2f * alpha);

            // 起点库藏金爆（DrawRadialBloomAt 占本帧全屏名额, 仅此处一处）
            ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.16f, 0.7f * alpha, TelegraphColors.Gold,
                rayCount: 16f, falloff: 2.2f);

            // 起点核心光球（加性, 收尾点缀）
            if (ACMAsset.LightShot != null) {
                SpriteBatch sb = Main.spriteBatch;
                Color orbColor = VaisravanaHelper.DivineWhite * alpha; orbColor.A = 0;
                sb.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f, ACMAsset.LightShot.Size() / 2f, 2.4f * alpha, SpriteEffects.None, 0f);
                Color goldOrb = TelegraphColors.Gold * alpha; goldOrb.A = 0;
                sb.Draw(ACMAsset.LightShot, drawPos, null, goldOrb, 0f, ACMAsset.LightShot.Size() / 2f, 3.6f * alpha, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 四方圣光 - 四方向同时发射的激光
    /// </summary>
    internal class QuadrantRay : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 1800f;
        private const int LaserDuration = 90;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2500;
        }

        public override void SetDefaults() {
            Projectile.width = 25;
            Projectile.height = 25;
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

            // 固定方位激光（夜叉锚定安全道机制依赖固定方向，不再旋转）
            Projectile.rotation = LaserAngle;

            // 粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                float dist = Main.rand.NextFloat(LaserLength);
                Vector2 dustPos = Projectile.Center + laserDir * dist;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }

            for (int i = 0; i < 9; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.98f, 0.95f) * 1.2f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 30f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D laserTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, laserTex.Height / 2f);

            float progress = 1f - (float)Projectile.timeLeft / LaserDuration;
            float alpha = progress < 0.12f ? progress / 0.12f : (progress > 0.88f ? (1f - progress) / 0.12f : 1f);

            Vector2 scale = new Vector2(LaserLength / laserTex.Width, 0.25f * alpha);

            Color beamColor = VaisravanaHelper.SpiritSilver * alpha;
            beamColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, beamColor, LaserAngle, origin, scale, SpriteEffects.None, 0f);

            Color glowColor = VaisravanaHelper.CelestialAzure * alpha * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(laserTex, drawPos, null, glowColor, LaserAngle, origin, scale * new Vector2(1f, 2f), SpriteEffects.None, 0f);

            return false;
        }
    }

    #endregion

    #region 夜叉仆从

    /// <summary>
    /// 夜叉仆从 - 毗沙门天王召唤的护法
    /// </summary>
    internal class YakshaMinion : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 56;
            NPC.height = 56;
            NPC.damage = 100;
            NPC.defense = 50;
            NPC.lifeMax = 80000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.dontTakeDamage = false;

            if (Main.expertMode) {
                NPC.lifeMax = 120000;
            }
            if (Main.masterMode) {
                NPC.lifeMax = 160000;
            }
        }

        private ref float OwnerIndex => ref NPC.ai[0];
        private ref float MinionIndex => ref NPC.ai[1];  // 0=北 1=东 2=南 3=西 锚定方向
        private ref float AttackTimer => ref NPC.ai[3];

        private float globalTime;

        // 四方锚点角度：0=北 1=东 2=南 3=西
        private static readonly float[] CardinalAngle = {
            -MathHelper.PiOver2, 0f, MathHelper.PiOver2, MathHelper.Pi
        };

        public override void AI() {
            globalTime += 1f / 60f;

            // 获取主人
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<Vaisravana>()) {
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

            // 四方锚点：固定守在所属方向（区别于环绕，杀序更可读）
            int dir = ((int)MinionIndex % 4 + 4) % 4;
            float anchorRadius = 250f + MathF.Sin(globalTime * 1.6f + dir) * 26f;
            Vector2 anchorPos = owner.Center + CardinalAngle[dir].ToRotationVector2() * anchorRadius;
            NPC.velocity = (anchorPos - NPC.Center) * 0.09f;

            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            AttackTimer++;
            float attackCooldown = Main.expertMode ? 80f : 105f;
            if (AttackTimer >= attackCooldown) {
                AttackTimer = 0;
                FireAtTarget(target);
            }

            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.92f, 0.7f) * 0.55f);
        }

        private void FireAtTarget(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = toTarget.RotatedBy(MathHelper.ToRadians(11 * i)) * 9.5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 3, 1f, Main.myPlayer);
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f }, NPC.Center);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = texture.Size() / 2f;

            // 财神金光晕（加性 SoftGlow，护法夜叉的金气锚点）
            if (ACMAsset.SoftGlow != null) {
                float pulse = 1f + MathF.Sin(globalTime * 3f) * 0.08f;
                Color halo = VaisravanaHelper.TowerGold * 0.5f; halo.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawPos, null, halo, 0f, ACMAsset.SoftGlow.Size() / 2f,
                    NPC.width / 40f * 1.4f * pulse, SpriteEffects.None, 0f);
            }

            // 拖尾（金）
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = VaisravanaHelper.TowerGold * progress * 0.3f;
                trailColor.A = 0;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                spriteBatch.Draw(texture, trailPos, null, trailColor, NPC.oldRot[i], origin, NPC.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层金光晕
            Color glowColor = TelegraphColors.Gold * 0.45f;
            glowColor.A = 0;
            spriteBatch.Draw(texture, drawPos, null, glowColor, NPC.rotation, origin, NPC.scale * 1.25f, SpriteEffects.None, 0f);

            // 本体
            spriteBatch.Draw(texture, drawPos, null, Color.White, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);

            // 核心高光
            Color coreColor = VaisravanaHelper.PureWhite;
            coreColor.A = 0;
            spriteBatch.Draw(texture, drawPos, null, coreColor * 0.5f, NPC.rotation, origin, NPC.scale * 0.75f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill() {
            for (int i = 0; i < 25; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(7, 7);
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 其他弹幕

    /// <summary>
    /// 仙气光环 - 旋转的光环弹幕
    /// </summary>
    internal class ImmortalHaloRing : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            // 旋转运动
            Projectile.ai[0] += 0.025f;
            float currentAngle = Projectile.ai[0];
            float speed = Projectile.velocity.Length();
            Projectile.velocity = currentAngle.ToRotationVector2() * speed;

            Projectile.rotation = currentAngle + MathHelper.PiOver2;

            // 光环粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 0.9f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.95f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.LightShot == null) return false;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            Color trailColor = VaisravanaHelper.ImmortalGold * 0.4f;
            trailColor.A = 0;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.4f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor * fade, 0f, origin, 0.35f * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            // 本体
            Color mainColor = VaisravanaHelper.PureWhite;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, 0f, origin, 0.45f, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 扫射光弹 - 快速直线光弹
    /// </summary>
    internal class SweepingLightBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
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
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.95f) * 0.7f);
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
                Color trailColor = VaisravanaHelper.SpiritSilver * fade;
                trailColor.A = 0;
                float trailScale = 0.06f * (1f - i * 0.03f);
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(0.25f, trailScale), SpriteEffects.None, 0f);
            }

            // 本体
            Color mainColor = VaisravanaHelper.PureWhite;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.35f, 0.08f), SpriteEffects.None, 0f);

            return false;
        }
    }

    #endregion
}

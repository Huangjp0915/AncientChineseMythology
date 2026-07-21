using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    #region 天眼仆从NPC（历史类型：当前仅作为天眼贴图持有者保留）

    /// <summary>
    /// 天眼仆从 - 环绕Boss并发射攻击（V3 后本体不再生成；保留类型与静态贴图供绘制/武器引用）。
    /// </summary>
    internal class CelestialEyeMinion : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public static Texture2D CelestialOverseerEye;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;

            // 天眼贴图：仅当存在真实 PNG 时才加载，否则保持 null 由 ACMAsset.BlankStar 兜底
            if (!Main.dedServ) {
                const string eyePath = "AncientChineseMythology/Celestias/Boss/CelestialOverseers/CelestialOverseerEye";
                if (ModContent.HasAsset(eyePath)) {
                    CelestialOverseerEye = ModContent.Request<Texture2D>(eyePath).Value;
                }
            }
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

            if (Main.expertMode) NPC.lifeMax = 75000;
            if (Main.masterMode) NPC.lifeMax = 100000;
        }

        private ref float OwnerIndex => ref NPC.ai[0];

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<CelestialOverseer>()) {
                NPC.life = 0;
                NPC.checkDead();
                return;
            }
            // 兼容遗留生成：静默环绕，不再自主攻击
            NPC.velocity = (owner.Center + new Vector2(0, -200) - NPC.Center) * 0.05f;
            Lighting.AddLight(NPC.Center, new Vector3(0.8f, 0.9f, 1f) * 0.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = CelestialOverseerEye ?? TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = texture.Size() / 2f;
            spriteBatch.Draw(texture, drawPos, null, Color.White, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill() {
            for (int i = 0; i < 20; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 基础弹幕（直线精密弹道 —— 机关身份：无追踪）

    /// <summary>
    /// 圣光球 - 基础直线弹（V3 去追踪：机关弹道必须精确可读；陪审员三连扇形使用）。
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
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微匀加速（直线，无转向）
            if (Projectile.velocity.Length() < 14f)
                Projectile.velocity *= 1.008f;

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

            if (ACMAsset.LightShot != null) {
                Color glowColor = new Color(255, 240, 180) * 0.6f;
                glowColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation - MathHelper.PiOver2, ACMAsset.LightShot.Size() / 2f, 0.8f, SpriteEffects.None, 0f);
            }

            Color trailColor = new Color(255, 230, 150);
            trailColor.A = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.4f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor * fade, Projectile.rotation, origin, Projectile.scale * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            Color mainColor = new Color(255, 245, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天眼光矢 - 天眼发射的直线光束弹（V3 去追踪：注视线预告在先，弹道严格沿线）。
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
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 100, new Color(200, 220, 255), 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.85f, 1f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave != null) {
                Texture2D beamTex = ACMAsset.GlaciateWave;
                Vector2 origin = new Vector2(0, beamTex.Height / 2f);

                Color beamColor = new Color(180, 220, 255) * 0.7f;
                beamColor.A = 0;

                float length = Projectile.velocity.Length() * 3f;
                Vector2 scale = new Vector2(length / beamTex.Width, 0.15f);

                for (int i = 0; i < 3; i++) {
                    float layerAlpha = 0.5f - i * 0.15f;
                    float layerScale = 1f + i * 0.3f;
                    Main.spriteBatch.Draw(beamTex, Projectile.Center - Main.screenPosition, null, beamColor * layerAlpha,
                        Projectile.rotation, origin, scale * layerScale, SpriteEffects.None, 0f);
                }
            }

            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(220, 240, 255);
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, Projectile.Center - Main.screenPosition, null, coreColor,
                    0f, ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 神圣光柱 - 从天而降的审判光柱（V3：落地前 8f 上膛无伤，伤害窗与视觉严格对齐）。
    /// </summary>
    internal class DivineLightPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>上膛帧数：期间快速淡入且无伤。</summary>
        private const int ArmTime = 8;

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

            if (!VaultUtils.isServer && Projectile.ai[0] % 2 == 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), Main.rand.NextFloat(-400, 400));
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, -2f, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            for (int i = 0; i < 5; i++) {
                Vector2 lightPos = Projectile.Center + new Vector2(0, -300 + i * 150);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 1.5f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.ai[0] < ArmTime)
                return false; // 上膛期无伤（公平阀门）
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
            float alpha = progress < 0.15f ? progress / 0.15f : (progress > 0.8f ? (1f - progress) / 0.2f : 1f);

            if (ACMAsset.GlaciateWave != null) {
                Texture2D pillarTex = ACMAsset.GlaciateWave;

                Color pillarColor = new Color(255, 240, 180) * alpha * 0.8f;
                pillarColor.A = 0;

                float rotation = MathHelper.PiOver2;
                Vector2 scale = new Vector2(1600f / pillarTex.Width, 0.25f);

                for (int i = 0; i < 3; i++) {
                    float layerAlpha = 0.6f - i * 0.15f;
                    float layerScale = 1f + i * 0.4f;
                    Main.spriteBatch.Draw(pillarTex, drawPos, null, pillarColor * layerAlpha, rotation,
                        pillarTex.Size() / 2f, scale * new Vector2(1f, layerScale), SpriteEffects.None, 0f);
                }
            }

            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(255, 255, 220) * alpha;
                coreColor.A = 0;
                for (int i = 0; i < 5; i++) {
                    Vector2 corePos = drawPos + new Vector2(0, -300 + i * 150);
                    Main.spriteBatch.Draw(ACMAsset.LightShot, corePos, null, coreColor * 0.5f, 0f,
                        ACMAsset.LightShot.Size() / 2f, 1.5f, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
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
    /// 星衡飞轮 - V3 重做：蛰伏材质化(ai0 帧, 星与星错拍) → 冻结瞄准线(30f, 服务器写入 ai1)
    /// → 沿线直射(无追踪)。伤害仅飞行态判定，瞄准线即弹道承诺。
    /// </summary>
    internal class CelestialStar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int AimTime = 30;

        private ref float DormantTime => ref Projectile.ai[0]; // 蛰伏帧数（生成时设定）
        private ref float AimAngle => ref Projectile.ai[1];    // 冻结瞄准角（服务器写入后同步）
        private ref float State => ref Projectile.ai[2];       // 0=蛰伏 1=瞄准 2=飞行
        private ref float StateTimer => ref Projectile.localAI[0];

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
            Projectile.timeLeft = 420;
        }

        public override bool? CanDamage() => State >= 2f ? null : false;

        public override void AI() {
            StateTimer++;

            // 兼容直射生成（带初速 + 无蛰伏参数）：跳过蛰伏/瞄准, 立即按给定方向飞行
            if (State == 0f && DormantTime <= 0f && Projectile.velocity.LengthSquared() > 0.5f) {
                State = 2f;
                AimAngle = Projectile.velocity.ToRotation();
                StateTimer = 0;
            }

            if (State == 0f) {
                // 蛰伏：材质化生长 + 悬停微浮
                Projectile.velocity = Vector2.Zero;
                Projectile.scale = MathHelper.Clamp(StateTimer / 20f, 0.1f, 1f);
                Projectile.rotation += 0.05f;
                if (StateTimer >= DormantTime) {
                    State = 1f;
                    StateTimer = 0;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 冻结瞄准（提前量），此后不再修正 —— 线即承诺
                        Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                        Vector2 dir = ACMUtils.LeadTarget(Projectile.Center, target.Center, target.velocity, 15f);
                        AimAngle = dir.ToRotation();
                        Projectile.netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
                }
            }
            else if (State == 1f) {
                // 瞄准：静止锁线（30f 逃逸窗）
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation += 0.12f;
                if (StateTimer >= AimTime) {
                    State = 2f;
                    StateTimer = 0;
                    Projectile.velocity = AimAngle.ToRotationVector2() * 15f;
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.8f }, Projectile.Center);
                }
            }
            else {
                // 飞行：直线加速 15→19
                Projectile.rotation += 0.18f;
                if (Projectile.velocity.Length() < 19f)
                    Projectile.velocity *= 1.02f;

                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.YellowStarDust, 0, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.6f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.BlankStar == null) return false;

            Texture2D starTex = ACMAsset.BlankStar;
            Vector2 origin = starTex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 瞄准线（冻结方向 = 弹道承诺；金色渐亮脉冲）
            if (State == 1f) {
                float t = StateTimer / AimTime;
                float pulse = 0.5f + 0.5f * MathF.Sin((float)Main.GameUpdateCount * 0.35f);
                Vector2 end = Projectile.Center + AimAngle.ToRotationVector2() * 1600f;
                ACMShaders.DrawBeam(Projectile.Center, end, 8f + t * 6f, TelegraphColors.Gold,
                    new Color(255, 160, 70, 130), (0.35f + 0.45f * t) * (0.7f + 0.3f * pulse),
                    flowSpeed: 1.8f, flowScale: 2.2f, coreSharp: 2.6f, coreGlow: 0.7f);
            }

            // 拖尾（仅飞行态）
            if (State >= 2f) {
                Color trailColor = new Color(255, 230, 150) * 0.5f;
                trailColor.A = 0;
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float scale = 0.5f * (1f - i * 0.06f);
                    Main.spriteBatch.Draw(starTex, pos, null, trailColor * fade, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
                }
            }

            // 双层反转星体（齿轮咬合感）
            Color glowColor = new Color(255, 245, 180) * 0.6f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(starTex, drawPos, null, glowColor, -Projectile.rotation * 0.7f, origin, 0.7f * Projectile.scale, SpriteEffects.None, 0f);

            Color coreColor = new Color(255, 255, 220);
            coreColor.A = 0;
            Main.spriteBatch.Draw(starTex, drawPos, null, coreColor, Projectile.rotation, origin, 0.5f * Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 15; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.YellowStarDust, dustVel.X, dustVel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
        }
    }

    /// <summary>
    /// 神圣光环 - 旋转的光环弹幕（历史类型保留，当前无生成点）。
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
            Projectile.ai[0] += 0.03f;
            float currentAngle = Projectile.ai[0];
            float speed = Projectile.velocity.Length();
            Projectile.velocity = currentAngle.ToRotationVector2() * speed;
            Projectile.rotation = currentAngle + MathHelper.PiOver2;

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

                Color trailColor = new Color(255, 220, 150) * 0.4f;
                trailColor.A = 0;
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = 0.4f * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.spriteBatch.Draw(tex, pos, null, trailColor * fade, 0f, origin, 0.4f * (1f - i * 0.05f), SpriteEffects.None, 0);
                }

                Color mainColor = new Color(255, 240, 180);
                mainColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, mainColor, 0f, origin, 0.5f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 大激光弹幕

    /// <summary>
    /// 凝视射线 - V3 重做：定速旋转（ai2=角速度, 无追踪），P3 环视凝射双线使用。
    /// 起始角有双线预告，顺旋向绕行即躲；前 10f 上膛无伤。
    /// </summary>
    internal class DivineDeathRay : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2000f;
        private const int LaserDuration = 165;
        private const int ArmTime = 10;

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
        private ref float SpinSpeed => ref Projectile.ai[2];

        private int Age => LaserDuration - Projectile.timeLeft;

        public override bool? CanDamage() => Age >= ArmTime ? null : false;

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            LaserAngle += SpinSpeed; // 定速旋转，无追踪（可预判绕行）
            Projectile.rotation = LaserAngle;

            if (!VaultUtils.isServer) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 4; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(15, 15);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 2f;
                }
            }

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
            if (Main.dedServ) return false;

            float progress = Age / (float)LaserDuration;
            float alpha = progress < 0.08f ? progress / 0.08f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);

            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            Color core = TelegraphColors.Gold;
            Color edge = new Color(255, 150, 60, 150);
            ACMShaders.DrawBeam(start, end, 30f * alpha + 6f, core, edge, alpha,
                flowSpeed: 2.0f, flowScale: 2.2f, coreSharp: 2.4f, coreGlow: 1.1f);

            if (ACMAsset.LightShot != null) {
                Color orbColor = new Color(255, 250, 200) * alpha;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, start - Main.screenPosition, null, orbColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 2f * alpha, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 终极天光 - 超大激光（V3：ai2=1 固定角模式，死亡演出垂天光柱复用；否则缓慢追踪）。
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
        private ref float FixedAngleMode => ref Projectile.ai[2];

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            // 固定角模式（死亡演出垂天光柱）：不追踪
            if (FixedAngleMode < 0.5f) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    float turnSpeed = Main.expertMode ? 0.035f : 0.025f;
                    LaserAngle = MathHelper.Lerp(LaserAngle, targetAngle, turnSpeed);
                }
            }

            Projectile.rotation = LaserAngle;

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

            for (int i = 0; i < 15; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 2f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.damage <= 0)
                return false; // 纯演出模式
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

            for (int layer = 3; layer >= 0; layer--) {
                float layerWidth = 1f + layer * 0.5f;
                float layerAlpha = 1f - layer * 0.2f;
                Color layerColor = Color.Lerp(new Color(255, 250, 220), new Color(255, 200, 100), layer / 3f) * alpha * layerAlpha;
                layerColor.A = 0;
                Main.spriteBatch.Draw(laserTex, drawPos, null, layerColor, LaserAngle, origin, scale * new Vector2(1f, layerWidth), SpriteEffects.None, 0f);
            }

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
    /// 辰枢十字激光 - V3：ai2=有符号角速度（生成端定转向/转速），前 12f 上膛无伤 + 宽度生长。
    /// </summary>
    internal class CrossLaserBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 1500f;
        private const int LaserDuration = 120;
        private const int ArmTime = 12;

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
        private ref float SpinSpeed => ref Projectile.ai[2];

        private int Age => LaserDuration - Projectile.timeLeft;

        public override bool? CanDamage() => Age >= ArmTime ? null : false;

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;

            float spin = SpinSpeed == 0f ? 0.013f : SpinSpeed;
            LaserAngle += spin;
            Projectile.rotation = LaserAngle;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                float dist = Main.rand.NextFloat(LaserLength);
                Vector2 dustPos = Projectile.Center + laserDir * dist;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

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
            if (Main.dedServ) return false;

            float progress = Age / (float)LaserDuration;
            float alpha = progress < 0.1f ? progress / 0.1f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);

            // BeamGrad 金芒权柄激光（锐利芯部使缓慢旋转的激光面读数清晰）
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            Color core = TelegraphColors.Gold;
            Color edge = new Color(255, 150, 60, 150);
            ACMShaders.DrawBeam(start, end, 24f * alpha + 5f, core, edge, alpha,
                flowSpeed: 1.8f, flowScale: 2.0f, coreSharp: 2.6f, coreGlow: 0.9f);

            return false;
        }
    }

    /// <summary>
    /// 扫射激光弹 - 快速直线激光弹（锁定扫描齐射使用）。
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

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(255, 230, 150) * fade;
                trailColor.A = 0;
                float trailScale = 0.08f * (1f - i * 0.03f);
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(0.3f, trailScale), SpriteEffects.None, 0f);
            }

            Color mainColor = new Color(255, 245, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.4f, 0.1f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 仆从同步激光 - 天眼齐射用直线激光弹。
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

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = 0.6f * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(180, 220, 255) * fade;
                trailColor.A = 0;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(0.5f, 0.12f * (1f - i * 0.04f)), SpriteEffects.None, 0f);
            }

            Color mainColor = new Color(220, 240, 255);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, new Vector2(0.6f, 0.15f), SpriteEffects.None, 0f);

            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(255, 255, 255);
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor, 0f, ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 审判 / 预告 / 仆从

    /// <summary>
    /// 审判射线 - 监视满槽触发的"审判标记"：方向锁定（不追踪），靠走出射线闪避。
    /// V3：前 8f 上膛无伤（伤害窗与视觉严格对齐）。
    /// </summary>
    internal class JudgmentBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float LaserLength = 2600f;
        private const int LaserDuration = 80;
        private const int ArmTime = 8;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
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

        private int Age => LaserDuration - Projectile.timeLeft;

        public override bool? CanDamage() => Age >= ArmTime ? null : false;

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active) { Projectile.Kill(); return; }
            Projectile.Center = owner.Center;
            Projectile.rotation = LaserAngle; // 锁定，不追踪

            if (!VaultUtils.isServer) {
                Vector2 laserDir = LaserAngle.ToRotationVector2();
                for (int i = 0; i < 8; i++) {
                    float dist = Main.rand.NextFloat(LaserLength);
                    Vector2 dustPos = Projectile.Center + laserDir * dist + Main.rand.NextVector2Circular(20, 20);
                    int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 80, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = laserDir * 3f;
                }
            }
            for (int i = 0; i < 14; i++) {
                Vector2 lightPos = Projectile.Center + LaserAngle.ToRotationVector2() * (i * 200);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.9f, 0.6f) * 2f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 55f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            float progress = Age / (float)LaserDuration;
            float alpha = progress < 0.08f ? progress / 0.08f : (progress > 0.85f ? (1f - progress) / 0.15f : 1f);

            // BeamGrad 纯红致命射线（唯一红 = 真正致命；锁定方向不追踪）
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + LaserAngle.ToRotationVector2() * LaserLength;
            Color core = TelegraphColors.Lethal;
            Color edge = new Color(150, 24, 34, 170);
            ACMShaders.DrawBeam(start, end, 58f * alpha + 10f, core, edge, alpha,
                flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.4f, coreGlow: 1.6f);

            if (ACMAsset.LightShot != null) {
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color orbColor = new Color(255, 90, 80) * alpha;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f, ACMAsset.LightShot.Size() / 2f, 4f * alpha, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 地面预告标线 - 纯视觉无伤。localAI[0]: 0=单向射线(十字/冲刺) 1=垂直光柱列 2=安全扇区 3=致命审判线。
    /// ai0=长度, ai1=角度, ai2=跟随NPC索引(-1为固定)。
    /// </summary>
    internal class OverseerGroundTelegraph : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
        }

        private ref float Length => ref Projectile.ai[0];
        private ref float Angle => ref Projectile.ai[1];
        private ref float OwnerIndex => ref Projectile.ai[2];
        private float Style => Projectile.localAI[0];

        public override bool? CanDamage() => false;

        // 同步样式与跟随目标（length/angle 走 ai0/ai1）
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write(Projectile.localAI[0]);
            writer.Write(OwnerIndex);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            Projectile.localAI[0] = reader.ReadSingle();
            OwnerIndex = reader.ReadSingle();
        }

        public override void AI() {
            int owner = (int)OwnerIndex;
            if (owner >= 0 && owner < Main.maxNPCs && Main.npc[owner].active) {
                Projectile.Center = Main.npc[owner].Center;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            // BeamGrad 直带原语；色彩语言——金芒=权柄预告(线/光柱) · 翠玉=安全缝 · 纯红=致命审判锁定线(style 3)
            float life = Projectile.timeLeft;
            float pulse = 0.5f + 0.5f * MathF.Sin((float)Main.GameUpdateCount * 0.3f);
            float baseAlpha = MathHelper.Clamp(life / 30f, 0.2f, 1f);
            float a = baseAlpha * (0.55f + 0.45f * pulse);
            Vector2 center = Projectile.Center;

            if (Style == 1f) {
                // 垂直光柱列预告（从地标向上）— 金芒
                Vector2 top = center + new Vector2(0, -Length);
                ACMShaders.DrawBeam(center, top, 26f, TelegraphColors.Gold, new Color(255, 150, 60, 150),
                    a * 0.9f, flowSpeed: 1.2f, flowScale: 1.6f, coreSharp: 2.0f, coreGlow: 0.8f);
            }
            else if (Style == 2f) {
                // 安全扇区缝（站此处安全）— 翠玉绿
                const float half = 0.55f;
                int rays = 5;
                for (int i = 0; i <= rays; i++) {
                    float ang = Angle - half + (2f * half) * i / rays;
                    Vector2 end = center + ang.ToRotationVector2() * Length;
                    ACMShaders.DrawBeam(center, end, 14f, TelegraphColors.Safe, new Color(120, 200, 150, 110),
                        a * 0.5f, flowSpeed: 1.0f, flowScale: 1.6f, coreSharp: 2.0f, coreGlow: 0.5f);
                }
            }
            else if (Style == 3f) {
                // 致命审判锁定线（唯一红）
                Vector2 end = center + Angle.ToRotationVector2() * Length;
                ACMShaders.DrawBeam(center, end, 30f, TelegraphColors.Lethal, new Color(150, 30, 40, 160),
                    a, flowSpeed: 2.0f, flowScale: 2.2f, coreSharp: 2.4f, coreGlow: 1.3f);
            }
            else {
                // 单向射线（十字/冲刺）— 金芒权柄
                Vector2 end = center + Angle.ToRotationVector2() * Length;
                ACMShaders.DrawBeam(center, end, 20f, TelegraphColors.Gold, new Color(255, 160, 70, 150),
                    a * 0.85f, flowSpeed: 1.6f, flowScale: 2.0f, coreSharp: 2.4f, coreGlow: 0.8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 锁定扫描锥 - V3 新增：探照锥搜索（冷钢蓝）→ 锁定冻结（金, 收窄）→ 机关齐射（扇形直线弹）。
    /// 本体无接触伤害；damage 传递给齐射弹。锥内玩家的监视槽额外上升（由本体轮询）。
    /// ai0=主人索引, ai1=锥心角, ai2=锁定状态(0=搜索; ≥1=已锁定且为锁定后帧数, 各端同步后本地推进)。
    /// </summary>
    internal class OverseerLockCone : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>搜索态锥半角（弧度）。锁定后收窄 45%（与着色器一致）。</summary>
        public const float SearchHalfAngle = 0.30f;
        /// <summary>锥长（像素）。</summary>
        public const float ConeLength = 1150f;

        // 专属扫描锥着色器（自缓存, 不依赖本体; 参考 Xuanwu 写法）
        private static Asset<Effect> scanConeRef;

        private static Effect ScanConeEffect {
            get {
                if (Main.dedServ) return null;
                scanConeRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/OverseerScanCone", AssetRequestMode.ImmediateLoad);
                return scanConeRef?.Value;
            }
        }

        public override void Unload() {
            scanConeRef = null;
        }

        private const int MaxSearchTime = 110;  // 超时强制锁定（对当前指向, 奖励走位）
        private const int LockToFire = 20;      // 锁定→开火固定逃逸窗
        private const int FadeAfterFire = 26;   // 开火后淡出

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false; // 锥体本身无伤；伤害在齐射弹
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 320;
        }

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float ConeAngle => ref Projectile.ai[1];
        private ref float LockState => ref Projectile.ai[2];
        private ref float InsideCounter => ref Projectile.localAI[0]; // 服务器权威的"照住"累计
        private ref float SearchAge => ref Projectile.localAI[1];

        private float lockFlash; // 锁定瞬间脉冲（纯本地视觉）

        public override bool? CanDamage() => false;

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<CelestialOverseer>()) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.rotation = ConeAngle;
            lockFlash *= 0.93f;

            if (LockState < 1f) {
                // —— 搜索：受限转速追向玩家 + 探照灯摆动 ——
                SearchAge++;
                Player target = Main.player[owner.target];
                if (target.active && !target.dead) {
                    float toPlayer = (target.Center - Projectile.Center).ToRotation();
                    float wobble = MathF.Sin(SearchAge * 0.05f + Projectile.whoAmI) * 0.10f;
                    float diff = MathHelper.WrapAngle(toPlayer + wobble - ConeAngle);
                    ConeAngle += MathHelper.Clamp(diff, -0.012f, 0.012f);

                    // 服务器权威：累计"照住"时长, 满 30f 或超时 → 锁定
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 d = target.Center - Projectile.Center;
                        bool inside = d.Length() < ConeLength
                            && Math.Abs(MathHelper.WrapAngle(d.ToRotation() - ConeAngle)) < SearchHalfAngle;
                        if (inside) InsideCounter++;
                        if (InsideCounter >= 30f || SearchAge >= MaxSearchTime) {
                            LockState = 1f; // 方向冻结
                            Projectile.netUpdate = true;
                        }
                    }
                }

                // 搜索扫掠尘（锥缘）
                if (!VaultUtils.isServer && (int)SearchAge % 3 == 0) {
                    for (int s = -1; s <= 1; s += 2) {
                        float a = ConeAngle + s * SearchHalfAngle;
                        Vector2 dp = Projectile.Center + a.ToRotationVector2() * Main.rand.NextFloat(100, ConeLength);
                        int dust = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 160, new Color(140, 190, 240), 0.9f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            else {
                // —— 锁定：方向冻结, 各端本地推进锁定计帧 ——
                if (LockState == 1f) {
                    lockFlash = 1f;
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.65f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.8f, Pitch = 0.5f }, Projectile.Center);
                }
                LockState++;

                // 锁定 20f 后：机关齐射（扇形 5 发直线激光弹, 沿冻结方向）
                if ((int)LockState == LockToFire && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int k = -2; k <= 2; k++) {
                        float a = ConeAngle + k * 0.07f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                            a.ToRotationVector2() * 13f,
                            ModContent.ProjectileType<SweepingLaserBolt>(), Projectile.damage, 1f, Main.myPlayer);
                    }
                    // 主人后坐（对自身武器的反应）
                    owner.velocity -= ConeAngle.ToRotationVector2() * 5f;
                }
                if ((int)LockState == LockToFire) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 1f, Pitch = 0.1f }, Projectile.Center);
                    ACMScreenShakeSystem.Add(4f);
                }

                if (LockState > LockToFire + FadeAfterFire)
                    Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center + ConeAngle.ToRotationVector2() * 300f,
                new Vector3(0.5f, 0.65f, 0.9f) * (LockState >= 1f ? 1.2f : 0.7f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            float locked = LockState >= 1f ? 1f : 0f;
            float fade = 1f;
            if (LockState > LockToFire)
                fade = MathHelper.Clamp(1f - (LockState - LockToFire) / FadeAfterFire, 0f, 1f);

            Effect fx = MythologyConfig.FullscreenShadersEnabled ? ScanConeEffect : null;
            if (fx == null) {
                // 退化：锥缘双线 + 锥心线（保底可读）
                float halfA = SearchHalfAngle * (locked > 0.5f ? 0.55f : 1f);
                for (int s = -1; s <= 1; s += 2) {
                    float a = ConeAngle + s * halfA;
                    ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + a.ToRotationVector2() * ConeLength, 8f,
                        locked > 0.5f ? TelegraphColors.Gold : new Color(90, 150, 215), new Color(60, 90, 140, 90),
                        0.55f * fade, coreSharp: 2.2f, coreGlow: 0.5f);
                }
                if (locked > 0.5f) {
                    ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + ConeAngle.ToRotationVector2() * ConeLength, 10f,
                        TelegraphColors.Gold, new Color(255, 160, 70, 120), 0.7f * fade, coreSharp: 2.6f, coreGlow: 0.9f);
                }
                return false;
            }

            ACMShaders.WorldDecalParams(Projectile.Center, ConeLength, out Vector2 uv, out float lenFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(0.85f * fade);
            fx.Parameters["uDir"]?.SetValue(ConeAngle);
            fx.Parameters["uHalfAngle"]?.SetValue(SearchHalfAngle);
            fx.Parameters["uLength"]?.SetValue(lenFrac);
            fx.Parameters["uLock"]?.SetValue(locked);
            fx.Parameters["uFlash"]?.SetValue(lockFlash);
            fx.Parameters["uColorSearch"]?.SetValue(new Vector4(new Color(90, 150, 215).ToVector3(), 1f));
            fx.Parameters["uColorLock"]?.SetValue(new Vector4(new Color(255, 205, 110).ToVector3(), 1f));
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx);
            return false;
        }
    }

    /// <summary>
    /// 天网律束 - 天网恢恢网格线：细线预告(收拢漂移) → 上膛(快门 28f 开/12f 熄, 熄灭窗可横穿) → 退场。
    /// 伤害窗与亮度严格对齐。ai0=朝向(0=纵线 1=横线)。
    /// </summary>
    internal class OverseerNetBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        /// <summary>细线预告时长（其间随初速向中心收拢）。</summary>
        public const int TelegraphTime = 55;
        /// <summary>上膛（伤害）时长。</summary>
        public const int ArmedTime = 130;
        /// <summary>退场淡出。</summary>
        public const int FadeTime = 15;
        /// <summary>总寿命。</summary>
        public const int TotalTime = TelegraphTime + ArmedTime + FadeTime;

        private const float HalfLength = 1700f;
        private const int ShutterPeriod = 40;   // 快门周期
        private const int ShutterOff = 12;      // 每周期熄灭帧数（横穿窗口）

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3600;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTime;
        }

        private ref float Orientation => ref Projectile.ai[0]; // 0=纵线 1=横线

        private int Age => TotalTime - Projectile.timeLeft;
        private bool Armed => Age >= TelegraphTime && Age < TelegraphTime + ArmedTime;

        /// <summary>快门开合：上膛期内周期性熄灭（熄灭窗 = 横穿机会, 各线同帧生成故全网同步）。</summary>
        private bool ShutterOn {
            get {
                if (!Armed) return false;
                int t = (Age - TelegraphTime) % ShutterPeriod;
                return t < ShutterPeriod - ShutterOff;
            }
        }

        private Vector2 LineDir => Orientation < 0.5f ? Vector2.UnitY : Vector2.UnitX;

        public override bool? CanDamage() => ShutterOn ? null : false;

        public override void AI() {
            // 预告期沿初速收拢；上膛后定死
            if (Age >= TelegraphTime)
                Projectile.velocity = Vector2.Zero;

            // 上膛瞬间咔哒
            if (Age == TelegraphTime) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            }

            if (ShutterOn) {
                Vector2 dir = LineDir;
                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.6f) * 0.8f);
                Lighting.AddLight(Projectile.Center + dir * 400f, new Vector3(1f, 0.9f, 0.6f) * 0.6f);
                Lighting.AddLight(Projectile.Center - dir * 400f, new Vector3(1f, 0.9f, 0.6f) * 0.6f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!ShutterOn)
                return false;
            float point = 0f;
            Vector2 start = Projectile.Center - LineDir * HalfLength;
            Vector2 end = Projectile.Center + LineDir * HalfLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 22f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            Vector2 start = Projectile.Center - LineDir * HalfLength;
            Vector2 end = Projectile.Center + LineDir * HalfLength;

            if (Age < TelegraphTime) {
                // 细线预告：金色渐亮 + 收拢
                float t = Age / (float)TelegraphTime;
                float pulse = 0.5f + 0.5f * MathF.Sin((float)Main.GameUpdateCount * 0.3f);
                ACMShaders.DrawBeam(start, end, 6f + t * 4f, TelegraphColors.Gold, new Color(255, 160, 70, 110),
                    (0.25f + 0.4f * t) * (0.7f + 0.3f * pulse), flowSpeed: 1.4f, flowScale: 2.4f, coreSharp: 2.4f, coreGlow: 0.5f);
            }
            else if (Armed) {
                if (ShutterOn) {
                    // 亮 = 有伤（严格对齐）
                    int t = (Age - TelegraphTime) % ShutterPeriod;
                    float ramp = MathHelper.Clamp(t / 5f, 0f, 1f); // 快门重开 5f 渐亮（重开缓冲）
                    ACMShaders.DrawBeam(start, end, 20f * ramp + 4f, TelegraphColors.Gold, new Color(255, 150, 60, 150),
                        0.95f * ramp, flowSpeed: 2.0f, flowScale: 2.0f, coreSharp: 2.4f, coreGlow: 1.0f);
                }
                else {
                    // 熄灭窗：极暗残线（位置仍可读, 无伤）
                    ACMShaders.DrawBeam(start, end, 5f, new Color(120, 110, 80), new Color(80, 70, 50, 60),
                        0.22f, flowSpeed: 1.0f, flowScale: 2.0f, coreSharp: 2.0f, coreGlow: 0.1f);
                }
            }
            else {
                // 退场淡出
                float t = 1f - (Age - TelegraphTime - ArmedTime) / (float)FadeTime;
                ACMShaders.DrawBeam(start, end, 10f * t, TelegraphColors.Gold, new Color(255, 150, 60, 100),
                    0.4f * t, flowSpeed: 1.4f, flowScale: 2.0f, coreSharp: 2.2f, coreGlow: 0.4f);
            }

            return false;
        }
    }

    /// <summary>
    /// 窥视眼泡 - 窥视相位中脱离的可击破天眼。击破降低监视槽并减少本轮真实攻击数。
    /// ai0=主人索引, ai1=序号。
    /// </summary>
    internal class OverseerScryingEye : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 44;
            NPC.height = 44;
            NPC.damage = 60;
            NPC.defense = 20;
            NPC.lifeMax = 28000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0.5f;
            if (Main.expertMode) NPC.lifeMax = 42000;
            if (Main.masterMode) NPC.lifeMax = 56000;
        }

        private ref float OwnerIndex => ref NPC.ai[0];
        private ref float Index => ref NPC.ai[1];
        private float orbitAngle;
        private float globalTime;

        public override void AI() {
            globalTime += 1f / 60f;
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<CelestialOverseer>()) {
                NPC.active = false;
                return;
            }
            Player target = Main.player[owner.target];

            orbitAngle += 0.03f + Index * 0.004f;
            float radius = 300f + MathF.Sin(globalTime * 2f + Index) * 30f;
            Vector2 targetPos = owner.Center + (orbitAngle + Index * MathHelper.TwoPi / CelestialOverseer.CelestialEyeCount).ToRotationVector2() * radius;
            NPC.velocity = (targetPos - NPC.Center) * 0.12f;
            if (target.active && !target.dead)
                NPC.rotation = (target.Center - NPC.Center).ToRotation();

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.8f, 1f) * 0.7f);
        }

        public override void OnKill() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (owner.active && owner.ModNPC is CelestialOverseer overseer) {
                overseer.OnScryingEyePopped();
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 18; i++) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(6, 6);
                    int d = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, v.X, v.Y, 100, new Color(200, 220, 255), 2f);
                    Main.dust[d].noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f }, NPC.Center);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            if (tex == null) return false;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = tex.Size() / 2f;
            Color glow = new Color(180, 210, 255) * 0.6f;
            glow.A = 0;
            spriteBatch.Draw(tex, drawPos, null, glow, NPC.rotation, origin, NPC.scale * 1.3f, SpriteEffects.None, 0f);
            Color core = new Color(255, 255, 240);
            core.A = 0;
            spriteBatch.Draw(tex, drawPos, null, core, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 天庭陪审 - 入侵终局事件召唤的"陪审团"。每名玩家 1 个，须在限时内清除。
    /// ai0=主人索引。
    /// </summary>
    internal class HeavenlyJuror : ModNPC
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 60;
            NPC.height = 60;
            NPC.damage = 90;
            NPC.defense = 40;
            NPC.lifeMax = 90000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 1f;
            if (Main.expertMode) { NPC.lifeMax = 130000; NPC.damage = 110; }
            if (Main.masterMode) { NPC.lifeMax = 170000; NPC.damage = 125; }
        }

        private ref float OwnerIndex => ref NPC.ai[0];
        private ref float AttackTimer => ref NPC.ai[1];
        private float globalTime;

        public override void AI() {
            globalTime += 1f / 60f;
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<CelestialOverseer>()) {
                NPC.active = false;
                return;
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) { NPC.velocity *= 0.95f; return; }

            // 向玩家侧方悬浮逼近
            Vector2 desired = target.Center + new Vector2(MathF.Sin(globalTime * 1.3f + NPC.whoAmI) * 220f, -160);
            Vector2 toDesired = desired - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toDesired * 0.04f, 0.08f);
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            AttackTimer++;
            float cd = Main.expertMode ? 50f : 70f;
            if (AttackTimer >= cd && Main.netMode != NetmodeID.MultiplayerClient) {
                AttackTimer = 0;
                Vector2 toT = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = toT.RotatedBy(MathHelper.ToRadians(12 * i)) * 8.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<HolyOrb>(), NPC.damage / 2, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f }, NPC.Center);
            }

            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.9f, 0.6f) * 0.7f);
        }

        public override void OnKill() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 24; i++) {
                    Vector2 v = Main.rand.NextVector2CircularEdge(7, 7);
                    int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                    int d = Dust.NewDust(NPC.Center, 0, 0, dustType, v.X, v.Y, 90, default, 2.2f);
                    Main.dust[d].noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f }, NPC.Center);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            if (tex == null) return false;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float p = 1f - (float)i / NPC.oldPos.Length;
                Color tc = new Color(255, 230, 150) * p * 0.3f;
                tc.A = 0;
                Vector2 tp = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                spriteBatch.Draw(tex, tp, null, tc, NPC.oldRot[i], origin, NPC.scale * p, SpriteEffects.None, 0f);
            }
            Color glow = new Color(255, 220, 150) * 0.6f;
            glow.A = 0;
            spriteBatch.Draw(tex, drawPos, null, glow, NPC.rotation, origin, NPC.scale * 1.4f, SpriteEffects.None, 0f);
            Color core = new Color(255, 255, 240);
            core.A = 0;
            spriteBatch.Draw(tex, drawPos, null, core, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    #endregion
}

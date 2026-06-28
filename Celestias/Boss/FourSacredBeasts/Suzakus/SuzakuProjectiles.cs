using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    // ================================================================
    //  朱雀 V2 自定义焰弹 —— 替换 V1 的 ProjectileID.InfernoFriendlyBlast 原版占位。
    //  所有弹幕复用现有贴图 AncientChineseMythology/Textures/Projectiles/BlankProjectile（贴图安全），
    //  视觉全部由 ACMAsset 灰度图程序化合成（服务端零绘制）。
    //  观感契约 §6.1：红 = 致命专用；非致命火焰预警一律用 Flame/Vermilion。
    // ================================================================

    /// <summary>
    /// 朱雀·赤焰余烬 —— 快、窄、低伤的压制弹（hitbox 16²）。与焰羽形成"快窄/慢宽"对比。
    /// </summary>
    public class SuzakuEmber : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float pulse;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 220;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            pulse += 0.3f;
            Lighting.AddLight(Projectile.Center, 0.9f, 0.4f, 0.1f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Torch,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float p = 1f + MathF.Sin(pulse * 4f) * 0.15f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 余烬尾迹
            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 go = glow.Size() / 2f;
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                Vector2 tp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float t = 1f - (float)i / ProjectileID.Sets.TrailCacheLength[Type];
                Color tc = Color.Lerp(new Color(255, 120, 40), new Color(150, 20, 10), 1f - t) * (t * 0.45f);
                tc.A = 0;
                sb.Draw(glow, tp, null, tc, 0f, go, 0.28f * t, SpriteEffects.None, 0f);
            }

            // LightShot 速度感拉伸芯（朝右贴图 → 直接用 rotation）
            Texture2D shot = ACMAsset.LightShot;
            Vector2 so = shot.Size() / 2f;
            Color streak = new Color(255, 180, 80, 0) * 0.85f;
            sb.Draw(shot, drawPos, null, streak, Projectile.rotation, so, new Vector2(0.55f, 0.32f) * p, SpriteEffects.None, 0f);

            // 核心
            Color core = new Color(255, 235, 200, 0) * 0.9f;
            sb.Draw(glow, drawPos, null, core, 0f, go, 0.22f * p, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3), 100, default, 1.3f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 朱雀·日炎焰羽 —— 慢、宽、穿透的大判定弹（hitbox 46²）。与余烬形成"快窄/慢宽"对比。
    /// </summary>
    public class SuzakuFeather : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float wave;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 320;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            wave += 0.12f;
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.15f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(18, 18), 0, 0,
                    DustID.SolarFlare, -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1.4f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float p = 1f + MathF.Sin(wave * 3f) * 0.12f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 拖尾羽影
            Texture2D blade = ACMAsset.GlaciateWave;
            Vector2 bo = blade.Size() / 2f;
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                Vector2 tp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float t = 1f - (float)i / ProjectileID.Sets.TrailCacheLength[Type];
                Color tc = Color.Lerp(new Color(255, 150, 60), new Color(150, 25, 12), 1f - t) * (t * 0.4f);
                tc.A = 0;
                sb.Draw(blade, tp, null, tc, Projectile.rotation, bo, new Vector2(0.22f, 0.11f) * t, SpriteEffects.None, 0f);
            }

            // 焰羽主体（月牙）
            Color outer = new Color(255, 130, 50, 0) * (0.5f * p);
            sb.Draw(blade, drawPos, null, outer, Projectile.rotation, bo, new Vector2(0.34f, 0.18f) * p, SpriteEffects.None, 0f);
            Color main = new Color(255, 185, 80, 0) * 0.7f;
            sb.Draw(blade, drawPos, null, main, Projectile.rotation, bo, new Vector2(0.26f, 0.12f), SpriteEffects.None, 0f);

            // 羽尖余烬
            Texture2D shards = ACMAsset.EmberShards;
            if (shards != null) {
                Color ec = new Color(255, 220, 140, 0) * 0.55f;
                sb.Draw(shards, drawPos, null, ec, wave, shards.Size() / 2f, 0.10f * p, SpriteEffects.None, 0f);
            }

            Texture2D glow = ACMAsset.SoftGlow;
            Color gc = new Color(255, 210, 150, 0) * 0.6f;
            sb.Draw(glow, drawPos, null, gc, 0f, glow.Size() / 2f, 0.5f * p, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.SolarFlare, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 100, default, 1.6f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 朱雀·日轮火柱 —— 地面"太阳符"先预警（windup 非致命），再点燃为垂直火柱（strike 致命）。
    /// 自带 telegraph 生命周期：spawn 即在落点画太阳符 → 倒计时 → 烈焰升腾。
    /// ai[0] 由内部计时；窗口由 <see cref="WindupTicks"/>/<see cref="StrikeTicks"/> 决定。
    /// </summary>
    public class SuzakuSunPillar : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public const int WindupTicks = 45;   // 地面太阳符预警（§6.3 中等）
        public const int StrikeTicks = 38;    // 火柱致命窗口
        private const int FadeTicks = 16;

        private int Timer => (int)Projectile.ai[0];
        private bool StrikeActive => Timer >= WindupTicks && Timer < WindupTicks + StrikeTicks;

        public override void SetDefaults() {
            Projectile.width = 86;
            Projectile.height = 560;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupTicks + StrikeTicks + FadeTicks;
            Projectile.ignoreWater = true;
        }

        public override bool CanHitPlayer(Player target) => StrikeActive;

        public override void AI() {
            Projectile.ai[0]++;
            Vector2 basePos = Projectile.Bottom;

            if (Timer == WindupTicks) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.2f, Volume = 0.9f }, basePos);
                if (!Main.dedServ) {
                    for (int i = 0; i < 26; i++) {
                        Dust d = Dust.NewDustDirect(basePos - new Vector2(Projectile.width / 2f, 8), Projectile.width, 16,
                            DustID.SolarFlare, Main.rand.NextFloat(-2, 2), Main.rand.NextFloat(-9, -2), 100, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }

            if (StrikeActive) {
                Lighting.AddLight(basePos + new Vector2(0, -160), 1.6f, 0.7f, 0.2f);
                if (!Main.dedServ && Main.rand.NextBool()) {
                    Dust d = Dust.NewDustDirect(basePos - new Vector2(Projectile.width / 2f, Main.rand.Next(0, 480)),
                        Projectile.width, 10, DustID.Torch, 0, Main.rand.NextFloat(-5, -1), 100, default, 1.8f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 basePos = Projectile.Bottom - Main.screenPosition;
            int t = Timer;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 go = glow.Size() / 2f;

            if (t < WindupTicks) {
                // —— 地面太阳符预警（非致命：朱雀赤/橙）——
                float grow = t / (float)WindupTicks;
                float ringScale = (0.55f + grow * 0.5f);
                Color ring = new Color(255, 110, 50, 0) * (0.35f + grow * 0.45f);
                sb.Draw(glow, basePos, null, ring, 0f, go, ringScale * 1.4f, SpriteEffects.None, 0f);

                // 太阳"光芒"符（Sparkle 放射）
                Texture2D spark = ACMAsset.Sparkle;
                if (spark != null) {
                    Color sc = new Color(255, 170, 70, 0) * (0.3f + grow * 0.55f);
                    sb.Draw(spark, basePos, null, sc, t * 0.04f, spark.Size() / 2f, (0.4f + grow * 0.45f), SpriteEffects.None, 0f);
                    sb.Draw(spark, basePos, null, sc * 0.7f, -t * 0.03f, spark.Size() / 2f, (0.45f + grow * 0.4f), SpriteEffects.None, 0f);
                }
            }
            else {
                // —— 烈焰火柱 ——
                int st = t - WindupTicks;
                float life = st < 6 ? st / 6f : 1f - MathHelper.Clamp((st - StrikeTicks) / (float)FadeTicks, 0f, 1f);
                life = MathHelper.Clamp(life, 0f, 1f);

                Texture2D burst = ACMAsset.SlashBurst;
                if (burst != null) {
                    Vector2 bo = new(burst.Width / 2f, burst.Height * 0.92f);
                    float h = Projectile.height / (float)burst.Height * 1.05f;
                    Color outer = new Color(255, 90, 35, 0) * (0.5f * life);
                    sb.Draw(burst, basePos, null, outer, 0f, bo, new Vector2(0.32f, h) * (1f + MathF.Sin(t * 0.6f) * 0.05f), SpriteEffects.None, 0f);
                    Color mid = new Color(255, 160, 60, 0) * (0.7f * life);
                    sb.Draw(burst, basePos, null, mid, 0f, bo, new Vector2(0.22f, h), SpriteEffects.None, 0f);
                    Color core = new Color(255, 235, 190, 0) * (0.65f * life);
                    sb.Draw(burst, basePos, null, core, 0f, bo, new Vector2(0.12f, h * 0.96f), SpriteEffects.None, 0f);
                }
                Color baseGlow = new Color(255, 200, 120, 0) * (0.7f * life);
                sb.Draw(glow, basePos, null, baseGlow, 0f, go, 1.1f, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 朱雀·赤日审判光束 —— 锚定本体、可缓扫的光束。windup 细线非致命预警（赤），strike 粗芯致命（红）。
    /// 经硬化 <see cref="ACMShaders.DrawBeam"/> 绘制；命中用线段-AABB 判定，仅 strike 生效。
    /// ai[0]=本体 NPC 索引；ai[1]=扫掠角速度(rad/帧, 可 0)。
    /// </summary>
    public class SuzakuSolarBeam : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public const int WindupTicks = 52;
        public const int StrikeTicks = 42;
        private const int FadeTicks = 14;
        private const float BeamLength = 2400f;

        private int Timer => (int)Projectile.localAI[0];
        private bool StrikeActive => Timer >= WindupTicks && Timer < WindupTicks + StrikeTicks;
        private float baseAngle;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupTicks + StrikeTicks + FadeTicks;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int idx = (int)Projectile.ai[0];
            if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active ||
                Main.npc[idx].type != ModContent.NPCType<Suzaku>()) {
                Projectile.Kill();
                return;
            }
            NPC boss = Main.npc[idx];
            Projectile.Center = boss.Center;

            if (Projectile.localAI[0] == 0f)
                baseAngle = Projectile.velocity.ToRotation();
            else
                baseAngle = Projectile.localAI[1];

            baseAngle += Projectile.ai[1];
            Projectile.localAI[1] = baseAngle;
            Projectile.rotation = baseAngle;
            Projectile.localAI[0]++;

            if (Timer == WindupTicks) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, Projectile.Center);
            }
            if (StrikeActive && !Main.dedServ && Main.rand.NextBool()) {
                Vector2 dir = baseAngle.ToRotationVector2();
                float d = Main.rand.NextFloat(120, BeamLength);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * d + Main.rand.NextVector2Circular(20, 20),
                    DustID.SolarFlare, dir.RotatedByRandom(0.2f) * 2f, 100, default, 1.6f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.15f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!StrikeActive) return false;
            float point = 0f;
            Vector2 start = Projectile.Center;
            Vector2 end = start + baseAngle.ToRotationVector2() * BeamLength;
            float hitW = 44f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, hitW, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            int t = Timer;
            Vector2 start = Projectile.Center;
            Vector2 end = start + baseAngle.ToRotationVector2() * BeamLength;

            if (t < WindupTicks) {
                // 细线预警（非致命：朱雀赤）
                float grow = t / (float)WindupTicks;
                Color core = TelegraphColors.Vermilion;
                Color edge = TelegraphColors.Flame;
                ACMShaders.DrawBeam(start, end, 4f + grow * 5f, core, edge, 0.25f + grow * 0.35f, 2.2f, 2.4f, 2.6f);
            }
            else {
                int st = t - WindupTicks;
                float life = st < 5 ? st / 5f : 1f - MathHelper.Clamp((st - StrikeTicks) / (float)FadeTicks, 0f, 1f);
                life = MathHelper.Clamp(life, 0f, 1f);
                // 致命光束：红芯 + 金边
                Color core = TelegraphColors.Lethal;
                Color edge = TelegraphColors.Gold;
                ACMShaders.DrawBeam(start, end, 46f * life, core, edge, life, 1.6f, 2.0f, 2.0f);
                ACMShaders.DrawBeam(start, end, 18f * life, Color.White, core, life * 0.9f, 1.6f, 2.0f, 2.6f);
            }
            return false;
        }
    }
}

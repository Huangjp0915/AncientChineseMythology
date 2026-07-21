using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    #region 1. 风刃 — 臂段挥出的弧线风压刃

    /// <summary>
    /// 风刃 — 臂段错相挥出, 微弧线飞行, 起步降速后加速（公平阀门: 首波慢 35%）。
    /// ai[0] = 弧线弯曲方向(±1), ai[1] = 起步速度比例
    /// 绘制: AoshunWindBlade.fx 程序化新月刃（SDF+流动噪声）+ LightShot 拖尾; 着色器缺失时退化 GlaciateWave
    /// </summary>
    public class AoshunWindBlade : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static Asset<Effect> bladeShaderRef;

        private const int LifeTime = 210;
        private float bladePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            bladePhase += 0.2f;
            int age = LifeTime - Projectile.timeLeft;

            // 起步降速→60f 内线性恢复→之后缓慢加速(风越吹越急)
            float startScale = Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
            if (age < 60) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                    * MathHelper.Lerp(11.5f * startScale, 11.5f, age / 60f);
            }
            else if (Projectile.velocity.Length() < 17f) {
                Projectile.velocity *= 1.012f;
            }

            // 微弧线
            Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] * 0.0038f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12, 12), DustID.Cloud);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = -Projectile.velocity * 0.06f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.NorthSeaCyan.ToVector3() * 0.35f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f)
                       * MathHelper.Clamp((LifeTime - Projectile.timeLeft) / 10f, 0f, 1f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // LightShot 拖尾
            Texture2D shot = ACMAsset.LightShot;
            if (shot != null) {
                Vector2 sOrigin = shot.Size() / 2f;
                for (int i = 1; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float p = 1f - i / (float)Projectile.oldPos.Length;
                    Color tc = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.NorthSeaCyan, p) * (p * 0.5f * fade);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    sb.Draw(shot, pos, null, tc, Projectile.oldRot[i], sOrigin, new Vector2(0.6f, 0.22f) * p, SpriteEffects.None, 0f);
                }
            }

            // 程序化新月刃（AoshunWindBlade.fx）; 着色器缺失时退化为 GlaciateWave 双层
            bladeShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/AoshunWindBlade", AssetRequestMode.ImmediateLoad);
            Effect bladeFx = bladeShaderRef?.Value;
            if (bladeFx != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, bladeFx, Main.GameViewMatrix.TransformationMatrix);

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                bladeFx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                bladeFx.Parameters["uIntensity"]?.SetValue(fade);
                bladeFx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.31f);
                bladeFx.Parameters["uColorCore"]?.SetValue(AoshunHelper.ElectricWhite.ToVector4());
                bladeFx.Parameters["uColorEdge"]?.SetValue(AoshunHelper.NorthSeaCyan.ToVector4());

                // 以中心为锚的旋转四边形: 用 placeholder 像素铺 quad, UV 0~1
                Texture2D pixel = VaultAsset.placeholder2.Value;
                const float BladeSize = 108f;
                sb.Draw(pixel, drawPos, new Rectangle(0, 0, pixel.Width, pixel.Height), Color.White,
                    Projectile.rotation, new Vector2(pixel.Width / 2f, pixel.Height / 2f),
                    BladeSize / pixel.Width, SpriteEffects.None, 0f);
            }
            else {
                Texture2D wave = ACMAsset.GlaciateWave;
                if (wave != null) {
                    Vector2 origin = wave.Size() / 2f;
                    float wobble = MathF.Sin(bladePhase) * 0.05f;
                    sb.Draw(wave, drawPos, null, AoshunHelper.NorthSeaCyan * 0.85f * fade,
                        Projectile.rotation + wobble, origin, new Vector2(0.30f, 0.115f), SpriteEffects.None, 0f);
                    sb.Draw(wave, drawPos, null, AoshunHelper.ElectricWhite * 0.55f * fade,
                        Projectile.rotation - wobble * 0.6f, origin, new Vector2(0.22f, 0.07f), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 刃形碰撞: 沿速度方向的线段
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 a = Projectile.Center - dir * 42f;
            Vector2 b = Projectile.Center + dir * 42f;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 22f, ref point);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
                d.scale = 1.4f;
            }
        }
    }

    #endregion

    #region 2. 龙鳞弹幕 — 顶点拖尾 + EmberShards碎片

    /// <summary>
    /// 带电龙鳞 — 破渊突袭喷泉式抛射, 带重力和弹跳
    /// 绘制: ColoredVertex TriangleStrip 拖尾 + EmberShards 碎片弹体, 弹跳时 Sparkle 闪光
    /// </summary>
    public class AoshunDragonScale : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int bounceCount;
        private float scalePhase;
        private float trailOffset;
        private float bounceFlash;

        private readonly List<Vector2> oldPositions = new();
        private const int MaxTrailLength = 12;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            scalePhase += 0.15f;
            trailOffset += 0.06f;
            Projectile.velocity.Y += 0.15f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            if (!VaultUtils.isServer) {
                oldPositions.Insert(0, Projectile.Center);
                if (oldPositions.Count > MaxTrailLength)
                    oldPositions.RemoveAt(oldPositions.Count - 1);
            }

            if (bounceFlash > 0) bounceFlash *= 0.9f;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = -Projectile.velocity * 0.08f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.NorthSeaCyan.ToVector3() * 0.4f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bounceCount++;
            if (bounceCount >= 2) return true;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 1f)
                Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 1f)
                Projectile.velocity.X = -oldVelocity.X * 0.5f;
            bounceFlash = 1f;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(3, 3);
                    d.scale = 1.5f;
                }
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(scalePhase * 2f) * 0.15f;

            if (oldPositions.Count >= 3) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                List<ColoredVertex> vertices = new();
                int count = oldPositions.Count;
                for (int i = 0; i < count; i++) {
                    float t = (float)i / count;
                    Vector2 basePos = oldPositions[i] - Main.screenPosition;
                    Vector2 dir = i < count - 1
                        ? (oldPositions[i] - oldPositions[Math.Min(i + 1, count - 1)]).SafeNormalize(Vector2.UnitX)
                        : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perpDir = new(-dir.Y, dir.X);
                    float width = 14f * (1f - t) * pulse;
                    Color c = Color.Lerp(AoshunHelper.NorthSeaCyan * 0.9f, AoshunHelper.ThunderPurple * 0.4f, t);
                    c.A = 0;
                    vertices.Add(new ColoredVertex(basePos + perpDir * width, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePos - perpDir * width, new Vector3(t + trailOffset, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            Texture2D shardsTex = ACMAsset.EmberShards;
            if (shardsTex != null) {
                int shardIndex = Projectile.whoAmI % 9;
                int shardW = shardsTex.Width / 3;
                int shardH = shardsTex.Height / 3;
                Rectangle shardRect = new(shardIndex % 3 * shardW, shardIndex / 3 * shardH, shardW, shardH);
                Vector2 shardOrigin = new(shardW / 2f, shardH / 2f);

                Color shardColor = Color.Lerp(AoshunHelper.NorthSeaCyan, AoshunHelper.LightningBlue, 0.5f + MathF.Sin(scalePhase) * 0.3f) * 0.85f;
                shardColor.A = 30;
                sb.Draw(shardsTex, drawPos, shardRect, shardColor, Projectile.rotation, shardOrigin, 0.18f * pulse, SpriteEffects.None, 0f);

                Color whiteLayer = AoshunHelper.ElectricWhite * 0.4f;
                whiteLayer.A = 0;
                sb.Draw(shardsTex, drawPos, shardRect, whiteLayer, Projectile.rotation, shardOrigin, 0.14f * pulse, SpriteEffects.None, 0f);
            }

            if (bounceFlash > 0.05f && ACMAsset.Sparkle != null) {
                Color flashColor = AoshunHelper.ElectricWhite * bounceFlash * 0.6f;
                flashColor.A = 0;
                sb.Draw(ACMAsset.Sparkle, drawPos, null, flashColor, scalePhase * 2f,
                    ACMAsset.Sparkle.Size() / 2f, 0.2f * bounceFlash, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 3. 龙卷 — AoshunCyclone 程序化着色器漏斗

    /// <summary>
    /// 程序化龙卷 — 压掌落地(mode 0)或沿风暴眼壁巡游(mode 1)。
    /// ai[0] = 模式, ai[1] = 壁巡方向(±1), ai[2] = 头部 whoAmI
    /// 公平: 生成 30f 后才有伤害; 吸力 20f 起效、随暴露时间 90f 线性衰减、不追踪玩家。
    /// 绘制: AoshunCyclone.fx 四边形（柱面云带/湍流/内部电闪）+ 底部扬尘。
    /// </summary>
    public class AoshunTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static Asset<Effect> cycloneShaderRef;

        private const int LifeTime = 360;
        private const float FunnelHeight = 430f;
        private const float FunnelHalfWidth = 120f;

        private float grow;          // 生成/消散渐变 0~1
        private float wallAngle;     // 模式1: 当前壁面角
        private bool wallInit;
        private float suckExposure;  // 本地玩家暴露计时（抗性衰减, 纯本地）

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        private Aoshun Head {
            get {
                int idx = (int)Projectile.ai[2];
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].ModNPC is Aoshun head)
                    return head;
                return null;
            }
        }

        public override void AI() {
            int age = LifeTime - Projectile.timeLeft;
            grow = Projectile.timeLeft < 40
                ? Projectile.timeLeft / 40f
                : MathHelper.Clamp(age / 30f, 0f, 1f);

            if ((int)Projectile.ai[0] == 1) {
                // 沿风暴眼壁巡游
                Aoshun head = Head;
                if (head != null && head.EyeActive) {
                    if (!wallInit) {
                        wallInit = true;
                        wallAngle = (Projectile.Center - head.EyeCenter).ToRotation();
                    }
                    wallAngle += Projectile.ai[1] * 0.011f;
                    Vector2 goal = head.EyeCenter + wallAngle.ToRotationVector2() * (head.EyeRadius - 60f);
                    Projectile.Center = Vector2.Lerp(Projectile.Center, goal, 0.12f);
                }
                else {
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, 40); // 眼消失则消散
                }
            }
            else {
                // 定点扎地: 轻微摇摆
                Projectile.velocity = Vector2.Zero;
                Projectile.position.X += MathF.Sin(age * 0.05f + Projectile.whoAmI) * 0.4f;
            }

            // —— 公平吸力: 只作用本地玩家, 20f 起效, 暴露 90f 内线性衰减到 0 ——
            if (!Main.dedServ) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead) {
                    Vector2 funnelMid = Projectile.Center + new Vector2(0, -FunnelHeight * 0.45f);
                    float dist = Vector2.Distance(funnelMid, lp.Center);
                    if (dist < 300f && grow > 0.6f && age > 20) {
                        suckExposure = Math.Min(suckExposure + 1f, 90f);
                        float resist = 1f - suckExposure / 90f;
                        float strength = 0.42f * resist * (1f - dist / 300f);
                        lp.velocity += (funnelMid - lp.Center).SafeNormalize(Vector2.Zero) * strength;
                    }
                    else {
                        suckExposure = Math.Max(suckExposure - 2f, 0f);
                    }
                }
            }

            // 底部扬尘 + 环绕碎云
            if (!VaultUtils.isServer && grow > 0.3f) {
                if (Main.rand.NextBool(2)) {
                    float h = Main.rand.NextFloat(FunnelHeight);
                    float w = MathHelper.Lerp(30f, FunnelHalfWidth, h / FunnelHeight) * grow;
                    Vector2 p = Projectile.Center + new Vector2(Main.rand.NextFloat(-w, w), -h);
                    var d = Dust.NewDustPerfect(p, Main.rand.NextBool(4) ? DustID.Electric : DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = new Vector2(MathF.Sign(p.X - Projectile.Center.X) * -3f, -2.5f);
                }
                if (Main.rand.NextBool(3)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), 0), DustID.Smoke);
                    d.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f));
                    d.scale = 1.6f;
                }
            }

            Lighting.AddLight(Projectile.Center + new Vector2(0, -FunnelHeight * 0.4f),
                AoshunHelper.StormGray.ToVector3() * 0.6f * grow);
        }

        /// <summary>成型 50% 前无伤害（生成即安全读秒）</summary>
        public override bool? CanDamage() => grow > 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 漏斗胶囊: 底窄顶宽, 用两段线段近似
            Vector2 baseP = Projectile.Center;
            Vector2 topP = Projectile.Center + new Vector2(0, -FunnelHeight);
            float point = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                baseP, topP, 62f * grow, ref point))
                return true;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            cycloneShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/AoshunCyclone", AssetRequestMode.ImmediateLoad);
            Effect fx = cycloneShaderRef?.Value;
            SpriteBatch sb = Main.spriteBatch;

            if (fx != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(grow);
                fx.Parameters["uSpin"]?.SetValue((int)Projectile.ai[0] == 1 ? 1.35f : 1f);
                fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.137f);
                fx.Parameters["uColorInner"]?.SetValue(new Color(66, 72, 96).ToVector4());
                fx.Parameters["uColorRim"]?.SetValue(AoshunHelper.LightningBlue.ToVector4());

                // 四边形: 以底点为锚, 向上展开
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 basePos = Projectile.Center - Main.screenPosition;
                Rectangle dest = new(
                    (int)(basePos.X - FunnelHalfWidth * grow),
                    (int)(basePos.Y - FunnelHeight),
                    (int)(FunnelHalfWidth * 2f * grow),
                    (int)FunnelHeight);
                sb.Draw(pixel, dest, Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // 底部涡流光晕
            if (ACMAsset.SoftGlow != null) {
                Color vortexColor = AoshunHelper.ThunderPurple * 0.3f * grow;
                vortexColor.A = 0;
                sb.Draw(ACMAsset.SoftGlow, Projectile.Center - Main.screenPosition, null, vortexColor,
                    (float)Main.GlobalTimeWrappedHourly * 2f, ACMAsset.SoftGlow.Size() / 2f, 1.0f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(0, -Main.rand.NextFloat(FunnelHeight)), DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 5f;
                d.scale = 2f;
            }
        }
    }

    #endregion

    #region 4. 天雷印 — LightningBranch预警 + SlashBurst引爆 + 顶点雷柱

    /// <summary>
    /// 天雷印标记 — 固定位置, 倒计时后引爆为雷击柱。
    /// ai[0] = 延迟帧数。预警符环 青白→纯红(致命契约)渐变。
    /// </summary>
    public class AoshunThunderSeal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float sealPhase;
        private bool detonated;
        private Vector2 detonationCenter;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            sealPhase += 0.1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0]--;

            if (Projectile.ai[0] > 0) {
                float urgency = Math.Clamp(1f - Projectile.ai[0] / 90f, 0f, 1f);
                if (!VaultUtils.isServer) {
                    int particleCount = (int)(urgency * 3) + 1;
                    for (int i = 0; i < particleCount; i++) {
                        Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(30 + urgency * 20, 30 + urgency * 20);
                        var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                        d.noGravity = true;
                        d.scale = 1f + urgency;
                        d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + urgency * 3f);
                    }
                }
                if (Projectile.ai[0] == 10)
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
                Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.4f * urgency);
            }
            else if (!detonated) {
                detonated = true;
                detonationCenter = Projectile.Center;
                Projectile.hostile = true;
                Projectile.width = 80;
                Projectile.height = 800;
                Projectile.position.X -= 20;
                Projectile.position.Y -= 700;
                Projectile.timeLeft = 15;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.5f }, Projectile.Center);

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 20; i++) {
                        Vector2 dustPos = detonationCenter + new Vector2(Main.rand.NextFloat(-40, 40), Main.rand.NextFloat(-300, 0));
                        var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 2.5f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-8, -2));
                    }
                }
                Lighting.AddLight(detonationCenter, AoshunHelper.ElectricWhite.ToVector3() * 2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (!detonated) {
                float urgency = Math.Clamp(1f - Projectile.ai[0] / 90f, 0f, 1f);
                float pulse = 1f + MathF.Sin(sealPhase * 4f) * 0.3f * urgency;

                // 致命预警语言: 青白电弧 → 命中前最后阶段纯红
                float lethalRamp = Math.Clamp((urgency - 0.6f) / 0.4f, 0f, 1f);
                Color teleColor = Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Lethal, lethalRamp);

                Texture2D branchTex = ACMAsset.LightningBranch;
                if (branchTex != null) {
                    Vector2 branchOrigin = branchTex.Size() / 2f;
                    for (int r = 0; r < 4; r++) {
                        float angle = sealPhase * 2f + MathHelper.TwoPi * r / 4;
                        float radius = 25f + urgency * 15f;
                        Vector2 runePos = drawPos + angle.ToRotationVector2() * radius;
                        Color runeColor = teleColor * (0.3f + urgency * 0.5f);
                        runeColor.A = 0;
                        sb.Draw(branchTex, runePos, null, runeColor, angle + MathHelper.PiOver2, branchOrigin,
                            (0.06f + urgency * 0.04f) * pulse, SpriteEffects.None, 0f);
                    }
                }

                if (ACMAsset.SoftGlow != null) {
                    Color markColor = Color.Lerp(AoshunHelper.ThunderPurple, TelegraphColors.Lethal, lethalRamp) * (0.2f + urgency * 0.35f);
                    markColor.A = 0;
                    sb.Draw(ACMAsset.SoftGlow, drawPos, null, markColor, 0f,
                        ACMAsset.SoftGlow.Size() / 2f, (0.5f + urgency * 0.3f) * pulse, SpriteEffects.None, 0f);
                }
            }
            else {
                float fade = Projectile.timeLeft / 15f;
                Vector2 pillarBase = detonationCenter - Main.screenPosition;

                Texture2D burstTex = ACMAsset.SlashBurst;
                if (burstTex != null) {
                    Vector2 burstOrigin = new(burstTex.Width / 2f, burstTex.Height);
                    Color burstColor = AoshunHelper.ElectricWhite * fade * 0.7f;
                    burstColor.A = 0;
                    sb.Draw(burstTex, pillarBase, null, burstColor, 0f, burstOrigin, 0.4f * fade, SpriteEffects.None, 0f);
                    sb.Draw(burstTex, pillarBase, null, burstColor * 0.5f, MathHelper.Pi,
                        new Vector2(burstTex.Width / 2f, 0), 0.32f * fade, SpriteEffects.None, 0f);
                }

                // 顶点雷柱（外层锯齿 + 内层亮芯）
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                DrawPillarStrip(gd, pillarBase, fade, 40f, 25f,
                    Color.Lerp(AoshunHelper.LightningBlue * 0.8f, AoshunHelper.ThunderPurple * 0.5f, 0.5f),
                    ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value);
                DrawPillarStrip(gd, pillarBase, fade, 15f, 10f,
                    AoshunHelper.ElectricWhite * 0.9f, VaultAsset.placeholder2.Value);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                if (ACMAsset.Sparkle != null) {
                    Color sparkColor = AoshunHelper.ElectricWhite * fade * 0.6f;
                    sparkColor.A = 0;
                    sb.Draw(ACMAsset.Sparkle, pillarBase, null, sparkColor, sealPhase * 3f,
                        ACMAsset.Sparkle.Size() / 2f, 0.5f * fade, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        private void DrawPillarStrip(GraphicsDevice gd, Vector2 pillarBase, float fade,
            float baseWidth, float tipShrink, Color color, Texture2D tex) {
            const int Segments = 25;
            const float PillarHeight = 800f;
            List<ColoredVertex> verts = new();
            for (int s = 0; s <= Segments; s++) {
                float t = (float)s / Segments;
                Vector2 segPos = pillarBase + new Vector2(0, -t * PillarHeight);
                segPos.X += MathF.Sin(sealPhase * 5f + t * 8f) * (10f + t * 5f);
                float width = (baseWidth - t * tipShrink) * fade;
                Color c = color * fade;
                verts.Add(new ColoredVertex(segPos + new Vector2(-width, 0), new Vector3(t, 0, 1), c));
                verts.Add(new ColoredVertex(segPos + new Vector2(width, 0), new Vector3(t, 1, 1), c));
            }
            if (verts.Count >= 3) {
                gd.Textures[0] = tex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts.ToArray(), 0, verts.Count - 2);
            }
        }
    }

    #endregion

    #region 5. 天雷柱 — 唤雷系: 细红线预告 → 贯天雷击

    /// <summary>
    /// 天雷柱 — 张臂唤雷/眼缘落雷的落雷单元。
    /// ai[0] = 预警帧数。预警期: 自天而降的细线 青白→红; 引爆: 贯天雷柱 14f。
    /// 绘制: ACMShaders.DrawBeam 主柱 + LightningBranch 分叉装饰。
    /// </summary>
    public class AoshunSkyBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float PillarHeight = 1200f;
        private const int StrikeTime = 14;

        private bool struck;
        private float warnTotal;
        private float branchSeed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (warnTotal <= 0f) {
                warnTotal = Math.Max(Projectile.ai[0], 1f);
                branchSeed = Projectile.whoAmI * 2.39996f;
            }

            Projectile.ai[0]--;

            if (Projectile.ai[0] > 0) {
                float urgency = 1f - Projectile.ai[0] / warnTotal;
                if (!VaultUtils.isServer && Main.rand.NextFloat() < urgency * 0.6f) {
                    Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), -Main.rand.NextFloat(60f));
                    var d = Dust.NewDustPerfect(dustPos, DustID.Electric);
                    d.noGravity = true;
                    d.scale = 0.9f + urgency;
                    d.velocity = new Vector2(0, -1.5f - urgency * 2f);
                }
                if ((int)Projectile.ai[0] == 8)
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 0.7f }, Projectile.Center);
            }
            else if (!struck) {
                struck = true;
                Projectile.hostile = true;
                Projectile.width = 76;
                Projectile.height = (int)PillarHeight;
                Projectile.position = new Vector2(Projectile.Center.X - 38, Projectile.Center.Y - PillarHeight + 20);
                Projectile.timeLeft = StrikeTime;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.15f, Volume = 1.3f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(4f);
                    Vector2 basePos = new(Projectile.Center.X, Projectile.position.Y + PillarHeight - 20);
                    for (int i = 0; i < 16; i++) {
                        var d = Dust.NewDustPerfect(basePos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                        d.noGravity = true;
                        d.scale = 2.2f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-5, 5), -Main.rand.NextFloat(2f, 9f));
                    }
                    Lighting.AddLight(basePos, AoshunHelper.ElectricWhite.ToVector3() * 2f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 groundPos = struck
                ? new Vector2(Projectile.Center.X, Projectile.position.Y + PillarHeight - 20)
                : Projectile.Center;
            Vector2 skyPos = groundPos - new Vector2(0, PillarHeight);

            if (!struck) {
                // 细红线预告: 天→地, 青白起红收
                float urgency = 1f - Projectile.ai[0] / Math.Max(warnTotal, 1f);
                Color lineColor = Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Lethal, ACMUtils.QuadIn(urgency));
                float width = 1.5f + urgency * 2.5f;
                ACMShaders.DrawBeam(skyPos, groundPos, width, lineColor, lineColor * 0.4f,
                    0.35f + urgency * 0.55f, flowSpeed: 3f, flowScale: 3f, coreSharp: 3f);

                // 落点符环
                if (ACMAsset.SoftGlow != null) {
                    Color markColor = lineColor * (0.25f + urgency * 0.4f);
                    markColor.A = 0;
                    Main.spriteBatch.Draw(ACMAsset.SoftGlow, groundPos - Main.screenPosition, null, markColor,
                        0f, ACMAsset.SoftGlow.Size() / 2f, 0.4f + urgency * 0.35f, SpriteEffects.None, 0f);
                }
            }
            else {
                float fade = Projectile.timeLeft / (float)StrikeTime;
                // 主雷柱
                ACMShaders.DrawBeam(skyPos, groundPos, 30f * fade + 6f,
                    AoshunHelper.ElectricWhite, AoshunHelper.LightningBlue, fade,
                    flowSpeed: 5f, flowScale: 1.6f, coreSharp: 2.8f, coreGlow: 1.2f);

                // LightningBranch 分叉装饰
                Texture2D branch = ACMAsset.LightningBranch;
                if (branch != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    Vector2 origin = new(branch.Width / 2f, 0f);
                    for (int i = 0; i < 3; i++) {
                        float y = PillarHeight * (0.18f + i * 0.28f);
                        float flip = (i + (int)branchSeed) % 2 == 0 ? 1f : -1f;
                        Color bc = AoshunHelper.LightningBlue * fade * 0.55f;
                        bc.A = 0;
                        sb.Draw(branch, groundPos - new Vector2(0, y) - Main.screenPosition, null, bc,
                            flip * (0.5f + MathF.Sin(branchSeed + i) * 0.25f), origin,
                            new Vector2(0.12f, 0.2f) * fade, flip > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
                    }
                }

                if (ACMAsset.SlashBurst != null) {
                    Color burstColor = AoshunHelper.ElectricWhite * fade * 0.6f;
                    burstColor.A = 0;
                    Main.spriteBatch.Draw(ACMAsset.SlashBurst, groundPos - Main.screenPosition, null, burstColor,
                        0f, new Vector2(ACMAsset.SlashBurst.Width / 2f, ACMAsset.SlashBurst.Height), 0.35f * fade, SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }

    #endregion

    #region 6. 雷链电网节点 — 迁移缺口环网

    /// <summary>
    /// 电网节点 — 环形排布, 相邻节点通电成网; 缺口对不通电且每 90f 顺移一位。
    /// ai[0] = 节点序号, ai[1] = 总数, ai[2] = 初始缺口序号。
    /// 各节点同帧生成 → 由自身年龄确定性推导缺口位置, 各端一致。
    /// 公平: 依次点亮(6f/个)预告 + 50f 激活延迟 + 缺口以翠玉色高亮。
    /// </summary>
    public class AoshunLightningNode : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ActivationDelay = 50;
        private const int Duration = 240;
        private const int GapShiftPeriod = 90;

        private float nodePhase;
        private float trailOffset;

        private int Age => ActivationDelay + Duration - Projectile.timeLeft;
        private bool Activated => Age >= ActivationDelay;
        private int NodeIndex => (int)Projectile.ai[0];
        private int TotalNodes => Math.Max((int)Projectile.ai[1], 2);

        /// <summary>当前缺口序号（该序号与下一节点之间不通电）</summary>
        private int CurrentGap {
            get {
                int shifts = Math.Max(Age - ActivationDelay, 0) / GapShiftPeriod;
                return ((int)Projectile.ai[2] + shifts) % TotalNodes;
            }
        }

        private bool ArcActive => Activated && NodeIndex != CurrentGap;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ActivationDelay + Duration;
        }

        public override void AI() {
            nodePhase += 0.08f;
            trailOffset += 0.04f;
            Projectile.velocity = Vector2.Zero;

            // 点亮预告: 节点 i 在 6*i 帧现身
            if (!VaultUtils.isServer && Age == NodeIndex * 6 + 1)
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f + NodeIndex * 0.05f, Volume = 0.5f }, Projectile.Center);

            if (!Activated) {
                if (!VaultUtils.isServer && Age > NodeIndex * 6 && Age % 6 == 0) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(15, 15), DustID.Electric);
                    d.noGravity = true;
                    d.scale = 1.2f;
                }
            }

            float breathe = 0.5f + MathF.Sin(nodePhase * 2f) * 0.3f;
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * breathe);
        }

        private Projectile FindNextNode() {
            int nextIndex = (NodeIndex + 1) % TotalNodes;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != Type || p.whoAmI == Projectile.whoAmI) continue;
                if ((int)p.ai[0] == nextIndex && (int)p.ai[1] == TotalNodes)
                    return p;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Activated) return false;
            if (Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 42f)
                return true;
            if (!ArcActive) return false;
            Projectile next = FindNextNode();
            if (next != null) {
                float point = 0f;
                if (Collision.CheckAABBvLineCollision(
                    targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, next.Center, 20f, ref point))
                    return true;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(nodePhase * 3f) * 0.25f;
            float appear = MathHelper.Clamp((Age - NodeIndex * 6) / 12f, 0f, 1f);
            if (appear <= 0f) return false;
            float alpha = (Activated ? 0.8f : 0.3f + MathF.Sin(nodePhase * 5f) * 0.2f) * appear;

            Projectile next = FindNextNode();

            // 通电弧线 / 缺口翠玉提示
            if (next != null && Activated) {
                if (ArcActive) {
                    DrawVertexArc(sb, gd, Projectile.Center, next.Center);
                }
                else {
                    // 缺口: 翠玉色暗淡虚线（安全通道提示）
                    Vector2 mid = (Projectile.Center + next.Center) / 2f - Main.screenPosition;
                    if (ACMAsset.SoftGlow != null) {
                        Color safeColor = TelegraphColors.Safe * 0.22f * (0.7f + 0.3f * MathF.Sin(nodePhase * 4f));
                        safeColor.A = 0;
                        sb.Draw(ACMAsset.SoftGlow, mid, null, safeColor, 0f, ACMAsset.SoftGlow.Size() / 2f, 1.4f, SpriteEffects.None, 0f);
                    }
                }
            }

            // 节点本体: BlankStar 旋转星光
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex != null) {
                Vector2 starOrigin = starTex.Size() / 2f;
                Color starColor = AoshunHelper.LightningBlue * alpha * 0.7f;
                starColor.A = 0;
                sb.Draw(starTex, drawPos, null, starColor, nodePhase * 0.8f, starOrigin, 0.25f * pulse * appear, SpriteEffects.None, 0f);
                Color starColor2 = AoshunHelper.ElectricWhite * alpha * 0.4f;
                starColor2.A = 0;
                sb.Draw(starTex, drawPos, null, starColor2, -nodePhase * 1.2f, starOrigin, 0.17f * pulse * appear, SpriteEffects.None, 0f);
            }

            // ElectricArcSheet 帧动画装饰
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null && Activated) {
                int arcIndex = (int)(nodePhase * 8f) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new(sourceRect.Width / 2f, sourceRect.Height / 2f);
                Color arcColor = AoshunHelper.LightningBlue * (0.35f * alpha);
                arcColor.A = 0;
                sb.Draw(arcSheet, drawPos, sourceRect, arcColor, nodePhase * 1.5f, arcOrigin, 0.15f * pulse, SpriteEffects.None, 0f);
            }

            // SoftGlow 核心
            if (ACMAsset.SoftGlow != null) {
                Color outerColor = AoshunHelper.ThunderPurple * 0.3f * alpha * pulse;
                outerColor.A = 0;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, outerColor, 0f, ACMAsset.SoftGlow.Size() / 2f, 1.0f * pulse, SpriteEffects.None, 0f);
                Color coreColor = AoshunHelper.ElectricWhite * 0.6f * alpha;
                coreColor.A = 0;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, coreColor, 0f, ACMAsset.SoftGlow.Size() / 2f, 0.4f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        /// <summary>锯齿电弧顶点条带（外层 LightningBranch + 内层亮芯）</summary>
        private void DrawVertexArc(SpriteBatch sb, GraphicsDevice gd, Vector2 worldStart, Vector2 worldEnd) {
            Vector2 start = worldStart - Main.screenPosition;
            Vector2 end = worldEnd - Main.screenPosition;
            Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-direction.Y, direction.X);
            float totalDist = Vector2.Distance(start, end);
            int segments = Math.Max((int)(totalDist / 18f), 4);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float flicker = MathF.Sin(nodePhase * 6f + Projectile.whoAmI) * 0.3f + 0.7f;

            for (int layer = 0; layer < 2; layer++) {
                float halfWidth = layer == 0 ? 18f : 7f;
                Texture2D tex = layer == 0
                    ? ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value
                    : VaultAsset.placeholder2.Value;

                List<ColoredVertex> vertices = new();
                for (int s = 0; s <= segments; s++) {
                    float t = (float)s / segments;
                    Vector2 basePoint = Vector2.Lerp(start, end, t);
                    float zigzag = MathF.Sin(t * MathF.PI * 5f + nodePhase * 4f) * 20f * MathF.Sin(t * MathF.PI);
                    basePoint += perp * zigzag;
                    float width = MathF.Sin(t * MathF.PI) * halfWidth * flicker;
                    Color c = layer == 0
                        ? Color.Lerp(AoshunHelper.LightningBlue * 0.8f, AoshunHelper.ElectricWhite, t) * flicker
                        : AoshunHelper.ElectricWhite * 0.9f * flicker;
                    vertices.Add(new ColoredVertex(basePoint + perp * width, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePoint - perp * width, new Vector3(t + trailOffset, 1, 1), c));
                }
                if (vertices.Count >= 3) {
                    gd.Textures[0] = tex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }

    #endregion

    #region 7. 冲击波 — 顶点拖尾 + LightShot弹体

    /// <summary>冲击波 — 破渊/压掌/怒啸的环形扩散弹</summary>
    public class AoshunShockwave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wavePhase;
        private float trailOffset;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            wavePhase += 0.1f;
            trailOffset += 0.05f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.98f;

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.15f;
            }
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(wavePhase * 3f) * 0.2f;

            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                List<ColoredVertex> vertices = new();
                int count = Projectile.oldPos.Length;
                for (int i = 0; i < count; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) break;
                    float t = (float)i / count;
                    Vector2 basePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Vector2 dir = (i < count - 1 && Projectile.oldPos[i + 1] != Vector2.Zero)
                        ? (Projectile.oldPos[i] - Projectile.oldPos[i + 1]).SafeNormalize(Vector2.UnitX)
                        : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perpDir = new(-dir.Y, dir.X);
                    float width = 16f * (1f - t) * pulse;
                    Color c = Color.Lerp(AoshunHelper.LightningBlue * 0.7f, AoshunHelper.ThunderPurple * 0.3f, t);
                    c.A = 0;
                    vertices.Add(new ColoredVertex(basePos + perpDir * width, new Vector3(t + trailOffset, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePos - perpDir * width, new Vector3(t + trailOffset, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            Texture2D shotTex = ACMAsset.LightShot;
            if (shotTex != null) {
                Vector2 shotOrigin = shotTex.Size() / 2f;
                Color shotColor = AoshunHelper.LightningBlue * 0.8f * pulse;
                shotColor.A = 0;
                sb.Draw(shotTex, drawPos, null, shotColor, Projectile.rotation, shotOrigin, 0.5f * pulse, SpriteEffects.None, 0f);
                Color coreColor = AoshunHelper.ElectricWhite * 0.6f;
                coreColor.A = 0;
                sb.Draw(shotTex, drawPos, null, coreColor, Projectile.rotation, shotOrigin, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    #endregion

    #region 8. 风暴之眼 — P3 常驻竞技场

    /// <summary>
    /// 风暴之眼 — T3 后常驻的生存竞技场: 眼内安全, 眼外风暴持续伤害 + 向心推挤。
    /// ai[0] = 头部 whoAmI。半径由年龄确定性推导(各端一致): 700 → 430 over 600f。
    /// 边界环视觉由 AoshunStormScreenSystem(ArenaRunic) 与 StormWarp 平静区共同呈现,
    /// 本弹幕只负责伤害结算(本地玩家)与壁面粒子。
    /// </summary>
    public class AoshunStormEye : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float StartRadius = Aoshun.EyeStartRadius;
        private const float HoldRadius = Aoshun.EyeHoldRadius;
        private const int ShrinkTime = 600;

        private int age;
        private float stormPhase;

        /// <summary>当前半径（头部与屏幕系统读取）</summary>
        public float CurrentRadius { get; private set; } = StartRadius;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        private bool BossAlive {
            get {
                int idx = (int)Projectile.ai[0];
                return idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active &&
                       Main.npc[idx].ModNPC is Aoshun;
            }
        }

        public override void AI() {
            age++;
            stormPhase += 0.05f;
            Projectile.velocity = Vector2.Zero;

            // Boss 存活期间常驻
            if (BossAlive && Projectile.timeLeft < 120)
                Projectile.timeLeft = 120;

            float shrink = AoshunHelper.SineInOut(MathHelper.Clamp(age / (float)ShrinkTime, 0f, 1f));
            CurrentRadius = MathHelper.Lerp(StartRadius, HoldRadius, shrink)
                          + MathF.Sin(stormPhase * 1.1f) * 10f;

            // —— 眼外结算: 只处理本地玩家（各客户端结算自己, 避免 MP 双重伤害） ——
            if (!Main.dedServ && age > 40) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead) {
                    float dist = Vector2.Distance(Projectile.Center, lp.Center);
                    if (dist > CurrentRadius) {
                        float overflow = MathHelper.Clamp((dist - CurrentRadius) / 240f, 0f, 1f);
                        if (Main.GameUpdateCount % 15 == 0) {
                            int dmg = (int)(12 + overflow * 34);
                            lp.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                                NetworkText.FromLiteral(lp.name + " 被北海风暴吞噬")), dmg, 0);
                        }
                        // 向心推挤: 温和且有上限（不做位移锁）
                        Vector2 push = (Projectile.Center - lp.Center).SafeNormalize(Vector2.Zero)
                            * (0.30f + overflow * 0.35f);
                        if (lp.velocity.Length() < 14f)
                            lp.velocity += push;
                    }
                }
            }

            // 壁面粒子: 眼缘环带云雾 + 电弧
            if (!VaultUtils.isServer) {
                int particleCount = Math.Max((int)(CurrentRadius / 60f), 4);
                for (int i = 0; i < particleCount; i++) {
                    float angle = stormPhase * 2f + MathHelper.TwoPi * i / particleCount;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * CurrentRadius + Main.rand.NextVector2Circular(18, 18);
                    var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool(3) ? DustID.Electric : DustID.Cloud);
                    d.noGravity = true;
                    d.scale = 1.8f;
                    d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fadeIn = MathHelper.Clamp(age / 60f, 0f, 1f);

            // 单层顶点风暴壁（主边界读法交给屏幕系统法阵环, 此处补体积感）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D wallTex = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
            const int WallSegments = 48;
            List<ColoredVertex> wallVerts = new();
            float ringRot = stormPhase * 1.6f;
            for (int s = 0; s <= WallSegments; s++) {
                float t = (float)s / WallSegments;
                float angle = ringRot + MathHelper.TwoPi * t;
                Vector2 outward = angle.ToRotationVector2();
                Vector2 circlePoint = drawPos + outward * CurrentRadius;
                Color c = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.LightningBlue, 0.4f) * (0.4f * fadeIn);
                wallVerts.Add(new ColoredVertex(circlePoint + outward * 26f, new Vector3(t * 4f + stormPhase, 0, 1), c));
                wallVerts.Add(new ColoredVertex(circlePoint - outward * 26f, new Vector3(t * 4f + stormPhase, 1, 1), c));
            }
            if (wallVerts.Count >= 3) {
                gd.Textures[0] = wallTex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, wallVerts.ToArray(), 0, wallVerts.Count - 2);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 壁面 Smoke 体积云
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex != null) {
                int frameW = smokeTex.Width / 4;
                int frameH = smokeTex.Height / 4;
                Vector2 smokeOrigin = new(frameW / 2f, frameH / 2f);
                for (int c = 0; c < 8; c++) {
                    float angle = stormPhase * 1.5f + MathHelper.TwoPi * c / 8;
                    Vector2 cloudPos = drawPos + angle.ToRotationVector2() * CurrentRadius;
                    int frame = ((int)(stormPhase * 4f + c * 3)) % 16;
                    Rectangle smokeRect = new(frame % 4 * frameW, frame / 4 * frameH, frameW, frameH);
                    Color cloudColor = AoshunHelper.StormGray * 0.4f * fadeIn;
                    cloudColor.A = 100;
                    sb.Draw(smokeTex, cloudPos, smokeRect, cloudColor, angle + stormPhase, smokeOrigin,
                        0.2f + MathF.Sin(stormPhase + c) * 0.03f, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * CurrentRadius;
                var d = Dust.NewDustPerfect(pos, DustID.Cloud);
                d.noGravity = true;
                d.velocity = angle.ToRotationVector2() * 8f;
                d.scale = 2.5f;
            }
        }
    }

    #endregion

    #region 9. 电痕 — 穿刺沿途驻留伤害

    /// <summary>
    /// 电痕 — 风暴穿刺沿途留下的驻留伤害区（全局 ≤24, 由生成器限流）。
    /// 公平: 生成 12f 后才有伤害(穿刺本体刚过, 给擦身逃逸窗)。
    /// </summary>
    public class AoshunElectricTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LifeTime = 150;
        private float trailPhase;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            trailPhase += 0.08f;
            Projectile.velocity = Vector2.Zero;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 15);
                var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1f + Main.rand.NextFloat(0.5f);
                d.velocity = Main.rand.NextVector2Circular(1, 1);
            }

            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.3f * fade);
        }

        public override bool? CanDamage() => LifeTime - Projectile.timeLeft > 12 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = Math.Min(Projectile.timeLeft / 30f, 1f);
            float pulse = 1f + MathF.Sin(trailPhase * 4f) * 0.2f;

            if (ACMAsset.SoftGlow != null) {
                Color baseColor = AoshunHelper.ThunderPurple * 0.25f * fade;
                baseColor.A = 0;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, baseColor, 0f, ACMAsset.SoftGlow.Size() / 2f, 0.8f * pulse, SpriteEffects.None, 0f);
            }

            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcIdx = (int)(trailPhase * 6f) % 4;
                int arcH = arcSheet.Height / 4;
                Rectangle arcRect = new(0, arcIdx * arcH, arcSheet.Width, arcH);
                Vector2 arcOrigin = new(arcRect.Width / 2f, arcRect.Height / 2f);

                Color arcColor = AoshunHelper.LightningBlue * 0.5f * fade * pulse;
                arcColor.A = 0;
                sb.Draw(arcSheet, drawPos, arcRect, arcColor, trailPhase * 1.2f, arcOrigin, 0.1f * pulse, SpriteEffects.None, 0f);
                Color arcColor2 = AoshunHelper.ThunderPurple * 0.3f * fade;
                arcColor2.A = 0;
                sb.Draw(arcSheet, drawPos, arcRect, arcColor2, -trailPhase * 0.8f + MathHelper.PiOver4, arcOrigin, 0.08f, SpriteEffects.FlipHorizontally, 0f);
            }

            if (ACMAsset.Sparkle != null && MathF.Sin(trailPhase * 5f) > 0.7f) {
                Color sparkColor = AoshunHelper.ElectricWhite * 0.35f * fade;
                sparkColor.A = 0;
                sb.Draw(ACMAsset.Sparkle, drawPos, null, sparkColor, trailPhase * 3f, ACMAsset.Sparkle.Size() / 2f, 0.15f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion

    #region 10. 地裂预警 — 破渊突袭落点标记（无伤害）

    /// <summary>
    /// 地裂预警 — 破渊突袭的落点标记, 纯预警无伤害。
    /// ai[0] = 预警帧数。地表裂纹扩张 + 电弧自地面喷冒 + 青白→红渐变。
    /// </summary>
    public class AoshunBreachCrack : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float warnTotal;
        private float crackPhase;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            crackPhase += 0.12f;
            Projectile.velocity = Vector2.Zero;
            if (warnTotal <= 0f) {
                warnTotal = Math.Max(Projectile.ai[0], 1f);
                Projectile.timeLeft = (int)warnTotal;
            }

            float urgency = 1f - Projectile.timeLeft / warnTotal;

            if (!VaultUtils.isServer) {
                // 地面电弧喷冒, 频率随紧迫度上升
                if (Main.rand.NextFloat() < 0.25f + urgency * 0.55f) {
                    float spread = 30f + urgency * 70f;
                    Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-spread, spread), 2);
                    var d = Dust.NewDustPerfect(dustPos, Main.rand.NextBool(3) ? DustID.Smoke : DustID.Electric);
                    d.noGravity = d.type == DustID.Electric;
                    d.scale = 1.4f + urgency;
                    d.velocity = new Vector2(Main.rand.NextFloat(-1, 1), -Main.rand.NextFloat(2f, 5f + urgency * 4f));
                }
                if (urgency > 0.6f)
                    ACMUtils.AddScreenShake(urgency * 1.5f);
            }

            Lighting.AddLight(Projectile.Center, TelegraphColors.Lethal.ToVector3() * 0.4f * urgency);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float urgency = 1f - Projectile.timeLeft / Math.Max(warnTotal, 1f);
            Color teleColor = Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Lethal, ACMUtils.QuadIn(urgency));

            // 地面横置裂纹: LightningBranch 压扁横放, 左右对称扩张
            Texture2D branch = ACMAsset.LightningBranch;
            if (branch != null) {
                Vector2 origin = new(branch.Width / 2f, 0f);
                float len = 0.10f + urgency * 0.12f;
                Color crackColor = teleColor * (0.35f + urgency * 0.45f);
                crackColor.A = 0;
                float flick = 0.8f + MathF.Sin(crackPhase * 6f) * 0.2f * urgency;
                sb.Draw(branch, drawPos, null, crackColor * flick, MathHelper.PiOver2, origin,
                    new Vector2(0.10f, len), SpriteEffects.None, 0f);
                sb.Draw(branch, drawPos, null, crackColor * flick * 0.8f, -MathHelper.PiOver2, origin,
                    new Vector2(0.10f, len), SpriteEffects.FlipHorizontally, 0f);
            }

            // 中心警示光点
            if (ACMAsset.SoftGlow != null) {
                Color markColor = teleColor * (0.25f + urgency * 0.4f);
                markColor.A = 0;
                float pulse = 1f + MathF.Sin(crackPhase * (4f + urgency * 5f)) * 0.3f;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, markColor, 0f, ACMAsset.SoftGlow.Size() / 2f,
                    (0.55f + urgency * 0.5f) * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-40, 40), 0), DustID.Smoke);
                d.velocity = new Vector2(Main.rand.NextFloat(-2, 2), -Main.rand.NextFloat(3f, 8f));
                d.scale = 2f;
            }
        }
    }

    #endregion
}

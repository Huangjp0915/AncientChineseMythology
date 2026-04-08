using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武冰锥 — 支持4种AI行为模式的冰晶弹幕
    /// ai[0] = 行为模式: 0=直射, 1=冰裂分叉, 2=玄冰锚, 3=弧光追猎
    /// ai[1] = 模式参数: Mode1=分裂延迟帧, Mode2=锚定持续帧, Mode3=目标玩家
    /// 渲染: 顶点ribbon拖尾 + 冰晶弹头 + Mode2冰域着色器
    /// </summary>
    public class XuanwuIceShard : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float crystalSpin;
        private int lifetime;
        private float baseSpeed;

        //Mode 2: 玄冰锚
        private bool anchored;
        private float anchorProgress;
        private float anchorRadius;

        //Mode 3: 弧光追猎
        private float arcTimer;
        private int arcSide = 1;
        private Vector2 arcTarget;

        //视觉
        private float crackFlash;
        private float pulsePhase;
        private static Asset<Effect> trailShaderRef;
        private static Asset<Effect> iceFieldShaderRef;

        private int Mode {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private float ModeParam => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            lifetime++;
            crystalSpin -= 0.06f;
            pulsePhase += 0.12f;

            switch (Mode) {
                case 0: AI_Straight(); break;
                case 1: AI_Fracture(); break;
                case 2: AI_FrostAnchor(); break;
                case 3: AI_ArcPursuit(); break;
                default: AI_Straight(); break;
            }

            if (!anchored)
                Projectile.rotation = Projectile.velocity.ToRotation();

            //霜迹粒子
            int dustRate = anchored ? 1 : 3;
            if (Main.rand.NextBool(dustRate)) {
                Vector2 offset = anchored
                    ? Main.rand.NextVector2Circular(anchorRadius * 0.5f, anchorRadius * 0.5f)
                    : Main.rand.NextVector2Circular(8, 8);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset,
                    0, 0, DustID.IceTorch,
                    anchored ? Main.rand.NextFloat(-1, 1) : -Projectile.velocity.X * 0.15f,
                    anchored ? Main.rand.NextFloat(-2, 0) : -Projectile.velocity.Y * 0.15f,
                    120, default, anchored ? 1.5f : 0.9f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            crackFlash *= 0.85f;
            Lighting.AddLight(Projectile.Center, 0.1f + crackFlash * 0.3f, 0.2f + crackFlash * 0.2f, 0.4f + crackFlash * 0.1f);
        }

        //Mode 0: 直线飞行
        private void AI_Straight() { }

        //Mode 1: 冰裂分叉 — 飞行delay帧后原地裂成3条
        private void AI_Fracture() {
            int splitDelay = (int)MathF.Max(ModeParam, 25f);
            if (lifetime == 1) baseSpeed = Projectile.velocity.Length();

            //分裂前: 冰晶振颤+裂纹闪烁
            if (lifetime > splitDelay - 12 && lifetime < splitDelay) {
                float urgency = 1f - (splitDelay - lifetime) / 12f;
                crystalSpin -= 0.1f * urgency;
                Projectile.Center += Main.rand.NextVector2Circular(urgency * 2f, urgency * 2f);
                crackFlash = urgency * 0.5f;
            }

            if (lifetime == splitDelay && Main.netMode != NetmodeID.MultiplayerClient) {
                //分裂: 产生2颗偏转子弹
                float baseAngle = Projectile.velocity.ToRotation();
                float splitAngle = MathHelper.ToRadians(32f);
                float childSpeed = baseSpeed * 0.85f;
                for (int side = -1; side <= 1; side += 2) {
                    float a = baseAngle + splitAngle * side;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * childSpeed;
                    int proj = Projectile.NewProjectile(
                        new EntitySource_Parent(Projectile),
                        Projectile.Center, vel,
                        Type, (int)(Projectile.damage * 0.7f), 0f, Main.myPlayer, 0, 0f);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 80;
                }
                //自身切为直射继续飞行
                Mode = 0;
                crackFlash = 1f;
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.6f, Volume = 0.5f }, Projectile.Center);
                //分裂粒子爆发
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Ice,
                            Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 80, default, 1.3f);
                        d.noGravity = true;
                    }
                }
            }
        }

        //Mode 2: 玄冰锚 — 减速→锚定→冰域→引爆
        private void AI_FrostAnchor() {
            int anchorDuration = (int)MathF.Max(ModeParam, 60f);
            if (lifetime == 1) baseSpeed = Projectile.velocity.Length();

            int decelFrames = 25;
            if (!anchored) {
                if (lifetime <= decelFrames) {
                    float decelT = (float)lifetime / decelFrames;
                    Projectile.velocity *= 1f - decelT * 0.035f;
                    crystalSpin -= 0.04f * decelT;
                }
                if (lifetime >= decelFrames || Projectile.velocity.LengthSquared() < 1f) {
                    anchored = true;
                    Projectile.velocity = Vector2.Zero;
                    anchorProgress = 0f;
                    anchorRadius = 0f;
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.3f, Volume = 0.4f }, Projectile.Center);
                }
            }
            else {
                int anchorLife = lifetime - decelFrames;
                float maxRadius = 100f;
                //展开
                float expandT = ACMUtils.Clamp01(anchorLife / 30f);
                anchorProgress = ACMUtils.QuadOut(expandT);
                anchorRadius = maxRadius * anchorProgress;

                crystalSpin -= 0.12f;
                Projectile.Center += Main.rand.NextVector2Circular(0.5f, 0.5f);

                //域内冰晶粒子沿边缘旋转
                if (Main.netMode != NetmodeID.Server && anchorLife % 2 == 0) {
                    float dustAngle = anchorLife * 0.15f;
                    for (int i = 0; i < 3; i++) {
                        float da = dustAngle + MathHelper.TwoPi / 3 * i;
                        Vector2 dpos = Projectile.Center + new Vector2(MathF.Cos(da), MathF.Sin(da)) * anchorRadius * 0.9f;
                        Dust d = Dust.NewDustDirect(dpos, 0, 0, DustID.IceTorch, 0, 0, 80, default, 1.0f);
                        d.noGravity = true;
                        d.velocity = new Vector2(-MathF.Sin(da), MathF.Cos(da)) * 2f;
                    }
                }

                //引爆
                if (anchorLife >= anchorDuration) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int count = 8;
                        for (int i = 0; i < count; i++) {
                            float a = MathHelper.TwoPi / count * i;
                            Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 10f;
                            int proj = Projectile.NewProjectile(
                                new EntitySource_Parent(Projectile),
                                Projectile.Center, vel,
                                Type, (int)(Projectile.damage * 0.6f), 0f, Main.myPlayer, 0, 0f);
                            if (proj >= 0 && proj < Main.maxProjectiles)
                                Main.projectile[proj].timeLeft = 80;
                        }
                    }
                    crackFlash = 1f;
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 20; i++) {
                            float a = Main.rand.NextFloat(MathHelper.TwoPi);
                            float spd = Main.rand.NextFloat(3, 8);
                            Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Ice,
                                MathF.Cos(a) * spd, MathF.Sin(a) * spd, 60, default, 2f);
                            d.noGravity = true;
                        }
                    }
                    Projectile.Kill();
                }
            }
        }

        //Mode 2: 锚定冰域扩大碰撞范围
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (anchored && anchorRadius > 10f) {
                float dx = targetHitbox.Center.X - Projectile.Center.X;
                float dy = targetHitbox.Center.Y - Projectile.Center.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float targetR = MathF.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
                return dist < anchorRadius + targetR;
            }
            return null;
        }

        //Mode 3: 弧光追猎 — 弧线式转向追踪
        private void AI_ArcPursuit() {
            int targetIdx = (int)ModeParam;
            Player target = targetIdx >= 0 && targetIdx < Main.maxPlayers
                ? Main.player[targetIdx] : null;
            if (target == null || !target.active)
                target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];

            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                arcTimer = 0;
                arcSide = Main.rand.NextBool() ? 1 : -1;
                arcTarget = target.Center + target.velocity * 15f;
            }

            arcTimer++;
            int arcDuration = 25;

            //平滑转向: 向弧线目标方向偏转，再叠加侧向弧度
            Vector2 toTarget = (arcTarget - Projectile.Center).SafeNormalize(Vector2.UnitX);
            float desiredAngle = toTarget.ToRotation();
            //侧向偏转: 制造弧线效果
            float arcBias = MathHelper.ToRadians(18f) * arcSide;
            float blendT = ACMUtils.Clamp01(arcTimer / (float)arcDuration);
            //弧线前半段偏转大，后半段修正回来
            float currentBias = arcBias * MathF.Sin(blendT * MathHelper.Pi);
            desiredAngle += currentBias;

            float currentAngle = Projectile.velocity.ToRotation();
            float maxTurn = MathHelper.ToRadians(4.5f);
            float diff = MathHelper.WrapAngle(desiredAngle - currentAngle);
            float turn = MathHelper.Clamp(diff, -maxTurn, maxTurn);

            //速度渐增
            float speed = MathHelper.Lerp(baseSpeed, baseSpeed * 1.8f, ACMUtils.Clamp01(lifetime / 100f));
            float newAngle = currentAngle + turn;
            Projectile.velocity = new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle)) * speed;

            //弧段结束: 重新计算目标
            if (arcTimer >= arcDuration) {
                arcTimer = 0;
                arcSide *= -1;
                arcTarget = target.Center + target.velocity * 15f;
                crackFlash = 0.4f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.IceTorch,
                            Main.rand.NextFloat(-2, 2), Main.rand.NextFloat(-2, 2), 100, default, 1.2f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            //Mode 2冰域: 先绘制冰域着色器效果
            if (anchored && anchorProgress > 0.01f)
                DrawIceField(sb, gd, drawPos);

            //收集拖尾位置
            var positions = new System.Collections.Generic.List<Vector2>();
            positions.Add(drawPos);
            for (int i = 0; i < trailLen; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                positions.Add(Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition);
            }

            //ribbon拖尾(非锚定状态)
            if (!anchored && positions.Count >= 3)
                DrawRibbonTrail(sb, gd, positions);

            //弹头sprite
            DrawCrystalHead(sb, drawPos);

            return false;
        }

        private void DrawIceField(SpriteBatch sb, GraphicsDevice gd, Vector2 drawPos) {
            iceFieldShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuIceField", AssetRequestMode.ImmediateLoad);
            Effect shader = iceFieldShaderRef?.Value;
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex == null) return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
                shader.Parameters["uProgress"]?.SetValue(anchorProgress);
                shader.Parameters["uIntensity"]?.SetValue(0.8f);
                shader.Parameters["uPulse"]?.SetValue(pulsePhase);
                shader.CurrentTechnique.Passes[0].Apply();
            }

            float drawScale = anchorRadius * 2f / glowTex.Width * 1.2f;
            Vector2 origin = glowTex.Size() / 2f;
            sb.Draw(glowTex, drawPos, null, Color.White, 0f, origin, drawScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawRibbonTrail(SpriteBatch sb, GraphicsDevice gd,
            System.Collections.Generic.List<Vector2> positions) {
            trailShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuTrailRibbon", AssetRequestMode.ImmediateLoad);
            Effect shader = trailShaderRef?.Value;
            bool isArcMode = Mode == 3;
            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;

            var posArr = positions.ToArray();
            var verts = ACMUtils.BuildRibbonStrip(
                posArr,
                p => {
                    float baseW = MathHelper.Lerp(isArcMode ? 16f : 14f, 2f, p);
                    float pulse = isArcMode ? (1f + MathF.Sin(p * 12f + time * 6f) * 0.15f) : 1f;
                    return baseW * pulse;
                },
                p => {
                    float alpha = (1f - p) * 0.75f;
                    Color c;
                    if (isArcMode) {
                        //弧光追猎: 冰白到深蓝的棱镜过渡
                        c = Color.Lerp(new Color(220, 240, 255), new Color(40, 100, 220), p) * alpha;
                    }
                    else {
                        c = Color.Lerp(new Color(180, 230, 255), new Color(60, 120, 200), p) * alpha;
                    }
                    c.A = 0;
                    return c;
                },
                uvScroll: time * (isArcMode ? 1.5f : 0.8f),
                subdivisions: 3
            );

            if (verts.Length >= 4) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                if (shader != null) {
                    shader.Parameters["uTime"]?.SetValue(time);
                    shader.Parameters["uGlowWidth"]?.SetValue(isArcMode ? 0.3f : 0.25f);
                    shader.Parameters["uAlphaFade"]?.SetValue(0.85f);
                    shader.Parameters["uScrollSpeed"]?.SetValue(isArcMode ? 2f : 1.2f);
                    shader.Parameters["uPulseRate"]?.SetValue(isArcMode ? 12f : 8f);
                    shader.Parameters["uPulseStrength"]?.SetValue(isArcMode ? 0.25f : 0.15f);
                    shader.Parameters["uGlowColor"]?.SetValue(new Vector4(0.5f, 0.8f, 1f, 0.6f));
                    shader.Parameters["uCoreColor"]?.SetValue(new Vector4(0.8f, 0.95f, 1f, 0.3f));
                    shader.CurrentTechnique.Passes[0].Apply();
                }

                gd.Textures[0] = ACMAsset.LightShot;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

                //内层ribbon
                var innerVerts = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => MathHelper.Lerp(6f, 0.5f, p),
                    p => {
                        float alpha = (1f - p) * 0.9f;
                        Color c = new Color(220, 245, 255) * alpha;
                        c.A = 0;
                        return c;
                    },
                    uvScroll: time * 1.5f,
                    subdivisions: 2
                );
                if (innerVerts.Length >= 4) {
                    gd.Textures[0] = ACMAsset.SoftGlow;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts, 0, innerVerts.Length - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        private void DrawCrystalHead(SpriteBatch sb, Vector2 drawPos) {
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex == null) return;
            Vector2 starOrigin = starTex.Size() / 2f;

            float flash = crackFlash;
            float scaleBonus = 0f;
            if (anchored) {
                scaleBonus = 0.2f + MathF.Sin(pulsePhase * 2f) * 0.1f;
            }
            else if (Mode == 1) {
                int splitDelay = (int)MathF.Max(ModeParam, 25f);
                if (lifetime > splitDelay - 15 && lifetime < splitDelay)
                    scaleBonus = 0.1f * MathF.Sin(lifetime * 0.5f);
            }

            Color starColor = new Color(100, 180, 255, 0) * (0.5f + flash * 0.3f);
            sb.Draw(starTex, drawPos, null, starColor, crystalSpin,
                starOrigin, 0.35f + scaleBonus, SpriteEffects.None, 0f);

            Color star2Color = new Color(160, 220, 255, 0) * (0.35f + flash * 0.2f);
            sb.Draw(starTex, drawPos, null, star2Color, -crystalSpin * 0.7f,
                starOrigin, 0.28f + scaleBonus, SpriteEffects.None, 0f);

            if (!anchored) {
                Texture2D shotTex = ACMAsset.LightShot;
                if (shotTex != null) {
                    Vector2 shotOrigin = shotTex.Size() / 2f;
                    Color shotColor = new Color(140, 210, 255, 0) * (0.55f + flash * 0.2f);
                    sb.Draw(shotTex, drawPos, null, shotColor, Projectile.rotation,
                        shotOrigin, 0.5f, SpriteEffects.None, 0f);
                }
            }

            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                float coreScale = anchored ? 0.8f + scaleBonus : 0.5f + scaleBonus * 0.5f;
                Color coreColor = new Color(200, 240, 255, 0) * (0.6f + flash * 0.4f);
                sb.Draw(glowTex, drawPos, null, coreColor, 0f,
                    glowOrigin, coreScale, SpriteEffects.None, 0f);
            }
        }

        private bool isOrbitingMode3() => false;

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.IceTorch, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4),
                    80, default, 1.2f);
                d.noGravity = true;
            }
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武蛇毒牙 — 支持4种AI行为模式的毒牙弹幕
    /// ai[0] = 行为模式: 0=直射, 1=闪蛇, 2=毒域, 3=连锁咬
    /// ai[1] = 模式参数: Mode1=闪避间隔帧, Mode2=毒域持续帧, Mode3=弹跳次数
    /// 渲染: VenomAura着色器有机拖尾 + 毒牙弹头sprite
    /// </summary>
    public class XuanwuVenomFang : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float venomPulse;
        private int lifetime;
        private float baseSpeed;

        //Mode 1: 闪蛇
        private int nextDashFrame;
        private int flickerSide = 1;

        //Mode 2: 毒域
        private bool poolActive;
        private float poolProgress;
        private float poolRadius;

        //Mode 3: 连锁咬
        private int bouncePhase;
        private int bounceTimer;
        private int bouncesLeft;
        private Vector2 lungeTarget;

        //视觉
        private float burstFlash;
        private static Asset<Effect> venomShaderRef;

        private int Mode {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private float ModeParam => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            lifetime++;
            venomPulse += 0.15f;

            switch (Mode) {
                case 0: AI_Straight(); break;
                case 1: AI_Flicker(); break;
                case 2: AI_VenomPool(); break;
                case 3: AI_ChainBite(); break;
                default: AI_Straight(); break;
            }

            if (!poolActive)
                Projectile.rotation = Projectile.velocity.ToRotation();

            //毒雾粒子
            int dustRate = poolActive ? 1 : 2;
            if (Main.rand.NextBool(dustRate)) {
                Vector2 offset = poolActive
                    ? Main.rand.NextVector2Circular(poolRadius * 0.4f, poolRadius * 0.4f)
                    : Main.rand.NextVector2Circular(6, 6) - Projectile.velocity * 0.3f;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset,
                    0, 0, DustID.CursedTorch,
                    poolActive ? Main.rand.NextFloat(-1, 1) : -Projectile.velocity.X * 0.1f,
                    poolActive ? Main.rand.NextFloat(-1.5f, 0.5f) : -Projectile.velocity.Y * 0.1f,
                    100, default, poolActive ? 1.6f : 1f);
                d.noGravity = true;
                d.fadeIn = 1.4f;
            }
            if (!poolActive && Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Venom,
                    Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(0, 2), 80, default, 0.6f);
            }

            burstFlash *= 0.85f;
            Lighting.AddLight(Projectile.Center, 0.1f + burstFlash * 0.2f, 0.3f + burstFlash * 0.2f, 0.05f);
        }

        //Mode 0: 直线飞行
        private void AI_Straight() { }

        //Mode 1: 闪蛇 — 飞行中随机瞬移闪避
        private void AI_Flicker() {
            int dashInterval = (int)MathF.Max(ModeParam, 15f);
            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                nextDashFrame = dashInterval + Main.rand.Next(-3, 4);
            }

            //轻微追踪
            Player nearest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            float targetAngle = (nearest.Center - Projectile.Center).ToRotation();
            float currentAngle = Projectile.velocity.ToRotation();
            float turnRate = 0.025f;
            float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
            Projectile.velocity = Projectile.velocity.RotatedBy(MathHelper.Clamp(angleDiff, -turnRate, turnRate));
            //保持速度
            float speed = Projectile.velocity.Length();
            if (speed < baseSpeed * 0.8f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * baseSpeed;

            //闪避触发
            if (lifetime == nextDashFrame) {
                float perpAngle = currentAngle + MathHelper.PiOver2 * flickerSide;
                float dashDist = Main.rand.NextFloat(70f, 120f);
                Vector2 dashOffset = new Vector2(MathF.Cos(perpAngle), MathF.Sin(perpAngle)) * dashDist;
                Projectile.Center += dashOffset;
                flickerSide *= -1;
                burstFlash = 0.6f;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.8f, Volume = 0.3f }, Projectile.Center);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center - dashOffset, 0, 0, DustID.CursedTorch,
                            dashOffset.X * 0.02f, dashOffset.Y * 0.02f, 80, default, 1.3f);
                        d.noGravity = true;
                    }
                }
                nextDashFrame = lifetime + dashInterval + Main.rand.Next(-4, 5);
                Projectile.netUpdate = true;
            }
        }

        //Mode 2: 毒域 — 抛物线落地后生成持续毒区
        private void AI_VenomPool() {
            int poolDuration = (int)MathF.Max(ModeParam, 120f);
            if (lifetime == 1) baseSpeed = Projectile.velocity.Length();

            if (!poolActive) {
                Projectile.velocity.Y += 0.3f;
                if (Projectile.velocity.Y > 18f) Projectile.velocity.Y = 18f;
                Projectile.velocity.X += MathF.Sin(lifetime * 0.1f) * 0.03f;

                bool landed = false;
                if (lifetime > 8) {
                    int tileX = (int)(Projectile.Center.X / 16f);
                    int tileY = (int)((Projectile.Center.Y + 10) / 16f);
                    if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY)
                        if (WorldGen.SolidTile(tileX, tileY)) landed = true;
                    if (lifetime > 60) landed = true;
                }

                if (landed) {
                    poolActive = true;
                    Projectile.velocity = Vector2.Zero;
                    poolProgress = 0f;
                    poolRadius = 0f;
                    Projectile.timeLeft = poolDuration + 30;
                    Projectile.penetrate = -1;
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f, Volume = 0.5f }, Projectile.Center);
                    burstFlash = 0.5f;
                }
            }
            else {
                int poolLife = Math.Max(lifetime - 60, 0);
                float maxR = 80f;
                float expandT = ACMUtils.Clamp01(poolLife / 25f);
                poolProgress = ACMUtils.QuadOut(expandT);
                poolRadius = maxR * poolProgress;

                if (poolLife > poolDuration) {
                    float fadeT = ACMUtils.Clamp01((poolLife - poolDuration) / 20f);
                    poolProgress = 1f - fadeT;
                    poolRadius = maxR * poolProgress;
                    if (fadeT >= 1f) Projectile.Kill();
                }

                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    float bx = Projectile.Center.X + Main.rand.NextFloat(-poolRadius, poolRadius);
                    Dust d = Dust.NewDustDirect(new Vector2(bx, Projectile.Center.Y),
                        0, 0, DustID.CursedTorch, 0, -Main.rand.NextFloat(1, 3), 80, default, 1.2f);
                    d.noGravity = true;
                }
            }
        }

        //Mode 2: 毒域圆形碰撞
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (poolActive && poolRadius > 10f) {
                float dx = targetHitbox.Center.X - Projectile.Center.X;
                float dy = targetHitbox.Center.Y - Projectile.Center.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float targetR = MathF.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
                return dist < poolRadius + targetR;
            }
            return null;
        }

        //毒域减少伤害频率
        public override bool CanHitPlayer(Player target) {
            if (poolActive) return lifetime % 15 == 0;
            return true;
        }

        //Mode 3: 连锁咬 — 蓄力→冲刺→弹跳→重复
        private void AI_ChainBite() {
            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                bouncesLeft = (int)MathF.Max(ModeParam, 2f);
                bouncePhase = 0;
                bounceTimer = 0;
                Projectile.velocity *= 0.1f;
                Projectile.timeLeft = 300;
            }
            bounceTimer++;
            switch (bouncePhase) {
                case 0: ChainBite_WindUp(); break;
                case 1: ChainBite_Lunge(); break;
                case 2: ChainBite_Reposition(); break;
            }
        }

        private void ChainBite_WindUp() {
            Projectile.velocity *= 0.9f;
            float windUpDur = 18f;
            float t = ACMUtils.Clamp01(bounceTimer / windUpDur);
            if (bounceTimer > 5)
                Projectile.Center += Main.rand.NextVector2Circular(t * 2f, t * 2f);

            if (Main.netMode != NetmodeID.Server && bounceTimer % 2 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 40f * (1f - t);
                Vector2 dpos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                Dust d = Dust.NewDustDirect(dpos, 0, 0, DustID.CursedTorch, 0, 0, 80, default, 1.5f);
                d.noGravity = true;
                d.velocity = (Projectile.Center - dpos).SafeNormalize(Vector2.Zero) * 4f;
            }

            if (bounceTimer >= (int)windUpDur) {
                Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                lungeTarget = target.Center + target.velocity * 8f;
                float lungeSpeed = baseSpeed * 2.5f;
                Projectile.velocity = (lungeTarget - Projectile.Center).SafeNormalize(Vector2.UnitX) * lungeSpeed;
                bouncePhase = 1;
                bounceTimer = 0;
                burstFlash = 0.5f;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
                Projectile.netUpdate = true;
            }
        }

        private void ChainBite_Lunge() {
            float distToTarget = Vector2.Distance(Projectile.Center, lungeTarget);
            if (distToTarget < 40f || bounceTimer > 15) {
                bouncesLeft--;
                if (bouncesLeft <= 0) {
                    Mode = 0;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 4; i++) {
                            float a = MathHelper.TwoPi / 4 * i + MathHelper.PiOver4;
                            Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 8f;
                            int proj = Projectile.NewProjectile(
                                new EntitySource_Parent(Projectile),
                                Projectile.Center, vel,
                                Type, (int)(Projectile.damage * 0.5f), 0f, Main.myPlayer, 0, 0f);
                            if (proj >= 0 && proj < Main.maxProjectiles)
                                Main.projectile[proj].timeLeft = 60;
                        }
                    }
                    burstFlash = 0.8f;
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.2f, Volume = 0.5f }, Projectile.Center);
                }
                else {
                    Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                    float bAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float bDist = Main.rand.NextFloat(180f, 280f);
                    Projectile.Center = target.Center + new Vector2(MathF.Cos(bAngle), MathF.Sin(bAngle)) * bDist;
                    Projectile.velocity = Vector2.Zero;
                    bouncePhase = 2;
                    bounceTimer = 0;
                    burstFlash = 0.4f;
                    Projectile.netUpdate = true;
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 5; i++) {
                            Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.CursedTorch,
                                Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3), 80, default, 1.5f);
                            d.noGravity = true;
                        }
                    }
                }
            }
        }

        private void ChainBite_Reposition() {
            Projectile.velocity *= 0.8f;
            if (bounceTimer >= 8) {
                bouncePhase = 0;
                bounceTimer = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            //毒域绘制
            if (poolActive && poolProgress > 0.01f)
                DrawVenomPool(sb, drawPos);

            //拖尾
            var positions = new System.Collections.Generic.List<Vector2>();
            positions.Add(drawPos);
            for (int i = 0; i < trailLen; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                positions.Add(Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition);
            }
            if (!poolActive && positions.Count >= 3)
                DrawVenomTrail(sb, gd, positions);

            //弹头
            DrawFangHead(sb, drawPos);

            return false;
        }

        private void DrawVenomPool(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex == null) return;
            Vector2 origin = glowTex.Size() / 2f;
            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;
            float drawScale = poolRadius * 2f / glowTex.Width * 1.3f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            //底层暗绿
            Color baseCol = new Color(30, 120, 20, 0) * (poolProgress * 0.4f);
            sb.Draw(glowTex, drawPos, null, baseCol, 0f, origin, drawScale * 1.2f, SpriteEffects.None, 0f);

            //中层脉动
            float pulse = MathF.Sin(time * 3f) * 0.15f + 0.85f;
            Color midCol = new Color(60, 200, 40, 0) * (poolProgress * 0.3f * pulse);
            sb.Draw(glowTex, drawPos, null, midCol, time * 0.5f, origin, drawScale, SpriteEffects.None, 0f);

            //内层亮核
            Color innerCol = new Color(140, 255, 80, 0) * (poolProgress * 0.25f);
            sb.Draw(glowTex, drawPos, null, innerCol, -time * 0.7f, origin, drawScale * 0.6f, SpriteEffects.None, 0f);

            //旋转气泡光点
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex != null) {
                Vector2 so = starTex.Size() / 2f;
                for (int i = 0; i < 5; i++) {
                    float angle = time * (1f + i * 0.3f) + i * MathHelper.TwoPi / 5f;
                    float r = poolRadius * (0.3f + i * 0.12f);
                    Vector2 bpos = drawPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
                    float bpulse = MathF.Sin(time * 4f + i * 2f) * 0.3f + 0.7f;
                    Color bc = new Color(80, 220, 50, 0) * (bpulse * poolProgress * 0.3f);
                    sb.Draw(starTex, bpos, null, bc, -angle, so, 0.08f, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawVenomTrail(SpriteBatch sb, GraphicsDevice gd,
            System.Collections.Generic.List<Vector2> positions) {
            venomShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuVenomAura", AssetRequestMode.ImmediateLoad);
            Effect shader = venomShaderRef?.Value;
            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;
            bool isChainBite = Mode == 3;
            float pulseSin = MathF.Sin(venomPulse);

            var posArr = positions.ToArray();
            var verts = ACMUtils.BuildRibbonStrip(
                posArr,
                p => {
                    float baseW = MathHelper.Lerp(isChainBite ? 14f : 10f, 4f, p);
                    return baseW * (1f + pulseSin * 0.12f);
                },
                p => {
                    float alpha = (1f - p) * 0.6f;
                    Color c = isChainBite
                        ? Color.Lerp(new Color(100, 255, 50), new Color(80, 30, 120), p) * alpha
                        : Color.Lerp(new Color(80, 220, 50), new Color(30, 100, 20), p) * alpha;
                    c.A = 0;
                    return c;
                },
                uvScroll: time * 0.6f,
                subdivisions: 3
            );

            if (verts.Length >= 4) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                if (shader != null) {
                    shader.Parameters["uTime"]?.SetValue(time);
                    shader.Parameters["uDissolveEdge"]?.SetValue(0.12f);
                    shader.Parameters["uAlphaFade"]?.SetValue(0.8f);
                    shader.Parameters["uFlowSpeed"]?.SetValue(isChainBite ? 1.0f : 0.6f);
                    shader.Parameters["uHueShift"]?.SetValue(isChainBite ? 2.5f : 1.5f);
                    shader.Parameters["uBaseColor"]?.SetValue(isChainBite
                        ? new Vector4(0.4f, 1f, 0.2f, 0.7f)
                        : new Vector4(0.3f, 0.9f, 0.2f, 0.6f));
                    shader.Parameters["uCoreColor"]?.SetValue(new Vector4(0.7f, 1f, 0.4f, 0.35f));
                    shader.Parameters["uDripStrength"]?.SetValue(0.3f);
                    shader.CurrentTechnique.Passes[0].Apply();
                }

                Texture2D trailTex = ACMAsset.SoftGlow;
                if (trailTex != null) gd.Textures[0] = trailTex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

                //内层毒芯
                var innerVerts = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => MathHelper.Lerp(4f, 0.5f, p),
                    p => {
                        float alpha = (1f - p) * 0.85f;
                        Color c = new Color(180, 255, 100) * alpha;
                        c.A = 0;
                        return c;
                    },
                    uvScroll: time * 1.2f,
                    subdivisions: 2
                );
                if (innerVerts.Length >= 4) {
                    gd.Textures[0] = ACMAsset.LightShot;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts, 0, innerVerts.Length - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        private void DrawFangHead(SpriteBatch sb, Vector2 drawPos) {
            float flash = burstFlash;
            float pulseSin = MathF.Sin(venomPulse);
            float scaleBonus = 0f;
            if (Mode == 3 && bouncePhase == 0) {
                float windUpT = ACMUtils.Clamp01(bounceTimer / 18f);
                scaleBonus = MathF.Sin(windUpT * MathHelper.Pi) * 0.2f;
            }

            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color outerGlow = new Color(60, 200, 40, 0) * (0.3f + pulseSin * 0.1f + flash * 0.3f);
                float outerScale = poolActive ? 0.5f : (1.0f + pulseSin * 0.15f + scaleBonus);
                sb.Draw(glowTex, drawPos, null, outerGlow, 0f, glowOrigin, outerScale, SpriteEffects.None, 0f);
            }
            if (!poolActive) {
                Texture2D shotTex = ACMAsset.LightShot;
                if (shotTex != null) {
                    Vector2 shotOrigin = shotTex.Size() / 2f;
                    Color fangColor = new Color(100, 255, 70, 0) * (0.7f + flash * 0.2f);
                    sb.Draw(shotTex, drawPos, null, fangColor, Projectile.rotation,
                        shotOrigin, 0.45f + scaleBonus * 0.3f, SpriteEffects.None, 0f);
                }
            }
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color coreColor = new Color(180, 255, 120, 0) * (0.5f + flash * 0.3f);
                float coreScale = poolActive ? 0.3f : (0.35f + scaleBonus * 0.5f);
                sb.Draw(glowTex, drawPos, null, coreColor, 0f, glowOrigin, coreScale, SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            int dustCount = poolActive ? 20 : 8;
            for (int i = 0; i < dustCount; i++) {
                int dustType = Main.rand.NextBool() ? DustID.CursedTorch : DustID.Venom;
                float spread = poolActive ? 6f : 3f;
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    dustType, Main.rand.NextFloat(-spread, spread), Main.rand.NextFloat(-spread, spread),
                    80, default, poolActive ? 1.8f : 1.1f);
                d.noGravity = true;
            }
        }
    }
}

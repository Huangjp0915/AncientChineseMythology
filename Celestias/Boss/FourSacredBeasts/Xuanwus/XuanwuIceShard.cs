using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武冰锥 — 支持4种AI行为模式的冰晶弹幕
    /// ai[0] = 行为模式: 0=直射, 1=正弦波, 2=延迟追踪, 3=引力轨道
    /// ai[1] = 模式参数: Mode1=振幅, Mode2=追踪延迟帧, Mode3=锚点NPC.whoAmI
    /// 渲染: 顶点ribbon拖尾(CatmullRom平滑) + 冰晶弹头sprite
    /// </summary>
    public class XuanwuIceShard : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float crystalSpin;
        private int lifetime; //AI帧计数器
        private float baseSpeed; //初始速度大小(记录用于Mode2加速)
        private float sinePhase; //正弦波相位偏移(Mode1)
        private float spiralAngle; //轨道角(Mode3)
        private float spiralRadius; //轨道半径(Mode3)
        private static Asset<Effect> trailShaderRef;

        private int Mode => (int)Projectile.ai[0];
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

            switch (Mode) {
                case 0: AI_Straight(); break;
                case 1: AI_SineWave(); break;
                case 2: AI_DelayedHoming(); break;
                case 3: AI_GravityOrbit(); break;
                default: AI_Straight(); break;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //霜迹粒子(所有模式共享，但密度随模式变)
            int dustChance = Mode == 3 ? 2 : 3;
            if (Main.rand.NextBool(dustChance)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.IceTorch,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    120, default, 0.9f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Ice,
                    Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(-1, 1), 100, default, 0.7f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.4f);
        }

        //Mode 0: 直线飞行(原始行为)
        private void AI_Straight() { }

        //Mode 1: 正弦波横向偏移 — 产生波浪形轨迹
        private void AI_SineWave() {
            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                sinePhase = ModeParam; //用ai[1]做初始相位，不同弹幕错开
            }
            float freq = 0.12f;
            float amplitude = 2.8f;
            //垂直于飞行方向的偏移
            float angle = Projectile.velocity.ToRotation();
            float perpAngle = angle + MathHelper.PiOver2;
            float sineOffset = MathF.Cos(lifetime * freq + sinePhase) * amplitude;
            Projectile.velocity = Projectile.velocity.RotatedBy(0); //保持基础方向
            Projectile.Center += new Vector2(MathF.Cos(perpAngle), MathF.Sin(perpAngle)) * sineOffset;
        }

        //Mode 2: 延迟追踪 — 前N帧减速悬浮旋转蓄力，之后角度渐进追踪最近玩家
        private void AI_DelayedHoming() {
            int delayFrames = (int)MathF.Max(ModeParam, 20f);
            if (lifetime == 1) baseSpeed = Projectile.velocity.Length();

            if (lifetime < delayFrames) {
                //减速悬浮 + 缓慢旋转
                Projectile.velocity *= 0.94f;
                //蓄力冰晶旋转加速
                crystalSpin -= 0.04f * (lifetime / (float)delayFrames);
                //悬浮时微微颤动
                if (lifetime > delayFrames / 2) {
                    Projectile.Center += Main.rand.NextVector2Circular(1.5f, 1.5f);
                }
            }
            else if (lifetime == delayFrames) {
                //释放: 锁定目标方向，加速
                Player nearest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                Vector2 toTarget = (nearest.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = toTarget * baseSpeed * 1.4f;
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
            }
            else {
                //追踪阶段: 角度渐进转向(每帧最多转0.04rad)
                Player nearest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                float targetAngle = (nearest.Center - Projectile.Center).ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float turnRate = 0.04f;
                float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
                float actualTurn = MathHelper.Clamp(angleDiff, -turnRate, turnRate);
                float newAngle = currentAngle + actualTurn;
                float speed = Projectile.velocity.Length();
                //追踪中缓慢加速
                speed = MathF.Min(speed + 0.15f, baseSpeed * 2f);
                Projectile.velocity = new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle)) * speed;
            }
        }

        //Mode 3: 引力轨道 — 围绕锚点(ai[1]=NPC.whoAmI)螺旋运行，半径递减后释放
        private void AI_GravityOrbit() {
            int anchorId = (int)ModeParam;
            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                NPC anchor = anchorId >= 0 && anchorId < Main.maxNPCs ? Main.npc[anchorId] : null;
                if (anchor != null && anchor.active) {
                    Vector2 offset = Projectile.Center - anchor.Center;
                    spiralAngle = offset.ToRotation();
                    spiralRadius = offset.Length();
                    if (spiralRadius < 60f) spiralRadius = 60f;
                }
                else {
                    spiralRadius = 120f;
                    spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                }
            }

            NPC anchorNpc = anchorId >= 0 && anchorId < Main.maxNPCs ? Main.npc[anchorId] : null;
            if (anchorNpc == null || !anchorNpc.active) {
                //锚点消失 → 直射释放
                return;
            }

            //轨道运行
            float angularSpeed = 0.08f + 0.02f * (1f - spiralRadius / 200f);
            spiralAngle += angularSpeed;
            spiralRadius -= 0.6f; //逐渐收缩

            if (spiralRadius > 20f) {
                //轨道中
                Vector2 targetPos = anchorNpc.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralRadius;
                Projectile.velocity = (targetPos - Projectile.Center) * 0.3f;
            }
            else {
                //释放: 向最近玩家飞出
                Player nearest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                Vector2 toTarget = (nearest.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = toTarget * baseSpeed * 1.6f;
                Projectile.ai[0] = 0; //切换为直射，不再轨道
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.3f, Volume = 0.3f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            //收集有效历史位置(屏幕坐标)
            var positions = new System.Collections.Generic.List<Vector2>();
            positions.Add(drawPos); //当前位置在最前
            for (int i = 0; i < trailLen; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                positions.Add(Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition);
            }

            //至少3个点才构建ribbon
            if (positions.Count >= 3) {
                //获取着色器
                trailShaderRef ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/XuanwuTrailRibbon",
                    AssetRequestMode.ImmediateLoad);
                Effect shader = trailShaderRef?.Value;

                //Mode3轨道中的拖尾更宽更亮
                bool isOrbiting = Mode == 3 && spiralRadius > 20f;
                float widthMultiplier = isOrbiting ? 1.4f : 1f;

                var posArr = positions.ToArray();
                var verts = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => {
                        float baseW = MathHelper.Lerp(14f, 2f, p); //前粗后细
                        return baseW * widthMultiplier;
                    },
                    p => {
                        float alpha = (1f - p) * 0.7f;
                        Color c = Color.Lerp(new Color(180, 230, 255), new Color(60, 120, 200), p) * alpha;
                        c.A = 0; //Additive效果
                        return c;
                    },
                    uvScroll: (float)Main.gameTimeCache.TotalGameTime.TotalSeconds * 0.8f,
                    subdivisions: 3
                );

                if (verts.Length >= 4) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                    if (shader != null) {
                        shader.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
                        shader.Parameters["uGlowWidth"]?.SetValue(0.25f);
                        shader.Parameters["uAlphaFade"]?.SetValue(0.85f);
                        shader.Parameters["uScrollSpeed"]?.SetValue(1.2f);
                        shader.Parameters["uPulseRate"]?.SetValue(8f);
                        shader.Parameters["uPulseStrength"]?.SetValue(isOrbiting ? 0.3f : 0.15f);
                        shader.Parameters["uGlowColor"]?.SetValue(new Vector4(0.5f, 0.8f, 1f, 0.6f));
                        shader.Parameters["uCoreColor"]?.SetValue(new Vector4(0.8f, 0.95f, 1f, 0.3f));
                        shader.CurrentTechnique.Passes[0].Apply();
                    }

                    Texture2D ribbonTex = ACMAsset.LightShot;
                    gd.Textures[0] = ribbonTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

                    //第二层: 更窄更亮的内层ribbon
                    var innerVerts = ACMUtils.BuildRibbonStrip(
                        posArr,
                        p => MathHelper.Lerp(6f, 0.5f, p) * widthMultiplier,
                        p => {
                            float alpha = (1f - p) * 0.9f;
                            Color c = new Color(220, 245, 255) * alpha;
                            c.A = 0;
                            return c;
                        },
                        uvScroll: (float)Main.gameTimeCache.TotalGameTime.TotalSeconds * 1.5f,
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

            //弹头sprite绘制(保留原有冰晶视觉)
            Texture2D starTex = ACMAsset.BlankStar;
            Vector2 starOrigin = starTex.Size() / 2f;

            //Mode2蓄力中: 冰晶放大+脉冲
            float scaleBonus = 0f;
            if (Mode == 2 && lifetime < (int)MathF.Max(ModeParam, 20f)) {
                scaleBonus = 0.15f * MathF.Sin(lifetime * 0.3f);
            }
            //Mode3轨道中: 冰晶旋转更快
            if (isOrbitingMode3()) crystalSpin -= 0.08f;

            Color starColor = new Color(100, 180, 255, 0) * 0.5f;
            sb.Draw(starTex, drawPos, null, starColor, crystalSpin,
                starOrigin, 0.35f + scaleBonus, SpriteEffects.None, 0f);

            Color star2Color = new Color(160, 220, 255, 0) * 0.35f;
            sb.Draw(starTex, drawPos, null, star2Color, -crystalSpin * 0.7f,
                starOrigin, 0.28f + scaleBonus, SpriteEffects.None, 0f);

            Texture2D shotTex = ACMAsset.LightShot;
            Vector2 shotOrigin = shotTex.Size() / 2f;
            Color shotColor = new Color(140, 210, 255, 0) * 0.55f;
            sb.Draw(shotTex, drawPos, null, shotColor, Projectile.rotation,
                shotOrigin, 0.5f, SpriteEffects.None, 0f);

            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Color coreColor = new Color(200, 240, 255, 0) * 0.6f;
            sb.Draw(glowTex, drawPos, null, coreColor, 0f,
                glowOrigin, 0.5f + scaleBonus * 0.5f, SpriteEffects.None, 0f);

            return false;
        }

        private bool isOrbitingMode3() => Mode == 3 && spiralRadius > 20f;

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

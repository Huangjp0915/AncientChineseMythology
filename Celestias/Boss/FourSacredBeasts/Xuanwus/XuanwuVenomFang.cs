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
    /// 玄武蛇毒牙 — 支持4种AI行为模式的毒牙弹幕
    /// ai[0] = 行为模式: 0=直射, 1=蛇形S曲线, 2=收束螺旋, 3=抛物线毒液
    /// ai[1] = 模式参数: Mode1=蛇行频率, Mode2=目标玩家whoAmI, Mode3=unused
    /// 渲染: 顶点ribbon拖尾(脉动宽度) + 毒牙弹头sprite
    /// </summary>
    public class XuanwuVenomFang : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float venomPulse;
        private int lifetime;
        private float baseSpeed;
        private float spiralAngle; //Mode2
        private float spiralRadius; //Mode2
        private int snakeSide = 1; //Mode1蛇行翻转
        private static Asset<Effect> trailShaderRef;

        private int Mode => (int)Projectile.ai[0];
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
                case 1: AI_SnakePath(); break;
                case 2: AI_ConvergingSpiral(); break;
                case 3: AI_ParabolicVenom(); break;
                default: AI_Straight(); break;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //毒雾拖尾(所有模式)
            if (Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2Circular(6, 6);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset - Projectile.velocity * 0.3f,
                    0, 0, DustID.CursedTorch,
                    -Projectile.velocity.X * 0.1f + Main.rand.NextFloat(-0.5f, 0.5f),
                    -Projectile.velocity.Y * 0.1f + Main.rand.NextFloat(-0.5f, 0.5f),
                    100, default, 1f);
                d.noGravity = true;
                d.fadeIn = 1.4f;
            }
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Venom,
                    Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(0, 2), 80, default, 0.6f);
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.05f);
        }

        //Mode 0: 直线飞行
        private void AI_Straight() { }

        //Mode 1: 蛇形S曲线 — 偏转轴周期性翻转
        private void AI_SnakePath() {
            if (lifetime == 1) baseSpeed = Projectile.velocity.Length();
            float freq = ModeParam > 0 ? ModeParam : 0.15f;
            float amplitude = 3.2f;

            //每半周期翻转偏转方向，产生蛇行S曲线
            float cycle = MathF.Sin(lifetime * freq);
            if (cycle > 0 && snakeSide == -1) snakeSide = 1;
            else if (cycle < 0 && snakeSide == 1) snakeSide = -1;

            float angle = Projectile.velocity.ToRotation();
            float perpAngle = angle + MathHelper.PiOver2 * snakeSide;
            float sineOffset = MathF.Abs(MathF.Sin(lifetime * freq)) * amplitude;
            Projectile.Center += new Vector2(MathF.Cos(perpAngle), MathF.Sin(perpAngle)) * sineOffset;

            //微弱追踪修正(很轻微，不破坏蛇形)
            Player nearest = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            float targetAngle = (nearest.Center - Projectile.Center).ToRotation();
            float currAngle = Projectile.velocity.ToRotation();
            float diff = MathHelper.WrapAngle(targetAngle - currAngle);
            Projectile.velocity = Projectile.velocity.RotatedBy(MathHelper.Clamp(diff, -0.015f, 0.015f));
        }

        //Mode 2: 收束螺旋 — 围绕目标位置做逐渐收紧的螺旋
        private void AI_ConvergingSpiral() {
            int targetId = (int)ModeParam;
            Player target = targetId >= 0 && targetId < Main.maxPlayers ? Main.player[targetId] : null;
            if (target == null || !target.active)
                target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];

            if (lifetime == 1) {
                baseSpeed = Projectile.velocity.Length();
                Vector2 off = Projectile.Center - target.Center;
                spiralAngle = off.ToRotation();
                spiralRadius = off.Length();
                if (spiralRadius < 80f) spiralRadius = 200f;
            }

            float angularSpeed = 0.07f + 0.03f * (1f - spiralRadius / 300f);
            spiralAngle += angularSpeed;
            spiralRadius -= 1.2f;

            if (spiralRadius > 15f) {
                Vector2 targetPos = target.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralRadius;
                Projectile.velocity = (targetPos - Projectile.Center) * 0.25f;
            }
            else {
                //收束到目标附近 → 加速冲刺
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = toTarget * baseSpeed * 2f;
                Projectile.ai[0] = 0; //切为直射
            }
        }

        //Mode 3: 抛物线毒液 — 重力弧线+落地毒雾
        private void AI_ParabolicVenom() {
            Projectile.velocity.Y += 0.25f; //重力
            //最大下落速度限制
            if (Projectile.velocity.Y > 18f) Projectile.velocity.Y = 18f;

            //轻微横向漂移增加观感
            Projectile.velocity.X += MathF.Sin(lifetime * 0.1f) * 0.05f;

            //落地检测(射弹到达地面时产生毒雾区域)
            Projectile.tileCollide = lifetime > 10; //前10帧忽略碰撞(避免卡墙)
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            //收集有效历史位置(屏幕坐标)
            var positions = new System.Collections.Generic.List<Vector2>();
            positions.Add(drawPos);
            for (int i = 0; i < trailLen; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                positions.Add(Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition);
            }

            if (positions.Count >= 3) {
                trailShaderRef ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/XuanwuTrailRibbon",
                    AssetRequestMode.ImmediateLoad);
                Effect shader = trailShaderRef?.Value;

                float pulse = MathF.Sin(venomPulse);
                var posArr = positions.ToArray();
                //外层ribbon: 毒雾晕染(较宽，脉动)
                var verts = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => {
                        float baseW = MathHelper.Lerp(10f, 4f, p);
                        return baseW * (1f + pulse * 0.15f); //脉动宽度
                    },
                    p => {
                        float alpha = (1f - p) * 0.55f;
                        Color c = Color.Lerp(new Color(80, 220, 50), new Color(30, 100, 20), p) * alpha;
                        c.A = 0;
                        return c;
                    },
                    uvScroll: (float)Main.gameTimeCache.TotalGameTime.TotalSeconds * 0.6f,
                    subdivisions: 3
                );

                if (verts.Length >= 4) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                    if (shader != null) {
                        shader.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
                        shader.Parameters["uGlowWidth"]?.SetValue(0.35f);
                        shader.Parameters["uAlphaFade"]?.SetValue(0.8f);
                        shader.Parameters["uScrollSpeed"]?.SetValue(0.8f);
                        shader.Parameters["uPulseRate"]?.SetValue(6f);
                        shader.Parameters["uPulseStrength"]?.SetValue(0.2f);
                        shader.Parameters["uGlowColor"]?.SetValue(new Vector4(0.3f, 0.9f, 0.2f, 0.5f));
                        shader.Parameters["uCoreColor"]?.SetValue(new Vector4(0.6f, 1f, 0.3f, 0.25f));
                        shader.CurrentTechnique.Passes[0].Apply();
                    }

                    gd.Textures[0] = ACMAsset.SoftGlow;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

                    //内层ribbon: 毒芯(窄，亮黄绿)
                    var innerVerts = ACMUtils.BuildRibbonStrip(
                        posArr,
                        p => MathHelper.Lerp(4f, 0.5f, p),
                        p => {
                            float alpha = (1f - p) * 0.8f;
                            Color c = new Color(180, 255, 100) * alpha;
                            c.A = 0;
                            return c;
                        },
                        uvScroll: (float)Main.gameTimeCache.TotalGameTime.TotalSeconds * 1.2f,
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

            //弹头sprite
            float pulseSin = MathF.Sin(venomPulse);
            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Color outerGlow = new Color(60, 200, 40, 0) * (0.3f + pulseSin * 0.1f);
            sb.Draw(glowTex, drawPos, null, outerGlow, 0f,
                glowOrigin, 1.0f + pulseSin * 0.15f, SpriteEffects.None, 0f);

            Texture2D shotTex = ACMAsset.LightShot;
            Vector2 shotOrigin = shotTex.Size() / 2f;
            Color fangColor = new Color(100, 255, 70, 0) * 0.7f;
            sb.Draw(shotTex, drawPos, null, fangColor, Projectile.rotation,
                shotOrigin, 0.45f, SpriteEffects.None, 0f);

            Color coreColor = new Color(180, 255, 120, 0) * 0.5f;
            sb.Draw(glowTex, drawPos, null, coreColor, 0f,
                glowOrigin, 0.35f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            //Mode3落地: 毒雾爆开更大
            int dustCount = Mode == 3 ? 16 : 8;
            for (int i = 0; i < dustCount; i++) {
                int dustType = Main.rand.NextBool() ? DustID.CursedTorch : DustID.Venom;
                float spread = Mode == 3 ? 6f : 3f;
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    dustType, Main.rand.NextFloat(-spread, spread), Main.rand.NextFloat(-spread, spread),
                    80, default, Mode == 3 ? 1.8f : 1.1f);
                d.noGravity = true;
            }
        }
    }
}

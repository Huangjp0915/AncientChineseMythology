using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武冰柱 — 从地面/空中刺出的巨型冰晶柱
    /// 完全不同于飞行弹幕: 固定位置、有预兆、延迟刺出、持续判定
    /// ai[0] = 行为模式: 0=向上刺出, 1=向下刺落, 2=指定角度
    /// ai[1] = 模式参数: Mode0/1=预兆延迟帧, Mode2=刺出角度(弧度)
    /// 阶段: 预兆(闪烁裂纹) → 刺出(快速生长) → 持续(站立判定) → 碎裂(消散)
    /// 渲染: IcePillar着色器绘制程序化冰晶棱柱
    /// </summary>
    public class XuanwuIcePillar : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        //阶段计时
        private int lifetime;
        private int warningDuration;  //预兆持续帧数
        private int growDuration = 12; //刺出帧数
        private int holdDuration = 45; //持续帧数
        private int shatterDuration = 18; //碎裂帧数

        //视觉状态
        private float growthProgress;  //0~1生长进度
        private float shatterProgress; //0~1碎裂进度
        private float warningFlash;    //预兆闪烁
        private float pillarAngle;     //冰柱朝向角(0=向上)
        private float pillarHeight = 240f; //冰柱高度(像素)
        private float pillarWidth = 1f;    //宽度系数

        //着色器
        private static Asset<Effect> pillarShaderRef;

        private int Mode => (int)Projectile.ai[0];
        private float ModeParam => Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 240;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
        }

        public override void AI() {
            lifetime++;

            //首帧初始化
            if (lifetime == 1) {
                warningDuration = Math.Max((int)ModeParam, 15);
                Projectile.timeLeft = warningDuration + growDuration + holdDuration + shatterDuration + 5;

                switch (Mode) {
                    case 0: //向上刺出
                        pillarAngle = 0f;
                        break;
                    case 1: //向下刺落
                        pillarAngle = MathHelper.Pi;
                        break;
                    case 2: //指定角度
                        pillarAngle = ModeParam;
                        warningDuration = 25;
                        break;
                }

                //根据高度调整碰撞箱
                Projectile.width = (int)(50 * pillarWidth);
                Projectile.height = (int)pillarHeight;

                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.5f, Volume = 0.3f }, Projectile.Center);
            }

            //阶段推进
            int phase = GetPhase();

            if (phase == 0) {
                //========== 预兆阶段 ==========
                Projectile.damage = 0; //预兆时不造成伤害
                float warnT = (float)(lifetime) / warningDuration;

                //闪烁越来越快
                float flashFreq = 4f + warnT * 20f;
                warningFlash = (MathF.Sin(lifetime * flashFreq * 0.1f) * 0.5f + 0.5f) * warnT;
                growthProgress = warnT * 0.05f; //微微露头

                //预兆粒子: 地面裂纹+寒气上升
                if (Main.netMode != NetmodeID.Server) {
                    SpawnWarningDust(warnT);
                }
            }
            else if (phase == 1) {
                //========== 刺出阶段 ==========
                int growFrame = lifetime - warningDuration;
                float growT = (float)growFrame / growDuration;
                //使用BackOut缓动: 有弹性回弹的快速刺出
                growthProgress = ACMUtils.BackOut(Math.Min(growT, 1f));
                shatterProgress = 0f;

                //刺出音效(仅第1帧)
                if (growFrame == 1) {
                    SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);

                    //屏幕震动
                    if (Main.netMode != NetmodeID.Server) {
                        float shakeStr = pillarHeight / 240f * 6f;
                        Main.instance.CameraModifiers.Add(
                            new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                                Projectile.Center, Vector2.UnitY, shakeStr, 4f, 8, 800f, "IcePillar"));
                    }
                }

                //刺出粒子: 大量冰碎从基座飞出
                if (Main.netMode != NetmodeID.Server && growFrame <= growDuration) {
                    SpawnEruptionDust(growT);
                }
            }
            else if (phase == 2) {
                //========== 持续阶段 ==========
                growthProgress = 1f;
                shatterProgress = 0f;

                //站立时微弱呼吸光效
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                    SpawnIdleDust();
                }
            }
            else {
                //========== 碎裂阶段 ==========
                int shatterFrame = lifetime - warningDuration - growDuration - holdDuration;
                float shatterT = (float)shatterFrame / shatterDuration;
                growthProgress = 1f;
                shatterProgress = ACMUtils.QuadIn(Math.Min(shatterT, 1f));

                Projectile.damage = 0; //碎裂时不造成伤害

                //碎裂音效
                if (shatterFrame == 1) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.5f, Volume = 0.8f }, Projectile.Center);
                }

                //碎裂粒子
                if (Main.netMode != NetmodeID.Server && shatterFrame <= shatterDuration) {
                    SpawnShatterDust(shatterT);
                }

                if (shatterT >= 1f) {
                    Projectile.Kill();
                }
            }
        }

        private int GetPhase() {
            if (lifetime <= warningDuration) return 0;
            if (lifetime <= warningDuration + growDuration) return 1;
            if (lifetime <= warningDuration + growDuration + holdDuration) return 2;
            return 3;
        }

        //碰撞检测: 只在生长和持续阶段有效，使用旋转后的矩形
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int phase = GetPhase();
            if (phase != 1 && phase != 2) return false;

            //沿冰柱方向的线段碰撞
            float rot = pillarAngle - MathHelper.PiOver2; //转为方向角
            Vector2 basePos = Projectile.Center;
            Vector2 dir = new Vector2(MathF.Cos(rot), MathF.Sin(rot));
            float activeHeight = pillarHeight * growthProgress;

            //使用线段碰撞: 从基座到尖端
            Vector2 tipPos = basePos - dir * activeHeight;
            float lineWidth = 25f * pillarWidth;
            float dummy = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.Location.ToVector2(), targetHitbox.Size(), basePos, tipPos, lineWidth, ref dummy);
        }

        //========== 渲染 ==========
        public override bool PreDraw(ref Color lightColor) {
            if (growthProgress < 0.01f && warningFlash < 0.01f) return false;
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            int phase = GetPhase();

            //预兆阶段: 绘制地面裂纹预警
            if (phase == 0) {
                DrawWarningIndicator(sb);
                return false;
            }

            //刺出/持续/碎裂: 用着色器绘制冰柱
            DrawIcePillar(sb, gd);

            return false;
        }

        private void DrawWarningIndicator(SpriteBatch sb) {
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex == null) return;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glowTex.Size() / 2f;

            //脉冲发光的线条预兆
            float scale = 0.8f * pillarWidth;
            Color warnColor = Color.Lerp(new Color(40, 100, 200, 0), new Color(180, 230, 255, 0), warningFlash);
            warnColor *= warningFlash * 0.6f;

            //水平线(标记刺出位置)
            sb.Draw(glowTex, drawPos, null, warnColor, 0f, origin, new Vector2(scale * 1.5f, 0.15f), SpriteEffects.None, 0f);

            //垂直箭头指示刺出方向
            float arrowRot = pillarAngle;
            Color arrowColor = warnColor * 0.7f;
            for (int i = 0; i < 3; i++) {
                float offset = (20f + i * 15f) * warningFlash;
                Vector2 arrowPos = drawPos - new Vector2(MathF.Sin(arrowRot), -MathF.Cos(arrowRot)) * offset;
                sb.Draw(glowTex, arrowPos, null, arrowColor * (1f - i * 0.3f), arrowRot,
                    origin, new Vector2(0.1f, 0.2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawIcePillar(SpriteBatch sb, GraphicsDevice gd) {
            pillarShaderRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuIcePillar", AssetRequestMode.ImmediateLoad);
            Effect shader = pillarShaderRef?.Value;
            Texture2D baseTex = ACMAsset.SoftGlow;
            if (baseTex == null) return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
                shader.Parameters["uGrowth"]?.SetValue(growthProgress);
                shader.Parameters["uShatter"]?.SetValue(shatterProgress);
                shader.Parameters["uIntensity"]?.SetValue(0.9f);
                shader.Parameters["uWidth"]?.SetValue(pillarWidth);
                shader.CurrentTechnique.Passes[0].Apply();
            }

            //计算绘制矩形: 冰柱是一个从基座向上延伸的矩形
            //着色器UV: y=0是尖端(顶), y=1是基座(底)
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float drawW = 80f * pillarWidth;
            float drawH = pillarHeight;

            // 旋转绘制: origin在底部中央(基座), 向上延伸
            Vector2 origin = new Vector2(baseTex.Width / 2f, baseTex.Height); //底部中心

            float rot = pillarAngle; //0=向上
            float scaleX = drawW / baseTex.Width;
            float scaleY = drawH / baseTex.Height;

            sb.Draw(baseTex, basePos, null, Color.White, rot,
                origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);

            //第二层: Additive叠加辉光
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            if (shader != null) {
                shader.Parameters["uIntensity"]?.SetValue(0.35f * (1f - shatterProgress));
                shader.CurrentTechnique.Passes[0].Apply();
            }
            sb.Draw(baseTex, basePos, null, Color.White, rot,
                origin, new Vector2(scaleX * 1.15f, scaleY * 1.02f), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //尖端光点
            if (growthProgress > 0.8f && shatterProgress < 0.3f) {
                Texture2D starTex = ACMAsset.BlankStar;
                if (starTex != null) {
                    Vector2 tipDir = new Vector2(MathF.Sin(pillarAngle), -MathF.Cos(pillarAngle));
                    Vector2 tipPos = basePos - tipDir * pillarHeight * growthProgress;
                    float tipScale = 0.3f * (1f - shatterProgress) * pillarWidth;
                    float tipPulse = 1f + MathF.Sin(lifetime * 0.3f) * 0.2f;
                    Color tipColor = new Color(180, 230, 255, 0) * 0.8f;
                    sb.Draw(starTex, tipPos, null, tipColor, lifetime * 0.05f,
                        starTex.Size() / 2f, tipScale * tipPulse, SpriteEffects.None, 0f);
                }
            }
        }

        //========== 粒子效果 ==========
        private void SpawnWarningDust(float warnT) {
            //地面裂纹: 从刺出点散开的冰尘
            int count = (int)(warnT * 3) + 1;
            for (int i = 0; i < count; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(30f * pillarWidth, 8f);
                Vector2 dustPos = Projectile.Center + offset;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Ice, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
                //寒气向刺出方向飘
                Vector2 upDir = new Vector2(MathF.Sin(pillarAngle), -MathF.Cos(pillarAngle));
                d.velocity = upDir * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(0.5f, 0.5f);
                d.fadeIn = 0.5f;
            }
        }

        private void SpawnEruptionDust(float growT) {
            //基座爆裂: 大量碎冰从基座喷出
            int count = (int)(8 * (1f - growT)) + 2;
            for (int i = 0; i < count; i++) {
                Vector2 sideDir = new Vector2(MathF.Cos(pillarAngle), MathF.Sin(pillarAngle));
                float side = Main.rand.NextFloat(-1f, 1f);
                Vector2 dustPos = Projectile.Center + sideDir * side * 25f * pillarWidth;

                int dustType = Main.rand.NextBool(3) ? DustID.Water : DustID.Ice;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 80, default, 2f + Main.rand.NextFloat(1f));
                d.noGravity = true;
                d.velocity = sideDir * side * Main.rand.NextFloat(3f, 8f) +
                    new Vector2(0, -Main.rand.NextFloat(1f, 4f));
            }
        }

        private void SpawnIdleDust() {
            //站立时: 柱身霜花飘落
            Vector2 upDir = new Vector2(MathF.Sin(pillarAngle), -MathF.Cos(pillarAngle));
            float h = Main.rand.NextFloat(0.2f, 0.9f) * pillarHeight;
            Vector2 dustPos = Projectile.Center - upDir * h +
                Main.rand.NextVector2Circular(20f * pillarWidth, 5f);
            Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Ice, 0, 0, 120, default, 1f);
            d.noGravity = false;
            d.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f));
        }

        private void SpawnShatterDust(float shatterT) {
            //碎裂: 冰柱碎片向四方飞散
            int count = (int)(6 * (1f - shatterT)) + 1;
            for (int i = 0; i < count; i++) {
                Vector2 upDir = new Vector2(MathF.Sin(pillarAngle), -MathF.Cos(pillarAngle));
                float h = Main.rand.NextFloat(0f, 1f) * pillarHeight * growthProgress;
                Vector2 dustPos = Projectile.Center - upDir * h;

                int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 60, default, 2.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(6f, 6f) +
                    new Vector2(0, -Main.rand.NextFloat(1f, 3f));
            }
        }
    }
}

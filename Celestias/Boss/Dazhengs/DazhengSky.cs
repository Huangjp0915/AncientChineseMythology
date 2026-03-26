using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class DazhengSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Dazheng>());
        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (player.Alives())
            {
                player.ManageSpecialBiomeVisuals(DazhengSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 大椿天空效果 — 万木神域天幕
    /// 
    /// 多层绘制结构：
    ///  1. 深林渐变底色 — 墨绿/琥珀梯度，古木森然
    ///  2. Smoke帧动画树冠剪影 — 浓密枝叶漂移
    ///  3. GlaciateWave金色神光 — 从天顶洒落的光柱
    ///  4. Sparkle/BlankStar落叶粒子 — 金绿叶片飘零
    ///  5. SoftGlow藤蔓暗角 — 屏幕边缘蔓生
    ///  6. 暗角压迫 + 金色脉冲呼吸
    /// </summary>
    internal class DazhengSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:DazhengSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.008f;
        private const float FadeOutSpeed = 0.015f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;

        // 颜色定义 — 大椿：墨绿 / 琥珀金 / 古木褐
        private static readonly Color DeepMoss = new(10, 30, 12);
        private static readonly Color AncientAmber = new(140, 100, 30);
        private static readonly Color SacredGold = new(220, 190, 80);
        private static readonly Color CanopyGreen = new(25, 60, 20);
        private static readonly Color BarkBrown = new(40, 25, 12);
        private static readonly Color LeafGold = new(180, 160, 50);
        private static readonly Color VineGreen = new(15, 45, 15);

        // 树冠阴影云
        private const int CanopyCount = 35;
        private readonly CanopyCloud[] canopies = new CanopyCloud[CanopyCount];

        // 神光光柱
        private const int GodRayCount = 5;
        private readonly GodRay[] godRays = new GodRay[GodRayCount];

        // 落叶粒子
        private const int LeafCount = 30;
        private readonly FallingLeaf[] leaves = new FallingLeaf[LeafCount];

        // 藤蔓暗角层
        private const int VineTendrilCount = 6;
        private readonly float[] vinePhases = new float[VineTendrilCount];

        #region IACMLoader 注册

        void IACMLoader.LoadData()
        {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.06f, 0.08f, 0.03f)
                .UseOpacity(0.3f), EffectPriority.High);

            for (int i = 0; i < CanopyCount; i++) canopies[i] = new CanopyCloud();
            for (int i = 0; i < GodRayCount; i++) godRays[i] = new GodRay(i);
            for (int i = 0; i < LeafCount; i++) leaves[i] = new FallingLeaf();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;

            for (int i = 0; i < CanopyCount; i++) canopies[i].Reset();
            for (int i = 0; i < GodRayCount; i++) godRays[i].Reset();
            for (int i = 0; i < LeafCount; i++) leaves[i].Reset();
            for (int i = 0; i < VineTendrilCount; i++) vinePhases[i] = i * 1.2f;
        }

        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.01f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime)
        {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive)
            {
                if (!active) Activate(Vector2.Zero);
                bossHealthPercent = (float)boss.life / boss.lifeMax;
                isPhase2 = bossHealthPercent < Dazheng.Phase2Threshold;

                float target = isPhase2 ? MaxIntensity * 1.15f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else
            {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            float driftMul = isPhase2 ? 1.4f : 1f;
            for (int i = 0; i < CanopyCount; i++) canopies[i].Update(driftMul);
            for (int i = 0; i < GodRayCount; i++) godRays[i].Update(globalTime, isPhase2);
            for (int i = 0; i < LeafCount; i++) leaves[i].Update(globalTime);
            for (int i = 0; i < VineTendrilCount; i++) vinePhases[i] += 0.008f;
        }

        private static NPC FindBoss()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == ModContent.NPCType<Dazheng>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f)
            {
                DrawBackground(spriteBatch);
                DrawCanopy(spriteBatch);
                DrawGodRays(spriteBatch);
                DrawLeaves(spriteBatch);
                DrawVineTendrils(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 深林渐变底色

        private void DrawBackground(SpriteBatch sb)
        {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            // 基底暗色
            sb.Draw(pixel, screen, DeepMoss * intensity * 0.85f);

            // 多段渐变：顶部深绿 → 中部琥珀暖光 → 底部古木褐
            int bands = 12;
            for (int i = 0; i < bands; i++)
            {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                Rectangle r = new(0, i * h, Main.screenWidth, h + 1);

                Color c;
                if (t < 0.4f)
                    c = Color.Lerp(CanopyGreen, DeepMoss, t / 0.4f);
                else if (t < 0.7f)
                    c = Color.Lerp(DeepMoss, BarkBrown, (t - 0.4f) / 0.3f);
                else
                    c = Color.Lerp(BarkBrown, DeepMoss, (t - 0.7f) / 0.3f);

                sb.Draw(pixel, r, c * intensity * 0.4f);
            }

            // 二阶段：琥珀色涌动呼吸
            float breath = (0.5f + MathF.Sin(globalTime * 1.2f) * 0.5f) * intensity * 0.05f;
            if (isPhase2) breath *= 2.2f;
            Color breathC = AncientAmber * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 树冠剪影

        private void DrawCanopy(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < CanopyCount; i++)
            {
                CanopyCloud c = canopies[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;

                // 树冠用深绿色，偶尔带金色点缀
                float lerp = MathF.Sin(globalTime * 0.3f + i * 0.4f) * 0.5f + 0.5f;
                Color cc = Color.Lerp(new Color(8, 28, 10), new Color(18, 45, 15), lerp);
                if (i % 7 == 0) cc = Color.Lerp(cc, AncientAmber, 0.08f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.55f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                // 微弱金色叶片光晕
                if (i % 4 == 0)
                {
                    Color glow = LeafGold * alpha * 0.12f;
                    glow.A = 0;
                    sb.Draw(tex, dp, src, glow, c.Rotation * 0.95f, origin, c.Scale * 1.2f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层3 — 金色神光（God Rays）

        private void DrawGodRays(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int i = 0; i < GodRayCount; i++)
            {
                GodRay ray = godRays[i];
                if (ray.Alpha <= 0.01f) continue;

                float alpha = ray.Alpha * intensity;

                // 金色光柱
                Color rayC = Color.Lerp(AncientAmber, SacredGold, ray.Alpha) * alpha * 0.35f;
                rayC.A = 0;

                Vector2 pos = new(ray.ScreenX, -tex.Height * 0.2f);
                // 拉伸成竖直光柱
                Vector2 scale = new(0.15f * ray.Width, Main.screenHeight * 1.4f / tex.Height);
                sb.Draw(tex, pos, null, rayC, ray.Angle, origin, scale, SpriteEffects.None, 0f);

                // 外层柔光
                Color outerC = SacredGold * alpha * 0.12f;
                outerC.A = 0;
                sb.Draw(tex, pos, null, outerC, ray.Angle, origin,
                    scale * new Vector2(1.6f, 1.05f), SpriteEffects.None, 0f);
            }

            // 二阶段：额外的密集金辉
            if (isPhase2)
            {
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow == null) return;
                Vector2 glowOrigin = glow.Size() / 2f;

                float pulse = (MathF.Sin(globalTime * 2.5f) * 0.5f + 0.5f) * intensity * 0.08f;
                Color gc = SacredGold * pulse;
                gc.A = 0;

                // 屏幕中央大范围金色光晕
                float gs = MathF.Max(Main.screenWidth, Main.screenHeight) * 0.6f / glow.Width;
                sb.Draw(glow, new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.3f),
                    null, gc, 0f, glowOrigin, gs, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 落叶粒子

        private void DrawLeaves(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < LeafCount; i++)
            {
                FallingLeaf leaf = leaves[i];
                if (!leaf.IsActive) continue;

                Vector2 dp = leaf.Position - Main.screenPosition;
                float progress = MathF.Sin(leaf.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.55f;

                // 金绿色交替的叶片
                float colorLerp = MathF.Sin(globalTime * 0.8f + i * 1.3f) * 0.5f + 0.5f;
                Color lc;
                if (i % 3 == 0)
                    lc = Color.Lerp(SacredGold, LeafGold, colorLerp) * alpha;
                else
                    lc = Color.Lerp(CanopyGreen, LeafGold, colorLerp * 0.6f) * alpha;
                lc.A = 0;

                float scale = leaf.Scale * (0.05f + progress * 0.08f);
                float rot = leaf.Rotation + globalTime * leaf.RotSpeed;

                // 扁平化模拟叶片翻转
                Vector2 leafScale = new(scale, scale * (0.5f + MathF.Abs(MathF.Sin(rot * 0.7f)) * 0.5f));
                sb.Draw(tex, dp, null, lc, rot, origin, leafScale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层5 — 藤蔓暗角

        private void DrawVineTendrils(SpriteBatch sb)
        {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 go = glow.Size() / 2f;

            float baseAlpha = intensity * 0.3f;
            if (isPhase2) baseAlpha *= 1.5f;

            for (int i = 0; i < VineTendrilCount; i++)
            {
                float phase = vinePhases[i];
                float sway = MathF.Sin(phase * 2.5f + i * 1.1f) * 30f;
                float breathe = 0.9f + MathF.Sin(phase * 1.8f + i) * 0.15f;

                // 沿屏幕四边分布
                Vector2 pos;
                float baseScale;
                switch (i % 4)
                {
                    case 0: // 左边
                        pos = new Vector2(-20 + sway * 0.3f, Main.screenHeight * (0.2f + i * 0.12f));
                        baseScale = 1.8f;
                        break;
                    case 1: // 右边
                        pos = new Vector2(Main.screenWidth + 20 - sway * 0.3f, Main.screenHeight * (0.15f + i * 0.11f));
                        baseScale = 1.8f;
                        break;
                    case 2: // 顶部
                        pos = new Vector2(Main.screenWidth * (0.15f + i * 0.1f) + sway, -15);
                        baseScale = 2.2f;
                        break;
                    default: // 底部
                        pos = new Vector2(Main.screenWidth * (0.25f + i * 0.08f) + sway, Main.screenHeight + 15);
                        baseScale = 1.6f;
                        break;
                }

                float scale = baseScale * breathe * Main.screenHeight * 0.3f / glow.Width;
                Color vc = VineGreen * baseAlpha * (0.6f + MathF.Sin(phase * 3f) * 0.2f);
                vc.A = 0;
                sb.Draw(glow, pos, null, vc, phase * 0.1f, go, scale, SpriteEffects.None, 0f);

                // 叠加深色层增强压迫感
                Color darkC = DeepMoss * baseAlpha * 0.4f;
                darkC.A = 0;
                sb.Draw(glow, pos, null, darkC, phase * 0.08f, go, scale * 0.85f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 金色脉冲

        private void DrawVignette(SpriteBatch sb)
        {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null)
            {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.5f;
                if (isPhase2) va *= 1.3f;
                Color vc = DeepMoss with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                // 四角暗色压迫
                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            // 金色脉冲 — 自然之力的律动
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f) * intensity * 0.06f;
            if (isPhase2) pulse *= 1.8f;

            Color topC = Color.Lerp(AncientAmber, SacredGold,
                MathF.Sin(globalTime * 0.7f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 4), topC);

            // 底部微弱的根须暗色
            Color bottomC = BarkBrown * pulse * 0.5f;
            bottomC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 3 / 4, Main.screenWidth, Main.screenHeight / 4), bottomC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor)
        {
            // 偏暖的金绿色调
            Color tint = Color.Lerp(Color.White, new Color(55, 60, 35), intensity * 0.3f);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.8f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        #region CanopyCloud — 树冠剪影

        private class CanopyCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset()
            {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 50);
            }

            public void Update(float mul)
            {
                if (!IsActive)
                {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                Rotation += 0.0003f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul)
            {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0008f, 0.003f);
                // 主要集中在屏幕上半部分（树冠区域）
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-500, Main.screenWidth + 500),
                    Main.screenPosition.Y + Main.rand.Next(-300, (int)(Main.screenHeight * 0.5f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(0.1f, 0.5f) * mul, Main.rand.NextFloat(-0.05f, 0.08f));
                Scale = Main.rand.NextFloat(3f, 6f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        #endregion

        #region GodRay — 金色神光

        private class GodRay
        {
            public float ScreenX, Alpha, Width, Angle;
            private readonly int index;
            private float timer;
            private readonly float period;
            private readonly float glowDuration;
            private bool glowing;

            public GodRay(int i)
            {
                index = i;
                period = 4f + i * 1.5f;
                glowDuration = 1.8f + i * 0.3f;
                Reset();
            }

            public void Reset()
            {
                Alpha = 0f;
                timer = 0f;
                glowing = false;
                ScreenX = Main.screenWidth * (0.1f + index * 0.18f);
                Width = 0.8f + index * 0.15f;
                Angle = -0.05f + index * 0.025f;
            }

            public void Update(float gTime, bool intense)
            {
                timer += 1f / 60f;
                float p = intense ? period * 0.6f : period;
                float pos = timer % p;

                if (pos < glowDuration && !glowing)
                {
                    glowing = true;
                    // 光柱位置有微弱的左右飘动
                    ScreenX = Main.screenWidth * (0.1f + index * 0.18f)
                            + MathF.Sin(gTime * 0.4f + index * 1.5f) * (Main.screenWidth * 0.05f);
                    Angle = -0.05f + index * 0.025f + MathF.Sin(gTime * 0.25f + index) * 0.03f;
                }

                if (glowing)
                {
                    if (pos < glowDuration)
                    {
                        float t = pos / glowDuration;
                        // 缓入缓出
                        Alpha = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                        Alpha = MathHelper.Clamp(Alpha, 0f, 1f);
                    }
                    else
                    {
                        glowing = false;
                        Alpha = 0f;
                    }
                }
                else
                {
                    Alpha = MathHelper.Lerp(Alpha, 0f, 0.1f);
                }
            }
        }

        #endregion

        #region FallingLeaf — 落叶

        private class FallingLeaf
        {
            public Vector2 Position;
            public float Scale, AnimProgress, AnimSpeed, Rotation, RotSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;
            private float swayPhase;

            public void Reset()
            {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(10, 80);
            }

            public void Update(float gTime)
            {
                if (!IsActive)
                {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                // 左右飘摆
                float sway = MathF.Sin(gTime * 1.5f + swayPhase) * 0.6f;
                Position += Velocity + new Vector2(sway, 0f);
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate()
            {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.004f, 0.012f);
                // 从屏幕上方飘落
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-50, Main.screenWidth + 50),
                    Main.screenPosition.Y + Main.rand.Next(-80, (int)(Main.screenHeight * 0.2f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.3f, 1.2f));
                Scale = Main.rand.NextFloat(0.6f, 1.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotSpeed = Main.rand.NextFloat(0.5f, 2.5f) * (Main.rand.NextBool() ? 1f : -1f);
                swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        #endregion
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 冥诏印记 —— 阴天子"点名审判"的宣判阶段（本体无伤害）。
    /// 落在玩家<b>当时</b>位置后便锁死不动：金环从 320px 收缩到 140px 执行圈，
    /// 三声递进磬音倒计时，宣判期满在原地天降审判柱（<see cref="YinEmperorJudgmentColumn"/>）。
    /// 反制方式唯一且清晰：离开执行圈即完全安全（印记不追踪）。
    /// ai[0] = 起拍延迟帧（多印错拍）；ai[1] = 执行圈半径（默认 140）。
    /// </summary>
    public class YinEmperorDecreeSeal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int AppearTime = 12;   // 印记展开
        private const int VerdictTime = 96;  // 宣判倒计时
        private const int FadeTime = 22;     // 执行后淡出

        private ref float Delay => ref Projectile.ai[0];
        private ref float ExecRadius => ref Projectile.ai[1];

        private float timer;
        private bool judged;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        private float SealR => ExecRadius > 10f ? ExecRadius : 140f;

        public override void AI() {
            // 起拍延迟（多印错拍：印与印执行时刻错开，形成逐步驱赶）
            if (Delay > 0) {
                Delay--;
                return;
            }

            timer++;
            Projectile.velocity = Vector2.Zero;

            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.55f, Volume = 0.9f }, Projectile.Center);
            }

            // 三声递进磬音（音高上行 = 可闭眼读秒的听觉倒计时）
            float vt = timer - AppearTime;
            if (vt == 30) SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.25f, Volume = 0.85f }, Projectile.Center);
            if (vt == 60) SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.1f, Volume = 0.95f }, Projectile.Center);
            if (vt == 85) SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.45f, Volume = 1.05f }, Projectile.Center);

            // 宣判期粒子：外环向执行圈汇聚
            if (Main.netMode != NetmodeID.Server && vt > 0 && vt < VerdictTime && timer % 3 == 0) {
                float prog = vt / VerdictTime;
                float ringR = MathHelper.Lerp(320f, SealR, ACMUtils.SineInOut(prog));
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * ringR;
                var d = Dust.NewDustPerfect(pos, prog > 0.75f ? DustID.RedTorch : DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.1f + prog * 0.7f;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (1.5f + prog * 2f);
            }

            // 宣判期满 → 执行（审判柱天降）
            if (!judged && vt >= VerdictTime) {
                judged = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int damage = YinEmperorHelper.GetScaledDamage(130);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorJudgmentColumn>(), damage, 3f, Main.myPlayer,
                        ai0: SealR);
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.35f, Volume = 1.2f }, Projectile.Center);
            }

            if (vt >= VerdictTime + FadeTime)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Delay > 0)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = ACMAsset.SoftGlow;
            Texture2D star = ACMAsset.BlankStar;
            if (glow == null)
                return false;

            float vt = timer - AppearTime;
            float appear = MathHelper.Clamp(timer / (float)AppearTime, 0f, 1f);
            float prog = MathHelper.Clamp(vt / VerdictTime, 0f, 1f);
            float fade = judged ? MathHelper.Clamp(1f - (vt - VerdictTime) / FadeTime, 0f, 1f) : 1f;
            float master = ACMUtils.BackOut(appear) * fade;
            if (master <= 0.01f)
                return false;

            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 gOrigin = glow.Size() / 2f;

            // 末 24 帧转红（红只留给即将造成伤害的预警）
            bool lastCall = vt > VerdictTime - 24;
            Color ringCol = lastCall ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            ringCol.A = 0;
            float tremble = lastCall ? MathF.Sin((float)Main.GlobalTimeWrappedHourly * 60f) * 3f : 0f;

            // 执行圈（常显危险区，随进度增亮）
            int segs = 36;
            for (int i = 0; i < segs; i++) {
                float a = MathHelper.TwoPi * i / segs;
                Vector2 pos = center + a.ToRotationVector2() * (SealR + tremble);
                sb.Draw(glow, pos, null, ringCol * (0.25f + prog * 0.55f) * master, 0f, gOrigin,
                    0.16f + prog * 0.1f, SpriteEffects.None, 0f);
            }

            // 收缩环（从 320 收拢到执行圈的"宣判进度"环）
            if (vt > 0 && !judged) {
                float shrinkR = MathHelper.Lerp(320f, SealR, ACMUtils.SineInOut(prog));
                Color shrinkCol = YinEmperorHelper.ImperialGold;
                shrinkCol.A = 0;
                for (int i = 0; i < segs; i++) {
                    float a = MathHelper.TwoPi * i / segs + prog * 2f;
                    Vector2 pos = center + a.ToRotationVector2() * shrinkR;
                    sb.Draw(glow, pos, null, shrinkCol * 0.6f * master, 0f, gOrigin, 0.2f, SpriteEffects.None, 0f);
                }
            }

            // 中心印玺：小法环 + 星芒
            Texture2D ring = YinEmperorHelper.RingTexture;
            if (ring != null) {
                Color sealCol = Color.Lerp(YinEmperorHelper.ImperialGold, TelegraphColors.Lethal, prog * 0.6f);
                sealCol.A = 0;
                float spin = (float)Main.GlobalTimeWrappedHourly * (1f + prog * 3f);
                sb.Draw(ring, center, null, sealCol * 0.8f * master, spin, ring.Size() / 2f,
                    0.32f + prog * 0.12f, SpriteEffects.None, 0f);
            }
            if (star != null) {
                Color starCol = Color.White;
                starCol.A = 0;
                sb.Draw(star, center, null, starCol * (0.4f + prog * 0.5f) * master,
                    prog * 4f, star.Size() / 2f, 0.5f + prog * 0.4f, SpriteEffects.None, 0f);
            }

            // 天降预告细线：从印记指向天顶的淡金线（提示柱将从上而下）
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color lineCol = ringCol * (0.12f + prog * 0.3f) * master;
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), lineCol, -MathHelper.PiOver2,
                new Vector2(0f, 0.5f), new Vector2(1400f, 2f + prog * 4f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 审判柱 —— 冥诏印记宣判期满后的"执行"：天降金红光柱。
    /// 预警形状（执行圈直径）与伤害形状严格一致；8 帧展开 + 12 帧持续 + 6 帧消退。
    /// 命中叠 2 层冥律。ai[0] = 半宽（执行圈半径）。
    /// </summary>
    public class YinEmperorJudgmentColumn : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ExpandTime = 8;
        private const int HoldTime = 12;
        private const int FadeTime = 6;
        private const float ColumnHeight = 1800f;

        private ref float HalfWidth => ref Projectile.ai[0];

        private float timer;
        private float widthRatio;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ExpandTime + HoldTime + FadeTime + 4;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            timer++;

            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.7f, Volume = 1.4f }, Projectile.Center);
                ACMScreenShakeSystem.Add(6f);
                // 落地喷发
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 26; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-11f, -3f));
                        var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-1f, 1f) * HalfWidth * 0.7f, 0), DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.6f + Main.rand.NextFloat(0.8f);
                        d.velocity = vel;
                    }
                }
            }

            if (timer <= ExpandTime)
                widthRatio = ACMUtils.ElasticOut(timer / (float)ExpandTime);
            else if (timer <= ExpandTime + HoldTime)
                widthRatio = 1f;
            else
                widthRatio = MathHelper.Clamp(1f - (timer - ExpandTime - HoldTime) / (float)FadeTime, 0f, 1f);

            // 柱内光照
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Projectile.Center - new Vector2(0, i * 300f),
                    YinEmperorHelper.DragonVeinGold.ToVector3() * 0.7f * widthRatio);
            }

            if (Main.netMode != NetmodeID.Server && widthRatio > 0.5f && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-1f, 1f) * HalfWidth * 0.8f,
                    Main.rand.NextFloat(0f, 900f));
                var d = Dust.NewDustPerfect(pos, Main.rand.NextBool(3) ? DustID.RedTorch : DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = new Vector2(0, Main.rand.NextFloat(3f, 8f));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伤害窗口与视觉严格对齐：宽度未达 6 成不判定
            if (widthRatio < 0.6f)
                return false;
            float halfW = HalfWidth * widthRatio;
            Rectangle column = new Rectangle(
                (int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - ColumnHeight),
                (int)(halfW * 2f), (int)(ColumnHeight + 40f));
            return column.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<YinJudgmentPlayer>().AddDecreeStack(2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (widthRatio <= 0.02f)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float w = HalfWidth * 2f * widthRatio;

            // 多层像素柱（外紫 → 金 → 白核）
            DrawColumnLayer(sb, pixel, basePos, w * 1.5f, YinEmperorHelper.AbyssPurple, 0.10f * widthRatio);
            DrawColumnLayer(sb, pixel, basePos, w * 1.0f, YinEmperorHelper.ImperialGold, 0.22f * widthRatio);
            DrawColumnLayer(sb, pixel, basePos, w * 0.55f, YinEmperorHelper.DragonVeinGold, 0.4f * widthRatio);
            DrawColumnLayer(sb, pixel, basePos, w * 0.22f, Color.White, 0.5f * widthRatio);

            // 底部落点扩散
            Color baseGlow = YinEmperorHelper.DragonVeinGold;
            baseGlow.A = 0;
            for (int i = 3; i >= 0; i--) {
                float size = w * (0.8f + i * 0.35f);
                sb.Draw(pixel, basePos, new Rectangle(0, 0, 1, 1), baseGlow * (0.12f / (i + 1)) * widthRatio,
                    0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.24f), SpriteEffects.None, 0f);
            }

            // BeamGrad 金核（着色器缺失时上方像素层兜底）
            ACMShaders.DrawBeam(Projectile.Center, Projectile.Center - new Vector2(0, ColumnHeight),
                w * 0.32f, YinEmperorHelper.DragonVeinGold, TelegraphColors.Execution, widthRatio,
                flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.6f);

            return false;
        }

        private void DrawColumnLayer(SpriteBatch sb, Texture2D pixel, Vector2 basePos, float width, Color color, float alpha) {
            color.A = 0;
            sb.Draw(pixel, basePos, new Rectangle(0, 0, 1, 1), color * alpha, -MathHelper.PiOver2,
                new Vector2(0f, 0.5f), new Vector2(ColumnHeight, width), SpriteEffects.None, 0f);
        }
    }
}

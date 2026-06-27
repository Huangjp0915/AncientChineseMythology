using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙·天罚雷柱 (V2) — 自带「预告→落雷」子状态的纵向雷柱。
    /// Telegraphed vertical thunder column: a fixed-position lethal strike that warns the player with a
    /// growing **red** column (TelegraphColors.Lethal, §6.1 红=致命) for <c>ai[0]</c> ticks, then snaps to a
    /// short azure damaging beam. 替代旧版「满屏 QinglongThunderBolt 无预告下落」的不可读弹幕。
    ///
    /// 演出经硬化 ACMShaders.DrawBeam (BeamGrad) 绘制; 服务端零绘制; 仅释放窗口 hostile=true。
    /// ai[0] = 预告 tick (由施放者按威胁等级传入, §6.1 时间编码)。
    /// </summary>
    public class QinglongThunderColumn : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int StrikeTicks = 18;
        private const float ColumnHeight = 1700f;

        private int Telegraph => Math.Max(12, (int)Projectile.ai[0]);
        private ref float Age => ref Projectile.localAI[1];

        private bool Striking => Age >= Telegraph;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = (int)ColumnHeight;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 释放期才致命; 预告期完全无害 (红柱只是路径警告)
            Projectile.hostile = Striking;

            if (Age == Telegraph) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.7f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) }, Projectile.Center);
                ACMUtils.AddScreenShake(5f);
            }

            if (!Main.dedServ) {
                if (!Striking && Age % 4 == 0) {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(0, Main.rand.NextFloat(-700, 700)), 4, 4,
                        DustID.RedTorch, 0, 0, 150, default, 0.9f);
                    d.noGravity = true;
                }
                else if (Striking) {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(0, Main.rand.NextFloat(-700, 700)), 6, 6,
                        DustID.Electric, Main.rand.NextFloat(-3, 3), 0, 60, default, 1.3f);
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, Striking ? new Vector3(0.4f, 0.7f, 0.9f) : new Vector3(0.5f, 0.1f, 0.12f));

            Age++;
            if (Age >= Telegraph + StrikeTicks)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 top = Projectile.Center - new Vector2(0, ColumnHeight * 0.5f);
            Vector2 bottom = Projectile.Center + new Vector2(0, ColumnHeight * 0.5f);

            if (!Striking) {
                // 预告: 红色细柱, 渐强可读 (命中前可读)
                float p = MathHelper.Clamp(Age / Telegraph, 0f, 1f);
                float width = MathHelper.Lerp(2.5f, 7f, p);
                float intensity = MathHelper.Lerp(0.28f, 0.95f, p);
                Color core = TelegraphColors.Lethal;
                Color edge = TelegraphColors.Lethal * 0.35f;
                ACMShaders.DrawBeam(top, bottom, width, core, edge, intensity, flowSpeed: 2.2f, flowScale: 3.0f);
            }
            else {
                // 释放: 青白雷柱, 快速收束淡出
                float sp = MathHelper.Clamp((Age - Telegraph) / (float)StrikeTicks, 0f, 1f);
                float fade = 1f - sp;
                float width = 10f + 24f * fade;
                Color core = new Color(210, 240, 255);
                Color edge = TelegraphColors.AzureDragon * 0.7f;
                ACMShaders.DrawBeam(top, bottom, width, core, edge, 0.6f + 0.4f * fade, flowSpeed: 3.2f, flowScale: 2.0f, coreGlow: 1.4f);
            }

            return false;
        }
    }
}

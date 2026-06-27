using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    internal class SaberHell : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.velocity = Projectile.velocity.UnitVector();
            // 处理特殊前置阶段：localAI[0] < 0 表示图案附加前旋或延伸
            if (Projectile.localAI[0] < 0) {
                Projectile.localAI[0]++;
                // 旋转阶段：围绕 ai 里记录的中心点公转
                if (Projectile.localAI[0] < -10) {
                    Vector2 center = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                    float ang = Projectile.velocity.ToRotation();
                    ang += 0.2f * Math.Sign(Projectile.velocity.X + Projectile.velocity.Y);
                    Vector2 toCenter = Projectile.Center - center;
                    toCenter = toCenter.RotatedBy(0.12f);
                    Projectile.Center = center + toCenter;
                }
                if (Projectile.localAI[0] == -10) {
                    // 向中心收束
                    Vector2 center = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                    Projectile.velocity = (center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 28f;
                }
                return;
            }

            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) Projectile.localAI[1] = 30;
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40) {
                    int num = 1000;
                    int num2 = 36;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Projectile.velocity *= -1;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                }
            }
            else {
                if (Projectile.localAI[1] > 0) Projectile.localAI[1]--;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // V2: 刀光升格为 BeamGrad 流动梯度直带 (toolkit §A.6 DrawBeam)。
            // 充能越满越红 = 致命预警 (§6.1 红只留给伤害源): 充满后旋即生成 SaberKiller(真实刀刃)。
            float chargeT;   // 0~1 充能进度(→红)
            float thickness; // 屏幕像素全宽
            float intensity; // 0~1 整体亮度/淡入淡出
            if (Projectile.localAI[0] < 0) {
                // 前置旋转/收束阶段: 细预告线渐增, 蓝色 (尚未致命)
                float pre = MathHelper.Clamp(Math.Abs(Projectile.localAI[0]) / 60f, 0f, 1f);
                chargeT = pre * 0.35f;
                thickness = MathHelper.Lerp(6f, 26f, pre);
                intensity = pre * 0.7f;
            }
            else {
                chargeT = MathHelper.Clamp(Projectile.localAI[0] / 40f, 0f, 1f);
                thickness = MathHelper.Lerp(8f, 64f, chargeT);
                intensity = MathHelper.Clamp(Projectile.localAI[1] / 30f, 0f, 1f);
            }
            if (intensity <= 0.01f)
                return false;

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            const float halfLen = 2600f;
            Vector2 start = Projectile.Center - dir * halfLen;
            Vector2 end = Projectile.Center + dir * halfLen;

            Color core = Color.Lerp(new Color(190, 224, 255), TelegraphColors.Lethal, chargeT);
            Color edge = Color.Lerp(new Color(40, 90, 160), new Color(150, 20, 30), chargeT);
            edge.A = 0;

            ACMShaders.DrawBeam(start, end, thickness * 0.5f, core, edge, intensity,
                flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.4f);
            return false;
        }
    }
}

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
            Texture2D back = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            int width = 14400;
            int height = (int)(Projectile.localAI[0] * 3);
            if (Projectile.localAI[0] < 0) height = (int)(Math.Abs(Projectile.localAI[0]) * 4f); //前置阶段渐增
            float alpha = Projectile.localAI[1] / 60f;
            Rectangle rect = new Rectangle(-width / 2, -height / 2, width, height);
            Vector2 origin = new Vector2(rect.Width / 2, rect.Height / 2);
            Color drawColor = VaultUtils.MultiStepColorLerp(MathHelper.Clamp(Projectile.localAI[0] / 40f, 0, 1), Color.Azure, Color.Red);
            Main.spriteBatch.Draw(back, drawPos, rect, drawColor with { A = 155 } * alpha,
                Projectile.velocity.ToRotation(), origin, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }
}

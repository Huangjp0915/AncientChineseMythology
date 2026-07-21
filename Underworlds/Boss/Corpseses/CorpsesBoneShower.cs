using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 尸骸骨镖 (V3 重做)。两种弹道 (ai[1]):
    ///   0 = 指骨连环的直线骨镖 (微重力, 出膛 12f 速度 20%→100% 渐升防 telefrag);
    ///   1 = 拍落溅射 / 骨雨的弧线骨屑 (标准重力抛体, 轨迹可读)。
    /// </summary>
    public class CorpsesBoneShower : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Timer => ref Projectile.ai[0];
        private bool IsArc => Projectile.ai[1] == 1f;
        private ref float BaseSpeed => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 210;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 0;
        }

        public override void AI() {
            if (BaseSpeed == 0f)
                BaseSpeed = Projectile.velocity.Length();

            Timer++;

            if (IsArc) {
                // 弧线骨屑: 标准抛体
                Projectile.velocity.Y += 0.3f;
                if (Projectile.velocity.Y > 15f)
                    Projectile.velocity.Y = 15f;
                Projectile.rotation += Projectile.velocity.X * 0.05f;
            }
            else {
                // 直线骨镖: wind-up 渐升 + 微重力, 镖头对齐轨迹
                if (Timer <= 12f) {
                    float ramp = MathHelper.Lerp(0.2f, 1f, Timer / 12f);
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * BaseSpeed * ramp;
                }
                Projectile.velocity.Y += 0.06f;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Bone);
                d.velocity = Projectile.velocity * 0.15f;
                d.scale = 0.9f;
            }

            Lighting.AddLight(Projectile.Center, TelegraphColors.GhostGreen.ToVector3() * 0.25f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.2f }, Projectile.position);
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Bone, Main.rand.NextVector2Circular(3f, 3f));
                    d.scale = Main.rand.NextFloat(0.9f, 1.5f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D texture = ModContent.Request<Texture2D>("Terraria/Images/Projectile_" + ProjectileID.Bone).Value;
            Vector2 origin = texture.Size() / 2f;

            // 鬼绿残迹
            for (int i = 0; i < Projectile.oldPos.Length; i += 2) {
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = (TelegraphColors.GhostGreen with { A = 0 }) * (progress * 0.35f);
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale * 0.9f, SpriteEffects.None);
            }

            // 骨白主体 (受光但保底可读)
            Color mainColor = Color.Lerp(lightColor, new Color(214, 232, 210), 0.65f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }
}

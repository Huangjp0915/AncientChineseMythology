using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙喷射的蓝色幽冥火
    /// </summary>
    internal class NetherFlameProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            // 帧动画
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                if (++Projectile.frame >= Main.projFrames[Type])
                    Projectile.frame = 0;
            }

            // 旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            for (int i = 0; i < 6; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }

            // 发光效果
            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(Color.Blue, Color.Cyan, 0.6f);

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = 0.5f * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, Projectile.scale * 0.8f, SpriteEffects.None, 0f);
            }

            // 主体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor with { A = 200 }, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // 外层发光
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.4f, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 死亡粒子效果
            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 100, Color.Cyan, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }
    }
}

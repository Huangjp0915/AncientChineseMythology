using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Underworlds;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥鬼火 (Nether Flame) —— 幽冥龙吐息锥/暴怒吐息发射的单发鬼绿魂火。
    /// V2: 不再背景常驻喷射, 仅由特定 telegraphed 状态(吐息锥/暴怒)发射; 命中叠 <see cref="UnderworldField"/> 魂蚀。
    /// </summary>
    internal class NetherFlameProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GreenTorch, 0, 0, 110, new Color(110, 230, 150), 1.4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.3f;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.18f, 0.45f, 0.28f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = Underworld.Fog;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() * 0.5f;
            Color core = new Color(180, 255, 210);
            Color glow = new Color(110, 230, 150);
            float baseScale = Projectile.width / (float)tex.Width * 1.4f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = 0.45f * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, null, glow * fade, Projectile.rotation, origin, baseScale * 0.85f,
                    SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, null, glow * 0.5f, Projectile.rotation, origin, baseScale * 1.6f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, core * 0.9f, Projectile.rotation, origin, baseScale, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GreenTorch, 0, 0, 100, new Color(110, 230, 150), 1.7f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }
    }
}

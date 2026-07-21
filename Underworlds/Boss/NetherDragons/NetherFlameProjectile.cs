using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥鬼火 (Nether Flame) —— 吐息锥/暴怒吐息/冲刺刹车环发射的单发鬼绿魂火。
    /// V3: 寿命收敛到 80f (射程与锥形预警区吻合 — 预警说到哪火就到哪), 末段轻微上飘熄灭;
    /// SoftGlow 双层核心 + 短拖尾, 每帧 1 尘 (V2 为 3)。命中叠 <see cref="UnderworldField"/> 魂蚀。
    /// </summary>
    internal class NetherFlameProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 幽火轨迹: 前段直, 30f 后轻微减速上飘 (鬼火的失重感)
            if (Projectile.timeLeft < 50) {
                Projectile.velocity *= 0.985f;
                Projectile.velocity.Y -= 0.035f;
            }

            if (!Main.dedServ) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.GreenTorch, Vector2.Zero, 110, new Color(110, 230, 150), 1.3f);
                d.noGravity = true;
                d.velocity = Projectile.velocity * 0.1f + new Vector2(0, -0.5f);
            }

            Lighting.AddLight(Projectile.Center, 0.18f, 0.45f, 0.28f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D soft = ACMAsset.SoftGlow;
            if (soft == null)
                return false;

            Vector2 origin = soft.Size() * 0.5f;
            Color core = new Color(200, 255, 220, 0);
            Color glow = new Color(110, 230, 150, 0);
            Color violet = new Color(120, 90, 200, 0);
            // 末段淡出 (熄灭而非硬消失)
            float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            float baseScale = Projectile.width / (float)soft.Width * 1.6f;
            float flicker = 1f + MathF.Sin((float)Main.timeForVisualEffects * 0.35f + Projectile.whoAmI) * 0.12f;

            // 短拖尾 (幽紫余烬)
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(soft, pos, null, violet * (0.35f * k * fade), 0f, origin,
                    baseScale * (0.7f * k + 0.2f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(soft, drawPos, null, glow * (0.55f * fade), 0f, origin,
                baseScale * 1.9f * flicker, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(soft, drawPos, null, core * (0.9f * fade), 0f, origin,
                baseScale * flicker, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, Vector2.Zero, 100,
                    new Color(110, 230, 150), 1.6f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(2.6f, 2.6f) + new Vector2(0, -1f);
            }
        }
    }
}

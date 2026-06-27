using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Underworlds;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥火残痕 (Nether-Flame DoT Trail) —— P1 龙身分段沿途留下的可破坏式空间机制。
    ///
    /// 把"被动跟随的蠕虫身段"升格为**机制**: 每隔数节身段在掘墓轨迹上留下一摊驻留鬼绿幽火,
    /// 玩家须读身段间的**可读空隙**穿行 (gapped); 站位其上叠加 <see cref="UnderworldField"/> 魂蚀 DoT。
    /// 出现有 <see cref="ArmTime"/> 的 telegraph 渐显窗口(非致命), 期满才造成伤害 (每招必预告)。
    /// </summary>
    internal class NetherFlameTrail : ModProjectile
    {
        // 复用 sibling 占位贴图 (本类绘制走 Underworld.Fog 灰度纹理, 仅需一个合法自动加载锚点)
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float ArmTimer => ref Projectile.ai[0];
        private const int ArmTime = 30;     // telegraph 渐显(非致命)

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 330;      // ~5.5s 驻留
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            ArmTimer++;

            // 渐显: alpha 由 telegraph 推进
            float armed = MathHelper.Clamp(ArmTimer / ArmTime, 0f, 1f);
            Projectile.alpha = (int)(255 * (1f - armed * 0.75f));

            // 末段淡出
            if (Projectile.timeLeft < 40)
                Projectile.alpha = (int)MathHelper.Lerp(64, 255, 1f - Projectile.timeLeft / 40f);

            if (!Main.dedServ && Main.rand.NextBool(armed > 0.99f ? 2 : 4)) {
                Vector2 dpos = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.4f, Projectile.height * 0.4f);
                int d = Dust.NewDust(dpos, 1, 1, DustID.GreenTorch, 0, 0, 120,
                    armed > 0.99f ? new Color(110, 230, 150) : new Color(120, 90, 200), 1.3f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = new Vector2(0, -Main.rand.NextFloat(0.6f, 1.6f));
            }

            Lighting.AddLight(Projectile.Center, 0.15f, 0.35f, 0.2f);
        }

        // telegraph 期间无伤 (可读空隙穿行, 不会"看不见就掉血")
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => ArmTimer < ArmTime ? false : base.Colliding(projHitbox, targetHitbox);

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 地府身份层: 站在幽火残痕上叠魂蚀 DoT
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = Underworld.Fog;
            if (tex == null)
                return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float armed = MathHelper.Clamp(ArmTimer / ArmTime, 0f, 1f);
            float alpha = (1f - Projectile.alpha / 255f);
            float pulse = 1f + MathF.Sin((float)Main.timeForVisualEffects * 0.08f + Projectile.whoAmI) * 0.12f;

            // telegraph: 紫预备 → 致命驻留: 鬼绿
            Color col = Color.Lerp(new Color(120, 90, 200), new Color(110, 230, 150), armed);
            float baseScale = Projectile.width / (float)tex.Width;

            for (int i = 0; i < 3; i++) {
                float s = baseScale * (1.6f - i * 0.4f) * pulse;
                Main.spriteBatch.Draw(tex, drawPos, null, col * (alpha * (0.18f + i * 0.06f)),
                    Projectile.rotation + i * 0.7f, origin, s, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}

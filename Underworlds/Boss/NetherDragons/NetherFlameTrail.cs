using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥火残痕 (Nether-Flame DoT Trail) —— P1《巡墓》龙身分段沿途留下的驻留幽火。
    ///
    /// 把"被动跟随的蠕虫身段"升格为机制: 每隔数节身段在掘墓轨迹上留一摊驻留鬼绿幽火,
    /// 玩家须读身段间的可读空隙穿行; 站位其上叠 <see cref="UnderworldField"/> 魂蚀 DoT。
    /// 出现有 <see cref="ArmTime"/> telegraph 渐显窗口 (紫, 非致命), 期满转鬼绿才造成伤害。
    /// V3 视觉: SoftGlow 焰堆 (下宽上收的三层焰体) + 上升焰舌尘, 取代 fog 贴图糊团。
    /// </summary>
    internal class NetherFlameTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float ArmTimer => ref Projectile.ai[0];
        private const int ArmTime = 30;     // telegraph 渐显 (非致命)

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

            float armed = MathHelper.Clamp(ArmTimer / ArmTime, 0f, 1f);
            Projectile.alpha = (int)(255 * (1f - armed * 0.75f));

            // 末段淡出
            if (Projectile.timeLeft < 40)
                Projectile.alpha = (int)MathHelper.Lerp(64, 255, 1f - Projectile.timeLeft / 40f);

            if (!Main.dedServ && Main.rand.NextBool(armed > 0.99f ? 3 : 5)) {
                Vector2 dpos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f) * Projectile.width,
                    Main.rand.NextFloat(0.1f, 0.4f) * Projectile.height);
                var d = Dust.NewDustPerfect(dpos, DustID.GreenTorch, Vector2.Zero, 120,
                    armed > 0.99f ? new Color(110, 230, 150) : new Color(120, 90, 200), 1.3f);
                d.noGravity = true;
                d.velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 2f));
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
            Texture2D soft = ACMAsset.SoftGlow;
            if (soft == null)
                return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = soft.Size() * 0.5f;
            float armed = MathHelper.Clamp(ArmTimer / ArmTime, 0f, 1f);
            float alpha = 1f - Projectile.alpha / 255f;
            float t = (float)Main.timeForVisualEffects * 0.08f + Projectile.whoAmI;

            // telegraph 紫预备 → 致命驻留鬼绿
            Color col = Color.Lerp(new Color(120, 90, 200, 0), new Color(110, 230, 150, 0), armed);
            Color core = new Color(200, 255, 220, 0);
            float baseScale = Projectile.width / (float)soft.Width;

            // 三层焰体: 底盘宽晕 + 两簇错相摇曳焰舌 (下宽上收)
            Main.spriteBatch.Draw(soft, drawPos + new Vector2(0, 10f), null, col * (alpha * 0.40f), 0f,
                origin, new Vector2(baseScale * 2.1f, baseScale * 0.9f), SpriteEffects.None, 0f);
            for (int i = 0; i < 2; i++) {
                float sway = MathF.Sin(t * (1.1f + i * 0.4f) + i * 2.2f) * 6f;
                float rise = 8f + i * 10f + MathF.Sin(t * 1.7f + i) * 3f;
                float s = baseScale * (1.15f - i * 0.32f);
                Main.spriteBatch.Draw(soft, drawPos + new Vector2(sway, -rise), null,
                    col * (alpha * (0.45f - i * 0.12f)), 0f, origin,
                    new Vector2(s * 0.8f, s * 1.25f), SpriteEffects.None, 0f);
            }
            // 白热芯 (仅致命态)
            if (armed > 0.9f)
                Main.spriteBatch.Draw(soft, drawPos - new Vector2(0, 4f), null, core * (alpha * 0.30f), 0f,
                    origin, baseScale * 0.5f, SpriteEffects.None, 0f);

            return false;
        }
    }
}

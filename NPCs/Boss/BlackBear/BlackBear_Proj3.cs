using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精·金冠光环 (V3) — P2 复发机制: 6 枚头饰从 <b>Boss 头顶飞行抵达</b>玩家周围环位
    /// (traveled 而非瞬移, 修复 V2 凭空出现), 环绕留缝 → 变红收拢预警 → 向内致命扫拢 → 消散。
    /// 全程伤害常开但入场路径可见 + 环绕期有明确空缝, 公平性由"看得见的抵达"保证。
    /// ai[0] = 环位基准角; ai[1] = 计时 (自增); ai[2] = 目标玩家索引。
    /// </summary>
    public class BlackBear_Proj3 : ModProjectile
    {
        private const int FlyInTicks = 30;    // 飞行抵达
        private const int OrbitTicks = 150;   // 环绕 (含 20f 变红预警尾段)
        private const int CollapseTicks = 42; // 收拢
        private const int WarnTicks = 20;     // 收拢前变红
        private const float OrbitRadius = 250f;
        private const float CollapseRadius = 24f;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Proj3";

        private ref float BaseAngle => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float TargetIdx => ref Projectile.ai[2];

        private Player Target => Main.player[(int)MathHelper.Clamp(TargetIdx, 0, Main.maxPlayers - 1)];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.width = 44;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FlyInTicks + OrbitTicks + CollapseTicks + 10;
            Projectile.alpha = 160;
            Projectile.light = 0.3f;
        }

        public override void AI() {
            Player target = Target;
            if (target == null || !target.active || target.dead) {
                Burst();
                Projectile.Kill();
                return;
            }

            // 首帧捕获基础伤害 (localAI: 各端在本地第 0 帧捕获生成包伤害, 确定一致);
            // 飞行抵达期 + 环绕成形前 10f 无伤害 — 入场路径不打人
            if (Timer == 0f)
                Projectile.localAI[0] = Projectile.damage;
            Timer++;
            Projectile.damage = Timer < FlyInTicks + 10 ? 0 : (int)Projectile.localAI[0];

            if (Projectile.alpha > 0)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 10);

            // 自转角由 Timer 推导 (各端确定, 不用 GlobalTime 避免跨端漂移)
            float ang = BaseAngle + Timer * 0.0225f;
            Projectile.rotation += 0.14f;

            if (Timer <= FlyInTicks) {
                // 飞行抵达: 从生成点 (Boss 头顶) 平滑奔向环位 — 玩家能看见"金冠飞来了"
                Vector2 slot = target.Center + ang.ToRotationVector2() * OrbitRadius;
                float t = Timer / FlyInTicks;
                Projectile.Center = Vector2.Lerp(Projectile.Center, slot, 0.10f + 0.22f * t * t);
                return;
            }

            float sinceOrbit = Timer - FlyInTicks;
            bool collapsing = sinceOrbit >= OrbitTicks;

            float radius;
            if (!collapsing) {
                radius = OrbitRadius;
            }
            else {
                float t = MathHelper.Clamp((sinceOrbit - OrbitTicks) / CollapseTicks, 0f, 1f);
                // 收拢曲线: t³ — 慢启动给反应时间, 末段猛收
                radius = MathHelper.Lerp(OrbitRadius, CollapseRadius, t * t * t);
                if (sinceOrbit >= OrbitTicks + CollapseTicks) {
                    Burst();
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.Center = target.Center + ang.ToRotationVector2() * radius;

            // 收拢预警/收拢期: 红芒内吸 dust
            bool warning = sinceOrbit >= OrbitTicks - WarnTicks;
            if (!Main.dedServ && warning && (int)Timer % 4 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 100, Color.OrangeRed, 1.1f);
                d.noGravity = true;
                d.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        private void Burst() {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Gold, Main.rand.NextVector2Circular(2.5f, 2.5f), 100);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            int frames = Math.Max(1, Main.projFrames[Type]);
            int fh = texture.Height / frames;
            Rectangle rect = new(0, fh * Projectile.frame, texture.Width, fh);
            Vector2 origin = new(texture.Width / 2f, fh / 2f);

            float sinceOrbit = Timer - FlyInTicks;
            bool warning = sinceOrbit >= OrbitTicks - WarnTicks;
            Color tint = warning
                ? Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, 0.65f)
                : TelegraphColors.Gold;
            tint *= Projectile.Opacity;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float factor = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, old, rect, tint * (factor * 0.45f), Projectile.oldRot[i], origin,
                    Projectile.scale * 0.8f, SpriteEffects.None, 0);
            }

            // 底层金晕
            Texture2D soft = ACMAsset.SoftGlow;
            if (soft != null) {
                Color halo = (warning ? TelegraphColors.Lethal : TelegraphColors.Gold) * (0.4f * Projectile.Opacity);
                halo.A = 0;
                Main.spriteBatch.Draw(soft, Projectile.Center - Main.screenPosition, null, halo, 0f,
                    soft.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rect, tint,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

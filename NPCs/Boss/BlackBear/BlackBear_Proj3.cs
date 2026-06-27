using Microsoft.Xna.Framework;
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
    /// 黑熊精头饰光环 (V2 改造)。原版是"半血一次性发射头饰弹", V2 改为 P2 <b>复发</b>机制:
    /// 由 Boss 在玩家周围生成一圈 (默认 6 颗), 环绕玩家 3s (含间隙, 可穿缝) → 向内收拢 (致命扫拢, 逼玩家离环) → 消散。
    /// 教学意图: 让新手第一次体会"换阶段=出现新机制", 而非单纯换贴图。
    /// owner = 目标玩家索引; ai[0] = 起始角; ai[1] = 计时。
    /// </summary>
    public class BlackBear_Proj3 : ModProjectile
    {
        private const int OrbitTicks = 180;   // 环绕 3s
        private const int CollapseTicks = 45; // 收拢
        private const float OrbitRadius = 240f;
        private const float CollapseRadius = 28f;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Proj3";

        public override void SetDefaults() {
            Projectile.hostile = true;
            Projectile.width = 56;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OrbitTicks + CollapseTicks + 20;
            Projectile.alpha = 255; // 淡入
            Projectile.light = 0.3f;
        }

        private ref float BaseAngle => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float BaseDamage => ref Projectile.ai[2];

        private Player Target => Main.player[(int)MathHelper.Clamp(Projectile.owner, 0, Main.maxPlayers - 1)];

        public override void AI() {
            Player target = Target;
            if (target == null || !target.active || target.dead) {
                Burst();
                Projectile.Kill();
                return;
            }

            // 首帧捕获基础伤害 (随生成同步), 之后据阶段开关致命性
            if (Timer == 0f)
                BaseDamage = Projectile.damage;
            Timer++;

            // 淡入
            if (Projectile.alpha > 0)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 12);

            float radius;
            bool collapsing = Timer >= OrbitTicks;
            float orbitSpin = (float)Main.GlobalTimeWrappedHourly * 1.2f;

            // 仅在成形后 (>30 帧) 或收拢期才致命; 淡入期安全
            Projectile.damage = (collapsing || Timer >= 30f) ? (int)BaseDamage : 0;

            if (!collapsing) {
                radius = OrbitRadius;
            }
            else {
                // 收拢期: 半径向内 lerp, 致命扫拢
                float t = MathHelper.Clamp((Timer - OrbitTicks) / (float)CollapseTicks, 0f, 1f);
                radius = MathHelper.Lerp(OrbitRadius, CollapseRadius, t);
                if (Timer >= OrbitTicks + CollapseTicks) {
                    Burst();
                    Projectile.Kill();
                    return;
                }
            }

            float ang = BaseAngle + orbitSpin;
            Vector2 desired = target.Center + ang.ToRotationVector2() * radius;
            Projectile.Center = desired;
            Projectile.rotation += 0.15f;

            // 收拢预警 dust (红芒脉冲)
            if (!Main.dedServ && collapsing && Timer % 4 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 100, Color.OrangeRed, 1.1f);
                d.noGravity = true;
                d.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f;
            }
        }

        private void Burst() {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(10f, 10f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Gold, Main.rand.NextVector2Circular(2f, 2f), 100);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            int frames = Math.Max(1, Main.projFrames[Type]);
            int fh = texture.Height / frames;
            Rectangle rectangle = new Rectangle(0, fh * Projectile.frame, texture.Width, fh);
            Vector2 origin = new Vector2(texture.Width / 2f, fh / 2f);

            bool collapsing = Timer >= OrbitTicks;
            // 收拢期叠红警示色, 环绕期金色
            Color tint = collapsing
                ? Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, 0.6f)
                : TelegraphColors.Gold;
            tint *= Projectile.Opacity;

            // 拖尾
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float factor = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, old, rectangle, tint * factor * 0.5f, Projectile.oldRot[i], origin, Projectile.scale * 0.8f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, tint,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 狐火弹 — 替换原版占位弹 (CultistBossFireBall/Clone)。
    /// 设计要点 (可读性): 先缓慢漂浮蓄势(金色, 预告), 后追踪加速(转橙红, 致命)。慢起 → 渐快 → 收口。
    /// 纯服务器生成 + 同步; 绘制纯本地 (狐火金橙双层 ribbon + 尾尖柔光)。
    /// ai[0]=自计时; ai[1]=追踪强度 0~1 (0=直线狐火, >0=慢起后追踪)。
    /// </summary>
    public class KyuubiFoxFire : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesTop";

        /// <summary>缓慢漂浮(预告)时长 tick。</summary>
        private const int DriftTime = 32;
        private const float MaxSpeed = 13f;

        private ref float Timer => ref Projectile.ai[0];
        private float HomeStrength => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Timer++;

            if (Timer < DriftTime) {
                // 漂浮蓄势: 缓慢减速到将停, 金色脉动 (慢=可读)
                Projectile.velocity *= 0.95f;
            }
            else if (HomeStrength > 0f) {
                Player target = FindTarget();
                if (target != null) {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    float t = MathHelper.Clamp((Timer - DriftTime) / 45f, 0f, 1f);
                    float spd = MathHelper.Lerp(2.5f, MaxSpeed, t);
                    Vector2 cur = Projectile.velocity.SafeNormalize(desired);
                    Vector2 dir = Vector2.Lerp(cur, desired, 0.05f + 0.05f * HomeStrength).SafeNormalize(desired);
                    Projectile.velocity = dir * spd;
                }
            }
            else {
                // 直线狐火: 漂浮后重新加速沿原方向
                float t = MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * MathHelper.Lerp(2.5f, MaxSpeed, t);
            }

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            // 狐火光照: 漂浮期偏金, 追踪期转暖橙
            float hot = MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.6f - hot * 0.25f, 0.25f) * 0.6f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    -Projectile.velocity * 0.1f, 120, default, 1.2f);
                d.noGravity = true;
            }
        }

        private Player FindTarget() {
            Player best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                float sq = Vector2.DistanceSquared(p.Center, Projectile.Center);
                if (sq < bestSq) {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 慢起金色 → 追踪暖橙(致命)的可读配色
            float hot = MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
            Color outer = Color.Lerp(new Color(220, 150, 40), new Color(210, 70, 20), hot);
            Color inner = Color.Lerp(new Color(255, 225, 140), new Color(255, 150, 70), hot);
            outer.A = 150;
            inner.A = 200;

            WeaponVFX.DrawProjectileTrail(Projectile, 16f, outer, inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.55f + 0.12f * MathF.Sin(Timer * 0.3f), inner);

            return false;
        }
    }

    /// <summary>
    /// 狐火曼陀罗的一条边墙 (P2 招牌 set-piece)。九条边围成绕玩家旋转的九边形, 缺口边为安全缝。
    /// 全部状态(中心/半径/旋转/缺口/伤害窗口)由本体 <see cref="KyuubiKitsune"/> 权威驱动, 边墙仅读取并自绘/判定。
    /// ai[0]=本体 whoAmI; ai[1]=边索引 0~8。
    /// </summary>
    public class KyuubiMandalaEdge : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesBody";

        private int EdgeIndex => (int)Projectile.ai[1];
        private Vector2 v1, v2;
        private bool isGap;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 5;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        private KyuubiKitsune Boss {
            get {
                int who = (int)Projectile.ai[0];
                if (who < 0 || who >= Main.maxNPCs)
                    return null;
                NPC n = Main.npc[who];
                if (!n.active || n.ModNPC is not KyuubiKitsune k || !k.InMandala)
                    return null;
                return k;
            }
        }

        public override void AI() {
            KyuubiKitsune boss = Boss;
            if (boss == null) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 5; // 由本体续命

            Vector2 center = boss.MandalaCenter;
            float radius = boss.MandalaRadius;
            float rot = boss.MandalaRotation;
            int gap = boss.MandalaGapIndex;

            float a1 = rot + MathHelper.TwoPi * EdgeIndex / 9f;
            float a2 = rot + MathHelper.TwoPi * (EdgeIndex + 1) / 9f;
            v1 = center + a1.ToRotationVector2() * radius;
            v2 = center + a2.ToRotationVector2() * radius;
            Projectile.Center = (v1 + v2) * 0.5f;

            isGap = EdgeIndex == gap;
        }

        public override bool? CanDamage() {
            KyuubiKitsune boss = Boss;
            if (boss == null || !boss.MandalaDamaging || isGap)
                return false;
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (isGap)
                return false;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(), v1, v2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            KyuubiKitsune boss = Boss;
            if (boss == null)
                return false;

            float alpha = boss.MandalaEdgeAlpha;
            if (alpha <= 0.01f)
                return false;

            if (!boss.MandalaDamaging) {
                // 预告窗口: 红色细线 (致命预警语言), 缺口边用安全翠玉
                Color tc = isGap ? TelegraphColors.Safe : TelegraphColors.Lethal;
                ACMShaders.DrawBeam(v1, v2, 3f + 2f * alpha, tc, tc * 0.4f, alpha * 0.9f,
                    flowSpeed: 2.4f, coreSharp: 3f);
            }
            else if (isGap) {
                // 安全缝: 微弱翠玉指示, 不挡视线
                ACMShaders.DrawBeam(v1, v2, 4f, TelegraphColors.Safe, TelegraphColors.Safe * 0.3f, alpha * 0.35f);
            }
            else {
                // 实墙: 狐火金橙(致命) 流动光束
                Color core = new(255, 200, 110);
                Color edge = new(210, 70, 20);
                ACMShaders.DrawBeam(v1, v2, 11f, core, edge, alpha,
                    flowSpeed: 1.8f, flowScale: 2.4f, coreSharp: 2.0f);
            }
            return false;
        }
    }
}

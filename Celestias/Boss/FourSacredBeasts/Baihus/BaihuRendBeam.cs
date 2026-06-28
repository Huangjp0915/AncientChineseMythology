using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎·裂地银脉 Rend Beam —— 沿固定方向的撕裂光束，用于「裂地灭世爪」的平行爪痕与三阶段爪裂射线。
    /// 两段式可读结构（§6.1）：先 <b>预告(红，非致命)</b> 渐亮 telegraph，再 <b>释放(银白，致命)</b>；
    /// 视觉走硬化 <see cref="ACMShaders.DrawBeam"/>（缺着色器自动降级 no-op）。固定不动、方向锁定，命中可读靠站位。
    /// </summary>
    public class BaihuRendBeam : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        /// <summary>光束长度(世界像素)。</summary>
        public ref float Length => ref Projectile.ai[0];
        /// <summary>预告时长(tick)，期间不致命。</summary>
        public ref float TelegraphTime => ref Projectile.ai[1];
        /// <summary>本地年龄计数(各客户端独立自增，确定性)。</summary>
        private ref float Age => ref Projectile.localAI[1];

        private float Dir => Projectile.rotation;
        private Vector2 DirVec => Dir.ToRotationVector2();
        private bool Active => Age >= TelegraphTime;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Age++;

            if (!Active) {
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    float t = Main.rand.NextFloat();
                    Vector2 p = Projectile.Center + DirVec * (Length * t);
                    Dust d = Dust.NewDustPerfect(p, DustID.RedTorch, Vector2.Zero, 150, default, 0.8f);
                    d.noGravity = true;
                }
            }
            else {
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    float t = Main.rand.NextFloat();
                    Vector2 p = Projectile.Center + DirVec * (Length * t);
                    Dust d = Dust.NewDustPerfect(p, DustID.Silver, DirVec.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-2f, 2f), 80, default, 1.1f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center + DirVec * Length * 0.5f, 0.5f, 0.55f, 0.6f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Active)
                return false;
            Vector2 a = Projectile.Center;
            Vector2 b = Projectile.Center + DirVec * Length;
            float dist = DistanceToSegment(targetHitbox.Center.ToVector2(), a, b);
            float half = 26f + Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return dist <= half;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 < 1f ? 0f : MathHelper.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + DirVec * Length;

            if (!Active) {
                float teleProg = TelegraphTime > 0 ? MathHelper.Clamp(Age / TelegraphTime, 0f, 1f) : 1f;
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f);
                ACMShaders.DrawBeam(start, end, 4f + 4f * teleProg,
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f,
                    (0.3f + 0.45f * teleProg) * pulse, flowSpeed: 0.8f, flowScale: 3f, coreSharp: 3f);
            }
            else {
                float lifeProg = MathHelper.Clamp(Projectile.timeLeft / 22f, 0f, 1f); // 末段淡出
                float intensity = MathHelper.Clamp(lifeProg, 0.2f, 1f);
                ACMShaders.DrawBeam(start, end, 22f * (0.6f + 0.4f * intensity),
                    Color.White, TelegraphColors.WhiteTiger, intensity,
                    flowSpeed: 1.8f, flowScale: 2f, coreSharp: 2.2f, coreGlow: 1.2f);
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            BaihuClawMark.Apply(target);
        }

        /// <summary>服务端权威生成一道裂脉。dirAngle=方向(弧度)。</summary>
        public static void Spawn(IEntitySource src, Vector2 start, float dirAngle, float length,
            int telegraph, int active, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int p = Projectile.NewProjectile(src, start, Vector2.Zero,
                ModContent.ProjectileType<BaihuRendBeam>(), damage, 4f, Main.myPlayer,
                ai0: length, ai1: telegraph);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].rotation = dirAngle;
                Main.projectile[p].timeLeft = telegraph + active;
                Main.projectile[p].netUpdate = true;
            }
        }
    }
}

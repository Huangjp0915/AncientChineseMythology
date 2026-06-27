using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥魂束 (Nether Soul Beam) —— P2《裂土》尾鞭横扫的单次 telegraphed 激光。
    ///
    /// V2: 不再是常驻喷射流, 而是传送门出口处尾鞭甩出的**一道**有预告激光:
    ///   ● 起手 <see cref="WindupTime"/> 为细红 telegraph 线(非致命, §6.1 红=致命路径预告)。
    ///   ● 期满展开为鬼绿魂束(致命), 命中叠 <see cref="UnderworldField"/> 魂蚀。
    /// 绘制走共享 <see cref="ACMShaders.DrawBeam"/> 原语(BeamGrad), 取代旧手抄 fog 贴图分段。
    /// </summary>
    internal class NetherLaserBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float LaserDirection => ref Projectile.ai[0];
        private ref float LaserTimer => ref Projectile.ai[1];
        private ref float MaxLength => ref Projectile.ai[2];

        private float currentLength = 0f;
        private const float TargetLength = 1600f;
        private const float BeamHalfWidth = 26f;
        private const int WindupTime = 26;     // 红色 telegraph 渐强(非致命)

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        private bool Armed => LaserTimer >= WindupTime;

        public override void AI() {
            LaserTimer++;

            if (!Armed) {
                // telegraph 期: 细线预告, 不伸展致命长度
                currentLength = TargetLength;
                if (LaserTimer == 1f)
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
            }
            else {
                int sinceArm = (int)(LaserTimer - WindupTime);
                if (sinceArm < 8)
                    currentLength = MathHelper.Lerp(0f, TargetLength, sinceArm / 8f);
                else if (Projectile.timeLeft < 18)
                    currentLength = MathHelper.Lerp(TargetLength, 0f, 1f - Projectile.timeLeft / 18f);
                else
                    currentLength = TargetLength;

                if (sinceArm == 0)
                    SoundEngine.PlaySound(SoundID.Item33, Projectile.Center);
            }

            MaxLength = GetLaserLength();

            // 轻微摆动 (尾鞭横扫感)
            LaserDirection += MathF.Sin(LaserTimer * 0.08f) * 0.0025f;

            if (Armed && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 laserEnd = Projectile.Center + LaserDirection.ToRotationVector2() * MaxLength;
                Vector2 dustPos = Vector2.Lerp(Projectile.Center, laserEnd, Main.rand.NextFloat(0.1f, 1f));
                int dust = Dust.NewDust(dustPos, 1, 1, DustID.GreenTorch, 0, 0, 110, new Color(110, 230, 150), 1.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.3f);
        }

        private float GetLaserLength() {
            float length = 50f;
            Vector2 direction = LaserDirection.ToRotationVector2();
            while (length <= currentLength) {
                Vector2 testPoint = Projectile.Center + direction * length;
                if (!Collision.CanHit(Projectile.Center, 1, 1, testPoint, 1, 1))
                    return length - 20f;
                length += 20f;
            }
            return currentLength;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Armed)
                return false; // telegraph 期无伤
            Vector2 start = Projectile.Center;
            Vector2 end = start + LaserDirection.ToRotationVector2() * MaxLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, BeamHalfWidth, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 start = Projectile.Center;
            Vector2 dir = LaserDirection.ToRotationVector2();
            Vector2 end = start + dir * MaxLength;

            if (!Armed) {
                // 红色致命路径预告 (细线渐强)
                float t = MathHelper.Clamp(LaserTimer / WindupTime, 0f, 1f);
                ACMShaders.DrawBeam(start, end, 2.5f + t * 2.5f,
                    TelegraphColors.Lethal, TelegraphColors.Lethal with { A = 0 }, 0.45f + t * 0.4f,
                    flowSpeed: 2.2f, flowScale: 3f, coreSharp: 3f);
            }
            else {
                float lenFrac = MaxLength / TargetLength;
                // 致命鬼绿魂束 (核心亮 + 外晕)
                ACMShaders.DrawBeam(start, start + dir * MaxLength, BeamHalfWidth,
                    new Color(180, 255, 210), new Color(110, 230, 150) with { A = 0 }, lenFrac,
                    flowSpeed: 1.8f, flowScale: 2.4f, coreSharp: 2.2f, coreGlow: 1.2f);
            }
            return false;
        }
    }
}

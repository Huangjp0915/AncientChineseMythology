using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥魂束 (Nether Soul Beam) —— V3 扫射魂束。
    ///
    /// 遵守扫射预警线规范: 预警期以锥形 shader (头部挂载时) + 两界红线画出**全部扫掠扇区**,
    /// 起始线最亮 (束将从此点燃); 期满自起始角以**恒定角速度**扫过扇区 (可预跑), 全程无变速。
    ///
    /// 参数 (全在 ai[] 内, 各端确定性推进, 零额外同步):
    ///   ai[0] = 起始角 (rad);
    ///   ai[1] = 带符号扫掠弧 (rad, 符号=方向; |弧|&lt;1 → 短预警 50f, 否则 75f);
    ///   ai[2] = 挂载头索引 (&lt;0 = 静止炮口, 万魂门的门口束)。
    /// 命中叠 <see cref="UnderworldField"/> 魂蚀。绘制走共享 <see cref="ACMShaders.DrawBeam"/>。
    /// </summary>
    internal class NetherLaserBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float StartAngle => ref Projectile.ai[0];
        private ref float SignedArc => ref Projectile.ai[1];
        private ref float HeadIndex => ref Projectile.ai[2];

        private float timer;
        private float currentLength;

        private const float TargetLength = 1500f;
        private const float BeamHalfWidth = 22f;
        private const float SweepRate = 0.0155f;   // rad/f 恒速 (≈80°/90f)
        private const int FadeTime = 16;

        private int WindupTime => MathF.Abs(SignedArc) < 1f ? 50 : 75;
        private int SweepTime => Math.Max(20, (int)(MathF.Abs(SignedArc) / SweepRate));
        private bool Armed => timer >= WindupTime;
        private bool AttachedToHead => HeadIndex >= 0;

        /// <summary>当前束角: 预警期停在起始角; 扫射期恒速推进。</summary>
        private float CurrentAngle {
            get {
                if (!Armed)
                    return StartAngle;
                float p = MathHelper.Clamp((timer - WindupTime) / SweepTime, 0f, 1f);
                return StartAngle + SignedArc * p;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 75 + 130;   // 上限; 实际按 windup+sweep+fade 提前 Kill
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            timer++;

            // 挂载头: 炮口跟随龙口
            if (AttachedToHead) {
                int idx = (int)HeadIndex;
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active &&
                    Main.npc[idx].ModNPC is NetherDragonHead) {
                    NPC head = Main.npc[idx];
                    Projectile.Center = head.Center + CurrentAngle.ToRotationVector2() * 46f;
                }
                else if (Armed) {
                    // 头没了 → 立即进入淡出
                    timer = MathF.Max(timer, WindupTime + SweepTime);
                }
            }

            if ((int)timer == 1)
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);

            if ((int)timer == WindupTime) {
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 1.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
                ACMUtils.AddScreenShake(4f);
            }

            // 束长: 点燃 8f 展开; 收尾 FadeTime 缩回
            int total = WindupTime + SweepTime + FadeTime;
            if (!Armed)
                currentLength = TargetLength;
            else if (timer < WindupTime + 8)
                currentLength = MathHelper.Lerp(0f, TargetLength, (timer - WindupTime) / 8f);
            else if (timer > total - FadeTime)
                currentLength = MathHelper.Lerp(TargetLength, 0f, (timer - (total - FadeTime)) / FadeTime);
            else
                currentLength = TargetLength;

            if (timer >= total) {
                Projectile.Kill();
                return;
            }

            // 头部挂载时把整个扫掠扇区推给锥形预警 (slot 按扫向分流 — P3 双向剪刀各占一槽)
            if (!Main.dedServ && AttachedToHead && !Armed) {
                float progress = timer / WindupTime;
                int slot = SignedArc >= 0f ? 0 : 1;
                NetherDragonScreenSystem.PublishCone(slot, Projectile.Center,
                    StartAngle + SignedArc * 0.5f, MathF.Abs(SignedArc) * 0.5f + 0.05f,
                    TargetLength * 0.6f, progress, 0.5f + progress * 0.5f);
            }

            // 扫射期末端火花
            if (Armed && !Main.dedServ && Main.rand.NextBool(2)) {
                float len = GetOccludedLength();
                Vector2 tip = Projectile.Center + CurrentAngle.ToRotationVector2() * len;
                Vector2 dustPos = Vector2.Lerp(Projectile.Center, tip, Main.rand.NextFloat(0.15f, 1f));
                var d = Dust.NewDustPerfect(dustPos, DustID.GreenTorch, Vector2.Zero, 110,
                    new Color(110, 230, 150), 1.2f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.3f);
        }

        private float GetOccludedLength() {
            float length = 50f;
            Vector2 direction = CurrentAngle.ToRotationVector2();
            while (length <= currentLength) {
                Vector2 testPoint = Projectile.Center + direction * length;
                if (!Collision.CanHit(Projectile.Center, 1, 1, testPoint, 1, 1))
                    return length - 20f;
                length += 40f;
            }
            return currentLength;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Armed)
                return false; // 预警期无伤 (伤害窗口与视觉严格对齐)
            Vector2 start = Projectile.Center;
            Vector2 end = start + CurrentAngle.ToRotationVector2() * GetOccludedLength();
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamHalfWidth, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 start = Projectile.Center;

            if (!Armed) {
                float t = MathHelper.Clamp(timer / WindupTime, 0f, 1f);
                // 起始线 (束将从此点燃, 最亮红)
                ACMShaders.DrawBeam(start, start + StartAngle.ToRotationVector2() * TargetLength,
                    2.5f + t * 2.5f, TelegraphColors.Lethal, TelegraphColors.Lethal with { A = 0 },
                    0.45f + t * 0.4f, flowSpeed: 2.2f, flowScale: 3f, coreSharp: 3f);
                // 终止线 (扇区另一界, 弱红) — 静止炮口 (万魂门) 没有锥形 shader, 靠双界线读扇区
                float endA = StartAngle + SignedArc;
                ACMShaders.DrawBeam(start, start + endA.ToRotationVector2() * TargetLength * 0.8f,
                    2f, TelegraphColors.Lethal, TelegraphColors.Lethal with { A = 0 },
                    (0.18f + t * 0.2f) * (AttachedToHead ? 0.6f : 1f),
                    flowSpeed: 2.2f, flowScale: 3f, coreSharp: 3f);
            }
            else {
                float len = GetOccludedLength();
                Vector2 dir = CurrentAngle.ToRotationVector2();
                float lenFrac = MathHelper.Clamp(len / TargetLength, 0f, 1f);
                // 致命鬼绿魂束 (核心亮 + 外晕)
                ACMShaders.DrawBeam(start, start + dir * len, BeamHalfWidth,
                    new Color(180, 255, 210), new Color(110, 230, 150) with { A = 0 }, lenFrac,
                    flowSpeed: 1.8f, flowScale: 2.4f, coreSharp: 2.2f, coreGlow: 1.2f);
            }
            return false;
        }
    }
}

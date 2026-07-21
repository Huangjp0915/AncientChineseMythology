using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒幽冥龙 - 魂焰舌 (V3)
    /// 单枚可读的冥火舌: 鬼绿焰芯 + 觉醒紫焰缘。
    /// 火舌主体经 <see cref="AwakeningNetherScreenSystem.RequestSoulflame"/> 专属着色器批量绘制,
    /// 命中判定处另画一枚紧凑亮核 (伤害窗口与视觉对齐)。
    /// </summary>
    public class AwakeningNetherBreath : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float intensity;

        // 是否为狂暴版本
        private bool IsEnraged => Projectile.ai[0] > 0;

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 110;
            Projectile.alpha = 60;
        }

        public override void AI() {
            intensity = MathHelper.Lerp(intensity, 1f, 0.08f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 加速 — 火舌越飞越急
            float maxSpeed = IsEnraged ? 21f : 17f;
            if (Projectile.velocity.Length() < maxSpeed)
                Projectile.velocity *= 1.02f;

            // 末段收焰淡出
            if (Projectile.timeLeft < 20)
                intensity = Projectile.timeLeft / 20f;

            // 火舌主体 (着色器批量队列, 常数批次开销)
            if (!Main.dedServ) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                float size = (IsEnraged ? 190f : 150f) * (0.6f + intensity * 0.4f);
                AwakeningNetherScreenSystem.RequestSoulflame(
                    Projectile.Center - dir * size * 0.18f, dir, size,
                    0.85f * intensity, Projectile.whoAmI * 0.173f, 0f,
                    TelegraphColors.GhostGreen, AwakeningNetherHelper.AwakeningPurple);

                // 稀疏余焰
                if (Main.rand.NextBool(3)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextBool() ? DustID.CursedTorch : DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.2f * intensity;
                    d.velocity = -Projectile.velocity * 0.1f;
                    d.alpha = 80;
                }
            }

            Lighting.AddLight(Projectile.Center, TelegraphColors.GhostGreen.ToVector3() * 0.5f * intensity);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            // 命中核: 紧凑亮核标示真实 hitbox (火舌大身段只是辉光)
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = glow.Size() / 2f;
            float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI) * 0.12f;

            Color outer = AwakeningNetherHelper.AwakeningPurple with { A = 0 };
            Color core = TelegraphColors.GhostGreen with { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, outer * (0.55f * intensity),
                Projectile.rotation, origin, 1.5f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * (0.85f * intensity),
                Projectile.rotation, origin, 0.9f * pulse, SpriteEffects.None, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, Color.White with { A = 0 } * (0.5f * intensity),
                Projectile.rotation, origin, 0.42f * pulse, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 虚空灼烧 + 魂蚀
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(IsEnraged ? 4 : 3);
            target.AddBuff(BuffID.ShadowFlame, 240);
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.8f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.CursedTorch : DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }
    }

    /// <summary>
    /// 觉醒幽冥龙 - 次元裂隙之门 (V3)
    /// 第二幕「次元裂隙」核心机制: 成对传送门, 吸积盘视觉走 VoidRift 专属着色器。
    /// ai[0]=体型档 ai[1]=1 表示出口门(危险门, 吸积辉光转致命红)。
    /// 只有完全开启后「门口」小范围造成伤害; 龙固定入A出B, 玩家离开连线即安全。
    /// </summary>
    public class AwakeningNetherRift : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowOrb;

        public const int OpenTime = 45;   // 预告开启
        public const int CloseTime = 45;  // 关闭动画

        private float riftScale;
        private bool isClosing;

        // ai[0] = 门的大小等级; ai[1] = 出口门(致命红)标记
        private int SizeLevel => (int)Projectile.ai[0];
        private bool IsExitGate => Projectile.ai[1] > 0f;
        private float Timer => Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 330;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            float targetScale = 1f + SizeLevel * 0.25f;
            if (Projectile.timeLeft < CloseTime) {
                isClosing = true;
                targetScale = 0f;
            }
            float openLerp = isClosing ? 0.1f : (Timer < OpenTime ? 0.12f : 0.05f);
            riftScale = MathHelper.Lerp(riftScale, targetScale, openLerp);

            if (isClosing && riftScale < 0.08f) {
                Projectile.Kill();
                return;
            }

            // 吸积盘 decal (批量队列): 出口门带致命红混合
            if (!Main.dedServ) {
                float progress = MathHelper.Clamp(riftScale / Math.Max(targetScale, 0.6f), 0f, 1f);
                if (isClosing)
                    progress = MathHelper.Clamp(riftScale, 0f, 1f);
                AwakeningNetherScreenSystem.RequestVoidRift(Projectile.Center,
                    340f * (1f + SizeLevel * 0.25f), progress,
                    Projectile.whoAmI * 0.53f + (IsExitGate ? 2.4f : 0f),
                    IsExitGate ? 0.55f : 0f, 0.95f,
                    AwakeningNetherHelper.AwakeningPurple,
                    IsExitGate ? TelegraphColors.Lethal : TelegraphColors.GhostGreen);

                // 吸入尘粒
                if (riftScale > 0.4f && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 120f * riftScale + Main.rand.NextFloat(40f);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                    Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = toCenter * 6f + new Vector2(-toCenter.Y, toCenter.X) * 3f;
                    d.alpha = 80;
                }
            }

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.VoidDarkPurple.ToVector3() * 0.8f * riftScale);
        }

        // 只有完全开启后、门口才会判定伤害 (可读的安全区设计)
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < OpenTime || isClosing)
                return false;
            float mouth = 58f * riftScale;
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < mouth;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(3);
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, Projectile.Center);
        }

        // 视觉全部由 VoidRift decal 队列承担
        public override bool PreDraw(ref Color lightColor) => false;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);
            for (int i = 0; i < 20; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? DustID.CursedTorch : DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.6f;
                d.velocity = Main.rand.NextVector2Circular(7f, 7f);
            }
        }
    }
}

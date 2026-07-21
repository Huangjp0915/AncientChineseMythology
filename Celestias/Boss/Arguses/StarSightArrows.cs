using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 星芒追踪箭 — 天目的箭矢 (速度按 px/f 设计; 贯穿箭在 AI 内确定性升级 extraUpdates=1 → 实效 2×)。
    /// 公平阀门: 生成后 12f 幽灵态淡入无伤害 — 杜绝出膛帧贴脸。
    ///
    /// ai[0] 模式:
    ///   0 = 直射 — 沿初速直线飞行。
    ///   1 = 悬停箭 — 初速仅编码方向 (|v|≈0.011); 原地悬停 ai[1] tick (亮线沿冻结方向渐红)
    ///       → 沿该方向以 ai[2] 速度射出。弹道生成瞬间即冻结, 绝不追身。
    ///   2 = 贯穿箭 — 狙击/审判的处决重箭: 无限穿透, 加长白热光矢 + 重拖尾。
    /// </summary>
    public class StarSightArrows : ModProjectile
    {
        private const int FadeInTicks = 12; // 幽灵态时长 (更新数)

        private ref float Mode => ref Projectile.ai[0];
        private ref float HoverTicks => ref Projectile.ai[1];
        private ref float LaunchSpeed => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        private float trailPhase;
        private float storedAngle;   // 悬停箭的冻结方向
        private bool launched;       // 悬停箭是否已射出

        private bool IsHover => Mode == 1f;
        private bool IsPierce => Mode == 2f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            // 悬停箭可能布设在屏外高空 → 出屏也要绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        // 幽灵态/悬停期无伤害 — 伤害窗口与满亮视觉严格对齐
        public override bool? CanDamage() {
            if (Age < FadeInTicks)
                return false;
            if (IsHover && (!launched || Age < HoverTicks + 6f))
                return false;
            return null;
        }

        public override void AI() {
            Age++;
            trailPhase += 0.12f;

            if (IsHover) {
                // 首次更新: 从初速取出冻结方向
                if (Age <= 1f) {
                    storedAngle = Projectile.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
                    Projectile.velocity = Vector2.Zero;
                }

                if (!launched) {
                    Projectile.rotation = storedAngle;
                    // 悬停期不留拖尾残影
                    for (int i = 0; i < Projectile.oldPos.Length; i++)
                        Projectile.oldPos[i] = Vector2.Zero;

                    if (Age >= HoverTicks) {
                        // 沿冻结方向射出
                        launched = true;
                        float speed = LaunchSpeed > 0.5f ? LaunchSpeed : 20f;
                        Projectile.velocity = storedAngle.ToRotationVector2() * speed;
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.5f, Pitch = 0.55f }, Projectile.Center);
                            for (int i = 0; i < 4; i++) {
                                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch,
                                    storedAngle.ToRotationVector2().RotatedByRandom(0.4f) * Main.rand.NextFloat(1.5f, 4f),
                                    80, default, 1.2f);
                                d.noGravity = true;
                            }
                        }
                    }
                    else {
                        // 悬停微光呼吸
                        if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                                DustID.PurpleTorch, Vector2.Zero, 140, default, 0.9f);
                            d.noGravity = true;
                        }
                        Lighting.AddLight(Projectile.Center, 0.25f, 0.15f, 0.45f);
                        return;
                    }
                }
            }

            //处决重箭: 各端确定性升级 (extraUpdates 不入网络同步, 须在 AI 内统一设置)
            if (IsPierce) {
                if (Projectile.penetrate >= 0)
                    Projectile.penetrate = -1;
                if (Projectile.extraUpdates == 0) {
                    Projectile.extraUpdates = 1;
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, 130);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 紫蓝星系轨迹粒子 (幽灵态减量, 贯穿箭加量)
            int dustDenom = Age < FadeInTicks ? 4 : IsPierce ? 1 : 2;
            if (Main.rand.NextBool(dustDenom)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    120, default, IsPierce ? 1.6f : 1.2f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.2f, 0.6f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            int count = IsPierce ? 14 : 8;
            for (int i = 0; i < count; i++) {
                int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float ghost = ACMUtils.Clamp01(Age / FadeInTicks); // 幽灵态 → 武装的亮度爬升

            // 悬停期: 沿冻结方向的瞄准线 (紫→红渐变, 越近发射越亮) — 弹道即预警
            if (IsHover && !launched && Age > 1f) {
                float warn = HoverTicks > 1f ? ACMUtils.Clamp01(Age / HoverTicks) : 1f;
                Vector2 dir = storedAngle.ToRotationVector2();
                ArgusFx.DrawSightLine(sb, Projectile.Center, dir, 860f,
                    ArgusFx.GazeColor(warn, warn > 0.85f), (0.1f + warn * 0.35f) * ghost, 1.6f + warn * 1.4f);
                if (warn > 0.7f)
                    ArgusFx.ReportIfLocalPlayerSighted(Projectile.Center, dir, 860f, 90f, 0.35f);
            }

            // 紫蓝渐变拖尾 (贯穿箭更长更亮)
            float trailBoost = IsPierce ? 1.5f : 1f;
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(180, 100, 255), new Color(60, 100, 255), t)
                    * (0.5f * (1f - t) * ghost * trailBoost);
                trailColor.A = 0;
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.4f) * trailBoost, SpriteEffects.None, 0f);
            }

            // 光矢芯 (LightShot 沿速度向拉伸 — 星光箭杆; 贯穿箭白热加长)
            if (ACMAsset.LightShot != null && ghost > 0.3f && Projectile.velocity.LengthSquared() > 4f) {
                Texture2D shot = ACMAsset.LightShot;
                Color coreC = IsPierce
                    ? Color.Lerp(Color.White, TelegraphColors.Lethal, 0.25f) * (0.85f * ghost)
                    : Color.Lerp(new Color(210, 160, 255), Color.White, 0.4f) * (0.55f * ghost);
                coreC.A = 0;
                Vector2 stretch = IsPierce ? new Vector2(1.15f, 0.2f) : new Vector2(0.62f, 0.13f);
                sb.Draw(shot, Projectile.Center - Main.screenPosition, null, coreC, Projectile.rotation,
                    new Vector2(shot.Width * 0.5f, shot.Height * 0.5f), stretch, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(trailPhase * 3f) * 0.08f;
            Color mainColor = Color.Lerp(new Color(200, 140, 255), new Color(100, 140, 255),
                MathF.Sin(trailPhase) * 0.5f + 0.5f) * (0.35f + 0.65f * ghost);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin,
                Projectile.scale * pulse * (IsPierce ? 1.3f : 1f), SpriteEffects.None, 0f);

            return false;
        }
    }
}

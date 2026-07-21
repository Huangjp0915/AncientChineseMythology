using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 鱼肠剑突刺 (左键) — 重做: 突刺曲线由匀速改"快出慢收" (poly(6) 出鞘 → 短驻 → 平滑回收);
    /// 配色改寒银白青; ai[1]=1 时为"透骨刺"(更长更亮 + 起手白闪)。
    /// </summary>
    public class YuChangSwordProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordProjectile";

        private static Asset<Texture2D> _blade; // 静态缓存, 禁止每帧 Request

        private bool IsPierce => Projectile.ai[1] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 100;
            Projectile.ownerHitCheck = true;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter, true);

            // 快出慢收: 前 35% poly(6) 一瞬出鞘 → 驻锋 → 末 30% 平滑回收
            float maxThrustLength = IsPierce ? 84f : 62f;
            float t = 1f - player.itemAnimation / (float)Math.Max(player.itemAnimationMax, 1); // 0→1
            float outEase = 1f - MathF.Pow(1f - MathHelper.Clamp(t / 0.35f, 0f, 1f), 6f);
            float retract = t > 0.7f ? 1f - MathHelper.SmoothStep(0f, 0.55f, (t - 0.7f) / 0.3f) : 1f;
            Projectile.ai[0] = maxThrustLength * outEase * retract;

            // 透骨刺起手白闪 (仅第一帧)
            if (IsPierce && t < 0.1f && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustPerfect(handPosition, DustID.WhiteTorch,
                            Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 7f),
                            120, default, Main.rand.NextFloat(0.9f, 1.3f));
                        d.noGravity = true;
                    }
                }
            }

            float baseRotation = Projectile.velocity.ToRotation();
            Projectile.rotation = baseRotation + MathHelper.PiOver4;
            Projectile.spriteDirection = player.direction;

            Vector2 thrustDirection = Vector2.UnitX.RotatedBy(baseRotation);
            Projectile.Center = handPosition + thrustDirection * Projectile.ai[0];

            if (player.itemAnimation <= 1)
                Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 透骨刺命中反馈 (普刺不刷演出, 保持高频攻击的干净感)
            if (IsPierce) {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Water, scale: 0.9f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 1.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 寒银白青双层拖尾 + 刃身呼吸辉光
            float width = IsPierce ? 9f : 6f;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: width,
                outerColor: new Color(60, 95, 130, 150), innerColor: new Color(225, 240, 255, 210),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
            float breathe = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (IsPierce ? 0.42f : 0.28f) + 0.1f * breathe,
                new Color(190, 220, 255) * (IsPierce ? 0.9f : 0.6f));

            _blade ??= ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad);
            Texture2D texture = _blade.Value;
            Vector2 origin = texture.Size() * 0.5f;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale * (IsPierce ? 1.12f : 1f), SpriteEffects.None, 0);
            return false;
        }
    }
}

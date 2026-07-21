using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.Swords;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 黑风熊掌 (黑熊剑·每第 3 挥) — 重做:
    /// 原"从屏幕边缘飞入 + Boss 头贴图"→ 从玩家身后墨黑黑风中显形蓄势 (10f), 随后一帧点射 26px/f 扑向瞄准方向;
    /// 墨绿黑风拖尾 + 琥珀蜜金核心 (黑熊精色彩语言), 命中涂蜜渍 + 琥珀爆发 + 屏震。
    /// </summary>
    public class BlackBearSwordProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlackBearSwordProj1";

        private const int EmergeTime = 10;  // 身后显形蓄势帧数
        private const int Life = EmergeTime + 40;

        private static Texture2D _trail553; // 厚重剑气拖尾纹理 (静态缓存)

        private Vector2 aimDir;   // 由生成 velocity 传入的瞄准方向 (各端由 velocity 同步还原)
        private bool launched;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;
            Projectile.aiStyle = -1;
            Projectile.light = 0.25f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            Player owner = Main.player[Projectile.owner];
            aimDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            // 从玩家身后黑风中显形 (瞄准反方向 70px, 略抬高)
            Projectile.Center = owner.MountedCenter - aimDir * 70f + new Vector2(0f, -12f);
            Projectile.velocity = aimDir * 1.2f; // 显形期缓漂
            Projectile.rotation = aimDir.ToRotation();

            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.4f, Volume = 0.7f }, Projectile.Center);
        }

        public override void AI() {
            aimDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = aimDir.ToRotation();
            int age = Life - Projectile.timeLeft;

            if (age < EmergeTime) {
                // 显形蓄势: 黑风聚拢, 半透明渐显
                Projectile.Opacity = age / (float)EmergeTime * 0.8f;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(34f, 34f),
                        DustID.TintableDustLighted, Vector2.Zero, 130, new Color(38, 52, 42), Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = true;
                    d.velocity = (Projectile.Center - d.position) * 0.14f; // 向心聚拢
                }
            }
            else if (!launched) {
                // 一帧点射 (launch is a set, not a ramp) + 该帧配震/音
                launched = true;
                Projectile.velocity = aimDir * 26f;
                Projectile.Opacity = 1f;
                Projectile.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.25f, Volume = 1.05f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 1.5f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDustLighted,
                            -aimDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 6f), 130,
                            new Color(38, 52, 42), Main.rand.NextFloat(1.2f, 1.8f));
                        d.noGravity = true;
                    }
                }
            }
            else {
                // 飞行: 黑风残絮 + 琥珀火漂
                Projectile.velocity *= 1.008f; // 复合微加速, 越飞越凶
                if (!Main.dedServ) {
                    if (Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                            DustID.TintableDustLighted, -Projectile.velocity * 0.08f, 140,
                            new Color(38, 52, 42), Main.rand.NextFloat(1f, 1.5f));
                        d.noGravity = true;
                    }
                    if (Main.rand.NextBool(4)) {
                        Dust g = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                            -Projectile.velocity * 0.05f, 0, default, Main.rand.NextFloat(0.7f, 1f));
                        g.noGravity = true;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.05f);
        }

        public override bool? CanDamage() => launched ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.friendly && !target.dontTakeDamage)
                target.AddBuff(ModContent.BuffType<BlackBearHoneyGlazed>(), 60 * 4);

            // 琥珀重击爆发 + 屏震
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Bronze, scale: 1.4f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.35f, Volume = 1f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            _trail553 ??= ModContent.Request<Texture2D>(
                "AncientChineseMythology/Textures/Projectiles/SwordTrail553", AssetRequestMode.ImmediateLoad).Value;

            // 墨绿黑风外层 + 琥珀蜜金内芯拖尾 (黑熊精色彩语言)
            if (launched) {
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 20f,
                    outerColor: new Color(30, 45, 36, 170), innerColor: new Color(235, 190, 110, 200),
                    tex: _trail553, uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // 残影 (墨绿, 加法)
            if (launched) {
                Color ghost = new Color(80, 120, 90) { A = 0 };
                for (int i = 0; i < 8; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    float factor = (1f - i / 8f) * 0.4f;
                    Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, oldCenter, null, ghost * factor,
                        Projectile.rotation, origin, 0.8f, SpriteEffects.None, 0);
                }
            }

            // 本体 (显形期半透明 + 琥珀辉光)
            float glow = launched ? 0.55f : Projectile.Opacity * 0.4f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.3f + glow * 0.25f, new Color(235, 190, 110) * glow);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                Color.White * Projectile.Opacity, Projectile.rotation, origin, 0.8f, SpriteEffects.None, 0);

            return false;
        }
    }
}

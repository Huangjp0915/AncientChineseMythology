using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 金金弓箭矢 (双弹种, ai[0]: 0=黑风箭 1=蜂蜜重矢)。
    /// 黑风箭: 墨紫风尾, 30 帧后极微风导 (保持直线可读)。
    /// 蜂蜜重矢: 琥珀大箭, 重弹道微下坠, 命中迟缓 + 炸出 3 颗蜜滴。
    /// 配色取黑熊精重做语言: 墨黑(8,6,14)/风紫(52,36,78)/袈裟金(255,209,107)。
    /// </summary>
    public class BlackBearBowProj1 : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowFriendly;

        // 黑熊精配色 (只读引用 Boss 语言, 常量本地化)
        private static readonly Color WindViolet = new(52, 36, 78);
        private static readonly Color WindPale = new(150, 130, 190);
        private static readonly Color HoneyAmber = new(255, 209, 107);
        private static readonly Color HoneyDeep = new(140, 90, 20);

        private bool IsHoney => Projectile.ai[0] >= 1f;
        private ref float Age => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.arrow = true;
        }

        public override void AI() {
            Age++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (IsHoney) {
                Projectile.scale = 1.35f;
                // 重弹道: 微下坠 (蜜矢的份量)
                if (Age > 30f && Projectile.velocity.Y < 14f)
                    Projectile.velocity.Y += 0.06f;

                if (Main.rand.NextBool(4)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Honey2,
                        -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.7f, 0.7f), 90, default, 1.05f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.12f);
            }
            else {
                // 黑风箭: 30 帧后极微风导 (0.01 rad/f 上限, 保持直线读法)
                if (Age > 30f) {
                    NPC target = FindNearestTarget(400f);
                    if (target != null) {
                        float current = Projectile.velocity.ToRotation();
                        float desired = (target.Center - Projectile.Center).ToRotation();
                        float turned = MathHelper.WrapAngle(desired - current);
                        turned = MathHelper.Clamp(turned, -0.01f, 0.01f);
                        Projectile.velocity = (current + turned).ToRotationVector2() * Projectile.velocity.Length();
                    }
                }

                if (Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f), 150, WindViolet, 1.0f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.18f, 0.12f, 0.3f);
            }
        }

        private NPC FindNearestTarget(float range) {
            NPC best = null;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (IsHoney) {
                // 蜜矢: 迟缓 + 琥珀 Burst + 蜜滴迸溅 + 微震
                target.AddBuff(BuffID.Slow, 180);
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.3f }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = 0.15f }, target.Center);
                WeaponVFX.AddScreenShake(target.Center, 2f);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Gold, 1.6f, Projectile.owner);

                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Honey2,
                        Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.4f, 1f), 60, default, 1.3f);
                    d.noGravity = true;
                }

                // 炸出 3 颗蜜滴 (owner 端生成)
                if (Projectile.owner == Main.myPlayer) {
                    int splatDamage = Math.Max(1, (int)(Projectile.damage * 0.4f));
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-7f, -4f));
                        Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Top, vel,
                            ModContent.ProjectileType<BlackBearHoneySplat>(), splatDamage, 1f, Projectile.owner);
                    }
                }
            }
            else {
                // 黑风箭: 暗风命中
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Shadow, 0.8f, Projectile.owner);
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Smoke,
                        Main.rand.NextVector2Circular(3f, 3f), 130, WindViolet, 1.2f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    IsHoney ? DustID.Honey2 : DustID.Smoke,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    120, IsHoney ? default : WindViolet, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 双层拖尾: 黑风箭墨紫风尾 / 蜜矢琥珀金尾
            if (IsHoney) {
                WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                    HoneyDeep with { A = 150 }, HoneyAmber with { A = 200 },
                    uvScroll: -(float)Main.GlobalTimeWrappedHourly * 1.2f);
            }
            else {
                WeaponVFX.DrawProjectileTrail(Projectile, 6f,
                    WindViolet with { A = 150 }, WindPale with { A = 190 },
                    uvScroll: -(float)Main.GlobalTimeWrappedHourly * 1.8f);
            }

            // 出膛闪 (前 6 帧)
            if (Age < 6f)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.3f + 0.55f * (1f - Age / 6f),
                    IsHoney ? HoneyAmber : WindPale);

            // 箭体 (原版箭贴图染色; 黑风箭压暗、蜜矢透琥珀)
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Color bodyColor = IsHoney
                ? Color.Lerp(lightColor, HoneyAmber, 0.65f)
                : Color.Lerp(lightColor, new Color(30, 22, 48), 0.6f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, bodyColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // 加法辉边
            Color glow = (IsHoney ? HoneyAmber : WindPale) * 0.45f;
            glow.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow,
                Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

            return false;
        }
    }

    /// <summary>
    /// 蜜滴迸溅 (蜂蜜重矢命中副产物): 抛物线小蜜珠, 落敌/落地绽小蜜花。
    /// </summary>
    public class BlackBearHoneySplat : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color HoneyAmber = new(255, 209, 107);
        private static readonly Color HoneyDeep = new(140, 90, 20);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.BlackBearHoneySplat.DisplayName",
                () => "Honey Splat");
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.28f; // 蜜珠抛物线
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Honey2,
                    Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 0.95f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 60);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Honey2,
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0f, 1f), 60, default, 1.15f);
                d.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, 5f,
                HoneyDeep with { A = 130 }, HoneyAmber with { A = 180 });

            // 程序化蜜珠: 柔光双层
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 pos = Projectile.Center - Main.screenPosition;
                Color outer = HoneyDeep * 0.7f; outer.A = 0;
                Color inner = HoneyAmber * 0.95f; inner.A = 0;
                Main.spriteBatch.Draw(glow, pos, null, outer, 0f, glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, pos, null, inner, 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}

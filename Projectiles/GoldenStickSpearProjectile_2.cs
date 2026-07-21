using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 金棍右键"掷棍如意": 旋转掷出 → 在目标点悬停化金光柱 30 帧 (持续判定 0.6x) → 自动回旋归手。
    /// "离手仍如臂使指"; 替换旧版瞬移玩家。掷出期间物品不可再用 (棍不在手)。
    /// ai[0] = 目标飞行距离 (owner 端 Shoot 时算好, 随生成包同步)。
    /// </summary>
    internal class GoldenStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GoldenStickSpearProjectile";

        private const int HoverFrames = 30;
        private const float FlySpeed = 22f;

        private enum ThrowState { Fly, Hover, Return }

        private ThrowState State {
            get => (ThrowState)Projectile.ai[2];
            set {
                Projectile.ai[2] = (float)value;
                Projectile.netUpdate = true;
            }
        }

        private ref float TargetDist => ref Projectile.ai[0];
        private float _traveled;
        private float _hoverTimer;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * FlySpeed;
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
        }

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            // 自旋 (飞行/悬停快转, 归手减速)
            float spin = State == ThrowState.Hover ? 0.55f : 0.4f;
            Projectile.rotation += spin * (Projectile.velocity.X >= 0f || State == ThrowState.Hover ? 1f : -1f);

            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.45f, 0.12f));

            switch (State) {
                case ThrowState.Fly:
                    _traveled += Projectile.velocity.Length();
                    if (_traveled >= MathF.Max(TargetDist, 60f)) {
                        State = ThrowState.Hover;
                        Projectile.velocity = Vector2.Zero;
                        SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);
                    }
                    break;

                case ThrowState.Hover:
                    _hoverTimer++;
                    // 金光柱状态广播: 密度随剩余时间衰减
                    if (Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-40f, 40f)),
                            DustID.GoldFlame, new Vector2(0f, Main.rand.NextFloat(-2.5f, -0.8f)), 100, default, Main.rand.NextFloat(1f, 1.6f));
                        d.noGravity = true;
                    }
                    if (_hoverTimer >= HoverFrames)
                        State = ThrowState.Return;
                    break;

                case ThrowState.Return:
                    Vector2 toOwner = owner.MountedCenter - Projectile.Center;
                    float dist = toOwner.Length();
                    if (dist < 28f) {
                        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = 0.1f }, owner.Center);
                        WeaponVFX.AddScreenShake(owner.Center, 1f);
                        Projectile.Kill();
                        return;
                    }
                    Projectile.velocity = toOwner.SafeNormalize(Vector2.Zero) * (dist / 12f + 14f);
                    break;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Projectile.Center.X ? 1 : -1;
            modifiers.FinalDamage *= State switch {
                ThrowState.Hover => 0.6f,
                ThrowState.Return => 0.8f,
                _ => 1f,
            };
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, State == ThrowState.Hover ? 0.7f : 1f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 金辉拖尾 (飞行/归手时)
            if (State != ThrowState.Hover)
                WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                    new Color(160, 110, 30, 150), new Color(255, 230, 150, 205),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            // 悬停金光柱: 双层柔光脉冲
            if (State == ThrowState.Hover) {
                float pulse = 0.75f + 0.25f * MathF.Sin(_hoverTimer * 0.5f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1.6f * pulse, new Color(255, 215, 110) * 0.85f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f, new Color(255, 245, 200));
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation, tex.Size() * 0.5f, Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}

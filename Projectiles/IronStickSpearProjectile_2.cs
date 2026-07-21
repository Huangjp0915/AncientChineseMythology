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
    /// 铁棍右键"撑杆跃": 棍向斜下撑地 8 帧 (前摇下压), 借力将玩家向瞄准方向抛起。
    /// 无无敌帧 (替换旧版免伤冲刺); 位移只在 owner 端写入 velocity。跃起中棍身可造成 0.9x 接触伤害。
    /// </summary>
    internal class IronStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/IronStickSpearProjectile";

        private const int PlantFrames = 8;
        private const int LifeFrames = 24;

        private ref float AimAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];
        private bool _launched;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames + 4;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            AimAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            Projectile.velocity = Vector2.Zero;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead || owner.noItems || owner.CCed) {
                Projectile.Kill();
                return;
            }

            owner.heldProj = Projectile.whoAmI;
            owner.itemAnimation = 2;
            owner.itemTime = 2;
            // spriteDirection 每帧从已同步的 AimAngle 推导 (OnSpawn 不在远端运行)
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            owner.ChangeDir(Projectile.spriteDirection);

            int dir = Projectile.spriteDirection;
            // 撑地方向: 斜下前方 (与瞄准同侧)
            float plantAngle = dir > 0 ? MathHelper.ToRadians(62f) : MathHelper.ToRadians(118f);

            if (Timer < PlantFrames) {
                // 前摇: 棍从手侧压向地面 (二次缓动)
                float t = Timer / PlantFrames;
                float startAngle = dir > 0 ? -0.5f : MathHelper.Pi + 0.5f;
                Projectile.rotation = MathHelper.Lerp(startAngle, plantAngle, t * t);
            }
            else {
                Projectile.rotation = plantAngle;
                if (!_launched) {
                    _launched = true;
                    // 借力起跳: 只在 owner 端写玩家速度 (多人安全), 其余端跟位置同步
                    if (Main.myPlayer == Projectile.owner) {
                        Vector2 aimDir = AimAngle.ToRotationVector2();
                        owner.velocity = new Vector2(MathHelper.Clamp(aimDir.X * 12f, -9f, 9f), -10.5f);
                        owner.fallStart = (int)(owner.position.Y / 16f);
                    }
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.25f }, owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = 0.25f }, owner.Bottom);
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustPerfect(owner.Bottom, DustID.Silver,
                            new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-3.5f, -1f)), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                        d.noGravity = Main.rand.NextBool();
                    }
                }
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 armPos = owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            armPos.Y += owner.gfxOffY;
            Projectile.Center = armPos + Projectile.rotation.ToRotationVector2() * 34f;
            Projectile.scale = 1.15f * owner.GetAdjustedItemScale(owner.HeldItem);

            Timer++;
            if (Timer >= LifeFrames)
                Projectile.Kill();
        }

        // 只在跃起阶段有判定 (撑竿本身不打人)
        public override bool? CanDamage() => Timer >= PlantFrames ? base.CanDamage() : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (96f * Projectile.scale);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 14f * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.FinalDamage *= 0.9f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 撑地核心钢蓝辉光
            if (Timer >= PlantFrames - 2)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.45f, new Color(150, 185, 230) * 0.7f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float rotOff = Projectile.spriteDirection > 0 ? MathHelper.ToRadians(45f) : MathHelper.ToRadians(135f);
            SpriteEffects fx = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation + rotOff, tex.Size() * 0.5f, Projectile.scale, fx, 0);
            return false;
        }
    }
}

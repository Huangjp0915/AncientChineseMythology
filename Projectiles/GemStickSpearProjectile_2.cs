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
    /// 宝石棍右键"棱光回旋": 以玩家为轴、棍伸长 1.6x 的双头旋转斩 (40 帧, 角速度慢→快→慢),
    /// 每转过 45° 切向甩出 1 枚棱光碎片 (共 8 枚)。期间横向移速 ×0.85 (贴脸 AOE 的决策代价)。
    /// 替换旧版鼠标吸附+瞬移。
    /// </summary>
    internal class GemStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GemStickSpearProjectile";

        private const int SpinFrames = 40;
        private const float TotalRotations = 2f; // 两整圈

        private ref float AimAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];

        private float _angle;          // 累计旋转角
        private int _shardsThrown;
        private readonly Vector2[] _tipTrail = new Vector2[10];
        private int _tipCount;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SpinFrames + 8;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            AimAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            Projectile.velocity = Vector2.Zero;
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = 0.15f }, Owner.Center);
        }

        public override bool ShouldUpdatePosition() => false;

        private float Reach => 98f * 1.6f * Projectile.scale;

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

            float t = MathHelper.Clamp(Timer / SpinFrames, 0f, 1f);
            // 角速度包络: 慢→快→慢 (sin 弧)
            float omega = MathHelper.Lerp(0.1f, 0.62f, MathF.Sin(t * MathHelper.Pi));
            _angle += omega * Projectile.spriteDirection;
            Projectile.rotation = AimAngle + _angle;
            Projectile.Center = owner.MountedCenter;
            Projectile.scale = 1.15f * owner.GetAdjustedItemScale(owner.HeldItem);
            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

            // 移速代价: 只在 owner 端阻尼横向速度
            if (Main.myPlayer == Projectile.owner)
                owner.velocity.X *= 0.9f;

            // 每 45° 甩碎片 (owner 端, 上限 8)
            int shardsDue = (int)(MathF.Abs(_angle) / MathHelper.PiOver4);
            if (Main.myPlayer == Projectile.owner && _shardsThrown < Math.Min(shardsDue, 8) && Timer < SpinFrames) {
                _shardsThrown++;
                if (owner.ownedProjectileCounts[ModContent.ProjectileType<GemShardProj>()] < 12) {
                    Vector2 tangent = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * Projectile.spriteDirection);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), TipPosition(), tangent * 9.5f,
                        ModContent.ProjectileType<GemShardProj>(), (int)(Projectile.damage * 0.25f), 1f,
                        Projectile.owner, Main.rand.Next(GemStickSpearProjectile.GemColors.Length));
                }
            }

            Lighting.AddLight(TipPosition(), new Vector3(0.35f, 0.25f, 0.5f));

            for (int i = _tipTrail.Length - 1; i > 0; i--)
                _tipTrail[i] = _tipTrail[i - 1];
            _tipTrail[0] = TipPosition();
            if (_tipCount < _tipTrail.Length)
                _tipCount++;

            Timer++;
            if (Timer >= SpinFrames + 6)
                Projectile.Kill();
        }

        private Vector2 TipPosition() => Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * Reach;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 双头判定: 穿过玩家中心的整根棍
            Vector2 mid = Owner.MountedCenter;
            Vector2 tipVec = Projectile.rotation.ToRotationVector2() * Reach;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                mid - tipVec, mid + tipVec, 14f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 mid = Owner.MountedCenter;
            Vector2 tipVec = Projectile.rotation.ToRotationVector2() * Reach;
            Utils.PlotTileLine(mid - tipVec, mid + tipVec, 14f * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.FinalDamage *= 0.75f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gem, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // hue 流转拖尾 (棍尖轨迹)
            if (_tipCount >= 2) {
                float hue = (Main.GlobalTimeWrappedHourly * 0.3f) % 1f;
                Color outer = Main.hslToRgb(hue, 1f, 0.35f);
                outer.A = 150;
                Color inner = Main.hslToRgb((hue + 0.12f) % 1f, 1f, 0.72f);
                inner.A = 205;
                var pts = new Vector2[_tipCount];
                Array.Copy(_tipTrail, pts, _tipCount);
                WeaponVFX.DrawRibbonTrail(pts, 8f, outer, inner, uvScroll: -Main.GlobalTimeWrappedHourly * 1.7f);
            }

            // 双端柔光
            Vector2 tipVec = Projectile.rotation.ToRotationVector2() * Reach;
            float hueGlow = (Main.GlobalTimeWrappedHourly * 0.3f + 0.05f) % 1f;
            Color glow = Main.hslToRgb(hueGlow, 0.9f, 0.7f);
            WeaponVFX.DrawGlowBurst(Owner.MountedCenter + tipVec, 0.4f, glow * 0.8f);
            WeaponVFX.DrawGlowBurst(Owner.MountedCenter - tipVec, 0.4f, glow * 0.8f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float rotOff = Projectile.spriteDirection > 0 ? MathHelper.ToRadians(45f) : MathHelper.ToRadians(135f);
            SpriteEffects fx = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            // 双头: 以玩家为中心画整根 (放大 1.6)
            Main.EntitySpriteDraw(tex, Owner.MountedCenter - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation + rotOff, tex.Size() * 0.5f, Projectile.scale * 1.6f, fx, 0);
            return false;
        }
    }
}

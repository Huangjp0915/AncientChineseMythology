using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 鱼肠剑·穿心背刺 (右键) — 重做: 由"直线飞弹"改为背刺连刺载体。
    /// ai[0] ≥ 0 = 目标 whoAmI: 瞬身后的三段连刺 (第 4/12/20 帧各一刺, 快出慢收微循环);
    /// ai[0] = -1 = 幽影突进模式: 单段重刺。
    /// 残血 (&lt;15%) 非 Boss 目标被连刺命中 → 处决 (致命红爆发 + 闷响)。
    /// </summary>
    public class YuChangSkillProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordProjectile";

        private static Asset<Texture2D> _blade; // 静态缓存

        private const int LifeFlurry = 30;
        private const int LifeDash = 16;
        private static readonly int[] StabFrames = { 4, 12, 20 };
        private const int StabWindow = 4; // 每刺伤害窗口帧数

        private bool DashMode => Projectile.ai[0] < 0f;
        private int TargetIndex => (int)Projectile.ai[0];

        private Player Owner => Main.player[Projectile.owner];
        private Vector2 stabDir = Vector2.UnitX;
        private float lunge;            // 当前突出距离 (视觉/判定共用)
        private bool executeFlash;      // 本次命中触发了处决

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFlurry;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public override bool ShouldUpdatePosition() => false;

        private int Age => (DashMode ? LifeDash : LifeFlurry) - Projectile.timeLeft;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            // 首帧初始化: 突进模式用更短寿命 (Age 以此为基准)
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (DashMode)
                    Projectile.timeLeft = LifeDash;
            }

            // 连刺期间锁手 (刺客起手不可取消)
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            // 方向: 目标存活 → 咬住目标; 否则沿生成方向
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            if (target != null && target.active && !target.friendly)
                stabDir = (target.Center - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            else
                stabDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            Owner.ChangeDir(stabDir.X >= 0f ? 1 : -1);

            // 突刺包络: 每刺快出 (poly6) 慢收
            lunge = 0f;
            int age = Age;
            if (DashMode) {
                float t = MathHelper.Clamp((age - 3) / 6f, 0f, 1f);
                lunge = 86f * (1f - MathF.Pow(1f - t, 6f)) * (age > 10 ? 1f - (age - 10) / 6f : 1f);
                if (age == 3)
                    StabFX(1.1f, 0.35f);
            }
            else {
                for (int i = 0; i < StabFrames.Length; i++) {
                    int since = age - StabFrames[i];
                    if (since < 0)
                        continue;
                    if (since <= StabWindow)
                        lunge = 74f * (1f - MathF.Pow(1f - since / (float)StabWindow, 6f));
                    else if (since <= 7)
                        lunge = 74f * (1f - (since - StabWindow) / 3f) * 0.5f;
                    if (since == 0)
                        StabFX(0.9f, 0.2f + i * 0.18f); // 音高逐刺递升
                }
            }

            Projectile.Center = Owner.MountedCenter + stabDir * (24f + lunge);
            Projectile.rotation = stabDir.ToRotation() + MathHelper.PiOver4;

            // 拖尾历史自记录 (刺尖)
            for (int i = ProjectileID.Sets.TrailCacheLength[Type] - 1; i > 0; i--)
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
            Projectile.oldPos[0] = Projectile.position;
        }

        private void StabFX(float volume, float pitch) {
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch, Volume = volume }, Owner.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(Owner.MountedCenter + stabDir * 30f, DustID.WhiteTorch,
                        stabDir.RotatedByRandom(0.25f) * Main.rand.NextFloat(4f, 9f), 130, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
        }

        // 只有突出期造成伤害 (视觉与判定严格对齐)
        public override bool? CanDamage() => lunge > 30f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + stabDir * (40f + lunge + 30f);
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 18f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 处决: 残血非 Boss → 巨额终伤走正常管线 (保留掉落/OnKill)
            if (!target.boss && target.lifeMax > 5 && target.life < target.lifeMax * 0.15f && !target.dontTakeDamage) {
                modifiers.FinalDamage *= 25f;
                executeFlash = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (executeFlash || target.life <= 0) {
                executeFlash = false;
                // 处决: 致命红爆发 + 闷响 + 屏震
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.LethalRed, scale: 1.4f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 3f);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 0.45f }, target.Center);
            }
            else {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Water, scale: 0.8f, owner: Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 刺出期: 寒银芯光束 + 白青拖尾
            if (lunge > 8f) {
                Vector2 start = Owner.MountedCenter + stabDir * 16f;
                Vector2 end = Owner.MountedCenter + stabDir * (40f + lunge + 26f);
                ACMShaders.DrawBeam(start, end, 7f,
                    new Color(235, 245, 255), new Color(110, 160, 210), MathHelper.Clamp(lunge / 74f, 0f, 1f) * 0.85f,
                    coreSharp: 3f);
            }
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
                outerColor: new Color(60, 95, 130, 150), innerColor: new Color(225, 240, 255, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.8f);

            _blade ??= ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad);
            Texture2D texture = _blade.Value;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor * MathHelper.Clamp(0.4f + lunge / 74f, 0f, 1f),
                Projectile.rotation, texture.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

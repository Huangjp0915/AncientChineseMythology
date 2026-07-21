using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 赤铜剑·入炉蓄力斩 (右键) — 重做:
    /// 旧版: 伤害纯随机 ×2~×8、AI 直读 Main.mouseRight (多人失步)。
    /// 新版: 按住右键"入炉"—— 45/105/165 帧升 L1/L2/L3 (×2.2/×3.6/×5.5),
    /// 每级白热闪 + 冲击环 + 升调音广播; 蓄力经 Owner.channel 原生同步;
    /// 松手"出炉"poly(10) 重斩, L2/L3 追加熔火溅珠。视觉与判定严格对齐。
    /// </summary>
    public class CrimsonbronzeSwordProj1 : ModProjectile
    {
        private const float SwingRange = 2.6f;   // 挥动覆角
        private const float Backswing = 0.5f;    // 蓄力后摆角
        private static readonly int[] LevelFrames = { 45, 105, 165 };
        private static readonly float[] LevelMult = { 2.2f, 3.6f, 5.5f };

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>当前蓄力等级 0~3 (owner 端推进, 经 ai[0] 广播供各端表现)。</summary>
        private int Level {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private ref float AimAngle => ref Projectile.ai[1]; // 瞄准角 (owner 写, 同步)
        private ref float Timer => ref Projectile.ai[2];

        private enum Stage { Charge, Swing, Fade }
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[0];
            set { Projectile.localAI[0] = (float)value; Timer = 0; }
        }
        private ref float Progress => ref Projectile.localAI[1];

        private int chargeTimer;       // owner 端蓄力计时 (等级经 ai[0] 广播)
        private int baseDamage = -1;   // 生成时的基础伤害 (乘级前)
        private bool pearlsSpawned;

        private float ExecTime => 9f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float FadeTime => 10f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/CrimsonbronzeSword";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.timeLeft = 1200;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void OnSpawn(IEntitySource source) {
            baseDamage = Projectile.damage;
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            AimAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((sbyte)Projectile.spriteDirection);
        public override void ReceiveExtraAI(BinaryReader reader) => Projectile.spriteDirection = reader.ReadSByte();

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }
            baseDamage = baseDamage < 0 ? Projectile.damage : baseDamage;

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            switch (CurrentStage) {
                case Stage.Charge: DoCharge(); break;
                case Stage.Swing: DoSwing(); break;
                default: DoFade(); break;
            }

            SetSwordPosition();

            for (int i = Projectile.oldRot.Length - 1; i > 0; i--)
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            Projectile.oldRot[0] = Projectile.rotation;

            Timer++;
        }

        private void DoCharge() {
            // 瞄准: 仅 owner 读鼠标, 节流同步
            if (Projectile.owner == Main.myPlayer) {
                Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
                AimAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                if ((int)Timer % 10 == 0)
                    Projectile.netUpdate = true;

                chargeTimer++;
                int newLevel = 0;
                for (int i = 0; i < LevelFrames.Length; i++)
                    if (chargeTimer >= LevelFrames[i])
                        newLevel = i + 1;
                if (newLevel != Level) {
                    Level = newLevel;
                    Projectile.netUpdate = true;
                    LevelUpFX();
                }

                // 松手出炉 / 无级取消
                if (!Owner.channel) {
                    if (Level <= 0) {
                        Projectile.Kill();
                        return;
                    }
                    Projectile.damage = (int)(baseDamage * LevelMult[Level - 1]);
                    Projectile.netUpdate = true;
                    CurrentStage = Stage.Swing;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f + Level * 0.12f, Volume = 1.2f }, Owner.Center);
                    return;
                }
            }
            else if (!Owner.channel && Level > 0) {
                // 远端客户端跟随 channel 状态进挥砍 (伤害由 owner 权威)
                CurrentStage = Stage.Swing;
                return;
            }

            // 蓄力姿态: 后摆 + 随级抖动 (入炉颤火)
            float tremble = Level * 0.012f * MathF.Sin(Timer * 2.2f);
            Progress = -Backswing * MathHelper.Clamp(Timer / 12f, 0f, 1f) + tremble;

            // 火尘向刃身汇聚, 密度 ∝ 级; 每级阈值前 8 帧静默 (吸气)
            if (!Main.dedServ) {
                bool preSilence = false;
                for (int i = 0; i < LevelFrames.Length; i++)
                    if (chargeTimer > LevelFrames[i] - 8 && chargeTimer < LevelFrames[i])
                        preSilence = true;
                if (!preSilence && Main.rand.NextBool(Math.Max(4 - Level, 1))) {
                    Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 60f;
                    Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(46f, 46f), DustID.Torch);
                    d.noGravity = true;
                    d.velocity = (tip - d.position) * 0.1f;
                    d.scale = Main.rand.NextFloat(1f, 1.4f + Level * 0.2f);
                }
                Lighting.AddLight(Projectile.Center, 0.3f + Level * 0.15f, 0.12f + Level * 0.05f, 0.03f);
            }
        }

        /// <summary>升级瞬间: 白热闪 + 冲击环 + 升调音 (owner 触发, 远端由 ai[0] 变化补帧闪光)。</summary>
        private void LevelUpFX() {
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f + Level * 0.25f, Volume = 0.9f }, Owner.Center);
            WeaponVFX.AddScreenShake(Owner.Center, 1f + Level * 0.5f);
            if (!Main.dedServ) {
                for (int i = 0; i < 10 + Level * 4; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        Main.rand.NextVector2Circular(5f, 5f), 0, default, Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }
        }

        private void DoSwing() {
            // poly(10) ease-out: 出炉一瞬
            float t = MathHelper.Clamp(Timer / ExecTime, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - t, 10f);
            Progress = MathHelper.Lerp(-Backswing, SwingRange, ease);

            // 熔火溅珠 (L2: 1 / L3: 3), 挥出前段一次性放出
            if (!pearlsSpawned && t >= 0.3f) {
                pearlsSpawned = true;
                if (Projectile.owner == Main.myPlayer && Level >= 2) {
                    int count = Level >= 3 ? 3 : 1;
                    Vector2 dir = AimAngle.ToRotationVector2();
                    for (int i = 0; i < count; i++) {
                        Vector2 vel = dir.RotatedBy(MathHelper.Lerp(-0.28f, 0.28f, count == 1 ? 0.5f : i / (float)(count - 1)))
                            * Main.rand.NextFloat(13f, 16f);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + dir * 40f, vel,
                            ModContent.ProjectileType<CrimsonbronzeSwordProj2>(), (int)(baseDamage * 0.8f), 2f, Projectile.owner);
                    }
                }
            }

            if (Timer >= ExecTime * 2f)
                CurrentStage = Stage.Fade;
        }

        private void DoFade() {
            float t = MathHelper.Clamp(Timer / FadeTime, 0f, 1f);
            Progress = MathHelper.Lerp(SwingRange, SwingRange * 1.08f, 1f - MathF.Pow(1f - t, 5f));
            Projectile.Opacity = 1f - t;
            if (Timer >= FadeTime)
                Projectile.Kill();
        }

        public void SetSwordPosition() {
            // 挥弧以瞄准角为中心偏后展开: 蓄力held于 aim−0.55R, 收尾于 aim+0.45R
            Projectile.rotation = AimAngle + Projectile.spriteDirection * (Progress - SwingRange * 0.55f);

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            armPosition.Y += Owner.gfxOffY;
            Projectile.Center = armPosition;
            Projectile.scale = (0.9f + Level * 0.08f) * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Swing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.05f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.05f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.OnFire))
                target.AddBuff(BuffID.OnFire, 120 + Level * 60);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Crimson, scale: 0.9f + Level * 0.25f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 1f + Level * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(0, Projectile.height);
                rotationOffset = MathHelper.ToRadians(45f);
                effects = SpriteEffects.None;
            }
            else {
                origin = new Vector2(Projectile.width, Projectile.height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }

            // 刃芯光束: 蓄力期随级白热, 挥砍期全亮 (视觉即伤害广播)
            float heat = CurrentStage == Stage.Charge ? Level / 3f : 1f;
            if (heat > 0.05f && Projectile.Opacity > 0.1f) {
                float bladeLen = Projectile.Size.Length() * Projectile.scale * 1.05f;
                Vector2 bladeStart = Owner.MountedCenter;
                Vector2 bladeEnd = bladeStart + Projectile.rotation.ToRotationVector2() * bladeLen;
                Color core = Color.Lerp(new Color(255, 120, 45), new Color(255, 235, 210), heat);
                ACMShaders.DrawBeam(bladeStart, bladeEnd, (8f + 6f * heat) * Projectile.scale,
                    core, new Color(255, 60, 40), MathHelper.Clamp(Projectile.Opacity, 0.25f, 1f) * (0.35f + 0.65f * heat));
            }

            // 挥砍残影 (赤橙)
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            if (CurrentStage != Stage.Charge) {
                Color ghost = new Color(255, 110, 45) { A = 0 };
                for (int i = 1; i < 9; i++) {
                    float factor = 0.5f - i / 18f;
                    Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                        ghost * factor * Projectile.Opacity,
                        Projectile.oldRot[i] + rotationOffset, origin, Projectile.scale, effects, 0);
                }
            }

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor * Projectile.Opacity, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);
            return false;
        }
    }

    /// <summary>
    /// 赤铜·熔火溅珠 (L2/L3 出炉附带) — 重做: 移除旧版 ×2~×8 随机倍率 (不可读);
    /// 固定 ×0.8, 抛物线坠落, 触地/命中溅火。
    /// </summary>
    internal class CrimsonbronzeSwordProj2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/CrimsonbronzeSwordProj2";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.knockBack = 0.6f;
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = 2;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.28f; // 熔珠坠弧
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, 0f, 0f, 100, default, 1.6f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.15f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!target.HasBuff(BuffID.OnFire))
                target.AddBuff(BuffID.OnFire, 180);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Crimson, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
                outerColor: new Color(190, 30, 20, 150), innerColor: new Color(255, 165, 70, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.4f, new Color(255, 120, 50));

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Color ghost = Color.White;
            ghost.A = 0;
            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float factor = 1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type];
                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, oldCenter, null, ghost * (factor * 0.5f),
                    Projectile.oldRot[i], origin, 1f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White * 0.6f,
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, 0f, 0f, 100, default, 2f);
                d.velocity *= 3f;
                if (Main.rand.NextBool(2)) {
                    d.scale = 0.5f;
                    d.fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 辉金虚空斩裂刃 —— 超级毕业大刀，挥击时释放撕裂虚空的巨型辉金剑波，
    /// 每次挥击同时生成一道穿屏斩浪
    /// </summary>
    public class AureateVoidrender : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 490;
            Item.DamageType = DamageClass.Melee;
            Item.width = 90;
            Item.height = 90;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 14;
            Item.crit = 30;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<AureateVoidrenderSwing>();
            Item.shootSpeed = 3f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 近战挥舞本体
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 大刀挥舞弹幕（类似 GanJiangSword 的扇形挥击）
    // ──────────────────────────────────────────────────────────────
    public class AureateVoidrenderSwing : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/AureateVoidrender";
        private const float SWING_RANGE = (float)Math.PI * 1.6f;
        private const float PREP_FRAC = 0.20f;
        private const float EXEC_FRAC = 0.55f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[0];
        private ref float InitAngle => ref Projectile.ai[1];
        private ref float RawProgress => ref Projectile.localAI[0];
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }
        private bool _waveFired = false;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.timeLeft = 10000;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            if (Projectile.spriteDirection == 1)
                InitAngle = MathHelper.Clamp(toMouse, -(float)Math.PI / 2.8f, (float)Math.PI / 5f)
                            - SWING_RANGE * 0.55f;
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                InitAngle = MathHelper.Clamp(toMouse, (float)Math.PI * 0.78f, (float)Math.PI * 1.4f)
                            + SWING_RANGE * 0.55f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            float totalTime = Owner.itemAnimationMax;
            float prepEnd = totalTime * PREP_FRAC;
            float execDur = totalTime * EXEC_FRAC;
            float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;
                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE,
                        Math.Min(Timer / execDur, 1f));
                    // 挥到一半时发射剑气
                    if (!_waveFired && Timer >= execDur * 0.35f) {
                        _waveFired = true;
                        Vector2 waveDir = Owner.DirectionTo(Main.MouseWorld);
                        Projectile.NewProjectile(
                            Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center, waveDir * 22f,
                            ModContent.ProjectileType<AureateVoidWave>(),
                            (int)(Owner.HeldItem.damage * 1.5f),
                            Owner.HeldItem.knockBack * 0.7f, Owner.whoAmI);
                        SoundEngine.PlaySound(SoundID.Item84, Owner.position);
                    }
                    if (Timer >= execDur) CurrentStage = Stage.Unwind;
                    break;
                case Stage.Unwind:
                    float t2 = Math.Min(Timer / unwindDur, 1f);
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.04f, t2);
                    if (Timer >= unwindDur) Projectile.Kill();
                    break;
            }

            // 定位
            float dir = Projectile.spriteDirection;
            Projectile.rotation = InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale = 1.3f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
            Timer++;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 s = Owner.MountedCenter;
            Vector2 e = s + Projectile.rotation.ToRotationVector2()
                        * Projectile.Size.Length() * Projectile.scale * 1.1f;
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                s, e, 24f * Projectile.scale, ref col);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float rotOff = Projectile.spriteDirection > 0
                ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            // ── 先切换到 Additive，绘制拖尾和光晕 ──
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute) {
                Texture2D slash = ACMAsset.GlaciateWave;
                for (int i = 1; i < 12 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float a = (1f - i / 12f) * 0.70f;
                    float rot = Projectile.oldRot[i] + rotOff;
                    // 金色主层
                    sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                        new Color(255, 200, 20) * a, rot,
                        new Vector2(slash.Width * 0.5f, slash.Height * 0.5f),
                        Projectile.scale * 0.52f, SpriteEffects.None, 0);
                    // 虚空紫色叠加
                    sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                        new Color(160, 30, 255) * (a * 0.48f), rot + 0.13f,
                        new Vector2(slash.Width * 0.5f, slash.Height * 0.5f),
                        Projectile.scale * 0.38f, SpriteEffects.None, 0);
                }

                // SoftGlow 脉冲光晕
                float pulse = 0.75f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
                Texture2D sg = ACMAsset.SoftGlow;
                sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 210, 40) * 0.65f * pulse, Projectile.rotation + rotOff,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                    Projectile.scale * 2.4f, SpriteEffects.None, 0);

                // Sparkle 剑尖星芒
                Texture2D sparkle = ACMAsset.Sparkle;
                Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                              * Projectile.Size.Length() * Projectile.scale * 0.6f;
                sb.Draw(sparkle, tip - Main.screenPosition, null,
                    new Color(255, 235, 80) * 0.55f,
                    (float)Main.timeForVisualEffects * 0.06f,
                    new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                    Projectile.scale * 0.75f, SpriteEffects.None, 0);
            }

            // ── 恢复 AlphaBlend 绘制刀身本体 ──
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects fx = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = Projectile.spriteDirection > 0
                ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation + rotOff, origin,
                Projectile.scale, fx, 0);

            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 虚空斩浪：高速穿透的金-紫双色剑气
    // ──────────────────────────────────────────────────────────────
    public class AureateVoidWave : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/GlaciateWave";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ACMAsset.GlaciateWave;
            Texture2D sg = ACMAsset.SoftGlow;

            float life = 1f - Projectile.timeLeft / 55f;
            float scaleX = MathHelper.Lerp(1.8f, 0.6f, ACMUtils.QuadIn(life));
            float scaleY = MathHelper.Lerp(0.70f, 0.28f, ACMUtils.QuadIn(life));
            float alpha = ACMUtils.QuadOut(1f - life) * 0.95f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            // 金色主体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 215, 40) * alpha,
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(scaleX, scaleY), SpriteEffects.None, 0);

            // 虚空紫色叠层（稍偏、稍窄，制造切割感）
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(140, 0, 255) * (alpha * 0.55f),
                Projectile.rotation + 0.06f,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(scaleX * 0.78f, scaleY * 0.78f), SpriteEffects.None, 0);

            // 前端 SoftGlow 弹头光折
            Vector2 frontPos = Projectile.Center
                + Projectile.velocity.SafeNormalize(Vector2.Zero) * 50f;
            sb.Draw(sg, frontPos - Main.screenPosition, null,
                new Color(255, 225, 80) * alpha * 0.80f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scaleY * 2.2f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}


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
            Item.width  = 90;
            Item.height = 90;
            Item.useTime      = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 14;
            Item.crit  = 30;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.noUseGraphic = true;
            Item.noMelee      = true;
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
        private const float PREP_FRAC   = 0.20f;
        private const float EXEC_FRAC   = 0.55f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer       => ref Projectile.ai[0];
        private ref float InitAngle   => ref Projectile.ai[1];
        private ref float RawProgress => ref Projectile.localAI[0];
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }
        private bool _waveFired = false;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type]   = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width  = 90;
            Projectile.height = 90;
            Projectile.friendly    = true;
            Projectile.timeLeft    = 10000;
            Projectile.penetrate   = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = -1;
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
            Owner.itemTime      = 2;

            float totalTime   = Owner.itemAnimationMax;
            float prepEnd     = totalTime * PREP_FRAC;
            float execDur     = totalTime * EXEC_FRAC;
            float unwindDur   = totalTime * (1f - PREP_FRAC - EXEC_FRAC);

            // 粒子
            if (CurrentStage == Stage.Execute) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                        DustID.GoldCoin,
                        Main.rand.NextVector2Circular(5, 5) - Projectile.velocity * 0.3f,
                        0, new Color(255, 210, 40), Main.rand.NextFloat(1.2f, 2.5f));
                    d.noGravity = true;
                }
                // 虚空紫尘
                if (Main.rand.NextBool(3)) {
                    Dust dv = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40, 40),
                        DustID.Shadowflame,
                        Main.rand.NextVector2Circular(3, 3), 0, default, 1.5f);
                    dv.noGravity = true;
                }
            }

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
                        Vector2 waveDir = Owner.To(Main.MouseWorld).UnitVector();
                        Projectile.NewProjectile(
                            Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center, waveDir * 22f,
                            ModContent.ProjectileType<AureateVoidWave>(),
                            (int)(Owner.HeldItem.damage * 1.5f),
                            Owner.HeldItem.knockBack * 0.7f, Owner.whoAmI);
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
            Projectile.rotation= InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale  = 1.3f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj    = Projectile.whoAmI;
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

            // Additive 模式绘制挥舞拖尾
            if (CurrentStage == Stage.Execute) {
                Texture2D slash = ACMAsset.GlaciateWave;
                for (int i = 1; i < 12 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float a = (1f - i / 12f) * 0.50f;
                    // 金色主层
                    Color cg = new Color(255, 200, 20, 0) * a;
                    float rot = Projectile.oldRot[i] +
                        (Projectile.spriteDirection > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi);
                    sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                        cg, rot,
                        new Vector2(slash.Width * 0.5f, slash.Height * 0.5f),
                        Projectile.scale * 0.38f, SpriteEffects.None, 0);
                    // 虚空紫色叠加
                    Color cv = new Color(160, 30, 255, 0) * a * 0.5f;
                    sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                        cv, rot + 0.12f,
                        new Vector2(slash.Width * 0.5f, slash.Height * 0.5f),
                        Projectile.scale * 0.32f, SpriteEffects.None, 0);
                }
            }

            // 本体绘制
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects fx = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rotOff = Projectile.spriteDirection > 0
                ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;
            Vector2 origin = Projectile.spriteDirection > 0
                ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation + rotOff, origin,
                Projectile.scale, fx, 0);

            // 叠加金色光晕
            if (CurrentStage == Stage.Execute) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                Texture2D sg = ACMAsset.SoftGlow;
                sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 210, 30, 0) * 0.4f, Projectile.rotation + rotOff,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                    Projectile.scale * 2.0f, SpriteEffects.None, 0);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }
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
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width  = 120;
            Projectile.height = 60;
            Projectile.friendly    = true;
            Projectile.tileCollide = false;
            Projectile.penetrate   = -1;
            Projectile.timeLeft    = 55;
            Projectile.DamageType  = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 12;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            // 散发粒子
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(30, 14),
                    DustID.GoldFlame, -Projectile.velocity * 0.1f, 0,
                    new Color(255, 200, 30), Main.rand.NextFloat(1f, 2.5f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex  = ACMAsset.GlaciateWave;

            float life = 1f - Projectile.timeLeft / 55f;
            float scaleX = MathHelper.Lerp(1.2f, 0.7f, life);
            float scaleY = MathHelper.Lerp(0.55f, 0.3f, life);

            // 金色主体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 210, 30, 0) * (1f - life * 0.5f),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(scaleX, scaleY), SpriteEffects.None, 0);

            // 虚空紫色叠层
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(130, 0, 255, 0) * (1f - life * 0.6f) * 0.6f,
                Projectile.rotation + 0.05f,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(scaleX * 0.85f, scaleY * 0.85f), SpriteEffects.None, 0);

            return false;
        }
    }
}


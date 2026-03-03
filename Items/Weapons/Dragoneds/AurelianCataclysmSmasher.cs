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
    /// 万劫狂金震碎者 —— 超级毕业锤子，轮抡时喷射烈金粒子，命中时掀起全屏金色裂地冲击波并剧烈震屏
    /// </summary>
    public class AurelianCataclysmSmasher : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 520;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 18;
            Item.crit = 22;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<AurelianCataclysmSmasherProj>();
            Item.shootSpeed = 3f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 弹幕：大锤挥舞本体 + 命中时产生巨型金色冲击波
    // ──────────────────────────────────────────────────────────────
    public class AurelianCataclysmSmasherProj : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/AurelianCataclysmSmasher";
        // 挥舞总弧度（约270°）
        private const float SWING_RANGE = (float)Math.PI * 1.5f;
        private const float PREP_FRAC = 0.25f; // 蓄力占比
        private const float SWING_FRAC = 0.55f; // 挥击占比
        // unwind 占剩余部分

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[0];
        private ref float InitAngle => ref Projectile.ai[1];
        private ref float RawProgress => ref Projectile.localAI[0];
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private Player Owner => Main.player[Projectile.owner];

        // 每次命中只产生一次冲击波
        private bool _hasBlasted = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
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
            // 从后方大角度开始挥
            InitAngle = toMouse - Projectile.spriteDirection * SWING_RANGE * 0.6f;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            float totalTime = Owner.itemAnimationMax;
            float prepEnd = totalTime * PREP_FRAC;
            float swingEnd = totalTime * (PREP_FRAC + SWING_FRAC);

            // 粒子拖尾（挥击阶段）
            if (CurrentStage == Stage.Execute && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(6, 6), 0,
                    new Color(255, 210, 50), Main.rand.NextFloat(1.5f, 3f));
                d.noGravity = true;
            }

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item62, Owner.position); // 沉重金属音
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    float execElapsed = Timer;
                    float execDuration = totalTime * SWING_FRAC;
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(execElapsed / execDuration, 1f));
                    if (Timer >= execDuration) CurrentStage = Stage.Unwind;
                    break;

                case Stage.Unwind:
                    float unwindDuration = totalTime * (1f - PREP_FRAC - SWING_FRAC);
                    float t = Math.Min(Timer / unwindDuration, 1f);
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.05f, t);
                    if (Timer >= unwindDuration) Projectile.Kill();
                    break;
            }

            // 定位
            Projectile.rotation = InitAngle + Projectile.spriteDirection * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale = 1.35f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;

            Timer++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!_hasBlasted) {
                _hasBlasted = true;
                // 震屏
                Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18f, 25);
                // 播放爆裂音
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode, target.Center);
                // EmberShards 碎片爆散
                for (int i = 0; i < 22; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f) * Main.rand.NextFloat(0.5f, 1.8f);
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel, 0,
                        new Color(255, 200, 30), Main.rand.NextFloat(2f, 4.5f));
                    d.noGravity = true;
                }
                // 大型 SoftGlow 爆闪
                for (int i = 0; i < 6; i++) {
                    Dust ds = Dust.NewDustPerfect(target.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(14f, 14f), 0,
                        new Color(255, 240, 130), 3.5f);
                    ds.noGravity = true;
                    ds.fadeIn = 1f;
                }
                // 生成环形冲击波弹幕（视觉用，无伤害）
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<AurelianShockwave>(),
                    0, 0f, Owner.whoAmI);
            }
            target.AddBuff(BuffID.OnFire3, 300);
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2()
                            * (Projectile.Size.Length() * Projectile.scale * 1.1f);
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, 22f * Projectile.scale, ref col);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float rotOff = Projectile.spriteDirection > 0
                ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D slashTex = ACMAsset.SlashBurst;
            Texture2D sgTex = ACMAsset.SoftGlow;

            if (CurrentStage == Stage.Execute) {
                // ── 拖尾：用 SlashBurst 扇形叠加，金色 + 紫虚空双层 ──
                for (int i = 1; i < 14 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float a = (1f - i / 14f) * 0.72f;
                    float rot = Projectile.oldRot[i] + rotOff;
                    // 金色层
                    sb.Draw(slashTex, Projectile.Center - Main.screenPosition, null,
                        new Color(255, 200, 30) * a, rot,
                        new Vector2(slashTex.Width * 0.5f, slashTex.Height),
                        Projectile.scale * 0.6f, SpriteEffects.None, 0);
                    // 紫色虚空叠层（偏移旋转更显撕裂感）
                    sb.Draw(slashTex, Projectile.Center - Main.screenPosition, null,
                        new Color(180, 40, 255) * (a * 0.45f), rot + 0.18f,
                        new Vector2(slashTex.Width * 0.5f, slashTex.Height),
                        Projectile.scale * 0.45f, SpriteEffects.None, 0);
                }

                // ── 锤头光晕：SoftGlow 大圆光 + Sparkle 星芒 ──
                float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
                sb.Draw(sgTex, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 210, 50) * 0.70f * pulse,
                    Projectile.rotation + rotOff,
                    new Vector2(sgTex.Width * 0.5f, sgTex.Height * 0.5f),
                    Projectile.scale * 2.5f, SpriteEffects.None, 0);

                Texture2D sparkle = ACMAsset.Sparkle;
                sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 230, 80) * 0.55f,
                    (float)Main.timeForVisualEffects * 0.04f,
                    new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                    Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            // ── 锤子本体 ──
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects fx = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = Projectile.spriteDirection > 0
                ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition,
                null, lightColor, Projectile.rotation + rotOff,
                origin, Projectile.scale, fx, 0);

            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 视觉冲击波：扩散的金色环形爆炸
    // ──────────────────────────────────────────────────────────────
    public class AurelianShockwave : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/Dragoneds/AurelianCataclysmSmasher";

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.alpha = 255;
            Projectile.aiStyle = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float progress = 1f - Projectile.timeLeft / 50f;
            float alpha = ACMUtils.QuadOut(1f - progress);
            float scale = ACMUtils.QuadOut(progress) * 18f;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D sparkle = ACMAsset.Sparkle;
            Texture2D sg = ACMAsset.SoftGlow;

            // ── 外圈八芒星冲击环  ──
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                new Color(255, 220, 60) * alpha,
                (float)Main.timeForVisualEffects * 0.02f,
                new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                scale * 0.75f, SpriteEffects.None, 0);
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                new Color(255, 200, 30) * (alpha * 0.55f),
                (float)Main.timeForVisualEffects * 0.02f + MathHelper.PiOver4,
                new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                scale * 0.60f, SpriteEffects.None, 0);

            // ── SlashBurst 径向爆散（4方向叠加）──
            Texture2D slash = ACMAsset.SlashBurst;
            for (int k = 0; k < 4; k++) {
                sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 180, 20) * (alpha * 0.65f),
                    k * MathHelper.PiOver2,
                    new Vector2(slash.Width * 0.5f, slash.Height),
                    new Vector2(scale * 0.28f, scale * 0.55f), SpriteEffects.None, 0);
            }

            // ── 中心 SoftGlow 强核 ──
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 240, 120) * alpha * 1.3f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.40f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}


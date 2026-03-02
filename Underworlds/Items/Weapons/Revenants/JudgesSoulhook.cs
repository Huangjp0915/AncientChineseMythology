using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 判官勾魂枪 - 判官用以勾取有罪之魂的长枪，近战长枪类武器
    /// 肉后中期，手持突刺型弹幕，命中时勾取灵魂恢复生命
    /// </summary>
    public class JudgesSoulhook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 72;
            Item.crit = 6;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<JudgesSoulhookProjectile>();
            Item.shootSpeed = 4f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<JudgesSoulhookProjectile>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 判官勾魂枪弹幕 - 手持突刺型长枪弹幕，枪尖带有勾魂光效
    /// 使用ACMAsset.LightShot叠加枪尖光弹，ACMAsset.SoftGlow绘制勾魂光圈
    /// </summary>
    public class JudgesSoulhookProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/JudgesSoulhook";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set {
                Projectile.ai[0] = (float)value;
                Timer = 0;
            }
        }

        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustDistance => ref Projectile.localAI[0];
        private const float MaxThrustDistance = 30f;
        private const float BaseOffset = 4f;
        private Player Owner => Main.player[Projectile.owner];

        private float PrepareTime => 3f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 7f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 5f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            switch (CurrentStage) {
                case AttackStage.Prepare:
                    HandlePrepare();
                    break;
                case AttackStage.Thrust:
                    HandleThrust();
                    break;
                case AttackStage.Retract:
                    HandleRetract();
                    break;
            }

            UpdatePositionAndRotation();
            SpawnSoulhookParticles();

            //判官绿色光照
            Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.3f);

            Timer++;
        }

        private void HandlePrepare() {
            ThrustDistance = MathHelper.Lerp(0, -8f, Timer / PrepareTime);
            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.1f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            float progress = Timer / ThrustTime;
            ThrustDistance = MathHelper.SmoothStep(-8f, MaxThrustDistance, progress);
            if (Timer >= ThrustTime) {
                CurrentStage = AttackStage.Retract;
            }
        }

        private void HandleRetract() {
            float progress = Timer / RetractTime;
            ThrustDistance = MathHelper.SmoothStep(MaxThrustDistance, 0, progress);
            if (Timer >= RetractTime) {
                Projectile.Kill();
            }
        }

        private void UpdatePositionAndRotation() {
            Vector2 direction = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();
            Projectile.spriteDirection = direction.X > 0 ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;

            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;
            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance);
        }

        private void SpawnSoulhookParticles() {
            Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 35f;

            //突刺时枪尖勾魂粒子
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(2)) {
                Dust hook = Dust.NewDustDirect(
                    tipPos, 8, 8, DustID.GreenTorch,
                    0f, 0f, 100, default, Main.rand.NextFloat(1.0f, 1.6f)
                );
                hook.noGravity = true;
                hook.velocity = -Projectile.rotation.ToRotationVector2() * 2f + Main.rand.NextVector2Circular(1f, 1f);
            }

            //枪身暗影粒子
            if (Main.rand.NextBool(4)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center, 10, 10, DustID.Shadowflame,
                    0f, 0f, 150, default, Main.rand.NextFloat(0.6f, 1.0f)
                );
                shadow.noGravity = true;
            }

            //勾魂锁链闪光（枪尖处）
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(6)) {
                Dust chain = Dust.NewDustDirect(
                    tipPos + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.Wraith,
                    0f, -1.5f, 120, default, 1.2f
                );
                chain.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //勾魂效果：吸取灵魂恢复生命
            int healAmount = Main.rand.Next(6, 14);
            Owner.Heal(healAmount);

            //附加减速
            target.AddBuff(BuffID.Slow, 120);
            target.AddBuff(BuffID.ShadowFlame, 90);

            //勾魂特效：灵魂从敌人飞向玩家
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f);
                velocity = velocity.RotatedByRandom(MathHelper.ToRadians(25));
                Dust soul = Dust.NewDustPerfect(
                    target.Center, DustID.GreenTorch, velocity,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                soul.noGravity = true;
            }

            //击中爆发暗影焰
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, 1.3f
                );
                burst.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.4f }, target.Center);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 55f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 20f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 55f);
            Utils.PlotTileLine(start, end, 20f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() {
            return CurrentStage == AttackStage.Thrust;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0) {
                rotationOffset = MathHelper.PiOver4;
                effects = SpriteEffects.None;
            }
            else {
                rotationOffset = MathHelper.Pi - MathHelper.PiOver4;
                effects = SpriteEffects.FlipHorizontally;
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0
            );

            //突刺时绘制勾魂光效
            if (CurrentStage == AttackStage.Thrust) {
                //枪身光晕
                Color glowColor = new Color(80, 200, 120) * 0.4f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(
                    texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.1f, effects, 0
                );

                //枪尖处使用LightShot叠加勾魂光弹
                Texture2D lightShot = ACMAsset.LightShot;
                if (lightShot != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 35f - Main.screenPosition;
                    Vector2 lsOrigin = lightShot.Size() / 2f;
                    Color tipGlow = new Color(100, 255, 150) * 0.5f;
                    tipGlow.A = 0;
                    Main.EntitySpriteDraw(lightShot, tipPos, null, tipGlow, Projectile.rotation, lsOrigin, 0.5f, SpriteEffects.None, 0);
                }

                //枪尖处使用SoftGlow叠加勾魂光圈
                Texture2D softGlow = ACMAsset.SoftGlow;
                if (softGlow != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 35f - Main.screenPosition;
                    Vector2 sgOrigin = softGlow.Size() / 2f;
                    Color circleGlow = new Color(60, 220, 100) * 0.35f;
                    circleGlow.A = 0;
                    float pulse = 0.6f + MathF.Sin(Timer * 0.3f) * 0.1f;
                    Main.EntitySpriteDraw(softGlow, tipPos, null, circleGlow, 0f, sgOrigin, pulse, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}

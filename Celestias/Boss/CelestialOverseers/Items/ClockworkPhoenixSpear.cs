using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items
{
    /// <summary>
    /// 机关凤凰枪 - 天庭观察者掉落的长枪
    /// 使用手持弹幕实现长枪突刺，突刺时释放凤凰火焰
    /// </summary>
    public class ClockworkPhoenixSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 3360;
            Item.DamageType = DamageClass.Melee;
            Item.width = 56;
            Item.height = 56;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<ClockworkPhoenixSpearProjectile>();
            Item.shootSpeed = 4f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<ClockworkPhoenixSpearProjectile>()] < 1;
        }
    }

    /// <summary>
    /// 机关凤凰枪弹幕 - 手持突刺型长枪
    /// </summary>
    public class ClockworkPhoenixSpearProjectile : ModProjectile
    {
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

        private const float MaxThrustDistance = 32f;
        private const float BaseOffset = 6f;

        private Player Owner => Main.player[Projectile.owner];

        private float PrepareTime => 5f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 10f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 7f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        private int thrustCount = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 72;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
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
            SpawnPhoenixParticles();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.7f, 0.3f) * 0.6f);

            Timer++;
        }

        private void HandlePrepare() {
            ThrustDistance = MathHelper.Lerp(0, -12f, Timer / PrepareTime);

            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                thrustCount++;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            float progress = Timer / ThrustTime;
            ThrustDistance = MathHelper.SmoothStep(-12f, MaxThrustDistance, progress);

            // 突刺顶点时释放凤凰火焰
            if (Timer == (int)(ThrustTime * 0.8f)) {
                FirePhoenixFlame();
            }

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

        private void FirePhoenixFlame() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 direction = Projectile.rotation.ToRotationVector2();
            Vector2 spawnPos = Owner.MountedCenter + direction * (BaseOffset + ThrustDistance + 40f);

            // 每3次突刺发射大凤凰
            if (Main.rand.NextBool(3)) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    spawnPos,
                    direction * 16f,
                    ModContent.ProjectileType<PhoenixFlameWave>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner
                );

                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f }, spawnPos);
            }
            else {
                // 普通突刺发射小火焰
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = direction.RotatedBy(MathHelper.ToRadians(15 * i)) * 12f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        spawnPos,
                        vel,
                        ModContent.ProjectileType<PhoenixSpark>(),
                        Projectile.damage / 3,
                        Projectile.knockBack / 2,
                        Projectile.owner
                    );
                }
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

        private void SpawnPhoenixParticles() {
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 35f;
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.rotation.ToRotationVector2() * 2f + Main.rand.NextVector2Circular(1, 1);
            }

            // 凤凰羽毛粒子
            if (Main.rand.NextBool(6)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20, 20);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.OrangeTorch, 0, -1f, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 凤凰烈焰
            target.AddBuff(BuffID.OnFire3, 180);

            // 击中粒子爆发
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.5f, Volume = 0.6f }, target.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 55f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 22f, ref collisionPoint);
        }

        public override bool? CanDamage() {
            return CurrentStage == AttackStage.Thrust;
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

            // 突刺时的火焰光晕
            if (CurrentStage == AttackStage.Thrust) {
                float glowPulse = MathF.Sin(Timer * 0.3f) * 0.2f + 0.8f;
                Color glowColor = new Color(255, 150, 50) * 0.4f * glowPulse;
                glowColor.A = 0;

                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.15f, effects, 0);
            }

            // 主体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            return false;
        }
    }

    /// <summary>
    /// 凤凰火焰波 - 大型凤凰形火焰
    /// </summary>
    public class PhoenixFlameWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float wavePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.scale = 1.2f;
        }

        public override void AI() {
            wavePhase += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 凤凰飞行波动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(wavePhase * 2f) * 2f;
            Projectile.position += perpendicular * wave * 0.5f;

            // 火焰尾迹
            for (int i = 0; i < 2; i++) {
                float angle = wavePhase + i * MathHelper.Pi;
                Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;
                dustPos += perpendicular * MathF.Sin(angle) * 12f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            // 金色羽毛
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(20, 10), 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.6f, 0.2f) * 0.8f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);

            // 凤凰爆发
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.OrangeTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 凤凰尾羽拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float bodyScale = progress * (0.8f + MathF.Sin(wavePhase + i * 0.3f) * 0.2f);

                Color trailColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 100, 50), 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, new Vector2(1f * bodyScale, 0.3f * bodyScale * Projectile.scale), SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);

            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 凤凰火星 - 普通突刺释放的小火焰
    /// </summary>
    public class PhoenixSpark : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.98f;

            // 火花粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.5f, 0.2f) * 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 150, 50) * progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.rotation, origin, 0.4f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 200, 100);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, 0.5f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

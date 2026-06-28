using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans.Items
{
    /// <summary>
    /// 冰龙镰刃 - 敖闰掉落的冰系快刀
    /// 挥砍释放冰龙弧斩，命中附加霜灼与减速
    /// </summary>
    public class GlacialDragonblade : ModItem
    {
        private int slashCount;

        public override void SetDefaults() {
            Item.damage = 355;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GlacialDragonSlash>();
            Item.shootSpeed = 14f;
            Item.crit = 8;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.IceSickle;

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, player.velocity.X * 0.15f, player.velocity.Y * 0.15f, 100, default, 1.4f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            slashCount++;
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);

            Projectile.NewProjectile(source, player.Center + direction * 24f, direction * Item.shootSpeed,
                type, damage, knockback, player.whoAmI);

            if (slashCount >= 3) {
                slashCount = 0;
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.15f, Volume = 0.85f }, player.Center);

                Projectile.NewProjectile(source, player.Center + direction * 30f, direction * (Item.shootSpeed + 2f),
                    type, (int)(damage * 1.35f), knockback * 1.2f, player.whoAmI, 1f);
            }

            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = direction.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(player.Center + direction * 20f, 0, 0, dustType, dustVel.X, dustVel.Y, 120, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 360);
            target.AddBuff(BuffID.Slow, 120);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoyuanLore", "西海龙王寒鳞所铸的快刃"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect", "挥砍释放冰龙弧斩"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect2", "每三刀释放强化龙息弧波，并迸射追踪霜晶碎片"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect3", "命中附加霜灼与霜冻减速；强化斩对受冻敌人造成碎裂暴伤"));
        }
    }

    /// <summary>
    /// 冰龙弧斩 - 霜纹龙形剑气，蛇形弧迹向前斩出
    /// </summary>
    public class GlacialDragonSlash : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Empowered => ref Projectile.ai[0];
        private float wavePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 48;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            wavePhase += 0.14f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            float arcStrength = Empowered >= 1f ? 4.5f : 2.8f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * MathF.Sin(wavePhase * 2f) * arcStrength;

            Projectile.velocity *= Empowered >= 1f ? 0.985f : 0.97f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < (Empowered >= 1f ? 3 : 2); i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(14, 8);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, Empowered >= 1f ? 2f : 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1f, 1f);
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * (Empowered >= 1f ? 0.9f : 0.6f));
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 寒霜碎裂：对已被冰冻减速 / 霜灼的敌人，强化弧斩造成碎裂暴伤
            if (Empowered >= 1f && (target.HasBuff(BuffID.Frostburn2) || target.HasBuff(BuffID.Slow) || target.HasBuff(BuffID.Chilled) || target.HasBuff(BuffID.Frozen))) {
                modifiers.SourceDamage *= 1.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, Empowered >= 1f ? 480 : 300);
            target.AddBuff(BuffID.Slow, Empowered >= 1f ? 180 : 90);

            AoyuanHelper.CreateIceBurst(target.Center, 60f, 2, 10);

            // 强化弧斩迸发追踪霜晶碎片
            if (Empowered >= 1f && Main.myPlayer == Projectile.owner) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = (Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy((i - 1) * 0.5f)) * 9f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                        ModContent.ProjectileType<GlacialIceShard>(), (int)(Projectile.damage * 0.45f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scaleMult = Empowered >= 1f ? 1.15f : 0.85f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.IceCrystalWhite, AoyuanHelper.DeepSeaBlue, 1f - progress);
                trailColor *= progress * (Empowered >= 1f ? 0.75f : 0.55f);
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1.1f * progress * scaleMult, 0.28f * progress * scaleMult), SpriteEffects.None, 0f);
            }

            Color mainColor = Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.IceCrystalWhite, 0.35f);
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin,
                new Vector2(1.2f * scaleMult, 0.32f * scaleMult), SpriteEffects.None, 0f);

            Color coreColor = AoyuanHelper.IceCrystalWhite * 0.85f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin,
                new Vector2(0.85f * scaleMult, 0.16f * scaleMult), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>霜晶碎片 — 冰龙强化弧斩迸射的追踪冰刺。</summary>
    public class GlacialIceShard : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float homingStrength;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (homingStrength < 0.14f) {
                homingStrength += 0.006f;
            }

            NPC target = FindClosestNPC(520f);
            if (target != null) {
                Vector2 toTarget = Projectile.DirectionTo(target.Center);
                float speed = Math.Max(Projectile.velocity.Length(), 10f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * speed, homingStrength);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool()) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);
            target.AddBuff(BuffID.Slow, 90);
            AoyuanHelper.CreateIceBurst(target.Center, 40f, 2, 8);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color c = Color.Lerp(AoyuanHelper.IceCrystalWhite, AoyuanHelper.DeepSeaBlue, 1f - progress) * progress * 0.5f;
                c.A = 0;
                Main.spriteBatch.Draw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition, null, c, 0f, origin, 0.45f * progress, SpriteEffects.None, 0f);
            }

            Color core = AoyuanHelper.IceCrystalWhite * 0.8f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, core, 0f, origin, 0.4f, SpriteEffects.None, 0f);
            return false;
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(this)) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }
    }

    /// <summary>
    /// 永冻三叉戟 - 敖闰掉落的矛系控场武器
    /// 永冻戳刺后投出，回收路径上附加霜冻减速与水冰双属性
    /// </summary>
    public class PermafrostTrident : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 360;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item19;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<PermafrostTridentProjectile>();
            Item.shootSpeed = 17f;
            Item.crit = 10;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Trident;

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<PermafrostTridentProjectile>()] < 1;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(4)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(42, 42);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Water,
                    1 => DustID.IceTorch,
                    _ => DustID.FrostStaff
                };
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1f, 140, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoyuanLore", "西海永冻之戟，寒潮与冰晶同铸"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect", "戳刺后投出三叉戟，自动回收"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect2", "回收路径伤害提升，命中附加霜冻减速"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect3", "飞行顶点插地展开永冻领域，减速并冻伤其中敌人"));
        }
    }

    /// <summary>
    /// 永冻三叉戟弹幕 - 戳刺、投出、回收三阶段
    /// </summary>
    public class PermafrostTridentProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private enum TridentPhase { Thrust, Flying, Returning }

        private TridentPhase Phase {
            get => (TridentPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float PhaseTimer => ref Projectile.ai[1];
        private float frostPhase;

        private const float ThrustDuration = 8f;
        private const float MaxThrustExtend = 92f;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.ownerHitCheck = true;
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = TridentPhase.Thrust;
            PhaseTimer = 0f;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            frostPhase += 0.12f;
            PhaseTimer++;

            switch (Phase) {
                case TridentPhase.Thrust:
                    HandleThrust();
                    break;
                case TridentPhase.Flying:
                    HandleFlying();
                    break;
                case TridentPhase.Returning:
                    HandleReturning();
                    break;
            }

            SpawnFrostWaterDust();
            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.65f);
        }

        private void HandleThrust() {
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            float progress = PhaseTimer / ThrustDuration;
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            if (progress >= 1f) {
                Phase = TridentPhase.Flying;
                PhaseTimer = 0f;
                Projectile.velocity = direction * 18f;
                Projectile.netUpdate = true;
                return;
            }

            float extend = AoyuanHelper.QuadOut(progress) * MaxThrustExtend;
            Projectile.rotation = direction.ToRotation() + MathHelper.PiOver4;
            Projectile.Center = Owner.MountedCenter + direction * (26f + extend);
            Owner.direction = direction.X >= 0 ? 1 : -1;

            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
        }

        private void HandleFlying() {
            Owner.heldProj = -1;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.velocity *= 0.985f;

            if (PhaseTimer > 28 || Projectile.velocity.Length() < 5f) {
                Phase = TridentPhase.Returning;
                PhaseTimer = 0f;
                Projectile.netUpdate = true;

                // 永冻领域：三叉戟飞行到顶点后插地展开冰封领域，减速并冻伤驻足敌人
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<PermafrostField>(), Math.Max(1, (int)(Projectile.damage * 0.3f)), 0f, Projectile.owner);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                    AoyuanHelper.CreateFrostVortex(Projectile.Center, 60f, 1.1f, 24);
                }
            }
        }

        private void HandleReturning() {
            Vector2 toOwner = Owner.Center - Projectile.Center;
            float distance = toOwner.Length();

            if (distance < 28f) {
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.1f, Volume = 0.6f }, Owner.Center);
                }
                Projectile.Kill();
                return;
            }

            float returnSpeed = 19f + PhaseTimer * 0.25f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.11f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        private void SpawnFrostWaterDust() {
            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(2)) {
                return;
            }

            int dustType = Main.rand.Next(4) switch {
                0 => DustID.Water,
                1 => DustID.IceTorch,
                2 => DustID.FrostStaff,
                _ => DustID.BlueCrystalShard
            };

            Vector2 dustVel = Phase == TridentPhase.Returning
                ? -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1f, 1f)
                : Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f);

            int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, dustVel.X, dustVel.Y, 130, default, 1.5f);
            Main.dust[dust].noGravity = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Phase == TridentPhase.Returning) {
                modifiers.SourceDamage *= 1.4f;
            }
            else if (Phase == TridentPhase.Thrust) {
                modifiers.SourceDamage *= 1.15f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, Phase == TridentPhase.Returning ? 180 : 120);
            target.AddBuff(BuffID.Chilled, 90);

            AoyuanHelper.CreateIceBurst(target.Center, 52f, 2, 10);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.IceTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != TridentPhase.Thrust) {
                return null;
            }

            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() * 36f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 22f, ref collisionPoint);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<PermafrostTrident>()].Value;
            Vector2 origin = new Vector2(tex.Width * 0.15f, tex.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(frostPhase * 2f) * 0.08f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.WestSeaTeal, AoyuanHelper.FrostCyan, 1f - progress);
                trailColor *= progress * (Phase == TridentPhase.Returning ? 0.7f : 0.5f);
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    0.75f * progress * pulse, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Lerp(AoyuanHelper.DeepSeaBlue, AoyuanHelper.IceCrystalWhite, 0.35f);
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glowColor * 0.55f * pulse, Projectile.rotation, origin,
                0.9f * pulse, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, drawPos, null, lightColor, Projectile.rotation, origin,
                Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>永冻领域 — 永冻三叉戟插地展开的冰封区域，减速并持续冻伤其中敌人。</summary>
    public class PermafrostField : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float fieldScale;
        private float runeSpin;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 150;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 210;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            fieldScale = MathHelper.Lerp(fieldScale, 1f, 0.1f);
            runeSpin += 0.02f;

            float radius = 90f * fieldScale;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > radius) continue;
                npc.AddBuff(BuffID.Slow, 30);
                npc.AddBuff(BuffID.Chilled, 30);
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius * Main.rand.NextFloat(0.4f, 1f);
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.4f * fieldScale;
                    d.velocity = new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f));
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.55f * fieldScale);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 120);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = new Vector2(targetHitbox.Center.X, targetHitbox.Center.Y);
            return Vector2.Distance(targetCenter, Projectile.Center) < 90f * fieldScale;
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Math.Min(Projectile.timeLeft / 40f, 1f);
            float radius = 90f * fieldScale;

            AoyuanHelper.DrawFrostAura(Main.spriteBatch, Projectile.Center, radius * 0.8f, runeSpin, 0.4f * fade);

            if (ACMAsset.SoftGlow != null) {
                Texture2D glow = ACMAsset.SoftGlow;
                Vector2 origin = glow.Size() / 2f;
                Color c = AoyuanHelper.DeepSeaBlue * 0.3f * fade;
                c.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, c, 0f, origin,
                    new Vector2(2.6f, 1.4f) * fieldScale, SpriteEffects.None, 0f);
            }

            if (ACMAsset.BlankStar != null) {
                Texture2D star = ACMAsset.BlankStar;
                Vector2 origin = star.Size() / 2f;
                for (int i = 0; i < 6; i++) {
                    float angle = runeSpin * 4f + MathHelper.TwoPi * i / 6f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius * 0.85f - Main.screenPosition;
                    Color c = AoyuanHelper.IceCrystalWhite * 0.5f * fade;
                    c.A = 0;
                    Main.spriteBatch.Draw(star, pos, null, c, angle, origin, 0.18f * fieldScale, SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 漩涡原染魔典 - 敖闰掉落的水系魔法书
    /// 释放三枚水墨漩涡 orb，蛇形追踪最近敌人
    /// </summary>
    public class VortexPrimordialStain : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 365;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 12;
            Item.shoot = ModContent.ProjectileType<PrimordialInkVortexOrb>();
            Item.shootSpeed = 14f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 3; i++) {
                float spread = MathHelper.ToRadians(-14f + i * 14f);
                Vector2 orbVelocity = direction.RotatedBy(spread) * Item.shootSpeed;
                int projIndex = Projectile.NewProjectile(
                    source,
                    player.Center + direction * 24f,
                    orbVelocity,
                    type,
                    damage,
                    knockback,
                    player.whoAmI);

                if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                    Main.projectile[projIndex].ai[1] = i * MathHelper.TwoPi / 3f;
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Vector2 dustVel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
                    int dustType = Main.rand.NextBool() ? DustID.FrostStaff : DustID.IceTorch;
                    int dust = Dust.NewDust(player.Center + direction * 24f, 0, 0, dustType, dustVel.X, dustVel.Y, 120, default, 1.6f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoyuanLore", "西海原初之墨凝成的漩涡魔典"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect", "释放三枚水墨漩涡 orb，蛇形追踪敌人"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect2", "命中烙下墨染印记，对受印记敌人额外侵蚀增伤"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BookofSkulls;
    }

    /// <summary>
    /// 原初墨漩 orb - 蛇形追踪的水墨漩涡弹幕
    /// ai[0]: 蛇行相位, ai[1]: 相位偏移
    /// </summary>
    public class PrimordialInkVortexOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float serpentPhase;
        private float vortexAngle;
        private float homingStrength;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 210;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            serpentPhase += 0.16f;
            vortexAngle += 0.22f;

            if (homingStrength < 0.11f) {
                homingStrength += 0.0035f;
            }

            NPC target = FindClosestNPC(640f);
            if (target != null) {
                Vector2 toTarget = Projectile.DirectionTo(target.Center);
                float speed = Math.Max(Projectile.velocity.Length(), 12f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * speed, homingStrength);
            }

            Vector2 travelDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perpendicular = travelDir.RotatedBy(MathHelper.PiOver2);
            float waveOffset = MathF.Sin((serpentPhase + Projectile.ai[1]) * 2.4f) * 2.6f;
            Projectile.position += perpendicular * waveOffset;
            Projectile.rotation = travelDir.ToRotation();

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    float orbitAngle = vortexAngle + MathHelper.TwoPi * i / 2f + Projectile.ai[1];
                    float orbitRadius = 14f + MathF.Sin(vortexAngle * 2f + i) * 4f;
                    Vector2 orbitPos = Projectile.Center + orbitAngle.ToRotationVector2() * orbitRadius;
                    int dustType = Main.rand.NextBool(3) ? DustID.FrostStaff : DustID.IceTorch;
                    var d = Dust.NewDustPerfect(orbitPos, dustType);
                    d.noGravity = true;
                    d.scale = 1.4f + Main.rand.NextFloat(0.6f);
                    d.velocity = (orbitAngle + MathHelper.PiOver2).ToRotationVector2() * 3.5f;
                    d.alpha = 110;
                }

                if (Main.rand.NextBool(2)) {
                    var trail = Dust.NewDustPerfect(
                        Projectile.Center - travelDir * 10f + Main.rand.NextVector2Circular(6f, 6f),
                        DustID.FrostStaff,
                        -travelDir * 0.4f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        140,
                        default,
                        1.2f);
                    trail.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.DeepSeaBlue.ToVector3() * 0.55f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 墨染标记的敌人受到漩涡额外侵蚀伤害
            if (target.HasBuff(ModContent.BuffType<PrimordialInkMark>())) {
                modifiers.SourceDamage *= 1.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 180);
            target.AddBuff(ModContent.BuffType<PrimordialInkMark>(), 300);

            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                int dustType = Main.rand.NextBool() ? DustID.FrostStaff : DustID.IceTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.2f, Volume = 0.5f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(serpentPhase * 2f) * 0.18f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.FrostCyan, progress * 0.7f);
                trailColor *= progress * 0.35f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, 0.45f * progress * pulse, SpriteEffects.None, 0f);
            }

            Color outerColor = AoyuanHelper.DeepSeaBlue * 0.45f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, vortexAngle, origin, 0.95f * pulse, SpriteEffects.None, 0f);

            Color midColor = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.WestSeaTeal, 0.5f) * 0.55f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, -vortexAngle * 0.7f, origin, 0.58f * pulse, SpriteEffects.None, 0f);

            Color coreColor = AoyuanHelper.IceCrystalWhite * 0.75f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.28f * pulse, SpriteEffects.None, 0f);

            AoyuanHelper.DrawFrostAura(Main.spriteBatch, Projectile.Center, 24f, vortexAngle, 0.55f * pulse);
            return false;
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(this)) {
                    continue;
                }

                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }
    }

    /// <summary>墨染印记 — 漩涡魔典烙下的侵蚀标记，持续削蚀生命。</summary>
    public class PrimordialInkMark : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }

            npc.lifeRegen -= 14;

            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(6)) {
                return;
            }

            Vector2 offset = Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.3f);
            var d = Dust.NewDustPerfect(npc.Center + offset, DustID.FrostStaff, Main.rand.NextVector2Circular(1.2f, 1.2f), 120, default, 1.3f);
            d.noGravity = true;
            d.color = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.DeepSeaBlue, Main.rand.NextFloat());
        }
    }

    /// <summary>
    /// 墨鳞流风扇 - 敖闰掉落的水系魔法扇
    /// 扇形涌出墨鳞游鱼，命中叠加潮涌 DoT
    /// </summary>
    public class InkscaledFlowFan : ModItem
    {
        private const int FishCount = 5;
        private const float FanSpreadRadians = 1.2566371f; // Pi / 5

        public override void SetDefaults() {
            Item.damage = 370;
            Item.DamageType = DamageClass.Magic;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item46;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 10;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<InkscaledFlowFanProj>();
            Item.shootSpeed = 13f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicMirror;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < FishCount; i++) {
                float spread = FishCount == 1
                    ? 0f
                    : MathHelper.Lerp(-FanSpreadRadians * 0.5f, FanSpreadRadians * 0.5f, i / (float)(FishCount - 1));
                Vector2 fishVelocity = direction.RotatedBy(spread) * Item.shootSpeed;
                float spawnOffset = 18f + i * 4f;

                int projIndex = Projectile.NewProjectile(
                    source,
                    player.Center + direction * spawnOffset,
                    fishVelocity,
                    type,
                    damage,
                    knockback,
                    player.whoAmI,
                    0f,
                    i * MathHelper.TwoPi / FishCount);

                if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                    Main.projectile[projIndex].ai[1] = i * MathHelper.TwoPi / FishCount;
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    float angle = direction.ToRotation() + Main.rand.NextFloat(-FanSpreadRadians * 0.55f, FanSpreadRadians * 0.55f);
                    Vector2 dustVel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
                    int dustType = Main.rand.NextBool(3) ? DustID.Wet : DustID.FrostStaff;
                    int dust = Dust.NewDust(player.Center + direction * 20f, 0, 0, dustType, dustVel.X, dustVel.Y, 120, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoyuanLore", "西海墨鳞凝成的流风扇"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect", "扇形涌出墨鳞游鱼，逐敌而游"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect2", "命中叠加潮涌层数，层数越高侵蚀越烈，满层引爆潮汐迸发"));
        }
    }

    /// <summary>潮涌 DoT — 墨鳞游鱼命中后留下的潮汐侵蚀。</summary>
    public class InkscaledFlowFanDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }

            int stacks = npc.GetGlobalNPC<InkscaledStackNPC>().tideStacks;
            npc.lifeRegen -= 10 + stacks * 4;

            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(5)) {
                return;
            }

            Vector2 offset = Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.3f);
            int dustType = Main.rand.NextBool(3) ? DustID.Wet : DustID.FrostStaff;
            var d = Dust.NewDustPerfect(npc.Center + offset, dustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 1.4f + stacks * 0.1f);
            d.noGravity = true;
            d.color = Color.Lerp(AoyuanHelper.DeepSeaBlue, AoyuanHelper.WestSeaTeal, Main.rand.NextFloat());
        }
    }

    /// <summary>潮涌层数追踪 — 墨鳞游鱼叠加潮涌，满层引爆潮汐迸发。</summary>
    public class InkscaledStackNPC : GlobalNPC
    {
        public const int MaxStacks = 6;

        public override bool InstancePerEntity => true;

        public int tideStacks;
        public int decayTimer;

        public static void AddStack(NPC npc, int owner, int baseDamage) {
            if (!npc.active || npc.friendly || npc.dontTakeDamage) return;

            var s = npc.GetGlobalNPC<InkscaledStackNPC>();
            s.tideStacks = Math.Min(MaxStacks, s.tideStacks + 1);
            s.decayTimer = 300;

            if (s.tideStacks >= MaxStacks) {
                s.tideStacks = 0;
                s.decayTimer = 0;
                TidalBurst(npc, baseDamage);
            }
        }

        private static void TidalBurst(NPC center, int baseDamage) {
            Vector2 c = center.Center;
            int dmg = (int)(baseDamage * 1.8f);
            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f, Volume = 0.85f }, c);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (NPC other in Main.ActiveNPCs) {
                    if (!other.CanBeChasedBy()) continue;
                    if (Vector2.Distance(other.Center, c) > 170f) continue;
                    other.SimpleStrikeNPC(dmg, 0, false, 2f);
                    other.AddBuff(BuffID.Wet, 240);
                    other.AddBuff(ModContent.BuffType<InkscaledFlowFanDebuff>(), 240);
                }
            }

            if (Main.dedServ) return;

            AoyuanHelper.CreateFrostVortex(c, 150f, 1.4f, 32);
            for (int i = 0; i < 22; i++) {
                float a = MathHelper.TwoPi * i / 22f;
                var d = Dust.NewDustPerfect(c, Main.rand.NextBool() ? DustID.Wet : DustID.FrostStaff, a.ToRotationVector2() * Main.rand.NextFloat(5f, 11f), 100, default, 2f);
                d.noGravity = true;
            }
        }

        public override void AI(NPC npc) {
            if (tideStacks <= 0) return;
            if (--decayTimer <= 0) {
                tideStacks = Math.Max(0, tideStacks - 1);
                decayTimer = 120;
            }
        }

        public override void OnKill(NPC npc) {
            tideStacks = 0;
            decayTimer = 0;
        }
    }

    /// <summary>
    /// 墨鳞游鱼 - 扇形涌出的水墨游鱼弹幕
    /// ai[1]: 游动相位偏移
    /// </summary>
    public class InkscaledFlowFanProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float swimPhase;
        private float homingStrength;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            swimPhase += 0.18f;

            if (homingStrength < 0.1f) {
                homingStrength += 0.004f;
            }

            NPC target = FindClosestNPC(520f);
            if (target != null) {
                Vector2 toTarget = Projectile.DirectionTo(target.Center);
                float speed = Math.Max(Projectile.velocity.Length(), 11f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * speed, homingStrength);
            }

            Vector2 travelDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perpendicular = travelDir.RotatedBy(MathHelper.PiOver2);
            float tailWiggle = MathF.Sin((swimPhase + Projectile.ai[1]) * 3.2f) * 2.4f;
            Projectile.position += perpendicular * tailWiggle;
            Projectile.rotation = travelDir.ToRotation();

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 tailPos = Projectile.Center - travelDir * (8f + i * 6f) + Main.rand.NextVector2Circular(4f, 4f);
                    int dustType = Main.rand.NextBool(3) ? DustID.Wet : DustID.FrostStaff;
                    var d = Dust.NewDustPerfect(tailPos, dustType, -travelDir * 0.25f + Main.rand.NextVector2Circular(0.6f, 0.6f), 130, default, 1.2f);
                    d.noGravity = true;
                    d.color = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.WestSeaTeal, 0.45f + i * 0.2f);
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.DeepSeaBlue.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<InkscaledFlowFanDebuff>(), 300);
            target.AddBuff(BuffID.Wet, 180);
            InkscaledStackNPC.AddStack(target, Projectile.owner, Projectile.damage);

            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
                int dustType = Main.rand.NextBool() ? DustID.Wet : DustID.FrostStaff;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.35f, Volume = 0.45f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(swimPhase * 2f + Projectile.ai[1]) * 0.12f;
            SpriteEffects flip = Projectile.velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.WestSeaTeal, progress * 0.75f);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(0.55f * progress * pulse, 0.14f * progress * pulse), flip, 0f);
            }

            Color bodyColor = Color.Lerp(AoyuanHelper.AbyssBlack, AoyuanHelper.DeepSeaBlue, 0.55f) * 0.7f * pulse;
            bodyColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, bodyColor, Projectile.rotation, origin,
                new Vector2(0.72f * pulse, 0.18f * pulse), flip, 0f);

            Color scaleColor = AoyuanHelper.FrostCyan * 0.55f * pulse;
            scaleColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos + Projectile.rotation.ToRotationVector2() * 4f, null, scaleColor, Projectile.rotation, origin,
                new Vector2(0.35f * pulse, 0.1f * pulse), flip, 0f);

            if (ACMAsset.LightShot != null) {
                Color eyeColor = AoyuanHelper.IceCrystalWhite * 0.75f * pulse;
                eyeColor.A = 0;
                Vector2 eyeOffset = Projectile.rotation.ToRotationVector2() * 10f;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos + eyeOffset, null, eyeColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 0.22f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Wet : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.4f);
                Main.dust[dust].noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(this)) {
                    continue;
                }

                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }
    }

    /// <summary>
    /// 暴雪穿云弓 - 敖闰掉落的冰系弓
    /// 发射暴雪穿云箭穿透敌阵，累计 frost 穿透计数后释放强化暴雪箭
    /// </summary>
    public class BlizzardPiercer : ModItem
    {
        public const int FrostPierceThreshold = 6;

        public override void SetDefaults() {
            Item.damage = 375;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<BlizzardPiercerArrow>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 10;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.IceBow;

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<BlizzardPiercerArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var piercerPlayer = player.GetModPlayer<BlizzardPiercerPlayer>();
            bool blizzardSurge = piercerPlayer.ConsumeBlizzardSurge();
            float ai0 = blizzardSurge ? 1f : 0f;
            int arrowDamage = blizzardSurge ? (int)(damage * 1.45f) : damage;

            Projectile.NewProjectile(source, position, velocity, type, arrowDamage, knockback, player.whoAmI, ai0);

            if (blizzardSurge) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.15f, Volume = 0.85f }, player.Center);

                if (Main.netMode != NetmodeID.Server) {
                    AoyuanHelper.CreateFrostVortex(player.Center + velocity.SafeNormalize(Vector2.UnitX) * 28f, 48f, 0.8f, 24);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int dust = Dust.NewDust(position, 0, 0, dustType, dustVel.X, dustVel.Y, 120, default, blizzardSurge ? 2f : 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoyuanLore", "西海暴雪凝成的穿云神弓"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect", "发射穿透敌阵的暴雪穿云箭"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect2", $"累计 {FrostPierceThreshold} 次 frost 穿透后，下一箭释放强化暴雪箭"));
            tooltips.Add(new TooltipLine(Mod, "AoyuanEffect3", "强化暴雪箭命中时召落天降冰锥，洞穿目标"));
        }
    }

    /// <summary>
    /// 追踪 frost 穿透计数，满计数后下一射触发暴雪 surge
    /// </summary>
    public class BlizzardPiercerPlayer : ModPlayer
    {
        public int FrostPierceCount { get; private set; }
        public bool PendingBlizzardSurge { get; private set; }

        public void RegisterFrostPierce() {
            if (PendingBlizzardSurge) {
                return;
            }

            FrostPierceCount++;
            if (FrostPierceCount >= BlizzardPiercer.FrostPierceThreshold) {
                FrostPierceCount = 0;
                PendingBlizzardSurge = true;
            }
        }

        public bool ConsumeBlizzardSurge() {
            if (!PendingBlizzardSurge) {
                return false;
            }

            PendingBlizzardSurge = false;
            return true;
        }
    }

    /// <summary>
    /// 暴雪穿云箭 - 高穿透冰箭，命中叠加 frost 穿透计数
    /// ai[0]: 1 = 强化暴雪 surge 箭
    /// </summary>
    public class BlizzardPiercerArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FrostArrow;

        private ref float BlizzardSurge => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            if (BlizzardSurge >= 1f && Projectile.penetrate < 10) {
                Projectile.penetrate = 10;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.Server) {
                int dustCount = BlizzardSurge >= 1f ? 3 : 2;
                for (int i = 0; i < dustCount; i++) {
                    Vector2 dustPos = Projectile.Center - Projectile.velocity * (0.4f + i * 0.25f);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, BlizzardSurge >= 1f ? 2f : 1.4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f);
                }

                if (BlizzardSurge >= 1f && Main.rand.NextBool(3)) {
                    var cloud = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                        DustID.Cloud,
                        Main.rand.NextVector2Circular(1.5f, 1.5f),
                        120,
                        AoyuanHelper.IceCrystalWhite,
                        1.6f);
                    cloud.noGravity = true;
                }
            }

            float lightStrength = BlizzardSurge >= 1f ? 0.9f : 0.55f;
            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * lightStrength);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                owner.GetModPlayer<BlizzardPiercerPlayer>().RegisterFrostPierce();
            }

            int frostDuration = BlizzardSurge >= 1f ? 420 : 240;
            target.AddBuff(BuffID.Frostburn2, frostDuration);
            target.AddBuff(BuffID.Slow, BlizzardSurge >= 1f ? 120 : 60);

            // 暴雪 surge 箭命中召落天降冰锥
            if (BlizzardSurge >= 1f && Main.myPlayer == Projectile.owner) {
                for (int i = 0; i < 4; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-90f, 90f), -Main.rand.NextFloat(360f, 520f));
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(15f, 20f));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel,
                        ModContent.ProjectileType<BlizzardIcicle>(), (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.3f, Projectile.owner);
                }
            }

            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            float burstRadius = BlizzardSurge >= 1f ? 90f : 55f;
            int burstRings = BlizzardSurge >= 1f ? 3 : 2;
            AoyuanHelper.CreateIceBurst(target.Center, burstRadius, burstRings, 12);

            for (int i = 0; i < (BlizzardSurge >= 1f ? 14 : 8); i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity) * (BlizzardSurge >= 1f ? 0.12f : 0.06f);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.IceCrystalWhite, AoyuanHelper.DeepSeaBlue, 1f - progress);
                trailColor *= progress * (BlizzardSurge >= 1f ? 0.75f : 0.55f);
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress * pulse, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.IceCrystalWhite, 0.35f) * (BlizzardSurge >= 1f ? 0.65f : 0.45f);
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.25f * pulse, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            if (BlizzardSurge >= 1f) {
                AoyuanHelper.DrawFrostAura(Main.spriteBatch, Projectile.Center, 20f, Main.GlobalTimeWrappedHourly * 3f, 0.45f * pulse);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>天降冰锥 — 强化暴雪箭命中后召落的穿刺冰锥。</summary>
    public class BlizzardIcicle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 24f) Projectile.velocity.Y = 24f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool()) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);
            target.AddBuff(BuffID.Slow, 60);
            AoyuanHelper.CreateIceBurst(target.Center, 44f, 2, 8);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color c = Color.Lerp(AoyuanHelper.IceCrystalWhite, AoyuanHelper.DeepSeaBlue, 1f - progress) * progress * 0.5f;
                c.A = 0;
                Main.spriteBatch.Draw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition, null, c,
                    Projectile.rotation, origin, new Vector2(0.3f, 0.55f) * progress, SpriteEffects.None, 0f);
            }

            Color core = AoyuanHelper.IceCrystalWhite * 0.85f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, core, Projectile.rotation, origin,
                new Vector2(0.32f, 0.6f), SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff, Main.rand.NextVector2Circular(3f, 3f));
                d.noGravity = true;
                d.scale = 1.4f;
            }
        }
    }
}

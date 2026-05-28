using AncientChineseMythology;
using AncientChineseMythology.Celestias.Boss.Aoshuns;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns.Items
{
    /// <summary>
    /// 雷尊方天戟 — 敖顺掉落的长戟类近战武器。
    /// 雷击突刺，命中与戳刺顶点向附近敌人弹射连锁闪电弧。
    /// </summary>
    public class ThunderlordHalberd : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 370;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<ThunderlordHalberdThrust>();
            Item.shootSpeed = 18f;
            Item.crit = 8;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoshunLore", "北海雷尊敖顺所持的雷霆方天戟"));
            tooltips.Add(new TooltipLine(Mod, "AoshunEffect", "突刺贯穿敌人，向周围弹射连锁闪电弧"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Gungnir;
    }

    /// <summary>雷尊方天戟突刺 — 手持突刺弹幕。</summary>
    public class ThunderlordHalberdThrust : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Gungnir;

        private float thrustProgress;
        private const float MaxExtend = 140f;
        private bool releasedPeakArc;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            thrustProgress += 0.1f;
            float extend;
            if (thrustProgress < 0.5f) {
                extend = ACMUtils.QuadOut(thrustProgress * 2f) * MaxExtend;
            }
            else {
                extend = (1f - ACMUtils.QuadIn((thrustProgress - 0.5f) * 2f)) * MaxExtend;
            }

            if (thrustProgress >= 1f) {
                Projectile.Kill();
                return;
            }

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation() + MathHelper.PiOver4;
            Projectile.Center = Owner.MountedCenter + direction * (-60f + extend);

            Owner.direction = direction.X >= 0 ? 1 : -1;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, direction.ToRotation() - MathHelper.PiOver2);

            if (thrustProgress >= 0.45f && thrustProgress < 0.55f && !releasedPeakArc) {
                releasedPeakArc = true;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.75f }, Projectile.Center);
                ThunderlordHalberdChain.SpawnArcs(Projectile.GetSource_FromThis(), Projectile.Center,
                    Projectile.damage, Projectile.knockBack, Projectile.owner, -1, 0, 2);

                if (!VaultUtils.isServer) {
                    AoshunHelper.CreateThunderBurst(Projectile.Center, 90f, 2, 12);
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + direction * 20f, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.6f;
                d.color = AoshunHelper.LightningBlue;
                d.velocity = direction * 2f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.55f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 240);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f, Volume = 0.65f }, target.Center);
            ThunderlordHalberdChain.SpawnArcs(Projectile.GetSource_FromThis(), target.Center,
                Projectile.damage, Projectile.knockBack, Projectile.owner, target.whoAmI, 0, 4);

            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(target.Center, 80f, 2, 10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRot = Projectile.rotation + (Owner.direction > 0 ? 0 : MathHelper.PiOver2);

            Color glowColor = AoshunHelper.LightningBlue * 0.45f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, drawRot, origin, Projectile.scale * 1.2f, effects, 0f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, drawRot, origin, Projectile.scale, effects, 0f);
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 55f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 34f, ref collisionPoint);
        }
    }

    /// <summary>雷尊方天戟连锁闪电弧 — 在敌人间弹跳。</summary>
    public class ThunderlordChainArc : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float ChainDepth => ref Projectile.ai[1];
        private ref float ExcludeNpcIndex => ref Projectile.ai[2];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 28;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            int targetId = (int)TargetIndex;
            if (targetId >= 0 && targetId < Main.maxNPCs) {
                NPC target = Main.npc[targetId];
                if (target.active && target.CanBeChasedBy()) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    if (toTarget.Length() > 8f) {
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * 16f, 0.18f);
                    }
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.3f;
                d.color = AoshunHelper.LightningBlue;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 180);

            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(target.Center, 60f, 2, 8);
            }

            int depth = (int)ChainDepth;
            if (depth < 3) {
                ThunderlordHalberdChain.SpawnArcs(Projectile.GetSource_FromThis(), target.Center,
                    Projectile.damage, Projectile.knockBack, Projectile.owner, target.whoAmI, depth + 1, 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, progress) * (progress * 0.65f);
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color core = AoshunHelper.ElectricWhite;
            core.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, core, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    internal static class ThunderlordHalberdChain
    {
        public static void SpawnArcs(IEntitySource source, Vector2 origin, int damage, float knockback, int owner,
            int excludeNpc, int chainDepth, int maxTargets) {
            int spawned = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.whoAmI == excludeNpc) continue;
                if (Vector2.Distance(origin, npc.Center) > 360f) continue;

                Vector2 direction = (npc.Center - origin).SafeNormalize(Vector2.Zero);
                if (direction == Vector2.Zero) continue;

                if (!VaultUtils.isServer) {
                    AoshunHelper.CreateLightningTrail(origin, direction * 14f, 1.1f);
                }

                Projectile.NewProjectile(source, origin, direction * 14f,
                    ModContent.ProjectileType<ThunderlordChainArc>(),
                    Math.Max(1, (int)(damage * 0.55f)), knockback * 0.5f, owner,
                    npc.whoAmI, chainDepth, excludeNpc);

                if (++spawned >= maxTargets) break;
            }
        }
    }

    /// <summary>
    /// 风暴链鞭 — 敖顺掉落的雷链鞭。
    /// 鞭击感电敌人，并在命中目标间弹射连锁闪电链接多个敌人。
    /// </summary>
    public class StormchainWhip : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<StormchainWhipProjectile>(), 30, 4f, 4f, 24);
            Item.damage = 375;
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.crit = 10;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoshunLore", "北海雷云凝成的风暴链鞭"));
            tooltips.Add(new TooltipLine(Mod, "AoshunEffect", "鞭击感电敌人，在命中目标间弹射连锁闪电"));
            tooltips.Add(new TooltipLine(Mod, "AoshunEffect2", "同一挥击内链接多个目标，后续命中伤害递减"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ThornWhip;
    }

    /// <summary>风暴链鞭 — 闪电链式鞭击弹幕。</summary>
    public class StormchainWhipProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornWhip;

        private ref float LastHitNpc => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 24;
            Projectile.WhipSettings.RangeMultiplier = 1.75f;
            Projectile.DamageType = DamageClass.SummonMeleeSpeed;
        }

        public override void OnSpawn(IEntitySource source) {
            LastHitNpc = 0f;
        }

        public override bool PreAI() {
            List<Vector2> points = Projectile.WhipPointsForCollision;
            points.Clear();
            Projectile.FillWhipControlPoints(Projectile, points);

            if (points.Count < 3) return true;

            Projectile.GetWhipSettings(Projectile, out _, out _, out _);
            float swingProgress = Projectile.ai[0] / (Main.player[Projectile.owner].itemAnimationMax * Projectile.MaxUpdates);
            if (Utils.GetLerpValue(0.12f, 0.72f, swingProgress, true) * Utils.GetLerpValue(0.92f, 0.72f, swingProgress, true) <= 0.45f)
                return true;

            int segment = Main.rand.Next(Math.Max(1, points.Count - 8), points.Count - 1);
            Vector2 pos = points[segment];
            Vector2 tangent = points[segment] - points[segment - 1];

            if (!VaultUtils.isServer) {
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(6f, 6f), DustID.Electric);
                d.noGravity = true;
                d.scale = 1.4f;
                d.color = AoshunHelper.LightningBlue;
                d.velocity = tangent.SafeNormalize(Vector2.Zero).RotatedBy(Main.player[Projectile.owner].direction * MathHelper.PiOver2) * 0.25f
                             + Main.rand.NextVector2Circular(1.2f, 1.2f);
            }

            if (segment >= points.Count - 3) {
                Lighting.AddLight(pos, AoshunHelper.LightningBlue.ToVector3() * 0.55f);
            }

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 300);
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;

            int previousTarget = (int)LastHitNpc - 1;
            StormchainWhipChain.ChainFromHit(Projectile.GetSource_FromThis(), target,
                Projectile.damage, Projectile.knockBack, Projectile.owner, previousTarget);

            LastHitNpc = target.whoAmI + 1f;

            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(target.Center, 70f, 2, 8);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.45f, Volume = 0.6f }, target.Center);
            Projectile.damage = Math.Max(1, (int)(Projectile.damage * 0.68f));
        }

        public override bool PreDraw(ref Color lightColor) {
            List<Vector2> points = [];
            Projectile.FillWhipControlPoints(Projectile, points);
            Main.DrawWhip_WhipBland(Projectile, points);

            if (ACMAsset.LightShot == null || points.Count < 2) return false;

            Texture2D spark = ACMAsset.LightShot;
            Vector2 origin = spark.Size() / 2f;
            for (int i = 1; i < points.Count - 1; i += 3) {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
                float rot = (b - a).ToRotation();
                float pulse = 0.55f + 0.15f * MathF.Sin(i * 0.8f + (float)Main.timeForVisualEffects * 0.18f);
                Color glow = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, i / (float)points.Count) * pulse;
                glow.A = 0;
                Main.spriteBatch.Draw(spark, mid, null, glow, rot, origin, new Vector2(0.35f, 0.12f), SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    internal static class StormchainWhipChain
    {
        public static void ChainFromHit(IEntitySource source, NPC primary, int damage, float knockback, int owner, int previousTargetIdx) {
            if (previousTargetIdx >= 0 && previousTargetIdx < Main.maxNPCs) {
                NPC previous = Main.npc[previousTargetIdx];
                if (previous.active && previous.CanBeChasedBy() && previous.whoAmI != primary.whoAmI) {
                    LinkTargets(source, previous, primary, damage, knockback, owner);
                }
            }

            int chained = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.whoAmI == primary.whoAmI) continue;
                if (Vector2.Distance(primary.Center, npc.Center) > 340f) continue;

                LinkTargets(source, primary, npc, damage, knockback, owner);
                if (++chained >= 2) break;
            }
        }

        private static void LinkTargets(IEntitySource source, NPC from, NPC to, int damage, float knockback, int owner) {
            if (!VaultUtils.isServer) {
                AoshunHelper.CreateLightningTrail(from.Center, to.Center - from.Center, 1.15f);
                DrawChainArc(from.Center, to.Center);
            }

            to.AddBuff(BuffID.Electrified, 240);

            if (Main.myPlayer != owner) return;

            int chainDamage = Math.Max(1, (int)(damage * 0.5f));
            to.StrikeNPC(new NPC.HitInfo {
                Damage = chainDamage,
                Knockback = knockback * 0.45f,
                HitDirection = to.Center.X > from.Center.X ? 1 : -1,
                DamageType = DamageClass.SummonMeleeSpeed
            }, false, false);
        }

        private static void DrawChainArc(Vector2 start, Vector2 end) {
            int segments = Math.Max(4, (int)(Vector2.Distance(start, end) / 28f));
            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                Vector2 pos = Vector2.Lerp(start, end, t);
                Vector2 perp = (end - start).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                pos += perp * Main.rand.NextFloat(-10f, 10f) * (1f - MathF.Abs(t - 0.5f) * 1.4f);

                var d = Dust.NewDustPerfect(pos, DustID.Electric, Main.rand.NextVector2Circular(1.5f, 1.5f));
                d.noGravity = true;
                d.scale = 1.35f;
                d.color = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.ElectricWhite, t);
            }
        }
    }

    /// <summary>
    /// 暴风连弩 - 敖顺掉落的连弩类远程武器
    /// 每次齐射三支雷暴弩箭，命中叠加雷暴标记并引爆
    /// </summary>
    public class TempestRepeater : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 380;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 12;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VenusMagnum;

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<TempestBolt>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = -1; i <= 1; i++) {
                Vector2 spreadVel = velocity.RotatedBy(MathHelper.ToRadians(6f * i));
                Projectile.NewProjectile(source, position, spreadVel, type, damage, knockback, player.whoAmI);
            }

            return false;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-4f, 0f);

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AoshunLore", "以北海雷云铸成的风暴连弩"));
            tooltips.Add(new TooltipLine(Mod, "AoshunEffect", "每次齐射三支雷暴弩箭"));
            tooltips.Add(new TooltipLine(Mod, "AoshunEffect2", "命中叠加雷暴标记，三层或延时后引爆雷霆"));
        }
    }

    /// <summary>
    /// 雷敕天书 — 敖顺掉落的雷系魔法典籍。
    /// 在光标处展开符箓雷阵并周期性降霆；同步扇形射出追踪雷符，命中引发落雷。
    /// </summary>
    public class LightningEdictTome : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 385;
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
            Item.mana = 14;
            Item.shoot = ModContent.ProjectileType<LightningEdictArray>();
            Item.shootSpeed = 0f;
            Item.crit = 8;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<LightningEdictArray>()] < 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 target = Main.MouseWorld;
            Projectile.NewProjectile(source, target, Vector2.Zero, type, damage, knockback, player.whoAmI);

            Vector2 direction = (target - player.Center).SafeNormalize(Vector2.UnitX);
            for (int i = -1; i <= 1; i++) {
                Vector2 spawnOffset = direction.RotatedBy(MathHelper.PiOver2) * i * 28f;
                Vector2 talismanVel = direction.RotatedBy(MathHelper.ToRadians(9f * i)) * 11f;
                Projectile.NewProjectile(source, player.Center + spawnOffset, talismanVel,
                    ModContent.ProjectileType<LightningEdictTalisman>(), (int)(damage * 0.65f), knockback * 0.5f, player.whoAmI);
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 14; i++) {
                    float angle = MathHelper.TwoPi * i / 14f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    int dust = Dust.NewDust(player.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 80,
                        AoshunHelper.LightningBlue, 1.4f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "EdictTomeLore", "「北海雷敕，符落天惊」"));
            tooltips.Add(new TooltipLine(Mod, "EdictTomeEffect", "在光标处展开落雷符箓阵列，阵内敌人周期性遭霆击"));
            tooltips.Add(new TooltipLine(Mod, "EdictTomeEffect2", "同步射出三枚追踪雷符，命中引发落雷并感电"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BookofSkulls;
    }

    /// <summary>雷敕符 — 追踪敌人的符箓弹幕，命中降下霆击。</summary>
    public class LightningEdictTalisman : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float sealSpin;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 140;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            sealSpin += 0.18f;
            Projectile.rotation = sealSpin;

            NPC target = FindClosestNPC(420f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Math.Max(Projectile.velocity.Length(), 9f), 0.07f);
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), DustID.Electric);
                d.noGravity = true;
                d.scale = 1.1f;
                d.color = AoshunHelper.LightningBlue;
                d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 180);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 strikeStart = target.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -520f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), strikeStart, new Vector2(0, 22f),
                    ModContent.ProjectileType<LightningEdictStrike>(), Projectile.damage / 2, 0f, Projectile.owner,
                    target.Center.X, target.Center.Y);
            }

            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(target.Center, 70f, 2, 10);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 0.55f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trail = AoshunHelper.ThunderPurple * progress * 0.45f;
                trail.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trail, Projectile.oldRot[i], origin, 0.35f * progress, SpriteEffects.None, 0f);
            }

            Color glow = AoshunHelper.LightningBlue * 0.55f;
            glow.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glow, sealSpin, origin, 0.5f, SpriteEffects.None, 0f);

            Color core = AoshunHelper.ElectricWhite;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, -sealSpin * 0.6f, origin, 0.32f, SpriteEffects.None, 0f);
            return false;
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float best = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    closest = npc;
                }
            }
            return closest;
        }
    }

    /// <summary>落雷符箓阵列 — 在地面/空中展开旋转符阵并周期性霆击。</summary>
    public class LightningEdictArray : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int Duration = 240;
        private const float StrikeRadius = 130f;

        private float arrayScale;
        private float runeRotation;
        private float pulsePhase;
        private int strikeTimer;
        private int auraTimer;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            pulsePhase += 0.09f;
            runeRotation += 0.035f;
            strikeTimer++;
            auraTimer++;

            float life = 1f - Projectile.timeLeft / (float)Duration;
            if (Projectile.timeLeft > Duration - 25)
                arrayScale = MathHelper.Lerp(arrayScale, 1f, 0.1f);
            else if (Projectile.timeLeft < 35)
                arrayScale = MathHelper.Lerp(arrayScale, 0f, 0.08f);
            else
                arrayScale = 1f;

            int hitboxSize = (int)(200 * Math.Max(arrayScale, 0.2f));
            Projectile.width = Projectile.height = hitboxSize;

            if (strikeTimer >= 42 && arrayScale > 0.55f) {
                strikeTimer = 0;
                StrikeNearestEnemy();
            }

            if (auraTimer >= 16 && arrayScale > 0.4f) {
                auraTimer = 0;
                DealAuraDamage();
            }

            if (!VaultUtils.isServer)
                SpawnArrayParticles(life);

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.7f * arrayScale);
        }

        private void StrikeNearestEnemy() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            NPC target = FindClosestNPC(StrikeRadius * 1.2f);
            Vector2 strikePos = target != null
                ? target.Center
                : Projectile.Center + Main.rand.NextVector2Circular(StrikeRadius * 0.6f, StrikeRadius * 0.6f);

            Vector2 start = strikePos + new Vector2(Main.rand.NextFloat(-30f, 30f), -560f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), start, new Vector2(0, 24f),
                ModContent.ProjectileType<LightningEdictStrike>(), (int)(Projectile.damage * 0.85f), 0f, Projectile.owner,
                strikePos.X, strikePos.Y);

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.15f, Volume = 0.5f }, strikePos);
        }

        private void DealAuraDamage() {
            float radius = StrikeRadius * arrayScale;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > radius) continue;

                npc.SimpleStrikeNPC((int)(Projectile.damage * 0.18f), 0, false, 0f);
                npc.AddBuff(BuffID.Electrified, 45);

                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    var d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height), DustID.Electric);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.color = AoshunHelper.LightningBlue;
                }
            }
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float best = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        private void SpawnArrayParticles(float life) {
            float radius = StrikeRadius * 0.85f * arrayScale;
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                var d = Dust.NewDustPerfect(pos, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.3f * arrayScale;
                d.color = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, life);
                d.velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 3.5f;
            }

            if (Main.rand.NextBool(5)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.6f * arrayScale;
                d.velocity = Vector2.UnitY * -2f;
            }
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D branchTex = ACMAsset.LightningBranch ?? TextureAssets.Projectile[Type].Value;
            Texture2D glowTex = ACMAsset.SoftGlow ?? branchTex;
            Vector2 branchOrigin = branchTex.Size() / 2f;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float radius = StrikeRadius * 0.75f * arrayScale;

            AoshunHelper.DrawThunderAura(sb, Projectile.Center, radius, runeRotation, 0.35f * arrayScale);

            const int talismanCount = 6;
            for (int i = 0; i < talismanCount; i++) {
                float angle = runeRotation * 1.4f + MathHelper.TwoPi * i / talismanCount;
                float pulse = MathF.Sin(pulsePhase + angle * 2f) * 0.25f + 0.75f;
                Vector2 runePos = center + angle.ToRotationVector2() * radius;

                Color runeColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, pulse) * (0.45f * arrayScale);
                runeColor.A = 0;
                float runeScale = (0.055f + pulse * 0.025f) * arrayScale;
                sb.Draw(branchTex, runePos, null, runeColor, angle + MathHelper.PiOver2, branchOrigin, runeScale, SpriteEffects.None, 0f);
            }

            Color coreColor = AoshunHelper.ThunderPurple * (0.35f + MathF.Sin(pulsePhase * 2f) * 0.1f) * arrayScale;
            coreColor.A = 0;
            sb.Draw(glowTex, center, null, coreColor, 0f, glowOrigin, 0.55f * arrayScale, SpriteEffects.None, 0f);

            const int borderSegments = 36;
            Texture2D nodeTex = ACMAsset.BlankStar ?? branchTex;
            Vector2 nodeOrigin = nodeTex.Size() / 2f;
            for (int i = 0; i < borderSegments; i++) {
                float angle = MathHelper.TwoPi * i / borderSegments;
                float pulse = MathF.Sin(pulsePhase * 2f + angle * 5f) * 0.3f + 0.7f;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Color border = AoshunHelper.LightningBlue * pulse * 0.35f * arrayScale;
                border.A = 0;
                sb.Draw(nodeTex, pos, null, border, angle, nodeOrigin, 0.14f * arrayScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(Projectile.Center, 100f * arrayScale, 3, 14);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && arrayScale > 0.45f) {
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f + runeRotation;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * 60f;
                    Vector2 start = pos + new Vector2(0, -520f);
                    Projectile.NewProjectile(Projectile.GetSource_Death(), start, new Vector2(0, 28f),
                        ModContent.ProjectileType<LightningEdictStrike>(), Projectile.damage, 0f, Projectile.owner, pos.X, pos.Y);
                }
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.1f, Volume = 0.7f }, Projectile.Center);
        }
    }

    /// <summary>霆击 — 从天而降的锯齿落雷。</summary>
    public class LightningEdictStrike : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private Vector2 targetPos;
        private bool hasStruck;
        private readonly System.Collections.Generic.List<Vector2> lightningPath = [];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 25;
        }

        public override void OnSpawn(IEntitySource source) {
            targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            BuildLightningPath();
        }

        private void BuildLightningPath() {
            lightningPath.Clear();
            lightningPath.Add(Projectile.Center);

            Vector2 dir = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
            float dist = Vector2.Distance(Projectile.Center, targetPos);
            int segments = Math.Max(4, (int)(dist / 36f));
            for (int i = 1; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(Projectile.Center, targetPos, t);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                basePos += perp * Main.rand.NextFloat(-24f, 24f) * (1f - t * 0.5f);
                lightningPath.Add(basePos);
            }

            lightningPath.Add(targetPos);
        }

        public override void AI() {
            if (!hasStruck) {
                Projectile.velocity *= 1.04f;
                if (Vector2.Distance(Projectile.Center, targetPos) < 40f || Projectile.timeLeft < 32) {
                    hasStruck = true;
                    Projectile.Center = targetPos;
                    Projectile.velocity = Vector2.Zero;
                    StrikeImpact();
                }
            }
            else if (Projectile.timeLeft > 18) {
                Projectile.timeLeft = 18;
            }

            if (!VaultUtils.isServer && !hasStruck && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = true;
                d.scale = 1.3f;
                d.color = AoshunHelper.ElectricWhite;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.ElectricWhite.ToVector3() * (hasStruck ? 0.45f : 0.9f));
        }

        private void StrikeImpact() {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.05f, Volume = 0.75f }, targetPos);

            if (!VaultUtils.isServer) {
                AoshunHelper.CreateThunderBurst(targetPos, 85f, 2, 12);
                for (int i = 0; i < 16; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7, 7);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                    var d = Dust.NewDustPerfect(targetPos, dustType, vel);
                    d.noGravity = true;
                    d.scale = 2f;
                }
            }

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) continue;
                if (Vector2.Distance(npc.Center, targetPos) > 72f) continue;
                npc.SimpleStrikeNPC(Projectile.damage, 0, false, 0f);
                npc.AddBuff(BuffID.Electrified, 120);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (lightningPath.Count < 2) return false;

            Texture2D tex = ACMAsset.LightningBranch ?? ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            float alpha = hasStruck ? Projectile.timeLeft / 18f : 1f;

            for (int seg = 0; seg < lightningPath.Count - 1; seg++) {
                Vector2 a = lightningPath[seg] - Main.screenPosition;
                Vector2 b = lightningPath[seg + 1] - Main.screenPosition;
                Vector2 mid = (a + b) * 0.5f;
                float rot = (b - a).ToRotation();
                float len = Vector2.Distance(a, b);
                float t = seg / (float)(lightningPath.Count - 1);

                Color glow = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, t) * alpha * 0.65f;
                glow.A = 0;
                Color core = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, t) * alpha;
                core.A = 0;

                Vector2 scale = new(len / tex.Width, 0.08f);
                Main.spriteBatch.Draw(tex, mid, null, glow, rot, origin, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(tex, mid, null, core, rot, origin, scale * 0.65f, SpriteEffects.None, 0f);
            }

            if (hasStruck && ACMAsset.SoftGlow != null) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                Vector2 glowOrigin = glowTex.Size() / 2f;
                float pulse = 1f + MathF.Sin(Projectile.timeLeft * 0.6f) * 0.25f;
                Color burst = AoshunHelper.ElectricWhite * alpha;
                burst.A = 0;
                Main.spriteBatch.Draw(glowTex, targetPos - Main.screenPosition, null, burst, 0f, glowOrigin, 0.7f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? CanHitNPC(NPC target) => false;
    }

    /// <summary>
    /// 苍海毁刃 — 敖顺 apex 近战
    /// 持握挥砍，斩击中段释放蔚蓝潮汐雷浪
    /// </summary>
    public class AzureRuinBlade : ModItem
    {
        private int attackType;

        public override void SetDefaults() {
            Item.damage = 420;
            Item.crit = 15;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 70;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8.5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<AzureRuinBladeSwing>();
            Item.shootSpeed = 3f;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(5)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(48f, 48f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Electric, 0f, 0f, 80, default, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.35f;
            }

            if (Main.rand.NextBool(8)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(56f, 56f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Water, 0f, -0.6f, 60, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 2;
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "AzureRuinLore", "北海龙王敖顺陨落所铸的终局巨刃"));
            tooltips.Add(new TooltipLine(Mod, "AzureRuinEffect", "挥砍中段释放蔚蓝潮汐雷浪"));
            tooltips.Add(new TooltipLine(Mod, "AzureRuinEffect2", "雷浪命中感电敌人并连锁电弧"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    /// <summary>
    /// 苍海毁刃挥砍 — 持握旋转，斩击中段释放潮汐雷浪
    /// </summary>
    public class AzureRuinBladeSwing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;

        private const float SWING_RANGE = MathF.PI * 1.55f;
        private const float PREP_FRAC = 0.18f;
        private const float EXEC_FRAC = 0.55f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[1];
        private ref float InitAngle => ref Projectile.ai[2];
        private ref float RawProgress => ref Projectile.localAI[0];
        private int AttackDir => (int)Projectile.ai[0] == 0 ? 1 : -1;

        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private bool _waveFired;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
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
            int dir = Projectile.spriteDirection * AttackDir;

            if (dir > 0) {
                toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
                InitAngle = toMouse - SWING_RANGE * 0.55f;
            }
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
                InitAngle = toMouse + SWING_RANGE * 0.55f;
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
            int dir = Projectile.spriteDirection * AttackDir;

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f }, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                    if (!_waveFired && Timer >= execDur * 0.40f) {
                        _waveFired = true;
                        Vector2 waveDir = Owner.DirectionTo(Main.MouseWorld);
                        int waveDamage = (int)(Owner.GetTotalDamage(DamageClass.Melee).ApplyTo(Owner.HeldItem.damage) * 1.35f);
                        Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center, waveDir * 20f,
                            ModContent.ProjectileType<AzureRuinTidal>(),
                            waveDamage,
                            Owner.HeldItem.knockBack * 0.75f, Owner.whoAmI);
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.35f, Volume = 0.95f }, Owner.position);

                        if (Owner.whoAmI == Main.myPlayer) {
                            Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 12);
                        }
                    }

                    if (Timer >= execDur) CurrentStage = Stage.Unwind;
                    break;

                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.04f,
                        Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) Projectile.Kill();
                    break;
            }

            Projectile.rotation = InitAngle + dir * RawProgress;
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

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 s = Owner.MountedCenter;
            Vector2 e = s + Projectile.rotation.ToRotationVector2()
                        * Projectile.Size.Length() * Projectile.scale * 1.1f;
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                s, e, 26f * Projectile.scale, ref col);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 360);
            AoshunHelper.CreateThunderBurst(target.Center, 80f, 2, 8);

            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    Main.rand.NextVector2Circular(6f, 6f), 50, default, 2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int dir = Projectile.spriteDirection * AttackDir;
            float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute) {
                Texture2D wave = ACMAsset.GlaciateWave;
                if (wave != null) {
                    for (int i = 1; i < 14 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                        float a = (1f - i / 14f) * 0.72f;
                        float rot = Projectile.oldRot[i] + rotOff;
                        sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                            AoshunHelper.NorthSeaCyan * a, rot,
                            wave.Size() * 0.5f,
                            Projectile.scale * 0.50f, SpriteEffects.None, 0);
                        sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                            AoshunHelper.LightningBlue * (a * 0.45f), rot + 0.10f,
                            wave.Size() * 0.5f,
                            Projectile.scale * 0.34f, SpriteEffects.None, 0);
                    }
                }

                float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.24f);
                Texture2D sg = ACMAsset.SoftGlow;
                if (sg != null) {
                    sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                        AoshunHelper.ThunderPurple * 0.50f * pulse, Projectile.rotation + rotOff,
                        sg.Size() * 0.5f,
                        Projectile.scale * 2.1f, SpriteEffects.None, 0);
                }

                Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                              * Projectile.Size.Length() * Projectile.scale * 0.62f;
                Texture2D sparkle = ACMAsset.Sparkle;
                if (sparkle != null) {
                    sb.Draw(sparkle, tip - Main.screenPosition, null,
                        AoshunHelper.ElectricWhite * 0.55f,
                        (float)Main.timeForVisualEffects * 0.07f,
                        sparkle.Size() * 0.5f,
                        Projectile.scale * 0.65f, SpriteEffects.None, 0);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Item[ItemID.BreakerBlade].Value;
            SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = dir > 0
                ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation + rotOff, origin,
                Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>
    /// 蔚蓝潮汐雷浪 — 挥砍释放的雷水弧形剑气
    /// </summary>
    public class AzureRuinTidal : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/GlaciateWave";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.975f;

            float life = 1f - Projectile.timeLeft / 55f;
            Lighting.AddLight(Projectile.Center,
                AoshunHelper.LightningBlue.ToVector3() * (0.55f * (1f - life)));

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.Water;
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 16f),
                    dustType, -Projectile.velocity * 0.12f, 50, default, 1.9f);
                d.noGravity = true;
            }

            if (Main.rand.NextBool(4) && ACMAsset.LightningBranch != null) {
                Vector2 sparkPos = Projectile.Center + Main.rand.NextVector2Circular(18f, 10f);
                Dust s = Dust.NewDustPerfect(sparkPos, DustID.Electric,
                    Main.rand.NextVector2Circular(2f, 2f), 30, default, 1.2f);
                s.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 480);
            target.AddBuff(BuffID.Wet, 300);

            AoshunHelper.CreateThunderBurst(target.Center, 110f, 2, 10);

            if (Main.rand.NextBool(3)) {
                ChainToNearby(target, damageDone);
            }

            for (int i = 0; i < 14; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.Water;
                Dust d = Dust.NewDustPerfect(target.Center, dustType,
                    Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.4f);
                d.noGravity = true;
            }
        }

        private void ChainToNearby(NPC origin, int damageDone) {
            NPC chained = null;
            float bestDist = 260f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) continue;
                if (npc.whoAmI == origin.whoAmI) continue;

                float dist = Vector2.Distance(origin.Center, npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    chained = npc;
                }
            }

            if (chained == null) return;

            AoshunHelper.CreateLightningTrail(origin.Center, chained.Center - origin.Center, 1.2f);
            chained.AddBuff(BuffID.Electrified, 240);

            if (Main.myPlayer == Projectile.owner) {
                int chainDamage = Math.Max(1, (int)(damageDone * 0.45f));
                chained.StrikeNPC(new NPC.HitInfo {
                    Damage = chainDamage,
                    Knockback = 2f,
                    HitDirection = chained.Center.X > origin.Center.X ? 1 : -1,
                    DamageType = DamageClass.Melee
                }, false, false);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D lsh = ACMAsset.LightShot;
            Texture2D branch = ACMAsset.LightningBranch;

            float life = 1f - Projectile.timeLeft / 55f;
            float scaleX = MathHelper.Lerp(1.75f, 0.55f, ACMUtils.QuadIn(life));
            float scaleY = MathHelper.Lerp(0.62f, 0.20f, ACMUtils.QuadIn(life));
            float alpha = ACMUtils.QuadOut(1f - life) * 0.94f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (lsh != null) {
                for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.58f * alpha;
                    sb.Draw(lsh,
                        Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        null, AoshunHelper.LightningBlue * a, Projectile.oldRot[i],
                        lsh.Size() * 0.5f,
                        new Vector2(0.50f + i * 0.014f, 0.18f), SpriteEffects.None, 0);
                }
            }

            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                AoshunHelper.NorthSeaCyan * alpha, Projectile.rotation,
                tex.Size() * 0.5f,
                new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                AoshunHelper.LightningBlue * (alpha * 0.50f), Projectile.rotation + 0.06f,
                tex.Size() * 0.5f,
                new Vector2(scaleX * 0.78f, scaleY * 0.72f), SpriteEffects.None, 0);

            if (branch != null) {
                float forkRot = Projectile.rotation + MathF.Sin((float)Main.timeForVisualEffects * 0.18f) * 0.35f;
                sb.Draw(branch, Projectile.Center - Main.screenPosition, null,
                    AoshunHelper.ElectricWhite * (alpha * 0.35f), forkRot,
                    branch.Size() * 0.5f,
                    new Vector2(scaleX * 0.55f, scaleY * 1.4f), SpriteEffects.None, 0);
            }

            Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 48f;
            if (sg != null) {
                sb.Draw(sg, front - Main.screenPosition, null,
                    AoshunHelper.ElectricWhite * alpha * 0.78f, 0f,
                    sg.Size() * 0.5f,
                    scaleY * 2.0f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 16; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.Water;
                Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                    Main.rand.NextVector2Circular(5f, 5f), 50, default, 1.8f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 雷暴弩箭 - 暴风连弩齐射的主弹幕
    /// </summary>
    public class TempestBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool(3) ? DustID.Electric : DustID.PurpleTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 120);
            TempestStormMark.TryApplyOrStack(Projectile, target, damageDone);

            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    Main.rand.NextVector2Circular(4f, 4f), 80, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0f);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, 1f - progress);
                trailColor *= progress * 0.55f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2, origin,
                    new Vector2(0.28f * progress, 0.45f * progress), SpriteEffects.None, 0f);
            }

            Color mainColor = AoshunHelper.ElectricWhite * 0.85f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.35f, 0.55f), SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.2f);
                Main.dust[d].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 雷暴标记 - 附着在敌人身上，叠满三层或延时后引爆
    /// </summary>
    public class TempestStormMark : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int MaxStacks = 3;
        private const int DetonationDelay = 150;

        private ref float TargetNPC => ref Projectile.ai[0];
        private ref float AccumulatedDamage => ref Projectile.ai[1];
        private ref float HitStacks => ref Projectile.localAI[0];
        private ref float Timer => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DetonationDelay;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;
        public override bool? CanCutTiles() => false;

        public static void TryApplyOrStack(Projectile source, NPC target, int damageDone) {
            int markType = ModContent.ProjectileType<TempestStormMark>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile mark = Main.projectile[i];
                if (!mark.active || mark.type != markType || mark.owner != source.owner) continue;
                if ((int)mark.ai[0] != target.whoAmI) continue;

                mark.ai[1] += damageDone;
                mark.localAI[0]++;
                mark.timeLeft = DetonationDelay;

                if (mark.localAI[0] >= MaxStacks) {
                    mark.Kill();
                }

                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.45f, Pitch = 0.2f }, target.Center);
                return;
            }

            int markIdx = Projectile.NewProjectile(source.GetSource_FromThis(), target.Center, Vector2.Zero,
                markType, source.damage, 0f, source.owner, target.whoAmI, damageDone);
            if (markIdx >= 0 && markIdx < Main.maxProjectiles) {
                Main.projectile[markIdx].localAI[0] = 1f;
            }
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = 0.4f }, target.Center);
        }

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center + new Vector2(0f, -target.height * 0.65f);
            Projectile.rotation += 0.08f;

            float progress = MathHelper.Clamp(Timer / DetonationDelay, 0f, 1f);
            float stackPulse = 0.5f + HitStacks / MaxStacks * 0.5f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(12f, 28f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Electric,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 60, default, 1.2f * stackPulse);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * (0.35f + progress * 0.45f * stackPulse));
        }

        public override void OnKill(int timeLeft) {
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs) return;

            NPC target = Main.npc[targetIdx];
            if (!target.active) return;

            int bonusDamage = (int)Math.Max(AccumulatedDamage * 1.5f, Projectile.damage * 2);
            if (Main.myPlayer == Projectile.owner) {
                target.SimpleStrikeNPC(bonusDamage, 0, false, 0f, null, false, 0, true);
            }

            target.AddBuff(BuffID.Electrified, 480);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.35f }, target.Center);
            AoshunHelper.CreateThunderBurst(target.Center, 140f, 3, 14);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.active || nearby.friendly || nearby.dontTakeDamage || !nearby.CanBeChasedBy()) continue;
                if (nearby.whoAmI == targetIdx) continue;
                if (Vector2.Distance(target.Center, nearby.Center) > 260f) continue;

                nearby.AddBuff(BuffID.Electrified, 240);
                if (Main.myPlayer == Projectile.owner) {
                    nearby.SimpleStrikeNPC(Math.Max(1, bonusDamage / 2), 0, false, 0f, null, false, 0, true);
                }
            }

            if (Projectile.owner == Main.myPlayer) {
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 14);
            }

            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(target.Center, dustType, vel, 40, default, Main.rand.NextFloat(2f, 3.5f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = MathHelper.Clamp(Timer / DetonationDelay, 0f, 1f);
            float opacity = MathHelper.Clamp(Timer / 12f, 0f, 1f);
            float pulse = 0.14f + MathF.Sin(Timer * 0.25f) * 0.04f + progress * 0.06f + HitStacks * 0.03f;

            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Vector2 origin = star.Size() / 2f;
                Color markColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ThunderPurple, progress) * opacity * 0.75f;
                markColor.A = 0;
                Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, markColor,
                    Projectile.rotation, origin, pulse, SpriteEffects.None, 0);
            }

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 origin = glow.Size() / 2f;
                Color glowColor = AoshunHelper.ElectricWhite * opacity * (0.25f + progress * 0.35f);
                glowColor.A = 0;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, glowColor,
                    0f, origin, pulse * 2.2f, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}

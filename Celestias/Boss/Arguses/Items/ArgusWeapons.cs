using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses.Items
{
    internal static class ArgusWeaponFx
    {
        public static readonly Color IrisGold = new(255, 210, 80);
        public static readonly Color IrisPurple = new(180, 100, 255);
        public static readonly Color IrisBlue = new(100, 140, 255);
    }

    /// <summary>
    /// 穿魂弧弓 — 百目 Argus 掉落远程武器
    /// 将箭矢化为瞳纹追踪箭，命中叠加弱点标记并在满层时穿魂引爆
    /// </summary>
    public class SoulPiercingArc : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1150;
            Item.crit = 10;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulPiercingArrow>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<SoulPiercingArrow>();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "SoulPierceLore", "百目追魂弧凝瞳纹为弦，箭矢必贯弱点"));
            tooltips.Add(new TooltipLine(Mod, "SoulPierceEffect", "将箭矢化为瞳纹追踪箭"));
            tooltips.Add(new TooltipLine(Mod, "SoulPierceEffect2", "命中叠加弱点标记，满层穿魂引爆"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PulseBow;
    }

    /// <summary>瞳纹追踪箭 — 穿魂弧弓射出的虹彩穿魂箭</summary>
    public class SoulPiercingArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private ref float TravelTimer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            TravelTimer++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (TravelTimer > 8f) {
                NPC target = FindClosestNPC(Projectile.Center, 640f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.055f);
                }
            }

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, Vector3.Lerp(ArgusWeaponFx.IrisPurple.ToVector3(), ArgusWeaponFx.IrisGold.ToVector3(), 0.45f) * 0.4f);
        }

        private static NPC FindClosestNPC(Vector2 center, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoulPierceMark.TryApplyOrStack(Projectile, target, damageDone);
            target.AddBuff(BuffID.Ichor, 180);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.4f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2CircularEdge(4f, 4f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width * 0.5f, 0f);
            float pulse = 1f + MathF.Sin(TravelTimer * 0.3f) * 0.08f;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(ArgusWeaponFx.IrisPurple, ArgusWeaponFx.IrisGold, progress) * (0.55f * progress);
                trailColor.A = 0;

                Main.spriteBatch.Draw(
                    tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.oldRot[i] + MathHelper.PiOver2,
                    origin,
                    new Vector2(0.35f * progress, 0.12f) * pulse,
                    SpriteEffects.None,
                    0f);
            }

            Color mainColor = Color.Lerp(ArgusWeaponFx.IrisGold, Color.White, 0.25f);
            mainColor.A = 0;
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                mainColor,
                Projectile.rotation,
                origin,
                new Vector2(0.5f, 0.14f) * pulse,
                SpriteEffects.None,
                0f);

            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = ArgusWeaponFx.IrisBlue * 0.35f;
                sparkleColor.A = 0;
                Main.spriteBatch.Draw(
                    ACMAsset.Sparkle,
                    Projectile.Center - Main.screenPosition,
                    null,
                    sparkleColor,
                    Projectile.rotation * 0.5f,
                    ACMAsset.Sparkle.Size() / 2f,
                    0.45f * pulse,
                    SpriteEffects.None,
                    0f);
            }

            return false;
        }
    }

    /// <summary>穿魂弱点标记 — 叠层后穿魂引爆</summary>
    public class SoulPierceMark : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int MaxStacks = 5;
        private const int DetonationDelay = 180;

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
            int markType = ModContent.ProjectileType<SoulPierceMark>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile mark = Main.projectile[i];
                if (!mark.active || mark.type != markType || mark.owner != source.owner)
                    continue;
                if ((int)mark.ai[0] != target.whoAmI)
                    continue;

                mark.ai[1] += damageDone;
                mark.localAI[0]++;
                mark.timeLeft = DetonationDelay;
                target.AddBuff(BuffID.BrokenArmor, 240);

                if (mark.localAI[0] >= MaxStacks)
                    mark.Kill();

                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.4f, Pitch = 0.15f + mark.localAI[0] * 0.05f }, target.Center);
                return;
            }

            int markIdx = Projectile.NewProjectile(source.GetSource_FromThis(), target.Center, Vector2.Zero,
                markType, source.damage, 0f, source.owner, target.whoAmI, damageDone);
            if (markIdx >= 0 && markIdx < Main.maxProjectiles) {
                Main.projectile[markIdx].localAI[0] = 1f;
            }

            target.AddBuff(BuffID.BrokenArmor, 180);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = 0.35f }, target.Center);
        }

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center + new Vector2(0f, -target.height * 0.62f);
            Projectile.rotation += 0.07f + HitStacks * 0.015f;

            float stackPulse = 0.45f + HitStacks / MaxStacks * 0.55f;
            float progress = MathHelper.Clamp(Timer / DetonationDelay, 0f, 1f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(10f, 24f + HitStacks * 4f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(pos, dustType,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (1.5f + HitStacks * 0.35f),
                    60, default, 1.1f * stackPulse);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center,
                Vector3.Lerp(ArgusWeaponFx.IrisPurple.ToVector3(), ArgusWeaponFx.IrisGold.ToVector3(), progress) * (0.3f + stackPulse * 0.35f));
        }

        public override void OnKill(int timeLeft) {
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs)
                return;

            NPC target = Main.npc[targetIdx];
            if (!target.active)
                return;

            float stackMultiplier = 1.2f + HitStacks * 0.18f;
            int bonusDamage = (int)Math.Max(AccumulatedDamage * stackMultiplier, Projectile.damage * (1.5f + HitStacks * 0.25f));
            if (Main.myPlayer == Projectile.owner)
                target.SimpleStrikeNPC(bonusDamage, 0, false, 0f, null, false, 0, true);

            target.AddBuff(BuffID.Ichor, 360);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, Pitch = 0.2f + HitStacks * 0.04f }, target.Center);

            for (int i = 0; i < 10 + (int)HitStacks * 4; i++) {
                float angle = MathHelper.TwoPi * i / (10 + HitStacks * 4f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f + HitStacks);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(target.Center, dustType, vel, 50, default, Main.rand.NextFloat(1.8f, 2.8f));
                d.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(4 + (int)HitStacks, 10 + (int)HitStacks * 2);
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = MathHelper.Clamp(Timer / DetonationDelay, 0f, 1f);
            float opacity = MathHelper.Clamp(Timer / 12f, 0f, 1f);
            float pulse = 0.12f + MathF.Sin(Timer * 0.28f) * 0.035f + progress * 0.05f + HitStacks * 0.025f;

            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Vector2 origin = star.Size() / 2f;
                Color markColor = Color.Lerp(ArgusWeaponFx.IrisPurple, ArgusWeaponFx.IrisGold, progress) * opacity * (0.65f + HitStacks * 0.08f);
                markColor.A = 0;
                Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, markColor,
                    Projectile.rotation, origin, pulse, SpriteEffects.None, 0);
            }

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 origin = glow.Size() / 2f;
                Color glowColor = Color.Lerp(ArgusWeaponFx.IrisBlue, ArgusWeaponFx.IrisGold, HitStacks / MaxStacks) * opacity * (0.22f + progress * 0.3f);
                glowColor.A = 0;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, glowColor,
                    0f, origin, pulse * (1.8f + HitStacks * 0.25f), SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 光华星炮 — 百目 Argus 掉落远程武器
    /// 聚星蓄能后射出星光能束，命中引发恒星爆发
    /// </summary>
    public class LuminanceStellarCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1200;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 42;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<LuminanceStellarShell>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Bullet;
            Item.crit = 8;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<LuminanceStellarShell>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 48f;

            for (int i = 0; i < 28; i++) {
                Vector2 gatherVel = (muzzlePos - (muzzlePos + Main.rand.NextVector2CircularEdge(70f, 70f))).SafeNormalize(Vector2.Zero)
                    * Main.rand.NextFloat(6f, 14f);
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustPerfect(muzzlePos + Main.rand.NextVector2Circular(8f, 8f), dustType, gatherVel, 100, default, 1.8f);
                d.noGravity = true;
            }

            Projectile.NewProjectile(source, muzzlePos, velocity, type, damage, knockback, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.15f, Volume = 0.85f }, muzzlePos);

            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);

            return false;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-10, 0);

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    /// <summary>星光能束 — 飞行中聚星膨胀，命中后触发恒星爆发</summary>
    public class LuminanceStellarShell : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private ref float TravelTimer => ref Projectile.ai[0];
        private ref float BurstSpawned => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            TravelTimer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            float growth = MathHelper.Clamp(TravelTimer / 30f, 0f, 1f);
            Projectile.scale = MathHelper.Lerp(0.55f, 1.35f, growth);

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.08f, -Projectile.velocity.Y * 0.08f,
                    120, default, 1.3f);
                d.noGravity = true;
                d.fadeIn = 1.1f;
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.22f, 0.55f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            TrySpawnStellarBurst(target.Center);
        }

        public override void OnKill(int timeLeft) {
            TrySpawnStellarBurst(Projectile.Center);
        }

        private void TrySpawnStellarBurst(Vector2 position) {
            if (BurstSpawned != 0f)
                return;
            BurstSpawned = 1f;
            SpawnStellarBurst(position);
        }

        private void SpawnStellarBurst(Vector2 position) {
            if (Projectile.owner != Main.myPlayer)
                return;

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 0.75f }, position);

            for (int i = 0; i < 18; i++) {
                int dustType = i % 3 == 0 ? DustID.YellowStarDust : (i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch);
                Dust d = Dust.NewDustDirect(position, 0, 0, dustType, 0, 0, 80, default, 1.6f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2CircularEdge(7f, 7f);
            }

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, Vector2.Zero,
                ModContent.ProjectileType<LuminanceStellarBurst>(),
                (int)(Projectile.damage * 0.55f), Projectile.knockBack, Projectile.owner);

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 12);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(TravelTimer * 0.25f) * 0.1f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(200, 140, 255), new Color(100, 140, 255), progress) * (progress * 0.55f);
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.45f + progress * 0.55f) * pulse, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color glowColor = Color.Lerp(new Color(220, 180, 255), new Color(140, 180, 255), MathF.Sin(TravelTimer * 0.2f) * 0.5f + 0.5f);
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glowColor * 0.55f, Projectile.rotation, origin, Projectile.scale * pulse * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White * 0.9f, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = new Color(255, 245, 255) * 0.35f;
                sparkleColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, Projectile.rotation * 0.5f,
                    ACMAsset.Sparkle.Size() / 2f, Projectile.scale * 0.7f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>星光爆裂 — 命中后的恒星爆发伤害场</summary>
    public class LuminanceStellarBurst : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private ref float Timer => ref Projectile.ai[0];
        private const int Duration = 42;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            float radius = Timer * 14f;

            for (int i = 0; i < 7; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.35f, radius);
                int dustType = Main.rand.NextBool(3) ? DustID.YellowStarDust : (Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch);
                Dust d = Dust.NewDustPerfect(pos, dustType, Main.rand.NextVector2Circular(2f, 2f), 60, default, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.45f, 0.3f, 0.65f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = Timer * 14f;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float prog = 1f - Projectile.timeLeft / (float)Duration;
            float alpha = ACMUtils.QuadOut(1f - prog) * 0.9f;
            float scale = MathHelper.SmoothStep(0f, 14f, ACMUtils.QuadOut(prog));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D sparkle = ACMAsset.Sparkle;

            for (int k = 0; k < 8; k++) {
                float bAngle = k * MathF.PI / 4f + Timer * 0.018f;
                bool strong = k % 2 == 0;
                Color bColor = strong ? new Color(200, 140, 255) : new Color(100, 160, 255);
                float bLen = strong ? scale * 0.58f : scale * 0.38f;
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    bColor * (alpha * 0.72f), bAngle,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    new Vector2(0.14f, bLen), SpriteEffects.None, 0);
            }

            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(160, 120, 255) * (alpha * 0.42f), 0f,
                sg.Size() * 0.5f,
                scale * 0.52f, SpriteEffects.None, 0);

            float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.6f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(245, 235, 255) * (alpha * flashAlpha), 0f,
                sg.Size() * 0.5f,
                scale * 0.16f, SpriteEffects.None, 0);

            if (sparkle != null) {
                sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 250, 255) * (alpha * 0.5f), Timer * 0.06f,
                    sparkle.Size() / 2f, scale * 0.35f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 虹膜湮灭手铳 - 百目 Argus 掉落的手铳
    /// 将子弹化为耀金虹膜光弹；长按蓄力环聚虹膜，松手释放金色光弹连射爆发
    /// </summary>
    public class LuminousIrisAnnihilator : ModItem
    {
        private int chargeTime;
        private const int MaxCharge = 40;
        private bool isFullyCharged;

        private static readonly Color IrisGold = new(255, 210, 80);
        private static readonly Color IrisPurple = new(180, 100, 255);
        private static readonly Color IrisBlue = new(100, 140, 255);

        public override void SetDefaults() {
            Item.damage = 1180;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 48;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<LuminousIrisShell>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Bullet;
            Item.crit = 12;
            Item.channel = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Handgun;

        public override void HoldItem(Player player) {
            if (player.channel && player.HasAmmo(Item)) {
                chargeTime++;

                float chargeProgress = Math.Min(chargeTime / (float)MaxCharge, 1f);
                SpawnIrisChargeFx(player, chargeProgress);

                if (chargeTime == MaxCharge) {
                    isFullyCharged = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.45f, Volume = 0.85f }, player.Center);
                }

                if (chargeTime > MaxCharge)
                    chargeTime = MaxCharge;
            }
            else if (chargeTime > 0 && !player.channel) {
                if (isFullyCharged)
                    FireShellBurst(player, shellCount: 8, spreadDegrees: 4f, damageMultiplier: 0.92f, empowered: true);
                else if (chargeTime > 12)
                    FireShellBurst(player, shellCount: 4, spreadDegrees: 6f, damageMultiplier: 0.85f, empowered: false);
                else
                    FireShellBurst(player, shellCount: 1, spreadDegrees: 0f, damageMultiplier: 1f, empowered: false);

                chargeTime = 0;
                isFullyCharged = false;
            }
        }

        private static void SpawnIrisChargeFx(Player player, float chargeProgress) {
            if (Main.netMode == NetmodeID.Server || chargeProgress <= 0.15f)
                return;

            if (Main.rand.NextBool(2)) {
                float ringRadius = MathHelper.Lerp(18f, 52f, 1f - chargeProgress);
                for (int ring = 0; ring < 3; ring++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi) + ring * MathHelper.TwoPi / 3f;
                    Vector2 dustPos = player.Center + angle.ToRotationVector2() * ringRadius;
                    int dustType = ring == 1 ? DustID.PurpleTorch : DustID.GoldFlame;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 1.2f + chargeProgress);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + chargeProgress * 4f);
                }
            }

            if (chargeProgress > 0.55f)
                Lighting.AddLight(player.Center, Vector3.Lerp(IrisPurple.ToVector3(), IrisGold.ToVector3(), chargeProgress) * chargeProgress * 0.8f);
        }

        private void FireShellBurst(Player player, int shellCount, float spreadDegrees, float damageMultiplier, bool empowered) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (!player.HasAmmo(Item))
                return;

            player.PickAmmo(Item, out _, out float speed, out int damage, out float knockback, out _);

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = player.Center + direction * 42f;
            float shotSpeed = Item.shootSpeed + speed;
            int shellDamage = (int)((damage + Item.damage) * damageMultiplier);
            float shellKnockback = knockback + Item.knockBack;

            for (int i = 0; i < shellCount; i++) {
                float spreadOffset = shellCount == 1
                    ? 0f
                    : MathHelper.Lerp(-spreadDegrees, spreadDegrees, i / (float)(shellCount - 1));
                Vector2 velocity = direction.RotatedBy(MathHelper.ToRadians(spreadOffset)) * shotSpeed;

                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    muzzle,
                    velocity,
                    ModContent.ProjectileType<LuminousIrisShell>(),
                    shellDamage,
                    shellKnockback,
                    player.whoAmI,
                    empowered ? 1f : 0f);
            }

            SoundEngine.PlaySound(SoundID.Item36 with { Pitch = empowered ? 0.25f : 0f, Volume = empowered ? 1.1f : 0.9f }, player.Center);
            if (empowered) {
                SoundEngine.PlaySound(SoundID.Item62 with { Pitch = 0.35f, Volume = 0.75f }, player.Center);
                if (player.whoAmI == Main.myPlayer)
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
            }

            for (int i = 0; i < (empowered ? 10 : 4); i++) {
                Vector2 dustVel = -direction.RotatedByRandom(0.45f) * Main.rand.NextFloat(3f, 7f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(muzzle, dustType, dustVel, 60, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-10, 0);

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "IrisLore", "「百目虹膜凝为铳口，耀金湮灭一切视界」"));
            tooltips.Add(new TooltipLine(Mod, "IrisEffect", "将子弹化为耀金虹膜光弹，命中时绽放湮灭爆炸"));
            tooltips.Add(new TooltipLine(Mod, "IrisEffect2", "长按蓄力环聚虹膜，松手释放金色光弹连射爆发"));
        }
    }

    /// <summary>耀虹炮弹 — 金色虹膜光弹</summary>
    public class LuminousIrisShell : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color IrisGold = new(255, 210, 80);
        private static readonly Color IrisPurple = new(180, 100, 255);

        private ref float IsEmpowered => ref Projectile.ai[0];
        private ref float HasExploded => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (IsEmpowered >= 1f)
                Projectile.extraUpdates = 2;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (IsEmpowered >= 1f && Projectile.timeLeft < 100) {
                NPC target = FindClosestNPC(Projectile.Center, 420f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.06f);
                }
            }

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, Vector3.Lerp(IrisPurple.ToVector3(), IrisGold.ToVector3(), 0.55f) * 0.45f);
        }

        private static NPC FindClosestNPC(Vector2 center, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnExplosion();
            target.AddBuff(BuffID.Ichor, 180);
        }

        public override void OnKill(int timeLeft) {
            SpawnExplosion();
        }

        private void SpawnExplosion() {
            if (HasExploded >= 1f || Projectile.owner != Main.myPlayer)
                return;

            HasExploded = 1f;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<LuminousIrisExplosion>(),
                (int)(Projectile.damage * 0.55f),
                Projectile.knockBack * 0.35f,
                Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(IrisPurple, IrisGold, progress) * (0.55f * progress);
                trailColor.A = 0;

                Main.spriteBatch.Draw(
                    tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    origin,
                    new Vector2(0.35f * progress, 0.12f),
                    SpriteEffects.None,
                    0f);
            }

            Color mainColor = Color.Lerp(IrisGold, Color.White, 0.25f);
            mainColor.A = 0;
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                mainColor,
                Projectile.rotation,
                origin,
                new Vector2(0.55f, 0.14f),
                SpriteEffects.None,
                0f);

            return false;
        }
    }

    /// <summary>耀虹湮灭爆炸 — 虹膜光弹命中时的金色湮灭环</summary>
    public class LuminousIrisExplosion : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color IrisGold = new(255, 210, 80);
        private static readonly Color IrisPurple = new(180, 100, 255);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 36;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;

            if (Main.netMode == NetmodeID.Server)
                return;

            float radius = Projectile.ai[0] * 9f;
            for (int i = 0; i < 5; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.45f, radius);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(pos, dustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 70, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Vector3.Lerp(IrisPurple.ToVector3(), IrisGold.ToVector3(), 0.65f) * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 120);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = Projectile.ai[0] * 9f;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.SoftGlow == null)
                return false;

            float progress = 1f - Projectile.timeLeft / 36f;
            float alpha = ACMUtils.QuadOut(1f - progress) * 0.85f;
            float scale = MathHelper.SmoothStep(0f, 10f, ACMUtils.QuadOut(progress));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D glow = ACMAsset.SoftGlow;
            Color outerColor = Color.Lerp(IrisPurple, IrisGold, 0.35f) * (alpha * 0.55f);
            outerColor.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, outerColor, 0f, glow.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0f);

            Color innerColor = IrisGold * (alpha * 0.75f);
            innerColor.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, innerColor, 0f, glow.Size() * 0.5f, scale * 0.28f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}

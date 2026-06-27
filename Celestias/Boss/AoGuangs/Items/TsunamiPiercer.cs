using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs.Items
{
    /// <summary>
    /// 海啸龙枪 - 敖广掉落的长枪类近战武器
    /// 突刺时释放水波冲击，蓄力可进行龙翔突刺
    /// </summary>
    public class TsunamiPiercer : ModItem
    {
        private int thrustCount = 0;
        private float oceanPower = 0f;
        private const float MaxOceanPower = 100f;

        public override void SetDefaults() {
            Item.damage = 350;
            Item.DamageType = DamageClass.Melee;
            Item.width = 70;
            Item.height = 70;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<TsunamiPiercerThrust>();
            Item.shootSpeed = 14f;
            Item.crit = 15;
        }

        public override void HoldItem(Player player) {
            // 海洋之力光环
            if (oceanPower > 50f && Main.rand.NextBool(5)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(55, 55);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1.5f, 150, default, 1.4f);
                Main.dust[dust].noGravity = true;
            }

            // 满力时强化
            if (oceanPower >= MaxOceanPower && Main.rand.NextBool(3)) {
                Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(70, 70);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, -2f, 100, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }

            // 力量衰减
            if (oceanPower > 0f) {
                oceanPower -= 0.06f;
                if (oceanPower < 0f) oceanPower = 0f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            thrustCount++;
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 普通突刺
            Projectile.NewProjectile(source, player.Center, direction * 18f,
                type, damage, knockback, player.whoAmI);

            // 每四次突刺释放水波
            if (thrustCount >= 4) {
                thrustCount = 0;
                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0f, Volume = 1f }, player.Center);

                Projectile.NewProjectile(source, player.Center, direction * 16f,
                    ModContent.ProjectileType<TsunamiWaveThrust>(), (int)(damage * 1.5f), knockback * 1.5f, player.whoAmI);

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 12);
                }
            }

            // 满力龙翔突刺
            if (oceanPower >= MaxOceanPower) {
                oceanPower = 0f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1f }, player.Center);

                // 玩家短暂无敌并冲刺
                player.immune = true;
                player.immuneTime = 30;
                player.velocity = direction * 25f;

                // 龙翔弹幕
                Projectile.NewProjectile(source, player.Center, direction * 22f,
                    ModContent.ProjectileType<DragonSoaringThrust>(), damage * 2, knockback * 2f, player.whoAmI);

                // 视觉爆发
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(8, 15);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(player.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 30);
                }
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            oceanPower += 7f;
            if (hit.Crit) oceanPower += 12f;
            if (oceanPower > MaxOceanPower) oceanPower = MaxOceanPower;

            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, oceanPower >= MaxOceanPower ? 1.6f : 1f, player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "以龙骨铸造的海啸龙枪"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "每四次突刺释放海啸冲击波"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "命中敌人积蓄海洋之力，满力时释放龙翔突刺"));
        }
    }

    /// <summary>
    /// 龙枪突刺弹幕
    /// </summary>
    public class TsunamiPiercerThrust : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float thrustProgress = 0f;
        private float maxExtend = 120f;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 15;
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

            // 突刺动作
            thrustProgress += 0.12f;
            float extend;
            if (thrustProgress < 0.5f) {
                extend = ACMUtils.QuadOut(thrustProgress * 2f) * maxExtend;
            }
            else {
                extend = (1f - ACMUtils.QuadIn((thrustProgress - 0.5f) * 2f)) * maxExtend;
            }

            if (thrustProgress >= 1f) {
                Projectile.Kill();
                return;
            }

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();
            Projectile.Center = Owner.MountedCenter + direction * (30f + extend);

            Owner.direction = direction.X >= 0 ? 1 : -1;
            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            // 突刺粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + direction * 20f;
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = direction * 3f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 60);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(4, 8);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 0.9f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<TsunamiPiercer>()].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 枪身
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation + MathHelper.ToRadians(68),
                tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            // 枪尖光效
            if (ACMAsset.LightShot != null) {
                Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 30f;
                Color tipColor = AoGuangHelper.PureWhite * 0.7f;
                tipColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, tipPos - Main.screenPosition, null, tipColor,
                    Projectile.rotation, ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 40f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 25f, ref collisionPoint);
        }
    }

    /// <summary>
    /// 海啸冲击波
    /// </summary>
    public class TsunamiWaveThrust : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float waveScale = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 160;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            waveScale = MathHelper.Lerp(waveScale, 1.3f, 0.08f);

            Projectile.scale = waveScale;

            // 水波粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                    float offset = MathF.Sin(Projectile.ai[0] * 0.4f + i) * 15f * waveScale;
                    Vector2 dustPos = Projectile.Center + perpendicular * offset;

                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 2f * waveScale);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * waveScale * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);

            // 冲击效果
            Vector2 knockDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            target.velocity += knockDir * 6f;

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7, 7);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 1.3f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 30f * waveScale,
                outerColor: new Color(30, 90, 170, 130), innerColor: new Color(170, 240, 255, 175),
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.3f);

            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 波浪拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float bodyScale = progress * waveScale * (0.9f + MathF.Sin(Projectile.ai[0] * 0.3f + i * 0.4f) * 0.1f);

                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.6f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1f * bodyScale, 0.3f * bodyScale), SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙翔突刺 - 满力释放的强力突刺
    /// </summary>
    public class DragonSoaringThrust : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float dragonPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 25;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 150;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            dragonPhase += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙形粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 5; i++) {
                    float angle = dragonPhase + MathHelper.TwoPi * i / 5;
                    Vector2 offset = angle.ToRotationVector2() * 25f;
                    Vector2 dustPos = Projectile.Center + offset;

                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f - Projectile.velocity * 0.15f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 1.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 180);
            target.AddBuff(BuffID.Frostburn, 120);

            // 龙形爆发
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20 + dragonPhase;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6, 12);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Water,
                    1 => DustID.BlueTorch,
                    _ => DustID.Wet
                };
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.7f, Volume = 0.6f }, target.Center);

            // 龙翔突刺·处决级东海冰蓝命中演出
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 2f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 龙翔突刺招牌演出: 用蛇形 ribbon 在冲刺轨迹上画"水龙真身段" (现代化双层) + 核心径向辉光
            var ribbon = new List<Vector2>(Projectile.oldPos.Length);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float waveOffset = MathF.Sin(dragonPhase - i * 0.3f) * 10f;
                float rot = Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation;
                Vector2 perp = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                ribbon.Add(Projectile.oldPos[i] + Projectile.Size / 2f + perp * waveOffset);
            }
            if (ribbon.Count >= 2)
                WeaponVFX.DrawRibbonTrail(ribbon.ToArray(), baseWidth: 40f,
                    outerColor: new Color(30, 90, 170, 150), innerColor: new Color(190, 245, 255, 190),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, 0.55f, new Color(120, 220, 255), 6f);

            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 龙形拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                // 蛇行
                float waveOffset = MathF.Sin(dragonPhase - i * 0.3f) * 8f * progress;
                Vector2 perpendicular = (Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation).ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Vector2 offset = perpendicular * waveOffset;

                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.8f;
                trailColor.A = 0;

                float bodyScale = (1f + MathF.Sin(dragonPhase + i * 0.2f) * 0.15f) * progress;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f + offset - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1.5f * bodyScale, 0.35f * bodyScale), SpriteEffects.None, 0f);
            }

            // 龙头
            Color headColor = AoGuangHelper.WaterGlow;
            headColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, headColor, Projectile.rotation,
                origin, new Vector2(2f, 0.5f), SpriteEffects.None, 0f);

            // 龙眼
            if (ACMAsset.LightShot != null) {
                Vector2 eyeOffset = Projectile.rotation.ToRotationVector2() * 25f;
                Color eyeColor = AoGuangHelper.PureWhite * 0.9f;
                eyeColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, Projectile.Center + eyeOffset - Main.screenPosition, null, eyeColor,
                    0f, ACMAsset.LightShot.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 1f }, Projectile.Center);

            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6, 12);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Water,
                    1 => DustID.BlueTorch,
                    _ => DustID.Wet
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 15);
        }
    }
}

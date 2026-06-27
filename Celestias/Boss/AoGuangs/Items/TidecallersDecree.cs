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
    /// 潮涌龙杖 - 敖广掉落的法杖类魔法武器
    /// 释放追踪水龙，蓄力可召唤巨型水龙卷
    /// </summary>
    public class TidecallersDecree : ModItem
    {
        private int castCount = 0;

        public override void SetDefaults() {
            Item.damage = 260;
            Item.DamageType = DamageClass.Magic;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 25);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item21;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<TidalDragonSpirit>();
            Item.shootSpeed = 12f;
            Item.mana = 18;
            Item.crit = 8;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            castCount++;
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);

            // 普通释放：水龙灵
            Projectile.NewProjectile(source, player.Center + direction * 30f, velocity,
                type, damage, knockback, player.whoAmI);

            // 每五次施法召唤水龙卷
            if (castCount >= 5) {
                castCount = 0;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 1.2f }, player.Center);

                // 在鼠标位置召唤水龙卷
                Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero,
                    ModContent.ProjectileType<SummonedWaterTornado>(), (int)(damage * 1.8f), knockback * 2f, player.whoAmI);

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 20);
                }

                // 视觉效果
                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20;
                    Vector2 vel = angle.ToRotationVector2() * 6f;
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(Main.MouseWorld, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DragonLore", "承载龙王潮涌之力的法杖"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect", "释放追踪水龙灵"));
            tooltips.Add(new TooltipLine(Mod, "DragonEffect2", "每五次施法在目标处召唤水龙卷"));
        }
    }

    /// <summary>
    /// 水龙灵 - 追踪水龙弹幕
    /// </summary>
    public class TidalDragonSpirit : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float dragonPhase;
        private float dragonLength = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 25;
            Projectile.height = 25;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            dragonPhase += 0.15f;
            dragonLength = MathHelper.Lerp(dragonLength, 1f, 0.05f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 蛇行移动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float waveOffset = MathF.Sin(dragonPhase * 2f) * 2f;
            Projectile.position += perpendicular * waveOffset;

            // 水龙粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * (10 + i * 8);
                    dustPos += Main.rand.NextVector2Circular(8, 8);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.8f * dragonLength);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.6f * dragonLength);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);

            // 水龙咬击效果
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.4f, Volume = 0.6f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 现代化: 用蛇形 ribbon 画水龙真身 (双层外深青/内冰蓝) 替代纯 dust 堆叠
            var ribbon = new List<Vector2>(Projectile.oldPos.Length);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float bodyWave = MathF.Sin(dragonPhase * 2f - i * 0.4f) * 6f;
                float rot = Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation;
                Vector2 perp = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                ribbon.Add(Projectile.oldPos[i] + Projectile.Size / 2f + perp * bodyWave);
            }
            if (ribbon.Count >= 2)
                WeaponVFX.DrawRibbonTrail(ribbon.ToArray(), baseWidth: 18f * dragonLength,
                    outerColor: new Color(30, 90, 170, 140), innerColor: new Color(180, 240, 255, 185),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 龙身拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                // 蛇行波动
                float bodyWave = MathF.Sin(dragonPhase * 2f - i * 0.4f) * 3f * progress;
                Vector2 perpendicular = (Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation).ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Vector2 bodyOffset = perpendicular * bodyWave;

                // 龙身渐变
                Color bodyColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                bodyColor *= progress * 0.7f * dragonLength;
                bodyColor.A = 0;

                float bodyScale = (0.8f + MathF.Sin(dragonPhase + i * 0.3f) * 0.1f) * progress * dragonLength;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f + bodyOffset - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, bodyColor, Projectile.oldRot[i], origin,
                    new Vector2(0.6f * bodyScale, 0.15f * bodyScale), SpriteEffects.None, 0f);
            }

            // 龙眼
            if (ACMAsset.LightShot != null) {
                Vector2 eyeOffset = Projectile.rotation.ToRotationVector2() * 12f;
                Color eyeColor = AoGuangHelper.PureWhite * 0.8f * dragonLength;
                eyeColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, Projectile.Center + eyeOffset - Main.screenPosition, null, eyeColor,
                    0f, ACMAsset.LightShot.Size() / 2f, 0.3f * dragonLength, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 召唤水龙卷 - 法杖召唤的中型水龙卷
    /// </summary>
    public class SummonedWaterTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tornadoRotation;
        private float tornadoAlpha = 0f;
        private float tornadoHeight = 0f;
        private const float MaxHeight = 400f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            tornadoRotation += 0.18f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.05f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.06f);

            // 吸引敌人
            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < 200f && distance > 30f) {
                    Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += pullDir * 0.8f;
                }
            }

            // 龙卷粒子 - 复用BarrierWaterTornado风格
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 5; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 25f + MathF.Abs(heightOffset / tornadoHeight) * 40f;

                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 5f, Main.rand.NextFloat(-1, 1));
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * tornadoAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 50f && heightDiff < tornadoHeight / 2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 90);

            for (int i = 0; i < 8; i++) {
                float angle = tornadoRotation + MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 1.2f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            if (tornadoAlpha > 0.5f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.14f, tornadoAlpha * 0.5f,
                    new Color(90, 200, 245), 0f);

            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 绘制水龙卷
            int segments = 14;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 0.5f + MathF.Abs(heightPercent - 0.5f) * 0.6f;
                float segRot = tornadoRotation + seg * 0.4f;

                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                Color outerColor = AoGuangHelper.OceanTeal * tornadoAlpha * 0.5f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.2f, SpriteEffects.None, 0f);

                Color midColor = AoGuangHelper.DragonBlue * tornadoAlpha * 0.7f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.3f, origin, segRadius, SpriteEffects.None, 0f);

                Color innerColor = AoGuangHelper.WaterGlow * tornadoAlpha * 0.4f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.6f, origin, segRadius * 0.6f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

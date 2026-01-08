using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls.Items
{
    /// <summary>
    /// 祖龙法杖 - 祖龙残魂掉落的魔法武器
    /// 一把由祖龙灵魂能量凝聚而成的迷幻法杖
    /// 特效：在目标位置召唤龙魂法阵，持续释放龙魂能量攻击敌人
    /// 蓄力后召唤祖龙虚影进行毁灭性打击
    /// </summary>
    public class ArchosaurStaff : ModItem
    {
        private int castCounter = 0;
        private const int MaxCast = 10;

        public override void SetDefaults() {
            Item.damage = 5200;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(platinum: 1, gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ArchosaurSoulCircle>();
            Item.shootSpeed = 0f;
            Item.mana = 20;
            Item.staff[Item.type] = true;
            Item.crit = 12;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 targetPos = Main.MouseWorld;
            castCounter++;

            // 限制同时存在的法阵数量
            int existingCircles = player.ownedProjectileCounts[type];
            if (existingCircles >= 4) {
                foreach (var proj in Main.projectile) {
                    if (proj.active && proj.owner == player.whoAmI && proj.type == type) {
                        proj.Kill();
                        break;
                    }
                }
            }

            // 召唤龙魂法阵
            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // 蓄力满后召唤祖龙虚影
            if (castCounter >= MaxCast) {
                castCounter = 0;

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1f }, player.Center);

                // 召唤祖龙虚影
                Projectile.NewProjectile(
                    source,
                    targetPos + new Vector2(0, -300),
                    new Vector2(0, 15f),
                    ModContent.ProjectileType<ArchosaurPhantom>(),
                    (int)(damage * 2f),
                    knockback * 2f,
                    player.whoAmI
                );

                // 祖龙降临特效
                for (int i = 0; i < 40; i++) {
                    Vector2 dustPos = targetPos + new Vector2(Main.rand.NextFloat(-150, 150), -300 + Main.rand.NextFloat(-50, 50));
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Cloud,
                        1 => DustID.WhiteTorch,
                        _ => DustID.Clentaminator_Cyan
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 5f, 200, Color.White, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 施法特效
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 0.8f }, targetPos);
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 dustVel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(targetPos + dustVel * 20, 0, 0, DustID.WhiteTorch, -dustVel.X, -dustVel.Y, 150, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // 从玩家到目标的能量线
            Vector2 toTarget = targetPos - player.Center;
            int lineCount = (int)(toTarget.Length() / 30);
            for (int i = 0; i < lineCount; i++) {
                float progress = (float)i / lineCount;
                Vector2 linePos = Vector2.Lerp(player.Center, targetPos, progress);
                int dust = Dust.NewDust(linePos, 0, 0, DustID.Cloud, 0, 0, 200, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(1, 1);
            }

            return false;
        }
    }

    /// <summary>
    /// 龙魂法阵 - 持续伤害区域
    /// </summary>
    public class ArchosaurSoulCircle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float circleRotation;
        private float pulsePhase;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            circleRotation += 0.03f;
            pulsePhase += 0.08f;

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;
            Projectile.scale = pulse;

            // 渐隐
            if (Projectile.timeLeft < 30) {
                Projectile.alpha = (int)(255 * (1f - Projectile.timeLeft / 30f));
            }

            // 法阵粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(50, 100);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;

                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1f, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 2f;
            }

            // 周期性释放龙魂能量
            if (Projectile.timeLeft % 30 == 0) {
                int orbCount = 5;
                for (int i = 0; i < orbCount; i++) {
                    float angle = circleRotation + MathHelper.TwoPi * i / orbCount;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * 80f;

                    // 寻找最近敌人
                    NPC target = FindClosestNPC(spawnPos, 400f);
                    if (target != null) {
                        Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 12f;
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            spawnPos,
                            vel,
                            ModContent.ProjectileType<ArchosaurSoulOrb>(),
                            Projectile.damage / 3,
                            Projectile.knockBack / 2f,
                            Projectile.owner
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.95f, 1f) * (1f - Projectile.alpha / 255f) * 0.6f);
        }

        private NPC FindClosestNPC(Vector2 position, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(position, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float alpha = 1f - Projectile.alpha / 255f;

            // 多层法阵环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = 50f + ring * 35f;
                float ringRotation = circleRotation * (1f + ring * 0.3f) * (ring % 2 == 0 ? 1 : -1);

                Color ringColor = ring switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(220, 240, 255),
                    _ => new Color(200, 225, 255)
                };
                ringColor *= alpha * (0.5f - ring * 0.1f);
                ringColor.A = 0;

                // 环上的符文点
                int runeCount = 8 + ring * 4;
                for (int i = 0; i < runeCount; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / runeCount;
                    Vector2 runePos = drawPos + angle.ToRotationVector2() * ringRadius;
                    float runeScale = 0.15f + MathF.Sin(pulsePhase + i * 0.5f) * 0.05f;

                    Main.spriteBatch.Draw(tex, runePos, null, ringColor, angle, origin, runeScale, SpriteEffects.None, 0f);
                }
            }

            // 中心光球
            if (ACMAsset.LightShot != null) {
                float coreScale = 1.5f + MathF.Sin(pulsePhase * 2f) * 0.3f;
                Color coreColor = new Color(255, 255, 255) * alpha * 0.6f;
                coreColor.A = 0;
                for (int i = 0; i < 8; i++) {
                    Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor, circleRotation + i * MathHelper.TwoPi / 8f,
                    ACMAsset.LightShot.Size() / 2f, coreScale, SpriteEffects.None, 0f);
                }

            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnKill(int timeLeft) {
            // 消散特效
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙魂能量球 - 法阵释放的追踪弹幕
    /// </summary>
    public class ArchosaurSoulOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float orbPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            orbPhase += 0.15f;
            Projectile.rotation = orbPhase;

            // 轻微追踪
            if (Projectile.timeLeft > 60) {
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.06f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            // 粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 180, Color.White, 0.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.9f, 1f) * 0.3f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 240, 255) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.3f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 255, 255) * 0.7f;
            mainColor.A = 0;

            for (int i = 0; i < 8; i++) {
                Main.spriteBatch.Draw(tex, drawPos, null, mainColor, i * MathHelper.TwoPi / 8f, origin, 0.5f, SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 150, Color.White, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2, 2);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Cloud, vel.X, vel.Y, 180, Color.White, 1f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 祖龙虚影 - 终极技能召唤的祖龙幻象
    /// </summary>
    public class ArchosaurPhantom : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float phantomPhase;
        private bool hasExploded = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 25;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            phantomPhase += 0.1f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 加速下冲
            if (Projectile.velocity.Length() < 30f) {
                Projectile.velocity *= 1.04f;
            }

            // 祖龙虚影雾气
            for (int i = 0; i < 5; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(50, 50);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 200, Color.White, 2.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
            }

            // 触地爆炸判定
            if (!hasExploded && Projectile.timeLeft < 80) {
                hasExploded = true;
                ExplodeDragonBreath();
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 1f, 1f) * 0.8f);
        }

        private void ExplodeDragonBreath() {
            // 龙息爆发
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.2f }, Projectile.Center);

            // 向四周发射龙息
            int breathCount = 16;
            for (int i = 0; i < breathCount; i++) {
                float angle = MathHelper.TwoPi * i / breathCount;
                Vector2 vel = angle.ToRotationVector2() * 15f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    ModContent.ProjectileType<ArchosaurBreathWave>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner
                );
            }

            // 大爆发特效
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * i / 60;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5, 15);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Frost
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 3f);
                Main.dust[dust].noGravity = true;
            }

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 40);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 龙形拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(200, 225, 255), 1f - progress);
                trailColor *= progress * 0.6f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float waveOffset = MathF.Sin(phantomPhase + i * 0.3f) * 10f;
                Vector2 perpendicular = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                pos += perpendicular * waveOffset;

                float segmentScale = 0.8f * progress;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(2f * progress, segmentScale), SpriteEffects.None, 0f);
            }

            // 龙头主体
            Color headColor = new Color(255, 255, 255) * 0.9f;
            headColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, headColor, Projectile.rotation, origin,
                new Vector2(2.5f, 1f), SpriteEffects.None, 0f);

            // 龙眼
            if (ACMAsset.LightShot != null) {
                Vector2 eyeOffset1 = Projectile.rotation.ToRotationVector2() * 30f + Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 15f;
                Vector2 eyeOffset2 = Projectile.rotation.ToRotationVector2() * 30f - Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 15f;

                Color eyeColor = new Color(255, 255, 255) * 0.8f;
                eyeColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos + eyeOffset1, null, eyeColor, Projectile.velocity.ToRotation(),
                    ACMAsset.LightShot.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos + eyeOffset2, null, eyeColor, Projectile.velocity.ToRotation(),
                    ACMAsset.LightShot.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 600);

            // 龙魂侵蚀
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit3 with { Pitch = 0.3f }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            // 消散龙魂
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10, 10);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Frost
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.5f, Volume = 0.6f }, Projectile.Center);
        }
    }
}

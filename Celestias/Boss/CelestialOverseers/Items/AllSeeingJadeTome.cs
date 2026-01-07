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
    /// 洞察玉典 - 天庭观察者掉落的魔法书
    /// 召唤天眼在鼠标位置注视并发射追踪光束
    /// </summary>
    public class AllSeeingJadeTome : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 350;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<AllSeeingEyeProjectile>();
            Item.shootSpeed = 0f;
            Item.mana = 12;
            Item.channel = false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 在鼠标位置召唤天眼
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // 施法特效
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 dustVel = angle.ToRotationVector2() * 3f;
                int dust = Dust.NewDust(targetPos, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }
    }

    /// <summary>
    /// 全视之眼 - 在位置注视并发射追踪光束
    /// </summary>
    public class AllSeeingEyeProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float eyeAlpha = 0f;
        private float eyeScale = 0.5f;
        private float eyeRotation = 0f;
        private float pulsePhase = 0f;
        private int attackTimer = 0;
        private NPC targetNPC;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 500;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 淡入和缩放
            if (Projectile.timeLeft > 160) {
                eyeAlpha = MathHelper.Lerp(eyeAlpha, 1f, 0.08f);
                eyeScale = MathHelper.Lerp(eyeScale, 1f, 0.06f);
            }
            else if (Projectile.timeLeft < 30) {
                eyeAlpha = MathHelper.Lerp(eyeAlpha, 0f, 0.08f);
                eyeScale = MathHelper.Lerp(eyeScale, 0.3f, 0.05f);
            }

            pulsePhase += 0.1f;
            eyeRotation += 0.02f;
            attackTimer++;

            // 寻找目标
            targetNPC = FindClosestNPC(600f);

            // 注视目标
            if (targetNPC != null) {
                Vector2 toTarget = targetNPC.Center - Projectile.Center;
                float targetRot = toTarget.ToRotation();
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRot, 0.1f);

                // 发射追踪光束
                if (attackTimer >= 30 && Projectile.timeLeft > 50) {
                    attackTimer = 0;
                    FireEyeBeam();
                }
            }
            else {
                // 缓慢旋转扫视
                Projectile.rotation += 0.03f;
            }

            // 天眼粒子
            SpawnEyeParticles();

            // 光照
            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.9f, 1f) * 0.6f * eyeAlpha);
        }

        private void FireEyeBeam() {
            if (Main.netMode == NetmodeID.MultiplayerClient || targetNPC == null) return;

            Vector2 toTarget = (targetNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);

            Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                Projectile.Center,
                toTarget * 12f,
                ModContent.ProjectileType<AllSeeingBeam>(),
                Projectile.damage / 2,
                Projectile.knockBack,
                Projectile.owner
            );

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);

            // 发射粒子
            for (int i = 0; i < 6; i++) {
                Vector2 vel = toTarget.RotatedByRandom(0.3f) * Main.rand.NextFloat(3, 6);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, new Color(180, 220, 255), 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        private void SpawnEyeParticles() {
            // 环绕粒子
            if (Main.rand.NextBool(3)) {
                float angle = pulsePhase + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * 35f * eyeScale;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, new Color(180, 220, 255), 1f * eyeAlpha);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f;
            }

            // 金色光粒
            if (Main.rand.NextBool(5)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20, 20);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f * eyeAlpha);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) {
            // 使用天眼纹理
            Texture2D eyeTexture = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            if (eyeTexture == null) return false;

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = eyeTexture.Size() / 2f;

            float pulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.1f;

            // 外层光环
            Color outerGlow = new Color(150, 200, 255) * 0.3f * eyeAlpha;
            outerGlow.A = 0;
            for (int i = 0; i < 3; i++) {
                float layerRot = eyeRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.5f + i * 0.3f) * eyeScale * pulse;
                sb.Draw(eyeTexture, drawPos, null, outerGlow * (0.5f - i * 0.1f), layerRot, origin, layerScale, SpriteEffects.None, 0f);
            }

            // 核心光晕
            Color coreGlow = new Color(200, 230, 255) * 0.5f * eyeAlpha;
            coreGlow.A = 0;
            sb.Draw(eyeTexture, drawPos, null, coreGlow, Projectile.rotation, origin, eyeScale * pulse * 0.8f, SpriteEffects.None, 0f);

            // 瞳孔（朝向目标）
            Color pupilColor = Color.White * eyeAlpha;
            sb.Draw(eyeTexture, drawPos, null, pupilColor, Projectile.rotation, origin, eyeScale * 0.6f, SpriteEffects.None, 0f);

            // 注视目标时的光线
            if (targetNPC != null) {
                DrawGazeLine(sb, drawPos);
            }

            return false;
        }

        private void DrawGazeLine(SpriteBatch sb, Vector2 drawPos) {
            if (ACMAsset.GlaciateWave == null || targetNPC == null) return;

            Vector2 toTarget = targetNPC.Center - Projectile.Center;
            float distance = toTarget.Length();
            float rotation = toTarget.ToRotation();

            Texture2D lineTex = ACMAsset.GlaciateWave;
            Vector2 origin = new Vector2(0, lineTex.Height / 2f);

            // 虚线注视效果
            float segmentLength = 50f;
            int segments = (int)(distance / segmentLength);
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                if ((i + (int)(pulsePhase * 5)) % 2 == 0) continue;

                float alpha = (1f - MathF.Abs(t - 0.5f) * 2f) * 0.2f * eyeAlpha;
                Color lineColor = new Color(180, 220, 255) * alpha;
                lineColor.A = 0;

                Vector2 segmentPos = drawPos + rotation.ToRotationVector2() * (t * distance);
                sb.Draw(lineTex, segmentPos, null, lineColor, rotation, origin, new Vector2(segmentLength / lineTex.Width, 0.03f), SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            // 消散粒子
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 全视光束 - 天眼发射的追踪光束
    /// </summary>
    public class AllSeeingBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 追踪
            if (Projectile.timeLeft > 60) {
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.06f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            // 光束粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 100, new Color(180, 220, 255), 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.7f, 1f) * 0.6f);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, new Color(180, 220, 255), 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(150, 200, 255) * progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, new Vector2(0.5f * progress, 0.1f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(180, 220, 255);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation,
                origin, new Vector2(0.6f, 0.12f), SpriteEffects.None, 0f);

            // 核心
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(220, 240, 255);
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor * 0.6f, 0f,
                    ACMAsset.LightShot.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, new Color(180, 220, 255), 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

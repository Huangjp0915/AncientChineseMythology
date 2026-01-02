using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences.Items
{
    /// <summary>
    /// 摆渡人 - 白无常掉落的幽灵弓
    /// 发射幽魂箭矢，命中敌人时会释放追踪幽魂
    /// </summary>
    public class Ferryman : ModItem
    {
        public override string Texture => BAWHelper.Path + "Items/Ferryman";

        public override void SetDefaults() {
            Item.damage = 95;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 24;
            Item.height = 56;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.sellPrice(gold: 12);
            Item.rare = ItemRarityID.LightPurple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<FerrymanArrow>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {

        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 发射主箭矢
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<FerrymanArrow>(), damage, knockback, player.whoAmI);

            // 20%几率额外发射一支幽魂箭（稍微偏移角度）
            if (Main.rand.NextBool(5)) {
                Vector2 offsetVel = velocity.RotatedByRandom(MathHelper.ToRadians(15)) * 0.9f;
                Projectile.NewProjectile(source, position, offsetVel, ModContent.ProjectileType<FerrymanArrow>(), (int)(damage * 0.7f), knockback * 0.5f, player.whoAmI, ai1: 1f);
            }

            return true;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }

        public override void AddRecipes() {
            // 可以添加合成配方
        }
    }

    /// <summary>
    /// 摆渡人弓发射的幽魂箭矢
    /// </summary>
    public class FerrymanArrow : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float ghostAlpha = 0f;
        private bool isGhostArrow => Projectile.ai[1] == 1f; // 额外的幽魂箭

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.1f);
            pulsePhase += 0.15f;

            // 箭矢朝向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 重力（幽魂箭无重力）
            if (!isGhostArrow) {
                Projectile.velocity.Y += 0.08f;
            }
            else {
                // 幽魂箭轻微追踪
                NPC target = FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.02f);
                }
            }

            // 幽魂粒子
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.7f * ghostAlpha;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 100;
            }

            Lighting.AddLight(Projectile.Center, new Color(180, 200, 255).ToVector3() * 0.3f * ghostAlpha);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
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
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制幽灵拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailAlpha = progress * 0.5f * ghostAlpha;
                float trailScale = (0.4f + progress * 0.6f) * (isGhostArrow ? 1.2f : 1f);

                Color trailColor = isGhostArrow
                    ? new Color(150, 200, 255) * trailAlpha
                    : new Color(180, 220, 255) * trailAlpha;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // 箭矢主体 - 使用幽灵光球效果
            float arrowPulse = 1f + MathF.Sin(pulsePhase) * 0.15f;
            Color coreColor = isGhostArrow ? new Color(150, 220, 255) : new Color(200, 230, 255);
            Color glowColor = isGhostArrow ? new Color(100, 180, 255) : new Color(150, 200, 255);

            BAWHelper.DrawGhostOrb(sb, Projectile.Center, coreColor * ghostAlpha, glowColor,
                0.8f * arrowPulse, pulsePhase);

            // 箭头指示（三角形光效）
            Vector2 tipOffset = Projectile.velocity.SafeNormalize(Vector2.Zero) * 8f;
            Color tipColor = Color.White * ghostAlpha * 0.6f;
            tipColor.A = 0;
            sb.Draw(tex, Projectile.Center + tipOffset - Main.screenPosition, null, tipColor,
                Projectile.rotation, origin, 0.4f * arrowPulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时释放追踪幽魂
            if (Main.rand.NextBool(3) || isGhostArrow) {
                var source = Projectile.GetSource_OnHit(target);
                Vector2 spawnPos = target.Center + Main.rand.NextVector2Circular(20, 20);
                Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);

                Projectile.NewProjectile(source, spawnPos, vel,
                    ModContent.ProjectileType<FerrymanGhost>(),
                    Projectile.damage / 2, 0f, Projectile.owner);
            }

            // 命中特效
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.5f, Volume = 0.6f }, target.Center);
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 摆渡人弓释放的追踪幽魂（友方弹幕）
    /// </summary>
    public class FerrymanGhost : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float ghostAlpha = 0f;
        private float wobblePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.08f);
            pulsePhase += 0.12f;
            wobblePhase += 0.1f;

            // 追踪最近敌人
            NPC target = FindClosestNPC(500f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float targetSpeed = 10f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * targetSpeed, 0.04f);
            }

            // 幽灵飘动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(wobblePhase * 2f) * 1.5f;
            Projectile.position += perpendicular * drift;

            Projectile.rotation += 0.1f;

            // 粒子
            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.6f * ghostAlpha;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 120;
            }

            Lighting.AddLight(Projectile.Center, new Color(150, 180, 255).ToVector3() * 0.25f * ghostAlpha);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
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
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailAlpha = progress * 0.35f * ghostAlpha;
                float trailScale = 0.5f + progress * 0.5f;

                Color trailColor = new Color(120, 180, 255) * trailAlpha;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float wobble = MathF.Sin(wobblePhase + i * 0.4f) * 2f;
                drawPos.Y += wobble;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // 主体幽魂
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(180, 220, 255) * ghostAlpha,
                new Color(100, 160, 255),
                0.9f, pulsePhase);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.6f, Volume = 0.4f }, target.Center);
            for (int i = 0; i < 5; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.7f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }
}

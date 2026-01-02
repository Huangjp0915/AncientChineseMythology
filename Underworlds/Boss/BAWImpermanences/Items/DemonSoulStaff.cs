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
    /// 勾魂法杖 - 白无常掉落的魔法法杖
    /// 在鼠标位置召唤旋转的幽灵法阵，持续伤害范围内敌人并吸取生命
    /// </summary>
    public class DemonSoulStaff : ModItem
    {
        public override string Texture => BAWHelper.Path + "Items/DemonSoulStaff";

        public override void SetDefaults() {
            Item.damage = 278;
            Item.DamageType = DamageClass.Magic;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.LightPurple;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DemonSoulCircle>();
            Item.shootSpeed = 0f;
            Item.mana = 18;
            Item.channel = false;
            Item.staff[Item.type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 在鼠标位置召唤法阵
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            // 施法特效
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f }, targetPos);
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                Vector2 dustVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                var d = Dust.NewDustPerfect(targetPos + dustVel * 10, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -dustVel;
            }

            return false;
        }

        public override void AddRecipes() {
            // 可以添加合成配方
        }
    }

    /// <summary>
    /// 勾魂法阵 - 持续伤害范围内敌人的魔法阵
    /// </summary>
    public class DemonSoulCircle : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float runeRotation = 0f;
        private float circleAlpha = 0f;
        private float circleRadius = 80f;
        private int damageTimer = 0;
        private int healAccumulator = 0;

        public override void SetDefaults() {
            Projectile.width = 160;
            Projectile.height = 160;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            // 淡入效果
            if (Projectile.timeLeft > 160) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 1f, 0.1f);
                circleRadius = MathHelper.Lerp(circleRadius, 100f, 0.05f);
            }
            // 淡出效果
            else if (Projectile.timeLeft < 30) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 0f, 0.05f);
                circleRadius = MathHelper.Lerp(circleRadius, 150f, 0.03f);
            }

            pulsePhase += 0.12f;
            runeRotation += 0.04f;
            damageTimer++;

            // 检测范围内敌人并造成伤害
            if (damageTimer >= 15) {
                damageTimer = 0;
                foreach (var npc in Main.npc) {
                    if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < circleRadius) {
                            // 对敌人造成伤害
                            Player owner = Main.player[Projectile.owner];
                            npc.SimpleStrikeNPC(Projectile.damage / 3, 0, false, 0, DamageClass.Magic);

                            // 累积治疗
                            healAccumulator += 2;

                            // 吸取特效
                            SpawnDrainEffect(npc.Center);
                        }
                    }
                }

                // 每累积一定量治疗玩家
                if (healAccumulator >= 5) {
                    Player owner = Main.player[Projectile.owner];
                    owner.Heal(healAccumulator);
                    healAccumulator = 0;
                }
            }

            // 法阵边缘粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circleRadius;
                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f * circleAlpha;
                d.velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2), MathF.Sin(angle + MathHelper.PiOver2)) * 2f;
                d.alpha = 100;
            }

            // 中心粒子漩涡
            if (Main.rand.NextBool(3)) {
                float spiralAngle = pulsePhase * 2f + Main.rand.NextFloat(MathHelper.TwoPi);
                float spiralRadius = Main.rand.NextFloat(circleRadius * 0.3f, circleRadius * 0.8f);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralRadius;
                var d = Dust.NewDustPerfect(dustPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.6f * circleAlpha;
                d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
                d.alpha = 80;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(150, 130, 220).ToVector3() * 0.5f * circleAlpha);
        }

        private void SpawnDrainEffect(Vector2 targetPos) {
            // 从敌人位置到法阵中心的吸取粒子
            Vector2 direction = (Projectile.Center - targetPos).SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 5; i++) {
                float t = i / 5f;
                Vector2 pos = Vector2.Lerp(targetPos, Projectile.Center, t);
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(5, 5), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.7f * (1f - t);
                d.velocity = direction * 6f;
                d.alpha = 100;
            }
        }

        public override bool? CanHitNPC(NPC target) {
            // 使用自定义伤害逻辑，禁用默认碰撞伤害
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制多层法阵环
            DrawMagicRings(sb, tex, origin);

            // 绘制中心核心
            DrawCenterCore(sb, tex, origin);

            // 绘制符文
            DrawRunes(sb, tex, origin);

            return false;
        }

        private void DrawMagicRings(SpriteBatch sb, Texture2D tex, Vector2 origin) {
            // 外圈
            int outerSegments = 24;
            for (int i = 0; i < outerSegments; i++) {
                float angle = runeRotation + MathHelper.TwoPi * i / outerSegments;
                float pulse = MathF.Sin(pulsePhase + angle * 3) * 0.2f + 0.8f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circleRadius;

                Color ringColor = new Color(150, 130, 220) * circleAlpha * pulse * 0.6f;
                ringColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, ringColor, angle, origin, 0.5f * pulse, SpriteEffects.None, 0);
            }

            // 中圈
            int midSegments = 16;
            for (int i = 0; i < midSegments; i++) {
                float angle = -runeRotation * 1.5f + MathHelper.TwoPi * i / midSegments;
                float pulse = MathF.Sin(pulsePhase * 1.2f + angle * 2) * 0.25f + 0.75f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circleRadius * 0.65f;

                Color ringColor = new Color(180, 160, 255) * circleAlpha * pulse * 0.5f;
                ringColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, ringColor, angle + MathHelper.PiOver4, origin, 0.6f * pulse, SpriteEffects.None, 0);
            }

            // 内圈
            int innerSegments = 8;
            for (int i = 0; i < innerSegments; i++) {
                float angle = runeRotation * 2f + MathHelper.TwoPi * i / innerSegments;
                float pulse = MathF.Sin(pulsePhase * 0.8f + angle) * 0.3f + 0.7f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circleRadius * 0.35f;

                Color ringColor = new Color(200, 180, 255) * circleAlpha * pulse * 0.7f;
                ringColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, ringColor, angle, origin, 0.7f * pulse, SpriteEffects.None, 0);
            }
        }

        private void DrawCenterCore(SpriteBatch sb, Texture2D tex, Vector2 origin) {
            float corePulse = 1f + MathF.Sin(pulsePhase * 1.5f) * 0.25f;

            // 使用幽灵光球效果
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(180, 150, 255) * circleAlpha,
                new Color(120, 100, 200),
                1.8f * corePulse, pulsePhase);
        }

        private void DrawRunes(SpriteBatch sb, Texture2D tex, Vector2 origin) {
            // 绘制六个主要符文
            int runeCount = 6;
            for (int i = 0; i < runeCount; i++) {
                float baseAngle = runeRotation * 0.5f + MathHelper.TwoPi * i / runeCount;
                float runeRadius = circleRadius * 0.85f;
                float pulse = MathF.Sin(pulsePhase + i * MathHelper.Pi / 3) * 0.2f + 0.8f;

                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle)) * runeRadius;

                // 符文发光
                Color glowColor = new Color(200, 180, 255) * circleAlpha * pulse * 0.4f;
                glowColor.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, glowColor, baseAngle * 2f, origin, 1.5f * pulse, SpriteEffects.None, 0);

                // 符文核心
                Color runeColor = new Color(220, 200, 255) * circleAlpha * pulse * 0.8f;
                runeColor.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, runeColor, baseAngle * 2f, origin, 0.8f * pulse, SpriteEffects.None, 0);

                // 连接线到中心
                DrawRuneConnection(sb, tex, origin, pos, pulse);
            }
        }

        private void DrawRuneConnection(SpriteBatch sb, Texture2D tex, Vector2 origin, Vector2 runePos, float pulse) {
            Vector2 direction = (Projectile.Center - runePos).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(runePos, Projectile.Center);
            int segments = (int)(dist / 15);

            for (int i = 1; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 pos = Vector2.Lerp(runePos, Projectile.Center, t);

                float segPulse = MathF.Sin(pulsePhase * 2f + t * MathHelper.Pi * 2) * 0.3f + 0.7f;
                float alpha = (1f - MathF.Abs(t - 0.5f) * 2f) * circleAlpha * pulse * segPulse * 0.4f;

                Color lineColor = new Color(180, 160, 255) * alpha;
                lineColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, lineColor, direction.ToRotation(), origin, 0.3f, SpriteEffects.None, 0);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);

            // 消散特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30;
                Vector2 dustVel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                var d = Dust.NewDustPerfect(Projectile.Center + dustVel * 5, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = dustVel;
            }
        }
    }

    /// <summary>
    /// 勾魂法杖的次级弹幕 - 游荡的勾魂使
    /// 在法阵周围游荡，额外追击敌人
    /// </summary>
    public class DemonSoulSeeker : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float wobblePhase = 0f;
        private float seekerAlpha = 0f;
        private NPC targetNPC = null;
        private Vector2 homePosition;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            if (homePosition == Vector2.Zero) {
                homePosition = Projectile.Center;
            }

            seekerAlpha = MathHelper.Lerp(seekerAlpha, 1f, 0.1f);
            pulsePhase += 0.15f;
            wobblePhase += 0.12f;

            // 寻找目标
            if (targetNPC == null || !targetNPC.active || targetNPC.friendly) {
                targetNPC = FindClosestNPC(350f);
            }

            if (targetNPC != null) {
                // 追击目标
                Vector2 toTarget = (targetNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 12f, 0.06f);
            }
            else {
                // 在基点附近游荡
                float wanderAngle = wobblePhase + Projectile.whoAmI;
                Vector2 wanderTarget = homePosition + new Vector2(MathF.Cos(wanderAngle), MathF.Sin(wanderAngle)) * 50f;
                Vector2 toWander = (wanderTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toWander * 4f, 0.03f);
            }

            // 幽灵飘动
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(wobblePhase * 2.5f) * 1f;
            Projectile.position += perpendicular * drift;

            Projectile.rotation += 0.08f;

            // 粒子
            if (Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.5f * seekerAlpha;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 130;
            }

            Lighting.AddLight(Projectile.Center, new Color(160, 140, 220).ToVector3() * 0.2f * seekerAlpha);
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

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailAlpha = progress * 0.3f * seekerAlpha;

                Color trailColor = new Color(140, 120, 200) * trailAlpha;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, 0.6f * progress, SpriteEffects.None, 0);
            }

            // 主体
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(180, 160, 240) * seekerAlpha,
                new Color(130, 110, 200),
                0.7f * pulse, pulsePhase);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.4f, Volume = 0.3f }, target.Center);
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.7f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            // 重置目标
            targetNPC = null;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.6f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }
    }
}

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
    /// 斩魂关刀 - 黑无常掉落的战士武器
    /// 大范围挥砍，释放暗影斩击波，命中敌人时有几率束缚敌人
    /// </summary>
    public class DemonicAnnihilation : ModItem
    {
        public override string Texture => BAWHelper.Path + "Items/DemonicAnnihilation";

        private int comboCount = 0;
        private int comboTimer = 0;

        public override void SetDefaults() {
            Item.damage = 125;
            Item.DamageType = DamageClass.Melee;
            Item.width = 70;
            Item.height = 70;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.LightPurple;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.useTurn = false;
            Item.shoot = ModContent.ProjectileType<DemonicSlashWave>();
            Item.shootSpeed = 12f;
            Item.scale = 1.2f;
        }

        public override void HoldItem(Player player) {
            // 连击计时器
            if (comboTimer > 0) {
                comboTimer--;
                if (comboTimer == 0) {
                    comboCount = 0;
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 连击系统
            comboCount = (comboCount + 1) % 3;
            comboTimer = 60; // 1秒内保持连击

            Vector2 direction = velocity.SafeNormalize(Vector2.Zero);

            // 根据连击段数发射不同的斩击
            switch (comboCount) {
                case 0: // 第一段：单道斩击
                    Projectile.NewProjectile(source, player.Center, direction * Item.shootSpeed, type, damage, knockback, player.whoAmI, ai0: 0);
                    break;
                case 1: // 第二段：双道交叉斩击
                    Projectile.NewProjectile(source, player.Center, direction.RotatedBy(0.2f) * Item.shootSpeed, type, (int)(damage * 0.8f), knockback, player.whoAmI, ai0: 1);
                    Projectile.NewProjectile(source, player.Center, direction.RotatedBy(-0.2f) * Item.shootSpeed, type, (int)(damage * 0.8f), knockback, player.whoAmI, ai0: 1);
                    break;
                case 2: // 第三段：大范围暗影爆发
                    Projectile.NewProjectile(source, player.Center, direction * Item.shootSpeed * 0.8f,
                        ModContent.ProjectileType<DemonicBurstWave>(), (int)(damage * 1.5f), knockback * 1.5f, player.whoAmI);

                    // 额外音效
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.2f }, player.Center);
                    break;
            }

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥砍时产生暗影粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width),
                                             hitbox.Y + Main.rand.Next(hitbox.Height));

                var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = player.velocity * 0.3f + Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时有几率释放锁链束缚效果
            if (Main.rand.NextBool(5)) {
                // 产生锁链环绕效果
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(player.GetSource_ItemUse(Item), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<DemonicChainBind>(), 0, 0, player.whoAmI, target.whoAmI, i * MathHelper.TwoPi / 3);
                }

                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.5f }, target.Center);
            }

            // 命中粒子
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override void AddRecipes() {
            // 可以添加合成配方
        }
    }

    /// <summary>
    /// 斩魂关刀的斩击波弹幕
    /// </summary>
    public class DemonicSlashWave : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float waveAlpha = 0f;
        private int slashType => (int)Projectile.ai[0]; // 0 = 单道, 1 = 交叉

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            waveAlpha = MathHelper.Lerp(waveAlpha, 1f, 0.15f);
            pulsePhase += 0.2f;

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 淡出
            if (Projectile.timeLeft < 15) {
                waveAlpha = Projectile.timeLeft / 15f;
            }

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Vector2 dustPos = Projectile.Center + perpendicular * Main.rand.NextFloat(-20, 20);

                var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.0f * waveAlpha;
                d.velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Color(80, 60, 100).ToVector3() * 0.4f * waveAlpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            // 绘制斩击波拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;

                // 斩击波形状（多层）
                int layers = slashType == 0 ? 5 : 3;
                for (int layer = -layers; layer <= layers; layer++) {
                    float layerOffset = layer * 8f * progress;
                    float layerAlpha = (1f - MathF.Abs(layer) / (layers + 1f)) * progress * waveAlpha * 0.5f;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 + perpendicular * layerOffset - Main.screenPosition;

                    // 波动效果
                    float wave = MathF.Sin(pulsePhase + i * 0.3f + layer * 0.2f) * 2f;
                    drawPos += perpendicular * wave;

                    Color trailColor = Color.Lerp(new Color(60, 40, 80), new Color(120, 80, 140), progress) * layerAlpha;
                    trailColor.A = 0;

                    float scaleX = 1.2f + progress * 0.5f;
                    float scaleY = 0.6f * progress;

                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
                }
            }

            // 主体斩击光芒
            float mainPulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.2f;
            for (int layer = -3; layer <= 3; layer++) {
                float layerOffset = layer * 10f;
                float layerAlpha = (1f - MathF.Abs(layer) / 4f) * waveAlpha * 0.7f;

                Vector2 drawPos = Projectile.Center + perpendicular * layerOffset - Main.screenPosition;

                Color waveColor = new Color(100, 70, 130) * layerAlpha;
                waveColor.A = 0;

                sb.Draw(tex, drawPos, null, waveColor, Projectile.rotation, origin, new Vector2(2f * mainPulse, 0.8f), SpriteEffects.None, 0);
            }

            // 前端亮点
            Color headColor = new Color(150, 120, 180) * waveAlpha * 0.8f;
            headColor.A = 0;
            Vector2 headPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f - Main.screenPosition;
            sb.Draw(tex, headPos, null, headColor, Projectile.rotation, origin, 1f * mainPulse, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = 0.2f, Volume = 0.6f }, target.Center);
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }
    }

    /// <summary>
    /// 斩魂关刀的暗影爆发波弹幕（第三段连击）
    /// </summary>
    public class DemonicBurstWave : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase = 0f;
        private float burstAlpha = 0f;
        private float burstRadius = 30f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            burstAlpha = MathHelper.Lerp(burstAlpha, 1f, 0.1f);
            pulsePhase += 0.15f;

            // 扩张半径
            burstRadius = MathHelper.Lerp(burstRadius, 100f, 0.08f);

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 减速
            Projectile.velocity *= 0.95f;

            // 淡出
            if (Projectile.timeLeft < 15) {
                burstAlpha = Projectile.timeLeft / 15f;
            }

            // 爆发粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(burstRadius * 0.5f, burstRadius);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f * burstAlpha;
                d.velocity = (dustPos - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f;
            }

            // 中心漩涡粒子
            if (Main.rand.NextBool(3)) {
                float spiralAngle = pulsePhase * 3f + Main.rand.NextFloat(MathHelper.TwoPi);
                float spiralDist = Main.rand.NextFloat(burstRadius * 0.2f, burstRadius * 0.6f);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * spiralDist;

                var d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.0f * burstAlpha;
                d.velocity = new Vector2(-MathF.Sin(spiralAngle), MathF.Cos(spiralAngle)) * 3f;
            }

            Lighting.AddLight(Projectile.Center, new Color(100, 70, 130).ToVector3() * 0.6f * burstAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 圆形碰撞检测
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < burstRadius + Math.Max(targetHitbox.Width, targetHitbox.Height) / 2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 绘制爆发环
            int ringSegments = 24;
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = burstRadius * (0.4f + ring * 0.3f);
                float ringAlpha = (1f - ring * 0.25f) * burstAlpha * 0.5f;
                float ringRotation = pulsePhase * (ring % 2 == 0 ? 1 : -1) * 0.5f;

                for (int i = 0; i < ringSegments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / ringSegments;
                    float segPulse = MathF.Sin(pulsePhase * 2f + angle * 3) * 0.3f + 0.7f;
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                    Color segColor = new Color(100, 70, 140) * ringAlpha * segPulse;
                    segColor.A = 0;

                    sb.Draw(tex, pos - Main.screenPosition, null, segColor, angle, origin, 0.7f * segPulse, SpriteEffects.None, 0);
                }
            }

            // 中心暗影核心
            float corePulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.25f;
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(80, 50, 120) * burstAlpha,
                new Color(120, 80, 160),
                2f * corePulse, pulsePhase);

            // 放射线
            int rayCount = 8;
            for (int i = 0; i < rayCount; i++) {
                float rayAngle = pulsePhase * 0.3f + MathHelper.TwoPi * i / rayCount;
                float rayLength = burstRadius * 0.9f;

                for (int j = 0; j < 5; j++) {
                    float t = j / 5f;
                    Vector2 rayPos = Projectile.Center + new Vector2(MathF.Cos(rayAngle), MathF.Sin(rayAngle)) * rayLength * t;
                    float rayAlpha = (1f - t) * burstAlpha * 0.4f;

                    Color rayColor = new Color(130, 100, 170) * rayAlpha;
                    rayColor.A = 0;

                    sb.Draw(tex, rayPos - Main.screenPosition, null, rayColor, rayAngle, origin, 0.5f * (1f - t * 0.5f), SpriteEffects.None, 0);
                }
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.2f, Volume = 0.7f }, target.Center);

            // 大量粒子
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);

            // 爆发消散
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
            }
        }
    }

    /// <summary>
    /// 锁链束缚效果弹幕
    /// </summary>
    public class DemonicChainBind : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float orbitAngle;
        private float bindAlpha = 0f;
        private NPC targetNPC => Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (targetNPC == null || !targetNPC.active) {
                Projectile.Kill();
                return;
            }

            bindAlpha = MathHelper.Lerp(bindAlpha, 1f, 0.1f);

            // 初始化轨道角度
            if (Projectile.localAI[0] == 0) {
                orbitAngle = Projectile.ai[1];
                Projectile.localAI[0] = 1;
            }

            // 环绕敌人
            orbitAngle += 0.1f;
            float radius = 40f + MathF.Sin(orbitAngle * 2f) * 5f;
            Projectile.Center = targetNPC.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * radius;

            // 减速敌人
            targetNPC.velocity *= 0.95f;

            // 淡出
            if (Projectile.timeLeft < 20) {
                bindAlpha = Projectile.timeLeft / 20f;
            }

            // 粒子
            if (Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 0.6f * bindAlpha;
                d.velocity = new Vector2(-MathF.Sin(orbitAngle), MathF.Cos(orbitAngle)) * 2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (targetNPC == null) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 绘制锁链到敌人中心
            Color chainColor = new Color(80, 60, 100) * bindAlpha;
            Color glowColor = new Color(120, 90, 150) * bindAlpha;

            BAWHelper.DrawGlowingChain(sb, Projectile.Center, targetNPC.Center, chainColor, glowColor,
                0.5f, 1f, 3f, orbitAngle * 10f);

            // 绘制锁链节点
            var tex = BAWHelper.DustTexture;
            if (tex != null) {
                Vector2 origin = tex.Size() / 2f;
                Color nodeColor = new Color(140, 100, 180) * bindAlpha * 0.8f;
                nodeColor.A = 0;

                float pulse = 1f + MathF.Sin(orbitAngle * 3f) * 0.2f;
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, nodeColor, orbitAngle, origin, 0.8f * pulse, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}

using AncientChineseMythology.Helpers;
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
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }

            // 命中演出 (更新阶段禁止直接绘制 — IRON RULE 1)
            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.9f, owner: player.whoAmI);
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
            if (Main.dedServ) return false;

            // 暗影斩波: 双层 GlaciateWave 弧形拖尾 + BeamGrad 裂斩核
            Color outer = new Color(70, 40, 120); outer.A = (byte)(150 * waveAlpha);
            Color inner = new Color(185, 145, 255); inner.A = (byte)(210 * waveAlpha);
            float width = slashType == 0 ? 30f : 20f;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: width,
                outerColor: outer, innerColor: inner, tex: ACMAsset.GlaciateWave,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f, subdivisions: 4);

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float len = slashType == 0 ? 60f : 44f;
            ACMShaders.DrawBeam(Projectile.Center - dir * len, Projectile.Center + dir * len * 0.4f,
                slashType == 0 ? 13f : 9f, new Color(205, 175, 255), new Color(110, 60, 185),
                waveAlpha, flowSpeed: 2.2f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f * waveAlpha + 0.2f, new Color(190, 150, 255));
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
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.7f, owner: Projectile.owner);
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
            if (Main.dedServ) return false;

            float corePulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.22f;

            // 暗影爆发: 双沿冲击环 + 核心辉光 + 径向泛光 (替代 BAWDust 多环叠贴图)
            WeaponVFX.DrawShockwaveRing(Projectile.Center, burstRadius, 14f, burstAlpha * 0.9f,
                new Color(195, 155, 255), new Color(80, 40, 135));
            WeaponVFX.DrawShockwaveRing(Projectile.Center, burstRadius * 0.6f, 9f, burstAlpha * 0.6f,
                new Color(225, 200, 255), new Color(110, 70, 170));

            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.4f * corePulse * burstAlpha + 0.2f,
                new Color(165, 115, 245));
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, 0.6f * burstAlpha,
                new Color(155, 110, 240), 10f);
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
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1.3f, owner: Projectile.owner);
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

        // 同屏每帧仅一座 ArenaRunic 牢笼罩 (多链不叠多张全屏 decal)
        private static ulong _lastCageFrame;

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

            // 束缚符环: ArenaRunic 牢笼罩绕住敌人 (三链中仅"首链" ai[1]≈0 承担, 每帧仅一张全屏 decal)
            if (Projectile.ai[1] < 0.05f && _lastCageFrame != Main.GameUpdateCount) {
                _lastCageFrame = Main.GameUpdateCount;
                Effect fx = ACMShaders.ArenaRunic;
                if (fx != null) {
                    float radius = 58f + MathF.Sin(orbitAngle) * 6f;
                    ACMShaders.WorldDecalParams(targetNPC.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uRadius"]?.SetValue(rFrac);
                    fx.Parameters["uIntensity"]?.SetValue(bindAlpha * 0.8f);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uColorPrimary"]?.SetValue(new Color(165, 115, 245).ToVector4());
                    fx.Parameters["uColorSecondary"]?.SetValue(new Color(70, 30, 125).ToVector4());
                    fx.Parameters["uRuneFreq"]?.SetValue(12f);
                    fx.Parameters["uMode"]?.SetValue(1f);  // 牢笼罩
                    fx.Parameters["uShape"]?.SetValue(0f); // 圆
                    sb.End();
                    ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }

            return false;
        }
    }
}

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
    /// 黄泉镰 - 黑无常掉落的链刃武器
    /// 丢出一个旋转的镰刀，由锁链连接玩家，可以收回
    /// </summary>
    public class NetherworldSickle : ModItem
    {
        public override string Texture => BAWHelper.Path + "Items/NetherworldSickle";

        public override void SetDefaults() {
            Item.damage = 505;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(gold: 12);
            Item.rare = ItemRarityID.LightPurple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<NetherworldSickleProjectile>();
            Item.shootSpeed = 18f;
            Item.channel = true; // 可以持续按住
        }

        public override bool CanUseItem(Player player) {
            // 只有在没有该弹幕时才能使用
            return player.ownedProjectileCounts[ModContent.ProjectileType<NetherworldSickleProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            // 可以添加合成配方
        }
    }

    /// <summary>
    /// 黄泉镰弹幕 - 链刃式飞行镰刀
    /// </summary>
    public class NetherworldSickleProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        // 状态机
        private enum SickleState
        {
            Flying,     // 飞出
            Returning,  // 返回
            Orbiting    // 绕玩家旋转
        }

        private SickleState currentState = SickleState.Flying;
        private float spinSpeed = 0.4f;
        private float pulsePhase = 0f;
        private float maxDistance = 400f;
        private float orbitAngle = 0f;
        private float orbitRadius = 80f;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            // 检查玩家状态
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            // 保持玩家使用动画
            Owner.itemAnimation = 10;
            Owner.itemTime = 10;
            Owner.heldProj = Projectile.whoAmI;

            // 面向镰刀方向
            if (Projectile.Center.X > Owner.Center.X)
                Owner.direction = 1;
            else
                Owner.direction = -1;

            pulsePhase += 0.1f;
            Projectile.rotation += spinSpeed;

            // 状态机逻辑
            switch (currentState) {
                case SickleState.Flying:
                    HandleFlyingState();
                    break;
                case SickleState.Returning:
                    HandleReturningState();
                    break;
                case SickleState.Orbiting:
                    HandleOrbitingState();
                    break;
            }

            // 锁链粒子效果
            SpawnChainParticles();

            // 光照
            Lighting.AddLight(Projectile.Center, new Color(80, 60, 100).ToVector3() * 0.4f);
        }

        private void HandleFlyingState() {
            // 减速
            Projectile.velocity *= 0.97f;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.5f, 0.02f);

            // 检查是否达到最大距离或速度过低
            float distanceToPlayer = Vector2.Distance(Projectile.Center, Owner.Center);
            if (distanceToPlayer > maxDistance || Projectile.velocity.Length() < 2f) {
                // 如果玩家仍在按住，进入环绕状态
                if (Owner.channel && Owner.controlUseItem) {
                    currentState = SickleState.Orbiting;
                    orbitAngle = (Projectile.Center - Owner.Center).ToRotation();
                    orbitRadius = Math.Min(distanceToPlayer, maxDistance * 0.6f);
                }
                else {
                    currentState = SickleState.Returning;
                }
            }

            // 如果玩家松开按键，直接返回
            if (!Owner.channel || !Owner.controlUseItem) {
                currentState = SickleState.Returning;
            }
        }

        private void HandleReturningState() {
            // 加速返回玩家
            Vector2 toPlayer = Owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);

            float returnSpeed = MathHelper.Lerp(8f, 25f, 1f - distance / maxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.15f);

            spinSpeed = MathHelper.Lerp(spinSpeed, 0.6f, 0.05f);

            // 接近玩家时消失
            if (distance < 30f) {
                Projectile.Kill();
            }
        }

        private void HandleOrbitingState() {
            // 绕玩家旋转
            float orbitSpeed = 0.08f;
            orbitAngle += orbitSpeed;

            // 根据鼠标位置调整轨道
            Vector2 mouseDir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.Zero);
            float targetAngle = mouseDir.ToRotation();
            orbitAngle = MathHelper.Lerp(orbitAngle, targetAngle, 0.05f);

            // 轨道脉动
            float radiusPulse = MathF.Sin(pulsePhase * 2f) * 10f;
            float currentRadius = orbitRadius + radiusPulse;

            Vector2 targetPos = Owner.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * currentRadius;
            Projectile.velocity = (targetPos - Projectile.Center) * 0.2f;

            spinSpeed = 0.35f + MathF.Abs(MathF.Sin(orbitAngle * 2f)) * 0.15f;

            // 松开按键返回
            if (!Owner.channel || !Owner.controlUseItem) {
                currentState = SickleState.Returning;
            }

            // 环绕时额外粒子
            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = new Vector2(-MathF.Sin(orbitAngle), MathF.Cos(orbitAngle)) * orbitSpeed * 30f;
            }
        }

        private void SpawnChainParticles() {
            if (Main.rand.NextBool(4)) {
                float t = Main.rand.NextFloat();
                Vector2 chainPoint = Vector2.Lerp(Owner.Center, Projectile.Center, t);
                var d = Dust.NewDustPerfect(chainPoint + Main.rand.NextVector2Circular(5, 5), DustID.Smoke);
                d.noGravity = true;
                d.scale = 0.5f;
                d.color = new Color(60, 60, 80);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制锁链
            DrawChain(sb);

            // 绘制镰刀拖尾
            DrawSickleTrail(sb);

            // 绘制镰刀主体
            DrawSickle(sb);

            return false;
        }

        private void DrawChain(SpriteBatch sb) {
            // 物理锁链体节 (保留拼接质感)
            float waveAmp = 6f + MathF.Sin(pulsePhase) * 3f;
            Color chainColor = new Color(70, 70, 90);
            Color glowColor = new Color(100, 80, 140);
            BAWHelper.DrawGlowingChain(sb, Owner.Center, Projectile.Center, chainColor, glowColor,
                0.7f, 1.2f, waveAmp, pulsePhase);

            // 能量链: BeamGrad 紫焰流光叠在锁链上 (additive)
            ACMShaders.DrawBeam(Owner.Center, Projectile.Center, 6f,
                new Color(210, 180, 255), new Color(110, 60, 185), 0.85f,
                flowSpeed: 2.6f, flowScale: 2.4f);
        }

        private void DrawSickleTrail(SpriteBatch sb) {
            // 双层 ribbon 链刃摆动残影 (外宽暗 + 内窄亮)
            Color outer = new Color(70, 40, 115); outer.A = 150;
            Color inner = new Color(185, 145, 255); inner.A = 200;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
                outerColor: outer, innerColor: inner, tex: ACMAsset.SoftGlow,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);
        }

        private void DrawSickle(SpriteBatch sb) {
            var tex = BAWHelper.SickleTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float glowPulse = 1f + MathF.Sin(pulsePhase * 2f) * 0.15f;

            // 镰刃能量辉光 + 径向泛光 (additive, 取代 A=0 的无效叠层)
            WeaponVFX.DrawGlowBurst(Projectile.Center, Projectile.scale * 0.95f * glowPulse, new Color(160, 110, 235));
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.05f, 0.45f, new Color(175, 125, 250), 8f);

            // 主体
            sb.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中特效
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.3f }, target.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }

            // 环绕状态下命中会产生额外暗影爆发
            if (currentState == SickleState.Orbiting) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6;
                    var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                }
            }

            // 命中演出 (更新阶段禁止直接绘制 — IRON RULE 1)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: currentState == SickleState.Orbiting ? 1.1f : 0.85f,
                owner: Projectile.owner);
        }

        public override void OnKill(int timeLeft) {
            // 回收特效
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.6f }, Owner.Center);
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 5f + Main.rand.NextVector2Circular(2, 2);
            }
        }
    }
}

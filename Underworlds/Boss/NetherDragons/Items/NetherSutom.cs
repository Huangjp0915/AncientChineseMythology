using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥召唤杖 - 召唤幽冥龙头跟随玩家并喷射火焰
    /// </summary>
    internal class NetherSutom : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults() {
            Item.damage = 120;
            Item.DamageType = DamageClass.Summon;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<NetherSutomMinon>();
            Item.shootSpeed = 0f;
            Item.mana = 10;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<NetherSutomBuff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            var projectile = Projectile.NewProjectileDirect(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<UmbralStoneItem>(), 15)
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽冥龙头召唤物Buff
    /// </summary>
    public class NetherSutomBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<NetherSutomMinon>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>
    /// 幽冥龙头召唤物 - 跟随玩家并喷射火焰
    /// </summary>
    public class NetherSutomMinon : ModProjectile
    {
        private enum MinionState
        {
            Following,  // 跟随玩家
            Attacking   // 攻击敌人
        }

        private MinionState State {
            get => (MinionState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float FlameTimer => ref Projectile.ai[1];
        private ref float AnimationFrame => ref Projectile.localAI[0];
        private ref float MinionIndex => ref Projectile.localAI[1];

        private NPC targetNPC;
        private Vector2 idlePosition;
        private Vector2 targetPosition; // 目标位置缓存
        private bool hasReachedAttackPosition; // 是否已到达攻击位置

        private const float FollowDistance = 120f;
        private const float TeleportDistance = 1200f;
        private const float FlameInterval = 60f; // 每秒喷一次火
        private const float AttackRange = 600f;
        private const float MinAttackDistance = 250f; // 最小攻击距离，避免太近
        private const float MaxAttackDistance = 450f; // 最大攻击距离
        private const float MinionSeparationRadius = 80f; // 召唤物之间的分离半径

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 2; // 两帧动画
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = false; // 龙头本身不造成伤害，只有火焰造成伤害
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() {
            return false; // 龙头本身不造成伤害
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            // 检查玩家状态
            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<NetherSutomBuff>());
                return;
            }

            // 维持Buff
            if (player.HasBuff(ModContent.BuffType<NetherSutomBuff>())) {
                Projectile.timeLeft = 2;
            }

            // 计算同类召唤物索引
            if (MinionIndex == 0) {
                int minionCount = 0;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile other = Main.projectile[i];
                    if (other.active && other.type == Projectile.type && other.owner == Projectile.owner) {
                        if (other.whoAmI == Projectile.whoAmI) {
                            MinionIndex = minionCount + 1;
                            break;
                        }
                        minionCount++;
                    }
                }
            }

            // 更新动画帧
            AnimationFrame += 0.15f;
            if (AnimationFrame >= 2f) {
                AnimationFrame = 0f;
            }
            Projectile.frame = (int)AnimationFrame;

            FlameTimer++;

            // 寻找目标
            targetNPC = FindTarget(player, AttackRange);

            if (targetNPC != null) {
                State = MinionState.Attacking;
                HandleAttackingState(player);
            }
            else {
                State = MinionState.Following;
                hasReachedAttackPosition = false;
                HandleFollowingState(player);
            }

            // 应用分离力，避免召唤物重叠
            ApplySeparationForce();

            // 产生幽冥粒子效果
            if (Main.rand.NextBool(5)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }

            // 发光效果
            Lighting.AddLight(Projectile.Center, 0.5f, 0.7f, 1.2f);
        }

        private void HandleFollowingState(Player player) {
            // 计算每个召唤物的唯一盘旋角度
            int totalMinions = CountSameMinions();
            float baseAngle = MathHelper.TwoPi * (MinionIndex - 1) / Math.Max(totalMinions, 1);
            float orbitAngle = baseAngle + Main.GlobalTimeWrappedHourly * 1.5f;
            float orbitRadius = FollowDistance + (totalMinions > 1 ? 20f * (MinionIndex - 1) : 0f);

            // 相对于玩家面向方向的后方
            Vector2 playerDirection = player.direction == 1 ? Vector2.UnitX : -Vector2.UnitX;
            Vector2 behindPlayer = player.Center - playerDirection * 60f;

            idlePosition = behindPlayer + new Vector2(
                MathF.Cos(orbitAngle) * orbitRadius * 0.5f,
                MathF.Sin(orbitAngle) * 40f - 80f
            );

            // 检查是否需要传送
            float distanceToPlayer = Vector2.Distance(Projectile.Center, player.Center);
            if (distanceToPlayer > TeleportDistance) {
                Projectile.Center = idlePosition;
                Projectile.velocity = Vector2.Zero;

                // 传送特效
                for (int i = 0; i < 20; i++) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        DustID.BlueTorch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(5, 5);
                }

                return;
            }

            // 平滑移动到跟随位置
            Vector2 toIdle = idlePosition - Projectile.Center;
            float distance = toIdle.Length();

            if (distance > 30f) {
                float speed = MathHelper.Clamp(distance * 0.08f, 3f, 16f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    toIdle.SafeNormalize(Vector2.Zero) * speed, 0.12f);
            }
            else {
                Projectile.velocity *= 0.88f;
            }

            // 龙头朝向移动方向
            if (Projectile.velocity.Length() > 0.5f) {
                float targetRotation = Projectile.velocity.ToRotation();
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);
            }
        }

        private void HandleAttackingState(Player player) {
            if (targetNPC == null || !targetNPC.active || targetNPC.life <= 0) {
                hasReachedAttackPosition = false;
                return;
            }

            Vector2 toTarget = targetNPC.Center - Projectile.Center;
            float distanceToTarget = toTarget.Length();

            // 计算理想攻击位置，避免多个召唤物重叠
            if (!hasReachedAttackPosition || Projectile.velocity.Length() < 1f) {
                // 为每个召唤物计算不同的环绕角度
                int totalMinions = CountSameMinions();
                float angleOffset = MathHelper.TwoPi * (MinionIndex - 1) / Math.Max(totalMinions, 1);
                float orbitAngle = angleOffset + Main.GlobalTimeWrappedHourly * 0.5f;

                // 在敌人周围环绕，保持合适的攻击距离
                float attackDistance = MathHelper.Lerp(MinAttackDistance, MaxAttackDistance,
                    ((MinionIndex - 1) % 3) / 3f);

                Vector2 orbitOffset = new Vector2(
                    MathF.Cos(orbitAngle) * attackDistance,
                    MathF.Sin(orbitAngle) * attackDistance * 0.6f - 80f
                );

                targetPosition = targetNPC.Center + orbitOffset;

                // 确保目标位置不会太远离玩家
                Vector2 toPlayerFromTarget = player.Center - targetPosition;
                if (toPlayerFromTarget.Length() > 800f) {
                    targetPosition = player.Center + toPlayerFromTarget.SafeNormalize(Vector2.Zero) * 800f;
                }
            }

            Vector2 toAttackPos = targetPosition - Projectile.Center;
            float distanceToAttackPos = toAttackPos.Length();

            // 移动到攻击位置
            if (distanceToAttackPos > 60f) {
                hasReachedAttackPosition = false;
                float speed = MathHelper.Clamp(distanceToAttackPos * 0.08f, 5f, 18f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    toAttackPos.SafeNormalize(Vector2.Zero) * speed, 0.15f);
            }
            else {
                hasReachedAttackPosition = true;
                // 到达攻击位置后，保持轻微移动避免完全静止
                Projectile.velocity *= 0.9f;

                // 添加轻微的环绕运动
                Vector2 orbitVelocity = toTarget.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 2f;
                Projectile.velocity += orbitVelocity * 0.1f;
            }

            // 龙头始终朝向敌人
            float targetRotation = toTarget.ToRotation();
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.2f);

            // 只有在合适的距离和角度时才喷射火焰
            bool canShoot = distanceToTarget > 150f && distanceToTarget < 700f;

            if (canShoot && FlameTimer >= FlameInterval) {
                FlameTimer = 0;
                ShootFlame(toTarget);
            }
        }

        private void ShootFlame(Vector2 direction) {
            if (Main.myPlayer != Projectile.owner)
                return;

            // 从龙头口部发射火焰
            Vector2 mouthOffset = direction.SafeNormalize(Vector2.Zero) * 30f;
            Vector2 spawnPos = Projectile.Center + mouthOffset;

            // 发射多个火焰弹幕形成扇形
            int flameCount = 3;
            float spread = 0.3f;

            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.Lerp(-spread, spread, i / (float)(flameCount - 1));
                Vector2 velocity = direction.SafeNormalize(Vector2.Zero).RotatedBy(angle) * 14f;

                int projectile = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<NetherDragonFlame>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner
                );
            }

            // 喷火音效
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);

            // 喷火粒子效果
            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = direction.SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f);
                int dust = Dust.NewDust(spawnPos, 10, 10, DustID.BlueTorch, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private NPC FindTarget(Player player, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            // 优先选择玩家的鼠标目标
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly) {
                    float dist = Vector2.Distance(targeted.Center, Projectile.Center);
                    if (dist < maxDistance * 1.5f) {
                        return targeted;
                    }
                }
            }

            // 寻找最近的敌人
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

        /// <summary>
        /// 计算同类召唤物数量
        /// </summary>
        private int CountSameMinions() {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active && other.type == Projectile.type && other.owner == Projectile.owner) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 应用分离力，避免召唤物重叠
        /// </summary>
        private void ApplySeparationForce() {
            Vector2 separationForce = Vector2.Zero;
            int nearbyCount = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];

                // 检查是否是同类召唤物且属于同一玩家
                if (other.active && other.type == Projectile.type &&
                    other.owner == Projectile.owner && other.whoAmI != Projectile.whoAmI) {
                    Vector2 diff = Projectile.Center - other.Center;
                    float distance = diff.Length();

                    // 如果距离小于分离半径，应用排斥力
                    if (distance < MinionSeparationRadius && distance > 0) {
                        // 距离越近，排斥力越强
                        float force = (MinionSeparationRadius - distance) / MinionSeparationRadius;
                        separationForce += diff.SafeNormalize(Vector2.Zero) * force * 3f;
                        nearbyCount++;
                    }
                }
            }

            // 应用平均分离力
            if (nearbyCount > 0) {
                separationForce /= nearbyCount;
                Projectile.velocity += separationForce;

                // 限制速度，避免分离力过强
                if (Projectile.velocity.Length() > 25f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 25f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            // 计算帧的矩形
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = frameRect.Size() / 2f;

            // 根据旋转角度决定翻转
            SpriteEffects effects = SpriteEffects.None;
            float rotation = Projectile.rotation - MathHelper.PiOver2;

            // 发光轮廓层（3层叠加）
            for (int i = 0; i < 3; i++) {
                Vector2 offset = new Vector2(
                    MathF.Cos(Main.GlobalTimeWrappedHourly * 4f + i * MathHelper.TwoPi / 3f),
                    MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i * MathHelper.TwoPi / 3f)) * 3f;

                Color glowColor = new Color(100, 150, 255, 0) * 0.4f;
                Main.EntitySpriteDraw(texture, Projectile.Center + offset - Main.screenPosition, frameRect,
                    glowColor, rotation, origin, Projectile.scale * 1.05f, effects);
            }

            // 主体绘制
            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frameRect,
                drawColor, rotation, origin, Projectile.scale, effects);

            // 眼睛发光效果
            Color eyeGlow = new Color(150, 200, 255, 0) * 0.8f;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frameRect,
                eyeGlow, rotation, origin, Projectile.scale * 1.02f, effects);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消失时的粒子效果
            for (int i = 0; i < 30; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(8, 8);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath55 with { Volume = 0.5f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 幽冥龙焰弹幕
    /// </summary>
    public class NetherDragonFlame : ModProjectile
    {
        private const int MaxParticles = 20;
        private List<FlameParticle> particles = new List<FlameParticle>();

        private class FlameParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public Color BaseColor;

            public FlameParticle(Vector2 pos, Vector2 vel) {
                Position = pos;
                Velocity = vel;
                MaxLife = Main.rand.NextFloat(0.5f, 1f);
                Life = MaxLife;
                Scale = Main.rand.NextFloat(0.6f, 1.2f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

                float colorMix = Main.rand.NextFloat();
                BaseColor = Color.Lerp(new Color(80, 120, 255), new Color(150, 200, 255), colorMix);
            }

            public void Update() {
                Position += Velocity;
                Velocity *= 0.96f;
                Life -= 0.03f;
                Rotation += 0.15f;
                Scale *= 0.98f;
            }

            public float Alpha => Life / MaxLife;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.1f;
            Projectile.velocity *= 0.98f;

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 持续生成火焰粒子
            for (int i = 0; i < 2; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(8, 8);
                Vector2 particleVel = Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(1.5f, 1.5f);
                particles.Add(new FlameParticle(Projectile.Center + offset, particleVel));
            }

            // 更新并移除死亡粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].Life <= 0) {
                    particles.RemoveAt(i);
                }
            }

            // 限制粒子数量
            while (particles.Count > MaxParticles) {
                particles.RemoveAt(0);
            }

            // 环境粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f,
                    100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);

            // 击中爆发粒子
            for (int i = 0; i < 10; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                particles.Add(new FlameParticle(target.Center, particleVel));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 绘制火焰粒子
            foreach (var particle in particles) {
                float alpha = particle.Alpha;

                for (int i = 0; i < 3; i++) {
                    float layerProgress = i / 3f;
                    float layerScale = particle.Scale * (1.3f - layerProgress * 0.4f);
                    float layerAlpha = alpha * (1f - layerProgress * 0.5f);

                    Color layerColor = Color.Lerp(Color.White, particle.BaseColor, layerProgress);
                    layerColor *= layerAlpha;

                    int dust = Dust.NewDustPerfect(particle.Position, DustID.BlueTorch,
                        Vector2.Zero, 0, layerColor, layerScale).dustIndex;
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].rotation = particle.Rotation;
                }
            }

            // 核心高亮
            for (int i = 0; i < 2; i++) {
                float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.3f;
                Color coreColor = new Color(200, 220, 255, 0) * 0.7f;

                int dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueFairy,
                    Vector2.Zero, 0, coreColor, 1.2f + i * 0.4f * pulseScale).dustIndex;
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.8f, 1.5f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消散粒子
            for (int i = 0; i < 15; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(5, 5);
                particles.Add(new FlameParticle(Projectile.Center, particleVel));
            }
        }
    }
}

using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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

            // 高速移动时的 DissolveBurn 冥焰残影 (拖在身后, 灼烧边)
            float headSpeed = Projectile.velocity.Length();
            if (headSpeed > 4.5f) {
                float ghost = MathHelper.Clamp((headSpeed - 4.5f) / 12f, 0f, 0.55f);
                Vector2 ghostPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 16f;
                WeaponVFX.ApplyDissolveBurn(texture, ghostPos, frameRect, NetherFX.Cyan * ghost,
                    rotation, origin, Projectile.scale, threshold: 0.4f, intensity: MathF.Min(1f, ghost * 2f),
                    edgeColor: new Color(150, 230, 255, 180), edgeWidth: 0.1f, noiseScale: 2f,
                    effects: effects);
            }

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

            // 龙口冥焰: 喷火瞬间 (FlameTimer 刚归零) 的 BeamGrad 龙息 + 口部 RadialBloom
            if (State == MinionState.Attacking && targetNPC != null && targetNPC.active && FlameTimer < 16f) {
                float flash = 1f - FlameTimer / 16f;
                Vector2 forward = Projectile.rotation.ToRotationVector2();
                Vector2 mouth = Projectile.Center + forward * 28f * Projectile.scale;
                float reach = MathHelper.Clamp(Vector2.Distance(mouth, targetNPC.Center), 90f, 540f);
                ACMShaders.DrawBeam(mouth, mouth + forward * reach, 7f + 14f * flash,
                    Color.Lerp(Color.White, NetherFX.Cyan, 0.4f), NetherFX.Violet, flash,
                    flowSpeed: 3f, flowScale: 1.8f, coreSharp: 1.8f);
                WeaponVFX.DrawRadialBloom(mouth, 0.03f + 0.06f * flash, 0.6f * flash, NetherFX.Cyan, 6f);
                WeaponVFX.DrawGlowBurst(mouth, 0.4f + 0.9f * flash, Color.Lerp(Color.White, NetherFX.Cyan, 0.3f));
            }

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
    /// 幽冥龙焰弹幕 — 龙头喷出的青蓝冥焰火舌 (纯着色器自绘, 附录 B)。
    /// </summary>
    public class NetherDragonFlame : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int MaxLife = 50;
        private float Life => 1f - Projectile.timeLeft / (float)MaxLife;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.1f;
            Projectile.velocity *= 0.98f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    Projectile.velocity * 0.2f, 120, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, NetherFX.Mix(Life).ToVector3() * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
            NetherFX.SoulDust(target.Center, 4f, 6, 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = Life;
            float size = MathHelper.Lerp(0.4f, 1f, MathF.Min(1f, life * 3f)) * (1f - life * 0.35f);

            // 火舌 BeamGrad 束 (尾→头)
            Vector2 tail = Projectile.Center;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] != Vector2.Zero) {
                    tail = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    break;
                }
            }
            ACMShaders.DrawBeam(tail, Projectile.Center, 10f + size * 14f,
                Color.Lerp(Color.White, NetherFX.Cyan, 0.4f), Color.Lerp(NetherFX.Cyan, NetherFX.Violet, life),
                1f - life * 0.5f, flowSpeed: 2.8f, flowScale: 1.6f, coreSharp: 1.7f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, size,
                Color.Lerp(Color.White, NetherFX.Cyan, 0.3f) * (1f - life * 0.4f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            NetherFX.SoulDust(Projectile.Center, 5f, 10, 1.2f);
        }
    }
}

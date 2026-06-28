using AncientChineseMythology.Helpers;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes.Items
{
    /// <summary>
    /// 幽冥狐典 - 幽冥青丘狐Boss专属魔法书
    /// 召唤九条幽冥尾巴抛射魂魄弹幕攻击敌人
    /// </summary>
    public class NetherKyuubiBook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 820;
            Item.DamageType = DamageClass.Magic;
            Item.width = 28;
            Item.height = 32;
            Item.useTime = 60;
            Item.useAnimation = 60;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 18);
            Item.rare = ItemRarityID.Cyan; // 地府强度
            Item.UseSound = SoundID.Item125;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<NetherBookTailController>();
            Item.shootSpeed = 0f;
            Item.mana = 22;
            Item.noMelee = true;
            Item.channel = false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, targetPos.X, targetPos.Y);
            ACMWeaponBurst.Spawn(source, player.Center, ACMWeaponBurst.SoulFire, scale: 0.8f, owner: player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            // TODO: 添加合成配方，使用幽冥青丘狐掉落物
        }
    }

    /// <summary>
    /// 幽冥书尾巴控制器 - 管理九条尾巴的生成和射弹攻击
    /// </summary>
    public class NetherBookTailController : ModProjectile
    {
        // 不可见控制器, 保留空白占位纹理 (纯逻辑, 不绘制)
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int TailCount = 9;
        private bool tailsSpawned = false;

        public override void SetDefaults() {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!tailsSpawned) {
                Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                SpawnAllTails(owner, targetPos);
                tailsSpawned = true;
            }

            if (Projectile.timeLeft > 5)
                Projectile.timeLeft = 5;
        }

        private void SpawnAllTails(Player owner, Vector2 targetPos) {
            // 九条尾巴从玩家背后均匀分布，向目标方向抛射
            for (int i = 0; i < TailCount; i++) {
                // 计算尾巴起始位置（扇形分布在玩家背后）
                float backAngle = (targetPos - owner.Center).ToRotation() + MathHelper.Pi;
                float spreadAngle = MathHelper.ToRadians(140f);
                float tailAngle = backAngle + MathHelper.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(TailCount - 1));

                Vector2 spawnOffset = tailAngle.ToRotationVector2() * 50f;
                Vector2 spawnPos = owner.Center + spawnOffset;

                // 每条尾巴延迟生成 (仅传延迟; 发射方向由各尾巴在 UpdateFire/FireSoulProjectiles 当场对 targetPos 计算)
                float delay = i * 4f;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<NetherBookTail>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    targetPos.X,
                    targetPos.Y,
                    delay // 传递生成延迟
                );
            }

            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 1.1f }, owner.Center);
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) => false; // 控制器不绘制
    }

    /// <summary>
    /// 幽冥书单条尾巴弹幕 - 蓄力后抛射魂魄弹
    /// </summary>
    public class NetherBookTail : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/NetherKitsunes/NetherMissesBody";

        // 尾巴参数
        private const int JointCount = 8;
        private const float BaseSegmentLength = 18f;

        private Vector2[] joints;
        private float[] segmentLengths;

        private enum TailPhase { Delay, Appear, Charge, Fire, Recover, Done }
        private TailPhase phase = TailPhase.Delay;
        private float phaseTimer = 0f;

        private Vector2 targetPos;
        private float delayTime;
        private bool hasFired = false;

        // 绘制参数
        private float glowIntensity = 0f;
        private float ghostAlpha = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (joints == null) {
                InitializeTail();
                targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                // ai[2] 即生成延迟 (发射方向当场对 targetPos 计算, 不再打包角度)
                delayTime = Projectile.ai[2];
            }

            phaseTimer++;

            switch (phase) {
                case TailPhase.Delay:
                    UpdateDelay();
                    break;
                case TailPhase.Appear:
                    UpdateAppear(owner);
                    break;
                case TailPhase.Charge:
                    UpdateCharge(owner);
                    break;
                case TailPhase.Fire:
                    UpdateFire(owner);
                    break;
                case TailPhase.Recover:
                    UpdateRecover(owner);
                    break;
                case TailPhase.Done:
                    Projectile.Kill();
                    return;
            }

            SolveFABRIK();
            Projectile.Center = joints[JointCount - 1];

            // 幽蓝色光照
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.4f, 0.7f) * glowIntensity * ghostAlpha);
        }

        private void InitializeTail() {
            joints = new Vector2[JointCount];
            segmentLengths = new float[JointCount];

            for (int i = 0; i < JointCount; i++) {
                joints[i] = Projectile.Center;
                segmentLengths[i] = BaseSegmentLength;
            }
        }

        private void UpdateDelay() {
            ghostAlpha = 0f;
            if (phaseTimer >= delayTime) {
                phase = TailPhase.Appear;
                phaseTimer = 0;
            }
        }

        private void UpdateAppear(Player owner) {
            // 尾巴从透明渐显
            float progress = phaseTimer / 15f;
            ghostAlpha = MathHelper.Clamp(progress, 0f, 1f);

            // 从玩家背后伸出
            Vector2 backDir = -(targetPos - owner.Center).SafeNormalize(Vector2.UnitX);
            float swayAngle = MathF.Sin(phaseTimer * 0.3f) * 0.2f;

            joints[0] = owner.Center + backDir * 30f;
            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                float angle = backDir.ToRotation() + swayAngle * t;
                joints[i] = joints[i - 1] + angle.ToRotationVector2() * segmentLengths[i - 1] * progress;
            }

            glowIntensity = progress * 0.3f;

            if (phaseTimer >= 15) {
                phase = TailPhase.Charge;
                phaseTimer = 0;
            }
        }

        private void UpdateCharge(Player owner) {
            // 尾巴蓄力，指向目标方向
            float progress = phaseTimer / 25f;

            Vector2 toTarget = (targetPos - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -toTarget;

            // 从后仰逐渐转向目标方向
            float aimProgress = EaseOutQuad(progress);
            Vector2 currentDir = Vector2.Lerp(backDir, toTarget, aimProgress * 0.6f);

            joints[0] = owner.Center + backDir * (25f - progress * 10f);

            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                // 末端更多地指向目标
                Vector2 segDir = Vector2.Lerp(currentDir, toTarget, t * aimProgress);
                float wobble = MathF.Sin(phaseTimer * 0.4f + i * 0.5f) * 0.1f * (1f - progress);
                joints[i] = joints[i - 1] + segDir.RotatedBy(wobble) * segmentLengths[i - 1];
            }

            glowIntensity = 0.3f + progress * 0.5f;

            // 蓄力粒子
            if (Main.rand.NextBool(3) && Main.netMode != NetmodeID.Server) {
                Vector2 dustPos = joints[JointCount - 1] + Main.rand.NextVector2Circular(15, 15);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (joints[JointCount - 1] - dustPos).SafeNormalize(Vector2.Zero) * 3f;
            }

            if (phaseTimer >= 25) {
                phase = TailPhase.Fire;
                phaseTimer = 0;
            }
        }

        private void UpdateFire(Player owner) {
            // 甩尾发射
            float progress = phaseTimer / 12f;
            float easedProgress = EaseOutQuad(progress);

            Vector2 toTarget = (targetPos - owner.Center).SafeNormalize(Vector2.UnitX);

            joints[0] = owner.Center - toTarget * 15f;

            // 快速甩向目标方向
            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                // 鞭打效果
                float whipPhase = easedProgress * MathHelper.Pi;
                float whipOffset = MathF.Sin(whipPhase + t * MathHelper.PiOver2) * 30f * (1f - easedProgress);
                Vector2 perpendicular = new Vector2(-toTarget.Y, toTarget.X);

                joints[i] = joints[i - 1] + toTarget * segmentLengths[i - 1] * (0.8f + easedProgress * 0.4f) + perpendicular * whipOffset * (1f - t);
            }

            glowIntensity = 0.8f + 0.2f * MathF.Sin(phaseTimer * 0.5f);

            // 发射魂魄弹
            if (!hasFired && phaseTimer >= 6) {
                hasFired = true;
                FireSoulProjectiles(owner);
            }

            if (phaseTimer >= 12) {
                phase = TailPhase.Recover;
                phaseTimer = 0;
            }
        }

        private void FireSoulProjectiles(Player owner) {
            if (Main.myPlayer != Projectile.owner)
                return;

            Vector2 tipPos = joints[JointCount - 1];
            Vector2 baseDir = (targetPos - owner.Center).SafeNormalize(Vector2.UnitX);

            // 发射3发魂魄弹，略微散射
            int projectileCount = 3;
            float spreadAngle = MathHelper.ToRadians(15f);

            for (int i = 0; i < projectileCount; i++) {
                float angleOffset = MathHelper.Lerp(-spreadAngle, spreadAngle, i / (float)(projectileCount - 1));
                if (projectileCount == 1) angleOffset = 0;

                Vector2 velocity = baseDir.RotatedBy(angleOffset) * 14f;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    tipPos,
                    velocity,
                    ModContent.ProjectileType<NetherSoulBolt>(),
                    Projectile.damage / 2,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner
                );
            }

            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.5f, Volume = 0.8f }, tipPos);

            // 发射特效
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustVel = baseDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(4f, 8f);
                    int dust = Dust.NewDust(tipPos, 0, 0, DustID.BlueTorch, dustVel.X, dustVel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        private void UpdateRecover(Player owner) {
            // 回收消散
            float progress = phaseTimer / 20f;

            ghostAlpha = 1f - progress;
            glowIntensity = (1f - progress) * 0.5f;

            // 尾巴下垂消散
            Vector2 backDir = -(targetPos - owner.Center).SafeNormalize(Vector2.UnitX);
            joints[0] = owner.Center + backDir * 30f;

            for (int i = 1; i < JointCount; i++) {
                Vector2 relaxDir = Vector2.Lerp(backDir, new Vector2(0, 1), progress);
                joints[i] = joints[i - 1] + relaxDir * segmentLengths[i - 1] * (1f - progress * 0.3f);
            }

            if (phaseTimer >= 20) {
                phase = TailPhase.Done;
            }
        }

        private void SolveFABRIK() {
            // 简化的FABRIK
            for (int i = 1; i < JointCount; i++) {
                Vector2 dir = (joints[i] - joints[i - 1]).SafeNormalize(Vector2.UnitY);
                joints[i] = joints[i - 1] + dir * segmentLengths[i - 1];
            }
        }

        public override bool? CanDamage() => false; // 尾巴本身不造成伤害，由射弹造成

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || joints == null || ghostAlpha <= 0.01f)
                return false;

            // 青狐火配色 (cyan fox-fire)
            byte a = (byte)MathHelper.Clamp(ghostAlpha * 255f, 0f, 255f);
            Color core = new Color(130, 240, 245) { A = (byte)(a * 0.85f) };     // 青芯
            Color outer = new Color(40, 130, 170) { A = (byte)(a * 0.6f) };      // 暗青底

            // 九尾飘带: 以 IK 关节为中心线绘制双层流动 ribbon (最契合九尾)
            float width = MathHelper.Lerp(9f, 18f, MathHelper.Clamp(glowIntensity, 0f, 1f));
            WeaponVFX.DrawRibbonTrail(joints, width, outer, core,
                tex: null, uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f, subdivisions: 4);

            // 尾尖狐火辉光
            Vector2 tip = joints[JointCount - 1];
            WeaponVFX.DrawGlowBurst(tip, 0.55f * (0.5f + glowIntensity),
                new Color(180, 250, 255) * (ghostAlpha * 0.7f));

            return false;
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }

    /// <summary>
    /// 魂魄弹 - 幽冥尾巴发射的追踪魂魄弹幕
    /// </summary>
    public class NetherSoulBolt : ModProjectile
    {
        // 占位魂弹改为纯程序化绘制 (双层拖尾 + RadialBloom 青狐火核), 保留空白占位纹理
        public override string Texture => "Terraria/Images/Projectile_1";

        private float homingStrength = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            // 旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 逐渐增加追踪强度
            if (homingStrength < 0.08f) {
                homingStrength += 0.002f;
            }

            // 寻找最近敌人并追踪
            float maxDetectRange = 400f;
            NPC closestNPC = null;
            float closestDist = maxDetectRange;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestNPC = npc;
                    }
                }
            }

            if (closestNPC != null) {
                Vector2 toTarget = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            }

            // 粒子效果
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.3f;
            }

            // 光照
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中时产生魂魄爆发
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.SoulFire, scale: 0.8f, owner: Projectile.owner);
        }

        public override void OnKill(int timeLeft) {
            // 消散效果
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Color core = new Color(140, 240, 245);  // 青狐火芯
            Color outer = new Color(40, 130, 170);   // 暗青底

            // 青狐火双层拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: outer with { A = 140 }, innerColor: core with { A = 200 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.8f);

            // 魂弹核: RadialBloom (名额仲裁; 被占退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.035f, 0.6f, core, 6f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.45f, new Color(190, 250, 255) * 0.6f);

            return false;
        }
    }
}

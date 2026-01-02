using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
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
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

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

                // 每条尾巴延迟生成
                float delay = i * 4f;

                // 计算射弹方向（向目标方向散射）
                float shotSpread = MathHelper.ToRadians(25f);
                float shotAngle = (targetPos - owner.Center).ToRotation();
                shotAngle += MathHelper.Lerp(-shotSpread, shotSpread, i / (float)(TailCount - 1));

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
                    delay + shotAngle // 传递延迟和射击角度
                );
            }

            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 1.1f }, owner.Center);
        }

        public override bool? CanDamage() => false;
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
        private float shotAngle;
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
                // 从ai[2]中解析延迟和射击角度
                float combined = Projectile.ai[2];
                delayTime = (int)combined % 100;
                shotAngle = combined - delayTime;
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
            if (joints == null || ghostAlpha <= 0.01f) return false;

            Texture2D bodyTex = NetherKitsune.NetherMissesBody;
            Texture2D tipTex = NetherKitsune.NetherMissesTop;

            if (bodyTex == null)
                bodyTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;
            if (tipTex == null)
                tipTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;

            SpriteBatch spriteBatch = Main.spriteBatch;

            // 绘制魂魄拖尾
            DrawSoulTrail(spriteBatch);

            // 绘制每个体节
            for (int i = 0; i < JointCount - 1; i++) {
                DrawSegment(spriteBatch, bodyTex, i, lightColor);
            }

            // 绘制尾尖
            DrawTip(spriteBatch, tipTex, lightColor);

            return false;
        }

        private void DrawSoulTrail(SpriteBatch spriteBatch) {
            if (glowIntensity < 0.2f) return;

            Texture2D bodyTex = NetherKitsune.NetherMissesBody;
            if (bodyTex == null) return;

            // 从尾尖向后绘制魂魄轨迹
            for (int i = JointCount - 1; i > JointCount - 4 && i > 0; i--) {
                float trailAlpha = glowIntensity * ghostAlpha * 0.3f * (i - (JointCount - 4)) / 3f;

                Vector2 pos = joints[i];
                Vector2 prevPos = joints[i - 1];
                Vector2 dir = (pos - prevPos).SafeNormalize(Vector2.UnitX);
                float rotation = dir.ToRotation();
                float length = Vector2.Distance(pos, prevPos);

                Color trailColor = new Color(80, 160, 230) * trailAlpha;
                trailColor.A = 0;

                Vector2 scale = new Vector2(length / bodyTex.Width * 1.5f, 0.4f);

                spriteBatch.Draw(
                    bodyTex,
                    (pos + prevPos) * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    rotation,
                    new Vector2(bodyTex.Width * 0.5f, bodyTex.Height * 0.5f),
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawSegment(SpriteBatch spriteBatch, Texture2D texture, int index, Color lightColor) {
            if (index >= JointCount - 1) return;

            Vector2 start = joints[index];
            Vector2 end = joints[index + 1];
            Vector2 direction = end - start;
            float rotation = direction.ToRotation();
            float length = direction.Length();

            float widthScale = MathHelper.Lerp(0.8f, 0.3f, (float)index / (JointCount - 1));

            // 幽蓝色调
            Color baseColor = Color.Lerp(lightColor, new Color(90, 160, 220), 0.5f);
            Color drawColor = Color.Lerp(baseColor, new Color(130, 200, 255), glowIntensity * 0.5f);
            drawColor *= ghostAlpha;

            Vector2 center = (start + end) * 0.5f;
            Vector2 scale = new Vector2(length / texture.Width, widthScale);

            spriteBatch.Draw(
                texture,
                center - Main.screenPosition,
                null,
                drawColor,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                scale,
                SpriteEffects.None,
                0f
            );

            // 发光层
            if (glowIntensity > 0.1f) {
                Color glowColor = new Color(100, 180, 255) * glowIntensity * ghostAlpha * 0.4f;
                glowColor.A = 0;
                spriteBatch.Draw(
                    texture,
                    center - Main.screenPosition,
                    null,
                    glowColor,
                    rotation,
                    new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                    scale * 1.3f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTip(SpriteBatch spriteBatch, Texture2D texture, Color lightColor) {
            if (JointCount < 2) return;

            Vector2 lastJoint = joints[JointCount - 1];
            Vector2 prevJoint = joints[JointCount - 2];
            Vector2 direction = (lastJoint - prevJoint).SafeNormalize(Vector2.UnitY);
            float rotation = direction.ToRotation();

            Color baseColor = Color.Lerp(lightColor, new Color(110, 190, 255), 0.6f);
            Color tipColor = Color.Lerp(baseColor, new Color(160, 220, 255), glowIntensity);
            tipColor *= ghostAlpha;

            float tipScale = 0.4f;

            spriteBatch.Draw(
                texture,
                lastJoint - Main.screenPosition,
                null,
                tipColor,
                rotation,
                new Vector2(0, texture.Height * 0.5f),
                tipScale,
                SpriteEffects.None,
                0f
            );

            // 尾尖发光
            if (glowIntensity > 0.2f) {
                Color glowColor = new Color(130, 200, 255) * glowIntensity * ghostAlpha * 0.5f;
                glowColor.A = 0;
                spriteBatch.Draw(
                    texture,
                    lastJoint - Main.screenPosition,
                    null,
                    glowColor,
                    rotation,
                    new Vector2(0, texture.Height * 0.5f),
                    tipScale * 1.5f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }

    /// <summary>
    /// 魂魄弹 - 幽冥尾巴发射的追踪魂魄弹幕
    /// </summary>
    public class NetherSoulBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpectreWrath;

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
            Main.instance.LoadProjectile(ProjectileID.ShadowOrb);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.ShadowOrb].Value;
            Vector2 origin = texture.Size() / 2f;

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                // 幽蓝色拖尾
                Color trailColor = new Color(80, 150, 220) * progress * 0.5f;
                trailColor.A = 0;

                Main.EntitySpriteDraw(texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale * (0.6f + progress * 0.4f), SpriteEffects.None);
            }

            // 主体 - 幽蓝色
            Color mainColor = new Color(120, 200, 255);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            // 发光核心
            Color coreColor = new Color(180, 230, 255) * 0.6f;
            coreColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                coreColor, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None);

            return false;
        }
    }
}

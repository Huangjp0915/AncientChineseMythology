using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes.Items
{
    /// <summary>
    /// 九尾天书 - 九尾狐Boss专属魔法书
    /// 召唤九条狐狸尾巴从玩家背后刺向敌人
    /// </summary>
    public class KyuubiBook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 185;
            Item.DamageType = DamageClass.Magic;
            Item.width = 28;
            Item.height = 32;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 12);
            Item.rare = ItemRarityID.Yellow; // 石巨人后强度
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<KyuubiBookTailController>();
            Item.shootSpeed = 0f;
            Item.mana = 25;
            Item.noMelee = true;
            Item.channel = false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 生成尾巴控制器弹幕
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, targetPos.X, targetPos.Y);
            return false;
        }

        public override void AddRecipes() {
            // TODO: 添加合成配方，使用九尾狐掉落物
        }
    }

    /// <summary>
    /// 九尾书尾巴控制器 - 管理九条尾巴的生成和攻击
    /// </summary>
    public class KyuubiBookTailController : ModProjectile
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
            Projectile.timeLeft = 120;
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

            // 只在第一帧生成所有尾巴
            if (!tailsSpawned) {
                Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                SpawnAllTails(owner, targetPos);
                tailsSpawned = true;
            }

            // 控制器只存在很短时间
            if (Projectile.timeLeft > 5)
                Projectile.timeLeft = 5;
        }

        private void SpawnAllTails(Player owner, Vector2 targetPos) {
            // 计算玩家到目标的方向
            Vector2 toTarget = (targetPos - owner.Center).SafeNormalize(Vector2.UnitX);

            // 九条尾巴从玩家背后均匀分布
            for (int i = 0; i < TailCount; i++) {
                // 计算每条尾巴的起始角度（背向目标方向，扇形分布）
                float backAngle = toTarget.ToRotation() + MathHelper.Pi;
                float spreadAngle = MathHelper.ToRadians(120f); // 120度扇形
                float tailAngle = backAngle + MathHelper.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(TailCount - 1));

                // 尾巴起始位置在玩家背后
                Vector2 spawnOffset = tailAngle.ToRotationVector2() * 60f;
                Vector2 spawnPos = owner.Center + spawnOffset;

                // 延迟生成，产生波浪效果
                float delay = i * 3f; // 每条尾巴延迟3帧

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<KyuubiBookTail>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    targetPos.X,
                    targetPos.Y,
                    delay
                );
            }

            // 播放音效
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 1.2f }, owner.Center);
        }

        public override bool? CanDamage() => false;
    }

    /// <summary>
    /// 九尾书单条尾巴弹幕 - 使用简化的IK系统模拟尾巴
    /// </summary>
    public class KyuubiBookTail : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesBody";

        // 尾巴参数
        private const int JointCount = 10;
        private const float BaseSegmentLength = 20f;
        private const float MaxExtension = 3.5f;

        private Vector2[] joints;
        private float[] segmentLengths;
        private float currentExtension = 1f;
        private float targetExtension = 1f;

        private enum TailPhase { Delay, Coil, Telegraph, Stab, Recover, Done }
        private TailPhase phase = TailPhase.Delay;
        private float phaseTimer = 0f;

        private Vector2 targetPos;
        private Vector2 stabDirection;
        private float delayTime;

        // 绘制参数
        private float glowIntensity = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            // 初始化
            if (joints == null) {
                InitializeTail();
                targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                delayTime = Projectile.ai[2];
                stabDirection = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
            }

            phaseTimer++;

            // 阶段状态机
            switch (phase) {
                case TailPhase.Delay:
                    UpdateDelay();
                    break;
                case TailPhase.Coil:
                    UpdateCoil(owner);
                    break;
                case TailPhase.Telegraph:
                    UpdateTelegraph(owner);
                    break;
                case TailPhase.Stab:
                    UpdateStab(owner);
                    break;
                case TailPhase.Recover:
                    UpdateRecover(owner);
                    break;
                case TailPhase.Done:
                    Projectile.Kill();
                    return;
            }

            // 更新延展
            currentExtension = MathHelper.Lerp(currentExtension, targetExtension, 0.2f);
            UpdateSegmentLengths();

            // 更新IK
            SolveFABRIK();

            // 更新弹幕位置为尾尖
            Projectile.Center = joints[JointCount - 1];

            // 发光
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.5f, 0.2f) * glowIntensity * 0.5f);
        }

        private void InitializeTail() {
            joints = new Vector2[JointCount];
            segmentLengths = new float[JointCount];

            for (int i = 0; i < JointCount; i++) {
                joints[i] = Projectile.Center;
                segmentLengths[i] = BaseSegmentLength;
            }
        }

        private void UpdateSegmentLengths() {
            for (int i = 0; i < JointCount; i++) {
                float factor = MathHelper.Lerp(0.6f, 1.4f, (float)i / (JointCount - 1));
                segmentLengths[i] = BaseSegmentLength * (1f + (currentExtension - 1f) * factor);
            }
        }

        private void UpdateDelay() {
            if (phaseTimer >= delayTime) {
                phase = TailPhase.Coil;
                phaseTimer = 0;
            }
        }

        private void UpdateCoil(Player owner) {
            // 尾巴蜷缩在玩家背后
            Vector2 backDir = -stabDirection;
            float coilRadius = 40f + 20f * MathF.Sin(phaseTimer * 0.2f);
            joints[0] = owner.Center + backDir * 30f;

            for (int i = 1; i < JointCount; i++) {
                float angle = backDir.ToRotation() + MathF.Sin(phaseTimer * 0.15f + i * 0.5f) * 0.5f;
                joints[i] = joints[i - 1] + angle.ToRotationVector2() * segmentLengths[i - 1] * 0.7f;
            }

            glowIntensity = MathHelper.Lerp(glowIntensity, 0.3f, 0.1f);

            if (phaseTimer >= 15) {
                phase = TailPhase.Telegraph;
                phaseTimer = 0;
            }
        }

        private void UpdateTelegraph(Player owner) {
            // 预判阶段 - 尾巴指向目标方向蓄力
            float progress = phaseTimer / 20f;
            joints[0] = owner.Center - stabDirection * 30f;

            // 尾巴逐渐拉直指向目标
            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                Vector2 idealPos = joints[0] + stabDirection * t * JointCount * BaseSegmentLength * 0.4f;
                Vector2 coilPos = joints[i - 1] + (joints[i] - joints[i - 1]).SafeNormalize(stabDirection) * segmentLengths[i - 1];
                joints[i] = Vector2.Lerp(coilPos, idealPos, progress * 0.5f);
            }

            glowIntensity = MathHelper.Lerp(glowIntensity, 0.7f, 0.1f);

            // 蓄力粒子
            if (Main.rand.NextBool(3)) {
                Vector2 dustPos = joints[JointCount - 1] + Main.rand.NextVector2Circular(20, 20);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, -stabDirection * 2f, 100, default, 1.5f);
                d.noGravity = true;
            }

            if (phaseTimer >= 20) {
                phase = TailPhase.Stab;
                phaseTimer = 0;
                targetExtension = MaxExtension;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void UpdateStab(Player owner) {
            // 刺出阶段 - 极速延展
            float progress = MathHelper.Clamp(phaseTimer / 8f, 0f, 1f);
            float easedProgress = 1f - MathF.Pow(1f - progress, 3f); // EaseOutCubic

            joints[0] = owner.Center - stabDirection * 20f;

            // 计算当前延展长度
            float totalLength = 0f;
            for (int i = 0; i < JointCount - 1; i++)
                totalLength += segmentLengths[i];

            // 尾巴笔直刺出
            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                joints[i] = joints[0] + stabDirection * totalLength * t * easedProgress;
            }

            glowIntensity = 1f;

            // 刺出粒子
            if (phaseTimer % 2 == 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = joints[JointCount - 1] + Main.rand.NextVector2Circular(10, 10);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, stabDirection * Main.rand.NextFloat(5f, 10f), 150, default, 2f);
                    d.noGravity = true;
                }
            }

            if (phaseTimer >= 12) {
                phase = TailPhase.Recover;
                phaseTimer = 0;
                targetExtension = 1f;
            }
        }

        private void UpdateRecover(Player owner) {
            // 回收阶段
            float progress = MathHelper.Clamp(phaseTimer / 25f, 0f, 1f);

            joints[0] = owner.Center - stabDirection * MathHelper.Lerp(20f, 40f, progress);

            // 尾巴收回并自然下垂
            float totalLength = 0f;
            for (int i = 0; i < JointCount - 1; i++)
                totalLength += segmentLengths[i];

            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                Vector2 extendedPos = joints[0] + stabDirection * totalLength * t;
                Vector2 relaxedPos = joints[i - 1] + new Vector2(0, segmentLengths[i - 1] * 0.8f);
                joints[i] = Vector2.Lerp(extendedPos, relaxedPos, progress);
            }

            glowIntensity = MathHelper.Lerp(1f, 0f, progress);
            Projectile.alpha = (int)(progress * 255);

            if (phaseTimer >= 30) {
                phase = TailPhase.Done;
            }
        }

        private void SolveFABRIK() {
            // 简化的FABRIK，只做长度约束
            for (int i = 1; i < JointCount; i++) {
                Vector2 dir = (joints[i] - joints[i - 1]).SafeNormalize(Vector2.UnitY);
                joints[i] = joints[i - 1] + dir * segmentLengths[i - 1];
            }
        }

        public override bool? CanDamage() {
            // 只在刺出阶段造成伤害
            return phase == TailPhase.Stab;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中时产生火焰粒子
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel, 100, default, 2f);
                d.noGravity = true;
            }

            // 仅刺出阶段命中 (CanDamage 限定) → 九尾金橙狐火演出
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Fox, scale: 0.8f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (joints == null) return false;

            // 狐火金橙双层 ribbon 覆盖在尾骨体上 (外暗内亮) + 尾尖柔光
            if (glowIntensity > 0.05f) {
                Color outer = new Color(200, 70, 25, (int)(160 * glowIntensity));
                Color inner = new Color(255, 215, 120, (int)(200 * glowIntensity));
                WeaponVFX.DrawRibbonTrail(joints, baseWidth: 18f,
                    outerColor: outer, innerColor: inner,
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
                WeaponVFX.DrawGlowBurst(joints[JointCount - 1], 0.5f + glowIntensity * 0.8f,
                    new Color(255, 200, 90) * glowIntensity);
            }

            Texture2D bodyTex = KyuubiKitsune.MissesBody;
            Texture2D tipTex = KyuubiKitsune.MissesTop;

            // 如果Boss纹理未加载，使用备用
            if (bodyTex == null)
                bodyTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;
            if (tipTex == null)
                tipTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;

            SpriteBatch spriteBatch = Main.spriteBatch;

            // 绘制每个体节
            for (int i = 0; i < JointCount - 1; i++) {
                Vector2 start = joints[i];
                Vector2 end = joints[i + 1];
                Vector2 direction = end - start;
                float rotation = direction.ToRotation();
                float length = direction.Length();

                // 宽度渐变
                float widthScale = MathHelper.Lerp(1f, 0.4f, (float)i / (JointCount - 1));

                // 颜色混合
                Color drawColor = Color.Lerp(lightColor, Color.OrangeRed, glowIntensity * 0.6f);
                drawColor *= 1f - Projectile.alpha / 255f;

                Vector2 center = (start + end) * 0.5f;
                Vector2 scale = new Vector2(length / bodyTex.Width, widthScale);

                spriteBatch.Draw(
                    bodyTex,
                    center - Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    new Vector2(bodyTex.Width * 0.5f, bodyTex.Height * 0.5f),
                    scale,
                    SpriteEffects.None,
                    0f
                );

                // 发光层
                if (glowIntensity > 0) {
                    Color glowColor = Color.OrangeRed * glowIntensity * 0.4f;
                    glowColor.A = 0;
                    spriteBatch.Draw(
                        bodyTex,
                        center - Main.screenPosition,
                        null,
                        glowColor,
                        rotation,
                        new Vector2(bodyTex.Width * 0.5f, bodyTex.Height * 0.5f),
                        scale * 1.2f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // 绘制尾尖
            if (JointCount >= 2) {
                Vector2 lastJoint = joints[JointCount - 1];
                Vector2 prevJoint = joints[JointCount - 2];
                Vector2 tipDir = (lastJoint - prevJoint).SafeNormalize(Vector2.UnitX);
                float tipRotation = tipDir.ToRotation();

                Color tipColor = Color.Lerp(lightColor, Color.Gold, glowIntensity);
                tipColor *= 1f - Projectile.alpha / 255f;
                float tipScale = 0.5f;

                spriteBatch.Draw(
                    tipTex,
                    lastJoint - Main.screenPosition,
                    null,
                    tipColor,
                    tipRotation,
                    new Vector2(0, tipTex.Height * 0.5f),
                    tipScale,
                    SpriteEffects.None,
                    0f
                );

                // 尾尖发光
                if (glowIntensity > 0) {
                    Color glowColor = Color.Gold * glowIntensity * 0.5f;
                    glowColor.A = 0;
                    spriteBatch.Draw(
                        tipTex,
                        lastJoint - Main.screenPosition,
                        null,
                        glowColor,
                        tipRotation,
                        new Vector2(0, tipTex.Height * 0.5f),
                        tipScale * 1.3f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false;
        }
    }
}

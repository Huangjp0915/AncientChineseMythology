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
    /// 妲己之书 - 九尾天书与幽冥狐典的合体上位武器
    /// 独特机制：九条尾巴会先在目标周围形成"魅惑之环"缠绕锁定敌人，
    /// 然后同时收缩刺击，最后爆发出火焰与魂魄交织的毁灭波动
    /// </summary>
    public class DakkiBook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2380;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 36;
            Item.useTime = 50;
            Item.useAnimation = 50;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(gold: 35);
            Item.rare = ItemRarityID.Purple; // 最高稀有度
            Item.UseSound = SoundID.Item119;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DakkiBookController>();
            Item.shootSpeed = 0f;
            Item.mana = 35;
            Item.noMelee = true;
            Item.channel = false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 targetPos = Main.MouseWorld;
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, targetPos.X, targetPos.Y);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "DakkiLore", "「千年狐妖之魂，魅惑天下之书」"));
        }

        public override void AddRecipes() {
            // 合成：九尾天书 + 幽冥狐典 + 高级材料
            // CreateRecipe()
            //     .AddIngredient<KyuubiBook>()
            //     .AddIngredient<NetherKyuubiBook>()
            //     .AddTile(TileID.LunarCraftingStation)
            //     .Register();
        }
    }

    /// <summary>
    /// 妲己书控制器 - 管理魅惑之环的形成和爆发
    /// </summary>
    public class DakkiBookController : ModProjectile
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
            Projectile.timeLeft = 200;
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
                SpawnCharmRing(owner, targetPos);
                tailsSpawned = true;
            }

            if (Projectile.timeLeft > 5)
                Projectile.timeLeft = 5;
        }

        private void SpawnCharmRing(Player owner, Vector2 targetPos) {
            // 先生成魅惑之环标记
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                targetPos,
                Vector2.Zero,
                ModContent.ProjectileType<DakkiCharmRing>(),
                Projectile.damage,
                0,
                Projectile.owner
            );

            // 九条尾巴从玩家身后出发，飞向目标形成包围圈
            for (int i = 0; i < TailCount; i++) {
                // 起始位置：玩家背后扇形分布
                float backAngle = (targetPos - owner.Center).ToRotation() + MathHelper.Pi;
                float spreadAngle = MathHelper.ToRadians(160f);
                float tailAngle = backAngle + MathHelper.Lerp(-spreadAngle / 2f, spreadAngle / 2f, i / (float)(TailCount - 1));

                Vector2 spawnPos = owner.Center + tailAngle.ToRotationVector2() * 50f;

                // 目标位置：围绕目标点形成圆环
                float ringAngle = MathHelper.TwoPi * i / TailCount;
                float ringRadius = 180f;
                Vector2 ringPos = targetPos + new Vector2(MathF.Cos(ringAngle), MathF.Sin(ringAngle)) * ringRadius;

                // 延迟生成
                float delay = i * 2f;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<DakkiBookTail>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ringPos.X, // 环绕目标位置
                    ringPos.Y,
                    delay + ringAngle * 100f // 传递延迟和环绕角度
                );
            }

            SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f, Volume = 1.3f }, owner.Center);
        }

        public override bool? CanDamage() => false;
    }

    /// <summary>
    /// 魅惑之环 - 在目标位置显示的视觉效果和伤害区域
    /// </summary>
    public class DakkiCharmRing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        private float ringRadius = 180f;
        private float ringAlpha = 0f;
        private float rotationOffset = 0f;
        private int phase = 0; // 0=扩张, 1=稳定, 2=收缩爆发, 3=消散
        private float phaseTimer = 0f;
        private bool hasExploded = false;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            phaseTimer++;
            rotationOffset += 0.03f;

            switch (phase) {
                case 0: // 扩张形成
                    ringAlpha = MathHelper.Clamp(phaseTimer / 30f, 0f, 0.8f);
                    if (phaseTimer >= 30) {
                        phase = 1;
                        phaseTimer = 0;
                    }
                    break;

                case 1: // 稳定缠绕
                    ringAlpha = 0.8f + 0.1f * MathF.Sin(phaseTimer * 0.2f);
                    // 缠绕粒子
                    if (Main.netMode != NetmodeID.Server && phaseTimer % 3 == 0) {
                        SpawnCharmParticles();
                    }
                    if (phaseTimer >= 50) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;

                case 2: // 收缩爆发
                    float shrinkProgress = phaseTimer / 20f;
                    ringRadius = MathHelper.Lerp(180f, 0f, EaseInQuad(shrinkProgress));
                    ringAlpha = 1f;

                    if (!hasExploded && phaseTimer >= 18) {
                        hasExploded = true;
                        TriggerExplosion();
                    }

                    if (phaseTimer >= 25) {
                        phase = 3;
                        phaseTimer = 0;
                    }
                    break;

                case 3: // 消散
                    ringAlpha = MathHelper.Clamp(1f - phaseTimer / 15f, 0f, 1f);
                    if (phaseTimer >= 15) {
                        Projectile.Kill();
                    }
                    break;
            }

            // 对范围内敌人造成持续伤害（缠绕阶段）
            if (phase == 1 || phase == 2) {
                Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.4f, 0.6f) * ringAlpha);
            }
        }

        private void SpawnCharmParticles() {
            // 双色粒子环绕
            for (int i = 0; i < 3; i++) {
                float angle = rotationOffset + MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                // 交替火焰和魂魄粒子
                int dustType = i % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                Vector2 vel = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;

                int dust = Dust.NewDust(pos, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void TriggerExplosion() {
            if (Main.myPlayer != Projectile.owner)
                return;

            // 发射双色爆发射弹
            int burstCount = 16;
            for (int i = 0; i < burstCount; i++) {
                float angle = MathHelper.TwoPi * i / burstCount;
                Vector2 velocity = angle.ToRotationVector2() * 12f;

                // 交替发射火焰弹和魂魄弹
                int projType = i % 2 == 0
                    ? ModContent.ProjectileType<DakkiFireBolt>()
                    : ModContent.ProjectileType<DakkiSoulBolt>();

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    projType,
                    Projectile.damage / 2,
                    Projectile.knockBack * 0.3f,
                    Projectile.owner
                );
            }

            // 爆发音效和视觉效果
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 1.0f }, Projectile.Center);

            // 大量双色粒子爆发
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 50; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(15, 15);
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 屏幕震动
            if (Main.LocalPlayer.Distance(Projectile.Center) < 800f) {
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(12, 20);
            }
        }

        public override bool? CanDamage() {
            return phase == 2 && !hasExploded; // 收缩时造成伤害
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 环形碰撞检测
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < ringRadius + 50f && dist > ringRadius - 50f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ringAlpha <= 0.01f) return false;

            SpriteBatch sb = Main.spriteBatch;

            // 绘制魅惑之环
            DrawCharmRing(sb);

            return false;
        }

        private void DrawCharmRing(SpriteBatch sb) {
            // 使用粒子/线条绘制环形
            int segments = 36;

            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments + rotationOffset;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments + rotationOffset;

                Vector2 pos1 = Projectile.Center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * ringRadius;
                Vector2 pos2 = Projectile.Center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * ringRadius;

                // 双色渐变：火焰色和魂魄色交替
                float colorLerp = (MathF.Sin(angle1 * 3f + rotationOffset * 2f) + 1f) * 0.5f;
                Color fireColor = new Color(255, 150, 50);
                Color soulColor = new Color(100, 180, 255);
                Color segColor = Color.Lerp(fireColor, soulColor, colorLerp) * ringAlpha;
                segColor.A = 0;

                // 简单的线段绘制（使用像素纹理）
                Vector2 center = (pos1 + pos2) * 0.5f - Main.screenPosition;
                float length = Vector2.Distance(pos1, pos2);
                float rotation = (pos2 - pos1).ToRotation();

                // 使用魔法像素或简单纹理
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Rectangle sourceRect = new Rectangle(0, 0, 1, 1);
                Vector2 scale = new Vector2(length, 4f + 2f * MathF.Sin(angle1 * 2f));

                sb.Draw(pixel, center, sourceRect, segColor, rotation, new Vector2(0, 0.5f), scale, SpriteEffects.None, 0f);

                // 外发光
                Color glowColor = segColor * 0.4f;
                glowColor.A = 0;
                sb.Draw(pixel, center, sourceRect, glowColor, rotation, new Vector2(0, 0.5f), scale * new Vector2(1f, 2.5f), SpriteEffects.None, 0f);
            }

            // 中心光点
            Texture2D glowTex = TextureAssets.Extra[98].Value;
            Color centerColor = Color.Lerp(new Color(255, 180, 100), new Color(150, 200, 255),
                (MathF.Sin(rotationOffset * 2f) + 1f) * 0.5f) * ringAlpha * 0.5f;
            centerColor.A = 0;
            sb.Draw(glowTex, Projectile.Center - Main.screenPosition, null, centerColor, 0f,
                glowTex.Size() * 0.5f, 0.8f, SpriteEffects.None, 0f);
        }

        private static float EaseInQuad(float t) => t * t;
    }

    /// <summary>
    /// 妲己书尾巴 - 飞向目标形成魅惑之环，然后刺向中心
    /// </summary>
    public class DakkiBookTail : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesBody";

        private const int JointCount = 12;
        private const float BaseSegmentLength = 22f;
        private const float MaxExtension = 3.0f;

        private Vector2[] joints;
        private float[] segmentLengths;
        private float currentExtension = 1f;
        private float targetExtension = 1f;

        private enum TailPhase { Delay, FlyToRing, Encircle, ChargeStab, Stab, Dissipate, Done }
        private TailPhase phase = TailPhase.Delay;
        private float phaseTimer = 0f;

        private Vector2 ringPosition;
        private float ringAngle;
        private float delayTime;
        private Vector2 ringCenter;

        private float glowIntensity = 0f;
        private float colorShift = 0f; // 0=火焰, 1=魂魄

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 250;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (joints == null) {
                InitializeTail();
                ringPosition = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                float combined = Projectile.ai[2];
                delayTime = (int)combined % 100;
                ringAngle = (combined - delayTime) / 100f;

                // 计算环中心（反推）
                ringCenter = ringPosition - new Vector2(MathF.Cos(ringAngle), MathF.Sin(ringAngle)) * 180f;
            }

            phaseTimer++;

            // 颜色在火焰和魂魄之间循环变化
            colorShift = (MathF.Sin(phaseTimer * 0.08f + ringAngle) + 1f) * 0.5f;

            switch (phase) {
                case TailPhase.Delay:
                    UpdateDelay();
                    break;
                case TailPhase.FlyToRing:
                    UpdateFlyToRing(owner);
                    break;
                case TailPhase.Encircle:
                    UpdateEncircle();
                    break;
                case TailPhase.ChargeStab:
                    UpdateChargeStab();
                    break;
                case TailPhase.Stab:
                    UpdateStab();
                    break;
                case TailPhase.Dissipate:
                    UpdateDissipate();
                    break;
                case TailPhase.Done:
                    Projectile.Kill();
                    return;
            }

            currentExtension = MathHelper.Lerp(currentExtension, targetExtension, 0.15f);
            UpdateSegmentLengths();
            SolveFABRIK();

            Projectile.Center = joints[JointCount - 1];

            // 双色光照
            Vector3 fireLight = new Vector3(1f, 0.5f, 0.2f);
            Vector3 soulLight = new Vector3(0.3f, 0.5f, 0.8f);
            Vector3 lightColor = Vector3.Lerp(fireLight, soulLight, colorShift) * glowIntensity * 0.6f;
            Lighting.AddLight(Projectile.Center, lightColor);
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
                float factor = MathHelper.Lerp(0.5f, 1.5f, (float)i / (JointCount - 1));
                segmentLengths[i] = BaseSegmentLength * (1f + (currentExtension - 1f) * factor);
            }
        }

        private void UpdateDelay() {
            glowIntensity = 0f;
            if (phaseTimer >= delayTime) {
                phase = TailPhase.FlyToRing;
                phaseTimer = 0;
            }
        }

        private void UpdateFlyToRing(Player owner) {
            // 尾巴从玩家飞向环上的位置
            float progress = MathHelper.Clamp(phaseTimer / 25f, 0f, 1f);
            float easedProgress = EaseOutCubic(progress);

            Vector2 startPos = owner.Center;
            joints[0] = Vector2.Lerp(startPos, ringPosition - (ringPosition - ringCenter).SafeNormalize(Vector2.Zero) * 100f, easedProgress);

            Vector2 toRing = (ringPosition - joints[0]).SafeNormalize(Vector2.UnitX);
            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                float wobble = MathF.Sin(phaseTimer * 0.3f + i * 0.4f) * (1f - progress) * 0.3f;
                joints[i] = joints[i - 1] + toRing.RotatedBy(wobble) * segmentLengths[i - 1] * progress;
            }

            glowIntensity = MathHelper.Lerp(0f, 0.6f, progress);

            if (phaseTimer >= 25) {
                phase = TailPhase.Encircle;
                phaseTimer = 0;
            }
        }

        private void UpdateEncircle() {
            // 在环上缠绕旋转
            float rotateSpeed = 0.04f;
            float currentAngle = ringAngle + phaseTimer * rotateSpeed;

            Vector2 currentRingPos = ringCenter + new Vector2(MathF.Cos(currentAngle), MathF.Sin(currentAngle)) * 180f;
            Vector2 tangent = new Vector2(-MathF.Sin(currentAngle), MathF.Cos(currentAngle));

            joints[0] = currentRingPos - tangent * 80f;

            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                // 尾巴沿切线方向延伸，略微向外弯曲
                Vector2 outward = (currentRingPos - ringCenter).SafeNormalize(Vector2.Zero);
                Vector2 segDir = Vector2.Lerp(tangent, outward, t * 0.3f);
                float wave = MathF.Sin(phaseTimer * 0.15f + i * 0.5f) * 0.15f;
                joints[i] = joints[i - 1] + segDir.RotatedBy(wave) * segmentLengths[i - 1];
            }

            glowIntensity = 0.6f + 0.2f * MathF.Sin(phaseTimer * 0.2f);

            // 缠绕粒子
            if (Main.netMode != NetmodeID.Server && phaseTimer % 5 == 0) {
                int dustType = colorShift > 0.5f ? DustID.BlueTorch : DustID.GoldFlame;
                Vector2 dustPos = joints[JointCount - 1] + Main.rand.NextVector2Circular(10, 10);
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, tangent.X * 2f, tangent.Y * 2f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            if (phaseTimer >= 50) {
                phase = TailPhase.ChargeStab;
                phaseTimer = 0;
            }
        }

        private void UpdateChargeStab() {
            // 蓄力准备刺向中心
            float progress = phaseTimer / 15f;

            Vector2 toCenter = (ringCenter - joints[0]).SafeNormalize(Vector2.UnitX);

            // 尾巴后撤蓄力
            float pullBack = MathF.Sin(progress * MathHelper.Pi) * 30f;
            joints[0] = ringPosition - toCenter * pullBack;

            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                Vector2 segDir = Vector2.Lerp(-toCenter, toCenter, progress * t);
                joints[i] = joints[i - 1] + segDir * segmentLengths[i - 1];
            }

            glowIntensity = MathHelper.Lerp(0.6f, 1f, progress);
            targetExtension = MathHelper.Lerp(1f, MaxExtension, progress);

            if (phaseTimer >= 15) {
                phase = TailPhase.Stab;
                phaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void UpdateStab() {
            // 极速刺向中心
            float progress = MathHelper.Clamp(phaseTimer / 10f, 0f, 1f);
            float easedProgress = EaseOutQuad(progress);

            Vector2 toCenter = (ringCenter - ringPosition).SafeNormalize(Vector2.UnitX);

            // 从环位置刺向中心
            float totalLength = 0f;
            for (int i = 0; i < JointCount - 1; i++)
                totalLength += segmentLengths[i];

            joints[0] = Vector2.Lerp(ringPosition, ringCenter - toCenter * 50f, easedProgress);

            for (int i = 1; i < JointCount; i++) {
                float t = (float)i / (JointCount - 1);
                joints[i] = joints[0] + toCenter * totalLength * t * easedProgress;
            }

            glowIntensity = 1f;

            // 刺击粒子
            if (Main.netMode != NetmodeID.Server && phaseTimer % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                    Vector2 dustPos = joints[JointCount - 1] + Main.rand.NextVector2Circular(8, 8);
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, toCenter.X * 8f, toCenter.Y * 8f, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (phaseTimer >= 12) {
                phase = TailPhase.Dissipate;
                phaseTimer = 0;
                targetExtension = 1f;
            }
        }

        private void UpdateDissipate() {
            // 消散
            float progress = phaseTimer / 20f;

            glowIntensity = 1f - progress;
            Projectile.alpha = (int)(progress * 255);

            // 尾巴向中心收缩并消散
            for (int i = 1; i < JointCount; i++) {
                joints[i] = Vector2.Lerp(joints[i], ringCenter, progress * 0.3f);
            }

            if (phaseTimer >= 20) {
                phase = TailPhase.Done;
            }
        }

        private void SolveFABRIK() {
            for (int i = 1; i < JointCount; i++) {
                Vector2 dir = (joints[i] - joints[i - 1]).SafeNormalize(Vector2.UnitY);
                joints[i] = joints[i - 1] + dir * segmentLengths[i - 1];
            }
        }

        public override bool? CanDamage() {
            return phase == TailPhase.Stab;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 双色粒子爆发
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustPerfect(target.Center, dustType, vel, 100, default, 2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (joints == null) return false;

            Texture2D bodyTex = KyuubiKitsune.MissesBody;
            Texture2D tipTex = KyuubiKitsune.MissesTop;

            if (bodyTex == null)
                bodyTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;
            if (tipTex == null)
                tipTex = TextureAssets.Projectile[ProjectileID.WoodenArrowFriendly].Value;

            SpriteBatch spriteBatch = Main.spriteBatch;

            // 绘制每个体节
            for (int i = 0; i < JointCount - 1; i++) {
                DrawSegment(spriteBatch, bodyTex, i, lightColor);
            }

            // 绘制尾尖
            DrawTip(spriteBatch, tipTex, lightColor);

            return false;
        }

        private void DrawSegment(SpriteBatch spriteBatch, Texture2D texture, int index, Color lightColor) {
            if (index >= JointCount - 1) return;

            Vector2 start = joints[index];
            Vector2 end = joints[index + 1];
            Vector2 direction = end - start;
            float rotation = direction.ToRotation();
            float length = direction.Length();

            float widthScale = MathHelper.Lerp(1f, 0.35f, (float)index / (JointCount - 1));

            // 双色混合
            Color fireColor = new Color(255, 180, 100);
            Color soulColor = new Color(100, 180, 255);
            Color baseColor = Color.Lerp(fireColor, soulColor, colorShift);
            Color drawColor = Color.Lerp(lightColor, baseColor, 0.6f + glowIntensity * 0.3f);
            drawColor *= 1f - Projectile.alpha / 255f;

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
            if (glowIntensity > 0) {
                Color glowColor = baseColor * glowIntensity * 0.5f;
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
            Vector2 tipDir = (lastJoint - prevJoint).SafeNormalize(Vector2.UnitX);
            float tipRotation = tipDir.ToRotation();

            Color fireColor = new Color(255, 200, 100);
            Color soulColor = new Color(150, 200, 255);
            Color tipColor = Color.Lerp(fireColor, soulColor, colorShift);
            tipColor = Color.Lerp(lightColor, tipColor, 0.7f + glowIntensity * 0.3f);
            tipColor *= 1f - Projectile.alpha / 255f;

            float tipScale = 0.6f;

            spriteBatch.Draw(
                texture,
                lastJoint - Main.screenPosition,
                null,
                tipColor,
                tipRotation,
                new Vector2(0, texture.Height * 0.5f),
                tipScale,
                SpriteEffects.None,
                0f
            );

            if (glowIntensity > 0) {
                Color glowColor = Color.Lerp(fireColor, soulColor, colorShift) * glowIntensity * 0.6f;
                glowColor.A = 0;
                spriteBatch.Draw(
                    texture,
                    lastJoint - Main.screenPosition,
                    null,
                    glowColor,
                    tipRotation,
                    new Vector2(0, texture.Height * 0.5f),
                    tipScale * 1.4f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3);
    }

    /// <summary>
    /// 妲己火焰弹 - 爆发时发射的火焰射弹
    /// </summary>
    public class DakkiFireBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 50;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation += 0.2f;

            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, 0, 0, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 0.1f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(255, 150, 50) * progress * 0.5f;
                trailColor.A = 0;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None);
            }

            Color mainColor = new Color(255, 200, 100);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }

    /// <summary>
    /// 妲己魂魄弹 - 爆发时发射的魂魄射弹
    /// </summary>
    public class DakkiSoulBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpectreWrath;

        private float homingStrength = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (homingStrength < 0.06f)
                homingStrength += 0.0015f;

            float maxDetectRange = 350f;
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

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0, 0, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.ShadowOrb);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.ShadowOrb].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(80, 150, 220) * progress * 0.5f;
                trailColor.A = 0;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None);
            }

            Color mainColor = new Color(120, 200, 255);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }
}

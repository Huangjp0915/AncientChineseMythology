using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 冥眼弹幕
    /// 四帧循环眼睛纹理（ArenaEdge.png），支持多种攻击模式：
    /// 0 = 列阵激光：多只眼睛排列成阵，齐射巨大柱状激光
    /// 1 = 环绕冲锋：一圈眼睛环绕后冲向玩家限制走位
    /// 2 = 守卫环绕：环绕Boss旋转，持续释放追踪弹
    /// 3 = 环形激光阵：眼睛围成圆环，向圆心齐射激光形成牢笼
    /// 4 = 十字激光阵：眼睛排列成十字，旋转扫射
    /// 5 = 扫射激光：单只眼睛缓慢旋转激光扫荡
    /// </summary>
    public class ArenaEdge : ModProjectile
    {
        private static Asset<Texture2D> _texture;

        public override string Texture => YinEmperorHelper.Path + "ArenaEdge";

        private const int MaxFrames = 4;
        private const int FrameSpeed = 6;

        // ai[0] = 攻击模式
        private int AttackMode => (int)Projectile.ai[0];
        // ai[1] = 模式参数（阵列索引/环绕角度偏移）
        private ref float ModeParam => ref Projectile.ai[1];

        // 本地状态
        private int frameCounter;
        private int currentFrame;
        private float pulsePhase;
        private float localTimer;
        private bool hasFiredLaser;
        private float orbitAngle;
        private float chargeProgress;
        private float sweepAngle;
        private int sweepLaserIndex = -1;

        // 激光模式参数
        private const int LaserChargeTime = 80;
        private const int LaserFireTime = 60;
        private const int LaserFadeTime = 30;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = MaxFrames;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            // 帧动画
            frameCounter++;
            if (frameCounter >= FrameSpeed) {
                frameCounter = 0;
                currentFrame = (currentFrame + 1) % MaxFrames;
            }
            Projectile.frame = currentFrame;

            pulsePhase += 0.1f;
            localTimer++;

            // 光照
            Lighting.AddLight(Projectile.Center, YinEmperorHelper.ImperialGold.ToVector3() * 0.4f);

            switch (AttackMode) {
                case 0:
                    AI_LaserFormation();
                    break;
                case 1:
                    AI_EncirclingCharge();
                    break;
                case 2:
                    AI_GuardianOrbit();
                    break;
                case 3:
                    AI_RingLaser();
                    break;
                case 4:
                    AI_CrossLaser();
                    break;
                case 5:
                    AI_SweepingLaser();
                    break;
            }
        }

        #region 模式0：列阵激光

        /// <summary>
        /// 列阵激光模式：
        /// 1. 眼睛飞到指定位置排列
        /// 2. 蓄力（瞳孔收缩+粒子汇聚）
        /// 3. 齐射巨大柱状激光
        /// 4. 激光消退后眼睛消散
        /// </summary>
        private void AI_LaserFormation() {
            NPC owner = FindOwner();
            if (owner == null) {
                Projectile.Kill();
                return;
            }

            Player target = FindTarget();
            if (target == null) {
                Projectile.Kill();
                return;
            }

            int totalDuration = LaserChargeTime + LaserFireTime + LaserFadeTime;

            // === 蓄力阶段 ===
            if (localTimer <= LaserChargeTime) {
                // 减速到位
                Projectile.velocity *= 0.92f;

                // 朝向目标（纹理正向朝上，ToRotation()右向为0，需补偿PiOver2）
                float targetAngle = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetAngle, 0.08f);

                chargeProgress = localTimer / (float)LaserChargeTime;

                // 蓄力粒子汇聚到眼前
                if (Main.netMode != NetmodeID.Server && localTimer % 3 == 0) {
                    float radius = 60f * (1f - chargeProgress);
                    Vector2 forward = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
                    Vector2 chargeCenter = Projectile.Center + forward * 20f;

                    for (int i = 0; i < 2; i++) {
                        Vector2 dustPos = chargeCenter + Main.rand.NextVector2Circular(radius, radius);
                        Vector2 dustVel = (chargeCenter - dustPos).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 6f);
                        var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 0.8f + chargeProgress;
                        d.velocity = dustVel;
                    }
                }

                // 蓄力音效
                if (localTimer == LaserChargeTime - 20) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);
                }

                // 不造成接触伤害
                Projectile.damage = 0;
            }
            // === 发射激光 ===
            else if (localTimer == LaserChargeTime + 1 && !hasFiredLaser) {
                hasFiredLaser = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float laserAngle = (target.Center - Projectile.Center).ToRotation(); // 激光方向无需纹理补偿
                    int damage = YinEmperorHelper.GetScaledDamage(110);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorLaser>(),
                        damage, 2f, Main.myPlayer,
                        ai0: laserAngle,
                        ai1: LaserFireTime
                    );
                }

                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.5f, Volume = 1.3f }, Projectile.Center);

                // 后坐力震颤（rotation含纹理补偿，减去PiOver2还原方向）
                Vector2 recoil = -(Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 4f;
                Projectile.velocity = recoil;
            }
            // === 激光持续中 ===
            else if (localTimer <= LaserChargeTime + LaserFireTime) {
                // 保持朝向，微微震颤
                Projectile.velocity *= 0.9f;
                if (Main.netMode != NetmodeID.Server) {
                    Projectile.Center += Main.rand.NextVector2Circular(1.5f, 1.5f);
                }

                // 发射中粒子
                if (Main.rand.NextBool(2)) {
                    Vector2 forward = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
                    Vector2 dustPos = Projectile.Center + forward * 25f + Main.rand.NextVector2Circular(8, 8);
                    var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = forward * 3f + Main.rand.NextVector2Circular(1, 1);
                }
            }
            // === 消散 ===
            else {
                Projectile.alpha += 8;
                Projectile.velocity *= 0.95f;
                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                }
            }

            // 超时保护
            if (localTimer > totalDuration + 30) {
                Projectile.Kill();
            }
        }

        #endregion

        #region 模式1：环绕冲锋

        /// <summary>
        /// 环绕冲锋模式：
        /// 1. 眼睛从远处均匀飞入轨道（入场期）
        /// 2. 稳定环绕收缩，形成可辨识的圆环（演出期）
        /// 3. 短暂停顿蓄力，闪烁警告
        /// 4. 全部冲向玩家中心
        /// </summary>
        private void AI_EncirclingCharge() {
            Player target = FindTarget();
            if (target == null) {
                Projectile.Kill();
                return;
            }

            orbitAngle = ModeParam;
            int approachDuration = 40;
            int orbitDuration = 120;
            int pauseDuration = 30;
            int chargeDuration = 35;
            int totalOrbitEnd = approachDuration + orbitDuration + pauseDuration;

            // === 入场飞向轨道 ===
            if (localTimer <= approachDuration) {
                float t = localTimer / (float)approachDuration;
                float radius = MathHelper.Lerp(500f, 300f, ACMUtils.SineInOut(t));
                orbitAngle += 0.02f;
                ModeParam = orbitAngle;

                Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * radius;
                Projectile.velocity = (orbitPos - Projectile.Center) * 0.12f;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.damage = 0;

                // 逐渐显现
                Projectile.alpha = (int)(255 * (1f - t));
            }
            // === 稳定环绕收缩 ===
            else if (localTimer <= approachDuration + orbitDuration) {
                float progress = (localTimer - approachDuration) / (float)orbitDuration;
                Projectile.alpha = 0;

                float radius = MathHelper.Lerp(300f, 140f, ACMUtils.SineInOut(progress));
                float speed = MathHelper.Lerp(0.035f, 0.06f, progress);
                orbitAngle += speed;
                ModeParam = orbitAngle;

                Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * radius;
                Projectile.velocity = (orbitPos - Projectile.Center) * 0.15f;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.damage = 0;

                // 拖尾光环
                if (Main.netMode != NetmodeID.Server && localTimer % 4 == 0) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1f;
                    d.velocity = -Projectile.velocity * 0.15f;
                }
            }
            // === 蓄力停顿 + 闪烁警告 ===
            else if (localTimer <= totalOrbitEnd) {
                float pauseT = (localTimer - approachDuration - orbitDuration) / (float)pauseDuration;

                // 停在当前位置
                Vector2 holdPos = target.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * 140f;
                Projectile.velocity = (holdPos - Projectile.Center) * 0.2f;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.damage = 0;

                // 闪烁警告
                Projectile.alpha = (int)(MathF.Sin(pauseT * MathHelper.Pi * 6f) * 80f);

                // 蓄力粒子
                if (Main.netMode != NetmodeID.Server && localTimer % 2 == 0) {
                    Vector2 forward = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    var d = Dust.NewDustPerfect(Projectile.Center + forward * 15f, DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.2f + pauseT;
                    d.velocity = forward * (2f + pauseT * 4f);
                }

                if (localTimer == totalOrbitEnd - 10) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.9f }, Projectile.Center);
                }
            }
            // === 冲向玩家 ===
            else if (localTimer == totalOrbitEnd + 1) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = toTarget * 20f;
                Projectile.damage = YinEmperorHelper.GetScaledDamage(95);
                Projectile.alpha = 0;

                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);
                YinEmperorHelper.CreateDragonBurst(Projectile.Center, 30f, 1, 6);
            }
            // === 冲锋中 ===
            else if (localTimer <= totalOrbitEnd + chargeDuration) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                if (Main.netMode != NetmodeID.Server) {
                    YinEmperorHelper.CreateImperialTrail(Projectile.Center, Projectile.velocity, 0.8f);
                }
            }
            // === 消散 ===
            else {
                Projectile.alpha += 12;
                Projectile.velocity *= 0.95f;
                if (Projectile.alpha >= 255)
                    Projectile.Kill();
            }
        }

        #endregion

        #region 模式2：守卫环绕

        /// <summary>
        /// 守卫环绕模式：
        /// 环绕Boss旋转，定期释放追踪弹
        /// </summary>
        private void AI_GuardianOrbit() {
            NPC owner = FindOwner();
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            orbitAngle = ModeParam;
            orbitAngle += 0.035f;
            ModeParam = orbitAngle;

            float radius = 130f + MathF.Sin(pulsePhase * 0.5f) * 15f;
            Vector2 orbitPos = owner.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * radius;
            Projectile.velocity = (orbitPos - Projectile.Center) * 0.12f;

            // 朝外看（纹理正向朝上，orbitAngle指向圆周位置，朝外=orbitAngle方向）
            Projectile.rotation = orbitAngle;

            // 定期发射追踪弹
            if (localTimer % 80 == 40 && Main.netMode != NetmodeID.MultiplayerClient) {
                Player target = FindTarget();
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    int damage = YinEmperorHelper.GetScaledDamage(65);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        toTarget * 10f,
                        ModContent.ProjectileType<YinEmperorBolt>(),
                        damage, 1f, Main.myPlayer
                    );
                }
            }

            // 环绕粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = Main.rand.NextVector2Circular(1, 1);
            }

            // 不造成接触伤害
            Projectile.damage = 0;
        }

        #endregion

        #region 模式3：环形激光阵

        /// <summary>
        /// 环形激光阵：
        /// 眼睛围成圆环，全部朝向圆心蓄力后齐射激光
        /// 形成激光牢笼，玩家必须在圆心附近躲避
        /// ModeParam = 圆环上的角度位置
        /// </summary>
        private void AI_RingLaser() {
            Player target = FindTarget();
            if (target == null) {
                Projectile.Kill();
                return;
            }

            int positionTime = 50;
            int totalDuration = positionTime + LaserChargeTime + LaserFireTime + LaserFadeTime;

            // === 飞向圆环位置 ===
            if (localTimer <= positionTime) {
                float t = localTimer / (float)positionTime;
                float radius = 450f;
                float angle = ModeParam;
                Vector2 ringPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                Projectile.velocity = (ringPos - Projectile.Center) * 0.1f;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.alpha = (int)(255 * (1f - ACMUtils.SineInOut(t)));
                Projectile.damage = 0;
            }
            // === 到位后蓄力 ===
            else if (localTimer <= positionTime + LaserChargeTime) {
                Projectile.alpha = 0;
                Projectile.velocity *= 0.9f;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;

                chargeProgress = (localTimer - positionTime) / (float)LaserChargeTime;

                // 蓄力粒子
                if (Main.netMode != NetmodeID.Server && localTimer % 3 == 0) {
                    float radius = 50f * (1f - chargeProgress);
                    Vector2 forward = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
                    Vector2 chargeCenter = Projectile.Center + forward * 20f;

                    var d = Dust.NewDustPerfect(
                        chargeCenter + Main.rand.NextVector2Circular(radius, radius),
                        DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 0.8f + chargeProgress;
                    d.velocity = (chargeCenter - d.position).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
                }

                if (localTimer == positionTime + LaserChargeTime - 15) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);
                }

                Projectile.damage = 0;
            }
            // === 发射激光 ===
            else if (localTimer == positionTime + LaserChargeTime + 1 && !hasFiredLaser) {
                hasFiredLaser = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float laserAngle = (target.Center - Projectile.Center).ToRotation();
                    int damage = YinEmperorHelper.GetScaledDamage(105);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorLaser>(),
                        damage, 2f, Main.myPlayer,
                        ai0: laserAngle, ai1: LaserFireTime
                    );
                }

                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
                Vector2 recoil = -(Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 3f;
                Projectile.velocity = recoil;
            }
            // === 激光持续 ===
            else if (localTimer <= positionTime + LaserChargeTime + LaserFireTime) {
                Projectile.velocity *= 0.9f;
                if (Main.netMode != NetmodeID.Server) {
                    Projectile.Center += Main.rand.NextVector2Circular(1f, 1f);
                }
            }
            // === 消散 ===
            else {
                Projectile.alpha += 10;
                Projectile.velocity *= 0.95f;
                if (Projectile.alpha >= 255)
                    Projectile.Kill();
            }

            if (localTimer > totalDuration + 30)
                Projectile.Kill();
        }

        #endregion

        #region 模式4：十字激光阵

        /// <summary>
        /// 十字激光阵：
        /// 眼睛排列成十字形（上下左右），蓄力后齐射激光
        /// 十字缓慢旋转，扫射大范围区域
        /// ModeParam = 十字上的位置索引（由Boss编排）
        /// </summary>
        private void AI_CrossLaser() {
            NPC owner = FindOwner();
            if (owner == null) {
                Projectile.Kill();
                return;
            }

            Player target = FindTarget();
            if (target == null) {
                Projectile.Kill();
                return;
            }

            int positionTime = 60;
            int chargeTime = 70;
            int fireTime = 90; // 较长的发射时间用于旋转扫射
            int fadeTime = 20;
            int totalDuration = positionTime + chargeTime + fireTime + fadeTime;

            // 十字中心为Boss位置
            Vector2 crossCenter = owner.Center;

            // === 飞向十字位置 ===
            if (localTimer <= positionTime) {
                Projectile.velocity *= 0.9f;
                Projectile.rotation = (crossCenter - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.alpha = (int)(255 * (1f - localTimer / (float)positionTime));
                Projectile.damage = 0;
            }
            // === 蓄力 ===
            else if (localTimer <= positionTime + chargeTime) {
                Projectile.alpha = 0;
                Projectile.velocity *= 0.85f;

                // 朝向远离Boss中心的方向
                Vector2 outward = (Projectile.Center - crossCenter).SafeNormalize(Vector2.UnitY);
                Projectile.rotation = outward.ToRotation() + MathHelper.PiOver2;

                chargeProgress = (localTimer - positionTime) / (float)chargeTime;

                if (Main.netMode != NetmodeID.Server && localTimer % 3 == 0) {
                    Vector2 forward = outward;
                    Vector2 chargePos = Projectile.Center + forward * 20f;
                    float r = 40f * (1f - chargeProgress);
                    var d = Dust.NewDustPerfect(
                        chargePos + Main.rand.NextVector2Circular(r, r), DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 0.8f + chargeProgress;
                    d.velocity = (chargePos - d.position).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
                }

                if (localTimer == positionTime + chargeTime - 15) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                }

                Projectile.damage = 0;
            }
            // === 发射 + 缓慢旋转 ===
            else if (localTimer == positionTime + chargeTime + 1 && !hasFiredLaser) {
                hasFiredLaser = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 outward = (Projectile.Center - crossCenter).SafeNormalize(Vector2.UnitY);
                    float laserAngle = outward.ToRotation();
                    int damage = YinEmperorHelper.GetScaledDamage(100);
                    sweepLaserIndex = Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorLaser>(),
                        damage, 2f, Main.myPlayer,
                        ai0: laserAngle, ai1: fireTime
                    );
                }

                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.4f, Volume = 1.2f }, Projectile.Center);
                sweepAngle = (Projectile.Center - crossCenter).ToRotation();
            }
            else if (localTimer > positionTime + chargeTime + 1 && localTimer <= positionTime + chargeTime + fireTime) {
                // 缓慢绕Boss中心旋转（整个十字一起转）
                sweepAngle += 0.008f;
                float dist = Vector2.Distance(Projectile.Center, crossCenter);
                Vector2 newPos = crossCenter + sweepAngle.ToRotationVector2() * dist;
                Projectile.velocity = (newPos - Projectile.Center) * 0.15f;

                Vector2 outward = (Projectile.Center - crossCenter).SafeNormalize(Vector2.UnitY);
                Projectile.rotation = outward.ToRotation() + MathHelper.PiOver2;

                // 同步激光方向
                if (sweepLaserIndex >= 0 && sweepLaserIndex < Main.maxProjectiles) {
                    var laser = Main.projectile[sweepLaserIndex];
                    if (laser.active && laser.type == ModContent.ProjectileType<YinEmperorLaser>()) {
                        laser.Center = Projectile.Center;
                        laser.ai[0] = outward.ToRotation();
                    }
                }

                // 震颤
                if (Main.netMode != NetmodeID.Server) {
                    Projectile.Center += Main.rand.NextVector2Circular(1f, 1f);
                }
            }
            // === 消散 ===
            else {
                Projectile.alpha += 12;
                Projectile.velocity *= 0.95f;
                if (Projectile.alpha >= 255)
                    Projectile.Kill();
            }

            if (localTimer > totalDuration + 30)
                Projectile.Kill();
        }

        #endregion

        #region 模式5：扫射激光

        /// <summary>
        /// 扫射激光：
        /// 单只眼睛飞到指定位置，释放激光并缓慢旋转扫射
        /// ModeParam = 初始扫射角度
        /// </summary>
        private void AI_SweepingLaser() {
            NPC owner = FindOwner();
            if (owner == null) {
                Projectile.Kill();
                return;
            }

            int positionTime = 50;
            int chargeTime = 60;
            int fireTime = 120; // 长时间扫射
            int fadeTime = 20;
            int totalDuration = positionTime + chargeTime + fireTime + fadeTime;

            // === 飞向位置 ===
            if (localTimer <= positionTime) {
                Projectile.velocity *= 0.92f;
                sweepAngle = ModeParam;
                Projectile.rotation = sweepAngle + MathHelper.PiOver2;
                Projectile.alpha = (int)(255 * (1f - localTimer / (float)positionTime));
                Projectile.damage = 0;
            }
            // === 蓄力 ===
            else if (localTimer <= positionTime + chargeTime) {
                Projectile.alpha = 0;
                Projectile.velocity *= 0.85f;
                Projectile.rotation = sweepAngle + MathHelper.PiOver2;

                chargeProgress = (localTimer - positionTime) / (float)chargeTime;

                if (Main.netMode != NetmodeID.Server && localTimer % 3 == 0) {
                    Vector2 forward = sweepAngle.ToRotationVector2();
                    Vector2 chargePos = Projectile.Center + forward * 20f;
                    float r = 40f * (1f - chargeProgress);
                    var d = Dust.NewDustPerfect(
                        chargePos + Main.rand.NextVector2Circular(r, r), DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 0.8f + chargeProgress;
                    d.velocity = (chargePos - d.position).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
                }

                if (localTimer == positionTime + chargeTime - 15) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);
                }

                Projectile.damage = 0;
            }
            // === 发射 + 缓慢旋转扫射 ===
            else if (localTimer == positionTime + chargeTime + 1 && !hasFiredLaser) {
                hasFiredLaser = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int damage = YinEmperorHelper.GetScaledDamage(100);
                    sweepLaserIndex = Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorLaser>(),
                        damage, 2f, Main.myPlayer,
                        ai0: sweepAngle, ai1: fireTime
                    );
                }

                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
            }
            else if (localTimer > positionTime + chargeTime + 1 && localTimer <= positionTime + chargeTime + fireTime) {
                // 缓慢旋转扫射（约扫过90度）
                sweepAngle += 0.013f;
                Projectile.rotation = sweepAngle + MathHelper.PiOver2;
                Projectile.velocity *= 0.9f;

                // 同步激光方向
                if (sweepLaserIndex >= 0 && sweepLaserIndex < Main.maxProjectiles) {
                    var laser = Main.projectile[sweepLaserIndex];
                    if (laser.active && laser.type == ModContent.ProjectileType<YinEmperorLaser>()) {
                        laser.Center = Projectile.Center;
                        laser.ai[0] = sweepAngle;
                    }
                }

                // 震颤
                if (Main.netMode != NetmodeID.Server) {
                    Projectile.Center += Main.rand.NextVector2Circular(1.2f, 1.2f);
                }

                // 扫射中粒子
                if (Main.rand.NextBool(2)) {
                    Vector2 forward = sweepAngle.ToRotationVector2();
                    var d = Dust.NewDustPerfect(
                        Projectile.Center + forward * 25f + Main.rand.NextVector2Circular(6, 6),
                        DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = forward * 3f;
                }
            }
            // === 消散 ===
            else {
                Projectile.alpha += 12;
                Projectile.velocity *= 0.95f;
                if (Projectile.alpha >= 255)
                    Projectile.Kill();
            }

            if (localTimer > totalDuration + 30)
                Projectile.Kill();
        }

        #endregion

        #region 工具方法

        private NPC FindOwner() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<YinEmperor>() && npc.active)
                    return npc;
            }
            return null;
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 2000f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            int frameHeight = tex.Height / MaxFrames;
            Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);

            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = YinEmperorHelper.AbyssPurple * progress * 0.4f;
                trailColor.A = 0;
                float trailScale = Projectile.scale * (0.5f + progress * 0.5f);
                sb.Draw(tex, pos, sourceRect, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // 外发光
            Color glowColor = YinEmperorHelper.ImperialGold;
            glowColor.A = 0;

            // 蓄力时发光增强
            float glowMod = AttackMode == 0 ? (1f + chargeProgress * 0.8f) : 1f;

            for (int i = 2; i >= 0; i--) {
                float glowScale = Projectile.scale * (1.2f + i * 0.12f) * pulse * glowMod;
                sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                    glowColor * (0.15f / (i + 1)) * ((255 - Projectile.alpha) / 255f),
                    Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            // 主体
            Color mainColor = Color.Lerp(lightColor, YinEmperorHelper.ImperialGold, 0.3f);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                mainColor * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        #endregion
    }
}

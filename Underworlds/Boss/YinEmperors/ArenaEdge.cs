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
        /// 1. 一圈眼睛以玩家为中心快速环绕
        /// 2. 逐渐收缩半径，限制走位
        /// 3. 最终全部冲向玩家中心
        /// </summary>
        private void AI_EncirclingCharge() {
            Player target = FindTarget();
            if (target == null) {
                Projectile.Kill();
                return;
            }

            orbitAngle = ModeParam;
            float orbitSpeed = 0.04f;
            int orbitDuration = 180;
            int chargeDuration = 40;

            // === 环绕阶段 ===
            if (localTimer <= orbitDuration) {
                float progress = localTimer / (float)orbitDuration;

                // 逐渐收缩半径
                float radius = MathHelper.Lerp(350f, 120f, ACMUtils.SineInOut(progress));

                // 加速旋转
                orbitSpeed = MathHelper.Lerp(0.03f, 0.08f, progress);
                orbitAngle += orbitSpeed;
                ModeParam = orbitAngle;

                // 围绕目标旋转
                Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * radius;
                Projectile.velocity = (orbitPos - Projectile.Center) * 0.15f;

                // 朝向中心（纹理正向朝上）
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;

                // 拖尾粒子
                if (Main.netMode != NetmodeID.Server && localTimer % 3 == 0) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = -Projectile.velocity * 0.2f;
                }

                // 收缩警告音效
                if (localTimer == orbitDuration - 30) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.8f }, Projectile.Center);
                }

                // 不在环绕时造成伤害（避免太难躲）
                Projectile.damage = 0;
            }
            // === 冲向玩家 ===
            else if (localTimer == orbitDuration + 1) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = toTarget * 22f;
                Projectile.damage = YinEmperorHelper.GetScaledDamage(95);

                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);

                // 冲锋能量波粒子
                YinEmperorHelper.CreateDragonBurst(Projectile.Center, 30f, 1, 6);
            }
            // === 冲锋中 ===
            else if (localTimer <= orbitDuration + chargeDuration) {
                // 轻微追踪
                if (localTimer < orbitDuration + 20) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 22f, 0.02f);
                }

                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                // 冲锋拖尾
                if (Main.netMode != NetmodeID.Server) {
                    YinEmperorHelper.CreateImperialTrail(Projectile.Center, Projectile.velocity, 0.8f);
                }
            }
            // === 冲过后消散 ===
            else {
                Projectile.alpha += 10;
                Projectile.velocity *= 0.96f;
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

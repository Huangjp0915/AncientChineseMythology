using System;
using System.IO;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡左键手持弹幕 —— 祭幡法器动作（非挥剑）：
    /// 1. 举幡（Raise）：从身侧将幡旗缓缓提起
    /// 2. 祭幡（Thrust）：向前方猛力直刺，幡旗直插前方
    /// 3. 引魂（Channel）：幡旗驻留原地，释放吸魂漩涡
    /// 4. 收魂（Retract）：收回幡旗，积蓄灵魂凝聚爆发
    /// 核心区别：直线刺出+驻留引魂，不是弧形横扫
    /// </summary>
    public class SoulBannerHeldProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        // ── 动画参数 ──
        private const float MaxExtend = 90f;       // 最大伸出距离
        private const float StartOffset = -30f;    // 起始偏移（幡旗从身后开始）
        private const float BaseAbsorbRadius = 260f; // 基础引魂漩涡半径

        // 各阶段基准帧数（受攻速影响）
        private const float BaseRaise = 10f;
        private const float BaseThrust = 7f;
        private const float BaseChannel = 20f;
        private const float BaseRetract = 8f;

        private enum BannerPhase { Raise, Thrust, Channel, Retract }

        private Player Owner => Main.player[Projectile.owner];

        // ai slots
        private ref float AimAngle => ref Projectile.ai[0];
        private ref float GlobalTimer => ref Projectile.ai[1];

        // localAI slots
        private BannerPhase CurrentPhase
        {
            get => (BannerPhase)(int)Projectile.localAI[0];
            set { Projectile.localAI[0] = (int)value; phaseTimer = 0; }
        }

        // 运行时状态（客户端）
        private float phaseTimer;
        private float currentExtend;
        private float bannerScale;
        private bool hasBurstPlayed;

        // 残影系统
        private const int AfterimageLength = 8;
        private Vector2[] afterimagePositions = new Vector2[AfterimageLength];
        private float[] afterimageRotations = new float[AfterimageLength];

        // 受攻速影响的阶段时长
        private float RaiseTime => BaseRaise / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => BaseThrust / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => BaseRetract / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        // 成长系统缓存（每次释放时读取一次）
        private float growthAbsorbRadius;
        private float growthChannelMul;
        private float growthHealMul;
        private float growthRatio; // 0~1, 驱动表现层亮度/吸魂弧/灵魂脉冲
        private float ChannelTime => BaseChannel * growthChannelMul / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float AbsorbRadius => growthAbsorbRadius;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 84;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = 10000;
        }

        public override void OnSpawn(IEntitySource source)
        {
            AimAngle = Projectile.velocity.ToRotation();
            Projectile.velocity = Vector2.Zero;
            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0 ? 1 : -1;
            bannerScale = 0f;
            currentExtend = StartOffset;

            // 缓存成长系统数值
            var sbPlayer = Owner.GetModPlayer<SoulBannerPlayer>();
            growthAbsorbRadius = BaseAbsorbRadius * sbPlayer.AbsorbRadiusMultiplier;
            growthChannelMul = sbPlayer.ChannelTimeMultiplier;
            growthHealMul = sbPlayer.HealMultiplier;
            growthRatio = sbPlayer.GrowthRatio;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)Projectile.spriteDirection);
            writer.Write(AimAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.spriteDirection = reader.ReadSByte();
            AimAngle = reader.ReadSingle();
        }

        public override void AI()
        {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed)
            {
                Projectile.Kill();
                return;
            }

            Owner.direction = Projectile.spriteDirection;
            GlobalTimer++;
            phaseTimer++;

            switch (CurrentPhase)
            {
                case BannerPhase.Raise: RaisePhase(); break;
                case BannerPhase.Thrust: ThrustPhase(); break;
                case BannerPhase.Channel: ChannelPhase(); break;
                case BannerPhase.Retract: RetractPhase(); break;
            }

            PositionBanner();
        }

        // ── 举幡：从身后缓缓提起幡旗 ──
        private void RaisePhase()
        {
            float t = Math.Clamp(phaseTimer / RaiseTime, 0f, 1f);

            // 幡旗从身后浮现，逐渐显形
            bannerScale = ACMUtils.QuadOut(t);
            currentExtend = MathHelper.Lerp(StartOffset, 15f, ACMUtils.SineInOut(t));

            // 上升的幽灵粒子：多层次
            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(24f, 35f);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                    Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3f), 150, default, 0.5f + 0.4f * t);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            // 暗影火焰升腾（阴气汇聚感）
            if (t > 0.3f && Main.rand.NextBool(3))
            {
                Vector2 flamePos = Owner.Center + new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(0f, 10f));
                Dust flame = Dust.NewDustDirect(flamePos, 1, 1, DustID.Shadowflame,
                    0f, -Main.rand.NextFloat(1f, 2.5f), 100, default, 0.7f * t);
                flame.noGravity = true;
            }

            // 脚下阴气烟雾
            if (Main.rand.NextBool(4))
            {
                Vector2 smokePos = Owner.Bottom + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-5f, 5f));
                Dust smoke = Dust.NewDustDirect(smokePos, 1, 1, DustID.Smoke,
                    Main.rand.NextFloat(-0.3f, 0.3f), -0.3f, 200, new Color(80, 30, 120), 0.8f);
                smoke.noGravity = true;
            }

            if (phaseTimer == 3)
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = -0.3f }, Owner.Center);

            if (phaseTimer >= RaiseTime)
            {
                // 初始化残影位置
                for (int i = 0; i < AfterimageLength; i++)
                {
                    afterimagePositions[i] = Projectile.Center;
                    afterimageRotations[i] = Projectile.rotation;
                }
                CurrentPhase = BannerPhase.Thrust;
            }
        }

        // ── 祭幡：猛力向前直刺（不是弧扫！） ──
        private void ThrustPhase()
        {
            float t = Math.Clamp(phaseTimer / ThrustTime, 0f, 1f);

            bannerScale = 1f;
            // QuadOut：快速刺出，到达终点时减速 —— 类似戮枪的爽快感
            currentExtend = MathHelper.Lerp(15f, MaxExtend, ACMUtils.QuadOut(t));

            // 更新残影队列
            UpdateAfterimages();

            if (phaseTimer == 1)
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);

            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 tipPos = Owner.MountedCenter + dir * currentExtend;
            Vector2 perp = new(-dir.Y, dir.X);

            // 双螺旋尾迹粒子
            if (t > 0.15f)
            {
                float spiralAngle = phaseTimer * 1.2f;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? 1f : -1f;
                    float spiralR = 8f + 10f * t;
                    Vector2 offset = perp * (MathF.Sin(spiralAngle + s * MathF.PI) * spiralR);
                    Vector2 spiralPos = tipPos + offset - dir * Main.rand.NextFloat(5f, 20f);
                    Dust spiral = Dust.NewDustDirect(spiralPos, 1, 1, DustID.PurpleTorch,
                        -dir.X * 2f + perp.X * side * 0.5f, -dir.Y * 2f + perp.Y * side * 0.5f,
                        80, default, 0.8f + 0.5f * t);
                    spiral.noGravity = true;
                }
            }

            // 沿路径的暗影火焰拖尾
            if (t > 0.2f && Main.rand.NextBool(2))
            {
                float trailDist = currentExtend * Main.rand.NextFloat(0.3f, 0.95f);
                Vector2 trailPos = Owner.MountedCenter + dir * trailDist + perp * Main.rand.NextFloat(-8f, 8f);
                Dust flame = Dust.NewDustDirect(trailPos, 1, 1, DustID.Shadowflame,
                    -dir.X * 1.5f, -dir.Y * 1.5f, 120, default, 0.6f + 0.4f * t);
                flame.noGravity = true;
            }

            // 幡尖前方散射粒子
            if (t > 0.4f)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 vel = dir * Main.rand.NextFloat(5f, 9f) + Main.rand.NextVector2Circular(2.5f, 2.5f);
                    Dust dust = Dust.NewDustDirect(tipPos, 1, 1, DustID.DungeonSpirit,
                        vel.X, vel.Y, 60, default, 1.0f + 0.4f * t);
                    dust.noGravity = true;
                    dust.fadeIn = 1.4f;
                }
            }

            // 到达终点的冲击波
            if (phaseTimer >= ThrustTime - 1)
                ThrustImpact();

            if (phaseTimer >= ThrustTime)
                CurrentPhase = BannerPhase.Channel;
        }

        /// <summary>更新残影位置队列</summary>
        private void UpdateAfterimages()
        {
            for (int i = AfterimageLength - 1; i > 0; i--)
            {
                afterimagePositions[i] = afterimagePositions[i - 1];
                afterimageRotations[i] = afterimageRotations[i - 1];
            }
            afterimagePositions[0] = Projectile.Center;
            afterimageRotations[0] = Projectile.rotation;
        }

        // ── 引魂：幡旗驻留原地，释放吸魂漩涡 ──
        // 这是万魂幡独有的核心阶段——没有任何剑会"停在前方持续吸魂"
        private void ChannelPhase()
        {
            float t = Math.Clamp(phaseTimer / ChannelTime, 0f, 1f);

            bannerScale = 1f;
            // 驻留时轻微前后呼吸感（幡旗受灵力涌动而微微晃动）
            float breathe = MathF.Sin(phaseTimer * 0.35f) * 4f;
            currentExtend = MaxExtend + breathe;

            // 吸魂漩涡粒子系统
            SpawnSoulVortex(t);

            // 引魂低沉音效
            if (phaseTimer == 4)
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);

            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 tipPos = Owner.MountedCenter + dir * currentExtend;
            Vector2 perp = new(-dir.Y, dir.X);

            // 幡旗末端飘荡粒子（强化版）
            for (int i = 0; i < 2; i++)
            {
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(
                        tipPos + perp * Main.rand.NextFloat(-25f, 25f),
                        1, 1, DustID.DungeonSpirit,
                        perp.X * Main.rand.NextFloat(-1.5f, 1.5f),
                        -Main.rand.NextFloat(0.8f, 2.5f),
                        100, default, 0.9f + 0.3f * t);
                    dust.noGravity = true;
                    dust.fadeIn = 1.4f;
                }
            }

            // 暗影火焰漂浮（鬼火感）
            if (Main.rand.NextBool(3))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(25f, 60f);
                Vector2 firePos = tipPos + angle.ToRotationVector2() * radius;
                Dust fire = Dust.NewDustDirect(firePos, 1, 1, DustID.Shadowflame,
                    0f, -Main.rand.NextFloat(0.5f, 1.5f), 150, default, 0.5f + 0.3f * t);
                fire.noGravity = true;
            }

            // 地面阴气弥漫
            if (Main.rand.NextBool(4))
            {
                Vector2 groundPos = new(tipPos.X + Main.rand.NextFloat(-80f, 80f), tipPos.Y + 40f);
                Dust mist = Dust.NewDustDirect(groundPos, 1, 1, DustID.Smoke,
                    Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.3f, 0.8f),
                    200, new Color(60, 20, 100), 1.2f);
                mist.noGravity = true;
            }

            // 间歇性灵魂脉冲波纹
            if ((int)phaseTimer % 15 == 0)
            {
                for (int i = 0; i < 12; i++)
                {
                    float pulseAngle = MathHelper.TwoPi * i / 12f;
                    float pulseR = 30f + 20f * t;
                    Vector2 pulsePos = tipPos + pulseAngle.ToRotationVector2() * pulseR;
                    Vector2 outwardVel = pulseAngle.ToRotationVector2() * 3f;
                    Dust pulse = Dust.NewDustPerfect(pulsePos, DustID.PurpleTorch, outwardVel, 100, default, 0.5f);
                    pulse.noGravity = true;
                }
            }

            if (phaseTimer >= ChannelTime)
                CurrentPhase = BannerPhase.Retract;
        }

        // ── 收魂：收回幡旗，灵魂凝聚爆发 ──
        private void RetractPhase()
        {
            float t = Math.Clamp(phaseTimer / RetractTime, 0f, 1f);

            bannerScale = 1f - ACMUtils.QuadIn(t) * 0.3f;
            // QuadIn：缓慢开始收回，末尾猛地抽回
            currentExtend = MathHelper.Lerp(MaxExtend, 0f, ACMUtils.QuadIn(t));

            // 收回起始瞬间的灵魂爆发
            if (!hasBurstPlayed)
            {
                hasBurstPlayed = true;
                SoulBurst();
            }

            if (phaseTimer >= RetractTime)
                Projectile.Kill();
        }

        /// <summary>
        /// 定位幡旗 —— 沿固定瞄准方向的直线延伸，不做弧形旋转
        /// 参考 HalberdThrust 的直刺定位方式
        /// </summary>
        private void PositionBanner()
        {
            Vector2 aimDir = AimAngle.ToRotationVector2();
            float armAngle = AimAngle - MathHelper.PiOver2;

            // 举幡阶段：手臂从偏下方抬到瞄准方向
            if (CurrentPhase == BannerPhase.Raise)
            {
                float raiseT = Math.Clamp(phaseTimer / RaiseTime, 0f, 1f);
                float startOffset = MathHelper.ToRadians(40f) * Projectile.spriteDirection;
                armAngle = MathHelper.Lerp(armAngle + startOffset, armAngle, ACMUtils.SineInOut(raiseT));
            }

            // 引魂阶段：手臂轻微颤抖（灵力涌动）
            if (CurrentPhase == BannerPhase.Channel)
                armAngle += MathF.Sin(phaseTimer * 0.5f) * 0.025f;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            Vector2 handPos = Owner.GetFrontHandPosition(
                Player.CompositeArmStretchAmount.Full, armAngle);
            handPos.Y += Owner.gfxOffY;

            // 幡旗沿瞄准方向直线延伸（不是绕中心旋转！）
            Projectile.Center = handPos + aimDir * Math.Max(currentExtend, 0f);

            // 幡旗朝向 = 固定瞄准方向 + 引魂时的轻微飘动
            float flutter = 0f;
            if (CurrentPhase == BannerPhase.Channel)
                flutter = MathF.Sin(phaseTimer * 0.4f) * 0.06f;
            Projectile.rotation = AimAngle + flutter;

            Projectile.scale = bannerScale * 1.1f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;

            // 光照
            float lightMul = CurrentPhase == BannerPhase.Channel ? 1.5f : 0.5f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.12f, 0.55f) * lightMul);
        }

        /// <summary>
        /// 刺出到达终点时的冲击波效果
        /// </summary>
        private void ThrustImpact()
        {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 impactPos = Owner.MountedCenter + dir * MaxExtend;

            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = 0.3f }, impactPos);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.3f, Pitch = -0.6f }, impactPos);

            // 屏幕震动
            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(3.5f, 8);

            // 外圈环形冲击波（大粒子）
            for (int i = 0; i < 18; i++)
            {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                Dust dust = Dust.NewDustDirect(impactPos, 1, 1, DustID.PurpleTorch,
                    vel.X, vel.Y, 60, default, 1.5f + Main.rand.NextFloat(0.3f));
                dust.noGravity = true;
                dust.fadeIn = 1.8f;
            }

            // 内圈暗影冲击
            for (int i = 0; i < 10; i++)
            {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
                Dust shadow = Dust.NewDustDirect(impactPos, 1, 1, DustID.Shadowflame,
                    vel.X, vel.Y, 100, default, 1.2f);
                shadow.noGravity = true;
            }

            // 前向幽灵冲击粒子（强化）
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = dir * Main.rand.NextFloat(6f, 14f) + Main.rand.NextVector2Circular(3f, 3f);
                Dust ghost = Dust.NewDustDirect(impactPos, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 40, default, 1.6f);
                ghost.noGravity = true;
                ghost.fadeIn = 2.0f;
            }

            // 沿前方的扇形冲击线
            for (int i = 0; i < 8; i++)
            {
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-35f, 35f));
                Vector2 vel = (AimAngle + spread).ToRotationVector2() * Main.rand.NextFloat(8f, 16f);
                Dust beam = Dust.NewDustDirect(impactPos, 1, 1, DustID.ShadowbeamStaff,
                    vel.X, vel.Y, 80, default, 1.0f);
                beam.noGravity = true;
            }

            // 向后反向飘散的残魂碎片
            for (int i = 0; i < 5; i++)
            {
                Vector2 vel = -dir * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(3f, 3f);
                Dust wisp = Dust.NewDustDirect(impactPos, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 120, default, 0.8f);
                wisp.noGravity = true;
            }
        }

        /// <summary>
        /// 引魂漩涡 —— 万魂幡的核心视觉效果
        /// 灵魂从周围敌人身上被抽离，螺旋飞向幡旗末端的漩涡中心
        /// </summary>
        private void SpawnSoulVortex(float channelProgress)
        {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 vortexCenter = Owner.MountedCenter + dir * currentExtend;
            float expandedRadius = AbsorbRadius * ACMUtils.QuadOut(Math.Min(channelProgress * 3f, 1f));

            // ── 从敌人身上抽取灵魂粒子（强化：多层次抽魂） ──
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this)) continue;

                float dist = Vector2.Distance(npc.Center, vortexCenter);
                if (dist > expandedRadius) continue;

                // 主灵魂流：大型螺旋灵魂粒子
                if (Main.rand.NextBool(2))
                {
                    Vector2 soulPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                    Vector2 toVortex = (vortexCenter - soulPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-toVortex.Y, toVortex.X);
                    Vector2 soulVel = toVortex * Main.rand.NextFloat(7f, 13f)
                        + tangent * Main.rand.NextFloat(-4f, 4f);

                    Dust dust = Dust.NewDustDirect(soulPos, 1, 1, DustID.DungeonSpirit,
                        soulVel.X, soulVel.Y, 40, default, 1.4f + 0.3f * channelProgress);
                    dust.noGravity = true;
                    dust.fadeIn = 2.0f;
                }

                // 暗影灵魂流：暗色伴随粒子
                if (Main.rand.NextBool(3))
                {
                    Vector2 darkPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.3f, npc.height * 0.3f);
                    Vector2 toV = (vortexCenter - darkPos).SafeNormalize(Vector2.Zero);
                    Vector2 darkVel = toV * Main.rand.NextFloat(5f, 9f);

                    Dust dark = Dust.NewDustDirect(darkPos, 1, 1, DustID.Shadowflame,
                        darkVel.X, darkVel.Y, 80, default, 0.9f);
                    dark.noGravity = true;
                }

                // 微光碎片：宝石紫色点缀
                if (Main.rand.NextBool(5))
                {
                    Vector2 sparkPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.6f, npc.height * 0.6f);
                    Vector2 toV = (vortexCenter - sparkPos).SafeNormalize(Vector2.Zero);
                    Dust spark = Dust.NewDustDirect(sparkPos, 1, 1, DustID.GemAmethyst,
                        toV.X * 8f, toV.Y * 8f, 0, default, 0.6f);
                    spark.noGravity = true;
                }
            }

            // ── 漩涡中心旋转粒子（双层螺旋） ──
            int vortexCount = (int)(6 + 8 * ACMUtils.QuadOut(Math.Min(channelProgress * 2f, 1f)));
            for (int j = 0; j < vortexCount; j++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(10f, 45f);
                Vector2 pos = vortexCenter + angle.ToRotationVector2() * radius;
                Vector2 toC = (vortexCenter - pos).SafeNormalize(Vector2.Zero);
                Vector2 tang = new(-toC.Y, toC.X);
                Vector2 vel = tang * (3.5f + 1.5f * channelProgress) + toC * 2f;

                int dustType = j % 3 == 0 ? DustID.Shadowflame : DustID.PurpleTorch;
                Dust dust = Dust.NewDustDirect(pos, 1, 1, dustType,
                    vel.X, vel.Y, 80, default, 0.6f + 0.4f * channelProgress);
                dust.noGravity = true;
            }

            // ── 内核脉动粒子（漩涡核心的高亮点） ──
            float coreIntensity = 0.5f + 0.5f * MathF.Sin(phaseTimer * 0.4f);
            for (int k = 0; k < (int)(3 * coreIntensity + 1); k++)
            {
                Vector2 corePos = vortexCenter + Main.rand.NextVector2Circular(8f, 8f);
                Dust core = Dust.NewDustDirect(corePos, 1, 1, DustID.PurpleTorch,
                    0f, 0f, 40, default, 1.0f + 0.5f * coreIntensity);
                core.noGravity = true;
                core.velocity *= 0.3f;
            }

            // ── 外圈引气粒子环（双环） ──
            if (channelProgress > 0.15f)
            {
                // 外环
                for (int r = 0; r < 2; r++)
                {
                    if (!Main.rand.NextBool(2)) continue;
                    float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float ringR = expandedRadius * (r == 0 ? Main.rand.NextFloat(0.7f, 1f) : Main.rand.NextFloat(0.4f, 0.6f));
                    Vector2 ringPos = vortexCenter + ringAngle.ToRotationVector2() * ringR;
                    Vector2 inward = (vortexCenter - ringPos).SafeNormalize(Vector2.Zero);
                    Vector2 tangent = new(-inward.Y, inward.X);
                    Vector2 ringVel = inward * 3f + tangent * 2f;

                    int ringDust = r == 0 ? DustID.PurpleTorch : DustID.DungeonSpirit;
                    Dust dust = Dust.NewDustPerfect(ringPos, ringDust, ringVel, 80, default, 0.5f + 0.2f * r);
                    dust.noGravity = true;
                }
            }

            // ── 八方符阵射线（仪式感） ──
            if (channelProgress > 0.3f && (int)phaseTimer % 3 == 0)
            {
                int directions = 8;
                float baseAngle = phaseTimer * 0.06f;
                for (int d = 0; d < directions; d++)
                {
                    float rayAngle = baseAngle + MathHelper.TwoPi * d / directions;
                    float rayDist = expandedRadius * (0.2f + 0.6f * channelProgress);
                    Vector2 rayPos = vortexCenter + rayAngle.ToRotationVector2() * rayDist;
                    Vector2 toCenter = (vortexCenter - rayPos).SafeNormalize(Vector2.Zero) * 1.5f;
                    Dust ray = Dust.NewDustPerfect(rayPos, DustID.ShadowbeamStaff, toCenter, 100, default, 0.4f);
                    ray.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 收魂时灵魂凝聚爆发
        /// </summary>
        private void SoulBurst()
        {
            Vector2 burstCenter = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = 0.5f }, burstCenter);
            SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 0.4f, Pitch = -0.3f }, burstCenter);

            // 屏幕震动
            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(4f, 10);

            // 第一波：向外爆发的幽灵（大型）
            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);
                Dust dust = Dust.NewDustDirect(burstCenter, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 30, default, 1.6f + Main.rand.NextFloat(0.4f));
                dust.noGravity = true;
                dust.fadeIn = 2.2f;
            }

            // 第二波：暗影火焰环
            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi * i / 16f + 0.1f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust flame = Dust.NewDustDirect(burstCenter, 1, 1, DustID.Shadowflame,
                    vel.X, vel.Y, 60, default, 1.3f);
                flame.noGravity = true;
            }

            // 第三波：紫色能量内爆
            for (int i = 0; i < 14; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                Dust energy = Dust.NewDustDirect(burstCenter, 1, 1, DustID.PurpleTorch,
                    vel.X, vel.Y, 60, default, 1.8f);
                energy.noGravity = true;
                energy.fadeIn = 1.5f;
            }

            // 宝石碎光散射
            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust gem = Dust.NewDustDirect(burstCenter, 1, 1, DustID.GemAmethyst,
                    vel.X, vel.Y, 0, default, 0.8f);
                gem.noGravity = true;
            }

            // 向上升腾的残魂
            for (int i = 0; i < 6; i++)
            {
                Vector2 pos = burstCenter + Main.rand.NextVector2Circular(20f, 10f);
                Dust rising = Dust.NewDustDirect(pos, 1, 1, DustID.DungeonSpirit,
                    Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 6f), 80, default, 1.2f);
                rising.noGravity = true;
                rising.fadeIn = 1.6f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 微震反馈
            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(1.5f, 4);

            // 万魂幡幽紫命中演出 (径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Soul, scale: 0.9f, owner: Projectile.owner);

            // 灵魂从敌人身上飞向幡旗（多层次）
            for (int i = 0; i < 10; i++)
            {
                Vector2 toOwner = (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero);
                Vector2 tangent = new(-toOwner.Y, toOwner.X);
                Vector2 vel = toOwner * Main.rand.NextFloat(5f, 11f)
                    + tangent * Main.rand.NextFloat(-2.5f, 2.5f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustDirect(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 50, default, 1.4f);
                dust.noGravity = true;
                dust.fadeIn = 1.8f;
            }

            // 暗影碎片
            for (int i = 0; i < 4; i++)
            {
                Vector2 toOwner = (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero);
                Vector2 vel = toOwner * Main.rand.NextFloat(3f, 7f) + Main.rand.NextVector2Circular(2f, 2f);
                Dust shadow = Dust.NewDustDirect(target.Center, 1, 1, DustID.Shadowflame,
                    vel.X, vel.Y, 100, default, 0.8f);
                shadow.noGravity = true;
            }

            // 命中闪光
            for (int i = 0; i < 3; i++)
            {
                Dust flash = Dust.NewDustDirect(target.Center, 1, 1, DustID.GemAmethyst,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 0, default, 0.5f);
                flash.noGravity = true;
            }

            // 吸取生命（受成长影响）
            if (Main.rand.NextBool(3))
            {
                int healAmount = Math.Max(1, (int)(damageDone / 20f * growthHealMul));
                Main.player[Projectile.owner].Heal(healAmount);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool? CanDamage()
        {
            // 举幡阶段不造成伤害
            if (CurrentPhase == BannerPhase.Raise)
                return false;
            return base.CanDamage();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 dir = AimAngle.ToRotationVector2();

            if (CurrentPhase == BannerPhase.Channel)
            {
                // 引魂阶段：以漩涡为中心的范围攻击（比视效略小）
                Vector2 vortexCenter = Owner.MountedCenter + dir * currentExtend;
                float checkRadius = AbsorbRadius * 0.45f;
                Vector2 closest = new(
                    MathHelper.Clamp(vortexCenter.X, targetHitbox.Left, targetHitbox.Right),
                    MathHelper.Clamp(vortexCenter.Y, targetHitbox.Top, targetHitbox.Bottom));
                return Vector2.Distance(vortexCenter, closest) < checkRadius;
            }
            else
            {
                // 刺出/收回阶段：沿幡旗杆身的线段碰撞
                Vector2 start = Owner.MountedCenter;
                Vector2 end = start + dir * Math.Max(currentExtend, 0f);
                float collisionPoint = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    start, end, 25f * Projectile.scale, ref collisionPoint);
            }
        }

        public override void CutTiles()
        {
            Vector2 dir = AimAngle.ToRotationVector2();
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + dir * Math.Max(currentExtend, 0f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        // ── 自定义绘制 ──
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(texture.Width / 2f, texture.Height / 2f);
            SpriteEffects effects = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;

            // ── 成长驱动表现层 (先于本体绘制, 各自开合批) ──
            // 1) 幡身幽紫柔光: 亮度随成长比例提升
            float growGlow = (0.35f + 0.85f * growthRatio) * Projectile.scale;
            WeaponVFX.DrawGlowBurst(Projectile.Center, growGlow,
                new Color(150, 60, 255) * (0.4f + 0.5f * growthRatio));

            // 2) 引魂阶段: 幽紫吸魂弧 (spectral ribbon 螺旋汇入幡尖)
            if (CurrentPhase == BannerPhase.Channel)
            {
                int arcs = 3;
                for (int a = 0; a < arcs; a++)
                {
                    Vector2[] pts = new Vector2[8];
                    float baseA = GlobalTimer * 0.1f + a * MathHelper.TwoPi / arcs;
                    for (int k = 0; k < 8; k++)
                    {
                        float t = k / 7f;
                        float r = MathHelper.Lerp(230f, 12f, t) * (0.6f + 0.4f * growthRatio);
                        float ang = baseA + t * 2.2f;
                        pts[k] = Projectile.Center + ang.ToRotationVector2() * r;
                    }
                    WeaponVFX.DrawRibbonTrail(pts, 9f,
                        new Color(95, 40, 165, 150), new Color(210, 165, 255, 200),
                        uvScroll: -GlobalTimer * 0.02f);
                }

                // 3) 满成长: 一记幽紫灵魂脉冲 (走全屏名额仲裁, 名额满则退化柔光)
                if (growthRatio > 0.95f)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, 0.55f,
                        new Color(180, 120, 255), 10f);
            }

            // ── 阶段光晕强度 ──
            float glowIntensity = CurrentPhase switch
            {
                BannerPhase.Channel => 0.6f + 0.3f * MathF.Sin(phaseTimer * 0.3f),
                BannerPhase.Thrust => 0.4f + 0.45f * Math.Clamp(phaseTimer / ThrustTime, 0f, 1f),
                BannerPhase.Retract => 0.7f * (1f - Math.Clamp(phaseTimer / RetractTime, 0f, 1f)),
                _ => 0.2f + 0.12f * MathF.Sin(GlobalTimer * 0.15f),
            };

            // ── 刺出阶段：残影拖尾 ──
            if (CurrentPhase == BannerPhase.Thrust)
            {
                for (int i = AfterimageLength - 1; i >= 1; i--)
                {
                    float progress = 1f - (float)i / AfterimageLength;
                    float alpha = progress * 0.35f;
                    float scale = Projectile.scale * (0.8f + 0.2f * progress);

                    // 紫色调残影，随距离变暗变蓝
                    Color trailColor = Color.Lerp(
                        new Color(80, 20, 160) * alpha,
                        new Color(40, 10, 100) * (alpha * 0.5f),
                        (float)i / AfterimageLength);

                    float trailRotation = afterimageRotations[i] + MathHelper.PiOver2;
                    Main.EntitySpriteDraw(texture,
                        afterimagePositions[i] - Main.screenPosition,
                        null, trailColor, trailRotation, origin,
                        scale, effects, 0);
                }
            }

            // ── 收回阶段：消散残影 ──
            if (CurrentPhase == BannerPhase.Retract)
            {
                float retractT = Math.Clamp(phaseTimer / RetractTime, 0f, 1f);
                Vector2 aimDir = AimAngle.ToRotationVector2();
                for (int i = 1; i <= 4; i++)
                {
                    Vector2 trailPos = Projectile.Center + aimDir * i * 16f * (1f - retractT);
                    float alpha = (1f - retractT) * 0.25f * (1f - i / 5f);
                    Color fadeColor = new Color(130, 50, 200) * alpha;
                    Main.EntitySpriteDraw(texture,
                        trailPos - Main.screenPosition,
                        null, fadeColor, drawRotation, origin,
                        Projectile.scale * (0.7f + 0.3f * (1f - i / 5f)), effects, 0);
                }
            }

            // ── 引魂阶段：多层光环（法阵感） ──
            if (CurrentPhase == BannerPhase.Channel)
            {
                float channelT = Math.Clamp(phaseTimer / ChannelTime, 0f, 1f);

                // 外层大光环：缓慢脉冲
                float outerPulse = 1.4f + 0.15f * MathF.Sin(phaseTimer * 0.2f);
                Color outerAura = new Color(120, 40, 220) * (glowIntensity * 0.15f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, outerAura, drawRotation, origin,
                    Projectile.scale * outerPulse, effects, 0);

                // 中层光环：快速脉冲，偏蓝色
                float midPulse = 1.2f + 0.1f * MathF.Sin(phaseTimer * 0.45f + 1f);
                Color midAura = new Color(80, 60, 255) * (glowIntensity * 0.2f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, midAura, drawRotation, origin,
                    Projectile.scale * midPulse, effects, 0);

                // 内层光环：紫红色高亮
                Color innerAura = new Color(200, 80, 255) * (glowIntensity * 0.25f);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, innerAura, drawRotation, origin,
                    Projectile.scale * 1.08f, effects, 0);
            }

            // ── 通用光晕层（色彩呼吸） ──
            float colorShift = MathF.Sin(GlobalTimer * 0.08f) * 0.5f + 0.5f;
            Color glowColor = Color.Lerp(
                new Color(130, 40, 210),
                new Color(80, 50, 255),
                colorShift) * glowIntensity;

            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, glowColor, drawRotation, origin,
                Projectile.scale * 1.14f, effects, 0);

            // ── 刺出阶段额外的前方高亮 ──
            if (CurrentPhase == BannerPhase.Thrust)
            {
                float thrustT = Math.Clamp(phaseTimer / ThrustTime, 0f, 1f);
                Color thrustGlow = new Color(200, 100, 255) * (0.3f * thrustT);
                Main.EntitySpriteDraw(texture,
                    Projectile.Center - Main.screenPosition,
                    null, thrustGlow, drawRotation, origin,
                    Projectile.scale * (1.05f + 0.15f * thrustT), effects, 0);
            }

            // ── 本体 ──
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition,
                null, lightColor * Projectile.Opacity, drawRotation, origin,
                Projectile.scale, effects, 0);

            return false;
        }
    }
}

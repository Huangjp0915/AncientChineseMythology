using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武 - 北方神兽，水/冰/土属性
    /// 龟蛇一体的Boss，最高防御和生命值
    /// 一阶段：镇龟之盾，冰弹幕与水盾防御
    /// 二阶段：灵蛇觉醒，蛇头涌现，毒与冰的双重攻击
    /// 三阶段：玄天武帝，不可阻挡的潮汐和绝对防御
    /// </summary>
    [AutoloadBossHead]
    public class Xuanwu : ModNPC
    {
        #region 常量定义

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.30f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Idle,
            Phase1_Walk,
            Phase1_Jump,
            Phase1_Slam,
            Phase1_SpinCharge,
            PhaseTransition_2,
            Phase2_SnakeStrike,
            Phase2_VenomSpray,
            Phase2_IceStorm,
            Phase2_DualAssault,
            Phase2_FrostWave,
            PhaseTransition_3,
            Phase3_AbsoluteDefense,
            Phase3_TidalCrush,
            Phase3_Blizzard,
            Phase3_NorthStarJudgment,
            Phase3_YinYangBalance,
            Phase3_Drift
        }

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        private float driftAngle;
        private int strikeCount;
        private float shellRotation;
        private bool absoluteDefenseActive;
        private float glowIntensity = 0.8f;
        private int frameCounter;
        private int phase1CycleIndex;
        private Vector2 chargeStart;
        private Vector2 chargeControl1;
        private Vector2 chargeControl2;
        private Vector2 chargeEnd;
        private float chargeDuration;
        private int chargeSide = 1;

        //着色器与视觉演出
        private static Asset<Effect> frostDistortionRef;
        private static Asset<Effect> causticsRef;
        private static Texture2D noiseTexture;
        private float frostIntensity;
        private float frostTargetIntensity;
        private float causticsIntensity;
        private float causticsTargetIntensity;
        private float shieldVisual; //六边形盾可视化强度
        private float snakeHeadExtend; //蛇头伸出程度(0~1)
        private float phaseFlash; //阶段转换闪光

        //弹簧-阻尼运动系统
        private Vector2 springVel; //弹簧速度分量
        private Vector2 chargeStartTangent; //Hermite起点切线
        private Vector2 chargeEndTangent; //Hermite终点切线

        //Verlet蛇身物理链(7节: 锚点+6段)
        private Vector2[] snakeChain;
        private Vector2[] snakeChainOld;
        private const int SnakeNodes = 7;
        private const float SnakeSegLen = 28f;

        //落地冲击波环
        private float shockwaveRadius;
        private float shockwaveAlpha;
        private Vector2 shockwaveCenter;

        //Lissajous漂移参数
        private float lissajousTime;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 10;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 200;
            NPC.height = 200;
            NPC.damage = 200;
            NPC.defense = 120;
            NPC.lifeMax = 2500000;
            NPC.HitSound = SoundID.NPCHit42;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 5);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.Boss2;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 掉落占位
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(strikeCount);
            writer.Write(absoluteDefenseActive);
            writer.Write(phase1CycleIndex);
            writer.Write(chargeSide);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            strikeCount = reader.ReadInt32();
            absoluteDefenseActive = reader.ReadBoolean();
            phase1CycleIndex = reader.ReadInt32();
            chargeSide = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void FindFrame(int frameHeight) {
            // 10帧: 第0帧=站立, 第6帧=跳跃/蓄力, 0-9连续播放=行走
            switch (Phase) {
                // 站立/静止
                case BossPhase.Intro:
                case BossPhase.PhaseTransition_2:
                case BossPhase.PhaseTransition_3:
                case BossPhase.Phase1_Idle:
                case BossPhase.Phase1_Slam when SubState == 1:
                case BossPhase.Phase2_FrostWave:
                case BossPhase.Phase3_AbsoluteDefense:
                    NPC.frame.Y = 0;
                    frameCounter = 0;
                    break;

                // 蓄力/跳跃姿态 (第6帧)
                case BossPhase.Phase1_Jump:
                case BossPhase.Phase1_Slam when SubState == 0:
                case BossPhase.Phase1_SpinCharge when SubState == 0:
                case BossPhase.Phase2_DualAssault when SubState == 0:
                case BossPhase.Phase3_TidalCrush when SubState == 0:
                case BossPhase.Phase3_NorthStarJudgment when SubState == 0:
                    NPC.frame.Y = 6 * frameHeight;
                    frameCounter = 0;
                    break;

                // 高速旋转 (快速切帧)
                case BossPhase.Phase1_SpinCharge when SubState == 1:
                case BossPhase.Phase2_DualAssault when SubState == 1:
                    frameCounter++;
                    if (frameCounter >= 2) {
                        frameCounter = 0;
                        NPC.frame.Y += frameHeight;
                        if (NPC.frame.Y >= frameHeight * 10)
                            NPC.frame.Y = 0;
                    }
                    break;

                // 蛇击: 准备=跳跃帧, 收招=站立帧
                case BossPhase.Phase2_SnakeStrike:
                    NPC.frame.Y = SubState == 0 ? 6 * frameHeight : 0;
                    frameCounter = 0;
                    break;

                // 默认: 行走动画循环
                default: {
                    bool isMoving = NPC.velocity.LengthSquared() > 1f;
                    int rate = isMoving ? 5 : 8;
                    frameCounter++;
                    if (frameCounter >= rate) {
                        frameCounter = 0;
                        NPC.frame.Y += frameHeight;
                        if (NPC.frame.Y >= frameHeight * 10)
                            NPC.frame.Y = 0;
                    }
                    break;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 4; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Ice, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Ice, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Water, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedXuanwu = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            NPC.defense = 120;
            globalTime += 1f / 60f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.5f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Idle: RunPhase1Idle(target); break;
                case BossPhase.Phase1_Walk: RunPhase1Walk(target); break;
                case BossPhase.Phase1_Jump: RunPhase1Jump(target); break;
                case BossPhase.Phase1_Slam: RunPhase1Slam(target); break;
                case BossPhase.Phase1_SpinCharge: RunPhase1SpinCharge(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_SnakeStrike: RunPhase2SnakeStrike(target); break;
                case BossPhase.Phase2_VenomSpray: RunPhase2VenomSpray(target); break;
                case BossPhase.Phase2_IceStorm: RunPhase2IceStorm(target); break;
                case BossPhase.Phase2_DualAssault: RunPhase2DualAssault(target); break;
                case BossPhase.Phase2_FrostWave: RunPhase2FrostWave(target); break;
                case BossPhase.PhaseTransition_3: RunPhaseTransition3(target); break;
                case BossPhase.Phase3_AbsoluteDefense: RunPhase3AbsoluteDefense(target); break;
                case BossPhase.Phase3_TidalCrush: RunPhase3TidalCrush(target); break;
                case BossPhase.Phase3_Blizzard: RunPhase3Blizzard(target); break;
                case BossPhase.Phase3_NorthStarJudgment: RunPhase3NorthStarJudgment(target); break;
                case BossPhase.Phase3_YinYangBalance: RunPhase3YinYangBalance(target); break;
                case BossPhase.Phase3_Drift: RunPhase3Drift(target); break;
            }

            // Verlet蛇身物理更新(二阶段起)
            if (IsPhase2 || IsPhase3) {
                if (snakeChain == null) {
                    snakeChain = new Vector2[SnakeNodes];
                    snakeChainOld = new Vector2[SnakeNodes];
                    for (int i = 0; i < SnakeNodes; i++) {
                        snakeChain[i] = NPC.Center + new Vector2(0, i * SnakeSegLen);
                        snakeChainOld[i] = snakeChain[i];
                    }
                }
                //锚点跟随蛇头位置
                snakeChain[0] = NPC.Center + new Vector2(NPC.spriteDirection * 25f, -85f - snakeHeadExtend * 40f);
                ACMUtils.VerletStep(snakeChain, 0.4f, SnakeSegLen, 4);
            }

            // 旋转攻击时使用shellRotation，普通状态使用轻微倾斜
            bool isSpinning = (Phase == BossPhase.Phase1_SpinCharge && SubState == 1) ||
                              (Phase == BossPhase.Phase2_DualAssault && SubState == 1);
            if (isSpinning)
                NPC.rotation = shellRotation;
            else
                NPC.rotation = NPC.velocity.X * 0.005f;

            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

            // 冰水光源
            float iceGlow = absoluteDefenseActive ? 2f : (IsPhase3 ? 1.5f : 1f);
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.5f, 0.9f) * glowIntensity * iceGlow);

            //视觉强度平滑过渡
            UpdateVisualIntensities();
        }

        private void UpdateVisualIntensities() {
            float lerpSpeed = 0.05f;
            frostIntensity = MathHelper.Lerp(frostIntensity, frostTargetIntensity, lerpSpeed);
            causticsIntensity = MathHelper.Lerp(causticsIntensity, causticsTargetIntensity, lerpSpeed);

            //默认目标: 根据阶段常驻轻微焦散
            if (IsPhase3)
                causticsTargetIntensity = MathHelper.Max(causticsTargetIntensity, 0.15f);
            else if (IsPhase2)
                causticsTargetIntensity = MathHelper.Max(causticsTargetIntensity, 0.08f);
            else
                causticsTargetIntensity = MathHelper.Max(causticsTargetIntensity, 0f);

            //蛇头伸出: 二阶段慢速伸出
            float snakeTarget = IsPhase2 || IsPhase3 ? 1f : 0f;
            snakeHeadExtend = MathHelper.Lerp(snakeHeadExtend, snakeTarget, 0.02f);

            //盾可视化自然衰减
            if (!absoluteDefenseActive)
                shieldVisual = MathHelper.Lerp(shieldVisual, 0f, 0.06f);

            //闪光衰减
            phaseFlash *= 0.9f;

            //着色器强度在没有特殊攻击时自然回落
            frostTargetIntensity *= 0.97f;
            if (causticsTargetIntensity > 0.15f && !IsAttackUsingCaustics())
                causticsTargetIntensity *= 0.97f;
        }

        private bool IsAttackUsingCaustics() {
            return Phase == BossPhase.Phase3_TidalCrush ||
                   Phase == BossPhase.Phase2_FrostWave ||
                   Phase == BossPhase.Phase2_DualAssault ||
                   (Phase == BossPhase.Phase3_Drift && IsPhase3);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.PhaseTransition_3 && Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_3);
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            strikeCount = 0;
            absoluteDefenseActive = false;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        private BossPhase GetNextPhase1Attack() {
            BossPhase next = phase1CycleIndex % 2 == 0
                ? BossPhase.Phase1_Walk
                : BossPhase.Phase1_SpinCharge;
            phase1CycleIndex++;
            return next;
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_SnakeStrike,
                1 => (int)BossPhase.Phase2_VenomSpray,
                2 => (int)BossPhase.Phase2_IceStorm,
                3 => (int)BossPhase.Phase2_DualAssault,
                _ => (int)BossPhase.Phase2_FrostWave
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase3_AbsoluteDefense,
                1 => (int)BossPhase.Phase3_TidalCrush,
                2 => (int)BossPhase.Phase3_Blizzard,
                3 => (int)BossPhase.Phase3_NorthStarJudgment,
                _ => (int)BossPhase.Phase3_YinYangBalance
            });
        }

        private int IceProjectile(Vector2 pos, Vector2 vel, int damage, int timeLeft = 180, int mode = 0, float modeParam = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<XuanwuIceShard>(), damage, 0f, Main.myPlayer, mode, modeParam);
            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].timeLeft = timeLeft;
            }
            return proj;
        }

        private int WaterProjectile(Vector2 pos, Vector2 vel, int damage, int mode = 0, float modeParam = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<XuanwuIceShard>(), damage, 0f, Main.myPlayer, mode, modeParam);
            return proj;
        }

        //毒牙生成助手: 支持AI模式
        private int VenomProjectile(Vector2 pos, Vector2 vel, int damage, int mode = 0, float modeParam = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<XuanwuVenomFang>(), damage, 0f, Main.myPlayer, mode, modeParam);
            return proj;
        }

        //斐波那契黄金角螺旋弹幕
        private void FibonacciSpiral(Vector2 center, int count, float speed, int damage, bool ice = true, int mode = 0, float modeParam = 0f) {
            for (int i = 0; i < count; i++) {
                float angle = i * ACMUtils.GoldenAngle;
                float spd = speed + i * 0.3f;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spd;
                if (ice)
                    IceProjectile(center, vel, damage, 180, mode, modeParam);
                else
                    VenomProjectile(center, vel, damage, mode, modeParam);
            }
        }

        //帘幕波次: 左右两侧依次发射
        private void CurtainWave(Vector2 center, float groundY, int count, int damage, float spacing = 60f) {
            for (int i = 1; i <= count; i++) {
                float delay = i * 4f; //每颗间隔4帧延迟(通过速度时差模拟)
                float speed = 3.5f + i * 1.5f;
                IceProjectile(new Vector2(center.X, groundY - 20), new Vector2(speed, -1.5f), damage, 160, 0, 0f);
                IceProjectile(new Vector2(center.X, groundY - 20), new Vector2(-speed, -1.5f), damage, 160, 0, 0f);
            }
        }

        //预判齐射: 使用LeadTarget计算提前量
        private void LeadTargetSalvo(Vector2 center, Player target, int count, float speed, int damage, float spread = 0.05f) {
            Vector2 aimDir = ACMUtils.LeadTarget(center, target.Center, target.velocity, speed);
            for (int i = 0; i < count; i++) {
                float offsetAngle = (i - (count - 1) * 0.5f) * spread;
                Vector2 vel = aimDir.RotatedBy(offsetAngle) * speed;
                IceProjectile(center, vel, damage, 200, 2, 20f); //Mode 2延迟追踪, 20帧后启动
            }
        }

        //双螺旋DNA扩展
        private void SpiralDNA(Vector2 center, int count, float speed, int damage, float rotSpeed = 0.18f) {
            for (int i = 0; i < count; i++) {
                float angle = i * rotSpeed;
                float spd = speed + i * 0.25f;
                //A链
                VenomProjectile(center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spd, damage, 1, rotSpeed);
                //B链(对称偏移π)
                VenomProjectile(center, new Vector2(MathF.Cos(angle + MathHelper.Pi), MathF.Sin(angle + MathHelper.Pi)) * spd, damage, 1, rotSpeed);
            }
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                // 玄武从大地中缓缓升起
                NPC.Center = target.Center + new Vector2(0, 500);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f }, target.Center);
            }

            Vector2 targetPos = target.Center + new Vector2(0, -200);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.015f);

            // 冰水粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, -2f, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity += (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
                }
            }

            if (PhaseTimer >= 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitY, 15f, 8f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 20; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10, 10);
                    }
                }
                TransitionTo(BossPhase.Phase1_Walk);
            }
        }

        #endregion

        #region 一阶段：镇龟

        private float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)(searchStartY / 16f);
            for (int tileY = startTileY; tileY < startTileY + 60; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return searchStartY + 500f;
        }

        /// <summary>
        /// 攻击循环间的短暂停留，交替选择行走-跳砸 / 旋转曲线冲刺
        /// </summary>
        private void RunPhase1Idle(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -280);
            //弹簧悬停: k=2低刚度 c=5适度阻尼 → 慢速漂移有惯性
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, hoverPos, ref springVel, 2f, 5f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.Ice, 0, -1f, 150, default, 1.2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 50) {
                TransitionTo(GetNextPhase1Attack());
            }
        }

        /// <summary>
        /// 在地面行走接近玩家，模拟重型龟甲在地面推进的压迫感
        /// </summary>
        private void RunPhase1Walk(Player target) {
            // 搜索地面
            float groundY = FindGroundY(NPC.Center.X, Math.Min(NPC.Center.Y, target.Center.Y));
            float targetY = groundY - NPC.height / 2f;

            // 垂直：弹簧贴地 k=4高刚度快速贴合 c=7略过阻尼有弹跳
            float newY = ACMUtils.SpringDamp(NPC.Center.Y, targetY, ref springVel.Y, 4f, 7f, 1f / 60f);
            NPC.velocity.Y = newY - NPC.Center.Y;

            // 水平：弹簧加速 k=1.5低刚度慢起步 c=3.5 → 行走有沉重启动感
            float dir = target.Center.X > NPC.Center.X ? 1f : -1f;
            float walkSpeed = Main.expertMode ? 7f : 5.5f;
            float walkTargetX = NPC.Center.X + dir * 500f;
            float newX = ACMUtils.SpringDamp(NPC.Center.X, walkTargetX, ref springVel.X, 1.5f, 3.5f, 1f / 60f);
            NPC.velocity.X = MathHelper.Clamp(newX - NPC.Center.X, -walkSpeed, walkSpeed);

            // 行走扬尘
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                Vector2 footPos = NPC.Center + new Vector2(Main.rand.NextFloat(-70, 70), NPC.height / 2f - 10);
                Dust d = Dust.NewDustDirect(footPos, 0, 0, DustID.Smoke, dir * -2f, -1.5f, 140, default, 1.8f);
                d.noGravity = false;
            }
            // 冰霜尾迹
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 40), 0, 0, DustID.Ice, 0, -1f, 120, default, 1.2f);
                d.noGravity = true;
            }

            // 接近玩家 或 行走超时 → 起跳
            float distX = MathF.Abs(NPC.Center.X - target.Center.X);
            if (distX < 250f || PhaseTimer > 160) {
                TransitionTo(BossPhase.Phase1_Jump);
            }
        }

        /// <summary>
        /// 跳跃到玩家头顶，分为起跳和悬停两个子阶段
        /// SubState 0: 弧形上升到目标位置（玩家头顶500px）
        /// SubState 1: 锁定位置蓄力，准备下砸
        /// </summary>
        private void RunPhase1Jump(Player target) {
            if (SubState == 0) {
                // ── 起跳：记录起点和目标，用缓动曲线飞过去 ──
                if (AttackTimer <= 1) {
                    chargeStart = NPC.Center;
                    // 目标：玩家头顶 500px，X 对齐玩家
                    chargeEnd = target.Center + new Vector2(0, -500);
                    NPC.velocity = Vector2.Zero;

                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.8f }, NPC.Center);

                    // 起跳碎石
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 18; i++) {
                            Dust d = Dust.NewDustDirect(
                                NPC.Center + new Vector2(Main.rand.NextFloat(-90, 90), NPC.height / 2f),
                                0, 0, DustID.Smoke, Main.rand.NextFloat(-5, 5), -4f, 130, default, 2.2f);
                            d.noGravity = false;
                        }
                        for (int i = 0; i < 8; i++) {
                            Dust d = Dust.NewDustDirect(
                                NPC.Center + new Vector2(Main.rand.NextFloat(-60, 60), NPC.height / 2f),
                                0, 0, DustID.Ice, Main.rand.NextFloat(-3, 3), -6f, 100, default, 1.5f);
                            d.noGravity = true;
                        }
                    }
                    NPC.netUpdate = true;
                }

                // 用 BackOut 缓动做弧形上升（先快后慢，略微超调再回弹）
                float riseDuration = 40f;
                float t = ACMUtils.Clamp01((AttackTimer - 1) / riseDuration);
                float tEased = ACMUtils.BackOut(t);
                Vector2 curPos = Vector2.Lerp(chargeStart, chargeEnd, tEased);
                NPC.velocity = curPos - NPC.Center;

                // 上升粒子
                if (Main.netMode != NetmodeID.Server && AttackTimer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 30), 0, 0, DustID.Ice, 0, 2f, 100, default, 1.5f);
                    d.noGravity = true;
                }

                // 上升完成 → 进入悬停
                if (t >= 1f) {
                    SubState = 1;
                    AttackTimer = 0;
                    // 锁定当前位置作为悬停点
                    chargeStart = NPC.Center;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
            }
            else {
                // ── 悬停蓄力（不追踪玩家，锁定位置）──
                NPC.velocity = (chargeStart - NPC.Center) * 0.2f;

                // 蓄力震颤（逐渐加强）
                float shakeStr = MathHelper.Clamp(AttackTimer / 55f, 0f, 1f) * 4f;
                if (AttackTimer > 10)
                    NPC.Center += Main.rand.NextVector2Circular(shakeStr, shakeStr);

                // 下砸预示 — 冰粒子向下汇聚
                if (Main.netMode != NetmodeID.Server) {
                    int dustCount = AttackTimer > 35 ? 6 : 3;
                    for (int i = 0; i < dustCount; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(70, 40);
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Ice, 0, 3f + shakeStr, 100, default, 1.8f);
                        d.noGravity = true;
                    }
                }

                if (AttackTimer > 20) {
                    TransitionTo(BossPhase.Phase1_Slam);
                }
            }
        }

        /// <summary>
        /// 从高空猛砸地面，落地瞬间爆发大量弹幕
        /// </summary>
        private void RunPhase1Slam(Player target) {
            if (SubState == 0) {
                // ── 加速下坠: 水平弹簧微追踪 + 真正加速度穾分 ──
                float hTarget = NPC.Center.X + (target.Center.X - NPC.Center.X) * 0.15f;
                float newHX = ACMUtils.SpringDamp(NPC.Center.X, hTarget, ref springVel.X, 1f, 4f, 1f / 60f);
                NPC.velocity.X = newHX - NPC.Center.X;
                NPC.velocity.Y += 2.2f;
                if (NPC.velocity.Y > 32f) NPC.velocity.Y = 32f;

                // 下落拖尾
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 20),
                            0, 0, DustID.Ice, Main.rand.NextFloat(-1, 1), -NPC.velocity.Y * 0.12f, 100, default, 2f);
                        d.noGravity = true;
                    }
                }

                // 地面检测
                float groundY = FindGroundY(NPC.Center.X, NPC.Center.Y);
                if (NPC.Center.Y + NPC.height / 2f >= groundY || AttackTimer > 90) {
                    // ── 着地 ──
                    if (NPC.Center.Y + NPC.height / 2f >= groundY)
                        NPC.Center = new Vector2(NPC.Center.X, groundY - NPC.height / 2f);
                    NPC.velocity = Vector2.Zero;
                    SubState = 1;
                    AttackTimer = 0;
                    //初始化冲击波环 + 弹簧反弹
                    shockwaveRadius = 0f;
                    shockwaveAlpha = 0.8f;
                    shockwaveCenter = NPC.Center;
                    springVel = new Vector2(0, -18f); //向上反弹速度

                    // 震屏
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -1f, Volume = 1.5f }, NPC.Center);
                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier camMod = new(NPC.Center, Vector2.UnitY, 22f, 14f, 45, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(camMod);
                    }

                    // 弹幕爆发 —— 帘幕波次设计
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 地面帘幕波: 左右递进冰柱(速度时差模拟依次升起)
                        int waveCount = Main.expertMode ? 10 : 7;
                        float waveY = groundY - 20;
                        CurtainWave(NPC.Center, groundY, waveCount, NPC.damage / 4);

                        // 正上方3颗延迟追踪冰锥(Mode 2, 30帧延迟)
                        for (int i = -1; i <= 1; i++) {
                            IceProjectile(NPC.Center, new Vector2(i * 2f, -12f), NPC.damage / 4, 240, 2, 30f);
                        }

                        // 上方散射正弦冰柱(Mode 1)
                        int scatterCount = Main.expertMode ? 8 : 5;
                        for (int i = 0; i < scatterCount; i++) {
                            float vx = Main.rand.NextFloat(-6f, 6f);
                            float vy = -Main.rand.NextFloat(10f, 18f);
                            IceProjectile(NPC.Center, new Vector2(vx, vy), NPC.damage / 5, 180, 1, Main.rand.NextFloat(0f, MathHelper.TwoPi));
                        }
                    }

                    // 落地粉碎粒子
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 35; i++) {
                            Dust d = Dust.NewDustDirect(
                                new Vector2(NPC.Center.X + Main.rand.NextFloat(-130, 130), groundY - 10),
                                0, 0, DustID.Ice, Main.rand.NextFloat(-9, 9), Main.rand.NextFloat(-14, -4), 80, default, 2.5f);
                            d.noGravity = true;
                        }
                        for (int i = 0; i < 25; i++) {
                            Dust d = Dust.NewDustDirect(
                                new Vector2(NPC.Center.X + Main.rand.NextFloat(-160, 160), groundY - 5),
                                0, 0, DustID.Smoke, Main.rand.NextFloat(-7, 7), -2.5f, 150, default, 2.2f);
                            d.noGravity = false;
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                // ── 落地恢复: 弹簧反弹抵地 ──
                float groundY = FindGroundY(NPC.Center.X, NPC.Center.Y - 100f);
                float anchorY = groundY - NPC.height / 2f;
                float newRY = ACMUtils.SpringDamp(NPC.Center.Y, anchorY, ref springVel.Y, 8f, 6f, 1f / 60f);
                NPC.velocity.Y = newRY - NPC.Center.Y;
                NPC.velocity.X *= 0.92f;
                //冲击波扩展
                if (shockwaveAlpha > 0.01f) {
                    shockwaveRadius += 18f;
                    shockwaveAlpha *= 0.92f;
                }
                if (AttackTimer > 45) {
                    NPC.scale = 1f;
                    TransitionTo(BossPhase.Phase1_Idle);
                }
            }
        }

        /// <summary>
        /// 旋转壳体沿贝塞尔曲线冲刺，交替弧度方向，3次冲刺一循环
        /// </summary>
        private void RunPhase1SpinCharge(Player target) {
            if (SubState == 0) {
                // ── 蓄力 ──
                shellRotation += 0.06f + AttackTimer * 0.004f;
                //蓄力制动: 弹簧阻尼
                Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc - NPC.Center;
                //能量蓄积脂冲: scale 1.0→1.08→1.0
                float chargeProg = ACMUtils.Clamp01(AttackTimer / 35f);
                NPC.scale = 1f + MathF.Sin(chargeProg * MathHelper.Pi) * 0.08f;

                //蓄力时冰霜扭曲逐渐增强
                frostTargetIntensity = MathHelper.Lerp(0f, 0.3f, ACMUtils.Clamp01(AttackTimer / 35f));

                // 粒子向中心收束
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 140f - AttackTimer * 2.5f;
                        if (dist < 30f) dist = 30f;
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Ice, 0, 0, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }

                if (AttackTimer > 35) {
                    // 计算贝塞尔曲线参数
                    chargeStart = NPC.Center;
                    Vector2 toTarget = target.Center - NPC.Center;
                    float totalDist = toTarget.Length();
                    if (totalDist < 1f) totalDist = 1f;
                    Vector2 forward = toTarget / totalDist;
                    Vector2 perp = new Vector2(-forward.Y, forward.X);

                    chargeEnd = target.Center + forward * 180f;

                    //Hermite切线: 起点切线=朝向玩家的动量, 终点切线=穿越后的滑行方向
                    chargeStartTangent = toTarget * 0.8f;
                    chargeEndTangent = forward * totalDist * 0.6f;

                    // 曲线偏移量 + Hermite辅助切线
                    if (strikeCount < 2) {
                        float curveOffset = totalDist * 0.55f * chargeSide;
                        chargeControl1 = chargeStart + toTarget * 0.3f + perp * curveOffset;
                        chargeControl2 = chargeStart + toTarget * 0.7f + perp * curveOffset * 0.5f;
                        //Hermite切线偏转
                        chargeStartTangent += perp * curveOffset * 0.4f;
                    }
                    else {
                        // 第3次: S形曲线穿越玩家
                        float sOffset = totalDist * 0.45f;
                        chargeControl1 = chargeStart + toTarget * 0.3f + perp * sOffset * chargeSide;
                        chargeControl2 = chargeStart + toTarget * 0.7f - perp * sOffset * chargeSide;
                        chargeStartTangent += perp * sOffset * 0.35f * chargeSide;
                        chargeEndTangent += perp * (-sOffset * 0.35f * chargeSide);
                    }

                    chargeDuration = 48f;
                    chargeSide *= -1;

                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // ── 曲线冲刺 ──
                shellRotation += 0.28f;

                //冲刺中保持冰霜扭曲
                frostTargetIntensity = 0.2f;

                float t = ACMUtils.Clamp01(AttackTimer / chargeDuration);
                float tSmooth = ACMUtils.SineInOut(t);
                //Hermite样条替代贝塞尔: 端点速度更可控
                Vector2 curvePos = ACMUtils.Hermite(chargeStart, chargeStartTangent, chargeEnd, chargeEndTangent, tSmooth);
                NPC.velocity = curvePos - NPC.Center;
                NPC.scale = 1f; //冲刺时恢复原始尺寸

                // 冲刺拖尾冰雾
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(35, 35),
                            0, 0, DustID.Ice, 0, 0, 80, default, 2.2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);
                    }
                }

                // 冲刺中散落正弦波冰碎(Mode 1)
                if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float shedAngle = shellRotation * 2f;
                    IceProjectile(NPC.Center,
                        new Vector2(MathF.Cos(shedAngle), MathF.Sin(shedAngle)) * 7f,
                        NPC.damage / 5, 120, 1, Main.rand.NextFloat(0f, MathHelper.TwoPi)); //Mode 1正弦波, 随机相位
                }

                if (AttackTimer >= (int)chargeDuration) {
                    SubState = 2;
                    AttackTimer = 0;
                    //动量保留滑行: 不是急停而是减速滑行
                    springVel = NPC.velocity * 0.6f;
                    NPC.velocity *= 0.35f;

                    //冲刺终点: 冰霜脉冲
                    frostTargetIntensity = 0.5f;
                    phaseFlash = 0.3f;

                    // 冲刺终点: 黄金角螺旋冰碎 + 延迟追踪冰锥
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        FibonacciSpiral(NPC.Center, 12, 8f, NPC.damage / 5, true, 0, 0f); //黄金角直射
                        //3颗延迟追踪冰锥(Mode 2, 25帧延迟)
                        for (int i = -1; i <= 1; i++) {
                            Vector2 hVel = new Vector2(i * 3f, -4f);
                            IceProjectile(NPC.Center, hVel, NPC.damage / 5, 240, 2, 25f);
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 12; i++) {
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 80, default, 2.5f);
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(6, 6);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                // ── 冲刺间短暂恢复: 弹簧滑行制动 ──
                Vector2 ncc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 1f, 8f, 1f / 60f);
                NPC.velocity = ncc - NPC.Center;
                shellRotation *= 0.95f;

                if (AttackTimer > 22) {
                    strikeCount++;
                    if (strikeCount < 3) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else {
                        TransitionTo(BossPhase.Phase1_Idle);
                    }
                }
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            //转阶阶段: 高阻尼制动
            Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 10f, 1f / 60f);
            NPC.velocity = nc - NPC.Center;
            NPC.dontTakeDamage = true;

            //0-40: 裂纹粒子扩散 + 冰霜着色器缓升
            if (PhaseTimer <= 40) {
                frostTargetIntensity = MathHelper.Lerp(0f, 0.3f, PhaseTimer / 40f);
            }

            //40-70: 水面焦散开始 + 蛇头伸出
            if (PhaseTimer > 40 && PhaseTimer <= 70) {
                causticsTargetIntensity = MathHelper.Lerp(0f, 0.35f, (PhaseTimer - 40f) / 30f);
                snakeHeadExtend = MathHelper.Lerp(0f, 1f, (PhaseTimer - 40f) / 30f);
            }

            // 蛇觉醒特效 - 水流涌动
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 350 - PhaseTimer * 2.5f;
                    if (dist < 40) dist = 40;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 7f;
                }
                // 蛇形绿色粒子
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.CursedTorch, 0, 0, 80, default, 2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                //蛇觉醒: 焦散闪烁 + 相位闪白
                phaseFlash = 0.5f;
                causticsTargetIntensity = 0.5f;
                frostTargetIntensity = 0.1f;
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 45, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (PhaseTimer >= 100) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.15f);
                glowIntensity = 1.2f;
                //进入P2后焦散保持低底色
                causticsTargetIntensity = 0.08f;
                frostTargetIntensity = 0f;
                TransitionTo(BossPhase.Phase2_SnakeStrike);
            }
        }

        private void RunPhaseTransition3(Player target) {
            //转阶: 高阻尼制动
            Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 10f, 1f / 60f);
            NPC.velocity = nc - NPC.Center;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);

            //0-50: 漩涡收拢 + 冰霜/焦散双升
            if (PhaseTimer <= 50) {
                float prog = PhaseTimer / 50f;
                frostTargetIntensity = MathHelper.Lerp(0f, 0.6f, prog);
                causticsTargetIntensity = MathHelper.Lerp(0f, 0.5f, prog);
            }
            //50-70: 最高强度蓄力
            if (PhaseTimer > 50 && PhaseTimer <= 70) {
                frostTargetIntensity = 0.7f;
                causticsTargetIntensity = 0.55f;
                shieldVisual = MathHelper.Lerp(shieldVisual, 0.8f, 0.08f);
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 15; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 300);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Ice,
                        1 => DustID.Water,
                        _ => DustID.CursedTorch
                    };
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 2f }, NPC.Center);
                //玄武合一: 全屏闪白 + 着色器骤降
                phaseFlash = 1f;
                frostTargetIntensity = 0.2f;
                causticsTargetIntensity = 0.15f;
                shieldVisual = 0f;
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 冰水爆发
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 24; i++) {
                        float angle = MathHelper.TwoPi / 24 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 13f;
                        IceProjectile(NPC.Center, vel, NPC.damage / 3);
                    }
                }
            }

            //100-130: P3环境焦散底色建立
            if (PhaseTimer > 100 && PhaseTimer <= 130) {
                causticsTargetIntensity = MathHelper.Lerp(0f, 0.1f, (PhaseTimer - 100f) / 30f);
                frostTargetIntensity = MathHelper.Lerp(frostTargetIntensity, 0.05f, 0.05f);
            }

            if (PhaseTimer >= 130) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.25f);
                glowIntensity = 1.8f;
                //P3常驻低焦散
                causticsTargetIntensity = 0.1f;
                frostTargetIntensity = 0.03f;
                TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        #endregion

        #region 二阶段：灵蛇觉醒

        private void RunPhase2SnakeStrike(Player target) {
            if (SubState == 0) {
                //蛇击蓄力: k=8高刚度快速攻击, c=6中等阻尼
                Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc - NPC.Center;
                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(0, -80) + Main.rand.NextVector2Circular(30, 30), 0, 0, DustID.CursedTorch, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                }

                if (AttackTimer > 20) {
                    SubState = 1;
                    AttackTimer = 0;

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 snakePos = NPC.Center + new Vector2(0, -80);
                        Vector2 vel = (target.Center - snakePos).SafeNormalize(Vector2.UnitX) * 22f;
                        //蛇形路径毒牙(Mode 1)
                        for (int i = -2; i <= 2; i++) {
                            Vector2 v = vel.RotatedBy(i * MathHelper.ToRadians(6f));
                            VenomProjectile(snakePos, v, NPC.damage / 4, 1, 0.15f + MathF.Abs(i) * 0.03f);
                        }
                        // 同步轨道冰爆(Mode 3: 围绕Boss旋转后释放)
                        for (int i = 0; i < 6; i++) {
                            float angle = MathHelper.TwoPi / 6 * i;
                            IceProjectile(NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 60f,
                                new Vector2(MathF.Cos(angle + MathHelper.PiOver2), MathF.Sin(angle + MathHelper.PiOver2)) * 4f,
                                NPC.damage / 5, 180, 3, NPC.whoAmI);
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                //蛇击恢复: 弹簧制动
                Vector2 nc2 = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc2 - NPC.Center;
                if (AttackTimer > 25) {
                    strikeCount++;
                    if (strikeCount < 6) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2VenomSpray(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -250);
            //弹簧悬停: k=2 c=4.5 蛇头喷射时微微晃动
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, hoverPos, ref springVel, 2f, 4.5f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 9 : 7;
                float spread = MathHelper.ToRadians(70f);
                for (int i = 0; i < count; i++) {
                    float angle = -spread / 2 + spread / (count - 1) * i;
                    Vector2 vel = dir.RotatedBy(angle) * Main.rand.NextFloat(12f, 16f);
                    //蛇形路径毒牙(Mode 1, 不同频率产生层次感)
                    VenomProjectile(NPC.Center, vel, NPC.damage / 4, 1, 0.1f + i * 0.01f);
                }
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f }, NPC.Center);
            }

            // 同步冰弹: 延迟追踪(Mode 2)
            if (AttackTimer % 16 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 8f;
                IceProjectile(NPC.Center, vel, NPC.damage / 5, 240, 2, 35f); //35帧延迟后追踪
                IceProjectile(NPC.Center, vel.RotatedBy(MathHelper.ToRadians(20)), NPC.damage / 5, 240, 2, 40f);
                IceProjectile(NPC.Center, vel.RotatedBy(-MathHelper.ToRadians(20)), NPC.damage / 5, 240, 2, 40f);
            }

            if (AttackTimer > 90) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2IceStorm(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -500);
            //弹簧高空悬停: k=2 c=5
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, hoverPos, ref springVel, 2f, 5f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            int interval = Main.expertMode ? 3 : 5;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                //瞄准预判冰雨: 使用LeadTarget计算提前量
                int count = Main.expertMode ? 4 : 3;
                for (int i = 0; i < count; i++) {
                    float x = target.Center.X + Main.rand.NextFloat(-550, 550);
                    Vector2 pos = new Vector2(x, target.Center.Y - 650);
                    Vector2 aimDir = ACMUtils.LeadTarget(pos, target.Center, target.velocity, 16f);
                    Vector2 vel = aimDir * 16f + new Vector2(Main.rand.NextFloat(-1f, 1f), 0);
                    IceProjectile(pos, vel, NPC.damage / 4, 200, 0, 0f);
                }
            }

            // 双侧蛇毒: 收束螺旋(Mode 2围绕玩家收紧)
            if (AttackTimer % 22 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 pos = target.Center + new Vector2(side * 600, -200 + i * 120);
                        VenomProjectile(pos, new Vector2(-side * 10f, 0), NPC.damage / 5, 2, NPC.target); //Mode 2收束螺旋围绕玩家
                    }
                }
            }

            if (AttackTimer > 120) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2DualAssault(Player target) {
            if (SubState == 0) {
                //蓄力制动
                Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc - NPC.Center;

                //蓄力: 蛇头抬起 + 焦散出现
                causticsTargetIntensity = MathHelper.Lerp(0.1f, 0.35f, ACMUtils.Clamp01(AttackTimer / 22f));
                snakeHeadExtend = MathHelper.Lerp(snakeHeadExtend, 1f, 0.08f);

                // 蓄力时轨道冰碎(Mode 3引力轨道: 绕Boss旋转后释放)
                if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float a = AttackTimer * 0.2f;
                    IceProjectile(NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 80f,
                        new Vector2(MathF.Cos(a + MathHelper.PiOver2), MathF.Sin(a + MathHelper.PiOver2)) * 5f,
                        NPC.damage / 6, 180, 3, NPC.whoAmI); //Mode 3轨道, 锤点=Boss NPC索引
                }
                if (AttackTimer > 22) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 22f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                shellRotation += 0.1f;

                //冲刺中焦散持续
                causticsTargetIntensity = 0.3f;

                // 蛇头独立追踪射击: 蛇形路径毒牙(Mode 1)
                if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 snakePos = NPC.Center + new Vector2(0, -80);
                    Vector2 vel = (target.Center - snakePos).SafeNormalize(Vector2.Zero) * 16f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(8f));
                    VenomProjectile(snakePos, vel, NPC.damage / 4, 1, 0.15f); //Mode 1蛇形路径
                }

                // 龟甲释放冰碎 + 垂直冰尾迹
                if (AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float angle = shellRotation;
                    IceProjectile(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f, NPC.damage / 5, 120);
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    IceProjectile(NPC.Center + perpDir * 40f, perpDir * 5f, NPC.damage / 6, 100);
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 80, default, 2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.1f);
                    }
                }

                if (AttackTimer > 22) {
                    //弹簧滑行减速代替乘法阻尼
                    Vector2 ncd = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 1f, 6f, 1f / 60f);
                    NPC.velocity = ncd - NPC.Center;
                }
                if (AttackTimer > 40) {
                    //终结: 交叉十字冰柱(4轴×8颗轮替) + 蛇形毒雾扇
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        //交叉十字: 4轴×4颗
                        for (int axis = 0; axis < 4; axis++) {
                            float baseAngle = axis * MathHelper.PiOver2 + strikeCount * MathHelper.ToRadians(22.5f);
                            for (int i = 0; i < 4; i++) {
                                float spd = 5f + i * 2.5f;
                                IceProjectile(NPC.Center, new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle)) * spd,
                                    NPC.damage / 5, 160, 0, 0f);
                            }
                        }
                        //蛇头毒雾扇: 蛇形路径(Mode 1)
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        for (int i = -3; i <= 3; i++) {
                            Vector2 venomVel = toPlayer.RotatedBy(i * MathHelper.ToRadians(8f)) * 14f;
                            VenomProjectile(NPC.Center + new Vector2(0, -80), venomVel, NPC.damage / 5, 1, 0.12f + MathF.Abs(i) * 0.02f);
                        }
                    }
                    phaseFlash = 0.2f;
                    strikeCount++;
                    if (strikeCount < 4) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2FrostWave(Player target) {
            //冰浪释放时高阻尼制动
            Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 10f, 1f / 60f);
            NPC.velocity = nc - NPC.Center;

            // 3层帘幕设计 — 各层几何特征不同
            if (AttackTimer == 25 || AttackTimer == 50 || AttackTimer == 75) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 1.2f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int waveIdx = (int)(AttackTimer - 25) / 25; //0,1,2
                    if (waveIdx == 0) {
                        //第1波: 密集窄角高速扇形(直射)
                        int count = Main.expertMode ? 20 : 14;
                        float spread = MathHelper.ToRadians(60f);
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        for (int i = 0; i < count; i++) {
                            float angle = -spread / 2 + spread / (count - 1) * i;
                            IceProjectile(NPC.Center, dir.RotatedBy(angle) * 12f, NPC.damage / 4, 160, 0, 0f);
                        }
                    }
                    else if (waveIdx == 1) {
                        //第2波: 稀疏全周正弦波(Mode 1慢速)
                        int count = Main.expertMode ? 16 : 12;
                        for (int i = 0; i < count; i++) {
                            float angle = MathHelper.TwoPi / count * i;
                            IceProjectile(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f,
                                NPC.damage / 4, 200, 1, i * 0.5f); //不同相位错开
                        }
                    }
                    else {
                        //第3波: 斐波那契螺旋追踪(Mode 2延迟追踪)
                        FibonacciSpiral(NPC.Center, Main.expertMode ? 18 : 12, 5f, NPC.damage / 4, true, 2, 20f);
                    }
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(8, 8);
                    }
                }
            }

            // 同步蛇毒: DNA双螺旋扩展
            if (AttackTimer % 18 == 0 && AttackTimer > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpiralDNA(NPC.Center, 5, 9f, NPC.damage / 5, 0.2f);
            }

            if (AttackTimer > 90) TransitionTo(GetRandomPhase2Attack());
        }

        #endregion

        #region 三阶段：玄天武帝

        private void RunPhase3Drift(Player target) {
            //Lissajous漂移: a:b=√2:1产生非重复巡逻轨迹
            lissajousTime += 1f / 60f;
            float radius = 300f;
            float lissA = MathF.Sqrt(2f);
            Vector2 driftPos = target.Center + new Vector2(
                MathF.Sin(lissA * lissajousTime + 0.7f) * radius,
                MathF.Sin(lissajousTime) * radius * 0.3f - 250);
            //弹簧漂移: k=1低刚度 c=3 → 惯性拖尾感
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, driftPos, ref springVel, 1f, 3f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, dustType, 0, -1f, 100, default, 2f);
                d.noGravity = true;
            }

            // 玄天巡航: 正弦波冰碎(Mode 1) + 蛇形毒牙(Mode 1)
            if (PhaseTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float a = PhaseTimer * 0.18f;
                for (int arm = 0; arm < 3; arm++) {
                    float angle = a + arm * MathHelper.TwoPi / 3f;
                    IceProjectile(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f,
                        NPC.damage / 6, 160, 1, arm * 0.8f); //不同相位的正弦波
                }
            }
            if (PhaseTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 12f;
                VenomProjectile(NPC.Center, vel, NPC.damage / 6, 1, 0.18f); //蛇形路径
            }

            if (PhaseTimer > 75) TransitionTo(GetRandomPhase3Attack());
        }

        private void RunPhase3AbsoluteDefense(Player target) {
            //绝对防御: k=0.5极低刚度 c=12极高阻尼 → 几乎钉死
            Vector2 anchorPos = NPC.Center;
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, anchorPos, ref springVel, 0.5f, 12f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;
            absoluteDefenseActive = true;
            NPC.dontTakeDamage = true;

            //激活时冰霜脉冲
            if (AttackTimer == 1) {
                frostTargetIntensity = 0.6f;
                phaseFlash = 0.4f;
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.8f, Volume = 1.3f }, NPC.Center);
            }

            //六边形盾可视化
            shieldVisual = MathHelper.Lerp(shieldVisual, 1f, 0.08f);
            frostTargetIntensity = MathHelper.Max(frostTargetIntensity, 0.25f);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = globalTime * 4f + MathHelper.TwoPi / 8 * i;
                    float dist = 130f + MathF.Sin(globalTime * 6f) * 20f;
                    Vector2 shieldPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(shieldPos, 0, 0, DustID.Ice, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 4f;
                }
            }

            // 双型反击: 黄金角螺旋 + 蛇毒收束螺旋
            if (AttackTimer % 14 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FibonacciSpiral(NPC.Center, 14, 11f, NPC.damage / 4, true, 0, 0f);
            }
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                //收束螺旋围绕玩家(Mode 2)
                for (int i = -1; i <= 1; i++) {
                    Vector2 offset = new Vector2(i * 120f, -60f);
                    VenomProjectile(NPC.Center + offset, new Vector2(i * 5f, -3f), NPC.damage / 4, 2, NPC.target);
                }
            }

            if (AttackTimer > 100) {
                absoluteDefenseActive = false;
                NPC.dontTakeDamage = false;
                //盾碎裂闪光
                phaseFlash = 0.5f;
                frostTargetIntensity = 0.4f;
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitY, 10f, 6f, 25, 1500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        /// <summary>
        /// 绝对防御受击反射 — 近战攻击时发射反射冰锥
        /// </summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (absoluteDefenseActive && Main.netMode != NetmodeID.MultiplayerClient) {
                //受击方向反射冰锥
                Player attacker = Main.player[NPC.target];
                Vector2 reflectDir = (attacker.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = reflectDir.RotatedBy(i * MathHelper.ToRadians(15f)) * 5f;
                    IceProjectile(NPC.Center + reflectDir * 80f, vel, NPC.damage / 6, 300, 2, 120f); //Mode 2延迟追踪, 120帧后锁定
                }
                //盾闪光反馈
                shieldVisual = MathHelper.Min(shieldVisual + 0.3f, 1.5f);
            }
        }

        private void RunPhase3TidalCrush(Player target) {
            if (SubState == 0) {
                //蓄力制动
                Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 10f, 1f / 60f);
                NPC.velocity = nc - NPC.Center;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(150, 150), 0, 0, DustID.Water, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }

                // 蓄力时旋转冰臂
                if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float a = AttackTimer * 0.2f;
                    for (int arm = 0; arm < 2; arm++) {
                        float angle = a + arm * MathHelper.Pi;
                        IceProjectile(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f, NPC.damage / 6);
                    }
                }

                if (AttackTimer > 50) {
                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

                    // 4层双向水浪 + 蛇毒雨
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int wave = 0; wave < 4; wave++) {
                            for (int i = 0; i < 10; i++) {
                                float speed = 5f + i * 2f + wave * 2.5f;
                                WaterProjectile(NPC.Center, new Vector2(speed, -2f + wave * 0.8f), NPC.damage / 3);
                                WaterProjectile(NPC.Center, new Vector2(-speed, -2f + wave * 0.8f), NPC.damage / 3);
                            }
                        }
                        // 蛇毒雨: 抛物线毒液(Mode 3重力落下)
                        for (int i = 0; i < 10; i++) {
                            float x = target.Center.X + Main.rand.NextFloat(-500, 500);
                            Vector2 pos = new Vector2(x, target.Center.Y - 600);
                            VenomProjectile(pos, new Vector2(0, Main.rand.NextFloat(12f, 18f)), NPC.damage / 4, 3, 0f); //Mode 3抛物线
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitX, 18f, 10f, 35, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                //潮汐释放后弹簧制动
                Vector2 nc2 = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc2 - NPC.Center;
                if (AttackTimer > 50) TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        private void RunPhase3Blizzard(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            //弹簧高空悬停: k=2 c=5
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, hoverPos, ref springVel, 2f, 5f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            int interval = Main.expertMode ? 2 : 3;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                //瞄准预判冰雪: LeadTarget计算提前量
                int count = Main.expertMode ? 5 : 4;
                for (int i = 0; i < count; i++) {
                    float x = target.Center.X + Main.rand.NextFloat(-700, 700);
                    Vector2 pos = new Vector2(x, target.Center.Y - 700);
                    Vector2 aimDir = ACMUtils.LeadTarget(pos, target.Center, target.velocity, 16f);
                    Vector2 vel = aimDir * 16f + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), 0);
                    IceProjectile(pos, vel, NPC.damage / 4, 200, 0, 0f);
                }
            }

            // 双侧蛇毒: 收束螺旋(Mode 2围绕玩家)
            if (AttackTimer % 22 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 5; i++) {
                        Vector2 pos = target.Center + new Vector2(side * 650, -250 + i * 120);
                        VenomProjectile(pos, new Vector2(-side * 10f, 0), NPC.damage / 5, 2, NPC.target);
                    }
                }
            }

            if (AttackTimer > 150) TransitionTo(BossPhase.Phase3_Drift);
        }

        private void RunPhase3NorthStarJudgment(Player target) {
            if (SubState == 0) {
                //预兆: 弹簧飞到玩家正上方 k=2.5 c=4
                Vector2 abovePos = target.Center + new Vector2(0, -450);
                Vector2 ns = ACMUtils.SpringDamp2D(NPC.Center, abovePos, ref springVel, 2.5f, 4f, 1f / 60f);
                NPC.velocity = ns - NPC.Center;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(3f + AttackTimer * 0.03f, 3f + AttackTimer * 0.03f);

                //冰霜扭曲持续增强
                frostTargetIntensity = MathHelper.Lerp(0.1f, 0.7f, ACMUtils.Clamp01(AttackTimer / 80f));
                causticsTargetIntensity = MathHelper.Lerp(0.05f, 0.3f, ACMUtils.Clamp01(AttackTimer / 80f));

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 12; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(80, 400);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = Main.rand.Next(3) switch { 0 => DustID.Ice, 1 => DustID.Water, _ => DustID.CursedTorch };
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                    }
                }

                // 蓄力时轨道冰臂(Mode 3围绕Boss旋转)
                if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float a = AttackTimer * 0.15f;
                    for (int arm = 0; arm < 3; arm++) {
                        float angle = a + arm * MathHelper.TwoPi / 3f;
                        IceProjectile(NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 100f,
                            new Vector2(MathF.Cos(angle + MathHelper.PiOver2), MathF.Sin(angle + MathHelper.PiOver2)) * 3f,
                            NPC.damage / 5, 200, 3, NPC.whoAmI);
                    }
                }

                if (AttackTimer > 80) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -1f, Volume = 2f }, NPC.Center);

                    //释放瞬间: 全屏冰霜脉冲 + 闪白
                    frostTargetIntensity = 0.9f;
                    phaseFlash = 0.8f;
                    causticsTargetIntensity = 0.5f;

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    // 北辰星柱: 7组北斗轨道冰柱(Mode 3悬浮后依次释放)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        //7道北斗星光: 轨道冰柱(Mode 3)形成星座图案
                        for (int star = 0; star < 7; star++) {
                            float starAngle = MathHelper.PiOver2 - MathHelper.ToRadians(45f) + MathHelper.ToRadians(90f) / 6f * star;
                            for (int i = 0; i < 6; i++) {
                                float dist = 80f + i * 50f;
                                Vector2 orbPos = NPC.Center + new Vector2(MathF.Cos(starAngle), MathF.Sin(starAngle)) * dist;
                                Vector2 tangent = new Vector2(-MathF.Sin(starAngle), MathF.Cos(starAngle)) * 3f;
                                IceProjectile(orbPos, tangent, NPC.damage / 3, 200, 3, NPC.whoAmI); //轨道围绕Boss
                            }
                        }
                        //8方向黄金角螺旋爆发
                        FibonacciSpiral(NPC.Center, 20, 10f, NPC.damage / 3, true, 0, 0f);
                        // DNA双螺旋蛇毒扩展
                        SpiralDNA(NPC.Center, 16, 7f, NPC.damage / 3, 0.18f);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                //北辰释放后弹簧制动
                Vector2 nc = ACMUtils.SpringDamp2D(NPC.Center, NPC.Center, ref springVel, 0.5f, 8f, 1f / 60f);
                NPC.velocity = nc - NPC.Center;

                // 爆发后预判追踪压制
                if (AttackTimer % 10 == 0 && AttackTimer < 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                    LeadTargetSalvo(NPC.Center, target, 3, 13f, NPC.damage / 5, 0.08f);
                }

                if (AttackTimer > 60) TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        private void RunPhase3YinYangBalance(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 200f, -300);
            //弹簧跟踪悬停: k=2.5 c=5
            Vector2 newCenter = ACMUtils.SpringDamp2D(NPC.Center, hoverPos, ref springVel, 2.5f, 5f, 1f / 60f);
            NPC.velocity = newCenter - NPC.Center;

            // 同时双型攻击: 预判冰柱齐射 + 蛇形毒牙
            int interval = Main.expertMode ? 5 : 8;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                //预判齐射(Mode 2延迟追踪)
                LeadTargetSalvo(NPC.Center, target, 5, 15f, NPC.damage / 4, 0.06f);
                // 蛇形毒牙窄扇(Mode 1)
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 venomVel = dir.RotatedBy(i * MathHelper.ToRadians(25)) * 13f;
                    VenomProjectile(NPC.Center, venomVel, NPC.damage / 4, 1, 0.16f);
                }
            }

            // 双型环射: 黄金角螺旋 + DNA毒液
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FibonacciSpiral(NPC.Center, 10, 7f, NPC.damage / 5, true, 1, AttackTimer * 0.1f); //正弦波螺旋
                SpiralDNA(NPC.Center, 4, 9f, NPC.damage / 5, 0.22f);
            }

            if (AttackTimer > 150) TransitionTo(BossPhase.Phase3_Drift);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool isSpinning = (Phase == BossPhase.Phase1_SpinCharge && SubState == 1) ||
                              (Phase == BossPhase.Phase2_DualAssault && SubState == 1);

            SpriteEffects effects = SpriteEffects.None;
            float drawRotation = NPC.rotation;

            if (!isSpinning) {
                bool facingRight = NPC.spriteDirection == 1;
                effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRotation = facingRight ? NPC.rotation : -NPC.rotation;
            }

            Vector2 drawPos = NPC.Center - screenPos;

            //层1: 冰气光环 — SoftGlow大尺寸，颜色随阶段变化
            DrawIceAura(spriteBatch, drawPos);

            //层2: 旋转冰晶环 — BlankStar围绕Boss旋转
            DrawOrbitingCrystals(spriteBatch, drawPos);

            //层3: 绝对防御六边形盾
            if (shieldVisual > 0.01f)
                DrawHexShield(spriteBatch, drawPos);

            //层4: 增强残影 — 颜色区分阶段
            DrawEnhancedTrail(spriteBatch, texture, frame, origin, screenPos, drawRotation, effects);

            //层5: 蛇头视觉(二阶段起)
            if (snakeHeadExtend > 0.01f)
                DrawSnakeHead(spriteBatch, drawPos);

            //层5b: Verlet蛇身ribbon(二阶段起)
            if (snakeChain != null && snakeHeadExtend > 0.1f)
                DrawSnakeBody(spriteBatch, screenPos);

            //层5c: 落地冲击波环
            if (shockwaveAlpha > 0.01f)
                DrawShockwaveRing(spriteBatch, screenPos);

            //主体绘制
            spriteBatch.Draw(texture, drawPos, frame, drawColor, drawRotation, origin, NPC.scale, effects, 0f);

            //层6: 阶段转换闪光
            if (phaseFlash > 0.01f)
                DrawPhaseFlash(spriteBatch, drawPos);

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //着色器后处理在PostDraw中绘制，确保覆盖所有内容
            if (Main.dedServ) return;
            DrawShaderEffects(spriteBatch);
        }

        #endregion

        #region 着色器管理

        private static void EnsureNoiseTexture() {
            if (noiseTexture == null || noiseTexture.IsDisposed)
                noiseTexture = GenerateNoiseTexture(Main.graphics.GraphicsDevice);
        }

        private static Effect GetFrostEffect() {
            frostDistortionRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuFrostDistortion",
                AssetRequestMode.ImmediateLoad);
            return frostDistortionRef?.Value;
        }

        private static Effect GetCausticsEffect() {
            causticsRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/XuanwuCaustics",
                AssetRequestMode.ImmediateLoad);
            return causticsRef?.Value;
        }

        private void DrawShaderEffects(SpriteBatch sb) {
            EnsureNoiseTexture();
            if (noiseTexture == null) return;

            Vector2 screenCenter = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            //冰霜扭曲
            if (frostIntensity > 0.01f) {
                Effect frost = GetFrostEffect();
                if (frost != null) {
                    frost.Parameters["uTime"]?.SetValue(globalTime);
                    frost.Parameters["uCenter"]?.SetValue(screenCenter);
                    frost.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(frostIntensity, 0f, 1f));
                    frost.Parameters["uFrostRadius"]?.SetValue(0.5f);
                    frost.Parameters["uCrystalScale"]?.SetValue(IsPhase3 ? 5f : 3.5f);
                    frost.Parameters["uAspect"]?.SetValue(aspect);

                    Main.graphics.GraphicsDevice.Textures[1] = noiseTexture;
                    Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                    sb.End();
                    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, frost, Matrix.Identity);

                    sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);

                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null,
                        Main.GameViewMatrix.TransformationMatrix);
                }
            }

            //水面焦散
            if (causticsIntensity > 0.01f) {
                Effect caustics = GetCausticsEffect();
                if (caustics != null) {
                    caustics.Parameters["uTime"]?.SetValue(globalTime);
                    caustics.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(causticsIntensity, 0f, 1f));
                    caustics.Parameters["uWaveSpeed"]?.SetValue(IsPhase3 ? 1.5f : 1f);
                    caustics.Parameters["uCausticsScale"]?.SetValue(3f);

                    Vector4 tint = IsPhase3
                        ? new Vector4(0.4f, 0.7f, 0.95f, 0.5f)
                        : new Vector4(0.2f, 0.5f, 0.85f, 0.3f);
                    caustics.Parameters["uColorTint"]?.SetValue(tint);

                    Main.graphics.GraphicsDevice.Textures[1] = noiseTexture;
                    Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                    sb.End();
                    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, caustics, Matrix.Identity);

                    sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);

                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null,
                        Main.GameViewMatrix.TransformationMatrix);
                }
            }
        }

        #endregion

        #region 视觉层绘制

        private void DrawIceAura(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 go = glow.Size() / 2f;

            //阶段颜色: P1冰蓝, P2冰蓝+毒绿, P3极光白
            Color auraColor;
            float auraScale;
            if (IsPhase3) {
                float pulse = MathF.Sin(globalTime * 2f) * 0.15f + 0.85f;
                auraColor = new Color(180, 220, 255, 0) * (0.35f * pulse * glowIntensity);
                auraScale = 3.5f + MathF.Sin(globalTime * 1.5f) * 0.3f;
            }
            else if (IsPhase2) {
                float t = MathF.Sin(globalTime * 1.2f) * 0.5f + 0.5f;
                Color ice = new Color(80, 160, 240, 0);
                Color venom = new Color(60, 200, 80, 0);
                auraColor = Color.Lerp(ice, venom, t) * (0.25f * glowIntensity);
                auraScale = 2.8f;
            }
            else {
                auraColor = new Color(60, 140, 220, 0) * (0.2f * glowIntensity);
                auraScale = 2.2f + MathF.Sin(globalTime * 0.8f) * 0.15f;
            }

            sb.Draw(glow, drawPos, null, auraColor, 0f, go, auraScale, SpriteEffects.None, 0f);

            //内层高亮
            Color coreColor = new Color(150, 210, 255, 0) * (0.15f * glowIntensity);
            sb.Draw(glow, drawPos, null, coreColor, 0f, go, auraScale * 0.5f, SpriteEffects.None, 0f);
        }

        private void DrawOrbitingCrystals(SpriteBatch sb, Vector2 drawPos) {
            Texture2D star = ACMAsset.BlankStar;
            if (star == null) return;
            Vector2 so = star.Size() / 2f;

            int count = IsPhase3 ? 8 : (IsPhase2 ? 6 : 4);
            float radius = IsPhase3 ? 160f : (IsPhase2 ? 140f : 120f);
            float speed = IsPhase3 ? 2f : (IsPhase2 ? 1.5f : 1f);
            float crystalScale = IsPhase3 ? 0.15f : 0.12f;

            for (int i = 0; i < count; i++) {
                float angle = globalTime * speed + MathHelper.TwoPi / count * i;
                //椭圆化
                Vector2 offset = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.4f);
                Vector2 crystalPos = drawPos + offset;

                float pulse = MathF.Sin(globalTime * 3f + i * 1.5f) * 0.3f + 0.7f;
                Color cc = new Color(120, 200, 255, 0) * (pulse * 0.5f);
                float rot = -angle * 0.5f;

                sb.Draw(star, crystalPos, null, cc, rot, so, crystalScale, SpriteEffects.None, 0f);

                //每个冰晶下方一个小型SoftGlow光点
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    Vector2 go = glow.Size() / 2f;
                    Color gc = new Color(80, 160, 240, 0) * (pulse * 0.2f);
                    sb.Draw(glow, crystalPos, null, gc, 0f, go, 0.4f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawHexShield(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = ACMAsset.SoftGlow;
            Texture2D star = ACMAsset.BlankStar;
            if (glow == null || star == null) return;
            Vector2 go = glow.Size() / 2f;
            Vector2 so = star.Size() / 2f;

            float alpha = shieldVisual;
            float shieldRadius = 160f + MathF.Sin(globalTime * 3f) * 10f;

            //六个顶点的六边形盾
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi / 6f * i + globalTime * 0.5f;
                Vector2 point = drawPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * shieldRadius;

                //顶点SoftGlow
                Color pointColor = new Color(100, 200, 255, 0) * (alpha * 0.6f);
                sb.Draw(glow, point, null, pointColor, 0f, go, 0.6f, SpriteEffects.None, 0f);

                //顶点BlankStar旋转
                Color starColor = new Color(180, 240, 255, 0) * (alpha * 0.4f);
                sb.Draw(star, point, null, starColor, globalTime * 2f + i, so, 0.1f, SpriteEffects.None, 0f);

                //边: 用多个小SoftGlow连接两个顶点
                float nextAngle = MathHelper.TwoPi / 6f * ((i + 1) % 6) + globalTime * 0.5f;
                Vector2 nextPoint = drawPos + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * shieldRadius;
                int segments = 5;
                for (int s = 1; s < segments; s++) {
                    float t = (float)s / segments;
                    Vector2 edgePos = Vector2.Lerp(point, nextPoint, t);
                    float edgePulse = MathF.Sin(globalTime * 4f + s + i * 2f) * 0.3f + 0.7f;
                    Color edgeColor = new Color(80, 180, 255, 0) * (alpha * 0.3f * edgePulse);
                    sb.Draw(glow, edgePos, null, edgeColor, 0f, go, 0.25f, SpriteEffects.None, 0f);
                }
            }

            //中心护盾辉光
            Color centerShield = new Color(60, 150, 240, 0) * (alpha * 0.15f);
            sb.Draw(glow, drawPos, null, centerShield, 0f, go, shieldRadius / (glow.Width / 2f), SpriteEffects.None, 0f);
        }

        private void DrawEnhancedTrail(SpriteBatch sb, Texture2D texture, Rectangle frame, Vector2 origin,
            Vector2 screenPos, float drawRotation, SpriteEffects effects) {
            int trailLen = NPCID.Sets.TrailCacheLength[Type];
            Texture2D glow = ACMAsset.SoftGlow;

            for (int i = trailLen - 1; i > 0; i--) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float progress = (float)i / trailLen;
                float alpha = 0.35f * (1f - progress);

                //阶段颜色区分
                Color trailColor;
                if (IsPhase3) {
                    trailColor = Color.Lerp(new Color(180, 230, 255), new Color(100, 180, 255), progress) * alpha;
                }
                else if (IsPhase2) {
                    //蓝绿交替
                    float t = MathF.Sin(globalTime + i) * 0.5f + 0.5f;
                    Color ice = new Color(80, 160, 255);
                    Color venom = new Color(60, 180, 100);
                    trailColor = Color.Lerp(ice, venom, t) * alpha;
                }
                else {
                    trailColor = new Color(60, 120, 220) * alpha;
                }

                float trailScale = NPC.scale * (1f - progress * 0.08f);
                sb.Draw(texture, trailPos, frame, trailColor, drawRotation, origin, trailScale, effects, 0f);

                //残影外发光
                if (glow != null && i % 2 == 0) {
                    Vector2 go = glow.Size() / 2f;
                    Color glowC = trailColor with { A = 0 } * 0.3f;
                    sb.Draw(glow, trailPos, null, glowC, 0f, go, 1.2f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawSnakeHead(SpriteBatch sb, Vector2 drawPos) {
            Texture2D shot = ACMAsset.LightShot;
            Texture2D glow = ACMAsset.SoftGlow;
            if (shot == null || glow == null) return;

            float extend = snakeHeadExtend;
            Vector2 snakeOffset = new(NPC.spriteDirection * 30f, -90f - extend * 40f);
            Vector2 snakePos = drawPos + snakeOffset;

            //蛇头朝向: 正在蛇击攻击时朝向玩家，否则朝上
            float snakeRot;
            if (Phase == BossPhase.Phase2_SnakeStrike && SubState == 0) {
                Player tgt = Main.player[NPC.target];
                snakeRot = (tgt.Center - NPC.Center).ToRotation();
            }
            else {
                snakeRot = -MathHelper.PiOver2 + MathF.Sin(globalTime * 2f) * 0.15f;
            }

            Vector2 shotOrigin = shot.Size() / 2f;
            Vector2 glowOrigin = glow.Size() / 2f;

            //蛇头主体 — LightShot (毒绿)
            Color headColor = new Color(80, 220, 60, 0) * (0.7f * extend);
            sb.Draw(shot, snakePos, null, headColor, snakeRot, shotOrigin, 0.6f * extend, SpriteEffects.None, 0f);

            //蛇眼光点
            Color eyeColor = new Color(200, 255, 80, 0) * (0.8f * extend);
            float eyePulse = MathF.Sin(globalTime * 5f) * 0.15f + 0.85f;
            sb.Draw(glow, snakePos, null, eyeColor * eyePulse, 0f, glowOrigin, 0.25f * extend, SpriteEffects.None, 0f);

            //蛇颈能量拖尾 — GlaciateWave
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave != null) {
                Vector2 neckPos = drawPos + new Vector2(NPC.spriteDirection * 20f, -60f);
                Vector2 wo = wave.Size() / 2f;
                Color neckColor = new Color(50, 180, 50, 0) * (0.3f * extend);
                float neckRot = snakeRot + MathHelper.PiOver2;
                sb.Draw(wave, neckPos, null, neckColor, neckRot, wo, new Vector2(0.15f, 0.3f) * extend, SpriteEffects.None, 0f);
            }
        }

        //冲击波环: TriangleStrip扩展圆环
        private void DrawShockwaveRing(SpriteBatch sb, Vector2 screenPos) {
            const int segments = 48;
            float innerR = MathF.Max(shockwaveRadius - 15f * shockwaveAlpha, 0f);
            float outerR = shockwaveRadius + 15f * shockwaveAlpha;
            if (outerR < 1f) return;

            var verts = new ColoredVertex[segments * 2 + 2];
            Vector2 center = shockwaveCenter - screenPos;
            Color ringColor = new Color(140, 200, 255, 0) * shockwaveAlpha;
            Color edgeColor = new Color(80, 160, 240, 0) * (shockwaveAlpha * 0.4f);

            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.TwoPi / segments * i;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);
                float u = (float)i / segments;

                Vector2 outerPos = center + new Vector2(cos, sin) * outerR;
                Vector2 innerPos = center + new Vector2(cos, sin) * innerR;

                verts[i * 2] = new ColoredVertex(outerPos, new Vector3(u, 0f, 0f), edgeColor);
                verts[i * 2 + 1] = new ColoredVertex(innerPos, new Vector3(u, 1f, 0f), ringColor);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            Texture2D ringTex = ACMAsset.GlaciateWave;
            if (ringTex != null) {
                var gd = Main.graphics.GraphicsDevice;
                gd.Textures[0] = ringTex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //Verlet蛇身ribbon: 双层顶点绘制
        private void DrawSnakeBody(SpriteBatch sb, Vector2 screenPos) {
            if (snakeChain == null || snakeChain.Length < 2) return;

            //将世界坐标转为屏幕坐标
            var posArr = new Vector2[snakeChain.Length];
            for (int i = 0; i < snakeChain.Length; i++)
                posArr[i] = snakeChain[i] - screenPos;

            float extend = snakeHeadExtend;
            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;

            //外层: 毒绿宽ribbon
            var outerVerts = ACMUtils.BuildRibbonStrip(
                posArr,
                p => MathHelper.Lerp(12f, 3f, p) * extend,
                p => {
                    float pulse = MathF.Sin(time * 4f + p * 8f) * 0.1f + 0.9f;
                    float alpha = (1f - p * 0.7f) * extend * pulse;
                    Color c = new Color(50, 200, 80) * alpha;
                    c.A = 0;
                    return c;
                },
                uvScroll: time * 0.6f,
                subdivisions: 3
            );

            if (outerVerts.Length >= 4) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                //应用ribbon着色器
                Effect shader = null;
                if (ModContent.HasAsset("AncientChineseMythology/Effects/XuanwuTrailRibbon")) {
                    shader = ModContent.Request<Effect>("AncientChineseMythology/Effects/XuanwuTrailRibbon", AssetRequestMode.ImmediateLoad).Value;
                }
                if (shader != null) {
                    shader.Parameters["uTime"]?.SetValue(time);
                    shader.Parameters["uGlowWidth"]?.SetValue(0.3f);
                    shader.Parameters["uAlphaFade"]?.SetValue(0.9f);
                    shader.Parameters["uScrollSpeed"]?.SetValue(0.8f);
                    shader.Parameters["uPulseRate"]?.SetValue(5f);
                    shader.Parameters["uPulseStrength"]?.SetValue(0.2f);
                    shader.Parameters["uGlowColor"]?.SetValue(new Vector4(0.2f, 0.9f, 0.3f, 0.7f));
                    shader.Parameters["uCoreColor"]?.SetValue(new Vector4(0.6f, 1f, 0.4f, 0.4f));
                    shader.CurrentTechnique.Passes[0].Apply();
                }

                var gd = Main.graphics.GraphicsDevice;
                Texture2D ribbonTex = ACMAsset.EmberShards;
                if (ribbonTex != null) gd.Textures[0] = ribbonTex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, outerVerts, 0, outerVerts.Length - 2);

                //内层: 亮黄绿窄ribbon
                var innerVerts = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => MathHelper.Lerp(5f, 0.8f, p) * extend,
                    p => {
                        float alpha = (1f - p) * 0.85f * extend;
                        Color c = new Color(180, 255, 100) * alpha;
                        c.A = 0;
                        return c;
                    },
                    uvScroll: time * 1.2f,
                    subdivisions: 2
                );
                if (innerVerts.Length >= 4) {
                    Texture2D glowTex = ACMAsset.SoftGlow;
                    if (glowTex != null) gd.Textures[0] = glowTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts, 0, innerVerts.Length - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        private void DrawPhaseFlash(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 go = glow.Size() / 2f;

            float flashScale = phaseFlash * 15f;
            Color flashColor = new Color(200, 240, 255, 0) * (phaseFlash * 0.8f);
            sb.Draw(glow, drawPos, null, flashColor, 0f, go, flashScale, SpriteEffects.None, 0f);
        }

        #endregion

        #region 着色器噪声纹理生成

        private static Texture2D GenerateNoiseTexture(GraphicsDevice device, int size = 256) {
            Color[] pixels = new Color[size * size];
            byte[][] channels = new byte[3][];

            for (int c = 0; c < 3; c++) {
                channels[c] = new byte[size * size];
                float[,] noise = GenerateTileableFBM(size, octaves: 5, seed: 77 + c * 131);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        channels[c][y * size + x] = (byte)(noise[x, y] * 255);
            }

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(channels[0][i], channels[1][i], channels[2][i], (byte)255);

            Texture2D tex = new(device, size, size, false, SurfaceFormat.Color);
            tex.SetData(pixels);
            return tex;
        }

        private static float[,] GenerateTileableFBM(int size, int octaves, int seed) {
            float[,] result = new float[size, size];
            Random rng = new(seed);
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int oct = 0; oct < octaves; oct++) {
                int grid = Math.Max(2, (int)(4 * frequency));
                float[] lattice = new float[(grid + 1) * (grid + 1)];
                for (int i = 0; i < lattice.Length; i++)
                    lattice[i] = (float)rng.NextDouble();

                for (int i = 0; i <= grid; i++) {
                    lattice[i * (grid + 1) + grid] = lattice[i * (grid + 1)];
                    lattice[grid * (grid + 1) + i] = lattice[i];
                }
                lattice[grid * (grid + 1) + grid] = lattice[0];

                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        float fx = (float)x / size * grid;
                        float fy = (float)y / size * grid;
                        int ix = Math.Min((int)fx, grid - 1);
                        int iy = Math.Min((int)fy, grid - 1);
                        float tx = fx - ix;
                        float ty = fy - iy;
                        tx = tx * tx * (3 - 2 * tx);
                        ty = ty * ty * (3 - 2 * ty);
                        float v00 = lattice[iy * (grid + 1) + ix];
                        float v10 = lattice[iy * (grid + 1) + ix + 1];
                        float v01 = lattice[(iy + 1) * (grid + 1) + ix];
                        float v11 = lattice[(iy + 1) * (grid + 1) + ix + 1];
                        float vx0 = v00 + (v10 - v00) * tx;
                        float vx1 = v01 + (v11 - v01) * tx;
                        result[x, y] += (vx0 + (vx1 - vx0) * ty) * amplitude;
                    }
                }
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            if (maxValue > 0) {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        result[x, y] /= maxValue;
            }
            return result;
        }

        #endregion
    }
}

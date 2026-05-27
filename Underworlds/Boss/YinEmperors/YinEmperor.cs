using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.YinEmperors.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 冥府超级Boss
    /// 人形Boss，类似月球领主，主纹理为三帧循环动画
    /// 与觉醒-冥府尽头-幽冥龙同级
    /// </summary>
    [AutoloadBossHead]
    public class YinEmperor : ModNPC
    {
        #region 常量

        private const int MaxFrames = 3;
        private const int FrameSpeed = 8;
        private const int IntroRiseDuration = 180;
        private const int IntroPauseDuration = 60;
        private const int IntroRoarDuration = 40;
        private const int IntroLightningCount = 6;
        private const float IntroRiseDistance = 900f;
        private const float HoverHeight = 280f;

        #endregion

        #region AI阶段系统

        private enum AIState
        {
            Intro,              // 出场演出
            ImperialHover,      // 帝冥悬浮（基础移动+弹幕）
            DragonSweep,        // 龙气横扫（冲刺攻击）
            NetherDecree,       // 冥谕降罚（天降雷柱+冥眼列阵激光）
            SoulSeal,           // 镇魂封印（冥眼环绕冲锋限制走位）
            ImperialWrath       // 帝怒（狂暴连击+冥眼守卫环绕）
        }

        private AIState CurrentState {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float PhaseTimer => ref NPC.ai[1];
        private ref float AttackTimer => ref NPC.ai[2];
        private ref float SpecialCounter => ref NPC.ai[3];

        #endregion

        #region 状态变量

        private int seed = -1;
        private Random random;
        private int frameCounter;
        private int currentFrame;

        // 出场演出
        private float introProgress;
        private bool introRoarDone;
        private bool introLightningDone;
        private float introPillarAlpha;
        private float introShakeIntensity;

        // 视觉效果
        private float pulsePhase;
        private float auraRotation;
        private float auraIntensity;
        private float hoverOffset;
        private float[] energyWaveRadius = new float[3];
        private float[] energyWaveAlpha = new float[3];

        // 战斗参数
        private int dashCount;
        private Vector2 dashTarget;
        private int sweepDirection;

        // 法环
        private float ringRotation;
        private float ringScale;
        private float ringAlpha;

        #endregion

        #region 目标获取

        private Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = MaxFrames;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 160;
            NPC.damage = 220;
            NPC.defense = 95;
            NPC.lifeMax = 12000000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 500000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 20f;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.8f);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YinImperialSeal>()));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YinEssence>(), 1, 18, 24));
            npcLoot.Add(ItemDropRule.Common(ItemID.SuperHealingPotion, 1, 25, 40));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<FengduImperialCrown>(),
                ModContent.ItemType<SoulBannerUnderworldRelic>(),
                ModContent.ItemType<GhostGateKey>()
            ));
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override bool CheckActive() => false;

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            CurrentState = AIState.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            introProgress = 0f;
            introRoarDone = false;
            introLightningDone = false;
            introPillarAlpha = 0f;
            auraIntensity = 0f;

            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)CurrentState);
            writer.Write(introProgress);
            writer.Write(pulsePhase);
            writer.Write(dashCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            CurrentState = (AIState)reader.ReadInt32();
            introProgress = reader.ReadSingle();
            pulsePhase = reader.ReadSingle();
            dashCount = reader.ReadInt32();
            random ??= new Random(seed);
        }

        #region 帧动画

        public override void FindFrame(int frameHeight) {
            frameCounter++;
            if (frameCounter >= FrameSpeed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= MaxFrames)
                    currentFrame = 0;
            }
            NPC.frame.Y = currentFrame * frameHeight;
        }

        #endregion

        #region 主AI

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            random ??= new Random(seed);

            // 视觉效果更新
            pulsePhase += 0.06f;
            auraRotation += 0.015f;
            ringRotation += 0.008f;
            hoverOffset = MathF.Sin(pulsePhase * 0.4f) * 8f;
            UpdateEnergyWaves();

            // 法环逐渐显现（出场后）
            if (CurrentState != AIState.Intro) {
                ringScale = MathHelper.Lerp(ringScale, 2.5f, 0.01f);
                ringAlpha = MathHelper.Lerp(ringAlpha, 0.7f, 0.015f);
            }

            // 目标验证
            NPC.TargetClosest();
            Player target = Target;
            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.5f;
                NPC.alpha += 3;
                if (NPC.alpha > 255 || NPC.timeLeft < 10) {
                    NPC.active = false;
                }
                return;
            }

            // 光照
            float lightIntensity = 0.8f + auraIntensity * 0.4f;
            Lighting.AddLight(NPC.Center, YinEmperorHelper.ImperialGold.ToVector3() * lightIntensity * 0.4f);
            Lighting.AddLight(NPC.Center, YinEmperorHelper.AbyssPurple.ToVector3() * lightIntensity * 0.3f);

            PhaseTimer++;
            AttackTimer++;

            // 状态机
            switch (CurrentState) {
                case AIState.Intro:
                    RunIntro(target);
                    break;
                case AIState.ImperialHover:
                    RunImperialHover(target);
                    break;
                case AIState.DragonSweep:
                    RunDragonSweep(target);
                    break;
                case AIState.NetherDecree:
                    RunNetherDecree(target);
                    break;
                case AIState.SoulSeal:
                    RunSoulSeal(target);
                    break;
                case AIState.ImperialWrath:
                    RunImperialWrath(target);
                    break;
            }

            // 环绕粒子（非出场阶段）
            if (CurrentState != AIState.Intro) {
                CreateAmbientParticles();
            }
        }

        #endregion

        #region 出场演出

        /// <summary>
        /// 震撼出场演出：
        /// 1. 天地变色，冥雷响彻
        /// 2. 地底缓缓升起，帝冥金光柱冲天
        /// 3. 符文碎裂爆发，帝王显现
        /// 4. 龙气冲击波扩散，屏幕震动
        /// </summary>
        private void RunIntro(Player target) {
            int totalIntroDuration = IntroRiseDuration + IntroPauseDuration + IntroRoarDuration;

            // === 第一阶段：缓缓升起 (0 ~ IntroRiseDuration) ===
            if (PhaseTimer <= IntroRiseDuration) {
                float riseT = MathHelper.Clamp(PhaseTimer / IntroRiseDuration, 0f, 1f);
                introProgress = ACMUtils.SineInOut(riseT);

                Vector2 startPos = target.Center + new Vector2(0, IntroRiseDistance);
                Vector2 endPos = target.Center + new Vector2(0, -HoverHeight);
                Vector2 desired = Vector2.Lerp(startPos, endPos, introProgress);

                NPC.Center += (desired - NPC.Center) * 0.08f;
                NPC.velocity *= 0.85f;

                // 半透明逐渐显现
                NPC.alpha = (int)(255 * (1f - introProgress * 0.8f));

                // 光柱强度随升起增加
                introPillarAlpha = introProgress * 0.8f;

                // 上升过程中的帝冥漩涡粒子
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 1.5f);
                }

                // 上升过程中释放符文碎片
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 8 == 0) {
                    for (int i = 0; i < 2; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 80f + Main.rand.NextFloat(40f);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(dustPos, dustType);
                        d.noGravity = true;
                        d.scale = 1.5f + introProgress;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                        d.alpha = 80;
                    }
                }

                // 冥雷在升起过程中逐步释放
                if (!introLightningDone && PhaseTimer > 40 && PhaseTimer % 25 == 0 && Main.netMode != NetmodeID.Server) {
                    float lightningX = NPC.Center.X + Main.rand.NextFloat(-400f, 400f);
                    Vector2 lightningTop = new Vector2(lightningX, NPC.Center.Y - 600f);
                    Vector2 lightningBottom = new Vector2(lightningX + Main.rand.NextFloat(-60, 60), NPC.Center.Y + 200f);
                    YinEmperorHelper.CreateNetherLightningPillar(lightningTop, lightningBottom, 0.8f);

                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 0.8f }, new Vector2(lightningX, NPC.Center.Y));
                }

                if (PhaseTimer == IntroRiseDuration) {
                    introLightningDone = true;
                }

                // 缓慢增加光环强度
                auraIntensity = MathHelper.Lerp(auraIntensity, introProgress * 0.6f, 0.02f);
            }
            // === 第二阶段：短暂停顿蓄力 (IntroRiseDuration ~ IntroRiseDuration + IntroPauseDuration) ===
            else if (PhaseTimer <= IntroRiseDuration + IntroPauseDuration) {
                float pauseT = (PhaseTimer - IntroRiseDuration) / IntroPauseDuration;

                // 停在目标上方
                Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
                NPC.Center += (hoverPos - NPC.Center) * 0.05f;
                NPC.velocity *= 0.9f;
                NPC.alpha = (int)(255 * 0.2f * (1f - pauseT));

                // 蓄力：符文能量向身体汇聚
                if (Main.netMode != NetmodeID.Server) {
                    float chargeRadius = 200f * (1f - pauseT);
                    int chargeCount = (int)(6 * pauseT) + 2;

                    for (int i = 0; i < chargeCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chargeRadius;
                        Vector2 dustVel = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + pauseT * 8f);

                        int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(dustPos, dustType);
                        d.noGravity = true;
                        d.scale = 1.8f + pauseT;
                        d.velocity = dustVel;
                    }
                }

                // 光柱逐渐变亮
                introPillarAlpha = 0.8f + pauseT * 0.2f;
                auraIntensity = MathHelper.Lerp(auraIntensity, 0.8f, 0.03f);

                // 蓄力期间发出持续低鸣
                if (PhaseTimer == IntroRiseDuration + 1) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.8f, Volume = 0.6f }, NPC.Center);
                }
            }
            // === 第三阶段：帝王咆哮 + 龙气爆发 ===
            else if (PhaseTimer <= totalIntroDuration) {
                float roarT = (PhaseTimer - IntroRiseDuration - IntroPauseDuration) / (float)IntroRoarDuration;

                // 完全显现
                NPC.alpha = 0;

                // 停在位置上
                Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
                NPC.Center += (hoverPos - NPC.Center) * 0.03f;
                NPC.velocity *= 0.95f;

                // 帝王咆哮
                if (!introRoarDone) {
                    introRoarDone = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.8f }, NPC.Center);

                    // 龙气大爆发
                    YinEmperorHelper.CreateImperialVortex(NPC.Center, 250f, 2f, 80);
                    YinEmperorHelper.CreateDragonBurst(NPC.Center, 200f, 5, 24);
                    YinEmperorHelper.CreateTalismanBurst(NPC.Center, 300f, 40);

                    // 触发多重能量波
                    for (int i = 0; i < 3; i++) {
                        TriggerEnergyWave();
                    }

                    // 屏幕震动
                    Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(25, 80);

                    // 屏幕闪烁
                    YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 1.2f);

                    // 六道冥雷同时劈下
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < IntroLightningCount; i++) {
                            float angle = MathHelper.TwoPi * i / IntroLightningCount;
                            float dist = 300f + Main.rand.NextFloat(100f);
                            Vector2 strikePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                            Vector2 lightningTop = strikePos - new Vector2(0, 800f);
                            YinEmperorHelper.CreateNetherLightningPillar(lightningTop, strikePos, 1.2f);
                        }
                    }
                }

                // 爆发后的余波震颤
                introShakeIntensity = (1f - roarT) * 8f;
                if (introShakeIntensity > 0.5f && Main.netMode != NetmodeID.Server) {
                    NPC.Center += Main.rand.NextVector2Circular(introShakeIntensity, introShakeIntensity);
                }

                // 光柱消退
                introPillarAlpha = 1f - roarT * 0.8f;
                auraIntensity = MathHelper.Lerp(auraIntensity, 1f, 0.05f);

                // 大量粒子飞散
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < (int)(8 * (1f - roarT)); i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(18f, 18f) * (1f - roarT * 0.5f);
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(NPC.Center, dustType);
                        d.noGravity = true;
                        d.scale = 2.5f * (1f - roarT * 0.5f);
                        d.velocity = vel;
                    }
                }
            }
            // === 演出结束，进入战斗 ===
            else {
                introPillarAlpha = 0f;
                auraIntensity = 1f;
                TransitionTo(AIState.ImperialHover);
            }
        }

        #endregion

        #region 阶段1 AI行为

        /// <summary>
        /// 帝冥悬浮 - 基础移动+弹幕攻击
        /// 在玩家上方悬浮，释放帝冥弹幕
        /// </summary>
        private void RunImperialHover(Player target) {
            // 帝王式悬浮移动
            float swayX = MathF.Sin(PhaseTimer * 0.02f) * 180f;
            Vector2 hoverPos = target.Center + new Vector2(swayX, -HoverHeight + hoverOffset);
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.05f, 0.06f);

            // 帝冥弹幕：扇形金紫能量弹
            if (AttackTimer % 80 == 0) {
                ShootImperialBolts(target);
            }

            // 地面符文攻击
            if (AttackTimer % 140 == 70) {
                ShootGroundSeals(target);
            }

            // 选择下一阶段
            if (PhaseTimer > 360) {
                ChooseNextPhase1State();
            }
        }

        /// <summary>
        /// 龙气横扫 - 帝王级冲刺攻击
        /// 蓄力后向玩家方向高速冲刺，留下龙气尾迹
        /// </summary>
        private void RunDragonSweep(Player target) {
            if (PhaseTimer <= 40) {
                // 蓄力阶段
                NPC.velocity *= 0.9f;

                if (PhaseTimer == 20) {
                    dashTarget = target.Center + target.velocity * 20f;
                    sweepDirection = target.Center.X > NPC.Center.X ? 1 : -1;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    YinEmperorHelper.CreateImperialVortex(NPC.Center, 80f, 0.8f, 25);
                }

                // 蓄力粒子汇聚
                if (PhaseTimer > 20 && Main.netMode != NetmodeID.Server) {
                    float chargeT = (PhaseTimer - 20) / 20f;
                    for (int i = 0; i < 3; i++) {
                        float radius = 120f * (1f - chargeT);
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(radius, radius);
                        Vector2 dustVel = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (4f + chargeT * 6f);
                        var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.5f + chargeT;
                        d.velocity = dustVel;
                    }
                }
            }
            else if (PhaseTimer == 41) {
                // 发动冲刺
                Vector2 direction = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = direction * 35f;
                dashCount++;

                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                TriggerEnergyWave();
                YinEmperorHelper.CreateDragonBurst(NPC.Center, 60f, 2, 10);
            }
            else if (PhaseTimer <= 65) {
                // 冲刺中 - 龙气拖尾
                YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 2f);

                // 冲刺减速
                if (PhaseTimer > 55) {
                    NPC.velocity *= 0.92f;
                }
            }
            else {
                NPC.velocity *= 0.9f;

                // 多段冲刺（最多3次）
                if (dashCount < 3 && PhaseTimer == 80) {
                    PhaseTimer = 0;
                    AttackTimer = 0;
                }
                else if (PhaseTimer > 90) {
                    dashCount = 0;
                    ChooseNextPhase1State();
                }
            }
        }

        /// <summary>
        /// 冥谕降罚 - 召唤多种冥眼激光阵列
        /// 每次进入随机选择一种激光阵型：侧方列阵 / 环形牢笼 / 十字扫射
        /// </summary>
        private void RunNetherDecree(Player target) {
            // 飞到高处
            Vector2 hoverPos = target.Center + new Vector2(0, -400f + hoverOffset);
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.04f, 0.06f);

            // 蓄力阶段
            if (PhaseTimer < 60) {
                auraIntensity = MathHelper.Lerp(auraIntensity, 1.5f, 0.02f);

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 6 == 0) {
                    YinEmperorHelper.CreateImperialTrail(NPC.Center, Vector2.Zero, 1f);
                }
            }

            // 根据SpecialCounter选择阵型（每次进入此状态会切换）
            int formation = (int)SpecialCounter % 3;

            if (PhaseTimer == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                switch (formation) {
                    case 0:
                        // 侧方列阵：两波左右列阵
                        SpawnEyeLaserFormation(target, -1);
                        break;
                    case 1:
                        // 环形牢笼：围成圆环向中心射激光
                        SpawnRingLaserFormation(target, 6);
                        break;
                    case 2:
                        // 十字扫射：十字排列缓慢旋转
                        SpawnCrossLaserFormation(target, 8);
                        break;
                }
            }

            if (PhaseTimer == 130 && formation == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnEyeLaserFormation(target, 1); // 侧方列阵第二波
            }

            // 冥雷粒子装饰
            if (PhaseTimer >= 60 && PhaseTimer <= 200 && PhaseTimer % 35 == 0 && Main.netMode != NetmodeID.Server) {
                float lightningX = target.Center.X + Main.rand.NextFloat(-300f, 300f);
                Vector2 top = new Vector2(lightningX, target.Center.Y - 600f);
                Vector2 bottom = new Vector2(lightningX, target.Center.Y + 100f);
                YinEmperorHelper.CreateNetherLightningPillar(top, bottom, 0.8f);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.8f }, new Vector2(lightningX, target.Center.Y));
            }

            auraIntensity = MathHelper.Lerp(auraIntensity, 1f, 0.01f);

            if (PhaseTimer > 280) {
                SpecialCounter++;
                ChooseNextPhase1State();
            }
        }

        /// <summary>
        /// 镇魂封印 - 冥眼环绕封锁
        /// 召唨5只冥眼整齐地环绕玩家收缩，演出感强
        /// Boss同时释放扫射激光增加压力
        /// </summary>
        private void RunSoulSeal(Player target) {
            // 缓慢逼近玩家
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 3f, 0.04f);

            // 蓄力阶段 - 符文向外扩散
            if (PhaseTimer < 60) {
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                    int sealPoints = 8;
                    float sealRadius = 30f + PhaseTimer * 3f;
                    for (int i = 0; i < sealPoints; i++) {
                        float angle = MathHelper.TwoPi * i / sealPoints + PhaseTimer * 0.05f;
                        Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sealRadius;

                        var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                    }
                }

                if (PhaseTimer == 50) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);
                }
            }
            // 封印激活 - 召唨5只冥眼环绕
            else if (PhaseTimer == 60) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SpawnEncirclingEyes(target, 5);
                }

                YinEmperorHelper.CreateDragonBurst(NPC.Center, 120f, 3, 20);
                TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);
            }
            // 冥眼环绕期间Boss释放扫射激光增加压力
            else if (PhaseTimer == 120 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnSweepingLaser(NPC.Center + new Vector2(-250, 0), MathHelper.PiOver4);
            }
            else if (PhaseTimer == 160 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnSweepingLaser(NPC.Center + new Vector2(250, 0), MathHelper.Pi - MathHelper.PiOver4);
            }

            if (PhaseTimer > 300) {
                ChooseNextPhase1State();
            }
        }

        /// <summary>
        /// 帝怒 - 阶段1特殊技能
        /// 召唤少量守卫冥眼 + 追踪弹波 + 激光活爆结束
        /// </summary>
        private void RunImperialWrath(Player target) {
            // 追踪逼近
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float chaseSpeed = 7f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer * chaseSpeed, 0.1f);

            // 帝王拖尾
            YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 1.5f);

            // 开始时召唤少量守卫冥眼
            if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnGuardianEyes(3);
            }

            // 追踪弹波（降低频率，每次发射更少）
            if (AttackTimer % 40 == 0) {
                ShootImperialBolts(target);
            }

            // 环形追踪弹波（数量减少）
            if (AttackTimer % 90 == 45) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = 6;
                    int damage = YinEmperorHelper.GetScaledDamage(75);
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi * i / count;
                        Vector2 dir = angle.ToRotationVector2();
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, dir * 9f,
                            ModContent.ProjectileType<YinEmperorBolt>(), damage, 1f,
                            Main.myPlayer
                        );
                    }
                }

                TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
            }

            // 激光爆发结束技：在Boss两侧召唤扫射激光
            if (PhaseTimer == 220 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnSweepingLaser(NPC.Center + new Vector2(-200, -100), 0f);
                SpawnSweepingLaser(NPC.Center + new Vector2(200, -100), MathHelper.Pi);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            }

            // 特效
            if (Main.rand.NextBool(3) && Main.netMode != NetmodeID.Server) {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(50f, 50f);
                var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.8f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            if (PhaseTimer > 340) {
                ChooseNextPhase1State();
            }
        }

        #endregion

        #region 攻击方法

        /// <summary>帝冥弹 - 扇形金紫追踪能量弹</summary>
        private void ShootImperialBolts(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = YinEmperorHelper.GetScaledDamage(90);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = 3;
            float spread = 0.2f;

            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 direction = toPlayer.RotatedBy(angle);
                float speed = 13f + Main.rand.NextFloat(-1.5f, 1.5f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 60f,
                    direction * speed,
                    ModContent.ProjectileType<YinEmperorBolt>(),
                    damage, 1f, Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 40f, 1, 6);
        }

        /// <summary>地面符文 - 在玩家脚下召唤冥眼从地下射出激光柱</summary>
        private void ShootGroundSeals(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = YinEmperorHelper.GetScaledDamage(75);
            int count = 3;

            for (int i = 0; i < count; i++) {
                Vector2 sealPos = target.Center + new Vector2((i - 1) * 200f + Main.rand.NextFloat(-40f, 40f), 50f);

                // 符文标记粒子
                if (Main.netMode != NetmodeID.Server) {
                    for (int j = 0; j < 8; j++) {
                        float angle = MathHelper.TwoPi * j / 8;
                        Vector2 dustPos = sealPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 40f;
                        var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.2f;
                        d.velocity = new Vector2(0, -2f);
                    }
                }

                // 从下方射出帝冥追踪弹
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    sealPos + new Vector2(0, 300f),
                    new Vector2(0, -12f),
                    ModContent.ProjectileType<YinEmperorBolt>(),
                    damage, 1f, Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 0.9f }, target.Center);
        }

        #region 冥眼召唤方法

        /// <summary>召唤冥眼列阵（激光模式）- 在玩家侧面排列眼睛</summary>
        private void SpawnEyeLaserFormation(Player target, int side) {
            int eyeCount = 4;
            float spacing = 120f;
            float sideOffset = 400f * side;
            int damage = YinEmperorHelper.GetScaledDamage(110);

            for (int i = 0; i < eyeCount; i++) {
                float yOffset = (i - (eyeCount - 1) / 2f) * spacing;
                Vector2 spawnPos = target.Center + new Vector2(sideOffset, yOffset);

                // 飞向目标位置的初速度
                Vector2 toPos = (spawnPos - NPC.Center).SafeNormalize(Vector2.UnitX) * 8f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    toPos,
                    ModContent.ProjectileType<ArenaEdge>(),
                    damage, 2f, Main.myPlayer,
                    ai0: 0, // 激光模式
                    ai1: i  // 阵列索引
                );
            }

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.1f, Volume = 1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 60f, 2, 10);
        }

        /// <summary>召唤冥眼环绕冲锋 - 一圈眼睛围住玩家后冲向中心</summary>
        private void SpawnEncirclingEyes(Player target, int count) {
            int damage = YinEmperorHelper.GetScaledDamage(95);

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 400f;
                Vector2 toTarget = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 6f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    toTarget,
                    ModContent.ProjectileType<ArenaEdge>(),
                    damage, 2f, Main.myPlayer,
                    ai0: 1,    // 环绕冲锋模式
                    ai1: angle // 初始角度偏移
                );
            }

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.2f }, target.Center);
        }

        /// <summary>召唤守卫冥眼 - 环绕Boss旋转并发射追踪弹</summary>
        private void SpawnGuardianEyes(int count) {
            int damage = YinEmperorHelper.GetScaledDamage(65);

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 130f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<ArenaEdge>(),
                    damage, 1f, Main.myPlayer,
                    ai0: 2,    // 守卫环绕模式
                    ai1: angle // 初始角度偏移
                );
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.9f }, NPC.Center);
        }

        /// <summary>召唤环形激光阵 - 眼睛围成圆环朝中心射激光</summary>
        private void SpawnRingLaserFormation(Player target, int count) {
            int damage = YinEmperorHelper.GetScaledDamage(105);

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = NPC.Center; // 从Boss身边飞出

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    angle.ToRotationVector2() * 10f, // 飞向圆环位置
                    ModContent.ProjectileType<ArenaEdge>(),
                    damage, 2f, Main.myPlayer,
                    ai0: 3,    // 环形激光模式
                    ai1: angle // 圆环上的角度位置
                );
            }

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0f, Volume = 1.1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 80f, 2, 15);
        }

        /// <summary>召唤十字激光阵 - 眼睛排列成十字形缓慢旋转扫射</summary>
        private void SpawnCrossLaserFormation(Player target, int totalEyes) {
            int damage = YinEmperorHelper.GetScaledDamage(100);
            int perArm = totalEyes / 4;
            float armLength = 350f;

            for (int arm = 0; arm < 4; arm++) {
                float baseAngle = arm * MathHelper.PiOver2; // 0, 90, 180, 270度
                for (int i = 0; i < perArm; i++) {
                    float dist = armLength * (i + 1) / perArm;
                    Vector2 spawnPos = NPC.Center + baseAngle.ToRotationVector2() * dist;
                    Vector2 toPos = (spawnPos - NPC.Center).SafeNormalize(Vector2.UnitX) * 8f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        toPos,
                        ModContent.ProjectileType<ArenaEdge>(),
                        damage, 2f, Main.myPlayer,
                        ai0: 4, // 十字激光模式
                        ai1: arm * perArm + i
                    );
                }
            }

            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
            YinEmperorHelper.CreateImperialVortex(NPC.Center, 100f, 1f, 30);
        }

        /// <summary>召唤扫射激光眼 - 单只眼睛释放旋转激光</summary>
        private void SpawnSweepingLaser(Vector2 position, float startAngle) {
            int damage = YinEmperorHelper.GetScaledDamage(100);
            Vector2 toPos = (position - NPC.Center).SafeNormalize(Vector2.UnitX) * 6f;

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                toPos,
                ModContent.ProjectileType<ArenaEdge>(),
                damage, 2f, Main.myPlayer,
                ai0: 5,         // 扫射激光模式
                ai1: startAngle // 初始扫射角度
            );
        }

        #endregion

        #endregion

        #region 阶段转换

        private void TransitionTo(AIState newState) {
            CurrentState = newState;
            PhaseTimer = 0;
            AttackTimer = 0;
            dashCount = 0;
            NPC.netUpdate = true;
        }

        private void ChooseNextPhase1State() {
            int choice = Main.rand.Next(5);

            switch (choice) {
                case 0:
                    TransitionTo(AIState.ImperialHover);
                    break;
                case 1:
                    TransitionTo(AIState.DragonSweep);
                    break;
                case 2:
                    TransitionTo(AIState.NetherDecree);
                    break;
                case 3:
                    TransitionTo(AIState.SoulSeal);
                    break;
                case 4:
                    TransitionTo(AIState.ImperialWrath);
                    break;
            }
        }

        #endregion

        #region 视觉效果

        private void TriggerEnergyWave() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] <= 0.1f) {
                    energyWaveRadius[i] = 0f;
                    energyWaveAlpha[i] = 1f;
                    break;
                }
            }
        }

        private void UpdateEnergyWaves() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0f) {
                    energyWaveRadius[i] += 14f;
                    energyWaveAlpha[i] -= 0.018f;
                    if (energyWaveAlpha[i] < 0f) energyWaveAlpha[i] = 0f;
                }
            }
        }

        private void CreateAmbientParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            // 帝冥金焰环绕
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 70f + Main.rand.NextFloat(30f);
                Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.3f * auraIntensity;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                d.alpha = 80;
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            int frameHeight = tex.Height / MaxFrames;
            Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);

            // 能量波
            DrawEnergyWaves(spriteBatch, screenPos);

            // 出场光柱
            if (introPillarAlpha > 0.01f) {
                YinEmperorHelper.DrawDragonPillar(spriteBatch, NPC.Center + new Vector2(0, frameHeight * 0.4f),
                    800f, 60f, pulsePhase, introPillarAlpha);
            }

            // 帝冥法环（Boss背后的旋转巨环）
            if (ringAlpha > 0.01f) {
                YinEmperorHelper.DrawImperialRing(spriteBatch, NPC.Center, ringScale,
                    ringRotation, pulsePhase, ringAlpha * ((255 - NPC.alpha) / 255f));
            }

            // 帝冥光环
            YinEmperorHelper.DrawImperialAura(spriteBatch, NPC.Center, 90f * auraIntensity,
                10, auraRotation, pulsePhase, auraIntensity);

            // 龙气环绕球
            if (auraIntensity > 0.5f) {
                YinEmperorHelper.DrawDragonOrbs(spriteBatch, NPC.Center, 110f, 4,
                    pulsePhase * 0.8f, pulsePhase);
            }

            // 帝冥色调
            Color imperialColor = Color.Lerp(drawColor, YinEmperorHelper.ImperialGold, 0.3f);
            imperialColor = Color.Lerp(imperialColor, YinEmperorHelper.AbyssPurple, 0.15f);

            // 拖尾
            DrawTrail(spriteBatch, screenPos, tex, sourceRect, origin, imperialColor);

            // 外发光
            Color glowColor = YinEmperorHelper.ImperialGold;
            glowColor.A = 0;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.08f;
            for (int i = 3; i >= 0; i--) {
                float glowScale = NPC.scale * (1.15f + i * 0.1f) * pulse * auraIntensity;
                spriteBatch.Draw(tex, NPC.Center - screenPos, sourceRect, glowColor * (0.12f / (i + 1)),
                    NPC.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, sourceRect, imperialColor * ((255 - NPC.alpha) / 255f),
                NPC.rotation, origin, NPC.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        private void DrawEnergyWaves(SpriteBatch sb, Vector2 screenPos) {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0.05f) {
                    YinEmperorHelper.DrawEnergyWave(sb, NPC.Center, energyWaveRadius[i], 25f,
                        YinEmperorHelper.ImperialGold, energyWaveAlpha[i] * 0.5f);
                }
            }
        }

        private void DrawTrail(SpriteBatch sb, Vector2 screenPos, Texture2D tex,
            Rectangle sourceRect, Vector2 origin, Color baseColor) {
            for (int layer = 0; layer < 2; layer++) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;

                    Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                    float progress = 1f - i / (float)NPC.oldPos.Length;
                    float fade = progress * (layer == 0 ? 0.25f : 0.4f) * auraIntensity;

                    Color trailColor = layer == 0 ? YinEmperorHelper.AbyssPurple : baseColor;
                    trailColor *= fade;
                    if (layer == 0) trailColor.A = 0;

                    float trailScale = NPC.scale * (layer == 0 ? 1.3f : 1f) * (0.5f + progress * 0.5f);

                    sb.Draw(tex, pos, sourceRect, trailColor, NPC.rotation, origin, trailScale,
                        SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 死亡

        public override void OnKill() {
            // 史诗级死亡特效
            YinEmperorHelper.CreateImperialVortex(NPC.Center, 350f, 2.5f, 120);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 300f, 6, 30);
            YinEmperorHelper.CreateTalismanBurst(NPC.Center, 400f, 60);

            // 多重能量波
            for (int i = 0; i < 5; i++) {
                TriggerEnergyWave();
            }

            // 八方冥雷
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 strikePos = NPC.Center + dir * 250f;
                YinEmperorHelper.CreateNetherLightningPillar(strikePos - new Vector2(0, 600), strikePos, 1.5f);
            }

            // 大量粒子爆发
            for (int i = 0; i < 250; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(28f, 28f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(NPC.Center, dustType);
                d.noGravity = true;
                d.scale = 3.5f;
                d.velocity = vel;
            }

            // 屏幕闪烁
            YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 2f);

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(30, 100);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 2f }, NPC.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                DownedBossSystem.downedYinEmperor = true;
            }
        }

        #endregion
    }
}

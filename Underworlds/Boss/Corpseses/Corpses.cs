using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 枉死千骸 - 大后期Boss，由头部和两只手臂组成，类似骷髅王的多部件Boss
    /// </summary>
    [AutoloadBossHead]
    internal class Corpses : ModNPC
    {
        // ====== Boss阶段系统 ======
        public enum BossPhase
        {
            Intro,          // 出场
            BasicAttack,    // 基础攻击
            SpiralHunt,     // 螺旋追猎
            FuryCombo,      // 狂暴连击
            DarkRitual,     // 黑暗仪式
            FinalRage       // 终极狂暴
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float LocalTimer => ref NPC.ai[2];
        public ref float SpecialTimer => ref NPC.ai[3];

        // ====== Boss状态变量 ======
        private int seed = -1;
        private Random random;
        private bool spawnHands = true;
        private bool didIntroShock = false;
        private float introAppear = 0f;
        private float spiralAngle = 0f;
        private float spiralSpeed = 0f;
        private int comboCount = 0;

        // ====== V2 阶段门控 ======
        private bool ritualTriggered = false;   // 引魂大阵 @55% 单次门控
        private bool cityGateTriggered = false;  // 城门闭合 @20% 单次门控

        // ====== V2 引魂大阵 (DarkRitual set-piece) ======
        private int ritualStage = 0;             // 0=脱体起阵 1=施法收缩 2=结算
        private Vector2 ritualCenter;            // 法阵中心 (开阵时锁定)
        private float ritualRadius = 0f;         // 当前法阵半径 (世界像素, 收缩)
        private float ritualDecalIntensity = 0f; // 法阵着色器淡入淡出
        private int ritualGateSeed = 0;          // 生门方位随机种 (同步)
        private float ritualBreakProgress = 0f;  // 站生门累计破阵进度 0~1
        private bool ritualBroken = false;       // 本次仪式是否被打断
        private int vulnerableTimer = 0;         // 头部破绽窗口 (打断成功奖励)
        private const int RitualWindup = 90;     // 脱体起阵前摇
        private const int RitualChannel = 360;   // 施法收缩时长 (~6s)
        private const float RitualStartRadius = 560f;
        private const float RitualEndRadius = 200f;
        private const int GateSlotCount = 4;     // 生门数

        // ====== V2 城门闭合 (FinalRage) ======
        private Vector2 cityCenter;              // 闭合中心
        private float cityRadius = 0f;           // 收缩中的城墙半径
        private const float CityStartRadius = 720f;
        private const float CityEndRadius = 360f;

        // ====== 手臂引用 ======
        private CorpsesHand leftHand;
        private CorpsesHand rightHand;
        private bool handsInitialized = false;

        // ====== 网络同步数据 ======
        private readonly int[] otherAI = new int[4];

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 120;
            NPC.defense = 60;
            NPC.lifeMax = 800000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 100000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            LocalTimer = 0;
            introAppear = 0;

            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(introAppear);
            writer.Write(spiralAngle);
            writer.Write(spiralSpeed);
            writer.Write(comboCount);
            writer.Write(ritualTriggered);
            writer.Write(cityGateTriggered);
            writer.Write(ritualStage);
            writer.WriteVector2(ritualCenter);
            writer.Write(ritualRadius);
            writer.Write(ritualGateSeed);
            writer.Write(ritualBreakProgress);
            writer.Write(ritualBroken);
            writer.Write(vulnerableTimer);
            writer.WriteVector2(cityCenter);
            writer.Write(cityRadius);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            introAppear = reader.ReadSingle();
            spiralAngle = reader.ReadSingle();
            spiralSpeed = reader.ReadSingle();
            comboCount = reader.ReadInt32();
            ritualTriggered = reader.ReadBoolean();
            cityGateTriggered = reader.ReadBoolean();
            ritualStage = reader.ReadInt32();
            ritualCenter = reader.ReadVector2();
            ritualRadius = reader.ReadSingle();
            ritualGateSeed = reader.ReadInt32();
            ritualBreakProgress = reader.ReadSingle();
            ritualBroken = reader.ReadBoolean();
            vulnerableTimer = reader.ReadInt32();
            cityCenter = reader.ReadVector2();
            cityRadius = reader.ReadSingle();
            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.8f);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            random ??= new Random(seed);

            // 生成手臂
            if (spawnHands) {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int leftHandIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                        ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, -1);
                    int rightHandIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                        ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, 1);
                }
            }

            // 初始化手臂引用
            if (!handsInitialized) {
                InitializeHandReferences();
            }

            // 目标选择
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.4f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            PhaseTimer++;
            LocalTimer++;

            if (vulnerableTimer > 0)
                vulnerableTimer--;

            // V2 血量阈值门控: 55% 引魂大阵 (单次) / 20% 城门闭合 (终幕)
            CheckPhaseGates(target);

            // 根据阶段执行AI
            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.BasicAttack:
                    RunBasicAttack(target);
                    break;
                case BossPhase.SpiralHunt:
                    RunSpiralHunt(target);
                    break;
                case BossPhase.FuryCombo:
                    RunFuryCombo(target);
                    break;
                case BossPhase.DarkRitual:
                    RunDarkRitual(target);
                    break;
                case BossPhase.FinalRage:
                    RunFinalRage(target);
                    break;
            }
        }

        private void InitializeHandReferences() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.active && npc.ModNPC is CorpsesHand hand && npc.ai[0] == NPC.whoAmI) {
                    if (npc.ai[1] > 0)
                        rightHand = hand;
                    else if (npc.ai[1] < 0)
                        leftHand = hand;
                }
            }

            if (leftHand != null && rightHand != null)
                handsInitialized = true;
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            LocalTimer = 0;
            NPC.netUpdate = true;

            // 重置相关状态
            comboCount = 0;

            if (newPhase == BossPhase.SpiralHunt) {
                spiralAngle = 0;
                spiralSpeed = 0;
            }
            else if (newPhase == BossPhase.DarkRitual) {
                ritualStage = 0;
                ritualBreakProgress = 0f;
                ritualBroken = false;
                ritualRadius = RitualStartRadius;
                ritualGateSeed = random.Next(0, 100000);
                // 法阵中心锁定在玩家与 Boss 之间偏玩家处, 保证两手与法阵同框
                Player tgt = Main.player[NPC.target];
                ritualCenter = tgt.active ? tgt.Center : NPC.Center;
            }
            else if (newPhase == BossPhase.FinalRage) {
                cityRadius = CityStartRadius;
                Player tgt = Main.player[NPC.target];
                cityCenter = tgt.active ? tgt.Center : NPC.Center;
            }
        }

        // ====== V2 阶段门控 ======
        private void CheckPhaseGates(Player target) {
            // 终幕: 城门闭合 (20%) —— 优先级最高, 一旦进入不再退出
            if (!cityGateTriggered && NPC.life < NPC.lifeMax * 0.2f
                && Phase != BossPhase.Intro && Phase != BossPhase.FinalRage) {
                cityGateTriggered = true;
                ReleaseHands();
                TransitionTo(BossPhase.FinalRage);
                return;
            }

            // 签名 set-piece: 引魂大阵 (55%) —— 单次触发, 打断常态编排
            if (!ritualTriggered && NPC.life < NPC.lifeMax * 0.55f
                && Phase != BossPhase.Intro && Phase != BossPhase.DarkRitual && Phase != BossPhase.FinalRage) {
                ritualTriggered = true;
                ReleaseHands();
                TransitionTo(BossPhase.DarkRitual);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualStart"),
                    UnderworldField.DecreeColor);
            }
        }

        private void ReleaseHands() {
            if (leftHand != null && leftHand.NPC.active) leftHand.ReleaseToIdle();
            if (rightHand != null && rightHand.NPC.active) rightHand.ReleaseToIdle();
        }

        private void AnnounceCenter(string text, Color color) {
            if (Main.dedServ || string.IsNullOrEmpty(text))
                return;
            CombatText.NewText(new Rectangle((int)NPC.Center.X, (int)NPC.Center.Y - 120, 1, 1), color, text, true);
        }

        // ====== AI阶段实现 ======
        private void RunIntro(Player target) {
            // 出场动画：从下方升起，带扭曲效果
            introAppear = ACMUtils.SineInOut(MathHelper.Clamp(PhaseTimer / 180f, 0, 1));

            Vector2 startPos = target.Center + new Vector2(0, 800);
            Vector2 endPos = target.Center + new Vector2(0, -250);
            Vector2 desired = Vector2.Lerp(startPos, endPos, introAppear);

            NPC.Center += (desired - NPC.Center) * 0.1f;
            NPC.velocity *= 0.85f;

            // 出场粒子效果
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 offset = Main.rand.NextVector2Circular(100, 100) * (1 - introAppear);
                    int dust = Dust.NewDust(NPC.Center + offset, 0, 0, DustID.Shadowflame, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -offset.SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            // 出场冲击
            if (!didIntroShock && introAppear > 0.95f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);

                // 屏幕震动 (§6.2 入场定格预算, 取 max)
                ACMUtils.AddScreenShake(15f);

                // 爆发粒子
                if (Main.netMode != NetmodeID.Server) {
                    for (int k = 0; k < 50; k++) {
                        Vector2 vel = Main.rand.NextVector2Circular(15, 15);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Smoke, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 200) {
                TransitionTo(BossPhase.BasicAttack);
            }
        }

        private void RunBasicAttack(Player target) {
            // 基础逼近玩家
            Vector2 toTarget = target.Center - NPC.Center;
            float distance = toTarget.Length();
            Vector2 desiredVel = toTarget.SafeNormalize(Vector2.Zero) * 8f;

            // 添加侧向移动
            float sideOffset = MathF.Sin(PhaseTimer * 0.04f) * 5f;
            desiredVel = desiredVel.RotatedBy(sideOffset * 0.1f);

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.08f);

            // 近战攻击 - 更频繁
            if (PhaseTimer % 80 == 20 && handsInitialized) {
                // 交替让左右手挥砍
                if (Main.rand.NextBool()) {
                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.Slashing);
                }
                else {
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.Slashing);
                }
            }

            // 近战突刺攻击
            if (PhaseTimer % 80 == 50 && handsInitialized) {
                CorpsesHand attackHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (attackHand != null && attackHand.NPC.active) {
                    CommandHandAttack(attackHand, target, CorpsesHand.HandState.Reaching);
                }
            }

            // 骨头泼洒攻击 - 降低频率
            if (PhaseTimer % 200 == 100 && handsInitialized) {
                CorpsesHand tossHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (tossHand != null && tossHand.NPC.active) {
                    CommandHandAttack(tossHand, target, CorpsesHand.HandState.BoneToss);
                }
            }

            // 发射追踪暗影球 - 降低频率
            if (PhaseTimer % 120 == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 spawnPos = NPC.Center + Main.rand.NextVector2Circular(50, 50);
                Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 6f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity,
                    ModContent.ProjectileType<CorpsesShadowOrb>(), GetBossDamage(0.7f), 2f);
            }

            // 双手拍掌攻击 - 降低频率，作为特殊技能
            if (PhaseTimer % 300 == 150 && handsInitialized && leftHand != null && rightHand != null) {
                // 确保两只手都处于空闲状态才发起拍掌
                if (leftHand.IsIdle() && rightHand.IsIdle()) {
                    Vector2 clapCenter = target.Center + target.velocity * 20f;
                    // 增加蓄力距离 (80 -> 180)
                    Vector2 leftTarget = clapCenter + new Vector2(-180, 0);
                    Vector2 rightTarget = clapCenter + new Vector2(180, 0);

                    CommandHandAttack(leftHand, target, CorpsesHand.HandState.ClapCharging, leftTarget);
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.ClapCharging, rightTarget);
                }
            }

            // 抓取攻击
            if (PhaseTimer % 150 == 75 && handsInitialized && distance < 300f) {
                CorpsesHand grabHand = Main.rand.NextBool() ? leftHand : rightHand;
                if (grabHand != null && grabHand.NPC.active) {
                    CommandHandAttack(grabHand, target, CorpsesHand.HandState.Grabbing);
                }
            }

            // 生命值低于70%时转换阶段
            if (NPC.life < NPC.lifeMax * 0.7f) {
                TransitionTo(BossPhase.SpiralHunt);
            }
            else if (PhaseTimer > 600) {
                TransitionTo(BossPhase.SpiralHunt);
            }
        }

        // 指挥手臂攻击的辅助方法
        private void CommandHandAttack(CorpsesHand hand, Player target, CorpsesHand.HandState attackType, Vector2? customTarget = null) {
            if (hand == null || hand.NPC == null || !hand.NPC.active)
                return;

            // 调用手臂的公开攻击触发方法
            Vector2 targetPos = customTarget ?? target.Center;

            // 根据攻击类型预测玩家位置
            if (!customTarget.HasValue) {
                if (attackType == CorpsesHand.HandState.Slashing) {
                    targetPos = target.Center + target.velocity * 10f;
                }
                else if (attackType == CorpsesHand.HandState.Reaching) {
                    targetPos = target.Center + target.velocity * 15f;
                }
                else if (attackType == CorpsesHand.HandState.BoneToss) {
                    targetPos = target.Center + target.velocity * 12f;
                }
            }

            hand.TriggerAttack(attackType, targetPos);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 这里可以添加Boss掉落物
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Corpsefragments>(), 1, 23, 30));
        }

        // 引魂大阵被打断后的头部破绽窗口: 提高受伤倍率 (奖励正反馈)
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (vulnerableTimer > 0)
                modifiers.FinalDamage *= 1.6f;
        }

        public override void OnKill() {
            // 进度门控: 补齐缺失的 downedCorpses (枉死城门, 解锁觉醒冥龙等)
            if (Main.netMode != NetmodeID.MultiplayerClient)
                DownedBossSystem.downedCorpses = true;

            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
            ACMUtils.AddScreenShake(16f);

            // 双手随头颅崩解
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (leftHand != null && leftHand.NPC.active) { leftHand.NPC.life = 0; leftHand.NPC.active = false; }
                if (rightHand != null && rightHand.NPC.active) { rightHand.NPC.life = 0; rightHand.NPC.active = false; }
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 60; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(13, 13);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        // ====== 千骸旋冢: 双手脱体环绕玩家, 整周收口内拍 ======
        // comboCount: 0=环绕 1=内拍收口; SpecialTimer: 内拍倒计时
        private const float OrbitRadius = 330f;
        private void RunSpiralHunt(Player target) {
            // Boss 退到上方俯瞰, 让双手在玩家周围环绕成"旋冢"
            Vector2 hoverPos = target.Center + new Vector2(0, -460 + MathF.Sin(PhaseTimer * 0.04f) * 30f);
            NPC.Center += (hoverPos - NPC.Center) * 0.08f;
            NPC.velocity *= 0.9f;

            if (!handsInitialized || leftHand == null || rightHand == null)
                return;

            // 起手: 双手脱体进入受控环绕
            if (PhaseTimer < 2) {
                comboCount = 0;
                spiralAngle = 0f;
                spiralSpeed = 0.045f;
                leftHand.EnterControlled(target.Center + new Vector2(-OrbitRadius, 0), true);
                rightHand.EnterControlled(target.Center + new Vector2(OrbitRadius, 0), true);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f }, NPC.Center);
            }

            if (comboCount == 0) {
                // —— 环绕: 两手对位绕玩家固定半径旋转 (IK 根可见的脱体环绕) ——
                spiralSpeed = MathHelper.Clamp(spiralSpeed + 0.0006f, 0.045f, 0.085f);
                spiralAngle += spiralSpeed;

                float r = OrbitRadius;
                Vector2 lp = target.Center + spiralAngle.ToRotationVector2() * r;
                Vector2 rp = target.Center + (spiralAngle + MathHelper.Pi).ToRotationVector2() * r;
                leftHand.DriveControlled(lp, true);
                rightHand.DriveControlled(rp, true);

                // 整周完成 -> 进入内拍收口 (可读: 环绕一圈后双手向内合)
                if (spiralAngle >= MathHelper.TwoPi) {
                    comboCount = 1;
                    SpecialTimer = 30; // 内拍窗口 (telegraph: 双手快速内收)
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = -0.2f }, target.Center);
                }
            }
            else {
                // —— 内拍收口: 双手从环绕位快速向玩家中心合拢, 中点爆冲击波 ——
                SpecialTimer--;
                float t = 1f - MathHelper.Clamp(SpecialTimer / 30f, 0f, 1f);
                float ease = ACMUtils.QuadIn(t);
                float r = MathHelper.Lerp(OrbitRadius, 36f, ease);
                Vector2 lp = target.Center + spiralAngle.ToRotationVector2() * r;
                Vector2 rp = target.Center + (spiralAngle + MathHelper.Pi).ToRotationVector2() * r;
                leftHand.DriveControlled(lp, true);
                rightHand.DriveControlled(rp, true);

                // 内收预警尘线 (青白, 沿合拢轴)
                if (!Main.dedServ && SpecialTimer > 6 && PhaseTimer % 2 == 0) {
                    Vector2 axis = spiralAngle.ToRotationVector2();
                    for (int s = -1; s <= 1; s += 2) {
                        var d = Dust.NewDustPerfect(target.Center + axis * r * s, DustID.Vortex);
                        d.noGravity = true; d.scale = 1.2f; d.velocity = -axis * s * 4f;
                    }
                }

                // 合拢点: 冲击波环 + 泛光 (复用拍掌弹)
                if (SpecialTimer == 6) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int count = 20;
                        for (int i = 0; i < count; i++) {
                            float a = MathHelper.TwoPi * i / count;
                            Vector2 vel = a.ToRotationVector2() * 14f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center, vel,
                                ModContent.ProjectileType<CorpsesClapWave>(), GetBossDamage(0.6f), 3f, Main.myPlayer, 0, 1);
                        }
                    }
                    leftHand.FlagClapBloom(target.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f }, target.Center);
                    ACMUtils.AddScreenShake(8f);
                }

                // 收口结束 -> 重新展开环绕
                if (SpecialTimer <= 0) {
                    comboCount = 0;
                    spiralAngle = 0f;
                }
            }

            if (PhaseTimer > 460) {
                ReleaseHands();
                TransitionTo(BossPhase.FuryCombo);
            }
        }

        // ====== 连骨审判: 脚本化 左-右-左 探抓 → 拍掌 → 传送拍掌, 每周期一次 ======
        private void RunFuryCombo(Player target) {
            // Boss 稳定悬于玩家上方中距, 让脚本化连段可读
            Vector2 anchor = target.Center + new Vector2(0, -300);
            NPC.Center += (anchor - NPC.Center) * 0.06f;
            NPC.velocity *= 0.9f;

            if (!handsInitialized || leftHand == null || rightHand == null)
                return;

            int t = (int)PhaseTimer;

            // —— 左-右-左 三段探抓 (每段前有手部伸展前摇, 可读) ——
            if (t == 20) {
                CommandHandAttack(leftHand, target, CorpsesHand.HandState.Reaching);
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f }, NPC.Center);
            }
            else if (t == 78) {
                CommandHandAttack(rightHand, target, CorpsesHand.HandState.Reaching);
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.1f }, NPC.Center);
            }
            else if (t == 136) {
                CommandHandAttack(leftHand, target, CorpsesHand.HandState.Reaching);
                SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
            }
            // —— 合掌审判 (双手两侧蓄力拍掌) ——
            else if (t == 196 && leftHand.IsIdle() && rightHand.IsIdle()) {
                Vector2 c = target.Center + target.velocity * 18f;
                CommandHandAttack(leftHand, target, CorpsesHand.HandState.ClapCharging, c + new Vector2(-190, 0));
                CommandHandAttack(rightHand, target, CorpsesHand.HandState.ClapCharging, c + new Vector2(190, 0));
            }
            // —— 终式: 传送拍掌 (瞬移玩家两侧合击) ——
            else if (t == 320 && leftHand.IsIdle() && rightHand.IsIdle()) {
                Vector2 p = target.Center + target.velocity * 26f;
                CommandHandAttack(leftHand, target, CorpsesHand.HandState.TeleportClap, p + new Vector2(-230, -40));
                CommandHandAttack(rightHand, target, CorpsesHand.HandState.TeleportClap, p + new Vector2(230, -40));
            }

            if (PhaseTimer > 440) {
                TransitionTo(BossPhase.BasicAttack);
            }
        }

        // ====== 引魂大阵 Soul-Summoning Ritual (签名 set-piece) ======
        // ritualStage: 0=脱体起阵 1=施法收缩(可破) 2=结算; SpecialTimer 作每阶段计时
        private void RunDarkRitual(Player target) {
            Vector2 altarL = ritualCenter + new Vector2(-380, -120);
            Vector2 altarR = ritualCenter + new Vector2(380, -120);
            Vector2 bossPos = ritualCenter + new Vector2(0, -300);
            NPC.Center += (bossPos - NPC.Center) * 0.08f;
            NPC.velocity *= 0.85f;

            if (!handsInitialized || leftHand == null || rightHand == null)
                return;

            // 阶段进入
            if (PhaseTimer < 2) {
                ritualStage = 0;
                SpecialTimer = 0;
                ritualDecalIntensity = 0f;
                ritualBreakProgress = 0f;
                ritualBroken = false;
                ritualRadius = RitualStartRadius;
                leftHand.EnterControlled(altarL, false);
                rightHand.EnterControlled(altarR, false);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.3f }, NPC.Center);
            }

            SpecialTimer++;

            switch (ritualStage) {
                case 0: // —— 脱体起阵: 双手飞向两坛, 法阵由虚到实 ——
                    leftHand.DriveControlled(altarL, false);
                    rightHand.DriveControlled(altarR, false);
                    ritualDecalIntensity = MathHelper.Lerp(ritualDecalIntensity, 1f, 0.05f);

                    if (SpecialTimer >= RitualWindup) {
                        ritualStage = 1;
                        SpecialTimer = 0;
                        leftHand.EnterChanneling(altarL);
                        rightHand.EnterChanneling(altarR);
                    }
                    break;

                case 1: // —— 施法收缩: 法阵收缩成献祭阵, 站生门破阵 / 硬抗 ——
                    leftHand.DriveControlled(altarL, false); // Channeling 内部自浮动, 仅保持坛位
                    rightHand.DriveControlled(altarR, false);
                    ritualDecalIntensity = 1f;

                    float prog = MathHelper.Clamp(SpecialTimer / (float)RitualChannel, 0f, 1f);
                    ritualRadius = MathHelper.Lerp(RitualStartRadius, RitualEndRadius, prog);

                    RitualFieldTick(target);

                    if (ritualBreakProgress >= 1f) {
                        ritualBroken = true;
                        ritualStage = 2;
                        SpecialTimer = 0;
                    }
                    else if (SpecialTimer >= RitualChannel) {
                        ritualBroken = false;
                        ritualStage = 2;
                        SpecialTimer = 0;
                    }
                    break;

                case 2: // —— 结算 ——
                    if (SpecialTimer == 1)
                        ResolveRitual(target);
                    ritualDecalIntensity = MathHelper.Lerp(ritualDecalIntensity, 0f, 0.08f);

                    if (SpecialTimer > 70) {
                        if (!ritualBroken)
                            ReleaseHands(); // 失败: 手回体; 成功时手处硬直, 不打扰
                        TransitionTo(BossPhase.BasicAttack);
                    }
                    break;
            }
        }

        // 生门方位 (确定性, 随 ritualGateSeed 同步)
        private float GateBaseAngle => (ritualGateSeed % 628) / 100f;
        private const float GateHalfWidth = 0.42f; // 生门角向半宽 (rad)

        private bool IsInGate(float angle) {
            for (int i = 0; i < GateSlotCount; i++) {
                float ga = GateBaseAngle + i * MathHelper.TwoPi / GateSlotCount;
                float diff = MathHelper.WrapAngle(angle - ga);
                if (Math.Abs(diff) < GateHalfWidth)
                    return true;
            }
            return false;
        }

        // 法阵每帧: 收缩死区 DoT (魂蚀) + 生门破阵进度
        private void RitualFieldTick(Player target) {
            Vector2 rel = target.Center - ritualCenter;
            float dist = rel.Length();
            float ang = rel.ToRotation();
            bool insideArray = dist < ritualRadius + 40f;
            bool inGate = IsInGate(ang);

            if (insideArray && inGate) {
                // 站在生门内: 累计破阵 (站满即打断)
                ritualBreakProgress += 1f / 150f; // ~2.5s 持续站位即破
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(20, 20), DustID.GoldFlame);
                    d.noGravity = true; d.scale = 1.3f; d.velocity = new Vector2(0, -2f);
                }
            }
            else if (insideArray) {
                // 收缩死区内(非生门): 魂蚀 DoT (沿法阵可视边界, 可读)
                if (SpecialTimer % 18 == 0)
                    UnderworldField.AddSoulErosion(target, 1);
            }
            else {
                // 离开法阵也算"硬抗失败路线"之一, 但不破阵 (进度缓退)
                ritualBreakProgress = MathHelper.Max(0f, ritualBreakProgress - 1f / 600f);
            }
        }

        private void ResolveRitual(Player target) {
            if (ritualBroken) {
                // 打断成功: 双手重伤硬直, Boss 头部破绽窗口 ~5s
                leftHand.StunHand(300);
                rightHand.StunHand(300);
                vulnerableTimer = 300;
                ACMUtils.AddScreenShake(10f);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualBreak"),
                    TelegraphColors.Safe);
            }
            else {
                // 仪式完成: 一层冥律标记 + 一次性可躲镇压波 (非持续喷射)
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    UnderworldField.AddNetherDecree(target, 1);
                    int count = 26;
                    for (int i = 0; i < count; i++) {
                        float a = MathHelper.TwoPi * i / count;
                        Vector2 vel = a.ToRotationVector2() * 13f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), ritualCenter, vel,
                            ModContent.ProjectileType<CorpsesClapWave>(), GetBossDamage(0.7f), 3f, Main.myPlayer, 0, 1);
                    }
                    // 镇压骨雨 (一次性, 可躲)
                    for (int i = 0; i < 8; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-12f, -6f));
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), ritualCenter, vel,
                            ModContent.ProjectileType<CorpsesBoneShower>(), GetBossDamage(0.5f), 2f);
                    }
                    // 抓取手追击 (用现有手部抓取)
                    CommandHandAttack(rightHand, target, CorpsesHand.HandState.Grabbing);
                }
                ACMUtils.AddScreenShake(11f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualComplete"),
                    TelegraphColors.Execution);
            }
        }

        // ====== 城门闭合 City-Gate Closure (终幕, 替换喷射狂暴) ======
        // 竞技场以暗影城墙收缩; 仅保留 抓取 + 拍掌 (定时 ~4s), 暗影球改为预告落地骨雨
        private void RunFinalRage(Player target) {
            if (PhaseTimer < 2) {
                cityRadius = CityStartRadius;
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.CityGate"),
                    TelegraphColors.Execution);
                SoundEngine.PlaySound(SoundID.DoorClosed with { Pitch = -0.6f, Volume = 1.4f }, NPC.Center);
            }

            // 城墙缓慢收缩 (持续可见的暗紫 prison-overlay)
            cityRadius = MathHelper.Max(CityEndRadius, cityRadius - 0.35f);

            // 城门中心缓随玩家(很慢), 保持可读边界
            cityCenter = Vector2.Lerp(cityCenter, target.Center, 0.004f);

            // Boss 在城心上方俯瞰
            Vector2 bossPos = cityCenter + new Vector2(0, -300);
            NPC.Center += (bossPos - NPC.Center) * 0.05f;
            NPC.velocity *= 0.9f;

            // 城墙外: 内推 + 魂蚀 (telegraphed, 墙可见)
            float pd = Vector2.Distance(target.Center, cityCenter);
            if (pd > cityRadius) {
                Vector2 inward = (cityCenter - target.Center).SafeNormalize(Vector2.Zero);
                target.velocity += inward * 0.6f;
                if (PhaseTimer % 16 == 0)
                    UnderworldField.AddSoulErosion(target, 1);
            }

            if (!handsInitialized || leftHand == null || rightHand == null)
                return;

            // 只剩两招, 间隔 ~4s 交替: 抓取 / 拍掌
            int cyc = (int)PhaseTimer % 240;
            if (cyc == 60 && leftHand.IsIdle() && rightHand.IsIdle()) {
                // 拍掌 (定时, 可读)
                Vector2 c = target.Center + target.velocity * 16f;
                CommandHandAttack(leftHand, target, CorpsesHand.HandState.ClapCharging, c + new Vector2(-180, 0));
                CommandHandAttack(rightHand, target, CorpsesHand.HandState.ClapCharging, c + new Vector2(180, 0));
            }
            else if (cyc == 180) {
                // 抓取
                CorpsesHand grab = (PhaseTimer % 480 < 240) ? leftHand : rightHand;
                if (grab.IsIdle())
                    CommandHandAttack(grab, target, CorpsesHand.HandState.Grabbing);
            }

            // 暗影球 → 预告落地骨雨地标 (玩家可读躲避)
            if (PhaseTimer % 90 == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 mark = cityCenter + Main.rand.NextVector2Circular(cityRadius * 0.8f, cityRadius * 0.5f);
                    mark.Y += 60f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), mark, Vector2.Zero,
                        ModContent.ProjectileType<CorpsesBoneRainMarker>(), GetBossDamage(0.6f), 0f, Main.myPlayer);
                }
            }

            // 愤怒残痕
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(NPC.Center, NPC.width, NPC.height, DustID.Torch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = texture.Size() / 2;

            // V2 演出层 (硬化 ACMShaders): 引魂大阵 / 城门闭合 地纹牢笼 + 锁链光束
            if (!Main.dedServ) {
                if (Phase == BossPhase.DarkRitual && ritualDecalIntensity > 0.01f)
                    DrawRitualVisuals(spriteBatch);
                else if (Phase == BossPhase.FinalRage)
                    DrawCityVisuals(spriteBatch);
            }

            // 绘制拖尾
            float trailOpacity = 0.3f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                Color trailColor = Color.White * trailOpacity;
                trailOpacity *= 0.7f;

                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation, origin, NPC.scale * (0.9f + 0.1f * trailOpacity), SpriteEffects.None, 0);
            }

            // 绘制主体
            float scale = NPC.scale;
            if (Phase == BossPhase.Intro) {
                scale *= MathHelper.Lerp(0.7f, 1f, introAppear);
            }

            Color mainColor = drawColor;
            if (Phase == BossPhase.FinalRage) {
                mainColor = Color.Lerp(Color.White, Color.Red, 0.3f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f));
            }
            // 头部破绽窗口: 柔白呼吸高光 (奖励正反馈, 非红)
            if (vulnerableTimer > 0) {
                float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);
                mainColor = Color.Lerp(mainColor, TelegraphColors.Safe, 0.4f + 0.3f * pulse);
            }

            spriteBatch.Draw(texture, NPC.Center - Main.screenPosition, null, mainColor, NPC.rotation, origin, scale, SpriteEffects.None, 0);

            return false;
        }

        // —— 引魂大阵: prison-overlay 法阵 + 两手锁链光束 + 生门安全缝 ——
        private void DrawRitualVisuals(SpriteBatch sb) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                ACMShaders.WorldDecalParams(ritualCenter, ritualRadius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(ritualDecalIntensity, 0f, 1f));
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(UnderworldField.SoulBoundColor.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(12f);
                fx.Parameters["uMode"]?.SetValue(1f);   // 牢笼/封锁罩 prison-overlay
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(sb, fx);
            }

            // 两手 → 法阵中心的符文锁链 (DrawBeam)
            if (leftHand != null && leftHand.NPC.active)
                ACMShaders.DrawBeam(leftHand.NPC.Center, ritualCenter, 10f,
                    TelegraphColors.NetherViolet, TelegraphColors.GhostGreen, ritualDecalIntensity, 1.6f, 2.2f);
            if (rightHand != null && rightHand.NPC.active)
                ACMShaders.DrawBeam(rightHand.NPC.Center, ritualCenter, 10f,
                    TelegraphColors.NetherViolet, TelegraphColors.GhostGreen, ritualDecalIntensity, 1.6f, 2.2f);

            // 生门安全缝: 柔白光束 (仅施法阶段, 玩家据此破阵)
            if (ritualStage == 1) {
                for (int i = 0; i < GateSlotCount; i++) {
                    float ga = GateBaseAngle + i * MathHelper.TwoPi / GateSlotCount;
                    Vector2 outer = ritualCenter + ga.ToRotationVector2() * (ritualRadius + 30f);
                    ACMShaders.DrawBeam(ritualCenter, outer, 26f,
                        TelegraphColors.Safe, TelegraphColors.Holy, 0.55f, 0.8f, 1.5f);
                }
            }
        }

        // —— 城门闭合: prison-overlay 收缩城墙 ——
        private void DrawCityVisuals(SpriteBatch sb) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;
            ACMShaders.WorldDecalParams(cityCenter, cityRadius, out Vector2 uv, out float rFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rFrac);
            fx.Parameters["uIntensity"]?.SetValue(0.9f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(UnderworldField.DecreeColor.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(14f);
            fx.Parameters["uMode"]?.SetValue(1f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx);
        }

        public int GetBossDamage(float scaling = 1f) {
            return (int)(NPC.damage * scaling);
        }
    }
}

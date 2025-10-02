using AncientChineseMythology.Items.Weapons.Bosses;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using static AncientChineseMythology.Projectiles.RuyiStickSpearProjectile_3;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    [VaultLoaden("AncientChineseMythology/Textures/")]
    [AutoloadBossHead]
    internal class Yingou : ModNPC
    {
        internal static Texture2D GlaciateWave;//一个水平向右的波浪形灰度图，适合做冲击类刀光一类的效果，大小512*512
        internal static Texture2D SoftGlow;//一个模糊发光效果，圆点灰度图大小64*64
        internal static Texture2D StarTexture;//一个星光点的纹理，大小326*326
        //====== 新阶段系统 ======
        public enum BossPhase
        {
            Intro,
            PatternSetA,   //基础挥砍 + 火球散射
            SpiralDread,   //螺旋+环绕压迫
            SaberHell,     //大刀地狱(扩展演出)
            FrenzyDash,    //新：多段连续追击冲刺
            BladeScatter,  //新：蓄力大斩 + 环形散射
            RecoverDash,   //回收冲刺（过渡）
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float LocalTimer => ref NPC.ai[2];
        public float PhaseLoopCounter; //计数阶段循环次数

        public AttackAIStyle aitype = AttackAIStyle.Melee; //保留给手部的模式提示
        public enum AttackAIStyle { Idle, Melee, Wave, Circle }

        public int seed = -1;
        public Random random = null;
        public bool spawnHands = true;
        public float circleCounter = 0;
        public float circlespeed = 0;
        internal ref int swordDir => ref otherAI[3];
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;
        public static int ReelBackTime => Main.masterMode ? 50 : 60;

        //视觉演出参数
        private float introAppear; //0-1 出场插值
        private float spiralPulse; //螺旋脉冲
        private float saberCharge; //大刀地狱充能
        private bool didIntroShock;

        //SaberHell 扩展图案控制
        private int saberPatternIndex; //当前图案序号
        private int lastSaberPatternTime; //上一次释放图案的时间戳
        private int saberPatternsPerPhase = 5; //每次 SaberHell 阶段释放几种图案

        //FrenzyDash 状态
        private int frenzyDashCount;
        private int frenzyDashTotal;
        private int frenzyDashState; //0 telegraph,1 dash,2 recover
        private int frenzyDashStateTimer;
        private Vector2 frenzyDashDir;
        private float frenzyDashTelegraphAngle = 0f;

        //BladeScatter 状态
        private float bladeScatterCharge;
        private int bladeScatterRingCount;

        //公开访问器，供手部获取冲刺状态
        public int FrenzyDashState => frenzyDashState;
        public int FrenzyDashStateTimer => frenzyDashStateTimer;

        //双手引用，用于动作指挥
        private YingouHand leftHand;
        private YingouHand rightHand;
        private bool handsInitialized = false;

        //动作编排系统
        private bool isPerformingAction = false;
        private int actionCooldown = 0;

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            LocalTimer = 0;
            introAppear = 0;
            saberPatternIndex = 0;
            lastSaberPatternTime = 0;
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 110;
            NPC.height = 110;
            NPC.damage = 66;
            NPC.defense = 40;
            NPC.lifeMax = 420000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.Roar;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YingouKnife>()));
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(introAppear);
            writer.Write(saberPatternIndex);
            writer.Write(lastSaberPatternTime);
            writer.Write(frenzyDashCount);
            writer.Write(frenzyDashTotal);
            writer.Write(frenzyDashState);
            writer.Write(frenzyDashStateTimer);
            writer.WriteVector2(frenzyDashDir);
            writer.Write(bladeScatterCharge);
            writer.Write(bladeScatterRingCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            introAppear = reader.ReadSingle();
            saberPatternIndex = reader.ReadInt32();
            lastSaberPatternTime = reader.ReadInt32();
            frenzyDashCount = reader.ReadInt32();
            frenzyDashTotal = reader.ReadInt32();
            frenzyDashState = reader.ReadInt32();
            frenzyDashStateTimer = reader.ReadInt32();
            frenzyDashDir = reader.ReadVector2();
            bladeScatterCharge = reader.ReadSingle();
            bladeScatterRingCount = reader.ReadInt32();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        private void TransitionTo(BossPhase next) {
            Phase = next;
            PhaseTimer = 0;
            LocalTimer = 0;
            NPC.netUpdate = true;
            if (next == BossPhase.SaberHell) {
                saberPatternIndex = 0;
                lastSaberPatternTime = 0;
            }
            if (next == BossPhase.FrenzyDash) {
                frenzyDashCount = 0;
                frenzyDashTotal = 4 + (Main.expertMode ? 1 : 0) + (Main.masterMode ? 1 : 0);
                frenzyDashState = 0;
                if (!VaultUtils.isClient) {
                    frenzyDashTelegraphAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                }
                frenzyDashStateTimer = 0;
                aitype = AttackAIStyle.Melee;
            }
            if (next == BossPhase.BladeScatter) {
                bladeScatterCharge = 0;
                bladeScatterRingCount = 0;
                aitype = AttackAIStyle.Idle;
            }
        }

        public override void AI() {
            random ??= new Random(seed);
            if (spawnHands) {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int leftHandId = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, 1);
                    int rightHandId = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, -1);
                }
            }

            //初始化双手引用
            if (!handsInitialized)
            {
                InitializeHandReferences();
            }

            if (!VaultUtils.isServer && !SkyManager.Instance[YingouSky.name].IsActive()) {
                SkyManager.Instance.Activate(YingouSky.name);
            }

            if (swordDir == 0) swordDir = 1;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.velocity *= 0.98f;
                    return;
                }
            }

            PhaseTimer++;
            LocalTimer++;

            //更新动作冷却
            if (actionCooldown > 0) actionCooldown--;

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.PatternSetA:
                    RunPatternSetA(target);
                    break;
                case BossPhase.SpiralDread:
                    RunSpiral(target);
                    break;
                case BossPhase.SaberHell:
                    RunSaberHell(target);
                    break;
                case BossPhase.FrenzyDash:
                    RunFrenzyDash(target);
                    break;
                case BossPhase.BladeScatter:
                    RunBladeScatter(target);
                    break;
                case BossPhase.RecoverDash:
                    RunRecoverDash(target);
                    break;
            }
        }

        private void InitializeHandReferences()
        {
            foreach (NPC npc in Main.npc)
            {
                if (npc.active && npc.ModNPC is YingouHand hand && npc.ai[0] == NPC.whoAmI)
                {
                    if (npc.ai[1] > 0) //右手
                        rightHand = hand;
                    else if (npc.ai[1] < 0) //左手
                        leftHand = hand;
                }
            }
            
            if (leftHand != null && rightHand != null)
                handsInitialized = true;
        }

        //双手动作指挥方法
        private void CommandBothHands(YingouHand.ActionCommand action, Vector2? leftTarget = null, Vector2? rightTarget = null, float leftAngle = 0f, float rightAngle = 0f)
        {
            if (leftHand != null && leftHand.IsActionComplete())
                leftHand.ExecuteAction(action, leftTarget, leftAngle);
            if (rightHand != null && rightHand.IsActionComplete())
                rightHand.ExecuteAction(action, rightTarget, rightAngle);
            
            isPerformingAction = true;
            actionCooldown = 20; //防止过于频繁的动作
        }

        private void CommandLeftHand(YingouHand.ActionCommand action, Vector2? target = null, float angle = 0f)
        {
            if (leftHand != null && leftHand.IsActionComplete())
                leftHand.ExecuteAction(action, target, angle);
        }

        private void CommandRightHand(YingouHand.ActionCommand action, Vector2? target = null, float angle = 0f)
        {
            if (rightHand != null && rightHand.IsActionComplete())
                rightHand.ExecuteAction(action, target, angle);
        }

        private bool AreBothHandsReady()
        {
            return (leftHand?.IsActionComplete() ?? true) && (rightHand?.IsActionComplete() ?? true);
        }

        private bool ShouldTriggerProjectilesFromHands()
        {
            return true;
        }

        private void RunIntro(Player target) {
            //出场缓动：从远处扭曲漂移进入
            introAppear = ACMUtils.SineInOut(MathHelper.Clamp(PhaseTimer / 120f, 0, 1));
            Vector2 appearOffset = new Vector2(0, -600).RotatedBy(MathHelper.ToRadians(PhaseTimer * 2));
            Vector2 desired = target.Center + appearOffset * (1 - introAppear) + Vector2.Lerp(new Vector2(-300, -200), new Vector2(0, -120), ACMUtils.QuadOut(introAppear));
            NPC.Center += (desired - NPC.Center) * 0.12f;
            NPC.velocity *= 0.8f;

            //扭曲粒子
            if (!VaultUtils.isServer && PhaseTimer % 4 == 0) {
                for (int i = 0; i < 6; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(80, 80) * (1 - introAppear);
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.GoldFlame, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.6f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 2f + Main.rand.NextVector2Circular(1, 1);
                }
            }

            //屏幕聚焦 + 震动落点
            if (!didIntroShock && introAppear > 0.92f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 40);
                for (int k = 0; k < 40; k++) {
                    Vector2 vel = Main.rand.NextVector2Circular(12, 12);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 120, default, 2.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer > 150) {
                aitype = AttackAIStyle.Melee;
                TransitionTo(BossPhase.PatternSetA);
            }
        }

        private void RunPatternSetA(Player target) {
            //移动：缓慢侧滑逼近 + 偶发腾挪
            Vector2 baseDir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            Vector2 lateral = baseDir.RotatedBy(MathHelper.PiOver2 * swordDir) * MathF.Sin(PhaseTimer * 0.04f) * 6f;
            Vector2 desiredVel = baseDir * 10 + lateral;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.05f);

            //每段循环内放出一次扇形火球 - 现在配合双手动作
            if (PhaseTimer % 120 == 50 && AreBothHandsReady() && actionCooldown <= 0) {
                //指挥双手执行扇形斩击动作
                Vector2 fanDirection = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float fanAngle = fanDirection.ToRotation();
                
                CommandBothHands(
                    YingouHand.ActionCommand.FanFireSlash,
                    target.Center + fanDirection.RotatedBy(-0.3f) * 200, //左手目标位置
                    target.Center + fanDirection.RotatedBy(0.3f) * 200,  //右手目标位置
                    fanAngle - 0.3f, //左手角度
                    fanAngle + 0.3f  //右手角度
                );
            }

            //检查是否到了发射火球的时机
            if (PhaseTimer % 120 == 60) {
                DoFanFire(target, 6 + (Main.expertMode ? 2 : 0) + (Main.masterMode ? 2 : 0), 70, 18f, 22f);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 18);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
            }

            //经过时间转向螺旋阶段
            if (PhaseTimer > 600) {
                YingouFireBall.KillAll();
                TransitionTo(BossPhase.SpiralDread);
                aitype = AttackAIStyle.Circle;
                circleCounter = 0;
                circlespeed = 0;
            }
        }

        private void DoFanFire(Player target, int fireballCount, float totalSpreadDeg, float minSpeed, float maxSpeed) {
            if (VaultUtils.isClient) return;
            float spread = MathHelper.ToRadians(totalSpreadDeg);
            float baseAngle = NPC.DirectionTo(target.Center).ToRotation();
            for (int i = 0; i < fireballCount; i++) {
                float angleOffset = MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(fireballCount - 1));
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
                Vector2 velocity = baseAngle.ToRotationVector2().RotatedBy(angleOffset) * speed;
                float power = i * 0.15f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(), 2f, Main.myPlayer, 0, 0, power);
            }
        }

        private void RunSpiral(Player target) {
            spiralPulse += 0.03f;
            circlespeed = MathHelper.Lerp(circlespeed, 1.4f, 0.01f);
            circleCounter += circlespeed * 0.16f;

            float radius = 1380 + MathF.Sin(spiralPulse * 2) * 90f * ACMUtils.SineInOut(MathF.Sin(spiralPulse));
            Vector2 dest = target.Center + (circleCounter * swordDir).ToRotationVector2() * radius;
            NPC.Center += (dest - NPC.Center) * 0.08f;
            NPC.velocity *= 0.8f;

            if (PhaseTimer % 90 == 10 && AreBothHandsReady() && actionCooldown <= 0) {
                //双手准备释放径向脉冲
                CommandBothHands(YingouHand.ActionCommand.SpinCast);
            }

            if (PhaseTimer % 150 == 70 && AreBothHandsReady() && actionCooldown <= 0) {
                //追踪弧形火球准备
                Vector2 targetDir = NPC.DirectionTo(target.Center);
                CommandBothHands(
                    YingouHand.ActionCommand.SweepSlash,
                    null, null,
                    targetDir.ToRotation() - 0.5f,
                    targetDir.ToRotation() + 0.5f
                );
            }

            //持续的单发攻击配合快速突刺
            if (PhaseTimer % 30 == 0 && AreBothHandsReady()) {
                Vector2 strikeDir = NPC.Center.To(target.Center).UnitVector();
                CommandRightHand(YingouHand.ActionCommand.QuickStrike, target.Center, strikeDir.ToRotation());
            }
            if (PhaseTimer % 15 == 0) {
                Projectile.NewProjectile(NPC.GetSource_FromAI()
                    , target.Center + target.velocity.UnitVector() * 1160
                    , NPC.Center.To(target.Center).UnitVector() * 24,
                    ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.9f), 2);
            }

            if (PhaseTimer > 540) {
                TransitionTo(BossPhase.SaberHell);
                aitype = AttackAIStyle.Idle;
                saberCharge = 0;
            }
        }

        private void RunSaberHell(Player target) {
            //蓄力 -> 连续多段释放
            saberCharge = MathHelper.Clamp(saberCharge + 0.012f, 0, 1);
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(0, -400 + MathF.Sin(PhaseTimer * 0.05f) * 30);
            NPC.Center += (hover - NPC.Center) * 0.05f;

            //充能粒子
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(160, 160) * saberCharge;
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.PurpleTorch, 0, 0, 120, default, Main.rand.NextFloat(1.6f, 2.7f));
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 4f * Main.rand.NextFloat(0.4f, 1f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 120) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 36);
            }

            //多图案轮换：每 70 tick 尝试触发一次（首段延迟 140）
            //现在每个图案都有对应的动作预演
            if (PhaseTimer > 140 && PhaseTimer - lastSaberPatternTime >= 70 && saberPatternIndex < saberPatternsPerPhase) {
                PrepareSaberPattern(target, saberPatternIndex % 5);
            }

            //检查是否到了实际发射的时机
            if (PhaseTimer > 140 && PhaseTimer - lastSaberPatternTime >= 70) {
                PerformNextSaberPattern(target);
            }

            if (PhaseTimer > 420) TransitionTo(BossPhase.FrenzyDash); //接入新冲刺阶段
        }

        private void PrepareSaberPattern(Player target, int patternIndex)
        {
            if (!AreBothHandsReady() || actionCooldown > 0) return;

            switch (patternIndex)
            {
                case 0: //RingDouble - 环形施法
                    CommandBothHands(YingouHand.ActionCommand.RingCast);
                    break;
                case 1: //CrossSweep - 十字斩击
                    Vector2 crossDir = NPC.DirectionTo(target.Center);
                    CommandBothHands(
                        YingouHand.ActionCommand.CrossSlash,
                        null, null,
                        crossDir.ToRotation(),
                        crossDir.ToRotation()
                    );
                    break;
                case 2: //RotatingBlades - 旋转施法
                    CommandBothHands(YingouHand.ActionCommand.SpinCast);
                    break;
                case 3: //ConvergingSpokes - 蓄力突刺
                    CommandBothHands(
                        YingouHand.ActionCommand.ChargeStab,
                        target.Center + new Vector2(-100, 0),
                        target.Center + new Vector2(100, 0),
                        NPC.DirectionTo(target.Center).ToRotation(),
                        NPC.DirectionTo(target.Center).ToRotation()
                    );
                    break;
                case 4: //AimedWaveBursts - 扇形连斩
                    Vector2 waveDir = NPC.DirectionTo(target.Center);
                    CommandBothHands(
                        YingouHand.ActionCommand.SweepSlash,
                        null, null,
                        waveDir.ToRotation() - 0.4f,
                        waveDir.ToRotation() + 0.4f
                    );
                    break;
            }
        }

        private void RunBladeScatter(Player target) {
            bladeScatterCharge = MathHelper.Clamp(bladeScatterCharge + 0.01f, 0, 1);
            Vector2 focus = target.Center + new Vector2(0, -260 + (float)Math.Sin(PhaseTimer * 0.05f) * 26f);
            NPC.Center += (focus - NPC.Center) * 0.12f;
            NPC.velocity *= 0.85f;

            //Telegraph 环光
            if (!VaultUtils.isServer && PhaseTimer % 5 == 0) {
                for (int i = 0; i < 8; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(180, 180) * bladeScatterCharge;
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.GoldFlame, 0, 0, 140, default, 1.4f);
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 3f;
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 1) SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);

            //为每次散射准备动作
            if ((PhaseTimer == 80 || PhaseTimer == 100 || PhaseTimer == 120) && AreBothHandsReady()) {
                CommandBothHands(
                    YingouHand.ActionCommand.ChargeStab,
                    target.Center + VaultUtils.RandVr(400, 600),
                    target.Center + VaultUtils.RandVr(400, 600),
                    -MathHelper.PiOver2,
                    -MathHelper.PiOver2
                );
            }

            if ((PhaseTimer == 90 || PhaseTimer == 110 || PhaseTimer == 130) && ShouldTriggerProjectilesFromHands()) {
                //3 轮环形散射，角度错列
                float startRot = (PhaseTimer == 90 ? 0f : (PhaseTimer == 110 ? 0.12f : 0.26f));
                int count = 22 + (Main.expertMode ? 4 : 0);
                float speed = 18f + (PhaseTimer - 90) * 0.2f;
                ShootScatterRing(target.Center + VaultUtils.RandVr(660, 820), count, speed, startRot);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 18);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.35f - (PhaseTimer - 90) * 0.01f, Volume = 1.1f }, NPC.Center);
            }

            if (PhaseTimer > 170) TransitionTo(BossPhase.RecoverDash);
        }

        private void ShootScatterRing(Vector2 center, int count, float speed, float startRotation) {
            if (VaultUtils.isClient) return;
            for (int i = 0; i < count; i++) {
                float ang = startRotation + MathHelper.TwoPi * i / count;
                Vector2 vel = ang.ToRotationVector2() * speed;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), center, vel,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.85f), 2f, Main.myPlayer, 0, 0, Main.rand.NextFloat(-1f, 1f));
            }
        }

        private void PerformNextSaberPattern(Player target) {
            lastSaberPatternTime = (int)PhaseTimer;
            switch (saberPatternIndex % 5) {
                case 0:
                    Pattern_RingDouble(target);
                    break;
                case 1:
                    Pattern_CrossSweep(target);
                    break;
                case 2:
                    Pattern_RotatingBlades(target);
                    break;
                case 3:
                    Pattern_ConvergingSpokes(target);
                    break;
                case 4:
                    Pattern_AimedWaveBursts(target);
                    break;
            }
            saberPatternIndex++;
            NPC.netUpdate = true;
        }

        //原始环形 + 内环
        private void Pattern_RingDouble(Player target) {
            if (VaultUtils.isClient) return;
            Vector2 basePos = target.Center;
            for (int ring = 0; ring < 2; ring++) {
                int slice = 6 + ring * 2;
                for (int i = 0; i < slice; i++) {
                    float ang = MathHelper.TwoPi * i / slice + ring * 0.15f;
                    Vector2 dir = ang.ToRotationVector2();
                    Vector2 spawn = basePos + dir * (260 + ring * 80);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, -dir * 10,
                        ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.9f), 2);
                }
            }
            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 18);
            SoundEngine.PlaySound(SoundID.Item71 with { PitchVariance = 0.2f, Volume = 1f }, target.Center);
        }

        //十字+斜十字扫线（延迟出现的刀幕）
        private void Pattern_CrossSweep(Player target) {
            if (VaultUtils.isClient) return;
            int lines = 8; //4 条正交 + 4 条斜线
            for (int i = 0; i < lines; i++) {
                float ang = MathHelper.PiOver4 * i; //45° 递增
                Vector2 dir = ang.ToRotationVector2();
                Vector2 spawn = target.Center + dir * 600;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, -dir * 24,
                    ModContent.ProjectileType<SaberHell>(), GetBossDamage(1f), 2);
            }
            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(9, 22);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.1f }, target.Center);
        }

        //旋转刀阵（外圈绕玩家旋转后内收）
        private void Pattern_RotatingBlades(Player target) {
            if (VaultUtils.isClient) return;
            int bladeCount = 10;
            float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < bladeCount; i++) {
                float ang = baseRot + MathHelper.TwoPi * i / bladeCount;
                Vector2 spawn = target.Center + ang.ToRotationVector2() * 480f;
                Vector2 vel = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * -20f; //先切向旋转
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, vel,
                    ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.85f), 2);
                if (p >= 0 && p < Main.maxProjectiles) {
                    Main.projectile[p].localAI[0] = -60; //利用负计时表示旋转阶段
                    Main.projectile[p].ai[0] = target.Center.X;
                    Main.projectile[p].ai[1] = target.Center.Y;
                }
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.25f, Volume = 0.9f }, target.Center);
        }

        //辐射收束：多条外向->停顿->反向回扑
        private void Pattern_ConvergingSpokes(Player target) {
            if (VaultUtils.isClient) return;
            int spokes = 12;
            for (int i = 0; i < spokes; i++) {
                float ang = MathHelper.TwoPi * i / spokes;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 spawn = target.Center + dir * 140;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, dir * 28,
                    ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.75f), 2);
                if (p >= 0 && p < Main.maxProjectiles) Main.projectile[p].localAI[0] = -30; //先向外延伸再回收
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.1f, Volume = 1.05f }, target.Center);
        }

        //多段朝向玩家的波状 burst
        private void Pattern_AimedWaveBursts(Player target) {
            if (VaultUtils.isClient) return;
            int waves = 3;
            for (int w = 0; w < waves; w++) {
                for (int i = -2; i <= 2; i++) {
                    float offsetAng = i * 0.11f + w * 0.05f;
                    Vector2 dir = NPC.DirectionTo(target.Center).RotatedBy(offsetAng);
                    Vector2 spawn = target.Center + dir.RotatedBy(MathHelper.PiOver2) * (i * 70) + new Vector2(0, -300 - w * 120);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, dir * 22f,
                        ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.8f), 2);
                }
            }
            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 20);
            SoundEngine.PlaySound(SoundID.Item71 with { PitchVariance = 0.3f, Volume = 1f }, target.Center);
        }

        private void RunRecoverDash(Player target) {
            //强力冲刺 + 回到 PatternSetA
            if (PhaseTimer == 1) {
                Vector2 dashDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));
                NPC.velocity = dashDir * 30f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 24);
            }
            NPC.velocity *= 0.985f;
            if (PhaseTimer > 40) {
                PhaseLoopCounter++;
                //循环选择下一个阶段交错
                if (PhaseLoopCounter % 3 == 0) {
                    TransitionTo(BossPhase.FrenzyDash);
                }
                else if (PhaseLoopCounter % 2 == 0) {
                    TransitionTo(BossPhase.BladeScatter);
                }
                else {
                    TransitionTo(BossPhase.PatternSetA);
                }
            }
        }

        internal int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = getOrigDamage ? NPC.defDamage : NPC.damage;
            return (int)(num * scaling);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[NPC.type].Value;
            float sengs = 0.25f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, null, drawColor * sengs, 0, mainValue.Size() / 2, NPC.scale * (0.9f + 0.1f * sengs), SpriteEffects.None, 0);
                sengs *= 0.75f;
            }
            float scale = NPC.scale;
            if (Phase == BossPhase.BladeScatter && PhaseTimer < 90) {
                float chargeT = MathHelper.Clamp(PhaseTimer / 90f, 0, 1);
                float pulse = 1f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8);
                scale *= MathHelper.Lerp(1f, 1.15f, ACMUtils.SineInOut(chargeT)) * pulse;
                //绘制发光圆
                if (SoftGlow != null) {
                    for (int i = 0; i < 3; i++) {
                        float gScale = 2.2f + i * 0.3f * chargeT;
                        Color gCol = Color.Lerp(Color.OrangeRed, Color.Gold, 0.5f + 0.5f * (float)Math.Sin(chargeT * Math.PI)) * (0.5f - 0.15f * i);
                        gCol.A = 0;
                        spriteBatch.Draw(SoftGlow, NPC.Center - Main.screenPosition, null, gCol, 0, SoftGlow.Size() / 2, gScale, SpriteEffects.None, 0);
                    }
                }
            }
            float introScale = Phase == BossPhase.Intro ? MathHelper.Lerp(0.6f, 1f, ACMUtils.BackOut(introAppear)) : scale;
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, null, drawColor, NPC.rotation, mainValue.Size() / 2, introScale, SpriteEffects.None);
            return false;
        }

        private void RunFrenzyDash(Player target) {
            //Telegraph -> Dash -> Recover，重复 frenzyDashTotal 次
            frenzyDashStateTimer++;
            switch (frenzyDashState) {
                case 0: //telegraph
                    NPC.velocity *= 0.85f;
                    Vector2 hover = target.Center + new Vector2(0, -680).RotatedBy(frenzyDashTelegraphAngle);
                    NPC.Center += (hover - NPC.Center) * 0.16f;
                    if (!VaultUtils.isServer && frenzyDashStateTimer % 6 == 0) {
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 150, default, 2f);
                        Main.dust[dust].noGravity = true; Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                    }
                    if (frenzyDashStateTimer == 36) {
                        frenzyDashDir = NPC.DirectionTo(target.Center).RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f));
                        NPC.velocity = frenzyDashDir * 36f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f, MaxInstances = 6 }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 18);
                        frenzyDashState = 1; frenzyDashStateTimer = 0;
                        DoFanFire(target, 6 + (Main.expertMode ? 2 : 0) + (Main.masterMode ? 2 : 0), 70, 18f, 22f);
                    }
                    break;
                case 1: //dash
                    NPC.velocity *= 0.985f;
                    if (frenzyDashStateTimer > 42 || NPC.collideX || NPC.collideY) {
                        frenzyDashState = 2; frenzyDashStateTimer = 0; NPC.velocity *= 0.4f;
                    }
                    break;
                case 2: //recover
                    NPC.velocity *= 0.9f;
                    if (frenzyDashStateTimer > 18) {
                        frenzyDashCount++;
                        if (frenzyDashCount >= frenzyDashTotal) {
                            TransitionTo(BossPhase.BladeScatter);
                        } else {
                            frenzyDashState = 0; frenzyDashStateTimer = 0; swordDir *= -1; NPC.netUpdate = true;
                            if (!VaultUtils.isClient) {
                                frenzyDashTelegraphAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            }
                        }
                    }
                    break;
            }
        }
    }
}

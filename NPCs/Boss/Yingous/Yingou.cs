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

        //===== 手臂动作挂起标志 =====
        private bool pendingFanFire;
        private bool pendingRadialPulse;
        private bool pendingTrackingArc;
        private bool pendingSingleStrike;
        private bool pendingSaberPattern;
        private bool pendingScatter1;
        private bool pendingScatter2;
        private bool pendingScatter3;

        //===== 侵略性动作系统 =====
        private int aggressiveActionTimer = 0;
        private int lastAggressiveAction = 0;
        private bool isInAggressiveCombo = false;
        private int aggressiveComboCount = 0;

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
            //重置所有挂起标志
            pendingFanFire = pendingRadialPulse = pendingTrackingArc = false;
            pendingSingleStrike = pendingSaberPattern = false;
            pendingScatter1 = pendingScatter2 = pendingScatter3 = false;
            //重置侵略性动作状态
            aggressiveActionTimer = 0;
            isInAggressiveCombo = false;
            aggressiveComboCount = 0;
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
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, 1);
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, -1);
                }
            }

            Main.dayTime = false;

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
            aggressiveActionTimer++;

            //更新动作冷却
            if (actionCooldown > 0) actionCooldown--;

            //持续性侵略动作系统 - 贯穿所有阶段
            ProcessAggressiveActions(target);

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
            //移动：稍微降低侵略性的逼近
            Vector2 baseDir = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            Vector2 lateral = baseDir.RotatedBy(MathHelper.PiOver2 * swordDir) * MathF.Sin(PhaseTimer * 0.05f) * 6f; //减少侧移幅度和频率
            Vector2 desiredVel = baseDir * 11f + lateral; //降低逼近速度从15到11
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.06f); //降低加速度从0.08到0.06

            //适当降低主要攻击频率 - 从80改为90tick周期
            if (PhaseTimer % 90 == 30 && AreBothHandsReady() && actionCooldown <= 0) {
                //指挥双手执行更华丽的斩击动作
                Vector2 fanDirection = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float fanAngle = fanDirection.ToRotation();
                
                //随机选择华丽的攻击模式
                int attackPattern = Main.rand.Next(0, 3);
                switch (attackPattern)
                {
                    case 0: //经典扇形斩击
                        CommandBothHands(
                            YingouHand.ActionCommand.FanFireSlash,
                            target.Center + fanDirection.RotatedBy(-0.4f) * 180,
                            target.Center + fanDirection.RotatedBy(0.4f) * 180,
                            fanAngle - 0.4f,
                            fanAngle + 0.4f
                        );
                        break;
                        
                    case 1: //交叉突刺
                        CommandBothHands(
                            YingouHand.ActionCommand.ChargeStab,
                            target.Center + fanDirection.RotatedBy(-0.3f) * 200,
                            target.Center + fanDirection.RotatedBy(0.3f) * 200,
                            fanAngle - 0.3f,
                            fanAngle + 0.3f
                        );
                        break;
                        
                    case 2: //花式旋转攻击
                        CommandBothHands(YingouHand.ActionCommand.SpinCast);
                        break;
                }
            }

            //检查是否到了发射火球的时机
            if (PhaseTimer % 90 == 55) { //调整时机避免与主攻击重叠
                DoFanFire(target, 7 + (Main.expertMode ? 2 : 0) + (Main.masterMode ? 2 : 0), 75, 18f, 24f); //稍微降低弹幕密度和速度
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 20); //降低震动强度
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center); //降低音量
            }

            //降低随机小规模攻击频率
            if (PhaseTimer % 50 == 25 && AreBothHandsReady()) //从40改为50tick
            {
                Vector2 strikeDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                if (Main.rand.NextBool(3)) //降低概率从50%到33%
                {
                    CommandRightHand(YingouHand.ActionCommand.QuickStrike, 
                        target.Center + strikeDir * 120, strikeDir.ToRotation());
                }
            }

            //经过时间转向螺旋阶段 - 稍微缩短持续时间增加节奏感
            if (PhaseTimer > 480) {
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
            spiralPulse += 0.035f; //稍微降低脉冲频率
            circlespeed = MathHelper.Lerp(circlespeed, 1.5f, 0.012f); //降低旋转速度从1.8到1.5
            circleCounter += circlespeed * 0.08f; //降低旋转频率

            float radius = 1300 + MathF.Sin(spiralPulse * 2.2f) * 100f * ACMUtils.SineInOut(MathF.Sin(spiralPulse)); //增加半径并降低波动
            Vector2 dest = target.Center + (circleCounter * swordDir).ToRotationVector2() * radius;
            NPC.Center += (dest - NPC.Center) * 0.09f; //降低移动响应速度从0.12到0.09
            NPC.velocity *= 0.8f; //增加阻力

            //降低旋转攻击频率
            if (PhaseTimer % 80 == 8 && AreBothHandsReady() && actionCooldown <= 0) { //从60改为80tick
                //双手交替旋转施法，营造华丽的视觉效果
                if ((PhaseTimer / 80) % 2 == 0)
                    CommandLeftHand(YingouHand.ActionCommand.SpinCast);
                else
                    CommandRightHand(YingouHand.ActionCommand.SpinCast);
            }

            //降低横扫攻击频率
            if (PhaseTimer % 110 == 40 && AreBothHandsReady() && actionCooldown <= 0) { //从90改为110tick
                Vector2 targetDir = NPC.DirectionTo(target.Center);
                CommandBothHands(
                    YingouHand.ActionCommand.SweepSlash,
                    null, null,
                    targetDir.ToRotation() - 0.6f,
                    targetDir.ToRotation() + 0.6f
                );
            }

            //适当降低连续突刺频率
            if (PhaseTimer % 25 == 0 && AreBothHandsReady()) { //从20改为25tick
                Vector2 strikeDir = NPC.Center.To(target.Center).UnitVector();
                //交替使用双手进行连续突刺
                if (PhaseTimer % 50 == 0) //从40改为50tick
                    CommandLeftHand(YingouHand.ActionCommand.QuickStrike, target.Center, strikeDir.ToRotation());
                else if (PhaseTimer % 50 == 25)
                    CommandRightHand(YingouHand.ActionCommand.QuickStrike, target.Center, strikeDir.ToRotation());
            }

            //适当降低随机华丽动作频率
            if (PhaseTimer % 140 == 90 && AreBothHandsReady()) //从120改为140tick
            {
                int flashyAction = Main.rand.Next(0, 3);
                switch (flashyAction)
                {
                    case 0: //十字斩击
                        Vector2 crossDir = NPC.DirectionTo(target.Center);
                        CommandBothHands(YingouHand.ActionCommand.CrossSlash, null, null, crossDir.ToRotation(), crossDir.ToRotation());
                        break;
                    case 1: //蓄力突刺
                        Vector2 chargeDir = NPC.DirectionTo(target.Center);
                        CommandBothHands(YingouHand.ActionCommand.ChargeStab, target.Center, target.Center, chargeDir.ToRotation(), chargeDir.ToRotation());
                        break;
                    case 2: //双重旋转
                        CommandBothHands(YingouHand.ActionCommand.SpinCast);
                        break;
                }
            }

            if (PhaseTimer > 420) { //缩短阶段时间增加节奏
                TransitionTo(BossPhase.SaberHell);
                aitype = AttackAIStyle.Idle;
                saberCharge = 0;
            }
        }

        private void RunSaberHell(Player target) {
            //蓄力 -> 连续多段释放
            saberCharge = MathHelper.Clamp(saberCharge + 0.015f, 0, 1); //稍微降低蓄力速度
            NPC.velocity *= 0.88f; //稍微增加阻力

            //稍微调整悬浮位置 - 降低侵略性
            Vector2 hover = target.Center + new Vector2(0, -380 + MathF.Sin(PhaseTimer * 0.06f) * 40); //增加高度，降低波动
            Vector2 lateralDrift = new Vector2(MathF.Sin(PhaseTimer * 0.035f) * 60, 0); //降低侧向漂移
            NPC.Center += ((hover + lateralDrift) - NPC.Center) * 0.06f; //降低移动速度

            //充能粒子 - 更华丽
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) { //稍微减少粒子数量
                    Vector2 off = Main.rand.NextVector2CircularEdge(160, 160) * saberCharge; //稍微减小范围
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.PurpleTorch, 0, 0, 120, default, Main.rand.NextFloat(1.6f, 2.8f));
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 4f * Main.rand.NextFloat(0.6f, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 100) { //稍微延后蓄力完成时间，给玩家更多准备时间
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center); //降低音量
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 35); //降低震动强度
            }

            //适当降低威慑性动作展示频率
            if (PhaseTimer % 35 == 0 && PhaseTimer < 130 && AreBothHandsReady()) //从30改为35tick，从120改为130
            {
                int intimidationAction = (int)((PhaseTimer / 35) % 4);
                switch (intimidationAction)
                {
                    case 0:
                        CommandLeftHand(YingouHand.ActionCommand.SaberCast);
                        break;
                    case 1:
                        CommandRightHand(YingouHand.ActionCommand.SaberCast);
                        break;
                    case 2:
                        CommandBothHands(YingouHand.ActionCommand.RingCast);
                        break;
                    case 3:
                        CommandBothHands(YingouHand.ActionCommand.SpinCast);
                        break;
                }
            }

            //多图案轮换：稍微放慢节奏 - 每 60 tick 触发
            if (PhaseTimer > 130 && PhaseTimer - lastSaberPatternTime >= 60 && saberPatternIndex < saberPatternsPerPhase) { //从50改为60tick
                PrepareSaberPattern(target, saberPatternIndex % 5);
            }

            //检查是否到了实际发射的时机
            if (PhaseTimer > 130 && PhaseTimer - lastSaberPatternTime >= 60) {
                PerformNextSaberPattern(target);
            }

            if (PhaseTimer > 380) TransitionTo(BossPhase.FrenzyDash); //稍微延长阶段时间
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
            bladeScatterCharge = MathHelper.Clamp(bladeScatterCharge + 0.012f, 0, 1); //降低蓄力速度
            Vector2 focus = target.Center + new Vector2(0, -250 + (float)Math.Sin(PhaseTimer * 0.06f) * 30f); //稍微增加距离和降低波动
            NPC.Center += (focus - NPC.Center) * 0.12f; //稍微降低移动速度
            NPC.velocity *= 0.82f; //增加阻力

            //Telegraph 环光 - 更华丽
            if (!VaultUtils.isServer && PhaseTimer % 4 == 0) { //降低粒子频率
                for (int i = 0; i < 10; i++) { //稍微减少粒子数量
                    Vector2 off = Main.rand.NextVector2CircularEdge(180, 180) * bladeScatterCharge; //稍微减小范围
                    int dust = Dust.NewDust(NPC.Center + off, 0, 0, DustID.GoldFlame, 0, 0, 140, default, 1.4f);
                    Main.dust[dust].velocity = -off.SafeNormalize(Vector2.Zero) * 3.5f;
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 1) SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.0f }, NPC.Center); //降低音量

            //蓄力过程中的威慑动作
            if (PhaseTimer % 30 == 0 && PhaseTimer < 80 && AreBothHandsReady()) //稍微延长间隔
            {
                int chargeAction = (int)((PhaseTimer / 30) % 3);
                switch (chargeAction)
                {
                    case 0:
                        CommandLeftHand(YingouHand.ActionCommand.RingCast);
                        break;
                    case 1:
                        CommandRightHand(YingouHand.ActionCommand.RingCast);
                        break;
                    case 2:
                        CommandBothHands(YingouHand.ActionCommand.SpinCast);
                        break;
                }
            }

            //为每次散射准备更华丽的动作 - 提前更多时间
            if ((PhaseTimer == 70 || PhaseTimer == 85 || PhaseTimer == 100) && AreBothHandsReady()) { //稍微延后时机
                CommandBothHands(
                    YingouHand.ActionCommand.ChargeStab,
                    target.Center + VaultUtils.RandVr(300, 500), //稍微调整距离
                    target.Center + VaultUtils.RandVr(300, 500),
                    -MathHelper.PiOver2,
                    -MathHelper.PiOver2
                );
            }

            if ((PhaseTimer == 85 || PhaseTimer == 100 || PhaseTimer == 115) && ShouldTriggerProjectilesFromHands()) { //稍微延后时机
                //3 轮环形散射，角度错列 - 稍微降低密度
                float startRot = (PhaseTimer == 85 ? 0f : (PhaseTimer == 100 ? 0.15f : 0.3f));
                int count = 22 + (Main.expertMode ? 4 : 0) + (Main.masterMode ? 3 : 0); //降低弹幕数量
                float speed = 18f + (PhaseTimer - 85) * 0.25f; //降低速度
                ShootScatterRing(target.Center + VaultUtils.RandVr(550, 700), count, speed, startRot);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 20); //降低震动强度
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f - (PhaseTimer - 85) * 0.012f, Volume = 1.2f }, NPC.Center); //降低音量
            }

            if (PhaseTimer > 140) TransitionTo(BossPhase.RecoverDash); //稍微延长阶段时间
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
                spriteBatch.Draw(mainValue, drawOldPos, null, Color.White * sengs, 0, mainValue.Size() / 2, NPC.scale * (0.9f + 0.1f * sengs), SpriteEffects.None, 0);
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
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, null, Color.White, NPC.rotation, mainValue.Size() / 2, introScale, SpriteEffects.None);
            return false;
        }

        private void RunFrenzyDash(Player target) {
            //Telegraph -> Dash -> Recover，重复 frenzyDashTotal 次
            frenzyDashStateTimer++;
            switch (frenzyDashState) {
                case 0: //telegraph
                    NPC.velocity *= 0.88f; //稍微增加阻力
                    Vector2 hover = target.Center + new Vector2(0, -720).RotatedBy(frenzyDashTelegraphAngle); //稍微增加距离
                    NPC.Center += (hover - NPC.Center) * 0.14f; //稍微降低移动速度
                    if (!VaultUtils.isServer && frenzyDashStateTimer % 7 == 0) { //降低粒子频率
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 150, default, 1.8f);
                        Main.dust[dust].noGravity = true; Main.dust[dust].velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                    }
                    if (frenzyDashStateTimer == 40) { //稍微延长预告时间
                        frenzyDashDir = NPC.DirectionTo(target.Center).RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));
                        NPC.velocity = frenzyDashDir * 32f; //稍微降低冲刺速度
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.15f, Volume = 0.8f, MaxInstances = 6 }, NPC.Center); //降低音量
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 16); //降低震动强度
                        frenzyDashState = 1; frenzyDashStateTimer = 0;
                        DoFanFire(target, 5 + (Main.expertMode ? 2 : 0) + (Main.masterMode ? 1 : 0), 60, 16f, 20f); //降低弹幕数量和速度
                    }
                    break;
                case 1: //dash
                    NPC.velocity *= 0.987f; //稍微增加阻力
                    if (frenzyDashStateTimer > 45 || NPC.collideX || NPC.collideY) { //稍微延长冲刺时间
                        frenzyDashState = 2; frenzyDashStateTimer = 0; NPC.velocity *= 0.35f;
                    }
                    break;
                case 2: //recover
                    NPC.velocity *= 0.92f; //稍微增加阻力
                    if (frenzyDashStateTimer > 20) { //稍微延长恢复时间
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

        //===== 侵略性动作系统 =====
        private void ProcessAggressiveActions(Player target)
        {
            //跳过intro阶段的侵略动作
            if (Phase == BossPhase.Intro) return;

            //根据距离和阶段调整侵略性
            float distToPlayer = Vector2.Distance(NPC.Center, target.Center);
            bool closeRange = distToPlayer < 350f; //稍微减小近距离判定范围
            bool mediumRange = distToPlayer < 750f; //稍微减小中距离判定范围

            //降低动作触发频率 - 每20-35tick检查一次
            int actionInterval = closeRange ? 20 : (mediumRange ? 25 : 35); //增加间隔时间
            
            if (aggressiveActionTimer >= actionInterval && AreBothHandsReady())
            {
                //根据情况选择侵略动作
                if (closeRange && !isInAggressiveCombo)
                {
                    StartAggressiveCombo(target);
                }
                else if (mediumRange)
                {
                    ExecuteMediumRangeAggression(target);
                }
                else
                {
                    ExecuteLongRangeAggression(target);
                }
                
                aggressiveActionTimer = 0;
                lastAggressiveAction = (int)PhaseTimer;
            }

            //处理连击状态 - 延长连击间隔
            if (isInAggressiveCombo && PhaseTimer - lastAggressiveAction > 40) //从30增加到40
            {
                ContinueAggressiveCombo(target);
            }
        }

        private void ExecuteLongRangeAggression(Player target) {
            //远距离威慑性动作
            int actionType = Main.rand.Next(0, 3);
            switch (actionType) {
                case 0: //远程施法姿态
                    CommandBothHands(YingouHand.ActionCommand.SaberCast);
                    break;

                case 1: //环形施法威慑
                    CommandBothHands(YingouHand.ActionCommand.RingCast);
                    break;

                case 2: //旋转展示
                    if (Main.rand.NextBool())
                        CommandLeftHand(YingouHand.ActionCommand.SpinCast);
                    else
                        CommandRightHand(YingouHand.ActionCommand.SpinCast);
                    break;
            }
        }

        private void StartAggressiveCombo(Player target)
        {
            isInAggressiveCombo = true;
            aggressiveComboCount = 0;
            
            //开场强势双刀交叉斩
            Vector2 playerDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float baseAngle = playerDir.ToRotation();
            
            CommandBothHands(
                YingouHand.ActionCommand.CrossSlash,
                target.Center + playerDir * 60, //稍微增加距离
                target.Center + playerDir * 60,
                baseAngle,
                baseAngle
            );
            
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.0f, Pitch = 0.3f }, NPC.Center); //降低音量
            aggressiveComboCount++;
        }

        private void ContinueAggressiveCombo(Player target)
        {
            if (aggressiveComboCount >= 3) //降低连击长度从4到3
            {
                isInAggressiveCombo = false;
                aggressiveComboCount = 0;
                return;
            }

            Vector2 playerDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float baseAngle = playerDir.ToRotation();
            
            switch (aggressiveComboCount)
            {
                case 1: //左手快刺
                    CommandLeftHand(YingouHand.ActionCommand.QuickStrike, 
                        target.Center + playerDir.RotatedBy(-0.25f) * 90, //稍微增加距离和降低角度
                        baseAngle - 0.25f);
                    break;
                    
                case 2: //右手横扫
                    CommandRightHand(YingouHand.ActionCommand.SweepSlash, 
                        target.Center + playerDir.RotatedBy(0.35f) * 110, //稍微增加距离和降低角度
                        baseAngle + 0.35f);
                    break;
            }
            
            aggressiveComboCount++;
            lastAggressiveAction = (int)PhaseTimer;
        }

        private void ExecuteMediumRangeAggression(Player target)
        {
            Vector2 playerDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float baseAngle = playerDir.ToRotation();
            
            //中距离快速突进斩击
            int actionType = Main.rand.Next(0, 3);
            switch (actionType)
            {
                case 0: //双刀蓄力突刺
                    CommandBothHands(
                        YingouHand.ActionCommand.ChargeStab,
                        target.Center + playerDir * 170, //稍微增加距离
                        target.Center + playerDir * 170,
                        baseAngle,
                        baseAngle
                    );
                    break;
                    
                case 1: //交替快刺
                    if (Main.rand.NextBool())
                    {
                        CommandLeftHand(YingouHand.ActionCommand.QuickStrike, target.Center + playerDir * 30, baseAngle); //增加缓冲距离
                    }
                    else
                    {
                        CommandRightHand(YingouHand.ActionCommand.QuickStrike, target.Center + playerDir * 30, baseAngle);
                    }
                    break;
                    
                case 2: //扇形斩击
                    CommandBothHands(
                        YingouHand.ActionCommand.FanFireSlash,
                        target.Center + playerDir.RotatedBy(-0.15f) * 220, //稍微增加距离和降低角度
                        target.Center + playerDir.RotatedBy(0.15f) * 220,
                        baseAngle - 0.15f,
                        baseAngle + 0.15f
                    );
                    break;
            }
        }
    }
}

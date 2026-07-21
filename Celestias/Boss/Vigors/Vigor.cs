using AncientChineseMythology.Celestias.Boss.Vigors.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 神威·断罪刃 / Vigor, the Sin-Severing Blade
    /// 天庭刑官 · 金焰巨手持斧钺 — 后月球领主 Boss (约200万HP)
    ///
    /// V3 核心机制:
    ///   1. 断罪符环 — 12 颗符文能量球环绕护体(吸伤诚实护盾) → 依次点亮蓄势(充能刻度可读) → 齐射/连锁引爆;
    ///      拆盾即减压: 护体期被打掉的球, 齐射时就少射几颗
    ///   2. 挥斧语法 — 收束(backswing) → 静默 → 爆发(poly20 斩击) → 余摆, 巨斧身体语言驱动一切"斩"类攻击
    ///   3. 手写轮替表 — 每阶段固定循环, 强弱交替, 绝不连抽同招
    ///   4. 三大演出 — 开庭入场 / 宣判庭启·天刑加冕换阶段 / 阖卷死亡(beep 加速→静默→裁决闪→溶解→终爆)
    ///
    /// 一阶段「听讼」: 断罪横扫+符印锁狱+冲锋判决+升空劈斩
    /// 二阶段「宣判」: 符环登场 — 连环斩+符环齐射+天降断罪+符环囚阵+格挡反击
    /// 三阶段「天刑」: 无间斩舞+四极封印+符环齐射Ⅱ+天罚执行+裁决风暴
    /// </summary>
    [AutoloadBossHead]
    public class Vigor : ModNPC
    {
        #region 常量

        internal const float Phase2Threshold = 0.60f;
        internal const float Phase3Threshold = 0.30f;

        private const int WardSlots = 12;          // 断罪符环槽位数
        private const float LeashDistance = 1400f; // 距离栓绳
        private const int SealFuse = 150;          // 符印默认引信

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,

            P1_Hub,
            P1_Sweep,
            P1_SealLock,
            P1_Charge,
            P1_RisingSlam,

            Trans2,

            P2_Hub,
            P2_ChainSlash,
            P2_WardVolley,
            P2_Descend,
            P2_Cage,
            P2_Counter,

            Trans3,

            P3_Hub,
            P3_Dance,
            P3_FourPillar,
            P3_WardVolley2,
            P3_Execution,
            P3_Storm,

            Death
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

        // —— 同步字段 (SendExtraAI) ——
        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private int attackCycleIndex;    // 手写轮替表游标
        private int chargeCount;         // 招式内重复段计数
        private int wardCharges;         // 断罪符环当前点亮数 0~12
        private bool isCounterReady;
        private bool counterTriggered;
        private bool deathFinished;      // 死亡演出完成, 允许真死
        private Vector2 chargeTarget;    // 锁定落点/冲刺终点

        // —— 本地字段 (各端由同步状态确定性驱动, 不需同步) ——
        private int counterCooldown;
        private int absorbCooldown;      // 符环吸伤间隔
        private int prevWardCharges;     // 检测符环增减 → 各端本地演出
        private float glowIntensity = 1f;

        // 挥斧语法
        private int swingTimer;
        private int swingDuration = 1;
        private int swingDir = 1;
        private float recoilRot;         // 后坐/余摆角
        private float recoilVel;         // 余摆角速度
        private bool contactDamage;      // 接触伤害窗口 (与视觉严格对齐)

        // 符环视觉
        private float wardOrbitRadius = 120f;
        private float wardOrbitTarget = 120f;
        private int wardPopFlash;        // 符环被击碎白闪

        // 冲刺预警线
        private int dashLineTimer;
        private int dashLineMax = 1;
        private Vector2 dashLineStart;
        private Vector2 dashLineDir = Vector2.UnitX;

        // ===== 断罪判决演出状态 (纯本地视觉, 客户端确定性驱动) =====
        private int hitstopTimer;          // 处决砸落"全屏定格": Boss 帧冻结
        private int verdictBloomTimer;     // 处决泛光寿命
        private int verdictBloomMax = 1;
        private Vector2 verdictBloomCenter;
        private float verdictBloomPeak;
        private float chargeRamp;          // 蓄力渐强泛光 0~1
        private int sealRunicTimer;        // 符印封锁区地纹寿命
        private float sealRunicPeak;
        private Vector2 sealRunicCenter;
        private float sealRunicRadius = 320f;
        private int execFlashTimer;        // 裁决闪 (全屏后处理) 寿命
        private int execFlashMax = 1;
        private float execFlashPeak;

        private struct JudgmentBeam { public Vector2 Start, End; public int Time, MaxTime; public float Width; }
        private readonly JudgmentBeam[] judgmentBeams = new JudgmentBeam[8];

        // 断罪法阵实例 (0=Boss随体充能环 1=地面锁定阵 2=入场/演出阵)
        private struct SigilInstance
        {
            public Vector2 Center;
            public float Radius, Progress, Charge, Flash, Intensity, SpinSeed;
            public int Segments;
        }
        private readonly SigilInstance[] sigils = new SigilInstance[3];

        // —— 天空联动 (VigorSky 读取) ——
        public float DeathDimForSky { get; private set; }    // 死亡压暗 0~1
        public float DeathFlashForSky { get; private set; }  // 终爆白闪 0~1

        // —— 着色器/贴图缓存 (参考 Xuanwu 写法: 静态 Asset 惰性 ImmediateLoad) ——
        private static Asset<Effect> sigilShaderRef;
        private static Asset<Effect> execFlashShaderRef;
        private static Asset<Texture2D> orbTextureRef;

        private static Effect SigilShader {
            get {
                if (Main.dedServ) return null;
                sigilShaderRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/VigorRunicSigil", AssetRequestMode.ImmediateLoad);
                return sigilShaderRef?.Value;
            }
        }

        private static Effect ExecFlashShader {
            get {
                if (Main.dedServ) return null;
                execFlashShaderRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/VigorExecutionFlash", AssetRequestMode.ImmediateLoad);
                return execFlashShaderRef?.Value;
            }
        }

        private static Texture2D OrbTexture {
            get {
                if (Main.dedServ) return null;
                orbTextureRef ??= ModContent.Request<Texture2D>(
                    "AncientChineseMythology/Celestias/Boss/Vigors/RunicEnergyOrbs", AssetRequestMode.ImmediateLoad);
                return orbTextureRef?.Value;
            }
        }

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 140;
            NPC.height = 180;
            NPC.damage = 200;
            NPC.defense = 85;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
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

        public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>()));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SinSeveringBlade>(),
                ModContent.ItemType<AureateVoidrender>(),
                ModContent.ItemType<VerdictSealHammer>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            attackCycleIndex = 0;
            wardCharges = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(attackCycleIndex);
            writer.Write(chargeCount);
            writer.Write(wardCharges);
            writer.Write(isCounterReady);
            writer.Write(counterTriggered);
            writer.Write(deathFinished);
            writer.WriteVector2(chargeTarget);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            attackCycleIndex = reader.ReadInt32();
            chargeCount = reader.ReadInt32();
            wardCharges = reader.ReadInt32();
            isCounterReady = reader.ReadBoolean();
            counterTriggered = reader.ReadBoolean();
            deathFinished = reader.ReadBoolean();
            chargeTarget = reader.ReadVector2();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        // 接触伤害窗口与视觉严格对齐 (公平阀门): 只有冲刺/俯冲/斩击窗口造成接触伤害
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => contactDamage;

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            // 格挡态: 大幅减伤并触发反击
            if (isCounterReady && counterCooldown <= 0) {
                modifiers.FinalDamage *= 0.15f;
                counterTriggered = true;
                counterCooldown = 30;
                NPC.netUpdate = true;
                return;
            }
            // 断罪符环: 点亮的球替 Boss 承伤 (诚实护盾 — 球亮=有盾, 打掉一颗少 45% 一次)
            if (WardShieldActive && wardCharges > 0 && absorbCooldown <= 0) {
                modifiers.FinalDamage *= 0.55f;
                wardCharges--;
                absorbCooldown = 8;
                NPC.netUpdate = true;
            }
        }

        private bool WardShieldActive =>
            IsPhase2 && Phase != BossPhase.Intro && Phase != BossPhase.Trans2 &&
            Phase != BossPhase.Trans3 && Phase != BossPhase.Death;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.BlueTorch, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        // 死亡演出拦截: 首次致死不真死, 进「阖卷」演出; 演出完成后放行
        public override bool CheckDead() {
            if (!deathFinished && Phase != BossPhase.Death) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Death);
                return false;
            }
            return true;
        }

        public override void OnKill() {
            DownedBossSystem.downedVigor = true;
            ACMScreenShakeSystem.Add(16f);
        }

        #endregion

        #region AI主循环

        public override void AI() {
            // 处决"全屏定格": Boss 帧冻结 (各端确定性), 期间维持泛光/震动
            if (hitstopTimer > 0) {
                hitstopTimer--;
                NPC.velocity = Vector2.Zero;
                PublishVerdictVisuals();
                return;
            }

            globalTime += 1f / 60f;
            if (counterCooldown > 0) counterCooldown--;
            if (absorbCooldown > 0) absorbCooldown--;
            TickVerdictTimers();
            TickSwing();
            DetectWardPop();
            chargeRamp = MathHelper.Lerp(chargeRamp, 0f, 0.08f);
            wardOrbitRadius = MathHelper.Lerp(wardOrbitRadius, wardOrbitTarget, 0.12f);
            contactDamage = false; // 各态自行开启

            // 死亡演出不依赖目标存活
            if (Phase == BossPhase.Death) {
                PhaseTimer++;
                AI_Death();
                UpdateVisuals();
                PublishVerdictVisuals();
                return;
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: AI_Intro(target); break;

                case BossPhase.P1_Hub: AI_Hub(target, 1); break;
                case BossPhase.P1_Sweep: AI_P1_Sweep(target); break;
                case BossPhase.P1_SealLock: AI_P1_SealLock(target); break;
                case BossPhase.P1_Charge: AI_ChargeVerdict(target, 3, 72f); break;
                case BossPhase.P1_RisingSlam: AI_P1_RisingSlam(target); break;

                case BossPhase.Trans2: AI_Trans2(target); break;

                case BossPhase.P2_Hub: AI_Hub(target, 2); break;
                case BossPhase.P2_ChainSlash: AI_ChainSlash(target, Main.expertMode ? 5 : 4, 58f); break;
                case BossPhase.P2_WardVolley: AI_WardVolley(target, false); break;
                case BossPhase.P2_Descend: AI_P2_Descend(target); break;
                case BossPhase.P2_Cage: AI_P2_Cage(target); break;
                case BossPhase.P2_Counter: AI_P2_Counter(target); break;

                case BossPhase.Trans3: AI_Trans3(target); break;

                case BossPhase.P3_Hub: AI_Hub(target, 3); break;
                case BossPhase.P3_Dance: AI_ChainSlash(target, Main.expertMode ? 8 : 6, 62f); break;
                case BossPhase.P3_FourPillar: AI_P3_FourPillar(target); break;
                case BossPhase.P3_WardVolley2: AI_WardVolley(target, true); break;
                case BossPhase.P3_Execution: AI_P3_Execution(target); break;
                case BossPhase.P3_Storm: AI_P3_Storm(target); break;
            }

            UpdateVisuals();
            PublishVerdictVisuals();
        }

        private void UpdateVisuals() {
            if (Math.Abs(NPC.velocity.X) > 0.8f)
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;

            // 身体语言合成: 速度倾角 + 挥斧曲线 + 后坐余摆 (余摆 = 阻尼振子)
            recoilRot += recoilVel;
            recoilVel -= recoilVel * 0.12f + recoilRot * 0.06f;
            recoilRot *= 0.96f;

            float swingRot = 0f;
            if (swingTimer > 0) {
                float t = 1f - swingTimer / (float)swingDuration;
                swingRot = SwingCurve(t) * swingDir;
            }
            NPC.rotation = NPC.velocity.X * 0.02f + swingRot + recoilRot;

            float baseIntensity = IsPhase3 ? 1.6f : IsPhase2 ? 1.3f : 1f;
            float pulse = baseIntensity + MathF.Sin(globalTime * 3f) * 0.15f;
            if (isCounterReady) pulse = 2.5f + MathF.Sin(globalTime * 12f) * 0.5f;
            if (Phase == BossPhase.Death) pulse *= MathHelper.Clamp(1f - DeathDimForSky, 0.2f, 1f);
            glowIntensity = pulse;

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.7f, 0.2f) * glowIntensity);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.Trans2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.Trans2);
                didPhase2Transition = true;
            }
            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.Trans3 && Phase != BossPhase.Trans2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.Trans3);
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            chargeCount = 0;
            isCounterReady = false;
            counterTriggered = false;
            contactDamage = false;
            wardOrbitTarget = 120f;
            NPC.Opacity = 1f;
            if (newPhase != BossPhase.Death)
                NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        // —— 手写轮替表 (PACING §2: 强弱交替, 绝不连抽同招) ——
        private static readonly BossPhase[] P1Rotation = [
            BossPhase.P1_Sweep, BossPhase.P1_Charge, BossPhase.P1_SealLock, BossPhase.P1_RisingSlam
        ];
        private static readonly BossPhase[] P2Rotation = [
            BossPhase.P2_ChainSlash, BossPhase.P2_WardVolley, BossPhase.P2_Descend,
            BossPhase.P2_Cage, BossPhase.P2_Counter
        ];
        private static readonly BossPhase[] P3Rotation = [
            BossPhase.P3_Dance, BossPhase.P3_FourPillar, BossPhase.P3_WardVolley2,
            BossPhase.P3_Execution, BossPhase.P3_Storm
        ];

        private BossPhase NextAttack(int tier) {
            BossPhase[] table = tier switch { 1 => P1Rotation, 2 => P2Rotation, _ => P3Rotation };
            BossPhase next = table[attackCycleIndex % table.Length];
            attackCycleIndex++;
            return next;
        }

        private void EndAttack() {
            TransitionTo(IsPhase3 ? BossPhase.P3_Hub : IsPhase2 ? BossPhase.P2_Hub : BossPhase.P1_Hub);
        }

        private static void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.hostile && p.damage > 0)
                    p.Kill();
            }
        }

        private void SpawnRuneSeal(Vector2 position, int fuse = SealFuse) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), position, Vector2.Zero,
                ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer,
                ai0: RunicEnergyOrbs.ModeSeal, ai1: fuse);
        }

        private void FireCleave(Vector2 pos, Vector2 vel, int damageDiv, float ai0 = 0f, float ai1 = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / damageDiv, 1f, Main.myPlayer, ai0, ai1);
        }

        private void FireOrb(Vector2 pos, Vector2 vel, int damageDiv, float mode = 0f, float ai1 = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / damageDiv, 0f, Main.myPlayer, mode, ai1);
        }

        #endregion

        #region 挥斧语法 (MOTION §1: anticipation 45% / strike 14% / recovery 41%)

        /// <summary>挥斩曲线: t 0~1 → 角度偏移。收束到 -0.91, poly(20) 斩到 +1.68, quintic 回正。</summary>
        private static float SwingCurve(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            if (t < 0.45f)
                return ACMUtils.QuadInOut(t / 0.45f) * -0.91f;
            if (t < 0.59f) {
                float s = (t - 0.45f) / 0.14f;
                float ease = 1f - MathF.Pow(1f - s, 20f); // poly(20) ease-out — "力"的最高杠杆旋钮
                return -0.91f + ease * 2.59f;
            }
            float r = (t - 0.59f) / 0.41f;
            float quint = r < 0.5f ? 16f * r * r * r * r * r : 1f - MathF.Pow(-2f * r + 2f, 5f) / 2f;
            return 1.68f * (1f - quint);
        }

        private void StartSwing(int dir, int duration) {
            swingDir = dir;
            swingDuration = Math.Max(duration, 1);
            swingTimer = swingDuration;
        }

        private void TickSwing() {
            if (swingTimer > 0) swingTimer--;
        }

        /// <summary>当前是否处于斩击爆发窗口 (t 0.45~0.62) — 用于门控接触伤害/残影/斩光。</summary>
        private bool InStrikeAct {
            get {
                if (swingTimer <= 0) return false;
                float t = 1f - swingTimer / (float)swingDuration;
                return t >= 0.45f && t < 0.62f;
            }
        }

        #endregion

        #region 断罪符环

        private Vector2 WardOrbPos(int slot) {
            float ang = globalTime * 1.4f + MathHelper.TwoPi / WardSlots * slot;
            return NPC.Center + ang.ToRotationVector2() * wardOrbitRadius;
        }

        // 符环被玩家击碎的本地演出 (absorbCooldown 刚被置位 = 本帧发生吸收; 齐射/beep 消耗各有自己的演出)
        private void DetectWardPop() {
            if (wardCharges < prevWardCharges && absorbCooldown >= 7 && Main.netMode != NetmodeID.Server) {
                wardPopFlash = 10;
                Vector2 popPos = WardOrbPos(Math.Min(prevWardCharges - 1, WardSlots - 1));
                for (int i = 0; i < 10; i++) {
                    int dustType = i % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                    Dust d = Dust.NewDustDirect(popPos, 0, 0, dustType, 0, 0, 60, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                }
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.5f }, popPos);
            }
            if (wardPopFlash > 0) wardPopFlash--;
            prevWardCharges = wardCharges;
        }

        /// <summary>符环点亮一颗 (蓄势 tick): 音高随点亮数上升 = 可听的充能进度。</summary>
        private void LightWardOrb() {
            if (wardCharges >= WardSlots) return;
            wardCharges++;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Pitch = -0.45f + wardCharges * 0.08f, Volume = 0.55f
                }, NPC.Center);
                Vector2 pos = WardOrbPos(wardCharges - 1);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.GoldFlame, 0, 0, 80, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                }
            }
        }

        #endregion

        #region 演出助手

        /// <summary>触发处决砸落泛光 + 屏幕震动 (可选 Boss 帧冻结 hitstop)。</summary>
        private void TriggerVerdictSlam(Vector2 center, float peak, int life, float shake, int hitstop = 0) {
            verdictBloomCenter = center;
            verdictBloomPeak = peak;
            verdictBloomTimer = life;
            verdictBloomMax = Math.Max(life, 1);
            if (hitstop > 0)
                hitstopTimer = Math.Max(hitstopTimer, hitstop);
            ACMScreenShakeSystem.Add(shake);
        }

        /// <summary>登记一道判决光束 (世界坐标, 寿命 life 帧)。</summary>
        private void AddJudgmentBeam(Vector2 start, Vector2 end, int life, float width) {
            for (int i = 0; i < judgmentBeams.Length; i++) {
                if (judgmentBeams[i].Time <= 0) {
                    judgmentBeams[i] = new JudgmentBeam { Start = start, End = end, Time = life, MaxTime = Math.Max(life, 1), Width = width };
                    return;
                }
            }
        }

        /// <summary>点亮符印封锁区地纹 (ArenaRunic, 引爆将近=渐亮的可读预警)。</summary>
        private void MarkSealZone(Vector2 center, float radius, float peak, int life) {
            sealRunicCenter = center;
            sealRunicRadius = radius;
            sealRunicPeak = peak;
            sealRunicTimer = Math.Max(sealRunicTimer, life);
        }

        /// <summary>写入断罪法阵实例 (slot 0=随体 1=地面锁定 2=演出); 每帧刷新, 不刷新自动淡出。</summary>
        private void SetSigil(int slot, Vector2 center, float radius, float progress, float charge,
            float intensity, int segments = WardSlots, float flash = 0f) {
            sigils[slot].Center = center;
            sigils[slot].Radius = radius;
            sigils[slot].Progress = MathHelper.Clamp(progress, 0f, 1f);
            sigils[slot].Charge = MathHelper.Clamp(charge, 0f, 1f);
            sigils[slot].Flash = MathHelper.Clamp(flash, 0f, 1f);
            sigils[slot].Intensity = MathHelper.Clamp(intensity, 0f, 1f);
            sigils[slot].Segments = segments;
            sigils[slot].SpinSeed = slot * 2.1f;
        }

        /// <summary>冲刺预警线 (致命红, §6.1: 冲刺线属致命预警)。</summary>
        private void SetDashLine(Vector2 start, Vector2 dir, int timer, int max) {
            dashLineStart = start;
            dashLineDir = dir;
            dashLineTimer = timer;
            dashLineMax = Math.Max(max, 1);
        }

        private void TickVerdictTimers() {
            if (verdictBloomTimer > 0) verdictBloomTimer--;
            if (sealRunicTimer > 0) sealRunicTimer--;
            if (execFlashTimer > 0) execFlashTimer--;
            if (dashLineTimer > 0) dashLineTimer--;
            for (int i = 0; i < judgmentBeams.Length; i++)
                if (judgmentBeams[i].Time > 0) judgmentBeams[i].Time--;
            for (int i = 0; i < sigils.Length; i++)
                sigils[i].Intensity *= 0.88f; // 未刷新自动淡出
        }

        /// <summary>触发裁决闪 (全屏后处理 impact frame)。仅换阶段3首秀与死亡定格两处。</summary>
        private void TriggerExecutionFlash(float peak, int life) {
            execFlashPeak = peak;
            execFlashTimer = life;
            execFlashMax = Math.Max(life, 1);
        }

        private void PublishVerdictVisuals() {
            if (Main.dedServ) return;

            int tier = IsPhase3 ? 2 : IsPhase2 ? 1 : 0;

            float counterTell = 0f;
            if (isCounterReady)
                counterTell = 0.55f + MathF.Sin(globalTime * 12f) * 0.35f;

            float bloom = 0f;
            if (verdictBloomTimer > 0) {
                float t = verdictBloomTimer / (float)verdictBloomMax;   // 1→0
                bloom = verdictBloomPeak * MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            }
            bloom = Math.Max(bloom, chargeRamp * 0.5f);
            Vector2 bloomCenter = verdictBloomTimer > 0 ? verdictBloomCenter : NPC.Center;

            float sealRunic = 0f;
            if (sealRunicTimer > 0)
                sealRunic = sealRunicPeak * MathHelper.Clamp(sealRunicTimer / 30f, 0.25f, 1f);

            VigorVerdictSystem.Publish(tier, MathHelper.Clamp(counterTell, 0f, 1f),
                sealRunicCenter, sealRunicRadius, MathHelper.Clamp(sealRunic, 0f, 1f),
                bloomCenter, 0.16f + (1f - bloom) * 0.22f, MathHelper.Clamp(bloom, 0f, 1f),
                (float)Main.GlobalTimeWrappedHourly, MathHelper.Clamp(DeathDimForSky, 0f, 1f));
        }

        #endregion

        #region 入场「开庭」

        private void AI_Intro(Player target) {
            NPC.dontTakeDamage = true;
            contactDamage = false;

            // 开庭位置快照 (法阵一旦显现便不再追人)
            if (PhaseTimer == 1) {
                chargeTarget = target.Center;
                NPC.Center = chargeTarget + new Vector2(0, -560);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;
                SoundEngine.PlaySound(SoundID.Item123 with { Pitch = -0.7f, Volume = 0.9f }, target.Center);
                NPC.netUpdate = true;
            }

            Vector2 sigilPos = chargeTarget + new Vector2(0, -560);
            Vector2 hoverPos = chargeTarget + new Vector2(0, -260);

            // 0~96: 天穹法阵旋出 + 符文粒子收束 (72% 处 hard-cut = 爆发前的吸气)
            if (PhaseTimer <= 110) {
                NPC.Center = sigilPos;
                float unfold = MathHelper.Clamp(PhaseTimer / 60f, 0f, 1f);
                float silence = PhaseTimer > 96 ? 1f : 0f;
                SetSigil(2, sigilPos, 240f * (1f - silence * 0.4f), unfold,
                    MathHelper.Clamp(PhaseTimer / 96f, 0f, 1f), 0.9f, 12, silence * 0.5f);

                if (Main.netMode != NetmodeID.Server && PhaseTimer > 20 && PhaseTimer <= 80 && Main.rand.NextBool(2)) {
                    // 收束流线 + 切向漩涡 (MOTION §6 充能语法)
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(220f, 420f);
                    Vector2 dustPos = sigilPos + angle.ToRotationVector2() * dist;
                    Vector2 pull = (sigilPos - dustPos) * 0.085f;
                    Vector2 swirl = pull.RotatedBy(MathHelper.PiOver2) * 0.4f;
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 90, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = pull + swirl;
                }

                if (PhaseTimer == 96)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 1.2f }, sigilPos);
            }

            // 110~120: 从法阵中砸落 (加速下坠 → impact)
            if (PhaseTimer > 110 && PhaseTimer <= 120) {
                NPC.Opacity = 1f;
                float t = (PhaseTimer - 110) / 10f;
                NPC.Center = Vector2.Lerp(sigilPos, hoverPos, ACMUtils.QuadIn(t));
                SetSigil(2, sigilPos, 240f, 1f, 1f, 0.9f * (1f - t), 12, 1f - t);
            }

            if (PhaseTimer == 120) {
                // impact: 一帧到位 + 震 + 吼 + 环爆 (本能4: 因果链)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.5f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerVerdictSlam(NPC.Center, 0.6f, 18, 0f);
                recoilVel += 0.10f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 40; i++) {
                        float a = MathHelper.TwoPi / 40 * i;
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame,
                            MathF.Cos(a) * 14f, MathF.Sin(a) * 14f, 80, default, 3f);
                        d.noGravity = true;
                    }
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i;
                        FireCleave(NPC.Center, angle.ToRotationVector2() * 10f, 4);
                    }
                    for (int dir = 0; dir < 4; dir++) {
                        float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * dir;
                        SpawnRuneSeal(target.Center + angle.ToRotationVector2() * 220f);
                    }
                }
            }

            // 120~168: 静止凝视 (menace is stillness), 只有金焰上飘
            if (PhaseTimer > 120) {
                NPC.velocity *= 0.9f;
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-50, 50), 60),
                        0, 0, DustID.GoldFlame, 0, -2f, 80, default, 2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer >= 168) {
                NPC.dontTakeDamage = false;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                TransitionTo(BossPhase.P1_Hub);
            }
        }

        #endregion

        #region 轮替枢纽 (connector — 追猎 + 喘息)

        private void AI_Hub(Player target, int tier) {
            float standoff = tier == 3 ? 240f : tier == 2 ? 280f : 320f;
            float dist = NPC.Distance(target.Center);

            Vector2 desiredPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * standoff;
            desiredPos.Y -= 60;
            float approach = dist > standoff + 150f ? 0.12f : 0.06f;
            // 距离栓绳: 飞远了加速咬回 (失败模式: boss 飞出屏幕绕圈)
            if (dist > LeashDistance) approach = 0.22f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (desiredPos - NPC.Center) * approach, 0.09f);

            // 稀疏点射 (保持威胁但留喘息)
            int pokeRate = tier == 3 ? 30 : tier == 2 ? 40 : 48;
            if (PhaseTimer % pokeRate == 0 && PhaseTimer > 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 10f;
                FireOrb(NPC.Center, vel, 5, tier == 3 ? RunicEnergyOrbs.ModeHoming : RunicEnergyOrbs.ModeMissile);
            }

            // 符环回充 (仅枢纽期, 拆掉的盾要花时间回来) + 常驻刻度环 (玩家随时读出盾量)
            if (tier >= 2) {
                int regenRate = tier == 3 ? 40 : 50;
                if (PhaseTimer % regenRate == 0 && wardCharges < WardSlots)
                    LightWardOrb();
                SetSigil(0, NPC.Center, wardOrbitRadius + 45f, 1f, wardCharges / (float)WardSlots, 0.32f, WardSlots);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 70), 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                d.noGravity = true;
            }

            int window = tier == 3 ? 40 : tier == 2 ? 48 : 60;
            if (PhaseTimer > window)
                TransitionTo(NextAttack(tier));
        }

        #endregion

        #region 一阶段「听讼」

        // 断罪横扫: 侧翼就位 → backswing 收束 → poly20 挥斩 + 扇形符刃 (带安全缺口) ×2
        private void AI_P1_Sweep(Player target) {
            // 第二段前摇缩短 (节奏递进), 挥斩曲线随前摇同步缩放
            int windup = chargeCount == 0 ? 34 : 22;
            int swingStart = chargeCount == 0 ? 8 : 2;

            if (SubState == 0) {
                // 前摇: 侧翼就位 + 挥斧收束 (启动减速 = 公平阀门)
                float side = NPC.Center.X > target.Center.X ? 1 : -1;
                Vector2 flankPos = target.Center + new Vector2(side * 350, -60);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (flankPos - NPC.Center) * 0.12f, 0.15f);
                if (AttackTimer > windup - 14) NPC.velocity *= 0.86f;

                // backswing 亮出 = 预警本体 (挥斩曲线 45% 收束段正好盖满前摇)
                if (AttackTimer == swingStart)
                    StartSwing(side > 0 ? -1 : 1, (int)((windup - swingStart + 6) / 0.45f));

                // 收束粒子, 末 8 帧静默 (爆发前吸气)
                if (Main.netMode != NetmodeID.Server && AttackTimer < windup - 8 && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(70, 70), 0, 0, DustID.GoldFlame, 0, 0, 80, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 5f;
                }

                if (AttackTimer >= windup + 6) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.88f;
                contactDamage = InStrikeAct; // 斩击窗口才有接触伤害 (与视觉严格对齐)

                // 挥斩爆发帧: 扇形符刃 + 后坐 (本能3: 每次发射都有反作用)
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    recoilVel -= swingDir * 0.09f;
                    NPC.velocity -= toPlayer * 7f;

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int bladeCount = Main.expertMode ? 9 : 8;
                        float totalSpread = MathHelper.ToRadians(64f);
                        int gapIndex = chargeCount % 2 == 0 ? bladeCount / 3 : bladeCount * 2 / 3; // 30° 安全缺口

                        for (int i = 0; i < bladeCount; i++) {
                            if (i == gapIndex) continue;
                            float t = (float)i / (bladeCount - 1) - 0.5f;
                            Vector2 vel = toPlayer.RotatedBy(t * totalSpread) * (12f + MathF.Abs(t) * 4f);
                            FireCleave(NPC.Center, vel, 4);
                        }
                    }
                }

                if (AttackTimer > 26) {
                    chargeCount++;
                    if (chargeCount < 2) { // 两段左右横扫
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        EndAttack();
                    }
                }
            }
        }

        // 符印锁狱: 法阵先亮 → 六芒印落 → 点射; 印间距留走位缝
        private void AI_P1_SealLock(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            // 锁定落点 (timer 5 快照, 不追身 = 公平)
            if (AttackTimer == 5) {
                chargeTarget = target.Center + target.velocity * 15f;
                NPC.netUpdate = true;
            }

            if (AttackTimer > 5) {
                float unfold = MathHelper.Clamp((AttackTimer - 5) / 40f, 0f, 1f);
                SetSigil(1, chargeTarget, 260f, unfold, unfold, 0.7f, 6);
                MarkSealZone(chargeTarget, 250f, 0.5f + unfold * 0.2f, 6);
            }

            if (AttackTimer == 45 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6 * i;
                    SpawnRuneSeal(chargeTarget + angle.ToRotationVector2() * 190f, 66); // 引信在招内燃尽
                }
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.3f, Volume = 0.9f }, chargeTarget);
            }

            if (AttackTimer > 50 && AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 13f;
                FireCleave(NPC.Center, vel, 5);
                recoilVel -= 0.03f;
            }

            if (Main.netMode != NetmodeID.Server && AttackTimer > 45) {
                Dust d = Dust.NewDustDirect(chargeTarget + Main.rand.NextVector2Circular(220, 220),
                    0, 0, DustID.BlueTorch, 0, -1.5f, 110, default, 1.6f);
                d.noGravity = true;
            }

            if (AttackTimer > 115)
                EndAttack();
        }

        // 冲锋判决: late-snap 反向抽身 + 致命冲刺线 → 9帧爆发直线 → 硬刹 (×reps)
        private void AI_ChargeVerdict(Player target, int reps, float dashSpeed) {
            const int windup = 30, dashLen = 9, brakeLen = 14;

            if (SubState == 0) {
                float t = AttackTimer / windup;

                // late-snap: 前 24 帧几乎不动, 最后骤然向后吸气 (MOTION §2)
                Vector2 away = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX);
                Vector2 anchor = target.Center + away * 440f + new Vector2(0, -40);
                Vector2 reelPos = anchor + away * MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 8f) * 260f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (reelPos - NPC.Center) * 0.14f, 0.2f);

                // 锁定冲刺线 (24 帧锁死, 预警线由暗转亮)
                if (AttackTimer == 1)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center); // 固定提前量 beep
                if (AttackTimer <= 24) {
                    chargeTarget = target.Center + target.velocity * 14f;
                    if (AttackTimer == 24) NPC.netUpdate = true;
                }
                SetDashLine(NPC.Center, (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX), (int)AttackTimer, windup);

                // 末 6 帧粒子静默
                if (Main.netMode != NetmodeID.Server && AttackTimer < windup - 6 && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.GoldFlame, 0, 0, 80, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 6f;
                }

                if (AttackTimer >= windup) {
                    SubState = 1;
                    AttackTimer = 0;
                    dashLineTimer = 0; // 预警线随爆发消失 (Boss 本体就是线)
                    // 爆发: 一帧设定, 不是渐加速 (本能2: 速度是对比)
                    NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX) * dashSpeed;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                contactDamage = true; // 冲刺中才有接触伤害

                // 侧涟漪符刃 (低速, 装饰主威胁)
                if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    FireCleave(NPC.Center + perpDir * 40f, perpDir * 4.5f, 5);
                    FireCleave(NPC.Center - perpDir * 40f, -perpDir * 4.5f, 5);
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                    }
                }

                if (AttackTimer >= dashLen) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 硬刹 ×0.65 (slam into position)
                NPC.velocity *= 0.65f;
                contactDamage = NPC.velocity.Length() > 20f;
                if (AttackTimer == 2) recoilVel += swingDir * 0.06f; // 刹车余摆

                if (AttackTimer >= brakeLen) {
                    chargeCount++;
                    if (chargeCount < reps) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        EndAttack();
                    }
                }
            }
        }

        // 升空劈斩: 升空收束 (72% 粒子截止) → 俯冲 → hitstop 落地 + 对称地浪
        private void AI_P1_RisingSlam(Player target) {
            const int riseLen = 40;

            if (SubState == 0) {
                float t = AttackTimer / (float)riseLen;
                NPC.velocity = new Vector2((target.Center.X - NPC.Center.X) * 0.012f,
                    MathHelper.Lerp(-4f, -17f, ACMUtils.QuadIn(MathHelper.Clamp(t, 0f, 1f))));

                // 锁定落点 + 地面法阵渐亮
                if (AttackTimer == 20) {
                    chargeTarget = target.Center + target.velocity * 10f;
                    NPC.netUpdate = true;
                }
                if (AttackTimer > 20) {
                    float unfold = MathHelper.Clamp((AttackTimer - 20) / 20f, 0f, 1f);
                    SetSigil(1, chargeTarget, 200f, unfold, unfold, 0.8f, 4);
                    MarkSealZone(chargeTarget, 200f, 0.4f + unfold * 0.4f, 6);
                    // 落点垂直预告光柱 (淡)
                    AddJudgmentBeam(chargeTarget + new Vector2(0, -900), chargeTarget, 2, 10f);
                }

                // 收束粒子 72% 截止 → 末 10 帧全静默
                if (Main.netMode != NetmodeID.Server && AttackTimer < riseLen * 0.72f && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(90, 90), 0, 0, DustID.GoldFlame, 0, 0, 70, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 7f;
                }

                if (AttackTimer >= riseLen) {
                    SubState = 1;
                    AttackTimer = 0;
                    StartSwing(NPC.Center.X < chargeTarget.X ? 1 : -1, 30);
                    NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * 35f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                contactDamage = true;

                if (NPC.Center.Y >= chargeTarget.Y - 20 || AttackTimer > 24) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    recoilVel += 0.14f;

                    TriggerVerdictSlam(NPC.Center + new Vector2(0, 40), 0.55f, 16, 10f, hitstop: 3);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -900), NPC.Center + new Vector2(0, 60), 14, 24f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 对称地浪 (沿地面扇出, 越远越快 → 跳跃可越)
                        for (int side = -1; side <= 1; side += 2) {
                            for (int i = 1; i <= 7; i++) {
                                Vector2 vel = new Vector2(side * i * 3f, -2.5f);
                                FireCleave(NPC.Center + new Vector2(0, 40), vel, 4);
                            }
                        }
                        for (int i = 0; i < 4; i++) {
                            float angle = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                            SpawnRuneSeal(NPC.Center + angle.ToRotationVector2() * 170f);
                        }
                    }
                }
            }
            else {
                NPC.velocity.Y -= 0.6f;
                if (AttackTimer > 32)
                    EndAttack();
            }
        }

        #endregion

        #region 换阶段演出

        // 宣判庭启: 举斧静默 → 符环列队降位 (逐颗点亮 tick) → 斧落宣判
        private void AI_Trans2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 2) {
                ClearHostileProjectiles(); // 换阶段清弹 (公平阀门)
                SoundEngine.PlaySound(SoundID.Item123 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                wardCharges = 0;
                // 超长收束 = 举斧过顶仪式; 45% 收束段恰好盖到 PhaseTimer≈110 的斧落宣判
                StartSwing(NPC.spriteDirection, 240);
            }

            // 50~98: 符环逐颗点亮 (每 4 帧一颗, 音高上升 — 首次展示"点亮"语法)
            if (PhaseTimer >= 50 && PhaseTimer < 98 && (PhaseTimer - 50) % 4 == 0)
                LightWardOrb();
            wardOrbitTarget = 120f;

            SetSigil(0, NPC.Center, wardOrbitRadius + 45f, MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f),
                wardCharges / (float)WardSlots, 0.75f, WardSlots);

            if (Main.netMode != NetmodeID.Server && PhaseTimer > 20 && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-100, 100), 80),
                    0, 0, DustID.GoldFlame, 0, -5f, 60, default, 2f);
                d.noGravity = true;
            }

            // 110: 斧落宣判 — 大震 + 12 向刃波 + 泛光
            if (PhaseTimer == 110) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                ACMScreenShakeSystem.Add(11f);
                TriggerVerdictSlam(NPC.Center, 0.6f, 18, 0f);
                recoilVel -= NPC.spriteDirection * 0.12f;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        FireCleave(NPC.Center, angle.ToRotationVector2() * 12f, 4);
                    }
                }
            }

            if (PhaseTimer >= 150) {
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                attackCycleIndex = 0;
                TransitionTo(BossPhase.P2_Hub);
            }
        }

        // 天刑加冕: 符环收束附斧 → 裁决闪首秀 → 双层环爆
        private void AI_Trans3(Player target) {
            NPC.velocity *= 0.90f;
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 2) {
                ClearHostileProjectiles();
                SoundEngine.PlaySound(SoundID.Item123 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
            }

            // 0~60: 符环快速全亮 + 收束到斧刃
            if (PhaseTimer < 60 && PhaseTimer % 4 == 0)
                LightWardOrb();
            wardOrbitTarget = PhaseTimer < 60 ? 120f : 34f;

            SetSigil(0, NPC.Center, wardOrbitRadius + 45f, 1f, wardCharges / (float)WardSlots,
                0.85f, WardSlots, PhaseTimer >= 60 ? (PhaseTimer - 60) / 12f : 0f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(150f, 380f);
                Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * dist;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, PhaseTimer < 60 ? DustID.GoldFlame : DustID.BlueTorch, 0, 0, 50, default, 2.6f);
                d.noGravity = true;
                d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 11f;
            }

            // 60: 白金裁决闪首秀 (低强度, 全屏名额契约)
            if (PhaseTimer == 60) {
                TriggerExecutionFlash(0.45f, 12);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.8f, Volume = 1.3f }, NPC.Center);
            }

            // 72: 双层环爆 + 符印环
            if (PhaseTimer == 72) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.6f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerVerdictSlam(NPC.Center, 0.75f, 22, 0f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        FireCleave(NPC.Center, angle.ToRotationVector2() * 14f, 3);
                        FireOrb(NPC.Center, (angle + MathHelper.ToRadians(11.25f)).ToRotationVector2() * 9f, 4);
                    }
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        SpawnRuneSeal(target.Center + angle.ToRotationVector2() * 330f);
                    }
                }
            }

            if (PhaseTimer >= 130) {
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.3f);
                wardCharges = WardSlots;
                wardOrbitTarget = 120f;
                attackCycleIndex = 0;
                TransitionTo(BossPhase.P3_Hub);
            }
        }

        #endregion

        #region 二阶段「宣判」

        // 连环斩 / 无间斩舞 (共用配方, reps/速度区分): Z字方位预排连斩, 段间静默
        private void AI_ChainSlash(Player target, int reps, float dashSpeed) {
            bool dance = Phase == BossPhase.P3_Dance;
            int windup = dance ? 10 : 16;
            int dashLen = dance ? 6 : 7;
            int brakeLen = dance ? 8 : 10;

            if (SubState == 0) {
                NPC.velocity *= 0.82f;

                // 方位预排: 交替斜向 (可预判的 Z 字), 舞态走八卦序
                if (AttackTimer == 1) {
                    float baseAng;
                    if (dance) {
                        baseAng = MathHelper.TwoPi / 8f * chargeCount;
                        Vector2 dir8 = baseAng.ToRotationVector2();
                        Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        if (Vector2.Dot(dir8, toP) < 0) dir8 = -dir8;
                        chargeTarget = NPC.Center + dir8 * 620f;
                    }
                    else {
                        float offsetAngle = (chargeCount % 2 == 0 ? 1 : -1) * MathHelper.ToRadians(24f);
                        Vector2 toP = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                        chargeTarget = NPC.Center + toP.RotatedBy(offsetAngle) * 620f;
                    }
                    StartSwing(chargeTarget.X > NPC.Center.X ? 1 : -1, windup + dashLen + brakeLen + 8);
                    NPC.netUpdate = true;
                }

                SetDashLine(NPC.Center, (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX), (int)AttackTimer, windup);

                // 反向抽身 (带阻尼, 防止漂移失控)
                Vector2 away = (NPC.Center - chargeTarget).SafeNormalize(Vector2.UnitX);
                NPC.velocity = NPC.velocity * 0.86f + away * ACMUtils.QuadIn(AttackTimer / (float)windup) * 3.4f;

                if (AttackTimer >= windup) {
                    SubState = 1;
                    AttackTimer = 0;
                    dashLineTimer = 0;
                    NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX) * dashSpeed;
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = 0.3f + chargeCount * 0.08f, Volume = 0.9f
                    }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                contactDamage = true;

                // 沿途留符印 (走位后患) — 舞态每段 1 印
                if (AttackTimer == dashLen / 2 && Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnRuneSeal(NPC.Center, 100);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.4f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.04f, 0.14f);
                    }
                }

                if (AttackTimer >= dashLen) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.68f;
                contactDamage = NPC.velocity.Length() > 20f;

                if (AttackTimer >= brakeLen) {
                    chargeCount++;
                    if (chargeCount < reps) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        // 终段: 回环大斩 + 环形刃波 (阖段收势)
                        StartSwing(-swingDir, 50);
                        recoilVel += swingDir * 0.08f;
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(dance ? 10f : 8f);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int n = dance ? 12 : 10;
                            for (int i = 0; i < n; i++) {
                                float angle = MathHelper.TwoPi / n * i;
                                FireCleave(NPC.Center, angle.ToRotationVector2() * (dance ? 14f : 11f), dance ? 3 : 4);
                            }
                        }
                        EndAttack();
                    }
                }
            }
        }

        // 符环齐射: 逐颗点亮(可读充能) → 静默收缩 → 连珠齐射 (点亮数=弹药数)
        private void AI_WardVolley(Player target, bool empowered) {
            int lightRate = empowered ? 6 : 8;
            int fireRate = empowered ? 3 : 4;

            Vector2 hoverPos = target.Center + new Vector2(0, -320);
            if (SubState <= 1)
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.035f, 0.07f);

            if (SubState == 0) {
                // 宣读罪状: 每 lightRate 帧点亮一颗 (被打掉的球不回来 — 拆盾即减压)
                wardOrbitTarget = 130f;
                if (AttackTimer % lightRate == 0 && AttackTimer > 8)
                    LightWardOrb();

                chargeRamp = wardCharges / (float)WardSlots * 0.6f;
                SetSigil(0, NPC.Center, wardOrbitRadius + 45f, 1f, wardCharges / (float)WardSlots,
                    0.85f, WardSlots);

                if (wardCharges >= WardSlots || AttackTimer > 120) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // 静默拍: 球收缩 + cos 颤动, 无粒子无声 (爆发前的吸气)
                wardOrbitTarget = 74f;
                float flick = MathF.Cos(AttackTimer * 0.9f) * 0.07f + 0.4f;
                SetSigil(0, NPC.Center, wardOrbitRadius + 40f, 1f, wardCharges / (float)WardSlots,
                    0.9f, WardSlots, flick);

                if (AttackTimer >= 22) {
                    SubState = 2;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 2) {
                // 行刑: 按序连珠齐射, 每颗一声 + 后坐 (阻尼防漂移失控)
                wardOrbitTarget = 100f;
                NPC.velocity *= 0.9f;
                if (AttackTimer % fireRate == 0 && wardCharges > 0) {
                    Vector2 orbPos = WardOrbPos(wardCharges - 1);
                    Vector2 aim = (target.Center + target.velocity * 12f - orbPos).SafeNormalize(Vector2.UnitY);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        if (empowered && wardCharges % 2 == 1)
                            FireOrb(orbPos, aim * 10f, 5, RunicEnergyOrbs.ModeHoming);
                        else
                            FireOrb(orbPos, aim * (empowered ? 14f : 12.5f), 5, RunicEnergyOrbs.ModeMissile, 1f);
                    }
                    wardCharges--;
                    NPC.velocity -= aim * 1.2f; // 齐射后坐
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.5f }, orbPos);
                        for (int i = 0; i < 4; i++) {
                            Dust d = Dust.NewDustDirect(orbPos, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 1.6f);
                            d.noGravity = true;
                            d.velocity = aim * Main.rand.NextFloat(2f, 5f);
                        }
                    }
                }

                if (wardCharges <= 0 || AttackTimer > 80) {
                    SubState = 3;
                    AttackTimer = 0;
                    // 强化版收尾: 一柄巨型断罪刃压轴
                    if (empowered && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 aim = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        FireCleave(NPC.Center, aim * 13f, 3, 1f);
                        recoilVel -= 0.1f;
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.94f;
                if (AttackTimer > 24)
                    EndAttack();
            }
        }

        // 天降断罪: 升空隐没 → 锁点亮阵 + 天光预告 → 瞬落 hitstop + 十字地浪
        private void AI_P2_Descend(Player target) {
            if (SubState == 0) {
                NPC.velocity = new Vector2((target.Center.X - NPC.Center.X) * 0.01f, -20f);
                NPC.Opacity = MathHelper.Lerp(1f, 0.35f, AttackTimer / 30f);

                if (AttackTimer >= 30) {
                    SubState = 1;
                    AttackTimer = 0;
                    chargeTarget = target.Center + target.velocity * 20f;
                    // 掩蔽位移: 低透明 + 旧位闪光, 直达锁点上空 (teleport-loop 删除折返时间)
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 16; i++) {
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 2.2f);
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(7f, 7f);
                        }
                    }
                    NPC.Center = chargeTarget + new Vector2(0, -620);
                    NPC.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.5f, Volume = 1.1f }, chargeTarget);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.9f;
                float unfold = MathHelper.Clamp(AttackTimer / 22f, 0f, 1f);
                SetSigil(1, chargeTarget, 300f, unfold, unfold, 0.85f, 8);
                MarkSealZone(chargeTarget, 280f, 0.85f, 8);
                // 垂直天光预告 (淡, 渐亮)
                AddJudgmentBeam(chargeTarget + new Vector2(0, -1000), chargeTarget, 2, 8f + unfold * 8f);

                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(chargeTarget + Main.rand.NextVector2Circular(240, 240),
                        0, 0, DustID.BlueTorch, 0, -2f, 100, default, 2f);
                    d.noGravity = true;
                }

                if (AttackTimer >= 25) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.Opacity = 1f;
                    NPC.velocity = new Vector2(0, 46f);
                    StartSwing(NPC.spriteDirection, 26);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 2) {
                contactDamage = true;

                if (NPC.Center.Y >= chargeTarget.Y || AttackTimer > 30) {
                    SubState = 3;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    recoilVel += 0.16f;

                    TriggerVerdictSlam(NPC.Center + new Vector2(0, 40), 0.7f, 18, 12f, hitstop: 3);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -1000), NPC.Center + new Vector2(0, 60), 16, 30f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 十字刃浪 (正交 4 向, 对角线是安全区)
                        for (int dir = 0; dir < 4; dir++) {
                            float angle = MathHelper.PiOver2 * dir;
                            for (int i = 0; i < 5; i++)
                                FireCleave(NPC.Center, angle.ToRotationVector2() * (8f + i * 3f), 3);
                        }
                        // 环出 6 颗慢速符球
                        for (int i = 0; i < 6; i++) {
                            float angle = MathHelper.TwoPi / 6 * i + MathHelper.PiOver4 * 0.5f;
                            FireOrb(NPC.Center, angle.ToRotationVector2() * 5f, 5);
                        }
                    }
                }
            }
            else {
                NPC.velocity.Y -= 0.5f;
                if (AttackTimer > 40)
                    EndAttack();
            }
        }

        // 符环囚阵: 节点空降成大环 (留缺口) → 连线渐亮 → 链式引爆
        private void AI_P2_Cage(Player target) {
            const int nodeCount = 14;
            const float cageRadius = 520f;

            if (SubState == 0) {
                if (AttackTimer == 1) {
                    chargeTarget = target.Center; // 阵心快照 (不追身)
                    SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.2f, Volume = 1f }, chargeTarget);
                    NPC.netUpdate = true;
                }

                Vector2 hoverPos = chargeTarget + new Vector2(0, -430);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);

                // 每 4 帧放一个节点 (跳过 2 个缺口槽位 = 永远有出路)
                int idx = ((int)AttackTimer - 2) / 4;
                if (AttackTimer >= 2 && (AttackTimer - 2) % 4 == 0 && idx < nodeCount && Main.netMode != NetmodeID.MultiplayerClient) {
                    if (idx != 4 && idx != 11) {
                        float angle = MathHelper.TwoPi / nodeCount * idx - MathHelper.PiOver2;
                        Vector2 nodePos = chargeTarget + angle.ToRotationVector2() * cageRadius;
                        Vector2 inward = (chargeTarget - nodePos).SafeNormalize(Vector2.Zero) * 0.35f;
                        // 引信错开 5 帧 = 链式引爆顺序可预判
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), nodePos, inward,
                            ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer,
                            ai0: RunicEnergyOrbs.ModeCageNode, ai1: 110 + idx * 5);
                    }
                }

                SetSigil(1, chargeTarget, cageRadius, MathHelper.Clamp(AttackTimer / 56f, 0f, 1f),
                    AttackTimer / 190f, 0.45f, nodeCount);

                if (AttackTimer >= 60) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 阵成: 外围点射施压, 节点自行倒计时链爆
                Vector2 hoverPos = chargeTarget + new Vector2(0, -430);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);
                SetSigil(1, chargeTarget, cageRadius, 1f, (60f + AttackTimer) / 190f, 0.45f, nodeCount);

                if (AttackTimer % 30 == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 12f;
                    FireOrb(NPC.Center, vel, 5, RunicEnergyOrbs.ModeMissile, 1f);
                    recoilVel -= 0.02f;
                }

                // 链爆完整落在招内 (末节点引信 ≈ 174 帧)
                if (AttackTimer > 180)
                    EndAttack();
            }
        }

        // 格挡反击: 符环收拢成护壁 → 被击则爆发反冲 → 超时泄势 (惩罚窗)
        private void AI_P2_Counter(Player target) {
            if (SubState == 0) {
                Vector2 approachPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 220f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (approachPos - NPC.Center) * 0.1f, 0.12f);

                if (AttackTimer > 20) {
                    SubState = 1;
                    AttackTimer = 0;
                    isCounterReady = true;
                    NPC.velocity = Vector2.Zero;
                    wardOrbitTarget = 70f; // 符环收拢成密集护壁 (视觉 tell)
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.9f;

                if (counterTriggered) {
                    SubState = 2;
                    AttackTimer = 0;
                    isCounterReady = false;
                    counterTriggered = false;
                    wardOrbitTarget = 120f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
                else if (AttackTimer > 80) {
                    SubState = 3;
                    AttackTimer = 0;
                    isCounterReady = false;
                    wardOrbitTarget = 120f;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 2) {
                if (AttackTimer == 1) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = toPlayer * 50f;
                    StartSwing(toPlayer.X > 0 ? 1 : -1, 30);
                    TriggerVerdictSlam(NPC.Center, 0.7f, 16, 12f, hitstop: 3);
                    AddJudgmentBeam(NPC.Center, NPC.Center + toPlayer * 1100f, 14, 26f);
                }
                contactDamage = NPC.velocity.Length() > 20f;

                if (AttackTimer == 12 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        FireCleave(NPC.Center, angle.ToRotationVector2() * 16f, 3);
                    }
                }

                if (AttackTimer > 8) NPC.velocity *= 0.88f;
                if (AttackTimer > 30)
                    EndAttack();
            }
            else {
                // 泄势: 架势落空, 40 帧僵直惩罚窗 (玩家的奖励回合)
                NPC.velocity *= 0.95f;
                if (Main.netMode != NetmodeID.Server && AttackTimer % 6 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(70, 70), 0, 0, DustID.BlueTorch, 0, 1f, 120, default, 1.4f);
                    d.noGravity = true;
                }
                if (AttackTimer > 40)
                    EndAttack();
            }
        }

        #endregion

        #region 三阶段「天刑」

        // 四极封印: 四隅刑柱 + 中央法阵 → 旋转十字判决轨 (轨上行刃)
        private void AI_P3_FourPillar(Player target) {
            const float pillarDist = 430f;

            if (SubState == 0) {
                if (AttackTimer == 1) {
                    chargeTarget = target.Center;
                    SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.5f, Volume = 1.2f }, chargeTarget);
                    NPC.netUpdate = true;
                }

                Vector2 hoverPos = chargeTarget + new Vector2(0, -380);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);

                float unfold = MathHelper.Clamp(AttackTimer / 45f, 0f, 1f);
                SetSigil(1, chargeTarget, 460f, unfold, unfold * 0.5f, 0.6f, 4);
                MarkSealZone(chargeTarget, 440f, 0.5f + unfold * 0.3f, 6);

                // 四隅刑柱降下 (符印形态, 引信在招式结束前燃尽 → 爆点落在招内不侵占喘息)
                if (AttackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int dir = 0; dir < 4; dir++) {
                        float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * dir;
                        SpawnRuneSeal(chargeTarget + angle.ToRotationVector2() * pillarDist, 160);
                    }
                }

                if (AttackTimer >= 50) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                Vector2 hoverPos = chargeTarget + new Vector2(0, -380);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);

                // 旋转十字判决轨: 前 30 帧半宽预热 (无刃), 之后轨上行刃
                // 转速刻意压低 (0.0035 rad/f), 行刃提前量 +0.08 rad — 刃与轨全程贴合可读
                float beamAngle = AttackTimer * 0.0035f;
                float warm = MathHelper.Clamp(AttackTimer / 30f, 0f, 1f);
                for (int axis = 0; axis < 2; axis++) {
                    float a = beamAngle + MathHelper.PiOver2 * axis;
                    Vector2 d = a.ToRotationVector2();
                    AddJudgmentBeam(chargeTarget - d * 1000f, chargeTarget + d * 1000f, 2, (6f + warm * 8f));
                }
                SetSigil(1, chargeTarget, 460f, 1f, 0.5f + AttackTimer / 240f, 0.6f, 4);

                // 轨上行刃: 每 10 帧沿 4 射线放刃 (伤害与视觉贴轨)
                if (warm >= 1f && AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int ray = 0; ray < 4; ray++) {
                        float a = beamAngle + 0.08f + MathHelper.PiOver2 * ray;
                        FireCleave(chargeTarget, a.ToRotationVector2() * 13f, 4);
                    }
                }

                // 刑柱点射 (交替瞄准)
                if (AttackTimer > 20 && AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    int dir = (int)(AttackTimer / 20) % 4;
                    float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * dir;
                    Vector2 pillarPos = chargeTarget + angle.ToRotationVector2() * pillarDist;
                    Vector2 vel = (target.Center - pillarPos).SafeNormalize(Vector2.Zero) * 11f;
                    FireOrb(pillarPos, vel, 5);
                }

                if (AttackTimer > 150)
                    EndAttack();
            }
        }

        // 天罚执行: 90帧长充能 (刻度可读+站桩dps窗) → hitstop 宣判 → 6波定向刃墙 (旋转缺口)
        private void AI_P3_Execution(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.9f; // 充能减速 = 站桩可打 (公平阀门: 玩家的输出窗口)
                NPC.Center += Main.rand.NextVector2Circular(3, 3);

                float t = MathHelper.Clamp(AttackTimer / 90f, 0f, 1f);
                chargeRamp = t;
                if (AttackTimer % 12 == 0)
                    ACMScreenShakeSystem.Add(1.5f + t * t * t * 6f); // shake ∝ charge³

                // 充能刻度: 6 格 = 6 波刃墙 (玩家直读波数)
                float contract = AttackTimer > 70 ? 1f - (AttackTimer - 70) / 20f * 0.28f : 1f;
                SetSigil(0, NPC.Center, 230f * contract, 1f, t, 0.95f, 6,
                    AttackTimer > 70 ? MathF.Cos(AttackTimer * 0.8f) * 0.15f + 0.15f : 0f);
                wardOrbitTarget = AttackTimer > 70 ? 60f : 120f;

                // 收束粒子 (72% 截止 → 末 20 帧静默)
                if (Main.netMode != NetmodeID.Server && AttackTimer < 65 && Main.rand.NextBool()) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(200, 560);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 40, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos) * 0.055f + (NPC.Center - dustPos).RotatedBy(MathHelper.PiOver2) * 0.02f;
                }

                if (AttackTimer >= 90) {
                    SubState = 1;
                    AttackTimer = 0;
                    chargeRamp = 0f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.6f }, NPC.Center);
                    // 宣判定格: 主 hitstop + 满级泛光 + 天柱光束
                    TriggerVerdictSlam(NPC.Center, 1f, 26, 12f, hitstop: 4);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -1100), NPC.Center + new Vector2(0, 80), 24, 42f);
                    MarkSealZone(target.Center, 380f, 1f, 36);
                    wardOrbitTarget = 120f;
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;

                // 6 波定向刃墙: 波向 = 60°×k 旋转, 每波带 50° 旋转缺口 (可预判逃生门)
                for (int wave = 0; wave < 6; wave++) {
                    if (AttackTimer != wave * 15 + 5) continue;

                    float baseAngle = MathHelper.TwoPi / 6 * wave + wave * 0.18f;
                    Vector2 beamDir = baseAngle.ToRotationVector2();
                    AddJudgmentBeam(NPC.Center, NPC.Center + beamDir * 1000f, 12, 22f);
                    TriggerVerdictSlam(NPC.Center, 0.5f, 12, 7f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f + wave * 0.13f, Volume = 1f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 刃墙: 从 620px 外向阵心收拢, 缺口角 50°
                        float gapCenter = baseAngle + MathHelper.Pi * 0.72f + wave * 0.5f;
                        int wallCount = 12;
                        for (int i = 0; i < wallCount; i++) {
                            float spread = MathHelper.ToRadians(52f);
                            float angle = baseAngle + spread * ((float)i / (wallCount - 1) - 0.5f);
                            Vector2 pos = target.Center + angle.ToRotationVector2() * 620f;
                            float toGap = MathHelper.WrapAngle(angle - gapCenter);
                            if (MathF.Abs(toGap) < MathHelper.ToRadians(25f)) continue;
                            Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (10f + wave * 1.6f);
                            FireCleave(pos, vel, 3);
                        }
                    }
                    // 符环链动: 每波射出一颗追踪符球 (符环参与行刑; 计数各端同跑保持一致)
                    if (wardCharges > 0) {
                        Vector2 orbPos = WardOrbPos(wardCharges - 1);
                        FireOrb(orbPos, beamDir * 9f, 4, RunicEnergyOrbs.ModeHoming);
                        wardCharges--;
                    }
                }

                if (AttackTimer > 130) TransitionTo(BossPhase.P3_Hub);
            }
        }

        // 裁决风暴: 大环节点收缩围场 + 穿环三连冲刺 → 全环齐爆
        private void AI_P3_Storm(Player target) {
            const int nodeCount = 16;

            if (SubState == 0) {
                NPC.velocity *= 0.92f;
                if (AttackTimer == 1) {
                    chargeTarget = target.Center;
                    SoundEngine.PlaySound(SoundID.Item100 with { Pitch = 0f, Volume = 1.1f }, chargeTarget);
                    NPC.netUpdate = true;
                }

                // 大环节点空降 (同引信 = 齐爆; 缓慢向心漂移收缩围场)
                int idx = ((int)AttackTimer - 2) / 2;
                if (AttackTimer >= 2 && (AttackTimer - 2) % 2 == 0 && idx < nodeCount && Main.netMode != NetmodeID.MultiplayerClient) {
                    if (idx != 5 && idx != 13) { // 双缺口
                        float angle = MathHelper.TwoPi / nodeCount * idx;
                        Vector2 nodePos = chargeTarget + angle.ToRotationVector2() * 780f;
                        Vector2 inward = (chargeTarget - nodePos).SafeNormalize(Vector2.Zero) * 1.2f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), nodePos, inward,
                            ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer,
                            ai0: RunicEnergyOrbs.ModeCageNode, ai1: 230);
                    }
                }

                if (AttackTimer >= 34) {
                    SubState = 1;
                    AttackTimer = 0;
                    chargeCount = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // 穿环三连冲刺 (复用冲锋配方节拍: 26 抽身 / 9 爆发 / 12 刹车)
                int cyc = (int)AttackTimer % 47;
                if (cyc < 26) {
                    Vector2 away = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 anchor = target.Center + away * 480f;
                    Vector2 reelPos = anchor + away * MathF.Pow(cyc / 26f, 8f) * 220f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (reelPos - NPC.Center) * 0.15f, 0.2f);
                    if (cyc == 1)
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);
                    if (cyc <= 20)
                        chargeTarget = target.Center + target.velocity * 13f;
                    SetDashLine(NPC.Center, (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX), cyc, 26);
                    if (cyc == 25) {
                        dashLineTimer = 0;
                        NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX) * 66f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f, Volume = 1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(6f);
                    }
                }
                else if (cyc < 35) {
                    contactDamage = true;
                    if (cyc % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                        SpawnRuneSeal(NPC.Center, 90);
                }
                else {
                    NPC.velocity *= 0.68f;
                    contactDamage = NPC.velocity.Length() > 20f;
                    if (cyc == 46) chargeCount++;
                }

                if (chargeCount >= 3 || AttackTimer > 200) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 等待环爆余韵 (节点自行齐爆; 爆点包含在招内, 不侵占喘息)
                NPC.velocity *= 0.94f;
                if (AttackTimer > 95)
                    EndAttack();
            }
        }

        #endregion

        #region 死亡「阖卷」

        private void AI_Death() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            contactDamage = false;
            isCounterReady = false;

            // —— 0~50: 定格失稳 —— 符环轨道抖动, 金焰忽明忽暗
            if (PhaseTimer <= 50) {
                NPC.velocity *= 0.85f;
                DeathDimForSky = PhaseTimer / 50f * 0.25f;
                wardOrbitTarget = 120f + MathF.Sin((float)PhaseTimer * 0.7f) * 24f;
                if (PhaseTimer == 2)
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
            }
            // —— 50~150: beep 加速序列 —— 每声熄爆一颗符环球, 间隔递减音高递升
            else if (PhaseTimer <= 150) {
                NPC.velocity = new Vector2(MathF.Sin((float)PhaseTimer * 0.11f) * 0.7f, -0.8f);
                DeathDimForSky = 0.25f + (PhaseTimer - 50f) / 100f * 0.3f;
                recoilVel += MathF.Sin((float)PhaseTimer * 0.23f) * 0.004f * ((PhaseTimer - 50f) / 100f + 0.3f);

                // 递减间隔 beep 表 (18,15,13,11,10,9,8,7,6,5,4,4 → 加速心跳)
                int t = (int)PhaseTimer - 50;
                ReadOnlySpan<int> beeps = [0, 18, 33, 46, 57, 67, 76, 84, 91, 97, 102, 106];
                for (int i = 0; i < beeps.Length; i++) {
                    if (t != beeps[i]) continue;
                    if (wardCharges > 0) wardCharges--;
                    ACMScreenShakeSystem.Add(2.5f + i * 0.35f);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.5f + i * 0.11f, Volume = 0.9f }, NPC.Center);
                        Vector2 pos = WardOrbPos(Math.Min(wardCharges, WardSlots - 1));
                        for (int k = 0; k < 8; k++) {
                            Dust d = Dust.NewDustDirect(pos, 0, 0, k % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch, 0, 0, 60, default, 2f);
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                        }
                    }
                }
            }
            // —— 150~180: 全静默 —— 天最暗, 万籁俱寂 (终爆前的吸气)
            else if (PhaseTimer <= 180) {
                NPC.velocity *= 0.9f;
                DeathDimForSky = 0.55f + (PhaseTimer - 150f) / 30f * 0.3f;
            }
            // —— 180: impact frame —— 裁决闪 + 唯一一次 shake 19
            else if (PhaseTimer <= 260) {
                if (PhaseTimer == 181) {
                    TriggerExecutionFlash(1f, 14);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.4f, Pitch = -0.4f }, NPC.Center);
                    ACMScreenShakeSystem.Add(19f);
                }
                DeathDimForSky = 0.85f;
                NPC.velocity = Vector2.Zero;

                // 溶解余烬缓落
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0, 1.5f, 120, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 2f));
                }
            }
            // —— 260~300: 白金终爆 → 真死 ——
            else {
                DeathFlashForSky = MathHelper.Clamp((PhaseTimer - 260f) / 15f, 0f, 1f) *
                                   MathHelper.Clamp(1f - (PhaseTimer - 275f) / 25f, 0f, 1f);
                if (PhaseTimer == 262) {
                    TriggerVerdictSlam(NPC.Center, 1f, 30, 14f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 40; i++) {
                            float a = MathHelper.TwoPi / 40 * i;
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, i % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch,
                                MathF.Cos(a) * 12f, MathF.Sin(a) * 12f, 60, default, 3f);
                            d.noGravity = true;
                        }
                    }
                }

                if (PhaseTimer >= 295 && Main.netMode != NetmodeID.MultiplayerClient && !deathFinished) {
                    deathFinished = true;
                    NPC.life = 0;
                    NPC.netUpdate = true;
                    NPC.checkDead(); // 放行真死 → OnKill 掉落/downed 照旧
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 裁决闪 (全屏后处理, 名额契约; 只在换阶段3首秀与死亡定格出现)
            DrawExecutionFlash(spriteBatch);

            // 断罪法阵 (专属着色器)
            DrawSigils(spriteBatch);

            // 冲刺预警线 (致命红) + 判决光束 + 格挡护罩环
            DrawDashLine();
            DrawJudgmentBeams();
            DrawCounterRing(spriteBatch);

            // 格挡态金色脉冲护盾
            if (isCounterReady) {
                float shieldPulse = MathF.Sin(globalTime * 10f) * 0.3f + 0.7f;
                Color shieldColor = new Color(255, 200, 60) * shieldPulse * 0.6f;
                for (int i = 0; i < 3; i++) {
                    float scale = NPC.scale * (1.1f + i * 0.05f);
                    spriteBatch.Draw(texture, NPC.Center - screenPos, frame, shieldColor * (1f - i * 0.3f),
                        NPC.rotation, origin, scale, effects, 0f);
                }
            }

            // 死亡溶解绘制路径 (DissolveBurn 共享着色器)
            if (Phase == BossPhase.Death && PhaseTimer > 180) {
                DrawWardOrbs(spriteBatch);
                DrawDissolvingBody(spriteBatch, texture, frame, origin, effects, screenPos);
                return false;
            }

            // 位移残影 (速度门控: 快才拖尾; 随本体透明度衰减)
            float opacity = NPC.Opacity;
            float speedGate = MathHelper.Clamp((NPC.velocity.Length() - 8f) / 22f, 0f, 1f);
            if (speedGate > 0.03f && opacity > 0.05f) {
                int trailLen = NPCID.Sets.TrailCacheLength[Type];
                for (int i = trailLen - 1; i > 0; i--) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    float t = (float)i / trailLen;
                    float alpha = 0.5f * (1f - t) * speedGate * (IsPhase3 ? 1.3f : 1f) * opacity;
                    Color trailColor = Color.Lerp(new Color(255, 200, 60), new Color(60, 120, 255), t) * alpha;
                    spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin, NPC.scale * (1f - t * 0.03f), effects, 0f);
                }
            }

            // 挥斩爆发帧: 旋转鬼影 + 斧光弧 (只在 strike act 亮 — 速度感的门控外衣)
            if (InStrikeAct && opacity > 0.05f) {
                float swingT = 1f - swingTimer / (float)swingDuration;
                for (int i = 1; i <= 4; i++) {
                    float ghostT = swingT - i * 0.025f;
                    if (ghostT < 0f) break;
                    float ghostRot = NPC.velocity.X * 0.02f + SwingCurve(ghostT) * swingDir + recoilRot;
                    Color ghostColor = new Color(255, 215, 110, 0) * ((0.42f - i * 0.09f) * opacity);
                    spriteBatch.Draw(texture, NPC.Center - screenPos, frame, ghostColor, ghostRot, origin, NPC.scale, effects, 0f);
                }
                DrawSlashArc(spriteBatch);
            }

            // 本体 (乘 Opacity: 入场法阵期隐身 / 天降断罪淡影)
            Color bodyColor = drawColor;
            if (Phase == BossPhase.Death)
                bodyColor = Color.Lerp(drawColor, new Color(70, 62, 55), MathHelper.Clamp(DeathDimForSky, 0f, 0.7f));
            spriteBatch.Draw(texture, NPC.Center - screenPos, frame, bodyColor * opacity, NPC.rotation, origin, NPC.scale, effects, 0f);

            // 断罪符环 (诚实护盾/弹药 双读法)
            DrawWardOrbs(spriteBatch);

            return false;
        }

        // 挥斩斧光弧 (GlaciateWave 叠层, 加性)
        private void DrawSlashArc(SpriteBatch sb) {
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave == null) return;

            float swingT = 1f - swingTimer / (float)swingDuration;
            float arcRot = NPC.rotation + (swingDir > 0 ? 0f : MathHelper.Pi);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float strikeGlow = MathF.Sin(MathHelper.Clamp((swingT - 0.45f) / 0.17f, 0f, 1f) * MathHelper.Pi);
            Vector2 pos = NPC.Center - Main.screenPosition;
            sb.Draw(wave, pos, null, new Color(255, 215, 110) * (0.7f * strikeGlow), arcRot,
                wave.Size() * 0.5f, new Vector2(0.62f, 0.5f), SpriteEffects.None, 0f);
            sb.Draw(wave, pos, null, new Color(120, 170, 255) * (0.35f * strikeGlow), arcRot + 0.1f * swingDir,
                wave.Size() * 0.5f, new Vector2(0.44f, 0.36f), SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        // 断罪符环: 点亮球体 + 暗位刻度 (球亮=有盾/有弹药, 完全诚实)
        private void DrawWardOrbs(SpriteBatch sb) {
            if (!IsPhase2 && Phase != BossPhase.Trans2)
                return;
            Texture2D orbTex = OrbTexture;
            Texture2D glow = ACMAsset.SoftGlow;
            if (orbTex == null)
                return;

            float deathMul = Phase == BossPhase.Death ? MathHelper.Clamp(1f - DeathDimForSky * 0.8f, 0.2f, 1f) : 1f;

            for (int slot = 0; slot < WardSlots; slot++) {
                Vector2 pos = WardOrbPos(slot) - Main.screenPosition;
                bool lit = slot < wardCharges;

                if (lit) {
                    float pulse = 1f + MathF.Sin(globalTime * 5f + slot) * 0.12f;
                    float popBoost = slot == wardCharges - 1 && wardPopFlash > 0 ? wardPopFlash / 10f * 0.8f : 0f;
                    if (glow != null) {
                        Color halo = new Color(255, 210, 100, 0) * ((0.4f + popBoost) * deathMul);
                        sb.Draw(glow, pos, null, halo, 0f, glow.Size() / 2f, 0.75f * pulse, SpriteEffects.None, 0f);
                    }
                    Color orbColor = Color.Lerp(new Color(255, 220, 120), Color.White, popBoost) * deathMul;
                    sb.Draw(orbTex, pos, null, orbColor, globalTime * 2f + slot, orbTex.Size() / 2f, 0.30f * pulse, SpriteEffects.None, 0f);
                }
                else {
                    // 熄灭槽位: 暗刻度点 (玩家读出"缺了几颗")
                    if (glow != null) {
                        Color dim = new Color(120, 95, 45, 0) * (0.22f * deathMul);
                        sb.Draw(glow, pos, null, dim, 0f, glow.Size() / 2f, 0.22f, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        // 断罪法阵实例绘制 (VigorRunicSigil, 专属着色器)
        private void DrawSigils(SpriteBatch sb) {
            if (Main.dedServ) return;
            Effect fx = SigilShader;
            Texture2D glow = ACMAsset.SoftGlow;
            if (fx == null || glow == null) return;

            for (int i = 0; i < sigils.Length; i++) {
                ref SigilInstance s = ref sigils[i];
                if (s.Intensity <= 0.02f || s.Radius < 4f) continue;

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uProgress"]?.SetValue(s.Progress);
                fx.Parameters["uIntensity"]?.SetValue(s.Intensity);
                fx.Parameters["uCharge"]?.SetValue(s.Charge);
                fx.Parameters["uSegments"]?.SetValue((float)s.Segments);
                fx.Parameters["uFlash"]?.SetValue(s.Flash);
                fx.Parameters["uSpin"]?.SetValue(s.SpinSeed);
                fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(new Color(255, 205, 95).ToVector3(), 1f));
                fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(new Color(110, 160, 255).ToVector3(), 1f));

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
                float scale = s.Radius * 2f / glow.Width;
                sb.Draw(glow, s.Center - Main.screenPosition, null, Color.White, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }
        }

        // 冲刺预警线 (BeamGrad, 致命红 — §6.1 冲刺线属致命预警)
        private void DrawDashLine() {
            if (Main.dedServ || dashLineTimer <= 0)
                return;
            float t = dashLineTimer / (float)dashLineMax;
            float intensity = t * t * 0.8f; // 由暗转亮
            ACMShaders.DrawBeam(dashLineStart, dashLineStart + dashLineDir * 1200f, 9f,
                TelegraphColors.Lethal, TelegraphColors.Gold * 0.4f, intensity,
                flowSpeed: 3f, flowScale: 3f, coreSharp: 3f, coreGlow: 0.9f);
        }

        // 判决光束 (BeamGrad): 金白芯 + 暖金边, 寿命包络淡出
        private void DrawJudgmentBeams() {
            if (Main.dedServ)
                return;
            Color core = new(255, 240, 180);
            Color edge = TelegraphColors.Gold;
            for (int i = 0; i < judgmentBeams.Length; i++) {
                JudgmentBeam b = judgmentBeams[i];
                if (b.Time <= 0)
                    continue;
                float life = b.Time / (float)b.MaxTime;
                float intensity = MathF.Sin(MathHelper.Clamp(life, 0f, 1f) * MathHelper.Pi * 0.5f);
                ACMShaders.DrawBeam(b.Start, b.End, b.Width * (0.4f + 0.6f * life),
                    core, edge * 0.5f, intensity, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f, coreGlow: 1.5f);
            }
        }

        // 格挡反击护罩环 (ArenaRunic 法阵): "现在别打"的世界级 tell
        private void DrawCounterRing(SpriteBatch sb) {
            if (Main.dedServ || !isCounterReady)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            float pulse = 0.55f + MathF.Sin(globalTime * 12f) * 0.35f;
            ACMShaders.WorldDecalParams(NPC.Center, 140f, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(new Color(255, 150, 50).ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(14f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        // 裁决闪 (VigorExecutionFlash 全屏后处理; RequestFullscreenSlot 名额契约)
        private void DrawExecutionFlash(SpriteBatch sb) {
            if (Main.dedServ || execFlashTimer <= 0)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ExecFlashShader;
            if (fx == null)
                return;

            float t = execFlashTimer / (float)execFlashMax; // 1→0
            float intensity = execFlashPeak * MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi * 0.85f);
            if (intensity < 0.01f)
                return;

            Vector2 uv = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uSlashAng"]?.SetValue(0.12f);
            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: false);
        }

        // 死亡溶解: DissolveBurn 共享着色器, 金焰灼边自下而上吞噬
        private void DrawDissolvingBody(SpriteBatch sb, Texture2D texture, Rectangle frame,
            Vector2 origin, SpriteEffects effects, Vector2 screenPos) {
            Effect fx = ACMShaders.DissolveBurn;
            if (fx == null) {
                float fade = MathHelper.Clamp(1f - (PhaseTimer - 180f) / 80f, 0f, 1f);
                sb.Draw(texture, NPC.Center - screenPos, frame, Color.White * fade, NPC.rotation, origin, NPC.scale, effects, 0f);
                return;
            }

            float threshold = MathHelper.Clamp((PhaseTimer - 180f) / 90f, 0f, 1f) * 0.9f;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(threshold);
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(new Color(255, 200, 90).ToVector3(), 1f));
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f));
            fx.Parameters["uSweepStrength"]?.SetValue(0.4f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(texture, NPC.Center - screenPos, frame, Color.White, NPC.rotation, origin, NPC.scale, effects, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        #endregion
    }
}

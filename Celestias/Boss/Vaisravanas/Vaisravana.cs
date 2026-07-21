using AncientChineseMythology.Celestias.Boss.Vaisravanas.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 北方多闻天王 · 四大天王之首 · 天将线终局门控 Boss (T35)
    ///
    /// V3 重做核心：「北天镇库 · 托塔天王」—— 威严护法武神，不是悬浮炮台。
    /// ● 天王步：沉腰长架步(反向蓄势) → 雷霆跨步(瞬时 46px/f) → 落地震山(硬刹+地波)，
    ///   接触伤害仅在跨步窗口开启，伤害盒与视觉严格对齐。
    /// ● 宝塔充能签名机制保留：玩家贴塔窃取赐福，把瞬发攻击转化为带预告的安全变体。
    /// ● 金刚怒目式蓄力语法：长架招(汇聚金光/震屏渐强) → 爆发前一拍静默 → 一击释放。
    /// ● 三大演出节拍齐备：天王降世入场 / 显三面六臂法相(P2) / 库藏开启(P3) / 金身崩解死亡。
    /// ● 三专属着色器：VaisravanaMandala(佛纹坛城) / VaisravanaGoldBody(金身法相) /
    ///   VaisravanaPillarBrand(镇压天光柱)。
    /// </summary>
    [AutoloadBossHead]
    internal partial class Vaisravana : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>宝塔环绕数量</summary>
        public const int TowerCount = 4;

        /// <summary>单座宝塔最大充能</summary>
        public const int MaxTowerCharge = 3;

        /// <summary>赐福区半径——玩家进入即可窃取宝塔充能</summary>
        public const float BlessingRadius = 130f;

        /// <summary>窃取充能可暂存的上限（用于转化后续攻击）</summary>
        public const int MaxPendingBlessing = 3;

        /// <summary>天王步瞬时速度 (px/f) —— 速度即对比：只保持几帧的极短爆发</summary>
        public const float StepSpeed = 46f;

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>是否处于三阶段</summary>
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool deathScriptDone;        // 死亡演出播毕，允许真正死亡

        // 宝塔状态
        private float[] towerAngles;
        private float[] towerDistances;
        private float towerOrbitSpeed;
        private float[] towerRise;           // 0~1 宝塔升起程度（入场逐座升起 / 死亡逐座熄灭）
        private float[] towerRecoil;         // 发射后座（绘制偏移，指数衰减）

        // 宝塔充能机制（签名机制）
        private int[] towerCharges;          // 各塔当前充能 0..MaxTowerCharge
        private int[] towerRechargeTimer;    // 各塔回充计时
        private int pendingBlessing;         // 已窃取、可转化下次攻击的赐福数
        private int blessingStealCooldown;   // 窃取冷却
        private int lastBlessedTower = -1;   // 最近被窃取的塔（用于绘制反馈）
        private int blessingFlash;           // 窃取闪光计时（绘制用）

        // 天王步 / 攻击控制
        private Vector2 dashTarget;          // 跨步落点（网络同步）
        private Vector2 dashVelocity;        // SpringDamp2D 阻尼累积
        private int dashCount;               // 步序计数（网络同步）
        private Vector2 stepStart;           // 架步锚点（各端本地记录，进入架步时取自当前位置）
        private Vector2 stepDir;             // 本步朝向（由 dashTarget 推导）
        private int stepTravelNeeded = 8;    // 本步跨步帧数（按距离/步速推算，6~15 帧的极短爆发）

        // 激光 / 镜轴控制
        private float laserAngle;
        private float laserSweepDirection;

        // 夜叉仆从控制（四方锚点）
        private int[] yakshaMinionIds;

        // 守护反击窗口（宝伞格挡）
        private bool guardActive;
        private int guardReflectCooldown;
        private float guardVisual;
        private float umbrellaOpen;          // 0~1 宝伞开合（视觉）

        // 三阶段库藏封印轮替
        private int sealCycle;               // 0=金环 1=镜射 2=终极

        // 视觉效果
        private float haloRotation;
        private float haloScale;
        private float glowIntensity;
        private float divineAuraAlpha;
        private float bodyFlash;             // 金身白闪 0~1（受击/爆发，快衰减）
        private float bodyCrack;             // 金身龟裂 0~1（死亡演出）
        private float dharmaAura;            // 三面六臂法相强度 0~1
        private float chargeConverge;        // 蓄力汇聚强度 0~1（金刚破军等，供绘制层）
        private bool particleSilence;        // 死亡演出"爆发前静默"：抑制一切本地粒子

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 130;
            NPC.height = 130;
            NPC.damage = 160;
            NPC.defense = 85;
            NPC.lifeMax = 1350000; // 月后级别血量
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(platinum: 2, gold: 50);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;

            // 调整难度
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.LunarBoss;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            InitializeTowers();
            towerOrbitSpeed = 0.015f;

            // 初始化仆从
            yakshaMinionIds = new int[TowerCount];
            for (int i = 0; i < yakshaMinionIds.Length; i++) yakshaMinionIds[i] = -1;

            // 初始化视觉效果
            haloRotation = 0f;
            haloScale = 1f;
            glowIntensity = 1f;
            divineAuraAlpha = 0f;

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(deathScriptDone);
            writer.WriteVector2(dashTarget);
            writer.Write(dashCount);
            writer.Write(laserAngle);
            writer.Write(mirrorAxis);
            writer.Write((byte)pendingBlessing);
            writer.Write((byte)sealCycle);
            writer.Write(guardActive);
            writer.Write((byte)p1Index);
            writer.Write((byte)p2Index);
            if (towerCharges == null) InitializeTowers();
            for (int i = 0; i < TowerCount; i++) writer.Write((byte)towerCharges[i]);
            for (int i = 0; i < TowerCount; i++) writer.Write((short)(yakshaMinionIds?[i] ?? -1));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            deathScriptDone = reader.ReadBoolean();
            dashTarget = reader.ReadVector2();
            dashCount = reader.ReadInt32();
            laserAngle = reader.ReadSingle();
            mirrorAxis = reader.ReadSingle();
            pendingBlessing = reader.ReadByte();
            sealCycle = reader.ReadByte();
            guardActive = reader.ReadBoolean();
            p1Index = reader.ReadByte();
            p2Index = reader.ReadByte();
            if (towerCharges == null) InitializeTowers();
            for (int i = 0; i < TowerCount; i++) towerCharges[i] = reader.ReadByte();
            yakshaMinionIds ??= new int[TowerCount];
            for (int i = 0; i < TowerCount; i++) yakshaMinionIds[i] = reader.ReadInt16();

            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>
        /// 死亡演出拦截：首次濒死不立即死亡，转入「金身崩解」脚本；演出播毕后放行真死。
        /// </summary>
        public override bool CheckDead() {
            if (deathScriptDone)
                return true;

            if (Phase != BossPhase.Death) {
                NPC.life = Math.Max(NPC.life, 1);
                TransitionTo(BossPhase.Death);
                NPC.dontTakeDamage = true; // 置于 TransitionTo 之后（其内部会复位该标记）
                guardActive = false;
                ClearBattlefield(fullClear: true);
                NPC.netUpdate = true;
            }
            else {
                NPC.life = Math.Max(NPC.life, 1);
            }
            return false;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>(), 1, 1, 3));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<TreasurePagodaStaff>(),
                ModContent.ItemType<VaultshadeVoidshot>(),
                ModContent.ItemType<CelestialCircletScepter>()
            ));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TreasurePagodaCharm>(), 4));
        }

        public override void OnKill() {
            DownedBossSystem.downedVaisravana = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier mod = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 25f, 12f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(mod);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            // 受击金身白闪（客户端反馈）
            bodyFlash = MathF.Max(bodyFlash, 0.22f);
        }

        /// <summary>
        /// 宝伞格挡 — 伞盖张开窗口内无敌，并向攻击者迸射招财金镖（劫财反震）。
        /// 玩家该收手走位而非继续输出。
        /// </summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (!guardActive)
                return;

            // 守护窗口内伤害归零
            modifiers.FinalDamage *= 0f;

            if (Main.netMode == NetmodeID.MultiplayerClient || guardReflectCooldown > 0)
                return;

            guardReflectCooldown = 14;
            guardVisual = MathHelper.Min(guardVisual + 0.4f, 1.6f);

            Player attacker = Main.player[NPC.target];
            Vector2 reflectDir = (attacker.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = reflectDir.RotatedBy(i * MathHelper.ToRadians(14f)) * 12f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + reflectDir * 70f, vel,
                    ModContent.ProjectileType<TreasureTowerOrb>(), NPC.defDamage / 4, 2f, Main.myPlayer);
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f }, NPC.Center);
        }

        #endregion

        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            if (towerAngles == null || towerCharges == null) {
                InitializeTowers();
            }

            // 接触伤害门控：默认无接触伤害，仅天王步跨步窗口由步进逻辑恢复
            NPC.damage = 0;

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if ((!target.active || target.dead) && Phase != BossPhase.Death) {
                    // 没有有效目标，升天离开
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            UpdateVisualEffects();
            UpdateTowers();
            UpdatePagodaCharges(target);

            if (!VaultUtils.isServer)
                PublishScreenState();

            if (guardReflectCooldown > 0) guardReflectCooldown--;
            guardVisual = MathHelper.Lerp(guardVisual, guardActive ? 1.4f : 0f, 0.12f);
            umbrellaOpen = MathHelper.Lerp(umbrellaOpen, guardActive ? 1f : 0f, guardActive ? 0.16f : 0.10f);
            bodyFlash *= 0.88f;

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_Hub:
                    RunPhase1Hub(target);
                    break;
                case BossPhase.Phase1_KingSteps:
                    RunPhase1KingSteps(target);
                    break;
                case BossPhase.Phase1_TowerVolley:
                    RunPhase1TowerVolley(target);
                    break;
                case BossPhase.Phase1_SweepingLight:
                    RunPhase1SweepingLight(target);
                    break;
                case BossPhase.Phase1_VajraPierce:
                    RunPhase1VajraPierce(target);
                    break;
                case BossPhase.Phase1_HeavenPillars:
                    RunPhase1HeavenPillars(target);
                    break;
                case BossPhase.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                case BossPhase.Phase2_Hub:
                    RunPhase2Hub(target);
                    break;
                case BossPhase.Phase2_YakshaSummon:
                    RunPhase2YakshaSummon(target);
                    break;
                case BossPhase.Phase2_QuadrantRay:
                    RunPhase2QuadrantRay(target);
                    break;
                case BossPhase.Phase2_ImmortalWave:
                    RunPhase2ImmortalWave(target);
                    break;
                case BossPhase.Phase2_StampFormation:
                    RunPhase2StampFormation(target);
                    break;
                case BossPhase.Phase2_PagodaSuppress:
                    RunPhase2PagodaSuppress(target);
                    break;
                case BossPhase.Phase2_GuardianStance:
                    RunPhase2GuardianStance(target);
                    break;
                case BossPhase.PhaseTransition_3:
                    RunPhaseTransition3(target);
                    break;
                case BossPhase.Phase3_SealRings:
                    RunPhase3SealRings(target);
                    break;
                case BossPhase.Phase3_YakshaMirror:
                    RunPhase3YakshaMirror(target);
                    break;
                case BossPhase.Phase3_UltimateTower:
                    RunPhase3UltimateTower(target);
                    break;
                case BossPhase.Phase3_SealBeat:
                    RunPhase3SealBeat(target);
                    break;
                case BossPhase.Death:
                    RunDeath(target);
                    break;
            }

            // 仙气白光照明
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.98f, 0.95f) * glowIntensity);

            // 宝塔光照
            for (int i = 0; i < TowerCount; i++) {
                if (towerRise[i] <= 0.05f) continue;
                Vector2 towerPos = GetTowerPosition(i);
                float chargeGlow = (0.4f + towerCharges[i] * 0.18f) * towerRise[i];
                Lighting.AddLight(towerPos, new Vector3(1f, 0.92f, 0.7f) * chargeGlow);
            }
        }

        private void InitializeTowers() {
            towerAngles = new float[TowerCount];
            towerDistances = new float[TowerCount];
            towerCharges = new int[TowerCount];
            towerRechargeTimer = new int[TowerCount];
            towerRise = new float[TowerCount];
            towerRecoil = new float[TowerCount];
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] = MathHelper.TwoPi * i / TowerCount;
                towerDistances[i] = 180f;
                towerCharges[i] = MaxTowerCharge;
                towerRise[i] = Phase == BossPhase.Intro ? 0f : 1f;
            }
        }

        private void UpdateTowers() {
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] += towerOrbitSpeed;
                towerRecoil[i] *= 0.86f;

                float baseDistance = 180f;
                if (IsPhase2) baseDistance = 210f;
                if (IsPhase3) baseDistance = 240f;

                towerDistances[i] = baseDistance + MathF.Sin(globalTime * 1.8f + i * 0.6f) * 18f;

                // 入场 / 死亡以外的常态：塔保持升起
                if (Phase != BossPhase.Intro && Phase != BossPhase.Death)
                    towerRise[i] = MathHelper.Lerp(towerRise[i], 1f, 0.08f);
            }
        }

        /// <summary>
        /// 宝塔充能机制核心：自动回充 + 玩家赐福窃取。
        /// 玩家飞入某座宝塔的赐福区且该塔有充能时，窃取一点充能并暂存为「赐福」，
        /// 用于把下一次瞬发攻击转化为带预告的安全光束。
        /// </summary>
        private void UpdatePagodaCharges(Player target) {
            if (blessingStealCooldown > 0) blessingStealCooldown--;
            if (blessingFlash > 0) blessingFlash--;

            // 自动回充（出场/转阶段/死亡不回充，避免堆叠）
            bool canRecharge = Phase != BossPhase.Intro &&
                               Phase != BossPhase.PhaseTransition_2 &&
                               Phase != BossPhase.PhaseTransition_3 &&
                               Phase != BossPhase.Death;
            int rechargeInterval = IsPhase3 ? 150 : (IsPhase2 ? 190 : 230);
            if (canRecharge) {
                for (int i = 0; i < TowerCount; i++) {
                    if (towerCharges[i] >= MaxTowerCharge) { towerRechargeTimer[i] = 0; continue; }
                    towerRechargeTimer[i]++;
                    if (towerRechargeTimer[i] >= rechargeInterval) {
                        towerRechargeTimer[i] = 0;
                        towerCharges[i]++;
                        NPC.netUpdate = true;
                    }
                }
            }

            // 赐福窃取（仅服务器/单机判定）
            if (Main.netMode != NetmodeID.MultiplayerClient && canRecharge &&
                blessingStealCooldown <= 0 && pendingBlessing < MaxPendingBlessing) {
                for (int i = 0; i < TowerCount; i++) {
                    if (towerCharges[i] <= 0) continue;
                    if (Vector2.DistanceSquared(target.Center, GetTowerPosition(i)) <= BlessingRadius * BlessingRadius) {
                        towerCharges[i]--;
                        pendingBlessing++;
                        blessingStealCooldown = 40;
                        lastBlessedTower = i;
                        blessingFlash = 26;
                        NPC.netUpdate = true;
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = 0.3f, Volume = 0.7f }, target.Center);
                            // 赐福窃取金闪：财宝泛光 + 轻震反馈"我抢到了"
                            VaisravanaTreasureScreenSystem.PulseBloom(0.55f);
                            ACMScreenShakeSystem.Add(2f);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>消费一点赐福。返回 true 表示本次攻击应使用「安全预告」变体。</summary>
        private bool ConsumeBlessing() {
            if (pendingBlessing > 0) {
                pendingBlessing--;
                NPC.netUpdate = true;
                return true;
            }
            return false;
        }

        /// <summary>消费一点指定宝塔充能，返回是否成功（用于把攻击强度与塔挂钩）。</summary>
        private bool ConsumeTowerCharge(int index) {
            if (index >= 0 && index < TowerCount && towerCharges[index] > 0) {
                towerCharges[index]--;
                towerRecoil[index] = 1f;
                return true;
            }
            return false;
        }

        private int ChargedTowerCount() {
            int c = 0;
            for (int i = 0; i < TowerCount; i++) if (towerCharges[i] > 0) c++;
            return c;
        }

        private Vector2 GetTowerPosition(int index) {
            if (towerAngles == null || towerDistances == null) return NPC.Center;
            float angle = towerAngles[index];
            float distance = towerDistances[index] * MathHelper.Clamp(towerRise?[index] ?? 1f, 0.001f, 1f);
            return NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        #endregion

        #region 天王步运动助手（架步→跨步→硬刹）

        /// <summary>
        /// 服务器选定跨步落点：越过玩家身位的另一侧，横向至少偏离 240px（防贴脸斩杀）。
        /// </summary>
        private void PickStepTarget(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            float side = MathF.Sign(target.Center.X - NPC.Center.X);
            if (side == 0) side = 1;
            dashTarget = new Vector2(
                target.Center.X + side * 300f,
                target.Center.Y - 110f);
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 架步（anticipation）：沉腰反向蓄势。前段几乎不动，末几帧猛然后坐（pow8 late-snap），
        /// 同时下沉 22px 做"沉腰"读感。t: 0~1。
        /// </summary>
        private void StepAnticipate(float t) {
            // 多人客户端可能错过 PhaseTimer==1 的锚点初始化（ai 同步落点不定），做位置保底
            if (stepStart == Vector2.Zero || Vector2.DistanceSquared(stepStart, NPC.Center) > 500f * 500f)
                stepStart = NPC.Center;

            stepDir = (dashTarget - stepStart).SafeNormalize(Vector2.UnitX);
            float reel = VaisravanaHelper.LateSnap(t, 8f) * 88f;
            float sink = ACMUtils.SineInOut(t) * 22f;
            NPC.Center = stepStart - stepDir * reel + new Vector2(0f, sink);
            NPC.velocity = Vector2.Zero;

            // 蓄势金尘沿脚下聚拢（客户端）
            if (!VaultUtils.isServer && t > 0.4f && Main.rand.NextBool(2)) {
                Vector2 dustPos = NPC.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), NPC.height * 0.42f);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(1f, 2.4f)), 120, default, 1.3f);
                d.noGravity = true;
            }
        }

        /// <summary>跨步发射：单帧瞬时提速 + 出手音效。跨步帧数按距离推算（6~15 帧极短爆发）。</summary>
        private void StepLaunch() {
            Vector2 toLand = dashTarget - NPC.Center;
            stepDir = toLand.SafeNormalize(Vector2.UnitX);
            stepTravelNeeded = (int)MathHelper.Clamp(MathF.Ceiling(toLand.Length() / StepSpeed) + 1f, 6f, 15f);
            NPC.velocity = stepDir * StepSpeed;
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.15f, Volume = 1.2f }, NPC.Center);
            if (!VaultUtils.isServer)
                ACMScreenShakeSystem.Add(4f);
        }

        /// <summary>
        /// 落地震山：硬刹第一帧的冲击反馈（震屏 + 金尘环 + 落地闷响）。
        /// spawnShock: 是否释放贴地地波（服务器）。shockTravel: 地波射程。
        /// </summary>
        private void StepLandImpact(bool spawnShock, float shockTravel, int shockDamageDiv = 3) {
            shockDamageDiv = Math.Max(1, shockDamageDiv);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);
            if (!VaultUtils.isServer) {
                ACMScreenShakeSystem.Add(7f);
                for (int i = 0; i < 18; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 9f);
                    vel.Y = -MathF.Abs(vel.Y) * 0.7f;
                    Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(0, NPC.height * 0.4f), DustID.GoldFlame, vel, 100, default, 1.7f);
                    d.noGravity = true;
                }
            }

            if (spawnShock && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 spawn = NPC.Center + new Vector2(0, NPC.height * 0.2f);
                float speed = Main.expertMode ? 9f : 7.5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(-speed, 0),
                    ModContent.ProjectileType<ImmortalGroundShock>(), NPC.defDamage / shockDamageDiv, 0f, Main.myPlayer, ai0: shockTravel);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(speed, 0),
                    ModContent.ProjectileType<ImmortalGroundShock>(), NPC.defDamage / shockDamageDiv, 0f, Main.myPlayer, ai0: shockTravel);
            }
        }

        #endregion

        #region 屏幕氛围发布 / 视觉状态

        /// <summary>
        /// 向 <see cref="VaisravanaTreasureScreenSystem"/> 发布库藏金幕氛围标量（纯本地视觉, 仅客户端）。
        /// 坛城法阵(专属 VaisravanaMandala 着色器)由各大招/演出在此统一发布。
        /// </summary>
        private void PublishScreenState() {
            // 库藏开启度：一阶段微金、二阶段渐浓、三阶段金光笼罩
            float goldTint = 0.05f;
            if (IsPhase3) goldTint = 0.30f;
            else if (IsPhase2) goldTint = 0.14f;

            bool runeActive = false;
            Vector2 runeCenter = NPC.Center;
            float runeIntensity = 0f;
            float runeRadius = 320f;
            float runeReveal = 1f;

            switch (Phase) {
                case BossPhase.Phase3_UltimateTower: {
                    // 终极宝塔：70 tick 蓄力逐圈点亮 + 发射期保持
                    runeActive = true;
                    if ((int)SubState == 0) {
                        float charge = MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f);
                        runeIntensity = 0.35f + charge * 0.65f;
                        runeReveal = charge;
                        runeRadius = MathHelper.Lerp(260f, 560f, charge);
                        goldTint = Math.Max(goldTint, 0.30f + charge * 0.40f);
                    }
                    else {
                        runeIntensity = 1f;
                        runeReveal = 1f;
                        runeRadius = 560f;
                        goldTint = Math.Max(goldTint, 0.55f);
                    }
                    break;
                }
                case BossPhase.Phase1_VajraPierce: {
                    // 金刚破军：蓄力期脚下坛城逐圈点亮
                    if ((int)SubState <= 1) {
                        float charge = MathHelper.Clamp(chargeConverge, 0f, 1f);
                        runeActive = charge > 0.02f;
                        runeIntensity = 0.3f + charge * 0.6f;
                        runeReveal = charge;
                        runeRadius = MathHelper.Lerp(200f, 380f, charge);
                    }
                    break;
                }
                case BossPhase.PhaseTransition_2: {
                    // 显法相：全阵点亮
                    float t = MathHelper.Clamp((PhaseTimer - 30f) / 48f, 0f, 1f);
                    runeActive = t > 0f;
                    runeIntensity = t;
                    runeReveal = t;
                    runeRadius = 520f;
                    goldTint = Math.Max(goldTint, 0.20f + t * 0.25f);
                    break;
                }
                case BossPhase.PhaseTransition_3: {
                    // 库藏开启：身后天库之门
                    float t = MathHelper.Clamp((PhaseTimer - 35f) / 35f, 0f, 1f);
                    runeActive = t > 0f;
                    runeIntensity = t;
                    runeReveal = t;
                    runeRadius = 640f;
                    goldTint = Math.Max(goldTint, 0.25f + t * 0.35f);
                    break;
                }
                case BossPhase.Death: {
                    // 金身崩解：光阵失稳 → 终爆全屏扩散
                    runeActive = true;
                    if (PhaseTimer < 200f) {
                        float destab = MathHelper.Clamp(PhaseTimer / 200f, 0f, 1f);
                        runeIntensity = 0.5f + MathF.Sin((float)PhaseTimer * 0.37f) * 0.22f * destab;
                        runeReveal = 1f - destab * 0.35f;
                        runeRadius = 420f;
                    }
                    else {
                        float burst = MathHelper.Clamp((PhaseTimer - 200f) / 60f, 0f, 1f);
                        runeIntensity = 1f - burst;
                        runeReveal = 1f;
                        runeRadius = MathHelper.Lerp(420f, 1150f, ACMUtils.SineInOut(burst));
                    }
                    goldTint = Math.Max(goldTint, 0.4f);
                    break;
                }
            }

            VaisravanaTreasureScreenSystem.Publish(NPC.Center, goldTint, runeActive, runeCenter,
                runeRadius, runeIntensity, runeReveal, (float)Main.GlobalTimeWrappedHourly);
        }

        private void UpdateVisualEffects() {
            haloRotation += 0.008f;

            if (IsPhase3) {
                haloScale = 1.5f + MathF.Sin(globalTime * 3.5f) * 0.2f;
                glowIntensity = 1.6f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.85f, 0.04f);
            }
            else if (IsPhase2) {
                haloScale = 1.25f + MathF.Sin(globalTime * 2.5f) * 0.12f;
                glowIntensity = 1.3f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.55f, 0.04f);
            }
            else {
                haloScale = 1f + MathF.Sin(globalTime * 1.8f) * 0.06f;
                glowIntensity = 1f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.35f, 0.04f);
            }

            // 法相常驻强度：二阶段起低强度常驻（换阶段/死亡演出中由脚本直接驱动）
            if (Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Death) {
                float dharmaTarget = IsPhase3 ? 0.45f : (IsPhase2 ? 0.32f : 0f);
                dharmaAura = MathHelper.Lerp(dharmaAura, dharmaTarget, 0.03f);
            }

            // 蓄力汇聚强度自然回落（各招式内部拉升）
            if (Phase != BossPhase.Phase1_VajraPierce)
                chargeConverge = MathHelper.Lerp(chargeConverge, 0f, 0.1f);
        }

        private void CheckPhaseTransition() {
            if (Phase == BossPhase.Death)
                return;

            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                ClearBattlefield();
                didPhase2Transition = true;
            }

            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.PhaseTransition_3 && Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_3);
                ClearBattlefield();
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            dashCount = 0;
            guardActive = false;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 清场（公平阀门）：清掉本 Boss 的全部敌对弹幕；fullClear 时连夜叉一并散去（死亡演出用）。
        /// </summary>
        private void ClearBattlefield(bool fullClear = false) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.hostile)
                    continue;
                if (p.type == ModContent.ProjectileType<TreasureTowerOrb>() ||
                    p.type == ModContent.ProjectileType<TowerBeam>() ||
                    p.type == ModContent.ProjectileType<VaisravanaStar>() ||
                    p.type == ModContent.ProjectileType<SweepingLightBolt>() ||
                    p.type == ModContent.ProjectileType<ImmortalGroundShock>() ||
                    p.type == ModContent.ProjectileType<TreasurySealRing>() ||
                    p.type == ModContent.ProjectileType<YakshaMirrorBolt>() ||
                    p.type == ModContent.ProjectileType<QuadrantRay>() ||
                    p.type == ModContent.ProjectileType<TreasureTowerRay>() ||
                    p.type == ModContent.ProjectileType<VajraSpear>() ||
                    p.type == ModContent.ProjectileType<VaisravanaLightPillar>()) {
                    p.Kill();
                }
            }

            if (fullClear && yakshaMinionIds != null) {
                for (int i = 0; i < yakshaMinionIds.Length; i++) {
                    int id = yakshaMinionIds[i];
                    if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active &&
                        Main.npc[id].type == ModContent.NPCType<YakshaMinion>()) {
                        Main.npc[id].life = 0;
                        Main.npc[id].checkDead();
                    }
                    yakshaMinionIds[i] = -1;
                }
            }
        }

        /// <summary>方向 dir(0=北,1=东,2=南,3=西) 的夜叉是否存活。</summary>
        private bool YakshaAlive(int dir) {
            if (yakshaMinionIds == null) return false;
            int id = yakshaMinionIds[dir];
            return id >= 0 && id < Main.maxNPCs && Main.npc[id].active &&
                   Main.npc[id].type == ModContent.NPCType<YakshaMinion>();
        }

        #endregion

        #region 出场演出「天王降世」

        /// <summary>
        /// 入场 180f：背景金点冲镜(假 Z) → 落场震地拍 → 60f 静止凝视(威严=静止) → 四塔依次升起。
        /// introProgress 供绘制层做假 Z 缩放。
        /// </summary>
        private void RunIntro(Player target) {
            // 0~70f: 从背景逼近（绘制层按 introProgress 缩放 0.1→1, 模拟纵深冲镜）
            if (PhaseTimer <= 70f) {
                introProgress = ACMUtils.SineInOut(MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f));
                introProgress = introProgress * introProgress; // cubed 观感: 后半段猛然放大
                Vector2 anchor = target.Center + new Vector2(0, -320f);
                // 高空缓降至落点
                NPC.Center = anchor + new Vector2(0, -240f * (1f - introProgress));
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0.25f + introProgress * 0.75f;

                if (PhaseTimer == 2)
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = 0.2f, Volume = 1.1f }, target.Center);

                // 冲镜金尘
                if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200f, 200f) * (1f - introProgress * 0.7f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame,
                        (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f, 120, default, 1.8f);
                    d.noGravity = true;
                }

                // 70f: 落场拍 —— 震屏 + 脚下坛城弹性展开(绘制层) + 金尘环
                if (PhaseTimer == 70f) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.35f, Volume = 1.5f }, NPC.Center);
                    if (!VaultUtils.isServer) {
                        ACMScreenShakeSystem.Add(14f);
                        VaisravanaTreasureScreenSystem.PulseBloom(0.5f);
                        for (int i = 0; i < 26; i++) {
                            float ang = MathHelper.TwoPi * i / 26f;
                            Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(0, 50f), DustID.GoldFlame,
                                ang.ToRotationVector2() * Main.rand.NextFloat(4f, 11f), 90, default, 2.1f);
                            d.noGravity = true;
                        }
                    }
                }
                return;
            }

            introProgress = 1f;
            NPC.Opacity = 1f;

            // 70~130f: 静止凝视 —— 威严来自静止，无任何动作，只有光轮缓亮
            if (PhaseTimer <= 130f) {
                NPC.velocity *= 0.8f;
                return;
            }

            // 130~178f: 四座宝塔从身后金光中依次升起（每 12f 一座，音阶递升）
            int riseStep = (int)((PhaseTimer - 130f) / 12f);
            for (int i = 0; i < TowerCount; i++) {
                if (i < riseStep)
                    towerRise[i] = MathHelper.Clamp(towerRise[i] + 0.09f, 0f, 1f);
            }
            if ((PhaseTimer - 130f) % 12f == 0f && riseStep >= 1 && riseStep <= TowerCount) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.1f + riseStep * 0.15f, Volume = 0.9f }, NPC.Center);
                if (!VaultUtils.isServer && riseStep - 1 < TowerCount) {
                    Vector2 tp = GetTowerPosition(riseStep - 1);
                    for (int k = 0; k < 8; k++) {
                        Dust d = Dust.NewDustPerfect(tp, DustID.GoldFlame, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            if (PhaseTimer == 178f) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(10f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.6f);
                }
            }

            if (PhaseTimer > 185f) {
                for (int i = 0; i < TowerCount; i++) towerRise[i] = 1f;
                TransitionTo(BossPhase.Phase1_Hub);
            }
        }

        #endregion

        #region 死亡演出「金身崩解」(260f)

        /// <summary>
        /// 死亡脚本：光轮失稳 → 四塔依次熄灭爆散 → 金身龟裂 → 40f 全场静默 → 金光大爆(白闪
        /// impact frame + 震屏 18, 全战唯一一次) → 金尘飘散 → 真死掉落。
        /// </summary>
        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            guardActive = false;
            NPC.damage = 0;

            float t = PhaseTimer;

            // 0~60f: 升至半空定身，光轮高频闪烁失稳
            if (t <= 60f) {
                Vector2 hoverPos = NPC.Center + new Vector2(0, -0.6f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center), 0.1f);
                towerOrbitSpeed = 0.015f + t * 0.0012f; // 塔轨道渐紊乱
                dharmaAura = MathHelper.Lerp(dharmaAura, 0.9f, 0.05f);

                if (t == 2f)
                    SoundEngine.PlaySound(SoundID.NPCDeath62 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                return;
            }

            // 60~160f: 四塔每 25f 一座熄灭爆散（音阶递降），金身开始龟裂
            if (t <= 160f) {
                NPC.velocity *= 0.9f;
                bodyCrack = MathHelper.Clamp((t - 60f) / 140f * 0.7f, 0f, 0.7f);

                int extinguishIndex = (int)((t - 60f) / 25f);
                if ((t - 60f) % 25f == 0f && extinguishIndex < TowerCount) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f - extinguishIndex * 0.2f, Volume = 1.1f }, GetTowerPosition(extinguishIndex));
                    if (!VaultUtils.isServer) {
                        Vector2 tp = GetTowerPosition(extinguishIndex);
                        ACMScreenShakeSystem.Add(4f);
                        for (int k = 0; k < 16; k++) {
                            Dust d = Dust.NewDustPerfect(tp, k % 3 == 0 ? DustID.GoldCoin : DustID.GoldFlame,
                                Main.rand.NextVector2Circular(6f, 6f) + new Vector2(0, 2f), 80, default, 1.8f);
                            d.noGravity = k % 3 != 0;
                        }
                    }
                }
                for (int i = 0; i < TowerCount; i++) {
                    if (i <= extinguishIndex)
                        towerRise[i] = MathHelper.Clamp(towerRise[i] - 0.06f, 0f, 1f);
                }

                // 金身漏光金尘 ∝ 进度
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.5f, NPC.height * 0.5f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 100, default, 1.6f);
                    d.noGravity = true;
                }
                return;
            }

            // 160~200f: 收缩静默 —— 一切粒子硬切，本体收缩颤闪（爆发前的吸气）
            if (t <= 200f) {
                particleSilence = true;
                NPC.velocity = Vector2.Zero;
                float shrinkT = (t - 160f) / 40f;
                NPC.scale = MathHelper.Lerp(1f, 0.42f, ACMUtils.SineInOut(shrinkT)) *
                            (1f + MathF.Cos(t * 0.8f) * 0.05f * shrinkT);
                bodyCrack = MathHelper.Lerp(0.7f, 0.85f, shrinkT);

                if (t == 161f)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.8f, Volume = 0.7f }, NPC.Center);
                return;
            }

            // 200f: 金光大爆 —— 全战唯一一次 impact frame
            if (t == 201f) {
                particleSilence = false;
                SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(18f);
                    VaisravanaTreasureScreenSystem.PulseWhiteFlash(1f);
                    VaisravanaTreasureScreenSystem.PulseBloom(1f);
                    for (int i = 0; i < 40; i++) {
                        float ang = MathHelper.TwoPi * i / 40f;
                        Dust d = Dust.NewDustPerfect(NPC.Center, i % 4 == 0 ? DustID.GoldCoin : DustID.GoldFlame,
                            ang.ToRotationVector2() * Main.rand.NextFloat(6f, 16f), 60, default, 2.3f);
                        d.noGravity = i % 4 != 0;
                    }
                }
            }

            // 200~260f: 金尘飘散 + 本体溶解淡出
            if (t > 201f) {
                float fade = MathHelper.Clamp((t - 201f) / 55f, 0f, 1f);
                bodyCrack = MathHelper.Lerp(0.85f, 1f, fade);
                NPC.Opacity = 1f - fade * 0.9f;
                NPC.scale = MathHelper.Lerp(0.42f, 0.55f, fade);
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(90f, 90f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.5f, 3.5f)), 110, default, 1.4f);
                    d.noGravity = true;
                }
            }

            if (t >= 260f && Main.netMode != NetmodeID.MultiplayerClient) {
                deathScriptDone = true;
                NPC.life = 0;
                NPC.checkDead();
                NPC.netUpdate = true;
            }
        }

        #endregion
    }
}

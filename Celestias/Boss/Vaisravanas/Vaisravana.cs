using System;
using System.IO;
using AncientChineseMythology.Celestias.Boss.Vaisravanas.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
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
    /// 毗沙门天王 - 北方多闻天王 / 财神 · 四大天王之首 · 天将线终局门控 Boss (T35)
    ///
    /// 重做核心：与「天庭观察者」拆分换皮，建立独立的【财宝·宝塔·守护】身份。
    /// 签名机制：宝塔充能（Pagoda Stack）——四座环绕宝塔各持 0-3 点充能，
    /// Boss 的攻击由宝塔充能驱动；玩家贴近宝塔的「赐福区」可窃取一点充能，
    /// 将本应瞬发的攻击转化为带预告的安全光束（防御/走位主导，而非更多弹幕）。
    /// 一阶段：宝塔威光——充能驱动的控场弹幕，教学赐福窃取。
    /// 二阶段：天王降临——召唤四方夜叉锚点 + 随地形起伏的仙气地波 + 守护反击窗口。
    /// 三阶段：库藏封印——脚本化 A/B/C 三幕轮替（金环收束 / 夜叉镜射 / 终极宝塔），
    ///         绝不退化为低血量加速喷弹。
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

        // 宝塔状态
        private float[] towerAngles;
        private float[] towerDistances;
        private float towerOrbitSpeed;

        // 宝塔充能机制（签名机制）
        private int[] towerCharges;          // 各塔当前充能 0..MaxTowerCharge
        private int[] towerRechargeTimer;    // 各塔回充计时
        private int pendingBlessing;         // 已窃取、可转化下次攻击的赐福数
        private int blessingStealCooldown;   // 窃取冷却
        private int lastBlessedTower = -1;   // 最近被窃取的塔（用于绘制反馈）
        private int blessingFlash;           // 窃取闪光计时（绘制用）

        // 攻击控制
        private Vector2 dashTarget;          // 网络同步保留
        private Vector2 dashVelocity;        // SpringDamp2D 阻尼累积
        private int dashCount;               // 网络同步保留

        // 星辰控制
        private int starCount;
        private Vector2[] starPositions;

        // 激光 / 镜轴控制
        private float laserAngle;
        private float laserSweepDirection;

        // 夜叉仆从控制（四方锚点）
        private int[] yakshaMinionIds;

        // 守护反击窗口（借鉴玄武 绝对防御）
        private bool guardActive;
        private int guardReflectCooldown;
        private float guardVisual;

        // 三阶段库藏封印轮替
        private int sealCycle;               // 0=金环 1=镜射 2=终极

        // 视觉效果
        private float haloRotation;
        private float haloScale;
        private float glowIntensity;
        private float divineAuraAlpha;

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

            // 初始化星辰
            starPositions = new Vector2[16];

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
            writer.WriteVector2(dashTarget);
            writer.Write(dashCount);
            writer.Write((byte)pendingBlessing);
            writer.Write((byte)sealCycle);
            writer.Write(guardActive);
            if (towerCharges == null) InitializeTowers();
            for (int i = 0; i < TowerCount; i++) writer.Write((byte)towerCharges[i]);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            dashTarget = reader.ReadVector2();
            dashCount = reader.ReadInt32();
            pendingBlessing = reader.ReadByte();
            sealCycle = reader.ReadByte();
            guardActive = reader.ReadBoolean();
            if (towerCharges == null) InitializeTowers();
            for (int i = 0; i < TowerCount; i++) towerCharges[i] = reader.ReadByte();

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

        /// <summary>
        /// 守护反击 — 处于「守护姿态」窗口时无敌并向攻击者迸射招财金镖（财神反震）。
        /// 借鉴玄武 绝对防御 的受击反射，但主题为「劫财反震」。
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
                    ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 4, 2f, Main.myPlayer);
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

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
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

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_Hub:
                    RunPhase1Hub(target);
                    break;
                case BossPhase.Phase1_TowerVolley:
                    RunPhase1TowerVolley(target);
                    break;
                case BossPhase.Phase1_SweepingLight:
                    RunPhase1SweepingLight(target);
                    break;
                case BossPhase.Phase1_StarRain:
                    RunPhase1StarRain(target);
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
            }

            // 仙气白光照明
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.98f, 0.95f) * glowIntensity);

            // 宝塔光照
            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i);
                float chargeGlow = 0.4f + towerCharges[i] * 0.18f;
                Lighting.AddLight(towerPos, new Vector3(1f, 0.92f, 0.7f) * chargeGlow);
            }
        }

        private void InitializeTowers() {
            towerAngles = new float[TowerCount];
            towerDistances = new float[TowerCount];
            towerCharges = new int[TowerCount];
            towerRechargeTimer = new int[TowerCount];
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] = MathHelper.TwoPi * i / TowerCount;
                towerDistances[i] = 180f;
                towerCharges[i] = MaxTowerCharge;
            }
        }

        private void UpdateTowers() {
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] += towerOrbitSpeed;

                float baseDistance = 180f;
                if (IsPhase2) baseDistance = 210f;
                if (IsPhase3) baseDistance = 240f;

                towerDistances[i] = baseDistance + MathF.Sin(globalTime * 1.8f + i * 0.6f) * 18f;
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

            // 自动回充（出场/转阶段不回充，避免堆叠）
            bool canRecharge = Phase != BossPhase.Intro &&
                               Phase != BossPhase.PhaseTransition_2 &&
                               Phase != BossPhase.PhaseTransition_3;
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
            if (Main.netMode != NetmodeID.MultiplayerClient && blessingStealCooldown <= 0 && pendingBlessing < MaxPendingBlessing) {
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

        /// <summary>消费一点指定/随机宝塔充能，返回是否成功（用于把攻击强度与塔挂钩）。</summary>
        private bool ConsumeTowerCharge(int index) {
            if (index >= 0 && index < TowerCount && towerCharges[index] > 0) {
                towerCharges[index]--;
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
            float distance = towerDistances[index];
            return NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        /// <summary>
        /// 向 <see cref="VaisravanaTreasureScreenSystem"/> 发布库藏金幕氛围标量（纯本地视觉, 仅客户端）。
        /// 库藏开启度随阶段升高; 终极宝塔（Pagoda Apex）蓄力期点亮地面坛城符文并加深金幕。
        /// </summary>
        private void PublishScreenState() {
            // 库藏开启度：一阶段微金、二阶段渐浓、三阶段金光笼罩
            float goldTint = 0.05f;
            if (IsPhase3) goldTint = 0.30f;
            else if (IsPhase2) goldTint = 0.14f;

            // 终极宝塔坛城符文（70 tick 蓄力逐圈点亮 + 蓄满发射期保持）
            bool runeActive = false;
            float runeIntensity = 0f;
            float runeRadius = 320f;
            if (Phase == BossPhase.Phase3_UltimateTower) {
                runeActive = true;
                if ((int)SubState == 0) {
                    float charge = MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f);
                    runeIntensity = charge;
                    runeRadius = MathHelper.Lerp(240f, 560f, charge);
                    goldTint = Math.Max(goldTint, 0.30f + charge * 0.40f);
                }
                else {
                    runeIntensity = 1f;
                    runeRadius = 560f;
                    goldTint = Math.Max(goldTint, 0.55f);
                }
            }

            VaisravanaTreasureScreenSystem.Publish(NPC.Center, goldTint, runeActive, NPC.Center,
                runeRadius, runeIntensity, (float)Main.GlobalTimeWrappedHourly);
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
            guardActive = false;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        /// <summary>方向 dir(0=北,1=东,2=南,3=西) 的夜叉是否存活。</summary>
        private bool YakshaAlive(int dir) {
            if (yakshaMinionIds == null) return false;
            int id = yakshaMinionIds[dir];
            return id >= 0 && id < Main.maxNPCs && Main.npc[id].active &&
                   Main.npc[id].type == ModContent.NPCType<YakshaMinion>();
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 180f, 0f, 1f);

            Vector2 introOffset = new Vector2(0, -650) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -360) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.025f);
            NPC.velocity *= 0.9f;

            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(170, 170) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(120, 120);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, -1.5f, 150, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 55) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 115) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(14f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.5f);
                }
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase1_Hub);
            }
        }

        #endregion
    }
}

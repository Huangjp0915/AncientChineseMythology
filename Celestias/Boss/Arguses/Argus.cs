using AncientChineseMythology.Celestias.Boss.Arguses.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 天目·追魂弧 / Argus, the Soul-Piercing Arc — 天界独眼弓将 (后月球领主, 约200万HP)。
    ///
    /// V3 重做核心:
    ///   ● 凝视语法 — 全 Boss 统一电报: 细视线游移追踪 → 吸附锁定(弹道就此冻结, 绝不追身)
    ///     → 增粗发亮(紫→红) → 熄灭一拍 → 星箭齐射。读懂锁定时机即可稳定躲避。
    ///   ● 星阵秩序 — 星轨球以几何编队出现(环/双环/坍缩阵), 成形延迟无伤害, 恒留安全缝。
    ///   ● 光弓重量 — BeamGrad 曲带程序化光弓: 大威力射击必见拉弦, 释放必有弦震荡与实体后坐。
    ///   ● 手写循环选招(压力/呼吸交替, 永不连续重复), 大招押在各阶段循环尾部。
    ///   ● 三大演出: 深空浮现+静止凝视入场 / 眨眼碎裂转阶段 / 星尘溶解闭眼死亡 (CheckDead 编排)。
    ///
    /// 阶段: P1 审视(100~60%) → P2 追猎(60~30%) → P3 天目审判(30~0%)。
    /// </summary>
    [AutoloadBossHead]
    public class Argus : ModNPC
    {
        #region 常量与配色

        internal const float Phase2Threshold = 0.60f;
        internal const float Phase3Threshold = 0.30f;

        private const float PreferredDistance = 560f;

        private static readonly Color ArgusPurple = new(185, 105, 255);
        private static readonly Color ArgusBlue = new(95, 145, 255);

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Reposition,          // 连接拍: 换位+至多一发骚扰箭 (刻意呼吸)

            // P1 审视
            P1_GazeTriple,       // 三连锁定 — 凝视语法教学
            P1_ArcVolley,        // 弧射箭幕 — 带缺口扇形齐射
            P1_StarRing,         // 星阵环 — 单环收缩+锁定单射
            P1_MeteorGraze,      // 流星掠射 — 唯一高速位移招

            Transition2,

            // P2 追猎
            P2_FlashstepBarrage, // 瞬移连射 — 预告闪现四连
            P2_TwinCage,         // 双环星笼 — 内外双环反向旋转
            P2_SentryEyes,       // 百目哨兵 — 部署分体之眼
            P2_StarfallColumns,  // 星落列雨 — 结构化列式箭雨
            P2_SniperDuel,       // 狙击对决 — 处决级凝视贯穿

            Transition3,

            // P3 天目审判
            P3_MyriadEyes,       // 万目凝射 — 环形眼阵交叉射击
            P3_Collapse,         // 星阵坍缩 — 全视之域签名
            P3_GazeSweep,        // 天目扫射 — 剪刀双扫射线
            P3_FinalJudgment,    // 最终审判 — 蓄力+三重处决 (impact frame)

            Death                // 死亡演出
        }

        #endregion

        #region 同步状态

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
        private int cycleIndex;      // 手写循环选招索引
        private int flashStep;       // 瞬移/审判等多段招内的段计数
        private int windup;          // 转阶段后弹速渐升阀门 (60→0)
        private Vector2 gazeAim;     // 凝视锁定点/招式锚点 (锁定后弹道冻结的依据)

        #endregion

        #region 视觉状态 (纯本地, 各端由同步计时器确定性推导)

        private float bowAngle;          // 光弓朝向
        private float bowDraw;           // 拉弦程度 0~1 (弹簧)
        private float bowDrawVel;
        private float bowDrawTarget;
        private float stringSnap;        // 释放后弦震荡计时
        private Vector2 recoilVisual;    // 后坐视觉偏移 (指数衰减)
        private float eyeOpen = 1f;      // 光环巨眼睁眼度 (弹簧)
        private float eyeOpenVel;
        private float eyeOpenTarget = 1f;
        private float eyeSlit;           // 竖瞳化 (P3)
        private float eyeNova;           // 锁定充能 0~1 (虹膜变红)
        private float drawScale = 1f;    // 绘制缩放 (入场 fake-Z / 死亡坍缩)
        private float glowIntensity = 1f;
        private float gazeBeamFlash;     // 处决命中沿线白闪
        private Vector2 gazeFlashFrom, gazeFlashTo;
        private float bloomBurst;        // 离散径向泛光脉冲
        private float bloomBurstRadius;
        private Color bloomBurstColor = new(190, 110, 255);
        private float domainPower;       // 全视之域签名进度
        private float ambientVoid;       // "被注视"氛围染色
        private float gazeFocus;         // 狙击对决聚焦暗角
        private float impactFrame;       // 全场唯一 impact frame (PaletteLUT 黑白)
        private int impactHold;
        private float deathDissolve;     // 死亡溶解进度

        /// <summary>供 <see cref="ArgusSky"/> 读取的天幕巨眼锁定信号 (0~1)。</summary>
        public static float DomainSignal;
        /// <summary>供 <see cref="ArgusSky"/> 读取的眨眼信号 (0=睁 1=闭, 随 Boss 光环眼联动)。</summary>
        public static float SkyBlink;
        /// <summary>供 <see cref="ArgusSky"/> 读取的死亡闭眼进度 (0~1)。</summary>
        public static float SkyDeathClose;

        #endregion

        #region 着色器缓存

        private static Asset<Effect> eyeIrisRef;

        private static Effect EyeIrisEffect {
            get {
                if (Main.dedServ)
                    return null;
                eyeIrisRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/ArgusEyeIris", AssetRequestMode.ImmediateLoad);
                return eyeIrisRef?.Value;
            }
        }

        #endregion

        #region 选招循环 (手写顺序: 压力/区域/精准/机动 交替, 大招收尾)

        private static readonly BossPhase[] P1Cycle = {
            BossPhase.P1_GazeTriple, BossPhase.P1_ArcVolley, BossPhase.P1_StarRing, BossPhase.P1_MeteorGraze
        };
        private static readonly BossPhase[] P2Cycle = {
            BossPhase.P2_FlashstepBarrage, BossPhase.P2_TwinCage, BossPhase.P2_SentryEyes,
            BossPhase.P2_StarfallColumns, BossPhase.P2_SniperDuel
        };
        private static readonly BossPhase[] P3Cycle = {
            BossPhase.P3_MyriadEyes, BossPhase.P3_Collapse, BossPhase.P3_GazeSweep, BossPhase.P3_FinalJudgment
        };

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 160;
            NPC.damage = 180;
            NPC.defense = 70;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit56;
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

        public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>()));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SoulPiercingArc>(),
                ModContent.ItemType<LuminanceStellarCannon>(),
                ModContent.ItemType<LuminousIrisAnnihilator>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            //上一场战斗的静态天幕信号不得残留
            DomainSignal = 0f;
            SkyBlink = 0f;
            SkyDeathClose = 0f;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(cycleIndex);
            writer.Write(flashStep);
            writer.Write(windup);
            writer.WriteVector2(gazeAim);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            cycleIndex = reader.ReadInt32();
            flashStep = reader.ReadInt32();
            windup = reader.ReadInt32();
            gazeAim = reader.ReadVector2();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            if (Phase == BossPhase.Death)
                return false;
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        //接触伤害只在流星掠射的高速段激活 (伤害窗口与视觉严格对齐)
        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
            => Phase == BossPhase.P1_MeteorGraze && NPC.velocity.Length() > 30f;

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 5; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, hit.HitDirection * 2f, -1f, 150, default, 1.4f);
        }

        /// <summary>首次归零 → 拦截进入死亡演出; 演出走完后放行真死。</summary>
        public override bool CheckDead() {
            if (Phase != BossPhase.Death) {
                NPC.life = 1;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Death);
                NPC.dontTakeDamage = true; //须在 TransitionTo 之后 (其会复位该标志)
                return false;
            }
            return true;
        }

        public override void OnKill() {
            DownedBossSystem.downedArgus = true;
            DomainSignal = 0f;
            SkyBlink = 0f;
            SkyDeathClose = 1f;
            if (Main.netMode != NetmodeID.Server)
                ACMScreenShakeSystem.Add(10f);
        }

        #endregion

        #region 通用工具

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            flashStep = 0;
            //状态出口保底: 无敌标志不得跨状态残留 (Death/各状态自行每帧重设)
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        /// <summary>结束连接拍, 按手写循环取下一招。</summary>
        private void NextAttack() {
            BossPhase[] cycle = IsPhase3 ? P3Cycle : IsPhase2 ? P2Cycle : P1Cycle;
            BossPhase next = cycle[cycleIndex % cycle.Length];
            cycleIndex++;
            TransitionTo(next);
        }

        /// <summary>速度导向悬停 (dist*k 封顶产生软到达)。</summary>
        private void HoverTo(Vector2 dest, float maxSpeed, float accel) {
            Vector2 to = dest - NPC.Center;
            Vector2 desired = to.SafeNormalize(Vector2.Zero) * MathF.Min(maxSpeed, to.Length() * 0.08f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, accel);
        }

        /// <summary>距离栓绳: 离目标过远时强力回拉, 防止飞出屏幕绕圈。</summary>
        private void Leash(Player target) {
            if (NPC.Distance(target.Center) > 1500f)
                NPC.velocity = Vector2.Lerp(NPC.velocity,
                    NPC.SafeDirectionTo(target.Center) * 22f, 0.06f);
        }

        /// <summary>转阶段后弹速渐升阀门 (首击 40% 速度, 60tick 内恢复)。</summary>
        private float WindupSpeed(float speed)
            => windup > 0 ? speed * MathHelper.Lerp(1f, 0.4f, windup / 60f) : speed;

        private void SpawnArrow(Vector2 pos, Vector2 vel, float dmgScale, float mode = 0f, float ai1 = 0f, float ai2 = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<StarSightArrows>(), (int)(NPC.damage * dmgScale), 1f, Main.myPlayer,
                mode, ai1, ai2);
        }

        /// <summary>
        /// 生成一圈轨道星球 (速度旋转运动学: 生成即确定, 无需锚点同步)。
        /// skipA/skipB = 留空的索引 (安全缝), -1 为不留。
        /// </summary>
        private void SpawnOrbRing(Vector2 center, int count, float radius, float omega, float drift,
            float chargeTicks, int skipA = -1, int skipB = -1, float angleOffset = 0f, float dmgScale = 0.2f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            float tangentSpeed = MathF.Abs(omega) * radius;
            for (int i = 0; i < count; i++) {
                if (i == skipA || i == skipB)
                    continue;
                float ang = MathHelper.TwoPi / count * i + angleOffset;
                Vector2 radial = ang.ToRotationVector2();
                //圆周运动: 速度 = 切向 (径向转 +90° 与 ω 同向自洽)
                Vector2 vel = radial.RotatedBy(MathHelper.PiOver2 * MathF.Sign(omega)) * tangentSpeed;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), center + radial * radius, vel,
                    ModContent.ProjectileType<SpinningGalacticOrbs>(), (int)(NPC.damage * dmgScale), 0f, Main.myPlayer,
                    omega, drift, chargeTicks);
            }
        }

        private static void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.hostile && p.damage > 0)
                    p.Kill();
            }
        }

        private void TriggerBloom(float radius, Color color) {
            bloomBurst = 1f;
            bloomBurstRadius = radius;
            bloomBurstColor = color;
        }

        private void ShakeLocal(float amount) {
            if (Main.netMode != NetmodeID.Server)
                ACMScreenShakeSystem.Add(amount);
        }

        #endregion

        #region 凝视语法 (统一电报核心)

        /// <summary>
        /// 凝视语法推进器: 追踪 trackT → 锁定 lockT (服务器裁定 gazeAim, 弹道冻结) → 熄灭 darkT → 开火。
        /// 每帧调用; t 为序列局部时间; 返回 true 表示本帧是开火帧 (调用方生成箭+后坐)。
        /// </summary>
        private bool GazeStep(Player target, float t, int trackT, int lockT, int darkT, float leadSpeed) {
            if (t < 0)
                return false;

            float fireTick = trackT + lockT + darkT;

            if (t < trackT) {
                //追踪期: 细线游移吸向目标, 亮度渐升; 弓随之拉开
                float p = t / trackT;
                bowDrawTarget = MathF.Max(bowDrawTarget, p * 0.85f);
                if (!Main.dedServ) {
                    Vector2 toT = target.Center - NPC.Center;
                    Vector2 perp = toT.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                    Vector2 wobble = perp * MathF.Sin(globalTime * 7.3f + t * 0.21f) * 70f * (1f - p);
                    QueueSight(NPC.Center, target.Center + wobble, 1.4f + p * 1.6f,
                        ArgusPurple, 0.22f + 0.4f * p);
                }
                return false;
            }

            if (t == trackT) {
                //锁定裁定: 各端同式预测消除视觉延迟, 服务器 netUpdate 下发权威值 (弹道就此冻结 — 绝不追身)
                Vector2 dir = ACMUtils.LeadTarget(NPC.Center, target.Center, target.velocity, leadSpeed);
                gazeAim = NPC.Center + dir * MathF.Max(NPC.Distance(target.Center), 240f);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.1f, Volume = 0.7f }, NPC.Center);
            }

            if (t < trackT + lockT) {
                //锁定期: 红线冻结增粗, 虹膜充能; 玩家在走廊内 → 屏幕边缘"被看见"警示
                float p = (t - trackT) / lockT;
                bowDrawTarget = MathF.Max(bowDrawTarget, 0.85f + p * 0.15f);
                eyeNova = MathF.Max(eyeNova, p);
                if (!Main.dedServ) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitX);
                    QueueSight(NPC.Center, gazeAim + dir * 500f, 2.2f + p * 2.4f,
                        Color.Lerp(ArgusPurple, TelegraphColors.Lethal, 0.4f + p * 0.6f), 0.6f + p * 0.4f);
                    ArgusFx.ReportIfLocalPlayerSighted(NPC.Center, dir, 1700f, 150f, 0.55f + p * 0.45f);
                }
                return false;
            }

            //熄灭一拍: 刻意黑暗 (爆发前的吸气)
            if (t < fireTick) {
                bowDrawTarget = MathF.Max(bowDrawTarget, 1f);
                return false;
            }

            return t == fireTick;
        }

        /// <summary>开火收拍: 弦回弹 + 实体后坐 + 命中线白闪 + 音效 (量级按 power 缩放)。</summary>
        private void FireBeat(float power, bool flashLine = false) {
            Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitX);
            NPC.velocity -= dir * (5f + power * 22f);
            stringSnap = 1f;
            eyeNova = 1f;
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.35f - power * 0.4f, Volume = 0.8f + power * 0.4f }, NPC.Center);
            ShakeLocal(3f + power * 6f);
            if (flashLine) {
                gazeBeamFlash = 1f;
                gazeFlashFrom = NPC.Center;
                gazeFlashTo = gazeAim + dir * 700f;
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            globalTime += 1f / 60f;
            if (!Main.dedServ)
                sightQueue.Clear();
            bowDrawTarget = 0.12f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if ((!target.active || target.dead) && Phase != BossPhase.Death) {
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
            if (windup > 0)
                windup--;

            switch (Phase) {
                case BossPhase.Intro: AI_Intro(target); break;
                case BossPhase.Reposition: AI_Reposition(target); break;

                case BossPhase.P1_GazeTriple: AI_P1_GazeTriple(target); break;
                case BossPhase.P1_ArcVolley: AI_P1_ArcVolley(target); break;
                case BossPhase.P1_StarRing: AI_P1_StarRing(target); break;
                case BossPhase.P1_MeteorGraze: AI_P1_MeteorGraze(target); break;

                case BossPhase.Transition2: AI_Transition2(target); break;

                case BossPhase.P2_FlashstepBarrage: AI_P2_FlashstepBarrage(target); break;
                case BossPhase.P2_TwinCage: AI_P2_TwinCage(target); break;
                case BossPhase.P2_SentryEyes: AI_P2_SentryEyes(target); break;
                case BossPhase.P2_StarfallColumns: AI_P2_StarfallColumns(target); break;
                case BossPhase.P2_SniperDuel: AI_P2_SniperDuel(target); break;

                case BossPhase.Transition3: AI_Transition3(target); break;

                case BossPhase.P3_MyriadEyes: AI_P3_MyriadEyes(target); break;
                case BossPhase.P3_Collapse: AI_P3_Collapse(target); break;
                case BossPhase.P3_GazeSweep: AI_P3_GazeSweep(target); break;
                case BossPhase.P3_FinalJudgment: AI_P3_FinalJudgment(target); break;

                case BossPhase.Death: AI_Death(target); break;
            }

            if (Phase != BossPhase.Death && Phase != BossPhase.Intro)
                Leash(target);

            UpdateVisuals(target);
        }

        private void CheckPhaseTransition() {
            if (Phase is BossPhase.Intro or BossPhase.Transition2 or BossPhase.Transition3 or BossPhase.Death)
                return;
            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Transition2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase2Transition = true;
                didPhase3Transition = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Transition3);
            }
        }

        #endregion

        #region 入场演出 (深空浮现 → 静止凝视 → 教学第一箭)

        private void AI_Intro(Player target) {
            NPC.dontTakeDamage = PhaseTimer < 110;

            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -1150);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;
                eyeOpen = 0f;
                eyeOpenTarget = 0f;
                drawScale = 0.22f;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.4f, Volume = 0.8f }, target.Center);
            }

            //0~40f: 远景微星
            if (PhaseTimer <= 40) {
                NPC.Opacity = PhaseTimer / 40f * 0.35f;
            }

            //40f: 天目睁开
            if (PhaseTimer == 40) {
                eyeOpenTarget = 1f;
                eyeOpenVel = 0.25f;
                TriggerBloom(0.14f, ArgusPurple);
                ShakeLocal(5f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, NPC.Center);
            }

            //40~110f: 从深空冲向镜头 (fake-Z 立方曲线)
            if (PhaseTimer > 40 && PhaseTimer <= 110) {
                float p = (PhaseTimer - 40) / 70f;
                float eased = 1f - MathF.Pow(1f - p, 3f);
                drawScale = MathHelper.Lerp(0.22f, 1f, eased);
                NPC.Opacity = MathHelper.Lerp(0.35f, 1f, eased);
                Vector2 from = target.Center + new Vector2(0, -1150);
                Vector2 to = target.Center + new Vector2(0, -460);
                NPC.Center = Vector2.Lerp(from, to, eased);

                //入场星尘螺旋收拢
                if (!Main.dedServ && PhaseTimer % 2 == 0) {
                    for (int arm = 0; arm < 2; arm++) {
                        float ang = globalTime * 6f + arm * MathHelper.Pi;
                        float dist = MathHelper.Lerp(320f, 50f, eased);
                        Vector2 dustPos = NPC.Center + ang.ToRotationVector2() * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0,
                            arm == 0 ? DustID.PurpleTorch : DustID.BlueTorch, 0, 0, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }
            }

            //110~170f: 静止凝视 (威压 = 静止, 只有瞳孔追着玩家)
            if (PhaseTimer > 110 && PhaseTimer <= 170) {
                drawScale = 1f;
                NPC.Opacity = 1f;
                NPC.velocity *= 0.85f;
            }

            //170~250f: 教学第一箭 — 完整凝视语法首秀 (慢速大预告)
            if (PhaseTimer > 170) {
                float t = PhaseTimer - 170;
                if (GazeStep(target, t, 40, 10, 6, 40f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * 20f, 0.22f);
                    FireBeat(0.5f, flashLine: true);
                }
            }

            //252f: 题名节拍, 入战
            if (PhaseTimer >= 252) {
                TriggerBloom(0.24f, ArgusPurple);
                ShakeLocal(12f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.15f, Volume = 1.2f }, NPC.Center);
                windup = 40;
                TransitionTo(BossPhase.Reposition);
            }
        }

        #endregion

        #region 连接拍: 换位

        private void AI_Reposition(Player target) {
            int duration = IsPhase3 ? 36 : IsPhase2 ? 44 : 52;

            //紧急阀门: 被贴身时预告后坐冲刺拉开 (快速"位移"而非瞬移贴图)
            if (SubState == 0 && NPC.Distance(target.Center) < 260f && PhaseTimer > 6) {
                SubState = 1;
                AttackTimer = 0;
                NPC.netUpdate = true;
            }
            if (SubState == 1) {
                if (AttackTimer <= 8) {
                    //预闪 8f (可读的"要退了")
                    NPC.velocity *= 0.8f;
                    if (!Main.dedServ && AttackTimer % 2 == 0) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(30, 30),
                            0, 0, DustID.PurpleTorch, 0, 0, 80, default, 1.8f);
                        d.noGravity = true;
                    }
                }
                else if (AttackTimer == 9) {
                    NPC.velocity = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 36f;
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = 0.4f }, NPC.Center);
                }
                else {
                    NPC.velocity *= 0.9f;
                    if (AttackTimer > 26) {
                        SubState = 0;
                        NPC.netUpdate = true;
                    }
                }
                return;
            }

            //悬停位: 保持在玩家上方一侧
            float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
            Vector2 anchor = target.Center + new Vector2(side * PreferredDistance * 0.85f, -PreferredDistance * 0.6f);
            HoverTo(anchor, 16f, 0.09f);

            //P2/P3: 换位中一发骚扰箭 (带迷你锁定语法, 不是无预警冷枪)
            if (IsPhase2 && PhaseTimer >= duration / 2 - 14) {
                float t = PhaseTimer - (duration / 2 - 14);
                if (GazeStep(target, t, 10, 4, 2, 44f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * WindupSpeed(22f), 0.2f);
                    FireBeat(0.1f);
                }
            }

            if (PhaseTimer >= duration)
                NextAttack();
        }

        #endregion

        #region P1: 审视

        /// <summary>三连锁定 — 凝视语法的教学招。每轮: 追踪30f→锁定8f→熄灭4f→单箭+双翼副箭。</summary>
        private void AI_P1_GazeTriple(Player target) {
            //慢速横移 (启动期动得少的公平阀门)
            Vector2 perp = NPC.SafeDirectionTo(target.Center).RotatedBy(MathHelper.PiOver2);
            NPC.velocity = Vector2.Lerp(NPC.velocity, perp * MathF.Sin(globalTime * 1.7f) * 5f, 0.05f);

            for (int r = 0; r < 3; r++) {
                float t = PhaseTimer - (14 + r * 68);
                if (t < 0 || t > 42)
                    continue;
                if (GazeStep(target, t, 30, 8, 4, 46f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * WindupSpeed(23f), 0.25f);
                    for (int s = -1; s <= 1; s += 2)
                        SpawnArrow(NPC.Center, dir.RotatedBy(s * MathHelper.ToRadians(6f)) * WindupSpeed(20f), 0.2f);
                    FireBeat(0.25f);
                }
            }

            if (PhaseTimer > 14 + 3 * 68 + 10)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>弧射箭幕 — 9 槽扇形齐射留 12° 缺口, 两波缺口镜像。</summary>
        private void AI_P1_ArcVolley(Player target) {
            NPC.velocity *= 0.93f;

            for (int w = 0; w < 2; w++) {
                float t = PhaseTimer - (10 + w * 78);
                if (t < 0 || t > 42)
                    continue;

                //锁定期间显示扇形虚影 (形状预告; 从锁定裁定帧的下一帧起, 保证读到冻结后的 gazeAim)
                if (t > 30 && t < 42 && !Main.dedServ) {
                    Vector2 dirL = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitX);
                    for (int i = -4; i <= 4; i++) {
                        if (IsVolleyGapSlot(i, w))
                            continue;
                        Vector2 d = dirL.RotatedBy(i * MathHelper.ToRadians(6f));
                        QueueSight(NPC.Center, NPC.Center + d * 560f, 1.2f,
                            Color.Lerp(ArgusPurple, TelegraphColors.Lethal, (t - 30) / 12f), 0.35f);
                    }
                }

                if (GazeStep(target, t, 30, 8, 4, 34f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = -4; i <= 4; i++) {
                        if (IsVolleyGapSlot(i, w))
                            continue;
                        SpawnArrow(NPC.Center, dir.RotatedBy(i * MathHelper.ToRadians(6f)) * WindupSpeed(17f), 0.22f);
                    }
                    FireBeat(0.45f);
                }
            }

            if (PhaseTimer > 10 + 2 * 78 + 20)
                TransitionTo(BossPhase.Reposition);
        }

        //弧射缺口槽位: 第一波缺 +1/+2, 第二波镜像缺 -1/-2 (缺口离中心一档, 引导小位移)
        private static bool IsVolleyGapSlot(int slot, int wave)
            => wave == 0 ? slot is 1 or 2 : slot is -1 or -2;

        /// <summary>星阵环 — 12 球环 (双对称缺口) 收缩, Boss 环外两发锁定单箭。</summary>
        private void AI_P1_StarRing(Player target) {
            HoverTo(target.Center + new Vector2(0, -PreferredDistance), 12f, 0.07f);

            if (PhaseTimer == 8) {
                SpawnOrbRing(target.Center, 12, 430f, 0.014f, -0.004f, 40f, 0, 6);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.9f }, target.Center);
            }

            for (int r = 0; r < 2; r++) {
                float t = PhaseTimer - (70 + r * 60);
                if (t < 0 || t > 32)
                    continue;
                if (GazeStep(target, t, 22, 6, 4, 48f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * WindupSpeed(24f), 0.25f);
                    FireBeat(0.2f);
                }
            }

            if (PhaseTimer > 240)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>流星掠射 — 唯一高速位移招: 入位→冲刺线预告36f→pow8后拉→90px/f掠射布悬停箭→硬刹。</summary>
        private void AI_P1_MeteorGraze(Player target) {
            //入位 (0~30f), 提前到位则跳时间 (不等自己的时钟)
            if (PhaseTimer <= 30) {
                float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
                Vector2 slot = target.Center + new Vector2(side * 720f, -170f);
                HoverTo(slot, 26f, 0.12f);
                if (PhaseTimer > 10 && NPC.Distance(slot) < 70f)
                    PhaseTimer = 30;
                return;
            }

            //30f: 锁定冲刺线 (轻预判)
            if (PhaseTimer == 31) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    gazeAim = target.Center + target.velocity * 8f;
                    NPC.netUpdate = true;
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
            }

            Vector2 dashDir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitX);

            //31~66f: 冲刺线常亮 (36f 电报) + 末段 pow8 后拉蓄势
            if (PhaseTimer > 31 && PhaseTimer < 67) {
                float p = (PhaseTimer - 31) / 36f;
                if (!Main.dedServ)
                    QueueSight(NPC.Center - dashDir * 200f, NPC.Center + dashDir * 1700f,
                        2f + p * 3f, TelegraphColors.Lethal, 0.35f + p * 0.5f);
                //后拉: 8次幂晚发 — 静止…静止…猛然吸气
                NPC.velocity = -dashDir * MathF.Pow(p, 8f) * 26f;
                bowDrawTarget = MathF.Max(bowDrawTarget, p);
                eyeNova = MathF.Max(eyeNova, p);
                return;
            }

            //67f: 发射
            if (PhaseTimer == 67) {
                NPC.velocity = dashDir * 88f;
                stringSnap = 1f;
                ShakeLocal(7f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
            }

            //掠射途中沿路布 5 颗悬停箭 (各自生成瞬间锁定当时的玩家方位)
            if (PhaseTimer > 67 && PhaseTimer <= 77 && PhaseTimer % 2 == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                int idx = (int)(PhaseTimer - 67) / 2;
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                SpawnArrow(NPC.Center, dir * 0.011f, 0.2f, 1f, 26f + idx * 7f, 19f);
            }

            //78f+: 硬刹 (×0.62 = 撞进位置的读感)
            if (PhaseTimer >= 78) {
                NPC.velocity *= 0.62f;
                if (PhaseTimer > 130)
                    TransitionTo(BossPhase.Reposition);
            }
        }

        #endregion

        #region 转阶段演出

        /// <summary>转阶段2: 清弹 → 定格碎裂 → 眨眼闭合 → 骤睁(红化) + 展开星环。全程无敌零攻击 (呼吸拍)。</summary>
        private void AI_Transition2(Player target) {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.92f;

            //0~40f: 定格, 光环眼高频闪烁 (不稳定感)
            if (PhaseTimer <= 40) {
                eyeOpenTarget = PhaseTimer % 8 < 4 ? 1f : 0.55f;
            }
            //40f: 碎裂闪 (Sparkle 迸射)
            if (PhaseTimer == 41) {
                TriggerBloom(0.18f, Color.White);
                ShakeLocal(6f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.4f, Volume = 1.1f }, NPC.Center);
                    for (int i = 0; i < 20; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.PurpleTorch, 0, 0, 60, default, 2.2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(9, 9);
                    }
                }
            }
            //41~70f: 眨眼闭合
            if (PhaseTimer > 41 && PhaseTimer <= 70)
                eyeOpenTarget = 0f;
            //70~100f: 闭眼静默
            if (PhaseTimer > 70 && PhaseTimer <= 100)
                eyeOpenTarget = 0f;
            //100f: 骤睁 + 爆发
            if (PhaseTimer == 100) {
                eyeOpenTarget = 1f;
                eyeOpenVel = 0.35f;
                TriggerBloom(0.26f, ArgusPurple);
                ShakeLocal(10f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                //自 Boss 展开的慢速压力星环 (2 缺口, 可读退场)
                SpawnOrbRing(NPC.Center, 12, 180f, 0.012f, 0.004f, 30f, 0, 6, 0f, 0.18f);
            }

            if (PhaseTimer >= 150) {
                NPC.dontTakeDamage = false;
                NPC.defense += 12;
                NPC.damage = (int)(NPC.damage * 1.15f);
                windup = 60;
                cycleIndex = 0; //新阶段循环从头编排 (大招押尾)
                TransitionTo(BossPhase.Reposition);
            }
        }

        /// <summary>转阶段3: 升空 → 天幕瞳孔拉满锁定 (DomainSignal) → 边缘眼预热 → 竖瞳化。</summary>
        private void AI_Transition3(Player target) {
            NPC.dontTakeDamage = true;

            //升至高处中央
            HoverTo(target.Center + new Vector2(0, -430f), 14f, 0.08f);

            //130f: 竖瞳觉醒闪
            if (PhaseTimer == 130) {
                TriggerBloom(0.3f, ArgusPurple);
                ShakeLocal(11f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.5f }, NPC.Center);
            }

            //汇聚星尘 (密度随进度)
            if (!Main.dedServ && PhaseTimer > 30 && PhaseTimer < 130 && PhaseTimer % 3 == 0) {
                float p = (PhaseTimer - 30) / 100f;
                for (int i = 0; i < 3; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(200f, 500f);
                    Vector2 dustPos = NPC.Center + ang.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0,
                        Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch, 0, 0, 60, default, 2f + p);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + p * 8f);
                }
            }

            if (PhaseTimer >= 190) {
                NPC.dontTakeDamage = false;
                NPC.defense += 18;
                NPC.damage = (int)(NPC.damage * 1.15f);
                windup = 60;
                cycleIndex = 0; //新阶段循环从头编排 (最终审判押尾)
                TransitionTo(BossPhase.Reposition);
            }
        }

        #endregion

        #region P2: 追猎

        //瞬移步内偏角序列 (交替两侧, 决定论无随机)
        private static readonly float[] FlashstepOffsets = { -0.55f, 0.7f, -0.9f, 0.6f };

        /// <summary>瞬移连射 — 4 步: 旧位聚缩预闪10f → 闪现至玩家远侧定角位 → 显形8f → 锁定 → 窄扇箭。</summary>
        private void AI_P2_FlashstepBarrage(Player target) {

            if (flashStep >= 4) {
                //收招
                NPC.velocity *= 0.93f;
                NPC.Opacity = 1f;
                if (AttackTimer > 24)
                    TransitionTo(BossPhase.Reposition);
                return;
            }

            float t = AttackTimer;

            //0~10f: 旧位聚缩预闪
            if (t <= 10) {
                NPC.velocity *= 0.85f;
                NPC.Opacity = 1f - t / 10f * 0.5f;
                if (!Main.dedServ && (int)t % 2 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(60, 60);
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.PurpleTorch, 0, 0, 90, default, 1.6f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }
                if (t == 10 && !Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.85f, Pitch = 0.3f }, NPC.Center);
                return;
            }

            //11f: 闪现 (定角位, 距玩家 540 — 不从屏幕外冷枪)
            if (t == 11 && Main.netMode != NetmodeID.MultiplayerClient) {
                float baseAng = (NPC.Center - target.Center).ToRotation() + FlashstepOffsets[flashStep];
                Vector2 slot = target.Center + baseAng.ToRotationVector2() * 540f;
                if (slot.Y > target.Center.Y - 120f)
                    slot.Y = target.Center.Y - 120f;
                NPC.Center = slot;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }

            //11~19f: 显形
            if (t > 10 && t <= 19)
                NPC.Opacity = (t - 10) / 9f;

            //15f 起凝视语法 (track 8 → lock 6 → dark 3 → fire)
            float gt = t - 15;
            bool last = flashStep == 3;
            if (GazeStep(target, gt, 8, 6, 3, 42f)) {
                Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                int n = last ? 2 : 1;
                for (int i = -n; i <= n; i++)
                    SpawnArrow(NPC.Center, dir.RotatedBy(i * MathHelper.ToRadians(last ? 8f : 5f)) * WindupSpeed(21f), 0.22f);
                FireBeat(last ? 0.55f : 0.25f);
            }

            //38f: 下一步
            if (t >= 38) {
                flashStep++;
                AttackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        /// <summary>双环星笼 — 内环扩张 (ω+) 外环收缩 (ω−) 反向旋转, 缝隙周期性对齐; 环外三发锁定箭。</summary>
        private void AI_P2_TwinCage(Player target) {
            HoverTo(target.Center + new Vector2(0, -700f), 12f, 0.07f);

            if (PhaseTimer == 12) {
                SpawnOrbRing(target.Center, 10, 300f, 0.02f, 0.0015f, 40f, 0);
                SpawnOrbRing(target.Center, 14, 560f, -0.014f, -0.0018f, 40f, 0, -1, MathHelper.ToRadians(12f));
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f }, target.Center);
            }

            for (int r = 0; r < 3; r++) {
                float t = PhaseTimer - (72 + r * 70);
                if (t < 0 || t > 36)
                    continue;
                if (GazeStep(target, t, 24, 8, 4, 50f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * WindupSpeed(25f), 0.25f);
                    FireBeat(0.25f);
                }
            }

            if (PhaseTimer > 285)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>百目哨兵 — 三段短闪现沿六边形布 6 枚分体之眼, 各自执行完整凝视语法 (错相波纹)。</summary>
        private void AI_P2_SentryEyes(Player target) {
            NPC.velocity *= 0.97f; //布眼冲刺窗口之外自然滑行减速

            //三次布眼节拍
            for (int drop = 0; drop < 3; drop++) {
                float t = PhaseTimer - (16 + drop * 40);
                if (t == 0 && !Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.5f }, NPC.Center);
                //短闪现 (走位而非硬瞬移: 高速冲向布置位)
                if (t >= 0 && t < 14) {
                    float ang = MathHelper.ToRadians(90f + drop * 120f);
                    Vector2 slot = target.Center + ang.ToRotationVector2() * 560f;
                    if (slot.Y > target.Center.Y - 80f)
                        slot.Y = target.Center.Y - 80f;
                    HoverTo(slot, 42f, 0.25f);
                }
                //布置 2 枚哨兵
                if (t == 14 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int k = 0; k < 2; k++) {
                        int hexIdx = drop * 2 + k;
                        float ang = MathHelper.ToRadians(hexIdx * 60f + 30f);
                        Vector2 pos = target.Center + ang.ToRotationVector2() * 470f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<AetherealWingblades>(), (int)(NPC.damage * 0.22f), 0f, Main.myPlayer,
                            1f, 26f + hexIdx * 14f);
                    }
                }
            }

            //Boss 补一波缺口弧射 (哨兵们锁定期间的复合压力)
            float vt = PhaseTimer - 150;
            if (vt >= 0 && vt <= 42 && GazeStep(target, vt, 26, 8, 4, 32f)) {
                Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -3; i <= 3; i++) {
                    if (i is 1 or -1)
                        continue;
                    SpawnArrow(NPC.Center, dir.RotatedBy(i * MathHelper.ToRadians(7f)) * WindupSpeed(16f), 0.2f);
                }
                FireBeat(0.4f);
            }

            if (PhaseTimer > 255)
                TransitionTo(BossPhase.Reposition);
        }

        //列雨落列顺序 (玩家起始列最后落, 引导持续移动)
        private static readonly int[] StarfallColumnOrder = { -2, 1, -1, 2, 0 };

        /// <summary>星落列雨 — 5 列结构化箭雨: 列顶竖直预告线 30f → 悬停箭列错时直落; 末波双列。</summary>
        private void AI_P2_StarfallColumns(Player target) {
            //列参考系在开招时冻结 (公平: 玩家可离开)
            if (PhaseTimer == 8 && Main.netMode != NetmodeID.MultiplayerClient) {
                gazeAim = target.Center;
                NPC.netUpdate = true;
            }

            //冻结前 gazeAim 是上一招残值 → 先以玩家为锚
            Vector2 colAnchor = PhaseTimer < 9 ? target.Center : gazeAim;
            HoverTo(colAnchor + new Vector2(0, -520f), 10f, 0.06f);

            const float ColWidth = 230f;

            for (int w = 0; w < 5; w++) {
                float t = PhaseTimer - (16 + w * 44);
                if (t < 0 || t > 32)
                    continue;
                float colX = gazeAim.X + StarfallColumnOrder[w] * ColWidth;

                //30f 列预告线
                if (t < 30 && !Main.dedServ) {
                    float p = t / 30f;
                    QueueSight(new Vector2(colX, gazeAim.Y - 760f), new Vector2(colX, gazeAim.Y + 460f),
                        2f + p * 2f, TelegraphColors.Lethal, 0.25f + p * 0.4f);
                }

                //落箭 (悬停模式打包生成, 错时发射)
                if (t == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 6; i++) {
                        float jitter = i * 37 % 110 - 55;
                        Vector2 pos = new(colX + jitter, gazeAim.Y - 700f - i % 3 * 60f);
                        SpawnArrow(pos, Vector2.UnitY * 0.011f, 0.22f, 1f, 4f + i * 4f, WindupSpeed(26f));
                    }
                    bowDrawTarget = 1f;
                    stringSnap = 1f;
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.5f, Volume = 0.7f }, target.Center);
                }
            }

            //末波: 双列齐落 (中列与外列安全)
            float ft = PhaseTimer - 240;
            if (ft >= 0 && ft <= 32) {
                for (int off = -1; off <= 1; off += 2) {
                    float colX = gazeAim.X + off * ColWidth;
                    if (ft < 30 && !Main.dedServ) {
                        float p = ft / 30f;
                        QueueSight(new Vector2(colX, gazeAim.Y - 760f), new Vector2(colX, gazeAim.Y + 460f),
                            2f + p * 2.5f, TelegraphColors.Lethal, 0.3f + p * 0.45f);
                    }
                    if (ft == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 6; i++) {
                            float jitter = i * 41 % 110 - 55;
                            Vector2 pos = new(colX + jitter, gazeAim.Y - 700f - i % 3 * 60f);
                            SpawnArrow(pos, Vector2.UnitY * 0.011f, 0.24f, 1f, 4f + i * 3f, WindupSpeed(27f));
                        }
                    }
                }
                if (ft == 30)
                    ShakeLocal(5f);
            }

            if (PhaseTimer > 295)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>
        /// 狙击对决 — 处决级凝视: 拉极远 → 聚焦暗角 → 游移40f → 锁定50f (音阶加速+满弦)
        /// → 熄灭6f → 贯穿箭 (60px/f 实效) + 白闪 + 大后坐。锁定后完全冻结 — 移动即安全。
        /// </summary>
        private void AI_P2_SniperDuel(Player target) {
            for (int r = 0; r < 2; r++) {
                float t = PhaseTimer - (16 + r * 140);
                if (t < 0 || t > 139)
                    continue;

                //0~22f: 拉极远
                if (t <= 22) {
                    Vector2 dir = (NPC.Center - target.Center).SafeNormalize(-Vector2.UnitY);
                    Vector2 far = target.Center + dir * 950f;
                    if (far.Y > target.Center.Y - 260f)
                        far.Y = target.Center.Y - 260f;
                    HoverTo(far, 30f, 0.14f);
                    gazeFocus = MathF.Max(gazeFocus, t / 22f * 0.8f);
                    continue;
                }

                gazeFocus = MathF.Max(gazeFocus, 0.8f);
                NPC.velocity *= 0.9f;

                //22~62f: 游移 (无威胁色) → 62f 锁定 → 112f 熄灭 → 118f 开火
                float gt = t - 22;
                //锁定期音阶加速 tick (手调延迟数组)
                if (!Main.dedServ && gt >= 40 && gt < 90) {
                    int[] tickAt = { 40, 58, 72, 82, 88 };
                    foreach (int ta in tickAt) {
                        if ((int)gt == ta)
                            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = (gt - 40) / 50f, Volume = 0.6f }, NPC.Center);
                    }
                }
                //蓄力星尘汇聚 (75% 硬切静默)
                if (!Main.dedServ && gt >= 40 && gt < 78 && (int)gt % 2 == 0) {
                    float p = (gt - 40) / 50f;
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(160f, 160f);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.PurpleTorch, 0, 0, 60, default, 1.2f + p);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos) * 0.07f;
                }

                if (GazeStep(target, gt, 40, 50, 6, 60f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * 30f, 0.36f, 2f);
                    FireBeat(1f, flashLine: true);
                    TriggerBloom(0.18f, TelegraphColors.Lethal);
                    gazeFocus = 0f;
                }
            }

            if (PhaseTimer > 16 + 2 * 140 + 12)
                TransitionTo(BossPhase.Reposition);
        }

        #endregion

        #region P3: 天目审判

        /// <summary>万目凝射 — 环形眼阵: 8 眼波纹错相各射一箭; 第二轮 6 眼齐射留 90° 安全楔。</summary>
        private void AI_P3_MyriadEyes(Player target) {
            HoverTo(target.Center + new Vector2(MathF.Sin(globalTime * 0.9f) * 260f, -430f), 10f, 0.06f);

            //第一轮: 8 眼波纹 (错相 12f, 同时活跃预警线 ≤3)
            if (PhaseTimer == 8 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi / 8f * i;
                    Vector2 pos = target.Center + ang.ToRotationVector2() * 680f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AetherealWingblades>(), (int)(NPC.damage * 0.25f), 0f, Main.myPlayer,
                        1f, 20f + i * 12f);
                }
            }

            //第二轮: 6 眼齐锁齐射, 缺 2 相邻方向 = 90° 安全楔 (背向 Boss 一侧)
            if (PhaseTimer == 150 && Main.netMode != NetmodeID.MultiplayerClient) {
                float wedgeBase = (target.Center - NPC.Center).ToRotation(); //安全楔开在 Boss 对侧
                for (int i = 2; i < 8; i++) {
                    float ang = wedgeBase + MathHelper.TwoPi / 8f * i + MathHelper.ToRadians(22.5f);
                    Vector2 pos = target.Center + ang.ToRotationVector2() * 560f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AetherealWingblades>(), (int)(NPC.damage * 0.25f), 0f, Main.myPlayer,
                        1f, 24f);
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f }, target.Center);
            }

            //Boss 收尾一发锁定箭
            float t = PhaseTimer - 250;
            if (t >= 0 && t <= 36 && GazeStep(target, t, 20, 8, 4, 48f)) {
                Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                SpawnArrow(NPC.Center, dir * WindupSpeed(24f), 0.28f);
                FireBeat(0.3f);
            }

            if (PhaseTimer > 320)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>星阵坍缩 — 全视之域签名: 双层反向星阵收缩 + 天幕巨眼锁定 + rift 折射; 中心贯穿双射。</summary>
        private void AI_P3_Collapse(Player target) {
            if (PhaseTimer == 10) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    gazeAim = target.Center; //域中心冻结
                    NPC.netUpdate = true;
                }
                SpawnOrbRing(target.Center, 18, 430f, -0.012f, -0.0035f, 60f, 0, 9, 0f, 0.22f);
                SpawnOrbRing(target.Center, 8, 185f, 0.017f, 0.0022f, 60f, 0, -1, MathHelper.ToRadians(20f), 0.22f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, target.Center);
                ShakeLocal(8f);
                TriggerBloom(0.24f, ArgusPurple);
            }

            //冻结前 gazeAim 是上一招残值 → 先以玩家为锚
            HoverTo((PhaseTimer < 11 ? target.Center : gazeAim) + new Vector2(0, -520f), 11f, 0.07f);

            //中心贯穿锁定射 ×2
            for (int r = 0; r < 2; r++) {
                float t = PhaseTimer - (86 + r * 80);
                if (t < 0 || t > 42)
                    continue;
                if (GazeStep(target, t, 26, 8, 6, 54f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * WindupSpeed(27f), 0.28f);
                    FireBeat(0.4f, flashLine: true);
                }
            }

            //收拍: 双箭 ±6°
            float ft = PhaseTimer - 250;
            if (ft >= 0 && ft <= 26 && GazeStep(target, ft, 14, 6, 4, 52f)) {
                Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int s = -1; s <= 1; s += 2)
                    SpawnArrow(NPC.Center, dir.RotatedBy(s * MathHelper.ToRadians(6f)) * 26f, 0.25f);
                FireBeat(0.45f);
            }

            if (PhaseTimer > 295)
                TransitionTo(BossPhase.Reposition);
        }

        /// <summary>天目扫射 — 剪刀双扫射线 (慢速反向), 中段同停 12f 重定位窗口后反转。角度纯时间函数, 全端一致。</summary>
        private void AI_P3_GazeSweep(Player target) {
            //入位并冻结基准角
            if (PhaseTimer <= 24) {
                HoverTo(target.Center + new Vector2(0, -380f), 16f, 0.1f);
                if (PhaseTimer == 8 && Main.netMode != NetmodeID.MultiplayerClient) {
                    gazeAim = new Vector2((target.Center - NPC.Center).ToRotation(), 0f); //X 分量存基准角
                    NPC.netUpdate = true;
                }
                return;
            }

            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(MathF.Sin(globalTime * 0.8f) * 3f, 0f), 0.05f);

            float a0 = gazeAim.X;
            GetSweepAngles(PhaseTimer, a0, out float angA, out float angB, out bool paused, out bool active);

            if (!active) {
                if (PhaseTimer > 250)
                    TransitionTo(BossPhase.Reposition);
                return;
            }

            //扫射线常亮 (暂停窗口调暗); 线扫到玩家附近时边缘警示
            if (!Main.dedServ) {
                float dim = paused ? 0.3f : 0.8f;
                QueueSight(NPC.Center, NPC.Center + angA.ToRotationVector2() * 1500f, 4f, TelegraphColors.Lethal, dim);
                QueueSight(NPC.Center, NPC.Center + angB.ToRotationVector2() * 1500f, 4f, TelegraphColors.Lethal, dim);
                if (!paused) {
                    ArgusFx.ReportIfLocalPlayerSighted(NPC.Center, angA.ToRotationVector2(), 1500f, 190f, 0.8f);
                    ArgusFx.ReportIfLocalPlayerSighted(NPC.Center, angB.ToRotationVector2(), 1500f, 190f, 0.8f);
                }
            }
            bowDrawTarget = MathF.Max(bowDrawTarget, 0.6f);

            //沿线每 5f 生成箭 (不追身)
            if (!paused && PhaseTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int k = 0; k < 2; k++) {
                    Vector2 dir = (k == 0 ? angA : angB).ToRotationVector2();
                    SpawnArrow(NPC.Center + dir * 70f, dir * WindupSpeed(21f), 0.22f);
                }
            }
        }

        //剪刀双扫角度: 纯时间函数 (t=24 起扫, 114~126 暂停, 126 反向 ×1.15, 216 结束)
        private static void GetSweepAngles(float timer, float a0, out float angA, out float angB, out bool paused, out bool active) {
            const float P1Start = 24f, PauseStart = 114f, P2Start = 126f, End = 216f;
            paused = timer >= PauseStart && timer < P2Start;
            active = timer < End;
            float p1 = MathF.Min(timer, PauseStart) - P1Start;
            float p2 = MathF.Max(timer - P2Start, 0f);
            if (timer > End)
                p2 = End - P2Start;
            angA = a0 - 0.5f + p1 * 0.016f - p2 * 0.0184f;
            angB = a0 + 0.5f - p1 * 0.012f + p2 * 0.0138f;
        }

        /// <summary>
        /// 最终审判 — 全场压轴: 蓄力90f (72%静默切断) → 坍缩拍 → 三重审判
        /// (快锁定+贯穿箭+环阵箭留 90° 旋转安全楔), 第三击 impact frame。蓄力期无敌 (呼吸+观赏)。
        /// </summary>
        private void AI_P3_FinalJudgment(Player target) {
            if (SubState == 0) {
                //蓄力 102f
                NPC.dontTakeDamage = true;
                NPC.velocity *= 0.9f;
                float p = MathF.Min(PhaseTimer / 90f, 1f);
                bowDrawTarget = MathF.Max(bowDrawTarget, MathF.Pow(p, 1.6f));
                eyeNova = MathF.Max(eyeNova, p * 0.8f);
                ShakeLocal(p * p * 4f);

                //汇聚星尘 ∝ sqrt(p), 72% 硬切静默 (尖叫前的吸气)
                if (!Main.dedServ && p < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(p) * 0.8f) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(180f, 560f);
                    Vector2 dustPos = NPC.Center + ang.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0,
                        Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch, 0, 0, 40, default, 2.4f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos) * 0.05f;
                }

                if (PhaseTimer >= 102) {
                    SubState = 1;
                    AttackTimer = 0;
                    flashStep = 0;
                    NPC.dontTakeDamage = false;
                    NPC.netUpdate = true;
                }
                return;
            }

            if (SubState == 1) {
                //三重审判, 每重 66f: track14 → lock8 → dark4 → fire
                if (flashStep >= 3) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    return;
                }

                NPC.velocity *= 0.92f;
                float t = AttackTimer;

                if (GazeStep(target, t, 14, 8, 4, 62f)) {
                    Vector2 dir = (gazeAim - NPC.Center).SafeNormalize(Vector2.UnitY);
                    SpawnArrow(NPC.Center, dir * 31f, 0.36f, 2f);

                    //环阵: 12 悬停箭围 gazeAim 向心, 留 3 相邻空位 (90° 安全楔逐击旋转)
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int skipStart = flashStep * 4;
                        for (int i = 0; i < 12; i++) {
                            int rel = (i - skipStart + 12) % 12;
                            if (rel < 3)
                                continue;
                            float ang = MathHelper.TwoPi / 12f * i;
                            Vector2 pos = gazeAim + ang.ToRotationVector2() * 560f;
                            Vector2 inward = (gazeAim - pos).SafeNormalize(Vector2.UnitY);
                            SpawnArrow(pos, inward * 0.011f, 0.26f, 1f, 12f, 14f);
                        }
                    }

                    bool lastJudgment = flashStep == 2;
                    FireBeat(1f, flashLine: true);
                    if (lastJudgment) {
                        //全场唯一 impact frame
                        impactFrame = 1f;
                        impactHold = 10;
                        ShakeLocal(13f);
                    }
                    else {
                        TriggerBloom(0.2f, TelegraphColors.Lethal);
                        ShakeLocal(9f);
                    }
                }

                if (t >= 66) {
                    flashStep++;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
                return;
            }

            //收招 50f: 弦震荡, 漂移退场
            NPC.velocity = Vector2.Lerp(NPC.velocity, (NPC.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 4f, 0.04f);
            if (AttackTimer > 50)
                TransitionTo(BossPhase.Reposition);
        }

        #endregion

        #region 死亡演出 (灯将熄 → 溶解 → 坍缩 → 星爆)

        private void AI_Death(Player target) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            NPC.velocity *= 0.92f;
            SkyDeathClose = MathF.Min(PhaseTimer / 140f, 1f);

            //0~30f: 弓垂落
            if (PhaseTimer <= 30)
                bowDrawTarget = 0f;

            //30~140f: 灯将熄闪烁 + 溶解剥落 (闪烁节拍音随之下行)
            if (PhaseTimer > 30 && PhaseTimer <= 140) {
                float p = (PhaseTimer - 30) / 110f;
                deathDissolve = p * 0.7f;
                //闪烁频率渐升、占空比渐降
                float period = MathHelper.Lerp(20f, 6f, p);
                bool lit = PhaseTimer % period < period * (1f - p * 0.6f);
                if (lit && eyeOpenTarget < 0.5f && !Main.dedServ)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.3f - p * 0.9f, Volume = 0.45f }, NPC.Center);
                eyeOpenTarget = lit ? 0.8f : 0.15f;
                ShakeLocal(1.5f);

                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch, 0, -1.5f, 120, default, 1.4f);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 3f));
                }
            }

            //140~180f: 反向收缩 (爆发前先变小); 172f 起 8f 完全静默 — 星爆前的吸气
            if (PhaseTimer > 140 && PhaseTimer <= 180) {
                float p = (PhaseTimer - 140) / 40f;
                drawScale = MathHelper.Lerp(1f, 0.25f, ACMUtils.QuadIn(p));
                deathDissolve = MathHelper.Lerp(0.7f, 0.95f, p);
                eyeOpenTarget = 0.9f;
                if (PhaseTimer > 172)
                    return; //静默拍: 不再产尘不再震
                if (!Main.dedServ && (int)PhaseTimer % 2 == 0) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(140f, 140f);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.PurpleTorch, 0, 0, 40, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos) * 0.12f;
                }
            }

            //180f: 星爆白闪
            if (PhaseTimer == 181) {
                deathDissolve = 1f;
                drawScale = 0.01f;
                eyeOpenTarget = 0f;
                TriggerBloom(0.36f, Color.White);
                ShakeLocal(16f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.2f }, NPC.Center);
                    for (int i = 0; i < 40; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0,
                            i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch, 0, 0, 60, default, 2.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(14f, 14f);
                    }
                }
            }

            //180~266f: 悬念留白 (星尘漂散, 天幕闭合)
            if (PhaseTimer > 181 && !Main.dedServ && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(120f, 120f),
                    0, 0, DustID.BlueTorch, 0, 0, 150, default, 1f);
                d.noGravity = true;
                d.velocity = new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f));
            }

            //266f: 真死 (CheckDead 放行, OnKill 掉落照常)
            if (PhaseTimer >= 266 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
            }
        }

        #endregion

        #region 视觉推进 (各端确定性)

        private void UpdateVisuals(Player target) {
            if (Phase != BossPhase.Intro && Phase != BossPhase.Death) {
                NPC.Opacity = Phase == BossPhase.P2_FlashstepBarrage ? NPC.Opacity : 1f;
                drawScale = MathHelper.Lerp(drawScale, 1f, 0.1f);
            }

            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.012f;

            //光弓朝向: 锁定期盯 gazeAim, 平时追玩家
            bool aimLocked = eyeNova > 0.35f && gazeAim != Vector2.Zero;
            Vector2 aimPoint = aimLocked ? gazeAim : target.Center;
            float wantAngle = (aimPoint - NPC.Center).ToRotation();
            bowAngle = bowAngle.AngleLerp(wantAngle, 0.14f);

            //拉弦弹簧 (释放后的弦震荡由 stringSnap 驱动)
            bowDraw = ACMUtils.SpringDamp(bowDraw, bowDrawTarget, ref bowDrawVel, 90f, 14f, 1f / 60f);
            bowDraw = MathHelper.Clamp(bowDraw, 0f, 1.05f);
            if (stringSnap > 0.01f)
                stringSnap *= 0.88f;
            else
                stringSnap = 0f;

            //后坐视觉偏移: 与实际速度冲量互补的沿弓反向位移
            recoilVisual *= 0.86f;
            if (stringSnap > 0.5f)
                recoilVisual = -bowAngle.ToRotationVector2() * stringSnap * 14f;

            //光环巨眼
            eyeOpen = ACMUtils.SpringDamp(eyeOpen, eyeOpenTarget, ref eyeOpenVel, 60f, 11f, 1f / 60f);
            eyeOpen = MathHelper.Clamp(eyeOpen, 0f, 1.1f);
            if (Phase != BossPhase.Transition2 && Phase != BossPhase.Death && Phase != BossPhase.Intro)
                eyeOpenTarget = 1f;
            eyeSlit = MathHelper.Lerp(eyeSlit,
                IsPhase3 || Phase == BossPhase.Transition3 && PhaseTimer > 130 ? 1f : 0f, 0.03f);
            eyeNova *= 0.9f;

            glowIntensity = (IsPhase3 ? 1.5f : IsPhase2 ? 1.25f : 1f) + MathF.Sin(globalTime * 4f) * 0.12f;
            Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.3f, 0.9f) * glowIntensity * eyeOpen);

            //衰减类
            gazeBeamFlash *= 0.86f;
            bloomBurst *= 0.90f;
            if (impactHold > 0)
                impactHold--;
            else
                impactFrame *= 0.78f;

            //全视之域签名进度 (坍缩 + 转阶段3 峰值)
            float domainTarget =
                Phase == BossPhase.P3_Collapse ? 1f :
                Phase == BossPhase.Transition3 && PhaseTimer is > 50 and < 160 ? 0.8f : 0f;
            domainPower = MathHelper.Lerp(domainPower, domainTarget, domainTarget > domainPower ? 0.06f : 0.1f);
            DomainSignal = domainPower;

            //"被注视"氛围染色
            float voidTarget = (IsPhase3 ? 0.5f : IsPhase2 ? 0.22f : 0f) + domainPower * 0.5f;
            if (Phase == BossPhase.Death)
                voidTarget = 0.25f * (1f - SkyDeathClose);
            ambientVoid = MathHelper.Lerp(ambientVoid, MathHelper.Clamp(voidTarget, 0f, 1f), 0.05f);

            //聚焦暗角 (狙击对决内由招式抬升, 此处只回落)
            gazeFocus = MathHelper.Lerp(gazeFocus, 0f, 0.06f);

            //天幕联动信号
            SkyBlink = MathHelper.Clamp(1f - eyeOpen, 0f, 1f);
            if (Phase != BossPhase.Death)
                SkyDeathClose = MathHelper.Lerp(SkyDeathClose, 0f, 0.1f);

            //发布屏幕氛围 (域中心 = 坍缩阵心 / 平时玩家)
            Vector2 domainCenter = Phase == BossPhase.P3_Collapse && gazeAim != Vector2.Zero ? gazeAim : target.Center;
            ArgusScreenSystem.Publish(ambientVoid, domainPower, gazeFocus, domainCenter, (float)Main.GlobalTimeWrappedHourly);
        }

        #endregion

        #region 绘制

        //凝视线队列 (AI 逐帧写入, PreDraw 单批冲刷)
        private struct SightLine
        {
            public Vector2 From, To;
            public float HalfWidth;
            public Color Color;
            public float Intensity;
        }

        private readonly List<SightLine> sightQueue = new(8);

        private void QueueSight(Vector2 from, Vector2 to, float halfWidth, Color color, float intensity) {
            if (Main.dedServ || sightQueue.Count >= 12)
                return;
            sightQueue.Add(new SightLine { From = from, To = to, HalfWidth = halfWidth, Color = color, Intensity = intensity });
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //1) 凝视线/预告线 (单批, 于本体之下)
            FlushSightBeams(spriteBatch);

            //2) 光环巨眼 (本体之后的身份视觉)
            DrawEyeHalo(spriteBatch);

            //3) 残影 + 本体 (死亡期走溶解绘制)
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 bodyPos = NPC.Center + recoilVisual - screenPos;

            //速度门控残影 (只在快时出现 — 快的时刻自动放大)
            float speedGate = MathHelper.Clamp((NPC.velocity.Length() - 8f) / 30f, 0f, 1f);
            if (speedGate > 0.05f && Phase != BossPhase.Death) {
                int trailLen = NPCID.Sets.TrailCacheLength[Type];
                for (int i = trailLen - 1; i > 0; i--) {
                    if (NPC.oldPos[i] == Vector2.Zero)
                        continue;
                    Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    float t = (float)i / trailLen;
                    Color trailColor = Color.Lerp(ArgusPurple, ArgusBlue, t) with { A = 0 }
                        * (0.55f * (1f - t) * speedGate * NPC.Opacity);
                    spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin,
                        NPC.scale * drawScale * (1f - t * 0.05f), effects, 0f);
                }
            }

            if (Phase == BossPhase.Death && deathDissolve > 0.01f) {
                DrawDissolvingBody(spriteBatch, texture, frame, origin, effects, bodyPos);
            }
            else {
                spriteBatch.Draw(texture, bodyPos, frame, drawColor * NPC.Opacity, NPC.rotation, origin,
                    NPC.scale * drawScale, effects, 0f);
                //独眼发光叠加
                float eyePulse = MathF.Sin(globalTime * 4f) * 0.2f + 0.5f;
                Color eyeGlow = Color.Lerp(new Color(180, 80, 255), TelegraphColors.Lethal, eyeNova)
                    with { A = 0 } * (eyePulse * glowIntensity * 0.4f * NPC.Opacity);
                spriteBatch.Draw(texture, bodyPos, frame, eyeGlow, NPC.rotation, origin,
                    NPC.scale * drawScale * 1.03f, effects, 0f);
            }

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            //光弓 (本体之上): 弓身曲带 + 弦 + 搭箭
            DrawBow(spriteBatch, screenPos);

            //处决白闪线 (一次性单独绘制, 不入常驻队列避免高刷新率下重复累积)
            if (gazeBeamFlash > 0.03f)
                ACMShaders.DrawBeam(gazeFlashFrom, gazeFlashTo, 9f * gazeBeamFlash,
                    Color.White, new Color(200, 180, 255), gazeBeamFlash,
                    flowSpeed: 3f, coreSharp: 3f, coreGlow: gazeBeamFlash);

            //全屏名额互斥: impact frame > 径向泛光 > 域折射
            if (impactFrame > 0.05f) {
                DrawImpactFrame(spriteBatch);
            }
            else if (bloomBurst > 0.02f) {
                ACMShaders.DrawRadialBloomAt(NPC.Center, bloomBurstRadius, bloomBurst, bloomBurstColor,
                    rayCount: 12f, falloff: 2.6f);
            }
            else if (domainPower > 0.05f) {
                DrawDomainRift(spriteBatch);
            }
        }

        /// <summary>单批冲刷全部凝视线 (BeamGrad, uniform 白色 + 顶点色调色 → 一次 Apply 画 N 条)。</summary>
        private void FlushSightBeams(SpriteBatch sb) {
            if (sightQueue.Count == 0 || Main.dedServ)
                return;
            Effect fx = ACMShaders.BeamGrad;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uColorCore"]?.SetValue(Vector4.One);
            fx.Parameters["uColorEdge"]?.SetValue(new Vector4(1f, 1f, 1f, 0.3f));
            fx.Parameters["uCoreGlow"]?.SetValue(0.55f);
            fx.Parameters["uFlowSpeed"]?.SetValue(2.2f);
            fx.Parameters["uFlowScale"]?.SetValue(2f);
            fx.Parameters["uCoreSharp"]?.SetValue(2.6f);
            fx.Parameters["uUseTexture"]?.SetValue(0f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            foreach (SightLine line in sightQueue) {
                Vector2 a = line.From - Main.screenPosition;
                Vector2 b = line.To - Main.screenPosition;
                if ((b - a).LengthSquared() < 4f)
                    continue;
                Color c = line.Color * line.Intensity;
                var verts = ACMUtils.BuildRibbonStrip([a, b], _ => line.HalfWidth, _ => c, 0f, 1);
                if (verts.Length >= 4)
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            //队列不在此清空 (由每个 AI tick 开头清空) — 高刷新率下多次 Draw 不闪烁
        }

        /// <summary>光环巨眼 + 转阶段3 边缘预热眼 (ArgusEyeIris 单批)。</summary>
        private void DrawEyeHalo(SpriteBatch sb) {
            if (Main.dedServ || eyeOpen < 0.02f && Phase != BossPhase.Transition3)
                return;
            Effect fx = EyeIrisEffect;
            Texture2D glow = ACMAsset.SoftGlow;
            if (fx == null || glow == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = glow.Size() / 2f;

            //主光环眼 (随凝视朝向, 锁定充能变红)
            if (eyeOpen > 0.02f) {
                fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(eyeOpen, 0f, 1f));
                fx.Parameters["uSlit"]?.SetValue(eyeSlit);
                fx.Parameters["uPupilShift"]?.SetValue(0.08f);
                fx.Parameters["uNova"]?.SetValue(eyeNova);
                fx.CurrentTechnique.Passes[0].Apply();

                Color tint = Color.Lerp(ArgusPurple, TelegraphColors.Lethal, eyeNova * 0.75f);
                tint.A = (byte)(255 * NPC.Opacity);
                float scale = 300f / glow.Width * drawScale * (0.95f + MathF.Sin(globalTime * 2.3f) * 0.05f);
                sb.Draw(glow, NPC.Center + recoilVisual - Main.screenPosition, null, tint,
                    bowAngle, origin, scale, SpriteEffects.None, 0f);
            }

            //转阶段3: 屏幕边缘 6 眼预热 (0→1→0 睁闭脉冲)
            if (Phase == BossPhase.Transition3 && PhaseTimer is > 60 and < 160) {
                float lt = (PhaseTimer - 60) / 100f;
                float bump = MathF.Sin(lt * MathHelper.Pi);
                fx.Parameters["uSlit"]?.SetValue(1f);
                fx.Parameters["uPupilShift"]?.SetValue(0.06f);
                fx.Parameters["uNova"]?.SetValue(0.3f);
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi / 6f * i + globalTime * 0.1f;
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * 520f;
                    fx.Parameters["uOpen"]?.SetValue(bump * (0.5f + 0.5f * MathF.Sin(lt * 8f + i * 1.7f)));
                    fx.CurrentTechnique.Passes[0].Apply();
                    Color tint = ArgusPurple with { A = 200 };
                    sb.Draw(glow, pos - Main.screenPosition, null, tint,
                        (NPC.Center - pos).ToRotation() + MathHelper.Pi, origin, 120f / glow.Width, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>光弓: BeamGrad 弓身曲带 + 弦二段 + 搭箭流光。全部几何由同步计时器推导。</summary>
        private void DrawBow(SpriteBatch sb, Vector2 screenPos) {
            if (bowDraw < 0.03f && stringSnap < 0.03f || Phase == BossPhase.Death && PhaseTimer > 140)
                return;
            Effect fx = ACMShaders.BeamGrad;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            Vector2 aimDir = bowAngle.ToRotationVector2();
            Vector2 bowCenter = NPC.Center + recoilVisual + aimDir * 30f;
            float visIntensity = MathHelper.Clamp(0.25f + bowDraw * 0.75f, 0f, 1f) * NPC.Opacity;

            //弓身弧 (9 点曲带)
            const int ArcPts = 9;
            Vector2[] arc = new Vector2[ArcPts];
            for (int i = 0; i < ArcPts; i++) {
                float s = (float)i / (ArcPts - 1) * 2f - 1f;
                //拉弦时弓身微弯 (张力可视)
                float bend = 1.02f + bowDraw * 0.14f;
                arc[i] = bowCenter + aimDir.RotatedBy(s * bend) * 88f - screenPos;
            }

            //弦: 弓梢 → 搭箭点 (拉弦后拉 + 释放震荡)
            float snapOsc = stringSnap > 0.01f
                ? MathF.Sin((1f - stringSnap) * 26f) * stringSnap * 18f : 0f;
            Vector2 nock = bowCenter + aimDir * (MathHelper.Lerp(46f, -34f, bowDraw) + snapOsc) - screenPos;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uColorCore"]?.SetValue(Vector4.One);
            fx.Parameters["uColorEdge"]?.SetValue(new Vector4(1f, 1f, 1f, 0.35f));
            fx.Parameters["uCoreGlow"]?.SetValue(0.5f + eyeNova * 0.6f);
            fx.Parameters["uFlowSpeed"]?.SetValue(1.6f);
            fx.Parameters["uFlowScale"]?.SetValue(1.6f);
            fx.Parameters["uCoreSharp"]?.SetValue(2.4f);
            fx.Parameters["uUseTexture"]?.SetValue(0f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            Color bowColor = Color.Lerp(ArgusPurple, TelegraphColors.Lethal, eyeNova * 0.6f) * visIntensity;
            var bowVerts = ACMUtils.BuildRibbonStrip(arc,
                t => 3.2f * (0.35f + 0.65f * MathF.Sin(t * MathHelper.Pi)), _ => bowColor, 0f, 2);
            if (bowVerts.Length >= 4)
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bowVerts, 0, bowVerts.Length - 2);

            Color stringColor = new Color(220, 210, 255) * (visIntensity * 0.8f);
            for (int k = 0; k < 2; k++) {
                Vector2 tip = k == 0 ? arc[0] : arc[ArcPts - 1];
                var sv = ACMUtils.BuildRibbonStrip([tip, nock], _ => 1.1f, _ => stringColor, 0f, 1);
                if (sv.Length >= 4)
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, sv, 0, sv.Length - 2);
            }

            //搭箭流光 (拉弦时显形)
            if (bowDraw > 0.15f) {
                Color arrowColor = Color.Lerp(Color.White, TelegraphColors.Lethal, eyeNova * 0.5f) * (visIntensity * bowDraw);
                var av = ACMUtils.BuildRibbonStrip([nock, nock + aimDir * (66f + bowDraw * 34f)],
                    _ => 1.8f, _ => arrowColor, 0f, 1);
                if (av.Length >= 4)
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, av, 0, av.Length - 2);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>死亡溶解: DissolveBurn 单批画本体 (灼烧边紫白, 自下而上剥落)。</summary>
        private void DrawDissolvingBody(SpriteBatch sb, Texture2D texture, Rectangle frame,
            Vector2 origin, SpriteEffects effects, Vector2 bodyPos) {
            Effect fx = ACMShaders.DissolveBurn;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(deathDissolve, 0f, 0.99f));
            fx.Parameters["uEdgeWidth"]?.SetValue(0.1f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.4f);
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(ArgusPurple.ToVector3(), 1f));
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f));
            fx.Parameters["uSweepStrength"]?.SetValue(0.35f);

            sb.End();
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            sb.Draw(texture, bodyPos, frame, Color.White, NPC.rotation, origin, NPC.scale * drawScale, effects, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>全场唯一 impact frame: PaletteLUT 黑白高对比 (占全屏名额)。</summary>
        private void DrawImpactFrame(SpriteBatch sb) {
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(impactFrame, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uSaturation"]?.SetValue(0f);
            fx.Parameters["uHueShift"]?.SetValue(0f);
            fx.Parameters["uShadowTint"]?.SetValue(new Vector4(0f, 0f, 0f, 1f));
            fx.Parameters["uHighlightTint"]?.SetValue(new Vector4(1f, 1f, 1f, 1f));
            fx.Parameters["uSplit"]?.SetValue(0f);

            ACMShaders.ApplyScreenPostProcess(sb, fx);
        }

        /// <summary>全视之域: 以域中心为心的弱全屏折射 (GenericWarp · rift, 占全屏名额)。</summary>
        private void DrawDomainRift(SpriteBatch sb) {
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 center = Phase == BossPhase.P3_Collapse && gazeAim != Vector2.Zero ? gazeAim : NPC.Center;
            Vector2 uv = (center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(domainPower * 0.55f, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(1.0f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWarpScale"]?.SetValue(1.1f);
            fx.Parameters["uChroma"]?.SetValue(0.35f);
            fx.Parameters["uRadialPull"]?.SetValue(0.18f);
            fx.Parameters["uMode"]?.SetValue(3f);
            fx.Parameters["uTint"]?.SetValue(new Vector4(new Color(120, 70, 220).ToVector3(), 0.4f));

            ACMShaders.ApplyScreenPostProcess(sb, fx);
        }

        #endregion
    }
}

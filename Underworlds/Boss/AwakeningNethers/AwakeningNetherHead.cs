using AncientChineseMythology.Underworlds.Boss.AwakeningNethers.Items;
using Microsoft.Xna.Framework.Graphics;
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

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 头部 — V3《冥渊尽头的活体地狱》。
    ///
    /// 编排核心（choreography skill）:
    ///   ● 速度对比: 盘旋 ~10px/f ←→ 冥渊穿刺 46~52px/f, 穿刺前 36f pow8 反向蓄势(吸气式后撤)。
    ///   ● 三幕手写循环表(非随机): 巡游(穿刺教学) → 次元裂隙(门穿梭+脊波尾鞭) → 虚空吞噬(衔尾困杀+奇点)。
    ///   ● 三大演出节拍齐备: 入场「冥渊苏醒」(震颤→骨节破土→破土冲天→咆哮定格) /
    ///     幕间无敌预告+清弹 / 死亡「冥渊崩解」(挣扎→尾到头逐节爆裂→头部内爆终响)。
    ///   ● 公平阀门: 冲刺线红色预警 36f 恒定、弹速 wind-up、追踪截止、距离栓绳、
    ///     伤害窗口与视觉对齐(演出中接触伤害清零)、换阶段清弹、每个状态保底出口。
    ///
    /// 多人安全: 状态机走 npc.ai[0..3] + SendExtraAI 扩展字段, 切换时 netUpdate;
    /// 弹幕/召唤仅服务器端; 演出标量与绘制只在客户端路径。
    /// </summary>
    [AutoloadBossHead]
    public class AwakeningNetherHead : AwakeningNether
    {
        public override WormType NPCWormType => WormType.Head;

        // ============================ 状态机 ============================

        private enum AIState
        {
            Awakening,      // 入场演出「冥渊苏醒」
            ActCycle,       // 幕内手写循环 (act 决定循环表)
            ActTransition,  // 幕间转换 (无敌预告节拍 + 清弹)
            Finality,       // 觉醒终末 (15% 一次性处决签名)
            DeathThroes     // 死亡演出「冥渊崩解」
        }

        /// <summary>幕内招式 — 每幕一张手写循环表, 压迫与喘息显式交替 (PACING §2)。</summary>
        private enum Move
        {
            OrbitVolley,    // 盘旋虚空弹 (聚能→齐射×3)
            Pierce,         // 冥渊穿刺 (pow8 反向蓄势 → 46px/f 直线穿刺 → 硬刹)
            BreathCorridor, // 魂焰走廊 (预警线扫过 → 吐息横扫铺魂雾)
            RiftGates,      // 裂隙门穿梭 (成对门, 穿入A出B ×3)
            SpineWhip,      // 脊波尾鞭 (头部甩头 → 行波沿身传播 → 波峰喷魂火余烬)
            SoulStorm,      // 灵魂风暴 (聚能→环形一次性爆发)
            OuroborosRing,  // 衔尾困杀 (龙环收缩 + 环上齐射, 尾隙即生门)
            Vortex,         // 虚空奇点 (吸积盘 + 可清除噬魂卫星)
            Breather        // 喘息 (刻意留白)
        }

        private static readonly Move[] ActICycle = [Move.OrbitVolley, Move.Pierce, Move.Pierce, Move.BreathCorridor, Move.Breather];
        private static readonly Move[] ActIICycle = [Move.RiftGates, Move.SpineWhip, Move.Pierce, Move.SoulStorm, Move.Breather];
        private static readonly Move[] ActIIICycle = [Move.OuroborosRing, Move.Pierce, Move.Pierce, Move.Vortex, Move.Breather];

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // ai[1] = 盘旋角 (CircleTarget)
        // ai[2] = attackTimer (当前状态/招式内推进帧)
        // ai[3] = moveIndex (当前幕循环表下标)
        private ref float AttackTimer => ref NPC.ai[2];
        private int MoveIndex {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        // ===== SendExtraAI 同步的扩展状态 =====
        private int act = 1;              // 当前幕 1/2/3
        private int pendingAct = 1;       // 转换目标幕
        private int moveStep;             // 招式内子步骤
        private int comboCount;           // 招式内重复计数 (穿门次数等)
        private bool finalityDone;        // 觉醒终末是否已触发 (一次性)
        private bool enraged;             // 终末后的永久狂暴
        private Vector2 gateA;            // 裂隙门·入口
        private Vector2 gateB;            // 裂隙门·出口
        private Vector2 arenaCenter;      // 衔尾环心 / 奇点 / 入场冥渊之眼
        private Vector2 dashDir;          // 穿刺锁定方向
        private float laneY;              // 魂焰走廊高度
        private float laneCenterX;        // 走廊中心X (锁定后不追踪)
        private float groundY;            // 入场地面高度

        // ===== 纯本地字段 (视觉/缓存, 不参与逻辑判定) =====
        private int defaultHeadDamage;
        private int despawnTimer;

        // V3 演出标量 (经 AwakeningNetherScreenSystem / PostDraw 消费)
        private float fogTint;
        private float riftWarp;
        private float voidWarp;
        private float bloom;
        private float runic;
        private Vector2 bloomCenter;
        private Vector2 warpCenter;
        private Vector2 runicCenter;
        private float runicRadius = 340f;
        private bool runicLethal;

        /// <summary>体节可见度 (入场破土前 0, 供基类绘制/光照统一取用)。</summary>
        public float SegmentAlpha { get; private set; } = 1f;

        /// <summary>演出节拍(入场/转换/死亡)中为 false — 全身接触伤害清零, 伤害窗口与视觉对齐。</summary>
        public bool ContactDamageActive =>
            CurrentState is not (AIState.Awakening or AIState.ActTransition or AIState.DeathThroes);

        /// <summary>狂暴节奏系数: 觉醒终末后所有节拍收紧 18%。</summary>
        private float Tempo => enraged ? 0.82f : 1f;
        private int D(int frames) => (int)(frames * Tempo);

        private Vector2 HeadingDir => NPC.velocity.LengthSquared() > 0.5f
            ? NPC.velocity.SafeNormalize(Vector2.UnitX)
            : (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
        private Vector2 Mouth => NPC.Center + HeadingDir * 55f;

        // ============================ 初始化 ============================

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AwakeningNetherBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 100;
            NPC.height = 100;
            NPC.lifeMax = 11200000;
            NPC.damage = 200;
            NPC.defense = 90;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AbyssalSpine>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<PhantomBreath>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulErosionScepter>(), 2));
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
            // 「冥渊苏醒」从玩家脚下的地底开始 — 位置细化在 Awakening t==1 完成
            CurrentState = AIState.Awakening;
            NPC.TargetClosest(false);
            if (NPC.HasValidTarget)
                NPC.Center = Target.Center + new Vector2(0f, 620f);
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }

        // ============================ 多人同步 ============================

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write((byte)act);
            writer.Write((byte)pendingAct);
            writer.Write((byte)moveStep);
            writer.Write((byte)comboCount);
            BitsByte flags = new(finalityDone, enraged);
            writer.Write(flags);
            writer.WriteVector2(gateA);
            writer.WriteVector2(gateB);
            writer.WriteVector2(arenaCenter);
            writer.WriteVector2(dashDir);
            writer.Write(laneY);
            writer.Write(laneCenterX);
            writer.Write(groundY);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            act = reader.ReadByte();
            pendingAct = reader.ReadByte();
            moveStep = reader.ReadByte();
            comboCount = reader.ReadByte();
            BitsByte flags = reader.ReadByte();
            finalityDone = flags[0];
            enraged = flags[1];
            gateA = reader.ReadVector2();
            gateB = reader.ReadVector2();
            arenaCenter = reader.ReadVector2();
            dashDir = reader.ReadVector2();
            laneY = reader.ReadSingle();
            laneCenterX = reader.ReadSingle();
            groundY = reader.ReadSingle();
        }

        // ============================ 主 AI ============================

        public override void AI() {
            base.AI();
            UnderworldPlayer.UnderworldEffect = true;

            if (defaultHeadDamage == 0)
                defaultHeadDamage = NPC.damage;

            // 全灭脱战: 潜回冥渊
            if (!TryValidateTarget())
                return;

            float lifePct = (float)NPC.life / NPC.lifeMax;

            // 头部脊波积分: 甩头/挣扎注入的冲量在此消化, 并向体节传播
            SpringVel *= 0.86f;
            SpringOffset += SpringVel;
            SpringOffset *= 0.92f;

            // 每帧默认可受击 — 仅演出节拍主动开启无敌
            NPC.dontTakeDamage = false;

            CheckActTransition(lifePct);

            AttackTimer++;

            UpdatePresentationTargets();
            EmitSegmentMiasma();

            switch (CurrentState) {
                case AIState.Awakening: AwakeningBehavior(); break;
                case AIState.ActCycle: ActCycleBehavior(); break;
                case AIState.ActTransition: ActTransitionBehavior(); break;
                case AIState.Finality: FinalityBehavior(); break;
                case AIState.DeathThroes: DeathThroesBehavior(); break;
            }

            UpdateContactDamage();
            UpdateRotation();

            // 发布 V3 演出标量 (纯本地视觉)
            if (!Main.dedServ) {
                AwakeningNetherScreenSystem.Publish(fogTint, bloom, bloomCenter,
                    runic, runicCenter, runicRadius, runicLethal, (float)Main.GlobalTimeWrappedHourly);
            }
        }

        /// <summary>目标校验与全灭脱战 (状态机保底出口)。</summary>
        private bool TryValidateTarget() {
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            if (NPC.HasValidTarget && !Main.player[NPC.target].dead) {
                despawnTimer = 0;
                return true;
            }

            // 全灭: 加速潜入地底, 远离后整列消失
            despawnTimer++;
            NPC.velocity.X *= 0.98f;
            NPC.velocity.Y += 0.55f;
            SegmentAlpha = MathHelper.Lerp(SegmentAlpha, 0f, 0.02f);
            if (despawnTimer > 150) {
                NPC.active = false; // 体节经 FatherNPC 链式跟随消失
                NPC.netUpdate = true;
            }
            return false;
        }

        /// <summary>接触伤害窗口与视觉严格对齐: 演出清零; 仅冲刺步骤全伤; 其余(含反向蓄势)降至 55%。</summary>
        private void UpdateContactDamage() {
            if (!ContactDamageActive) {
                NPC.damage = 0;
                return;
            }
            bool dashing = CurrentState == AIState.ActCycle
                && ((CurrentMove == Move.Pierce && moveStep == 1)
                    || (CurrentMove == Move.RiftGates && moveStep == 2));
            NPC.damage = dashing && NPC.velocity.Length() > 22f
                ? defaultHeadDamage
                : (int)(defaultHeadDamage * 0.55f);
        }

        /// <summary>
        /// 演出标量驱动: 冥雾随幕递进加深; 扭曲/泛光/符阵为脉冲式, 自然衰减、各 beat 抬升。
        /// </summary>
        private void UpdatePresentationTargets() {
            float fogTarget = CurrentState == AIState.DeathThroes ? 0.5f
                : act >= 3 ? 0.42f : (act == 2 ? 0.30f : 0.18f);
            fogTint = MathHelper.Lerp(fogTint, fogTarget, 0.02f);

            riftWarp = MathHelper.Lerp(riftWarp, 0f, 0.05f);
            voidWarp = MathHelper.Lerp(voidWarp, 0f, 0.06f);
            bloom = MathHelper.Lerp(bloom, 0f, 0.08f);
            runic = MathHelper.Lerp(runic, 0f, 0.06f);

            bloomCenter = NPC.Center;
        }

        // ============================ 幕转换 ============================

        /// <summary>
        /// 脚本化幕转换: 60% → 第二幕, 30% → 第三幕, 15% → 觉醒终末 (一次性)。
        /// 只允许从 ActCycle 打断 — 演出节拍不可被打断。
        /// </summary>
        private void CheckActTransition(float lifePct) {
            if (CurrentState != AIState.ActCycle)
                return;

            if (!finalityDone && lifePct <= 0.15f) {
                EnterFinality();
                return;
            }

            int targetAct = lifePct > 0.6f ? 1 : (lifePct > 0.3f ? 2 : 3);
            if (targetAct > act) {
                pendingAct = targetAct;
                EnterTransition();
            }
        }

        private void EnterTransition() {
            CurrentState = AIState.ActTransition;
            AttackTimer = 0;
            moveStep = 0;
            NPC.velocity *= 0.3f;
            ClearBossProjectiles(); // 换阶段清弹 (公平阀门)
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
            ACMUtils.AddScreenShake(12f);
            bloom = 1f;
            bloomCenter = NPC.Center;
            warpCenter = NPC.Center;
            if (pendingAct >= 3)
                voidWarp = Math.Max(voidWarp, 0.55f);
            else
                riftWarp = Math.Max(riftWarp, 0.55f);
            NPC.netUpdate = true;
        }

        private void EnterFinality() {
            finalityDone = true;
            CurrentState = AIState.Finality;
            AttackTimer = 0;
            moveStep = 0;
            comboCount = 0;
            NPC.velocity *= 0.3f;
            ClearBossProjectiles();
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.5f }, NPC.Center);
            ACMUtils.AddScreenShake(12f);
            bloom = 1f;
            bloomCenter = NPC.Center;
            warpCenter = NPC.Center;
            voidWarp = Math.Max(voidWarp, 0.5f);
            NPC.netUpdate = true;
        }

        /// <summary>清除本 Boss 的全部敌对弹幕 (换阶段/死亡的公平阀门)。</summary>
        private void ClearBossProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int[] mine = [
                ModContent.ProjectileType<AwakeningNetherBreath>(),
                ModContent.ProjectileType<AwakeningNetherRift>(),
                ModContent.ProjectileType<AwakeningNetherVoidBolt>(),
                ModContent.ProjectileType<AwakeningNetherSoulOrb>(),
                ModContent.ProjectileType<AwakeningNetherMiasma>(),
                ModContent.ProjectileType<AwakeningNetherSegmentLaser>(),
                ModContent.ProjectileType<AwakeningNetherSoulSatellite>(),
                ModContent.ProjectileType<AwakeningNetherSoulWisp>(),
            ];
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.hostile && Array.IndexOf(mine, proj.type) >= 0)
                    proj.Kill();
            }
        }

        /// <summary>
        /// 幕间转换 — 无敌预告节拍。结束后进入目标幕循环表首招 (该幕的"教学招")。
        /// </summary>
        private void ActTransitionBehavior() {
            NPC.dontTakeDamage = true;

            Vector2 hover = Target.Center + new Vector2(0, -340f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 v = Main.rand.NextVector2CircularEdge(12f, 12f);
                var d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = v;
                d.scale = 2.2f;
                d.alpha = 100;
            }

            if (AttackTimer >= 75) {
                act = pendingAct;
                StartCycle();
            }
        }

        // ============================ 幕内循环 ============================

        private Move[] CurrentTable => act >= 3 ? ActIIICycle : (act == 2 ? ActIICycle : ActICycle);
        private Move CurrentMove => CurrentTable[Math.Clamp(MoveIndex, 0, CurrentTable.Length - 1)];

        private void StartCycle() {
            CurrentState = AIState.ActCycle;
            MoveIndex = 0;
            AttackTimer = 0;
            moveStep = 0;
            comboCount = 0;
            NPC.netUpdate = true;
        }

        /// <summary>推进循环表 — 每招保证退出, 无死路 (失败模式 #状态机死路)。</summary>
        private void NextMove() {
            MoveIndex = (MoveIndex + 1) % CurrentTable.Length;
            AttackTimer = 0;
            moveStep = 0;
            comboCount = 0;
            NPC.netUpdate = true;
        }

        private void ActCycleBehavior() {
            switch (CurrentMove) {
                case Move.OrbitVolley: Move_OrbitVolley(); break;
                case Move.Pierce: Move_Pierce(); break;
                case Move.BreathCorridor: Move_BreathCorridor(); break;
                case Move.RiftGates: Move_RiftGates(); break;
                case Move.SpineWhip: Move_SpineWhip(); break;
                case Move.SoulStorm: Move_SoulStorm(); break;
                case Move.OuroborosRing: Move_OuroborosRing(); break;
                case Move.Vortex: Move_Vortex(); break;
                default: Move_Breather(); break;
            }
        }

        /// <summary>环绕玩家的基础移动 (含距离栓绳 — 防"飞出屏幕绕圈")。</summary>
        private void CircleTarget(float radius, float angSpeed, float chase = 8f) {
            NPC.ai[1] += angSpeed;
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 170f);
            Vector2 toTarget = targetPos - NPC.Center;
            float inertia = 18f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / chase) / inertia;

            // 距离栓绳
            if (NPC.Center.Distance(Target.Center) > 1500f)
                NPC.velocity += (Target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.9f;
        }

        /// <summary>聚能收敛粒子 — 密度 ∝ 进度且在 72% 硬截止 (爆发前的静默吸气)。</summary>
        private void ChargeDust(Vector2 center, float radius, float chargeT, int perFrame = 3) {
            if (Main.dedServ || chargeT > 0.72f)
                return;
            for (int i = 0; i < perFrame; i++) {
                if (!Main.rand.NextBool(2))
                    continue;
                Vector2 p = center + Main.rand.NextVector2CircularEdge(radius, radius);
                var d = Dust.NewDustPerfect(p, Main.rand.NextBool(3) ? DustID.CursedTorch : DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f + chargeT;
                d.velocity = (center - p) * 0.09f;
                d.alpha = 60;
            }
        }

        // ---------- A1 盘旋虚空弹 ----------

        private void Move_OrbitVolley() {
            CircleTarget(460f, 0.042f);
            int t = (int)AttackTimer;
            int period = D(70);

            // 每轮: [T-30,T-8] 聚能, [T-8,T] 静默, T 齐射
            int inPeriod = t % period;
            int fireAt = period - 1;
            if (inPeriod >= fireAt - 30 && inPeriod < fireAt - 8) {
                float chargeT = (inPeriod - (fireAt - 30)) / 30f;
                ChargeDust(Mouth, 80f, chargeT);
                if (!Main.dedServ)
                    AwakeningNetherScreenSystem.RequestSoulflame(Mouth, HeadingDir, 130f,
                        0.35f + chargeT * 0.4f, NPC.whoAmI * 0.37f, 0.85f,
                        TelegraphColors.GhostGreen, AwakeningNetherHelper.AwakeningPurple);
            }
            if (inPeriod == fireAt - 30 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);

            if (inPeriod == fireAt) {
                ShootVoidBolts();
                // 后坐 — 质量即反作用
                NPC.velocity -= HeadingDir * 6f;
            }

            if (t >= period * 3)
                NextMove();
        }

        // ---------- A2 冥渊穿刺 (招牌) ----------

        private void Move_Pierce() {
            int t = (int)AttackTimer;
            switch (moveStep) {
                case 0: // 反向蓄势 36f: pow8 后撤 — 前 30f 近乎静止(可读), 最后 6f 猛然吸气
                    if (t == 1) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.35f, Volume = 1.1f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    // 预警线方向: 28f 前跟踪, 之后锁定(玩家有 8f 反应期)
                    if (t <= 28)
                        dashDir = (Target.Center + Target.velocity * 14f - NPC.Center).SafeNormalize(Vector2.UnitX);

                    float reel = MathF.Pow(t / 36f, 8f) * 42f;
                    NPC.velocity = NPC.velocity * 0.72f - dashDir * reel;

                    if (t >= 36) {
                        moveStep = 1;
                        AttackTimer = 0;
                        NPC.velocity = dashDir * (enraged ? 52f : 46f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                        ACMUtils.AddScreenShake(7f);
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 穿刺: 零转向直线 (直线才够快) — 穿过玩家 280px 或超时即刹
                    if (!Main.dedServ && Main.rand.NextBool()) {
                        var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Shadowflame);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * 0.2f;
                        d.scale = 1.8f;
                    }
                    bool passed = Vector2.Dot(Target.Center - NPC.Center, dashDir) < -280f;
                    if ((t > 10 && passed) || t >= 34) {
                        moveStep = 2;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 2: // 硬刹: ×0.80/f — "砸进位置"的质量感
                    NPC.velocity *= 0.80f;
                    if (t >= 18)
                        NextMove();
                    break;
            }
        }

        // ---------- A3 魂焰走廊 ----------

        private void Move_BreathCorridor() {
            int t = (int)AttackTimer;
            float dirSign = laneCenterX >= NPC.Center.X ? 1f : -1f;

            switch (moveStep) {
                case 0: // 瞄准 45f: 锁定走廊, 预警线先行
                    if (t == 1) {
                        laneY = Target.Center.Y + 150f;
                        laneCenterX = Target.Center.X;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    dirSign = laneCenterX >= NPC.Center.X ? 1f : -1f;
                    Vector2 aim = new(laneCenterX - dirSign * 640f, laneY - 330f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (aim - NPC.Center) * 0.05f, 0.09f);

                    ChargeDust(Mouth, 70f, t / 45f);
                    if (!Main.dedServ)
                        AwakeningNetherScreenSystem.RequestSoulflame(Mouth, HeadingDir, 140f,
                            0.3f + t / 45f * 0.5f, NPC.whoAmI * 0.37f, 0.85f,
                            TelegraphColors.GhostGreen, AwakeningNetherHelper.AwakeningPurple);

                    if (t >= 45) {
                        moveStep = 1;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 扫射 55f: 吐息焦点沿走廊扫过, 铺魂雾
                    float sweepT = t / 55f;
                    float sweepX = laneCenterX + MathHelper.Lerp(-560f, 560f, sweepT) * dirSign;
                    Vector2 lanePoint = new(sweepX, laneY);

                    Vector2 hover = new(sweepX - dirSign * 260f, laneY - 340f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.06f, 0.1f);

                    if (t % 6 == 0)
                        FireBreathAt(lanePoint, 70);
                    if (t % 12 == 0)
                        SpawnMiasma(new Vector2(laneCenterX + Main.rand.NextFloat(-520f, 520f), laneY), 1.2f);

                    bloom = Math.Max(bloom, 0.45f);
                    bloomCenter = Mouth;

                    if (t >= 55) {
                        moveStep = 2;
                        AttackTimer = 0;
                    }
                    break;

                case 2: // 抬升脱离
                    NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(NPC.velocity.X, -8f), 0.06f);
                    if (t >= 25)
                        NextMove();
                    break;
            }
        }

        private void FireBreathAt(Vector2 point, int dmg) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Vector2 dir = (point - NPC.Center).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 50f, dir * 14f,
                ModContent.ProjectileType<AwakeningNetherBreath>(), GetProjectileDamage(dmg), 0f, Main.myPlayer,
                ai0: enraged ? 1 : 0);
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.4f, Volume = 0.65f }, NPC.Center);
        }

        // ---------- B1 裂隙门穿梭 ----------

        private void Move_RiftGates() {
            int t = (int)AttackTimer;
            switch (moveStep) {
                case 0: // 开门 60f: 成对生成, 出口门(B)即危险门
                    if (t == 1) {
                        float side = NPC.Center.X < Target.Center.X ? -1f : 1f;
                        gateA = Target.Center + new Vector2(side * 660f, -140f);
                        gateB = Target.Center + new Vector2(-side * 660f, -140f);
                        SpawnGate(gateA, lethal: false);
                        SpawnGate(gateB, lethal: true);
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                        NPC.netUpdate = true;
                    }
                    CircleTarget(430f, 0.05f);
                    // 出口门向心收口预警 (主题色 → 龙将至转致命)
                    runicCenter = gateB;
                    runicRadius = 300f;
                    runicLethal = t > 35;
                    runic = Math.Max(runic, 0.3f + t / 60f * 0.35f);
                    riftWarp = Math.Max(riftWarp, 0.3f);
                    warpCenter = gateB;

                    if (t >= 60) {
                        moveStep = 1;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 就位: 飞向入口门外侧
                    Vector2 standby = gateA + (gateA - gateB).SafeNormalize(Vector2.UnitX) * 260f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (standby - NPC.Center) * 0.07f, 0.1f);
                    riftWarp = Math.Max(riftWarp, 0.35f);
                    warpCenter = gateB;
                    if (NPC.Center.Distance(standby) < 120f || t >= 50) {
                        moveStep = 2;
                        AttackTimer = 0;
                        NPC.velocity = (gateA - NPC.Center).SafeNormalize(Vector2.UnitY) * 42f;
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    break;

                case 2: // 穿梭: 入A出B, 直线贯场 ×3 (直线可读, 玩家离开 A↔B 连线即安全)
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        (gateA - NPC.Center).SafeNormalize(Vector2.UnitY) * 42f, 0.06f);
                    riftWarp = Math.Max(riftWarp, 0.6f);
                    warpCenter = gateB;
                    runicCenter = gateB;
                    runicLethal = true;
                    runic = Math.Max(runic, 0.65f);

                    if (NPC.Center.Distance(gateA) < 90f) {
                        NPC.Center = gateB;
                        NPC.velocity = (gateA - gateB).SafeNormalize(Vector2.UnitY) * 42f;
                        comboCount++;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                        ACMUtils.AddScreenShake(6f);
                        bloom = Math.Max(bloom, 0.8f);
                        bloomCenter = gateB;
                        riftWarp = 0.85f;
                        NPC.netUpdate = true;
                        if (comboCount >= 3) {
                            moveStep = 3;
                            AttackTimer = 0;
                        }
                    }
                    if (t >= 260) { // 保底出口
                        moveStep = 3;
                        AttackTimer = 0;
                    }
                    break;

                case 3: // 收尾
                    NPC.velocity *= 0.9f;
                    if (t >= 25)
                        NextMove();
                    break;
            }
        }

        // ---------- B2 脊波尾鞭 ----------

        private void Move_SpineWhip() {
            int t = (int)AttackTimer;
            CircleTarget(480f, 0.035f);

            // 两次甩头注入行波 (一次输入 → 一秒沿身传播的有机运动)
            if (t == 12 || t == 52) {
                SpringVel += t == 12 ? 30f : -34f;
                SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.5f, Volume = 1.1f }, NPC.Center);
                ACMUtils.AddScreenShake(4f);
            }
            if (t == 18 || t == 58)
                SpringVel -= t == 18 ? 24f : -26f;

            // 波峰经过体节 → 涟漪式喷出魂火余烬 (index*2 帧后波到达该节)
            if (Main.netMode != NetmodeID.MultiplayerClient && t >= 14 && t <= 150) {
                var segs = GetSegments();
                foreach (NPC seg in segs) {
                    if (seg.ModNPC is not AwakeningNether worm)
                        continue;
                    int idx = worm.SummonCount;
                    if (idx % 5 != 0)
                        continue;
                    // 第一波 / 第二波到达帧
                    if (t == 14 + idx * 2 || t == 54 + idx * 2)
                        SpawnWisp(seg.Center, seg.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2)
                            * (idx % 10 == 0 ? 1f : -1f) * 3.4f);
                }
            }

            if (t >= D(165))
                NextMove();
        }

        // ---------- B3 灵魂风暴 ----------

        private void Move_SoulStorm() {
            int t = (int)AttackTimer;
            Vector2 hover = Target.Center - new Vector2(0, 390f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            switch (moveStep) {
                case 0: // 聚能 50f (72% 截止 → 静默)
                    if (t == 1)
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                    ChargeDust(NPC.Center, 150f, t / 50f, 4);
                    if (t >= 50) {
                        moveStep = 1;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 一次性环爆
                    if (t == 1) {
                        ShootSoulStorm();
                        ACMUtils.AddScreenShake(9f);
                        bloom = 0.9f;
                        bloomCenter = NPC.Center;
                    }
                    if (t >= 45)
                        NextMove();
                    break;
            }
        }

        // ---------- C1 衔尾困杀 ----------

        private void Move_OuroborosRing() {
            int t = (int)AttackTimer;
            switch (moveStep) {
                case 0: // 40f: 锁环心, 加速进入环轨
                    if (t == 1) {
                        arenaCenter = Target.Center;
                        NPC.ai[1] = (NPC.Center - arenaCenter).ToRotation();
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    runicCenter = arenaCenter;
                    runicRadius = 520f;
                    runicLethal = false;
                    runic = Math.Max(runic, 0.25f + t / 40f * 0.3f);

                    Vector2 entry = arenaCenter + NPC.ai[1].ToRotationVector2() * 520f;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (entry - NPC.Center) * 0.08f, 0.12f);
                    if (NPC.Center.Distance(entry) < 100f || t >= 55) {
                        moveStep = 1;
                        AttackTimer = 0;
                        comboCount = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 240f: 环轨收缩 520→340, 环上涟漪齐射; 尾隙即生门
                    float shrinkT = MathHelper.Clamp(t / 240f, 0f, 1f);
                    float radius = MathHelper.Lerp(520f, 340f, shrinkT);
                    float angSpeed = 21f / radius; // 头部线速度 ~21px/f
                    NPC.ai[1] += angSpeed;
                    Vector2 orbit = arenaCenter + NPC.ai[1].ToRotationVector2() * radius;
                    Vector2 toOrbit = orbit - NPC.Center;
                    NPC.velocity = toOrbit.LengthSquared() > 26f * 26f
                        ? toOrbit.SafeNormalize(Vector2.Zero) * 26f
                        : toOrbit;

                    runicCenter = arenaCenter;
                    runicRadius = radius;
                    runicLethal = false;
                    runic = Math.Max(runic, 0.45f);

                    // 环上体节向心齐射 (慢 wisp, 内密外疏; 纯走位可解)
                    if (Main.netMode != NetmodeID.MultiplayerClient && t % D(30) == 0 && t > 20) {
                        var segs = GetSegments();
                        int volley = t / D(30);
                        for (int k = 0; k < 4; k++) {
                            int idx = (volley * 3 + k * 11) % Math.Max(segs.Count, 1);
                            if (idx < segs.Count) {
                                Vector2 inward = (arenaCenter - segs[idx].Center).SafeNormalize(Vector2.Zero);
                                SpawnWisp(segs[idx].Center, inward * 3.4f);
                            }
                        }
                    }

                    // 尾隙生门高亮 (鬼绿 — 安全色语义)
                    if (!Main.dedServ && t % 4 == 0) {
                        NPC tail = FindTail();
                        if (tail != null) {
                            var d = Dust.NewDustPerfect(tail.Center + Main.rand.NextVector2Circular(26f, 26f), DustID.CursedTorch);
                            d.noGravity = true;
                            d.scale = 1.7f;
                            d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                        }
                    }

                    // 玩家已出环 → 早退 (不硬等自己的时间表)
                    if (Target.Center.Distance(arenaCenter) > radius + 240f)
                        comboCount++;
                    else
                        comboCount = 0;
                    if (comboCount >= 40 || t >= 240) {
                        moveStep = 2;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 2: // 散开 30f → 循环表接穿刺×2 (环收拢自然坍缩成穿刺连段)
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        (NPC.Center - arenaCenter).SafeNormalize(Vector2.UnitX) * 16f, 0.08f);
                    if (t >= 30)
                        NextMove();
                    break;
            }
        }

        private NPC FindTail() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.realLife == NPC.whoAmI && n.ModNPC is AwakeningNetherTail)
                    return n;
            }
            return null;
        }

        // ---------- C2 虚空奇点 ----------

        private void Move_Vortex() {
            int t = (int)AttackTimer;
            switch (moveStep) {
                case 0: // 55f: 奇点旋开 + 卫星召唤
                    if (t == 1) {
                        arenaCenter = Target.Center - new Vector2(0, 260f);
                        SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    CircleTarget(500f, 0.045f);

                    float openT = t / 55f;
                    voidWarp = Math.Max(voidWarp, openT * 0.4f);
                    warpCenter = arenaCenter;
                    runicCenter = arenaCenter;
                    runicRadius = 360f;
                    runicLethal = false;
                    runic = Math.Max(runic, 0.3f + openT * 0.3f);
                    if (!Main.dedServ)
                        AwakeningNetherScreenSystem.RequestVoidRift(arenaCenter, 460f, openT, NPC.whoAmI * 0.61f,
                            0f, 0.85f, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);

                    if (t >= 55) {
                        SpawnSoulSatellites();
                        ACMUtils.AddScreenShake(10f);
                        bloom = 0.9f;
                        bloomCenter = arenaCenter;
                        voidWarp = Math.Max(voidWarp, 0.6f);
                        moveStep = 1;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 300f: 奇点活跃 — 环带引力(留反制窗口) + 卫星压场
                    CircleTarget(470f, 0.05f);
                    voidWarp = Math.Max(voidWarp, enraged ? 0.75f : 0.6f);
                    warpCenter = arenaCenter;
                    runicCenter = arenaCenter;
                    runicLethal = true;
                    runic = Math.Max(runic, 0.55f);
                    if (!Main.dedServ)
                        AwakeningNetherScreenSystem.RequestVoidRift(arenaCenter, 460f, 1f, NPC.whoAmI * 0.61f,
                            0.55f, 1f, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);

                    // 环带引力: 仅 220~900px, 强度约为玩家加速度一半 — 可对抗
                    foreach (Player p in Main.player) {
                        if (p == null || !p.active || p.dead)
                            continue;
                        float dist = p.Distance(arenaCenter);
                        if (dist > 220f && dist < 900f)
                            p.velocity += (arenaCenter - p.Center).SafeNormalize(Vector2.Zero) * 0.16f;
                        // 奇点核心: 魂蚀叠层 (DoT 身份, 非爆发)
                        if (dist < 120f && t % 18 == 0)
                            p.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(1);
                    }

                    if (t >= D(300)) {
                        moveStep = 2;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 2: // 60f: 奇点闭合喘息
                    NPC.velocity *= 0.94f;
                    if (!Main.dedServ) {
                        float closeT = 1f - t / 60f;
                        AwakeningNetherScreenSystem.RequestVoidRift(arenaCenter, 460f, closeT, NPC.whoAmI * 0.61f,
                            0f, closeT, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);
                    }
                    if (t >= 60)
                        NextMove();
                    break;
            }
        }

        // ---------- 喘息 ----------

        private void Move_Breather() {
            CircleTarget(520f, 0.03f);
            if (AttackTimer >= D(60))
                NextMove();
        }

        // ============================ 入场「冥渊苏醒」 ============================

        private void AwakeningBehavior() {
            NPC.dontTakeDamage = true;
            int t = (int)AttackTimer;

            if (t <= 1) {
                // 锁定冥渊之眼: 玩家脚下的地面
                groundY = FindGroundY(Target.Center);
                arenaCenter = new Vector2(Target.Center.X, groundY);
                NPC.Center = arenaCenter + new Vector2(0f, 520f);
                NPC.velocity = Vector2.Zero;
                SegmentAlpha = 0f;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.9f, Pitch = -0.7f }, arenaCenter);
                NPC.netUpdate = true;
            }

            if (t < 152) {
                NPC.velocity = Vector2.Zero;
                SegmentAlpha = 0f;
            }

            // —— 0~150: 地面震颤渐强 + 冥渊之眼旋开 + 三次逼近的骨节破土 ——
            if (t < 150) {
                float rumbleT = t / 150f;
                ACMUtils.AddScreenShake(rumbleT * rumbleT * 5f);
                fogTint = Math.Max(fogTint, rumbleT * 0.3f);
                if (!Main.dedServ)
                    AwakeningNetherScreenSystem.RequestVoidRift(arenaCenter, 380f, rumbleT * 0.45f,
                        0f, 0f, 0.8f, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);

                // 骨节破土: 越来越近、越来越响 (蜂鸣加速原理)
                if (t == 60 || t == 95 || t == 125) {
                    int k = t == 60 ? 0 : (t == 95 ? 1 : 2);
                    float off = (k % 2 == 0 ? -1f : 1f) * (260f - k * 100f);
                    Vector2 burst = new(arenaCenter.X + off, groundY);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f + k * 0.12f, Volume = 0.9f + k * 0.2f }, burst);
                        for (int i = 0; i < 26; i++) {
                            var d = Dust.NewDustPerfect(burst + Main.rand.NextVector2Circular(28f, 8f),
                                Main.rand.NextBool() ? DustID.Ash : DustID.Shadowflame);
                            d.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-9f, -3f));
                            d.scale = 1.6f;
                        }
                    }
                    ACMUtils.AddScreenShake(6f + k * 1.5f);
                }
            }
            // t 150~151: 刻意的 2 帧静默 — 爆发前的收缩

            // —— 152: 破土冲天 ——
            if (t == 152) {
                NPC.Center = new Vector2(arenaCenter.X, groundY + 60f);
                NPC.velocity = new Vector2(0f, -34f);
                SegmentAlpha = 1f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                bloom = 1f;
                bloomCenter = arenaCenter;
                if (!Main.dedServ) {
                    for (int i = 0; i < 60; i++) {
                        var d = Dust.NewDustPerfect(arenaCenter + Main.rand.NextVector2Circular(70f, 14f),
                            Main.rand.NextBool(3) ? DustID.Ash : DustID.Shadowflame);
                        d.velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-16f, -5f));
                        d.scale = 2.1f;
                        d.noGravity = Main.rand.NextBool();
                    }
                }
                NPC.netUpdate = true;
            }

            // —— 152~200: 冲天 + 转向悬停 ——
            if (t > 152 && t < 200) {
                Vector2 apex = arenaCenter + new Vector2(0f, -430f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (apex - NPC.Center).SafeNormalize(Vector2.Zero) * 22f, 0.05f);
                if (!Main.dedServ)
                    AwakeningNetherScreenSystem.RequestVoidRift(arenaCenter, 380f, 1f - (t - 152) / 60f,
                        (t - 152) * 0.12f, 0f, 0.8f, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);
            }

            // —— 200~250: 空中定格 + 咆哮全屏冲击 (威压主要来自静止) ——
            if (t >= 200) {
                NPC.velocity *= 0.86f;
                if (t == 205) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.75f, Volume = 1.6f }, NPC.Center);
                    ACMUtils.AddScreenShake(9f);
                    bloom = 1f;
                    bloomCenter = NPC.Center;
                    voidWarp = Math.Max(voidWarp, 0.55f);
                    warpCenter = NPC.Center;
                }
            }

            if (t >= 250)
                StartCycle();
        }

        /// <summary>向下扫描最近的实心地面 (冥渊之眼落点)。</summary>
        private static float FindGroundY(Vector2 from) {
            int tx = Math.Clamp((int)(from.X / 16f), 10, Main.maxTilesX - 10);
            int ty = Math.Clamp((int)(from.Y / 16f), 10, Main.maxTilesY - 10);
            for (int y = ty; y < Math.Min(ty + 90, Main.maxTilesY - 10); y++) {
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                    return y * 16f;
            }
            return from.Y + 620f;
        }

        // ============================ 觉醒终末 (一次性处决签名) ============================

        private void FinalityBehavior() {
            int t = (int)AttackTimer;
            switch (moveStep) {
                case 0: // 75f: 龙体拉直 + 尖啸加速 + 泛光 t³ ramp
                    if (t <= 22)
                        NPC.dontTakeDamage = true; // 防转阶段瞬间被倒地秒杀

                    float dirSign = NPC.Center.X < Target.Center.X ? 1f : -1f;
                    Vector2 straight = new(Target.Center.X + dirSign * 1050f, Target.Center.Y - 250f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity,
                        (straight - NPC.Center).SafeNormalize(Vector2.Zero) * 17f, 0.06f);

                    // 尖啸蜂鸣加速 (延迟数组 → 升调)
                    if (t == 5 || t == 25 || t == 41 || t == 53 || t == 62 || t == 68 || t == 72)
                        SoundEngine.PlaySound(SoundID.Item29 with {
                            Pitch = -0.4f + t / 75f * 0.9f, Volume = 0.7f + t / 75f * 0.5f
                        }, NPC.Center);

                    ChargeDust(NPC.Center, 170f, t / 75f, 5);
                    float rampT = t / 75f;
                    bloom = Math.Max(bloom, rampT * rampT * rampT);
                    bloomCenter = NPC.Center;

                    if (t >= 75) {
                        moveStep = 1;
                        AttackTimer = 0;
                        FireSegmentLasers();
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                        ACMUtils.AddScreenShake(12f);
                        bloom = 1f;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // 90f: 巨型魂焰横扫 + 体节激光帘幕同步落下
                    NPC.velocity *= 0.97f;
                    if (t % 5 == 0)
                        FireFinalityBreath(t);
                    bloom = Math.Max(bloom, 0.5f);
                    bloomCenter = Mouth;
                    if (t >= 90) {
                        moveStep = 2;
                        AttackTimer = 0;
                    }
                    break;

                case 2: // 收束 → 回第三幕永久狂暴
                    NPC.velocity *= 0.92f;
                    if (t >= 30) {
                        enraged = true;
                        act = 3;
                        StartCycle();
                    }
                    break;
            }
        }

        /// <summary>觉醒终末的巨型魂焰扇 (横扫)。</summary>
        private void FireFinalityBreath(int t) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float sweep = MathF.Sin(t * 0.12f) * MathHelper.ToRadians(40f);
            int count = 5;
            float spread = MathHelper.ToRadians(50f);
            for (int i = 0; i < count; i++) {
                float a = sweep - spread / 2 + spread * i / (count - 1);
                Vector2 dir = toPlayer.RotatedBy(a);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 50f, dir * 13f,
                    ModContent.ProjectileType<AwakeningNetherBreath>(), GetProjectileDamage(80), 0f, Main.myPlayer, ai0: 1);
            }
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
        }

        /// <summary>同步体节激光: 沿龙体取样, 同时向下喷射, 形成可读的激光帘幕。</summary>
        private void FireSegmentLasers() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            var segs = GetSegments();
            if (segs.Count == 0)
                return;
            int step = Math.Max(1, segs.Count / 12);
            for (int i = 0; i < segs.Count; i += step) {
                NPC seg = segs[i];
                Projectile.NewProjectile(NPC.GetSource_FromAI(), seg.Center, Vector2.Zero,
                    ModContent.ProjectileType<AwakeningNetherSegmentLaser>(), GetProjectileDamage(110), 0f, Main.myPlayer,
                    ai0: seg.whoAmI, ai1: MathHelper.PiOver2);
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
        }

        // ============================ 死亡「冥渊崩解」 ============================

        public override bool CheckDead() {
            if (CurrentState != AIState.DeathThroes) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                EnterDeathThroes();
                return false;
            }
            return true;
        }

        private void EnterDeathThroes() {
            // 注意: 本方法经 CheckDead 仅在服务器/单机执行 — 音效放到 Behavior t==1 (全端播放)
            CurrentState = AIState.DeathThroes;
            AttackTimer = 0;
            moveStep = 0;
            ClearBossProjectiles();
            NPC.netUpdate = true;
        }

        private void DeathThroesBehavior() {
            NPC.dontTakeDamage = true;
            NPC.life = Math.Max(NPC.life, 1);
            int t = (int)AttackTimer;

            if (t == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.85f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
            }

            // —— 0~90: 挣扎 — 乱序脊波冲量, 鞭状乱摆, 逐渐失速 ——
            if (t < 90) {
                NPC.velocity *= 0.97f;
                if (t % 22 == 3) {
                    SpringVel += (t / 22 % 2 == 0 ? 1f : -1f) * (26f + t * 0.1f);
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);
                }
            }

            // —— 60~195: 尾到头逐节爆裂 (伤害叙事: 身体持续降解而非阈值) ——
            if (t >= 60 && t < 195 && (t - 60) % 3 == 0) {
                int idx = (t - 60) / 3;
                DetonateSegment(idx);
            }

            // —— 195~230: 头部拉升 + 粒子向内收缩 (内爆前奏, 密度递减至静默) ——
            if (t >= 195 && t < 230) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, -12f), 0.07f);
                if (!Main.dedServ && t < 222 && Main.rand.NextBool(2)) {
                    Vector2 p = NPC.Center + Main.rand.NextVector2CircularEdge(180f, 180f);
                    var d = Dust.NewDustPerfect(p, DustID.CursedTorch);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - p) * 0.08f;
                    d.scale = 1.8f;
                }
            }

            // —— 230~248: 内爆收缩 → 终爆 ——
            if (t >= 230) {
                NPC.velocity *= 0.8f;
                NPC.scale = MathHelper.Lerp(NPC.scale, 0.45f, 0.12f);
                if (!Main.dedServ)
                    AwakeningNetherScreenSystem.RequestVoidRift(NPC.Center, 420f, (t - 230f) / 18f,
                        -(t - 230f) * 0.3f, 0.2f, 1f, AwakeningNetherHelper.AwakeningPurple, TelegraphColors.GhostGreen);
                if (t == 244) {
                    bloom = 1f;
                    bloomCenter = NPC.Center;
                    voidWarp = 0.9f;
                    warpCenter = NPC.Center;
                    ACMUtils.AddScreenShake(16f); // 一次性死亡定格 (统一预算)
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1.8f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 1.3f }, NPC.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 90; i++) {
                            int type = Main.rand.NextBool(3) ? DustID.CursedTorch : DustID.Shadowflame;
                            var d = Dust.NewDustPerfect(NPC.Center, type);
                            d.noGravity = true;
                            d.scale = 2.6f;
                            d.velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(6f, 24f);
                        }
                    }
                }
            }

            if (t >= 248 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.checkDead(); // CurrentState 已是 DeathThroes → CheckDead 放行, 正常结算掉落
            }
        }

        /// <summary>死亡演出: 引爆第 idx 节 (从尾数起), 隐藏该节并喷出魂火。</summary>
        private void DetonateSegment(int idx) {
            var segs = GetSegments();
            // 尾在前: 按 SummonCount 降序
            segs.Sort((a, b) => {
                int sa = a.ModNPC is AwakeningNether wa ? wa.SummonCount : 0;
                int sb = b.ModNPC is AwakeningNether wb ? wb.SummonCount : 0;
                return sb.CompareTo(sa);
            });
            if (idx >= segs.Count)
                return;
            NPC seg = segs[idx];
            if (seg.ModNPC is not AwakeningNether worm || worm.Detonated)
                return;
            worm.Detonated = true;

            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    int type = Main.rand.NextBool(3) ? DustID.CursedTorch : DustID.Shadowflame;
                    var d = Dust.NewDustPerfect(seg.Center, type);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = Main.rand.NextVector2Circular(7f, 7f) + new Vector2(0f, -2f);
                }
                if (idx % 8 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = 0.2f - idx * 0.004f, Volume = 0.8f }, seg.Center);
                    ACMUtils.AddScreenShake(7f);
                }
                else if (idx % 3 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.3f, Volume = 0.6f }, seg.Center);
                }
            }
        }

        public override void OnKill() {
            base.OnKill();
            // 主体演出已由 DeathThroes 承担 — 此处只留终响余韵
            if (!Main.dedServ) {
                for (int i = 0; i < 40; i++) {
                    var d = Dust.NewDustPerfect(NPC.Center, DustID.CursedTorch);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = Main.rand.NextVector2Circular(14f, 14f);
                }
            }
        }

        // ============================ 体节机制 / 生成辅助 ============================

        /// <summary>收集本龙的所有体节 (身体 + 尾巴)。</summary>
        private List<NPC> GetSegments() {
            var list = new List<NPC>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.whoAmI != NPC.whoAmI && n.realLife == NPC.whoAmI)
                    list.Add(n);
            }
            return list;
        }

        /// <summary>体节被动机制: 战斗状态下按幕的节拍从随机体节渗出虚空魂雾 (带同屏上限)。</summary>
        private void EmitSegmentMiasma() {
            if (Main.netMode == NetmodeID.MultiplayerClient || CurrentState != AIState.ActCycle)
                return;
            int cadence = act >= 3 ? (enraged ? 55 : 65) : (act == 2 ? 85 : 110);
            if (Main.GameUpdateCount % (ulong)cadence != 0)
                return;
            if (CountProjectiles(ModContent.ProjectileType<AwakeningNetherMiasma>()) >= 10)
                return;
            var segs = GetSegments();
            if (segs.Count == 0)
                return;
            NPC seg = segs[Main.rand.Next(segs.Count)];
            SpawnMiasma(seg.Center, 1f);
        }

        private static int CountProjectiles(int type) {
            int count = 0;
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.type == type)
                    count++;
            }
            return count;
        }

        private void SpawnMiasma(Vector2 pos, float size) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountProjectiles(ModContent.ProjectileType<AwakeningNetherMiasma>()) >= 10)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<AwakeningNetherMiasma>(), GetProjectileDamage(25), 0f, Main.myPlayer, ai0: size);
        }

        private void SpawnWisp(Vector2 pos, Vector2 vel) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountProjectiles(ModContent.ProjectileType<AwakeningNetherSoulWisp>()) >= 14)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<AwakeningNetherSoulWisp>(), GetProjectileDamage(60), 0f, Main.myPlayer);
        }

        private void SpawnGate(Vector2 pos, bool lethal) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<AwakeningNetherRift>(), GetProjectileDamage(80), 0f, Main.myPlayer,
                ai0: act >= 3 ? 1 : 0, ai1: lethal ? 1 : 0);
        }

        private void SpawnSoulSatellites() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int count = enraged ? 6 : 5;
            int who = NPC.target;
            for (int i = 0; i < count; i++) {
                float a = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = Target.Center + a.ToRotationVector2() * 330f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<AwakeningNetherSoulSatellite>(), GetProjectileDamage(85), 0f, Main.myPlayer,
                    ai0: who, ai1: a);
            }
        }

        /// <summary>更新旋转和朝向</summary>
        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }
        }

        #region 弹幕发射

        /// <summary>盘旋齐射: 扇形虚空弹 (带 wind-up 与追踪截止的公平版)。</summary>
        private void ShootVoidBolts() {
            SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.35f, Volume = 1.1f }, NPC.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 最小发射距离: 贴脸不射 (反 telefrag, 亦是策略层)
            if (Target.Center.Distance(NPC.Center) < 260f)
                return;

            int damage = GetProjectileDamage(90);
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = act >= 3 ? 7 : (act == 2 ? 5 : 3);
            float spread = act >= 2 ? 0.17f : 0.12f;

            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 direction = toPlayer.RotatedBy(angle);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + direction * 60f,
                    direction * 14f,
                    ModContent.ProjectileType<AwakeningNetherVoidBolt>(), damage, 0f, Main.myPlayer,
                    ai0: enraged ? 1 : 0, ai1: act >= 2 ? 1 : 0);
            }
        }

        /// <summary>灵魂风暴 — 一次性环形爆发 (16 连, 螺旋/直线交替)。</summary>
        private void ShootSoulStorm() {
            SoundEngine.PlaySound(SoundID.Item113 with { Pitch = 0.1f, Volume = 1.2f }, NPC.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = GetProjectileDamage(80);
            int count = enraged ? 20 : 16;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 direction = angle.ToRotationVector2();
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, direction * 10f,
                    ModContent.ProjectileType<AwakeningNetherSoulOrb>(), damage, 0f, Main.myPlayer,
                    ai0: i % 2, ai1: i % 3);
            }
        }

        /// <summary>获取弹幕伤害 (根据难度调整)。</summary>
        private int GetProjectileDamage(int baseDamage) {
            if (Main.masterMode)
                return (int)(baseDamage * 1.5f);
            if (Main.expertMode)
                return (int)(baseDamage * 1.25f);
            return baseDamage;
        }

        #endregion

        // ============================ 绘制 ============================

        /// <summary>
        /// 全屏 screenTarget 扭曲 (GenericWarp · rift/void) — 占本帧唯一全屏后处理名额。
        /// 虚空吞噬/入场/死亡优先 void (向心吸入); 裂隙门冲刺走 rift。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            bool useVoid = voidWarp > 0.04f;
            float intensity = useVoid ? voidWarp : riftWarp;
            if (intensity <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 center = warpCenter == Vector2.Zero ? NPC.Center : warpCenter;
            Vector2 centerUV = (center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            if (useVoid) {
                fx.Parameters["uRadius"]?.SetValue(0.75f);
                fx.Parameters["uWarpScale"]?.SetValue(1.5f);
                fx.Parameters["uChroma"]?.SetValue(0.8f);
                fx.Parameters["uRadialPull"]?.SetValue(0.9f);
                fx.Parameters["uMode"]?.SetValue(4f); // void
                fx.Parameters["uTint"]?.SetValue(new Vector4(AwakeningNetherHelper.VoidDarkPurple.ToVector3(), 0.55f));
            }
            else {
                fx.Parameters["uRadius"]?.SetValue(0.55f);
                fx.Parameters["uWarpScale"]?.SetValue(1.3f);
                fx.Parameters["uChroma"]?.SetValue(0.7f);
                fx.Parameters["uRadialPull"]?.SetValue(0.6f);
                fx.Parameters["uMode"]?.SetValue(3f); // rift
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.5f));
            }

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 速度门控残影拖尾 — 只在穿刺时亮起 (dressing 必须被速度门控)
            float speed = NPC.velocity.Length();
            float trailAlpha = Utils.GetLerpValue(20f, 44f, speed, true);
            if (trailAlpha > 0.05f) {
                Texture2D tex = TextureAssets.Npc[Type].Value;
                Vector2 origin = new(tex.Width / 2f, tex.Height * 0.4f);
                SpriteEffects fx = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                float drawRot = NPC.spriteDirection == -1 ? NPC.rotation - MathHelper.Pi : NPC.rotation;
                for (int i = 2; i < NPC.oldPos.Length; i += 2) {
                    if (NPC.oldPos[i] == Vector2.Zero)
                        continue;
                    float progress = 1f - i / (float)NPC.oldPos.Length;
                    Color tc = AwakeningNetherHelper.AwakeningPurple * (progress * 0.42f * trailAlpha);
                    tc.A = 0;
                    spriteBatch.Draw(tex, NPC.oldPos[i] + NPC.Size / 2 - screenPos, null, tc,
                        drawRot, origin, NPC.scale * (0.7f + progress * 0.3f), fx, 0);
                }
            }

            DrawTelegraphs();

            // 主体经基类绘制 (脊波偏移 + 单层辉光 + SegmentAlpha)
            return base.PreDraw(spriteBatch, screenPos, drawColor);
        }

        /// <summary>穿刺线 / 走廊线预警 (BeamGrad, 红=致命契约)。</summary>
        private void DrawTelegraphs() {
            if (CurrentState == AIState.ActCycle && CurrentMove == Move.Pierce && moveStep == 0) {
                float t = MathHelper.Clamp(AttackTimer / 36f, 0f, 1f);
                Vector2 start = NPC.Center + dashDir * 40f;
                Vector2 end = NPC.Center + dashDir * 1500f;
                Color edge = Color.Lerp(TelegraphColors.NetherViolet, TelegraphColors.Lethal, t);
                ACMShaders.DrawBeam(start, end, 2.5f + t * 4f,
                    Color.Lerp(edge, Color.White, 0.3f), edge,
                    0.3f + 0.6f * t, flowSpeed: 2.4f, flowScale: 2.2f);
            }
            else if (CurrentState == AIState.ActCycle && CurrentMove == Move.BreathCorridor && moveStep == 0 && AttackTimer > 6) {
                float t = MathHelper.Clamp(AttackTimer / 45f, 0f, 1f);
                Vector2 start = new(laneCenterX - 620f, laneY);
                Vector2 end = new(laneCenterX + 620f, laneY);
                Color edge = Color.Lerp(TelegraphColors.GhostGreen, TelegraphColors.Lethal, t * 0.85f);
                ACMShaders.DrawBeam(start, end, 3f + t * 5f,
                    Color.Lerp(edge, Color.White, 0.25f), edge,
                    0.25f + 0.5f * t, flowSpeed: 1.8f, flowScale: 2.6f);
            }
            else if (CurrentState == AIState.ActCycle && CurrentMove == Move.SpineWhip) {
                // 脊柱能量流: 甩鞭时头部→前段体节的一条流光, 标示波源
                float glow = MathHelper.Clamp(MathF.Abs(SpringOffset) / 20f, 0f, 1f);
                if (glow > 0.1f) {
                    Vector2 back = NPC.Center - HeadingDir * 320f;
                    ACMShaders.DrawBeam(NPC.Center, back, 6f * glow,
                        TelegraphColors.GhostGreen with { A = 190 },
                        AwakeningNetherHelper.VoidDarkPurple with { A = 110 },
                        glow * 0.8f, flowSpeed: 3f, flowScale: 1.8f);
                }
            }
        }
    }
}

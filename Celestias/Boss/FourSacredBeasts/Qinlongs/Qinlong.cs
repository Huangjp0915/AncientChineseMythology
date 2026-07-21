using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items;
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

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙 (V3) — 东方·木风雷·苍龙降世。继承四圣兽共享骨架 <see cref="SacredBeastBase"/>。
    ///
    /// V3 重做核心 (机制保留, 动作/视觉重写, 见 Docs/BossRedo/FourSacredBeasts.md §4.1/§5.1):
    ///  · <b>程序化蛇身</b>: 位置历史环形缓冲 → 固定弧长重采样 14 段, 叠加速度挂钩的行进正弦波
    ///    (快=拉直 / 慢=大摆), 双层 TriangleStrip + QinglongDragonRibbon 鳞纹着色器。身体纯视觉不判伤。
    ///  · <b>龙跃 Dragon Lunge</b> (新签名机动): pow(t,8) late-snap 反向蓄势 → velocity set 72px/f
    ///    爆发 11 帧 → ×0.68/f 硬刹 + 甩尾风刃。接触伤害仅冲刺速度 &gt;30px/f 时生效 (CanHitPlayer)。
    ///  · <b>书法笔画运动</b>: 8 字升腾节点间速度 0.35→1.6 倍调制, 盘龙环绕角速度 0.03→0.08 缓入缓出,
    ///    巡游从 Lerp 悬停改为蛇形游曳。
    ///  · <b>三大演出</b>: 入场「穿云见龙」(背景龙影三掠→高速破入→仰首长吟) / P2 劈雷通电 /
    ///    P3 衔尾聚风 / 死亡「化雨升天」(CheckDead 拦截 → 螺旋盘升 + 递进落雷 → StrikeInstantKill)。
    ///  · 既有机制保留: 风刃扇 / 雷柱阵安全缝 / 盘龙锁径 / 天气抉择 / 风域天罚 / 觉醒预兆。
    /// </summary>
    [AutoloadBossHead]
    public class Qinlong : SacredBeastBase
    {
        #region 五行身份 / 阈值

        public override SacredElement Element => SacredElement.Wood;
        public override string SkyName => QinglongSky.SkyName;
        // 阶段阈值沿用骨架默认 (P2=0.60 / P3=0.30)。

        #endregion

        #region 状态枚举

        public enum QlState
        {
            Intro,
            Patrol,             // 巡游枢纽 (蛇形游曳, 零弹幕)
            WindBladeFan,       // 风刃扇 (telegraph 锥)
            ThunderColumns,     // 天罚雷柱阵 (带安全缝)
            DragonCoil,         // 盘龙锁径 set-piece
            AzureAscent,        // 苍龙升腾 8 字 (节点出招)
            WeatherDeck,        // 天气抉择 (承诺 Wind/Storm 窗口)
            StormfieldJudgment, // 风域天罚 (招牌)
            AwakeningForeshadow,// §5.7 觉醒预兆
            PhaseTransition2,   // 相变: 天劈巨雷·龙身通电
            PhaseTransition3,   // 相变: 衔尾盘环·聚风爆发
            DragonLunge,        // 龙跃 (V3 新签名机动)
            Death               // 死亡演出「化雨升天」
        }

        public QlState State {
            get => (QlState)RawState;
            set => RawState = (int)value;
        }

        #endregion

        #region 字段

        // ---- 同步逻辑字段 (SendExtraAI/ReceiveExtraAI 两端顺序一致) ----
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool didAwaken;
        private int weatherMode;        // 0=中性 1=风 2=雷暴
        private int weatherTimer;       // 当前天气剩余 tick
        private int weatherCommitCount; // 确定性 Wind/Storm 交替
        private bool deathAnimStarted;  // 死亡演出已接管 (CheckDead 拦截标记)
        private int lungeIndex;         // 龙跃当前段序号
        private Vector2 lungeTarget;    // 龙跃锁定点 (锁定帧冻结, 服务器广播矫正)

        // ---- 本地视觉/瞬态 (无需同步; 漂移由位置同步自然矫正) ----
        private float glowIntensity = 1f;
        private float weatherFlash;
        private Vector2 setpieceAnchor;
        private int lastNode = -1;
        private bool spawnedSetpiece;
        private float thetaAccum;       // 8 字角度累积 (速度调制用)
        private float coilAngle;        // 盘龙/自绕/死亡螺旋角度累积
        private float electrify;        // 龙身通电强度 (ribbon uElectrify)
        private float bodyFade = 1f;    // 蛇身覆盖度 (死亡化雨渐隐)
        private float lungeCompress;    // 龙跃蓄势时蛇身 S 波压缩 (0~1)
        private Vector2 lungeStart;     // 本段冲刺起点 (行程栓绳)

        // ---- 程序化蛇身 (纯客户端视觉) ----
        private const int HistoryLen = 300;
        private const int BodyNodeCount = 15;   // 头 + 14 段
        private const float SegLen = 46f;       // 固定重采样弧长
        private Vector2[] posHistory;           // 头位置历史环形缓冲
        private int historyHead;
        private int historyCount;
        private Vector2[] bodyNodes;            // 重采样脊线
        private Vector2[] renderNodes;          // 叠加波动后的渲染脊线

        // ---- 天幕落雷 decal (客户端演出队列) ----
        private struct SkyBolt
        {
            public Vector2 From, To;
            public float Seed;
            public int Life, MaxLife;
        }
        private readonly SkyBolt[] skyBolts = new SkyBolt[3];
        private int skyBoltCursor;

        // 供 QinglongSky / 天气滤镜读取的本帧快照
        internal static float s_weatherIntensity;
        internal static int s_weatherMode;
        internal static float s_galeIntensity;  // QinglongGale 风幕强度
        internal static float s_galeAngle;      // 风幕方向 (弧度)

        private static readonly Vector2[] DomainOffsets = { new(-470f, -20f), new(470f, -20f), new(0f, -300f) };

        // 死亡演出递进落雷帧表 (间隔 40→10 手调递减, pitch 递升)
        private static readonly int[] DeathStrikeFrames = { 24, 64, 98, 126, 148, 165, 178, 188 };

        private bool Server => Main.netMode != NetmodeID.MultiplayerClient;

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 160;
            NPC.height = 160;
            NPC.damage = 220;
            NPC.defense = 80;
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

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 灵材 + AzureTorrentBlades 武器桥 (保留 V1 掉落契约)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<QingLongSpirit>(), 1, 6, 10));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<AzureTorrentBlades>(),
                ModContent.ItemType<WindserpentDao>(),
                ModContent.ItemType<ThunderclapLongbow>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            ResetAllRotations();
            weatherMode = 0;
            weatherTimer = 0;
            weatherCommitCount = 0;
            didAwaken = false;
            didPhase2Transition = false;
            didPhase3Transition = false;
            deathAnimStarted = false;
            lungeIndex = 0;
            GoTo(QlState.Intro);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            SendSacredBeastAI(writer);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didAwaken);
            writer.Write(weatherMode);
            writer.Write(weatherTimer);
            writer.Write(weatherCommitCount);
            writer.Write(deathAnimStarted);
            writer.Write(lungeIndex);
            writer.WriteVector2(lungeTarget);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ReceiveSacredBeastAI(reader);
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didAwaken = reader.ReadBoolean();
            weatherMode = reader.ReadInt32();
            weatherTimer = reader.ReadInt32();
            weatherCommitCount = reader.ReadInt32();
            deathAnimStarted = reader.ReadBoolean();
            lungeIndex = reader.ReadInt32();
            lungeTarget = reader.ReadVector2();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            if (State == QlState.Death)
                return false; // 死亡演出中隐藏血条 (1 HP 悬挂无意义)
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>接触伤害只在龙跃爆发 + 速度 &gt;30px/f 时生效 (伤害窗口与视觉严格对齐, §6.1)。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
            => State == QlState.DragonLunge && InStrike && NPC.velocity.Length() > 30f;

        /// <summary>
        /// 死亡演出「化雨升天」拦截: 首次致死 → 清弹/无敌/伤害归零, 转入 Death 状态机播放 (~200f),
        /// 结束后由 AI 服务器端 <see cref="NPC.StrikeInstantKill"/> 真实死亡 (掉落/downed 照常走 OnKill)。
        /// </summary>
        public override bool CheckDead() {
            if (!deathAnimStarted) {
                deathAnimStarted = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                ClearHostileProjectiles();
                GoTo(QlState.Death);
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        /// <summary>相变/死亡清弹 (公平阀门): 服务器端抹掉全场敌意弹幕。</summary>
        private void ClearHostileProjectiles() {
            if (!Server)
                return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.hostile && p.damage > 0)
                    p.Kill();
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedQinlong = true;
            s_weatherIntensity = 0f;
            s_weatherMode = 0;
            s_galeIntensity = 0f;
            ACMUtils.AddScreenShake(14f); // §6.2 死亡定格 (统一预算, 入场/死亡 ≤16)
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            // 死亡演出自持: 不依赖有效目标 (玩家全灭也要演完并结算)
            if (State == QlState.Death) {
                GlobalTime += 1f / 60f;
                PhaseTimer++;
                AttackTimer++;
                RunDeath();
                UpdateRotation();
                UpdateWeather();
                UpdateBodyChain();
                Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.9f, 0.6f) * (glowIntensity + electrify));
                return;
            }

            if (!RunStandardPrologue(out Player target))
                return;

            CheckPhaseTransition();

            switch (State) {
                case QlState.Intro: RunIntro(target); break;
                case QlState.Patrol: RunPatrol(target); break;
                case QlState.WindBladeFan: RunWindBladeFan(target); break;
                case QlState.ThunderColumns: RunThunderColumns(target); break;
                case QlState.DragonCoil: RunDragonCoil(target); break;
                case QlState.AzureAscent: RunAzureAscent(target); break;
                case QlState.WeatherDeck: RunWeatherDeck(target); break;
                case QlState.StormfieldJudgment: RunStormfield(target); break;
                case QlState.AwakeningForeshadow: RunAwakening(target); break;
                case QlState.PhaseTransition2: RunPhaseTransition2(target); break;
                case QlState.PhaseTransition3: RunPhaseTransition3(target); break;
                case QlState.DragonLunge: RunDragonLunge(target); break;
            }

            UpdateRotation();
            UpdateWeather();
            UpdateBodyChain();
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.9f, 0.4f) * glowIntensity);
        }

        /// <summary>切到新状态 + 清掉本地每招瞬态标记。</summary>
        private void GoTo(QlState s) {
            lastNode = -1;
            spawnedSetpiece = false;
            thetaAccum = 0f;
            coilAngle = 0f;
            lungeCompress = 0f;
            lungeIndex = 0;
            TransitionToState((int)s);
        }

        /// <summary>
        /// 头部朝向: WrapAngle 最短路径插值 (修复跨 ±π 整圈打转 bug), 插值速率随速度上抬 —
        /// 高速甩头快、低速摆头慢, 读得出重量。
        /// </summary>
        private void UpdateRotation() {
            float speed = NPC.velocity.Length();
            if (speed > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                float lerp = MathHelper.Clamp(0.08f + speed * 0.004f, 0.08f, 0.30f);
                float diff = MathHelper.WrapAngle(targetRot - NPC.rotation);
                NPC.rotation = MathHelper.WrapAngle(NPC.rotation + diff * lerp);
                if (MathF.Abs(NPC.velocity.X) > 2f)
                    NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateWeather() {
            if (weatherTimer > 0)
                weatherTimer--;
            else
                weatherMode = 0;

            s_weatherMode = weatherMode;
            float targetI = weatherMode != 0 ? MathHelper.Clamp(weatherTimer / 90f, 0f, 1f) * 0.32f : 0f;
            s_weatherIntensity = MathHelper.Lerp(s_weatherIntensity, targetI, 0.05f);

            // QinglongGale 风幕 overlay 目标强度 (风域天罚/雷暴/死亡化雨/入场起风)
            float targetGale = 0f;
            if (State == QlState.StormfieldJudgment && !InWindup)
                targetGale = 0.8f;
            else if (State == QlState.Death)
                targetGale = 0.30f + 0.35f * bodyFade;
            else if (State == QlState.Intro)
                targetGale = 0.28f * MathHelper.Clamp(PhaseTimer / 150f, 0f, 1f);
            else if (weatherMode == 1)
                targetGale = 0.50f;
            else if (weatherMode == 2)
                targetGale = 0.30f;
            s_galeIntensity = MathHelper.Lerp(s_galeIntensity, targetGale, 0.045f);
            // 风向: 近水平缓摆 (周期性换边)
            float baseAng = MathF.Sin(GlobalTime * 0.11f) > 0f ? 0f : MathHelper.Pi;
            s_galeAngle = baseAng + MathF.Sin(GlobalTime * 0.7f) * 0.16f + 0.10f;

            if (weatherFlash > 0f)
                weatherFlash -= 0.045f;
            glowIntensity = MathHelper.Lerp(glowIntensity, IsPhase3 ? 1.8f : 1f, 0.03f);

            // 通电基线: P2/P3 常驻微通电, 事件脉冲 (相变劈雷/死亡) 叠加后指数衰回
            float elecBase = State == QlState.Death ? 0.5f : IsPhase3 ? 0.16f : IsPhase2 ? 0.10f : 0f;
            electrify = MathF.Max(electrify * 0.965f, elecBase);

            // 雷暴天气氛围余弹 (QinglongThunderBolt ambient 模式, 纯视觉零伤害)
            if (Server && weatherMode == 2 && weatherTimer % 90 == 30 && State != QlState.Death && NPC.target >= 0) {
                Player t = Main.player[NPC.target];
                if (t.active && !t.dead)
                    SpawnAmbientBolt(t.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -1000f));
            }
        }

        private void CheckPhaseTransition() {
            if (State is QlState.Intro or QlState.PhaseTransition2 or QlState.PhaseTransition3 or QlState.AwakeningForeshadow or QlState.Death)
                return;

            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                GoTo(QlState.PhaseTransition2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                GoTo(QlState.PhaseTransition3);
            }
        }

        #endregion

        #region 确定性轮替 (替代随机 hub)

        private static readonly int[] P1Rotation = {
            (int)QlState.WindBladeFan, (int)QlState.DragonLunge, (int)QlState.ThunderColumns,
            (int)QlState.DragonCoil, (int)QlState.AzureAscent
        };
        private static readonly int[] P2Rotation = {
            (int)QlState.WeatherDeck, (int)QlState.DragonLunge, (int)QlState.AzureAscent,
            (int)QlState.ThunderColumns, (int)QlState.WindBladeFan, (int)QlState.DragonCoil
        };
        private static readonly int[] P3Rotation = {
            (int)QlState.WeatherDeck, (int)QlState.StormfieldJudgment, (int)QlState.DragonLunge,
            (int)QlState.AzureAscent, (int)QlState.ThunderColumns, (int)QlState.DragonCoil
        };

        protected override int[] GetPhaseRotation(int phaseTier) => phaseTier switch {
            1 => P1Rotation,
            2 => P2Rotation,
            _ => P3Rotation
        };

        #endregion

        #region 程序化蛇身 (客户端)

        /// <summary>每帧记录头位置 → 固定弧长重采样 → 叠加行进正弦波。服务器早退, 纯视觉。</summary>
        private void UpdateBodyChain() {
            if (Main.dedServ)
                return;

            if (posHistory == null) {
                posHistory = new Vector2[HistoryLen];
                bodyNodes = new Vector2[BodyNodeCount];
                renderNodes = new Vector2[BodyNodeCount];
                ResetBodyChain(-NPC.rotation.ToRotationVector2());
            }

            // 移动 ≥2px 才推进游标 (悬停时原地覆写, 防止历史堆积同点)
            Vector2 last = posHistory[historyHead];
            if (Vector2.DistanceSquared(last, NPC.Center) > 4f) {
                historyHead = (historyHead + 1) % HistoryLen;
                posHistory[historyHead] = NPC.Center;
                if (historyCount < HistoryLen)
                    historyCount++;
            }
            else {
                posHistory[historyHead] = NPC.Center;
            }

            ResampleBody();
            BuildRenderNodes();
        }

        /// <summary>瞬移/入场后重置身体: 历史缓冲填成头后方直线, 避免蛇身横跨半屏拉丝。</summary>
        private void ResetBodyChain(Vector2 backDir) {
            if (posHistory == null)
                return;
            if (backDir.LengthSquared() < 0.01f)
                backDir = -Vector2.UnitX;
            historyHead = 0;
            historyCount = HistoryLen;
            for (int i = 0; i < HistoryLen; i++) {
                int idx = historyHead - i;
                while (idx < 0)
                    idx += HistoryLen;
                posHistory[idx] = NPC.Center + backDir * (i * 8f);
            }
            ResampleBody();
            BuildRenderNodes();
        }

        /// <summary>沿历史缓冲按固定弧长 SegLen 重采样出脊线节点。</summary>
        private void ResampleBody() {
            bodyNodes[0] = NPC.Center;
            int node = 1;
            float need = SegLen;
            Vector2 cur = NPC.Center;
            int idx = historyHead;
            int walked = 0;

            while (node < BodyNodeCount && walked < historyCount - 1) {
                int prev = idx - 1;
                if (prev < 0)
                    prev += HistoryLen;
                Vector2 nxt = posHistory[prev];
                float d = Vector2.Distance(cur, nxt);
                while (d >= need && node < BodyNodeCount) {
                    cur += (nxt - cur) * (need / d);
                    d = Vector2.Distance(cur, nxt);
                    bodyNodes[node++] = cur;
                    need = SegLen;
                }
                if (node >= BodyNodeCount)
                    break;
                need -= d;
                cur = nxt;
                idx = prev;
                walked++;
            }

            // 历史弧长不足: 沿末段方向直线补齐 (刚重置/低速时)
            if (node < BodyNodeCount) {
                Vector2 dir = node >= 2
                    ? (bodyNodes[node - 1] - bodyNodes[node - 2]).SafeNormalize(Vector2.UnitX)
                    : -NPC.rotation.ToRotationVector2();
                for (; node < BodyNodeCount; node++)
                    bodyNodes[node] = bodyNodes[node - 1] + dir * SegLen;
            }
        }

        /// <summary>
        /// 叠加沿身正弦波: 波幅与速度挂钩 (快=拉直 / 慢=大摆), 头端钉扎尾端放大;
        /// 龙跃蓄势时波幅压缩 + 频率上抬 (S 收紧的"深吸一口气")。
        /// </summary>
        private void BuildRenderNodes() {
            float speed = NPC.velocity.Length();
            float speedFac = MathHelper.Clamp(speed / 26f, 0f, 1f);
            float amp = MathHelper.Lerp(26f, 4f, speedFac);
            float freq = 1.9f;
            if (lungeCompress > 0f) {
                amp *= 1f - 0.65f * lungeCompress;
                freq += lungeCompress * 1.6f;
            }
            float tphase = GlobalTime * (3.2f + speed * 0.09f);

            renderNodes[0] = bodyNodes[0];
            for (int i = 1; i < BodyNodeCount; i++) {
                float s = i / (float)(BodyNodeCount - 1);
                Vector2 tang = (bodyNodes[Math.Min(i + 1, BodyNodeCount - 1)] - bodyNodes[Math.Max(i - 1, 0)]).SafeNormalize(Vector2.UnitX);
                Vector2 nrm = new(-tang.Y, tang.X);
                // 包络: 头端钉扎 (跟随实际轨迹), 越往尾摆幅越大
                float envelope = MathF.Min(1f, s * 3f) * (0.3f + 0.7f * s);
                float wave = MathF.Sin(s * freq * MathHelper.TwoPi - tphase) * amp * envelope;
                renderNodes[i] = bodyNodes[i] + nrm * wave;
            }
        }

        #endregion

        #region 入场「穿云见龙」/ 巡游

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 1) {
                // 隐身挂在高空场外, 天幕先起风; 破入方向由 whoAmI 决定 (确定性)
                NPC.Center = target.Center + new Vector2(0, -860);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.55f, Pitch = -0.6f }, target.Center);
            }

            // 背景龙影三掠, 每掠一声远雷渐近 (演出绘制见 DrawIntroSilhouettes)
            if (PhaseTimer == 20 || PhaseTimer == 60 || PhaseTimer == 100) {
                float near = (PhaseTimer - 20) / 80f;
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.45f + 0.35f * near, Pitch = -0.6f + 0.3f * near }, target.Center);
            }

            if (PhaseTimer < 130) {
                NPC.Opacity = 0f;
                NPC.Center = target.Center + new Vector2(0, -860);
                NPC.velocity = Vector2.Zero;
            }
            else if (PhaseTimer == 130) {
                // 第 3 掠之后: 从屏幕侧面高速破入 (80px/f)
                int side = NPC.whoAmI % 2 == 0 ? 1 : -1;
                Vector2 entry = target.Center + new Vector2(-side * 1150f, -430f);
                Vector2 dest = target.Center + new Vector2(0, -330);
                NPC.Center = entry;
                NPC.Opacity = 1f;
                NPC.velocity = (dest - entry).SafeNormalize(Vector2.UnitX) * 80f;
                NPC.rotation = NPC.velocity.ToRotation();
                ResetBodyChain(-NPC.velocity.SafeNormalize(Vector2.UnitX));
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.2f }, entry);
            }
            else if (PhaseTimer < 152) {
                // 头顶硬刹: 蛇身靠历史缓冲自然甩尾归位
                Vector2 dest = target.Center + new Vector2(0, -330);
                if (PhaseTimer >= 144 || Vector2.DistanceSquared(NPC.Center, dest) < 140f * 140f)
                    NPC.velocity *= 0.60f;
            }
            else if (PhaseTimer == 152) {
                // 仰首长吟 + 全屏翠光
                NPC.velocity = new Vector2(0, -3.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.25f, Volume = 1.3f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(14f);
                weatherFlash = 1.4f;
                QinglongSky.FlashLightning(0.9f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 36; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 90, default, 2.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(11f, 11f);
                    }
                }
            }
            else {
                NPC.velocity *= 0.90f;
            }

            if (PhaseTimer >= 176) {
                NPC.dontTakeDamage = false;
                ResetAllRotations();
                GoTo(QlState.Patrol);
            }
        }

        /// <summary>巡游枢纽: 蛇形游曳 (移动锚点绕玩家 + 垂直速度方向的行波推力), 保持零弹幕职责。</summary>
        private void RunPatrol(Player target) {
            float t = PhaseTimer + NPC.whoAmI * 37f;
            Vector2 anchor = target.Center + new Vector2(
                MathF.Cos(t * 0.020f) * 430f,
                -310f + MathF.Sin(t * 0.041f) * 110f);
            Vector2 desired = (anchor - NPC.Center) * 0.045f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.085f);

            // 游曳摆动: 沿速度法向的正弦推力 → 身体波动与头部轨迹同步
            Vector2 dir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            NPC.velocity += new Vector2(-dir.Y, dir.X) * MathF.Sin(t * 0.13f) * 0.85f;

            if (PhaseTimer >= 46) {
                // §5.7 觉醒预兆: 三阶段低血一次性切入终曲
                if (IsPhase3 && NPC.life < NPC.lifeMax * 0.12f && !didAwaken) {
                    GoTo(QlState.AwakeningForeshadow);
                    return;
                }
                int next = NextAttack(PhaseTier);
                GoTo(next < 0 ? QlState.WindBladeFan : (QlState)next);
            }
        }

        #endregion

        #region 攻击: 龙跃 Dragon Lunge (V3 新签名机动)

        private int LungeWindupTicks => lungeIndex == 0 ? 42 : 34; // 后续段并入 24f 重定位
        private const int LungeLockLead = 14;   // 末 14 帧红线锁定
        private const int LungeStrikeTicks = 11;
        private const int LungeRecoverTicks = 26;
        private const float LungeMaxTravel = 900f; // 单段行程栓绳

        private void RunDragonLunge(Player target) {
            int maxLunges = IsPhase2 ? 3 : 2; // P1 两连, P2+ 三连
            int windupTicks = LungeWindupTicks;

            switch (Telegraph) {
                case TelegraphPhase.Windup: {
                    float p = MathHelper.Clamp(AttackTimer / (float)windupTicks, 0f, 1f);
                    bool locked = AttackTimer >= windupTicks - LungeLockLead;

                    if (AttackTimer == 1)
                        setpieceAnchor = NPC.Center;

                    if (!locked) {
                        // 追踪期: 滑向玩家侧上方对位点, 持续预测目标
                        lungeTarget = target.Center + target.velocity * 12f;
                        Vector2 pre = target.Center + new Vector2(MathF.Sign(NPC.Center.X - target.Center.X + 0.01f) * 420f, -240f);
                        setpieceAnchor = Vector2.Lerp(setpieceAnchor, pre, 0.06f);
                    }
                    else if (AttackTimer == windupTicks - LungeLockLead) {
                        // 锁定帧: 冻结目标 (服务器广播矫正) + 预警 beep (固定 14f 提前量)
                        lungeTarget = target.Center + target.velocity * 12f;
                        if (Server)
                            NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.55f, Volume = 0.9f }, NPC.Center);
                    }

                    // 头部沿反方向 pow(t,8) late-snap 后拉 ~300px (什么都不动…猛然后吸)
                    Vector2 awayDir = (setpieceAnchor - lungeTarget).SafeNormalize(Vector2.UnitX);
                    Vector2 dest = setpieceAnchor + awayDir * MathF.Pow(p, 8f) * 300f;
                    NPC.velocity = (dest - NPC.Center) * 0.30f;

                    lungeCompress = p; // 蛇身 S 波幅压缩蓄势 (视觉)

                    if (!Main.dedServ && locked && Main.rand.NextBool(2)) {
                        // 锁定期沿冲刺线聚风粒子
                        Vector2 ldir = (lungeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                        Vector2 pos = NPC.Center + ldir * Main.rand.NextFloat(80f, 500f) + Main.rand.NextVector2Circular(26f, 26f);
                        Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.GreenTorch, 0, 0, 120, default, 1.4f);
                        d.noGravity = true;
                        d.velocity = -ldir * 4f;
                    }

                    if (AttackTimer >= windupTicks)
                        SetTelegraph(TelegraphPhase.Strike);
                    break;
                }
                case TelegraphPhase.Strike: {
                    if (AttackTimer == 1) {
                        // 爆发: velocity 直接 set (launch is a set, not a ramp)
                        Vector2 dir = (lungeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.velocity = dir * 72f;
                        lungeStart = NPC.Center;
                        lungeCompress = 0f;
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = 0.15f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.4f }, NPC.Center);
                        ACMUtils.AddScreenShake(6f);
                    }
                    else {
                        // 每帧 ×1.02 复合加速 + 微曲穿越 (≤0.006 rad/f 朝目标缓旋)
                        NPC.velocity *= 1.02f;
                        float want = (lungeTarget - NPC.Center).ToRotation();
                        float cur = NPC.velocity.ToRotation();
                        float dd = MathHelper.WrapAngle(want - cur);
                        NPC.velocity = NPC.velocity.RotatedBy(MathHelper.Clamp(dd, -0.006f, 0.006f));
                    }

                    // 行程栓绳: 单段 ≤900px, 达标提前进入收招 (不给死航程)
                    if (AttackTimer >= LungeStrikeTicks || Vector2.DistanceSquared(lungeStart, NPC.Center) > LungeMaxTravel * LungeMaxTravel)
                        SetTelegraph(TelegraphPhase.Recover);
                    break;
                }
                default: {
                    // 收招: ×0.68/f 硬刹 + 首帧甩尾放 3 枚风刃
                    if (AttackTimer == 1)
                        FireTailBlades(target);
                    NPC.velocity *= 0.68f;
                    lungeCompress = 0f;

                    if (AttackTimer >= LungeRecoverTicks) {
                        lungeIndex++;
                        if (lungeIndex >= maxLunges) {
                            GoTo(QlState.Patrol);
                        }
                        else {
                            SetTelegraph(TelegraphPhase.Windup); // 段间重定位并入下一段前摇
                            if (Server)
                                NPC.netUpdate = true;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>甩尾风刃: 从尾部方向扇出 3 枚 (服务器权威, 尾位取速度反向近似, 不依赖客户端蛇身)。</summary>
        private void FireTailBlades(Player target) {
            if (!Server)
                return;
            Vector2 tailPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.UnitX) * 240f;
            Vector2 baseDir = (target.Center - tailPos).SafeNormalize(Vector2.UnitY);
            float spread = MathHelper.ToRadians(17f);
            for (int i = -1; i <= 1; i++)
                NewWindBlade(tailPos, baseDir.RotatedBy(i * spread) * 13f, NPC.damage / 4);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, tailPos);
        }

        #endregion

        #region 攻击: 风刃扇

        private void RunWindBladeFan(Player target) {
            // 悬停也保持游曳感 (小幅波动而非死 Lerp)
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GlobalTime * 1.6f) * 140f, -360f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.06f, 0.1f);
            Vector2 sdir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            NPC.velocity += new Vector2(-sdir.Y, sdir.X) * MathF.Sin(PhaseTimer * 0.16f) * 0.4f;

            bool done = AdvanceTelegraph(30, 66, 22);

            if (InStrike) {
                int interval = Main.expertMode ? 14 : 18;
                if (AttackTimer % interval == 1)
                    FireWindFan(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void FireWindFan(Player target) {
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = 5 + (weatherMode == 1 ? 2 : 0);
            float spread = MathHelper.ToRadians(9f);
            for (int i = -count / 2; i <= count / 2; i++)
                NewWindBlade(NPC.Center, dir.RotatedBy(i * spread) * 15f, NPC.damage / 4);

            if (weatherMode == 2)
                SpawnThunderColumn(target.Center, ThreatTier.Medium);

            // 发射后坐: 头部反冲 ~6px (mass is reaction)
            NPC.velocity -= dir * 2.6f;

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
        }

        #endregion

        #region 攻击: 天罚雷柱阵 (安全缝)

        private void RunThunderColumns(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -440);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.08f);
            Vector2 sdir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            NPC.velocity += new Vector2(-sdir.Y, sdir.X) * MathF.Sin(PhaseTimer * 0.12f) * 0.35f;

            bool done = AdvanceTelegraph(10, 120, 26);

            if (InStrike) {
                const int volleyInterval = 38;
                if (AttackTimer % volleyInterval == 1)
                    FireColumnVolley(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void FireColumnVolley(Player target) {
            int cols = 5 + (weatherMode == 2 ? 2 : 0);
            float span = 820f;
            int volley = (int)(AttackTimer / 38);
            int safe = volley % cols; // 旋转的安全缝, 玩家有可读逃生位
            for (int i = 0; i < cols; i++) {
                if (i == safe)
                    continue;
                float x = MathHelper.Lerp(-span, span, cols == 1 ? 0.5f : (float)i / (cols - 1));
                SpawnThunderColumn(new Vector2(target.Center.X + x, target.Center.Y), ThreatTier.Medium);
            }
            // 唤雷仰身: 每轮上扬一拍 (蓄力语言) + 天幕预闪
            NPC.velocity.Y -= 3.2f;
            QinglongSky.FlashLightning(0.22f);
        }

        #endregion

        #region 攻击: 盘龙锁径 set-piece

        private void RunDragonCoil(Player target) {
            const float radius = 360f;
            const int nodePeriod = 40;

            if (PhaseTimer == 1)
                coilAngle = (NPC.Center - target.Center).ToRotation();

            // 环绕角速度按节点周期缓入缓出 0.03→0.08 rad/f: 逼近节点加速、放环后回落
            float ph = (PhaseTimer % nodePeriod) / (float)nodePeriod;
            float angSpeed = MathHelper.Lerp(0.03f, 0.08f, ACMUtils.SineInOut(ph));
            coilAngle += angSpeed;

            Vector2 orbit = target.Center + coilAngle.ToRotationVector2() * radius + new Vector2(0, -40);
            NPC.velocity = (orbit - NPC.Center) * 0.2f;

            // 节点向内放风刃环, 在「龙所在角度」留安全缝
            if ((int)PhaseTimer % nodePeriod == nodePeriod - 1) {
                FireCoilRing(target, coilAngle, radius);
                // 放环反冲: 身体向外弹一拍
                NPC.velocity += (NPC.Center - target.Center).SafeNormalize(Vector2.Zero) * 7f;
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
                d.velocity = NPC.velocity * 0.1f;
            }

            if (PhaseTimer > 312)
                GoTo(QlState.Patrol);
        }

        private void FireCoilRing(Player target, float bossAngle, float radius) {
            int count = 18;
            float gapHalf = MathHelper.ToRadians(34f);
            for (int i = 0; i < count; i++) {
                float a = MathHelper.TwoPi / count * i;
                if (MathF.Abs(MathHelper.WrapAngle(a - bossAngle)) < gapHalf)
                    continue; // 安全缝 = 龙所在处, 「站到龙那边」
                Vector2 pos = target.Center + a.ToRotationVector2() * radius;
                Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 7f;
                NewWindBlade(pos, vel, NPC.damage / 4);
            }
            SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
        }

        #endregion

        #region 攻击: 苍龙升腾 8 字 (节点出招)

        private void RunAzureAscent(Player target) {
            if (PhaseTimer == 1) {
                thetaAccum = 0f;
                lastNode = -1;
            }

            Vector2 anchor = target.Center + new Vector2(0, -220);

            // 节点间速度调制 0.35→1.6 倍: 慢顿(节点) - 快甩(中段) 的书法笔画感
            float nodePhase = (thetaAccum % MathHelper.PiOver2) / MathHelper.PiOver2;
            float bump = MathF.Sin(nodePhase * MathHelper.Pi);
            float mod = 0.35f + 1.25f * MathF.Pow(bump, 1.3f);
            thetaAccum += 0.05f * mod;
            float theta = thetaAccum;

            // Lemniscate of Gerono: 8 字
            Vector2 off = new(MathF.Cos(theta) * 420f, MathF.Sin(theta) * MathF.Cos(theta) * 600f);
            Vector2 dest = anchor + off;
            NPC.velocity = (dest - NPC.Center) * 0.25f;

            int node = (int)(theta / MathHelper.PiOver2);
            if (node > lastNode && PhaseTimer > 8) {
                lastNode = node;
                FireAscentNode(target); // 节点=慢速点出招, 节点间零弹幕
            }

            if (theta >= MathHelper.TwoPi * 2f || PhaseTimer > 720)
                GoTo(QlState.Patrol);
        }

        private void FireAscentNode(Player target) {
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int n = 4;
            float spread = MathHelper.ToRadians(11f);
            for (int i = -n / 2; i <= n / 2; i++)
                NewWindBlade(NPC.Center, dir.RotatedBy(i * spread) * 16f, NPC.damage / 4);

            if (weatherMode == 2 || IsPhase3)
                SpawnThunderColumn(target.Center, ThreatTier.Medium);

            // 发射后坐 (中量级)
            NPC.velocity -= dir * 4f;

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f }, NPC.Center);
        }

        #endregion

        #region 攻击: 天气抉择 Weather Deck

        private void RunWeatherDeck(Player target) {
            bool done = AdvanceTelegraph(64, 6, 24);

            if (InWindup) {
                // 蓄力: 龙身盘成圆自绕 (半径渐缩), 元素粒子收束
                if (AttackTimer == 1)
                    setpieceAnchor = NPC.Center + new Vector2(0, -40);
                float selfAng = AttackTimer * 0.115f;
                float r = MathHelper.Lerp(210f, 110f, MathHelper.Clamp(AttackTimer / 64f, 0f, 1f));
                Vector2 dest = setpieceAnchor + selfAng.ToRotationVector2() * r;
                NPC.velocity = (dest - NPC.Center) * 0.3f;
            }
            else {
                NPC.velocity *= 0.90f;
            }

            if (InStrike && !spawnedSetpiece) {
                spawnedSetpiece = true;
                CommitWeather();
            }

            if (!Main.dedServ && InWindup && PhaseTimer % 2 == 0 && AttackTimer < 58) {
                // 末 6 帧粒子骤停 = 承诺前静默
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * Main.rand.NextFloat(160, 320);
                int dustId = (weatherCommitCount % 2 == 0) ? DustID.GreenTorch : DustID.Electric;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustId, 0, 0, 120, default, 2f);
                d.noGravity = true;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void CommitWeather() {
            weatherMode = (weatherCommitCount % 2 == 0) ? 1 : 2;
            weatherCommitCount++;
            weatherTimer = 600;
            weatherFlash = 1f;
            glowIntensity = 1.7f;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
            ACMUtils.AddScreenShake(10f);

            if (weatherMode == 2) {
                // 雷暴承诺: 天空真闪电 + 劈向龙身的承诺雷 (纯演出)
                QinglongSky.FlashLightning(1f);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.1f }, NPC.Center);
                PushSkyBolt(NPC.Center - new Vector2(0, 1100), NPC.Center, 2.3f + weatherCommitCount * 0.7f, 14);
                electrify = MathF.Max(electrify, 0.8f);
            }

            if (Server)
                NPC.netUpdate = true;
        }

        #endregion

        #region 攻击: 风域天罚 Stormfield Judgment (招牌)

        private void RunStormfield(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -460);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.03f, 0.06f);
            Vector2 sdir = NPC.velocity.SafeNormalize(Vector2.UnitX);
            NPC.velocity += new Vector2(-sdir.Y, sdir.X) * MathF.Sin(PhaseTimer * 0.1f) * 0.3f;
            setpieceAnchor = target.Center;

            bool done = AdvanceTelegraph(78, 210, 26);

            // 前摇: 天幕转暗渐强 (风暴压境)
            if (InWindup)
                QinglongSky.DarkenSky(0.45f * MathHelper.Clamp(AttackTimer / 78f, 0f, 1f));

            if (InStrike) {
                if (!spawnedSetpiece) {
                    spawnedSetpiece = true;
                    SpawnWindDomains(target);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = -0.4f }, NPC.Center);
                    ACMUtils.AddScreenShake(11f);
                    weatherFlash = 1f;
                }
                const int interval = 30;
                if (AttackTimer % interval == 1)
                    RainGapColumns(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void SpawnWindDomains(Player target) {
            if (!Server)
                return;
            for (int i = 0; i < DomainOffsets.Length; i++) {
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center + DomainOffsets[i], Vector2.Zero,
                    ModContent.ProjectileType<QinglongWindDomain>(), 0, 0f, Main.myPlayer, 250f, i % 2 == 0 ? 1f : -1f);
                if (p >= 0 && p < Main.maxProjectiles)
                    Main.projectile[p].timeLeft = 200;
            }
        }

        private void RainGapColumns(Player target) {
            float[] xs = { -235f, 235f, 0f };
            int volley = (int)(AttackTimer / 30);
            float jx = xs[volley % xs.Length];
            SpawnThunderColumn(new Vector2(target.Center.X + jx, target.Center.Y), ThreatTier.Medium);
            if (IsPhase3)
                SpawnThunderColumn(new Vector2(target.Center.X - jx, target.Center.Y), ThreatTier.Medium);
        }

        #endregion

        #region 攻击: 觉醒预兆 (§5.7)

        private void RunAwakening(Player target) {
            didAwaken = true; // 立刻置位, 杜绝重复触发
            NPC.dontTakeDamage = true;
            Vector2 hover = target.Center + new Vector2(0, -320);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.1f);
            glowIntensity = MathHelper.Lerp(glowIntensity, 2.3f, 0.04f);
            QinglongSky.DarkenSky(0.5f * MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f));

            // 55 帧后粒子骤停 = 爆发前静默 (inhale before the scream)
            if (!Main.dedServ && PhaseTimer % 2 == 0 && PhaseTimer < 56) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * MathF.Max(80f, 520f - PhaseTimer * 3f);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.Electric, 0, 0, 60, default, 3f);
                d.noGravity = true;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 9f;
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.3f }, NPC.Center);
                ACMUtils.AddScreenShake(16f);
                weatherFlash = 1.4f;
                QinglongSky.FlashLightning(1.2f);
                PushSkyBolt(NPC.Center - new Vector2(0, 1100), NPC.Center, 6.1f, 16);
                electrify = 1f;
                weatherMode = 2;       // 终曲锁定雷暴天气
                weatherTimer = 1200;
                weatherCommitCount++;
                if (Server)
                    NPC.netUpdate = true;
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                ResetRotation(3);
                GoTo(QlState.StormfieldJudgment);
            }
        }

        #endregion

        #region 阶段过渡 (演出节拍)

        /// <summary>P2 相变 (~90f): 盘圆制动 → 天空瞬暗 → 分叉巨雷劈中龙身 → 通电 + 雷暴底色。</summary>
        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            if (PhaseTimer <= 30) {
                // 盘圆制动
                if (PhaseTimer == 1)
                    setpieceAnchor = NPC.Center;
                float ang = PhaseTimer * 0.12f;
                Vector2 dest = setpieceAnchor + ang.ToRotationVector2() * MathHelper.Lerp(150f, 90f, PhaseTimer / 30f);
                NPC.velocity = (dest - NPC.Center) * 0.25f;
            }
            else {
                NPC.velocity *= 0.90f;
            }

            // 天空瞬暗 (0.4s)
            if (PhaseTimer > 26 && PhaseTimer < 46)
                QinglongSky.DarkenSky(0.9f);

            if (PhaseTimer == 44) {
                // 分叉巨雷劈中龙身: 白闪 + 通电
                PushSkyBolt(NPC.Center - new Vector2(0, 1150), NPC.Center, 3.7f, 16);
                QinglongSky.FlashLightning(1.2f);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.4f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(11f);
                weatherFlash = 1.1f;
                electrify = 1f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 30; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Electric, 0, 0, 60, default, 2.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(12f, 12f);
                    }
                }
            }

            if (PhaseTimer >= 84) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.15f);
                ClearHostileProjectiles(); // 相变清弹 (公平阀门)
                ResetRotation(2);
                GoTo(QlState.Patrol);
            }
        }

        /// <summary>P3 相变 (~110f): 首尾相衔盘环 → 环心聚风 → 风环爆发清弹。</summary>
        private void RunPhaseTransition3(Player target) {
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 1) {
                setpieceAnchor = NPC.Center;
                coilAngle = 0f;
            }

            if (PhaseTimer <= 80) {
                // 衔尾蛇姿态: 头沿小圆轨盘绕 → 蛇身自然首尾相衔成环
                coilAngle += 0.13f;
                float r = PhaseTimer <= 58 ? 118f : MathHelper.Lerp(118f, 100f, (PhaseTimer - 58) / 22f);
                Vector2 dest = setpieceAnchor + coilAngle.ToRotationVector2() * r;
                NPC.velocity = (dest - NPC.Center) * 0.3f;
            }
            else {
                NPC.velocity *= 0.88f;
            }

            // 环心聚风 (58~80): 粒子向环心收束
            if (!Main.dedServ && PhaseTimer > 58 && PhaseTimer <= 80 && PhaseTimer % 2 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = setpieceAnchor + a.ToRotationVector2() * Main.rand.NextFloat(220f, 420f);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.GreenTorch, 0, 0, 90, default, 2.4f);
                d.noGravity = true;
                d.velocity = (setpieceAnchor - pos).SafeNormalize(Vector2.Zero) * 10f;
            }

            if (PhaseTimer == 80) {
                // 风环爆发: 清弹 + 冲击
                ClearHostileProjectiles();
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                weatherFlash = 1.2f;
                QinglongSky.FlashLightning(0.6f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 48; i++) {
                        float a = MathHelper.TwoPi / 48f * i;
                        Dust d = Dust.NewDustDirect(setpieceAnchor + a.ToRotationVector2() * 90f, 0, 0, DustID.GreenTorch, 0, 0, 60, default, 2.8f);
                        d.noGravity = true;
                        d.velocity = a.ToRotationVector2() * 13f;
                    }
                }
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.25f);
                glowIntensity = 1.8f;
                ResetRotation(3);
                GoTo(QlState.Patrol);
            }
        }

        #endregion

        #region 死亡演出「化雨升天」

        /// <summary>
        /// ~205f: 螺旋盘升 (半径渐缩, 蛇身透明度渐隐为雨丝) → 递减间隔落雷 (pitch 递升) →
        /// 最后一道巨雷正中 → 白闪 → 翠玉光雨弹开 → 服务器 StrikeInstantKill 真实死亡。
        /// 全程由 ai 状态机驱动保证 MP 同步; 掉落/downedQinlong 照常走 OnKill。
        /// </summary>
        private void RunDeath() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            if (PhaseTimer == 1) {
                setpieceAnchor = NPC.Center;
                coilAngle = 0f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
            }

            float p = MathHelper.Clamp(PhaseTimer / 190f, 0f, 1f);

            if (PhaseTimer <= 196) {
                // 螺旋盘升: 半径渐缩 + 缓慢上升, 越升越快 (归天加速)
                coilAngle += MathHelper.Lerp(0.045f, 0.10f, p);
                float radius = MathHelper.Lerp(300f, 40f, ACMUtils.QuadIn(p));
                Vector2 dest = setpieceAnchor + coilAngle.ToRotationVector2() * radius + new Vector2(0, -PhaseTimer * 2.1f);
                NPC.velocity = (dest - NPC.Center) * 0.16f;
                // 蛇身渐隐为雨丝
                bodyFade = MathHelper.Lerp(1f, 0.22f, p);
            }
            else {
                NPC.velocity *= 0.80f;
                bodyFade = 0f; // 巨雷之后龙身已碎为光雨
            }

            // 雨丝粒子: 从身体节点落下 (客户端)
            if (!Main.dedServ && renderNodes != null && PhaseTimer % 2 == 0) {
                int ni = Main.rand.Next(1, BodyNodeCount);
                Dust d = Dust.NewDustDirect(renderNodes[ni] + Main.rand.NextVector2Circular(16f, 16f), 0, 0,
                    DustID.Water, 0, 0, 130, default, 1.3f);
                d.noGravity = false;
                d.velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(2f, 4.5f));
            }

            // 递减间隔落雷 (40→10 帧手调数组, pitch 递升)
            for (int i = 0; i < DeathStrikeFrames.Length; i++) {
                if ((int)PhaseTimer == DeathStrikeFrames[i]) {
                    DeathBolt(i);
                    break;
                }
            }

            if (PhaseTimer == 196) {
                // 最后一道巨雷正中 → 白闪 → 翠玉光雨弹开
                PushSkyBolt(NPC.Center - new Vector2(0, 1250), NPC.Center, 9.7f, 20);
                QinglongSky.FlashLightning(1.6f);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.5f, Pitch = 0.4f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = 0.5f }, NPC.Center);
                ACMUtils.AddScreenShake(15f);
                weatherFlash = 1.5f;
                bodyFade = 0f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 70; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0,
                            Main.rand.NextBool() ? DustID.GreenTorch : DustID.Water, 0, 0, 60, default, 2.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(16f, 16f);
                    }
                }
            }

            if (PhaseTimer > 196)
                NPC.Opacity = MathF.Max(0f, NPC.Opacity - 0.12f);

            // 服务器端真实死亡 (掉落/downed 照常; 保底出口: 演出结束即杀, 不可回退)
            if (PhaseTimer >= 208 && Server) {
                NPC.life = 1;
                NPC.StrikeInstantKill();
            }
        }

        /// <summary>死亡演出第 i 道递进落雷: pitch 递升 + 天幕闪光 + 分叉 decal + 氛围余弹。</summary>
        private void DeathBolt(int i) {
            float pitch = -0.35f + i * 0.09f;
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f + i * 0.05f, Pitch = pitch }, NPC.Center);
            ACMUtils.AddScreenShake(MathF.Min(4f + i * 0.5f, 7f));
            QinglongSky.FlashLightning(0.5f + i * 0.06f);

            // 确定性伪随机横向偏移 (各端一致)
            float offX = ((i * 73) % 7 - 3) * 95f;
            Vector2 hit = NPC.Center + new Vector2(offX, 30f);
            PushSkyBolt(hit - new Vector2(offX * 0.4f, 1150f), hit, 1.7f + i * 1.31f, 13);

            if (Server)
                SpawnAmbientBolt(NPC.Center + new Vector2(offX * 2.2f, -950f));
        }

        #endregion

        #region 弹幕施放助手 (server 权威)

        private void NewWindBlade(Vector2 pos, Vector2 vel, int dmg) {
            if (!Server)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<QinglongWindBlade>(), dmg, 0f, Main.myPlayer);
        }

        private void SpawnThunderColumn(Vector2 worldPos, ThreatTier tier) {
            if (!Server)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), worldPos, Vector2.Zero,
                ModContent.ProjectileType<QinglongThunderColumn>(), NPC.damage / 3, 0f, Main.myPlayer,
                TelegraphColors.TelegraphTicks(tier));
        }

        /// <summary>氛围余弹: QinglongThunderBolt ambient 模式 (零伤害纯视觉, ai[1]=1)。</summary>
        private void SpawnAmbientBolt(Vector2 pos) {
            if (!Server)
                return;
            Vector2 vel = new(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(14f, 20f));
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<QinglongThunderBolt>(), 0, 0f, Main.myPlayer, 0f, 1f);
        }

        /// <summary>入队一道天幕分叉雷 decal (客户端演出, 由 PostDraw 逐帧绘制衰减)。</summary>
        private void PushSkyBolt(Vector2 from, Vector2 to, float seed, int life = 14) {
            if (Main.dedServ)
                return;
            skyBolts[skyBoltCursor] = new SkyBolt { From = from, To = to, Seed = seed, Life = life, MaxLife = life };
            skyBoltCursor = (skyBoltCursor + 1) % skyBolts.Length;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 入场背景龙影阶段: 本体隐形, 只画 fake-Z 剪影
            if (State == QlState.Intro && PhaseTimer < 130) {
                DrawIntroSilhouettes(spriteBatch);
                return false;
            }

            if (NPC.Opacity <= 0.01f)
                return false;

            // 蛇身 ribbon (画在头之下)
            DrawDragonBody(spriteBatch, screenPos);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingLeft = MathF.Abs(NPC.rotation) > MathHelper.PiOver2;
            SpriteEffects effects = facingLeft ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 速度门控残影: 只在高速时出现 (speed-gated dressing)
            float speed = NPC.velocity.Length();
            float ghost = ACMUtils.Clamp01((speed - 16f) / 22f);
            if (ghost > 0.05f) {
                Color weatherTail = WeatherColor();
                for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                    if (NPC.oldPos[i] == Vector2.Zero)
                        continue;
                    Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    float alpha = 0.55f * ghost * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                    Color trailColor = Color.Lerp(drawColor, weatherTail, weatherMode != 0 ? 0.45f : 0.2f) * alpha;
                    trailColor.G = (byte)Math.Min(trailColor.G * 1.3f, 255);
                    spriteBatch.Draw(texture, trailPos, frame, trailColor * NPC.Opacity, NPC.rotation, origin,
                        NPC.scale * (1f - i * 0.015f), effects, 0f);
                }
            }

            Vector2 drawPos = NPC.Center - screenPos;

            // 通电时头部叠加白闪
            if (electrify > 0.05f) {
                Color elecGlow = new Color(200, 240, 255, 0) * (electrify * 0.5f * (0.7f + 0.3f * MathF.Sin(GlobalTime * 24f)));
                spriteBatch.Draw(texture, drawPos, frame, elecGlow * NPC.Opacity, NPC.rotation, origin, NPC.scale * 1.06f, effects, 0f);
            }

            spriteBatch.Draw(texture, drawPos, frame, drawColor * NPC.Opacity, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        /// <summary>入场演出: 背景三次龙影掠过 (本体贴图小 scale 半透明快速横掠 + 递缩残段拼长龙)。</summary>
        private void DrawIntroSilhouettes(SpriteBatch sb) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int pass = 0; pass < 3; pass++) {
                int start = 20 + pass * 40;
                float pr = (PhaseTimer - start) / 30f;
                if (pr < 0f || pr > 1f)
                    continue;

                int dir = pass % 2 == 0 ? 1 : -1;
                float scale = 0.40f + pass * 0.07f;
                float sx = MathHelper.Lerp(dir > 0 ? -0.25f : 1.25f, dir > 0 ? 1.25f : -0.25f, pr);
                float sy = 0.30f - pass * 0.05f + MathF.Sin(pr * MathHelper.TwoPi + pass) * 0.03f;
                Vector2 headPos = new(Main.screenWidth * sx, Main.screenHeight * sy);
                float rot = (dir > 0 ? 0f : MathHelper.Pi) + MathF.Sin(pr * 9f + pass * 2f) * 0.08f;
                float alpha = (0.30f + pass * 0.12f) * MathF.Sin(pr * MathHelper.Pi);
                SpriteEffects fxFlip = dir > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                // 龙影链: 头 + 5 段递缩残段 = 长龙剪影
                for (int j = 0; j < 6; j++) {
                    float fall = 1f - j * 0.13f;
                    Vector2 seg = headPos - new Vector2(dir, 0) * (j * 46f * scale)
                        - new Vector2(0, MathF.Sin(pr * 10f - j * 0.9f) * 14f * scale);
                    Color body = new Color(12, 46, 36) * (alpha * fall);
                    sb.Draw(tex, seg, null, body, rot, origin, scale * fall, fxFlip, 0f);
                    Color rim = new Color(60, 200, 150) * (alpha * fall * 0.25f);
                    rim.A = 0;
                    sb.Draw(tex, seg, null, rim, rot, origin, scale * fall * 1.04f, fxFlip, 0f);
                }
            }
        }

        /// <summary>
        /// 程序化蛇身绘制: BuildRibbonStrip 双层 TriangleStrip —
        /// 外层走 QinglongDragonRibbon 鳞纹着色器 (s0=共享噪声), 内层亮芯同 shader 的 uLayer=1 分支。
        /// 着色器缺失时回退为 GlaciateWave 纹理顶点带。
        /// </summary>
        private void DrawDragonBody(SpriteBatch sb, Vector2 screenPos) {
            if (renderNodes == null || bodyFade <= 0.02f)
                return;

            var posArr = new Vector2[BodyNodeCount];
            for (int i = 0; i < BodyNodeCount; i++)
                posArr[i] = renderNodes[i] - screenPos;

            float fade = bodyFade * NPC.Opacity;
            if (fade <= 0.02f)
                return;

            // 天气染色随节点传入顶点色
            Color tint = weatherMode == 2 ? new Color(195, 225, 255)
                       : weatherMode == 1 ? new Color(205, 255, 225)
                       : Color.White;

            float time = GlobalTime;
            var outer = ACMUtils.BuildRibbonStrip(
                posArr,
                p => MathHelper.Lerp(30f, 5f, MathF.Pow(p, 1.25f)) * (0.9f + 0.1f * MathF.Sin(time * 4f - p * 7f)),
                p => new Color(tint.R, tint.G, tint.B, (byte)(MathHelper.Clamp((1f - p * 0.45f) * fade, 0f, 1f) * 255)),
                uvScroll: 0f,
                subdivisions: 3);
            if (outer.Length < 4)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Effect fx = QinglongVFX.RibbonEffect;
            Texture2D noise = ACMShaders.NoiseTexture;

            if (fx != null && noise != null) {
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uElectrify"]?.SetValue(MathHelper.Clamp(electrify, 0f, 1f));
                fx.Parameters["uLayer"]?.SetValue(0f);
                fx.Parameters["uFade"]?.SetValue(1f); // 渐隐已并入顶点 alpha
                fx.Parameters["uColorScale"]?.SetValue(new Color(70, 225, 160).ToVector4());
                fx.Parameters["uColorDeep"]?.SetValue(new Color(10, 60, 45).ToVector4());
                Color fin = weatherMode == 2 ? new Color(160, 220, 255) : new Color(205, 235, 120);
                fx.Parameters["uColorFin"]?.SetValue(fin.ToVector4());

                gd.Textures[0] = noise;
                gd.SamplerStates[0] = SamplerState.LinearWrap;
                fx.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, outer, 0, outer.Length - 2);

                // 内层亮芯 (uLayer=1 分支: 柔光核 + 通电白闪)
                var inner = ACMUtils.BuildRibbonStrip(
                    posArr,
                    p => MathHelper.Lerp(11f, 1.5f, p),
                    p => new Color(tint.R, tint.G, tint.B, (byte)(MathHelper.Clamp((1f - p * 0.6f) * fade * 0.9f, 0f, 1f) * 255)),
                    uvScroll: 0f,
                    subdivisions: 2);
                if (inner.Length >= 4) {
                    fx.Parameters["uLayer"]?.SetValue(1f);
                    fx.CurrentTechnique.Passes[0].Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, inner, 0, inner.Length - 2);
                }
            }
            else {
                // 守卫回退: 无着色器时用剑气灰度图顶点带
                gd.Textures[0] = ACMAsset.GlaciateWave ?? VaultAsset.placeholder2.Value;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, outer, 0, outer.Length - 2);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            Player target = (NPC.target >= 0 && NPC.target < Main.maxPlayers) ? Main.player[NPC.target] : null;

            // 蓄力/承诺/觉醒 爆闪 (经 RequestFullscreenSlot 仲裁的单全屏泛光)
            if (weatherFlash > 0.05f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, MathHelper.Clamp(weatherFlash, 0f, 1f), WeatherColor(), 12f, 2.2f);

            switch (State) {
                case QlState.WindBladeFan:
                    if (InWindup && target != null)
                        DrawConeTelegraph(target);
                    break;
                case QlState.AzureAscent:
                    if (target != null)
                        DrawAimBeam(target, 0.35f);
                    break;
                case QlState.DragonCoil:
                    if (target != null)
                        DrawArenaRing(target.Center, 360f, TelegraphColors.AzureDragon, CoilRingIntensity());
                    break;
                case QlState.WeatherDeck:
                    if (InWindup)
                        DrawDeckCharge();
                    break;
                case QlState.StormfieldJudgment:
                    if (InWindup)
                        DrawDomainPreviews();
                    break;
                case QlState.AwakeningForeshadow:
                    DrawAwakenCharge();
                    break;
                case QlState.DragonLunge:
                    DrawLungeTelegraph();
                    break;
                case QlState.Death:
                    DrawDeathGlow();
                    break;
            }

            DrawSkyBolts();
        }

        private Color WeatherColor() => weatherMode == 2 ? new Color(120, 200, 255) : new Color(80, 235, 150);

        private float CoilRingIntensity() {
            const int nodePeriod = 40;
            float ph = (PhaseTimer % nodePeriod) / (float)nodePeriod;
            return 0.40f + 0.45f * ph; // 节点逼近时渐亮 = 节奏可读
        }

        /// <summary>龙跃红色致命冲刺线: 只在锁定后 14 帧出现 (红=致命, §6.1)。</summary>
        private void DrawLungeTelegraph() {
            if (!InWindup)
                return;
            int windupTicks = LungeWindupTicks;
            if (AttackTimer < windupTicks - LungeLockLead)
                return;
            float pr = MathHelper.Clamp((AttackTimer - (windupTicks - LungeLockLead)) / (float)LungeLockLead, 0f, 1f);
            Vector2 dir = (lungeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 980f, 4.5f,
                TelegraphColors.Lethal, TelegraphColors.Lethal * 0.3f, 0.35f + 0.55f * pr, flowSpeed: 3f);
        }

        /// <summary>死亡演出辉光: 盘升中的龙心翠光渐盛。</summary>
        private void DrawDeathGlow() {
            float p = MathHelper.Clamp(PhaseTimer / 196f, 0f, 1f);
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null || NPC.Opacity <= 0.01f)
                return;
            Vector2 drawPos = NPC.Center - Main.screenPosition;
            Color c = Color.Lerp(new Color(80, 235, 150, 0), new Color(210, 245, 255, 0), p) * (0.35f + 0.5f * p);
            Main.spriteBatch.Draw(glow, drawPos, null, c * NPC.Opacity, 0f, glow.Size() / 2f, 2.4f + 2.4f * p, SpriteEffects.None, 0f);
        }

        /// <summary>绘制并推进天幕分叉雷 decal 队列 (相变劈雷 / 死亡递进落雷 / 天气承诺)。</summary>
        private void DrawSkyBolts() {
            for (int i = 0; i < skyBolts.Length; i++) {
                if (skyBolts[i].Life <= 0)
                    continue;
                ref SkyBolt b = ref skyBolts[i];
                float pr = b.Life / (float)b.MaxLife;
                float inten = MathF.Pow(pr, 0.6f);
                QinglongVFX.DrawLightningDecal(b.From, b.To, inten, b.Seed, new Color(185, 230, 255), 0.85f, 0.008f, pr * 0.5f);
                b.Life--;
            }
        }

        private void DrawConeTelegraph(Player target) {
            float prog = MathHelper.Clamp(AttackTimer / 30f, 0f, 1f);
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = 5 + (weatherMode == 1 ? 2 : 0);
            float half = count / 2 * MathHelper.ToRadians(9f);
            Color core = TelegraphColors.AzureDragon;
            Color edge = TelegraphColors.AzureDragon * 0.3f;
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir.RotatedBy(-half) * 900f, 3f, core, edge, 0.5f * prog);
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir.RotatedBy(half) * 900f, 3f, core, edge, 0.5f * prog);
        }

        private void DrawAimBeam(Player target, float intensity) {
            ACMShaders.DrawBeam(NPC.Center, target.Center, 2.5f,
                TelegraphColors.AzureDragon, TelegraphColors.AzureDragon * 0.3f, intensity);
        }

        private void DrawDeckCharge() {
            float prog = MathHelper.Clamp(AttackTimer / 64f, 0f, 1f);
            Color c = (weatherCommitCount % 2 == 0) ? new Color(80, 235, 150) : new Color(120, 200, 255);
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.10f + 0.10f * prog, 0.25f + 0.6f * prog, c, 14f, 2.4f);
        }

        private void DrawDomainPreviews() {
            float prog = MathHelper.Clamp(AttackTimer / 78f, 0f, 1f);
            for (int i = 0; i < DomainOffsets.Length; i++)
                ElementTelegraphCircle(Main.spriteBatch, setpieceAnchor + DomainOffsets[i], 250f, 0.5f * prog, false);
        }

        private void DrawAwakenCharge() {
            float prog = MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f);
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.15f + 0.18f * prog, prog, new Color(120, 200, 255), 14f, 2.0f);
        }

        private void DrawArenaRing(Vector2 center, float worldRadius, Color primary, float intensity) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null || intensity <= 0.01f)
                return;
            ACMShaders.WorldDecalParams(center, worldRadius, out Vector2 uv, out float rf, out float asp);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rf);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(asp);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(16, 90, 64).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }

        #endregion
    }

    /// <summary>
    /// 青龙专属着色器静态缓存与分叉雷 decal 助手 (照抄 Xuanwu.GetFrostEffect 写法, 不注册 ACMShaders)。
    /// 分叉雷 decal 为满屏 pass, 设每帧 ≤3 道的通道预算防止雷柱齐射时叠爆。
    /// </summary>
    internal static class QinglongVFX
    {
        private const string Path = "AncientChineseMythology/Effects/";

        private static Asset<Effect> ribbonRef;
        private static Asset<Effect> lightningRef;
        private static Asset<Effect> galeRef;

        public static Effect RibbonEffect => Get(ref ribbonRef, "QinglongDragonRibbon");
        public static Effect LightningEffect => Get(ref lightningRef, "QinglongLightning");
        public static Effect GaleEffect => Get(ref galeRef, "QinglongGale");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        // 每帧分叉雷 decal 通道预算
        private static ulong budgetFrame;
        private static int budgetUsed;

        private static bool TryAcquireBoltPass() {
            if (budgetFrame != Main.GameUpdateCount) {
                budgetFrame = Main.GameUpdateCount;
                budgetUsed = 0;
            }
            if (budgetUsed >= 3)
                return false;
            budgetUsed++;
            return true;
        }

        /// <summary>缩放感知的世界坐标 → 归一化屏幕 UV (与 ACMShaders.WorldDecalParams 同约定)。</summary>
        public static Vector2 WorldToScreenUV(Vector2 world) {
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 screen = (world - Main.screenPosition - halfScreen) * zoom + halfScreen;
            return screen / new Vector2(Main.screenWidth, Main.screenHeight);
        }

        /// <summary>
        /// 在**已有活动批**的阶段 (PreDraw/PostDraw) 绘制一道程序化分叉闪电 decal。
        /// 内部走 ACMShaders.DrawScreenSpaceDecal (End→Begin→End→恢复默认批), s0 绑共享噪声。
        /// </summary>
        public static void DrawLightningDecal(Vector2 worldStart, Vector2 worldEnd, float intensity, float seed,
            Color color, float branch = 0.8f, float thickness = 0.007f, float flash = 0f) {
            if (Main.dedServ || intensity <= 0.02f)
                return;
            Effect fx = LightningEffect;
            if (fx == null || !TryAcquireBoltPass())
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uStart"]?.SetValue(WorldToScreenUV(worldStart));
            fx.Parameters["uEnd"]?.SetValue(WorldToScreenUV(worldEnd));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uColor"]?.SetValue(color.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uBranch"]?.SetValue(MathHelper.Clamp(branch, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uThickness"]?.SetValue(thickness);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }
    }

    /// <summary>
    /// 青龙天气滤镜 — 把 Weather Deck 承诺的 Wind/Storm 窗口外溢为屏幕元素染色 (ElementalScreenTint),
    /// 并叠加 QinglongGale 方向性风幕流线 (风域天罚/雷暴/死亡化雨期间)。
    /// 走 PostDrawTiles (无活动批, 在实体之前) 当氛围底色, 两者都不读 screenTarget 故不占全屏后处理名额;
    /// 受 MythologyConfig 降级开关与强度快照驱动, 服务端/截图/无 Boss 时零绘制。
    /// </summary>
    public class QinglongWeatherSystem : ModSystem
    {
        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;
            if (!NPC.AnyNPCs(ModContent.NPCType<Qinlong>()))
                return;

            DrawWeatherTint();
            DrawGaleOverlay();
        }

        private static void DrawWeatherTint() {
            float intensity = Qinlong.s_weatherIntensity;
            if (intensity <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            bool storm = Qinlong.s_weatherMode == 2;
            Color tint = storm ? new Color(70, 150, 230) : new Color(40, 170, 120);
            Color tint2 = storm ? new Color(18, 38, 88) : new Color(8, 58, 40);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.5f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(tint2.ToVector3(), 1f));
            fx.Parameters["uVignette"]?.SetValue(0.35f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        private static void DrawGaleOverlay() {
            float galeI = Qinlong.s_galeIntensity;
            if (galeI <= 0.01f)
                return;

            Effect gale = QinglongVFX.GaleEffect;
            if (gale == null)
                return;

            Color gc = Qinlong.s_weatherMode == 2 ? new Color(150, 200, 235) : new Color(120, 230, 170);

            gale.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            gale.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(galeI, 0f, 1f) * 0.55f);
            gale.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            gale.Parameters["uAngle"]?.SetValue(Qinlong.s_galeAngle);
            gale.Parameters["uDensity"]?.SetValue(6.5f);
            gale.Parameters["uSpeed"]?.SetValue(1.6f);
            gale.Parameters["uColor"]?.SetValue(gc.ToVector4());

            ACMShaders.DrawFullscreenOverlay(gale, BlendState.Additive);
        }

        public override void OnWorldUnload() {
            Qinlong.s_weatherIntensity = 0f;
            Qinlong.s_weatherMode = 0;
            Qinlong.s_galeIntensity = 0f;
            QinglongSky.ResetHooks();
        }
    }
}

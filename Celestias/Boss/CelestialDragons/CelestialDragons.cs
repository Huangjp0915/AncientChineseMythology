using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天御金龙 - 月球领主后的蠕虫类Boss (V3「云海天威」重做)。
    /// 继承BasicWorm以使用正确的蠕虫跟随系统; 贴图朝向: 右边向前(正方向)。
    ///
    /// V3 设计 (设计文档 Docs/BossRedo/CelestialDragon.md):
    ///   ● 三幕规则升级保留 (巡天 &gt;60% / 敕令 ≤60% / 天罚 ≤25%), 招池换成手写轮替表;
    ///   ● 动作波形化: 破云俯冲 = 75f 蓄势弧(速度 24→9 + 末段 pow8 抽身) → 13f 64px/f 爆发 → 硬刹回旋;
    ///   ● 三大演出: 贯天光柱入场 / 螺旋收拢过场(清弹+i-frame) / 金龙升天死亡脚本 (CheckDead 拦截);
    ///   ● 视觉: CelestialDragonVFX 四专属着色器(龙身条带流光/云海/龙珠/天光柱) + 共享 BeamGrad/RadialBloom/ArenaRunic;
    ///   ● 公平阀门: 伤害窗口=视觉窗口(冲刺速度门控), 幕跨越清弹+40f接触豁免+弹速缓升, 距离拴绳, 每状态保底出口。
    /// 红=致命预警, 金=主题/安全 (TelegraphColors 契约)。
    /// </summary>
    public abstract class CelestialDragons : BasicWorm
    {
        // 贴图尺寸常量
        protected const int HeadTextureWidth = 382;
        protected const int HeadTextureHeight = 256;
        protected const int BodyTextureWidth = 152;
        protected const int BodyTextureHeight = 92;
        protected const int TailTextureWidth = 412;
        protected const int TailTextureHeight = 124;

        // 体节覆盖比例（40%覆盖）
        protected const float SegmentOverlapRatio = 0.40f;

        // ===== V3 状态机 =====
        public const int StateCruise = 0;       // 巡航 (连接词/喘息)
        public const int StateSword = 1;        // 剑气长虹 (8字航线, 速度门控发射)
        public const int StateDive = 2;         // 破云俯冲 (招牌: 蓄势-爆发-收招)
        public const int StateCircle = 3;       // 金鳞游曳 (体节充能波 + 尾甩逆鳞)
        public const int StateEdict = 4;        // 敕令法标 (可破目标战)
        public const int StateFullScreen = 5;   // 天罚·万剑归宗 (Act2 破标后一次性)
        public const int StateTransition = 6;   // 幕跨越 (清弹 + i-frame + 螺旋收拢)
        public const int StateRecovery = 7;     // 天罚后强制巡航喘息
        public const int StatePearl = 8;        // 龙珠·天光柱阵 (充能语法招牌)
        public const int StateIntro = 9;        // 入场演出 (贯天光柱 + S弧 + 凝视)
        public const int StateDeath = 10;       // 死亡演出 (金龙升天)

        // 手写轮替表 (PACING §2: 攻击序列本身就是编排 — 压制/区域/持续/喘息交替)
        private static readonly int[] RotAct0 = { StateDive, StateSword, StateCircle, StateCruise };
        private static readonly int[] RotAct1 = { StateEdict, StateDive, StatePearl, StateCircle, StateCruise };
        private static readonly int[] RotAct2 = { StateEdict, StateDive, StatePearl, StateCircle };

        // 入场/俯冲节拍常量
        private const int IntroDuration = 180;
        private const int DiveCoilTime = 75;      // 蓄势弧
        private const int DiveCoilShort = 45;     // Act2 连锁缩短前摇
        private const int DiveDashMax = 24;       // 爆发最长帧数
        private const int DeathDuration = 250;

        // 头部专用状态 (实例字段; 关键项经 SendExtraAI/ReceiveExtraAI 同步)
        private int storedAct = -1;     // 当前已进入的幕 (用于检测跨幕)
        private int rotIndex;           // 攻击轮替索引
        private bool fullScreenArmed;   // 天罚: 破标后解锁一次全屏
        private bool sealsSpawned;      // 本次敕令是否已布标
        private bool edictBroken;       // 本次敕令是否已破
        private float aimX, aimY;       // 俯冲锁定点 / 状态锚点 (同步)
        private int graceTimer;         // 幕跨越后的接触豁免 + 弹速缓升窗口 (同步)
        private bool deathReal;         // 死亡演出结束, 允许真正死亡 (同步)

        // 表现层 (纯本地)
        private float bloomPulse;       // 金芒径向泛光强度
        private float tintLevel;        // 金芒屏幕底色强度
        private float ribbonBoost;      // 条带流光临时增亮 (冲刺/演出)

        // 体节侧状态 (Body/Tail; 由 SegmentAI 读头部同步状态确定性推导)
        public bool Charging;           // 充能窗口 (接触伤害 +50%)
        private float chargeVis;        // 充能可视强度
        private bool crumbled;          // 死亡崩解波已过境 (排气爆已放)
        private float dissolve;         // 死亡溶解 0(实体)~1(消失)

        /// <summary>不使用SpriteDirection翻转，我们手动处理</summary>
        public override bool IsUseSpriteDirection => false;

        /// <summary>目标玩家</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>头节 NPC (头自身返回自己; 体节经 realLife 取头)。</summary>
        protected NPC HeadNPC {
            get {
                if (NPCWormType == WormType.Head)
                    return NPC;
                if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active)
                    return Main.npc[NPC.realLife];
                return null;
            }
        }

        private int segIndexCache = -1;

        /// <summary>
        /// 链序 (头=0, 向尾递增)。沿 FatherWorm 链上溯计数 — SummonCount 只在服务器 OnSpawn 赋值,
        /// 多人客户端恒为 0, 而 FatherWorm 经 SendExtraAI 同步, 因此充能波/条带链序统一用本属性。
        /// 链拓扑不变, 首次计算后缓存。
        /// </summary>
        public int SegmentIndex {
            get {
                if (NPCWormType == WormType.Head)
                    return 0;
                if (segIndexCache > 0)
                    return segIndexCache;
                int idx = 0;
                NPC cur = NPC;
                int guard = 0;
                while (cur?.ModNPC is BasicWorm bw && bw.FatherWorm >= 0 && guard++ < 128) {
                    cur = Main.npc[bw.FatherWorm];
                    idx++;
                }
                // FatherWorm 尚未同步时会得到 0 — 不缓存, 等下帧重算
                if (idx > 0)
                    segIndexCache = idx;
                return idx;
            }
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[NPC.type] = 3;
            NPCID.Sets.TrailCacheLength[NPC.type] = 10;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.damage = 280;
            NPC.defense = 110;
            NPC.lifeMax = 1800000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 500000f;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            SummonMax = 50;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(storedAct);
            writer.Write(rotIndex);
            writer.Write(fullScreenArmed);
            writer.Write(aimX);
            writer.Write(aimY);
            writer.Write(graceTimer);
            writer.Write(deathReal);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            storedAct = reader.ReadInt32();
            rotIndex = reader.ReadInt32();
            fullScreenArmed = reader.ReadBoolean();
            aimX = reader.ReadSingle();
            aimY = reader.ReadSingle();
            graceTimer = reader.ReadInt32();
            deathReal = reader.ReadBoolean();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (NPCWormType != WormType.Head)
                return false;
            // 入场/死亡演出中隐藏血条 (电影节拍)
            if ((int)NPC.ai[0] == StateIntro || (int)NPC.ai[0] == StateDeath)
                return false;
            return null;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.velocity.ToRotation();
        }

        // ============================================================
        //  伤害窗口 (公平阀门: 伤害窗口与视觉严格对齐)
        // ============================================================

        /// <summary>演出/豁免期零接触伤害; 俯冲仅爆发帧(速度门控)造成头部接触伤害。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            NPC head = HeadNPC;
            if (head?.ModNPC is not CelestialDragons headDragon)
                return true;

            int s = (int)head.ai[0];
            if (s == StateIntro || s == StateDeath || s == StateTransition)
                return false;
            // 出场豁免: 前 40f 零接触 (graceTimer 全长 60f 同时驱动弹速缓升)
            if (headDragon.graceTimer > 20)
                return false;

            // 俯冲: 头部只在爆发段且速度足够时命中 (蓄势弧擦身无伤)
            if (NPCWormType == WormType.Head && s == StateDive)
                return (int)head.ai[2] == 1 && head.velocity.Length() > 24f;
            return true;
        }

        /// <summary>接触伤害窗口化: 充能体节 +50%; 俯冲爆发 +30%; 龙珠盘环 -30%; 平时体节 -25%。</summary>
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            if (Charging) {
                modifiers.SourceDamage *= 1.5f;
                return;
            }
            NPC head = HeadNPC;
            int s = head != null ? (int)head.ai[0] : -1;
            if (NPCWormType == WormType.Head) {
                if (s == StateDive)
                    modifiers.SourceDamage *= 1.3f;
                else if (s == StatePearl)
                    modifiers.SourceDamage *= 0.7f;
            }
            else {
                modifiers.SourceDamage *= s == StatePearl ? 0.55f : 0.75f;
            }
        }

        // ============================================================
        //  AI 主干
        // ============================================================

        public override void AI() {
            base.AI();

            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            if (NPCWormType == WormType.Head)
                HeadAI();
            else
                SegmentAI();

            // 常驻金色微粒 (死亡溶解后停止)
            if (!Main.dedServ && dissolve < 0.9f && Main.rand.NextBool(14)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height,
                    DustID.GoldFlame, NPC.velocity.X * 0.1f, NPC.velocity.Y * 0.1f, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(NPC.Center, 0.5f, 0.4f, 0.1f);
        }

        /// <summary>重写位置计算 - 体节被头部牵引，不是插值跟随</summary>
        public override void ChangePos() {
            if (FatherNPC == null) return;

            float segmentWidth = GetSegmentWidth();
            float parentWidth = GetParentSegmentWidth();
            float targetDistance = (parentWidth + segmentWidth) * 0.5f * (1f - SegmentOverlapRatio);

            Vector2 directionFromParent = (NPC.Center - FatherNPC.Center).SafeNormalize(Vector2.UnitX);
            NPC.Center = FatherNPC.Center + directionFromParent * targetDistance;

            NPC.velocity = (FatherNPC.Center - NPC.Center).SafeNormalize(Vector2.Zero) * FatherNPC.velocity.Length();

            NPC.rotation = (FatherNPC.Center - NPC.Center).ToRotation();
        }

        protected virtual float GetSegmentWidth() {
            return NPCWormType switch {
                WormType.Head => HeadTextureWidth * 0.5f,
                WormType.Body => BodyTextureWidth,
                WormType.Tail => TailTextureWidth * 0.4f,
                _ => BodyTextureWidth
            };
        }

        protected float GetParentSegmentWidth() {
            if (FatherNPC?.ModNPC is CelestialDragons parentDragon) {
                return parentDragon.GetSegmentWidth();
            }
            return HeadTextureWidth * 0.5f;
        }

        // ============================================================
        //  头部状态机
        // ============================================================

        private static int DesiredAct(float lifeRatio) => lifeRatio <= 0.25f ? 2 : (lifeRatio <= 0.6f ? 1 : 0);

        private void HeadAI() {
            Player player = Target;
            bool dying = (int)NPC.ai[0] == StateDeath;

            if (!dying && (!player.active || player.dead)) {
                NPC.TargetClosest();
                player = Target;
                if (!player.active || player.dead) {
                    NPC.velocity.Y -= 0.5f;
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;
                    return;
                }
            }

            // 初始化: 直接进入入场演出 (位置/锚点仅服务器写入, 客户端等同步 — 避免错位闪帧)
            if (NPC.localAI[3] == 0f) {
                NPC.localAI[3] = 1f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.ai[3] = Main.rand.NextBool() ? 1f : -1f;
                    NPC.ai[0] = StateIntro;
                    storedAct = DesiredAct((float)NPC.life / NPC.lifeMax);
                    // 入场锚点: 玩家侧方, 贯天光柱在此落下
                    Vector2 anchor = player.Center + new Vector2(NPC.ai[3] * 520f, 40f);
                    aimX = anchor.X;
                    aimY = anchor.Y;
                    NPC.Center = anchor + new Vector2(0f, -1350f);
                    NPC.velocity = new Vector2(NPC.ai[3] * 0.5f, 1.5f);
                    NPC.dontTakeDamage = true;
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = -0.4f }, player.Center);
            }

            NPC.localAI[0]++;
            if (graceTimer > 0)
                graceTimer--;

            float lifeRatio = (float)NPC.life / NPC.lifeMax;
            int act = DesiredAct(lifeRatio);

            // —— 跨幕: 血量跌破阈值且非演出状态时触发 ——
            int cur = (int)NPC.ai[0];
            if (act > storedAct && cur != StateTransition && cur != StateIntro && cur != StateDeath) {
                SetState(StateTransition);
            }

            switch ((int)NPC.ai[0]) {
                case StateIntro: RunIntro(player); break;
                case StateTransition: RunTransition(player, act); break;
                case StateCruise: RunCruise(player, act); break;
                case StateSword: RunSwordSweep(player, act); break;
                case StateDive: RunDive(player, act); break;
                case StateCircle: RunCircle(player, act); break;
                case StateEdict: RunEdict(player, act); break;
                case StatePearl: RunPearl(player, act); break;
                case StateFullScreen: RunFullScreen(player, act); break;
                case StateRecovery: RunRecovery(player, act); break;
                case StateDeath: RunDeath(player); break;
                default: NPC.ai[0] = StateCruise; break;
            }

            NPC.ai[1]++;

            PublishPresentation(player, act);

            NPC.rotation = NPC.velocity.ToRotation();
        }

        /// <summary>切换到指定状态并重置计时/航点; 翻转巡航方向。ai[1] 置 -1: 帧尾自增后新状态首帧 t=0。</summary>
        private void SetState(int s) {
            NPC.ai[0] = s;
            NPC.ai[1] = -1f;
            NPC.ai[2] = 0;
            NPC.localAI[1] = 0;
            NPC.localAI[2] = 0;
            NPC.ai[3] *= -1f;
            sealsSpawned = false;
            edictBroken = false;
            NPC.netUpdate = true;
        }

        /// <summary>按幕的手写轮替表选择下一状态; Act2 破标后优先插入天罚。</summary>
        private void AdvanceState(int act) {
            if (act == 2 && fullScreenArmed) {
                fullScreenArmed = false;
                SetState(StateFullScreen);
                return;
            }
            int[] table = act == 0 ? RotAct0 : (act == 1 ? RotAct1 : RotAct2);
            rotIndex++;
            SetState(table[rotIndex % table.Length]);
        }

        /// <summary>幕跨越后的弹速缓升 (公平阀门: 新幕首轮弹速 20%→100% 走 60f)。</summary>
        private float WindUpMul() => graceTimer > 0 ? MathHelper.Lerp(1f, 0.2f, graceTimer / 60f) : 1f;

        /// <summary>战斗播报 (本地 CombatText; key 位于 NPCs.CelestialDragonsHead 下)。</summary>
        private void Announce(string key, Color color) {
            if (Main.dedServ)
                return;
            string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.CelestialDragonsHead." + key);
            CombatText.NewText(NPC.getRect(), color, text, true);
        }

        // ============================================================
        //  入场: 贯天光柱 → 破云俯冲而出 → S弧 → 凝视节拍 (§5.1)
        // ============================================================

        private void RunIntro(Player player) {
            NPC.dontTakeDamage = NPC.ai[1] < 165;
            float t = NPC.ai[1];
            Vector2 anchor = new(aimX, aimY);

            if (t < 30f) {
                // 光柱先行, 龙在柱顶盘桓 (菩萨低眉之前的天光)
                Vector2 hold = anchor + new Vector2(0f, -1300f + t * 2f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (hold - NPC.Center) * 0.04f, 0.2f);
                if (NPC.velocity.Length() > 12f)
                    NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitY) * 12f;

                if ((int)t == 6 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 1f }, anchor);
                    ACMUtils.AddScreenShake(3f);
                }
                // 光柱下的金尘瀑布
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 pos = anchor + new Vector2(Main.rand.NextFloat(-70f, 70f), -Main.rand.NextFloat(1100f));
                    int d = Dust.NewDust(pos, 0, 0, DustID.GoldFlame, 0, 6f, 100, default, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }
            else if (t < 110f) {
                // 破柱俯冲而出 + 大 S 弧横贯 (纯演出)
                if ((int)t == 30) {
                    Vector2 dir = (player.Center + new Vector2(-NPC.ai[3] * 400f, 260f) - NPC.Center).SafeNormalize(Vector2.UnitY);
                    NPC.velocity = dir * 58f;
                    NPC.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = 0.1f }, NPC.Center);
                        ACMUtils.AddScreenShake(7f);
                        CelestialScreenSystem.PublishCloudPunch(anchor + new Vector2(0, 200f), 1f);
                    }
                }

                // S 弧航点 (localAI[1] = 航点序)
                int wp = (int)NPC.localAI[1];
                Vector2 targetPos = wp switch {
                    0 => player.Center + new Vector2(-NPC.ai[3] * 680f, 250f),
                    1 => player.Center + new Vector2(NPC.ai[3] * 740f, -160f),
                    _ => player.Center + new Vector2(-NPC.ai[3] * 520f, -470f)
                };
                if (Vector2.Distance(NPC.Center, targetPos) < 240f && wp < 2)
                    NPC.localAI[1]++;

                float speed = MathHelper.Lerp(58f, 26f, ACMUtils.Clamp01((t - 30f) / 80f));
                SteerTowards(targetPos, speed, 0.05f, 0.3f);
                ribbonBoost = MathHelper.Max(ribbonBoost, 0.5f);

                // 高速穿云再 punch 一次
                if ((int)t == 52 && !Main.dedServ)
                    CelestialScreenSystem.PublishCloudPunch(NPC.Center, 0.8f);
            }
            else if (t < 150f) {
                // 凝视节拍: 减速滑翔, 什么都不做 (威仪 = 静止)
                if (NPC.velocity.Length() > 7f)
                    NPC.velocity *= 0.94f;
            }
            else {
                if ((int)t == 150) {
                    // 仰天咆哮 + 金芒泛光脉冲
                    bloomPulse = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.4f, Pitch = -0.1f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                    }
                }
                if (NPC.velocity.Length() < 10f)
                    NPC.velocity *= 1.03f;
            }

            if (t >= IntroDuration) {
                storedAct = DesiredAct((float)NPC.life / NPC.lifeMax);
                rotIndex = 0;
                NPC.dontTakeDamage = false;
                SetState(RotAct0[0]);
            }
        }

        // ============================================================
        //  幕跨越: 清弹 + 螺旋收拢 + 爆闪宣告 (§5.2)
        // ============================================================

        private void RunTransition(Player player, int act) {
            NPC.dontTakeDamage = true;
            float t = NPC.ai[1];

            if ((int)t == 1) {
                ClearHostileProjectiles();
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.4f, Pitch = -0.2f }, NPC.Center);
            }

            Vector2 center = player.Center + new Vector2(0f, -650f);
            if (t < 50f) {
                // 螺旋收拢: 角速度恒定, 半径 400→170
                float ang = t * 0.13f * NPC.ai[3];
                float radius = MathHelper.Lerp(400f, 170f, t / 50f);
                Vector2 orbit = center + ang.ToRotationVector2() * radius;
                SteerTowards(orbit, 26f, 0.09f, 0.25f);

                float p = t / 50f;
                ACMUtils.AddScreenShake(2f + p * 8f);
                bloomPulse = MathHelper.Max(bloomPulse, 0.3f + p * 0.4f);
                ribbonBoost = MathHelper.Max(ribbonBoost, p);

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 spawn = NPC.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(140f, 320f);
                    Vector2 vel = (NPC.Center - spawn) * 0.08f;
                    int d = Dust.NewDust(spawn, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[d].noGravity = true;
                }
            }
            else if ((int)t == 50) {
                // 环心爆闪 + 宣告新幕
                storedAct = act;
                bloomPulse = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.3f }, NPC.Center);
                    ACMUtils.AddScreenShake(10f);
                    CelestialScreenSystem.PublishFlash(0.45f);
                    CelestialScreenSystem.PublishCloudPunch(NPC.Center, 1f);
                }
                Announce(act >= 2 ? "ActWrath" : "ActEdict", TelegraphColors.Gold);
                NPC.netUpdate = true;
            }
            else if (t > 65f) {
                // 散环俯出
                Vector2 exit = player.Center + new Vector2(NPC.ai[3] * 700f, -420f);
                SteerTowards(exit, 24f, 0.06f, 0.2f);
            }

            if (t >= 75f) {
                NPC.dontTakeDamage = false;
                rotIndex = 0;
                graceTimer = 60; // 出场豁免: 40f 接触豁免 + 弹速缓升 (统一 60f 窗口)
                SetState(StateEdict); // 升幕首发: 敕令登场
            }
        }

        /// <summary>清空本 Boss 生成的全部敌对弹幕 (幕跨越/死亡公平阀门)。</summary>
        private void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int t1 = ModContent.ProjectileType<CelestialLightning>();
            int t2 = ModContent.ProjectileType<GoldenSwordAura>();
            int t3 = ModContent.ProjectileType<GoldenEnergy>();
            int t4 = ModContent.ProjectileType<FallingSword>();
            int t5 = ModContent.ProjectileType<ForkedCelestialLightning>();
            int t6 = ModContent.ProjectileType<ForkedLightningWarning>();
            int t7 = ModContent.ProjectileType<CelestialScale>();
            int t8 = ModContent.ProjectileType<CelestialSkyPillar>();
            int t9 = ModContent.ProjectileType<EdictBeacon>();
            int t10 = ModContent.ProjectileType<CelestialDragonPearlOrb>();
            int t11 = ModContent.ProjectileType<CelestialPathWarning>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active)
                    continue;
                int type = p.type;
                if (type == t1 || type == t2 || type == t3 || type == t4 || type == t5 ||
                    type == t6 || type == t7 || type == t8 || type == t9 || type == t10 || type == t11)
                    p.Kill();
            }
        }

        // ============================================================
        //  巡航 (连接词/喘息, §4.7)
        // ============================================================

        private void RunCruise(Player player, int act) {
            float direction = NPC.ai[3];
            int wp = (int)NPC.localAI[1];
            const float horizontalDist = 950f;
            const float verticalRange = 420f;
            const float baseHeight = 320f;

            Vector2 targetPos = (wp % 4) switch {
                0 => player.Center + new Vector2(direction * horizontalDist, -baseHeight - verticalRange),
                1 => player.Center + new Vector2(-direction * horizontalDist, -baseHeight + verticalRange * 0.5f),
                2 => player.Center + new Vector2(-direction * horizontalDist, -baseHeight - verticalRange * 0.8f),
                _ => player.Center + new Vector2(direction * horizontalDist, -baseHeight + verticalRange * 0.3f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 250f)
                NPC.localAI[1]++;
            ApplyMovement(targetPos, 22f, 0.025f, 18f);

            // 路径预警
            if ((int)NPC.ai[1] % 100 == 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 futurePos = NPC.Center + NPC.velocity * 50f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer, futurePos.X, futurePos.Y);
            }
            // 低密度辐射弹
            if ((int)NPC.ai[1] % 90 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 4;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + NPC.ai[1] * 0.01f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 6f * WindUpMul(),
                        ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
                }
            }

            if ((int)NPC.ai[1] == 90)
                TrySpawnMandateZone(player, 420f, 300f);

            if (NPC.ai[1] >= 240)
                AdvanceState(act);
        }

        // ============================================================
        //  剑气长虹: 8字航线 + 速度门控发射 (§4.2)
        // ============================================================

        private void RunSwordSweep(Player player, int act) {
            float t = NPC.ai[1];
            // Lissajous 8 字: 弧顶慢 / 弧底快由曲线导数自然给出
            float theta = t * 0.021f * NPC.ai[3];
            Vector2 center = player.Center + new Vector2(0f, -300f);
            Vector2 targetPos = center + new Vector2(MathF.Sin(theta) * 840f, MathF.Sin(theta * 2f) * 320f);

            // 速度调制: 沿 8 字底部(纵向速度大)加速, 顶部减速
            float speedPhase = MathF.Abs(MathF.Cos(theta));
            float speed = MathHelper.Lerp(11f, 30f, speedPhase);
            ApplyMovement(targetPos, speed, 0.045f, 9f);

            bool fastSegment = NPC.velocity.Length() > 24f;
            ribbonBoost = MathHelper.Max(ribbonBoost, fastSegment ? 0.6f : 0f);

            // 只在快段发射 (张弛都在一招之内); 慢段完全静默
            if (fastSegment && (int)t % 16 == 0 && t > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                const int projectileCount = 3;
                const float spread = 0.42f;
                for (int i = 0; i < projectileCount; i++) {
                    float angle = spread * ((i - (projectileCount - 1) / 2f) / (projectileCount - 1));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toAim * 50, toAim.RotatedBy(angle) * 14f * WindUpMul(),
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, NPC.Center);
            }

            if (t >= 300)
                AdvanceState(act);
        }

        // ============================================================
        //  破云俯冲: 蓄势弧 → 爆发 → 硬刹回旋 (招牌, §4.1)
        // ============================================================

        private void RunDive(Player player, int act) {
            float side = NPC.ai[3];
            int sub = (int)NPC.ai[2];
            float t = NPC.ai[1];
            int cycle = (int)NPC.localAI[1];
            // Act2 连锁俯冲: 第 2/3 次前摇缩短 (删除死航程)
            int coilTime = (act == 2 && cycle > 0) ? DiveCoilShort : DiveCoilTime;
            float launchT = coilTime;

            if (sub == 0) {
                // —— 蓄势弧: 玩家斜上方画弧, 速度 24→9 递减 ——
                float prog = ACMUtils.Clamp01(t / coilTime);
                Vector2 anchor = player.Center + new Vector2(side * 680f, -640f);
                float ang = t * 0.10f * side;
                Vector2 orbit = anchor + ang.ToRotationVector2() * MathHelper.Lerp(300f, 200f, prog);

                // 末段 pow8 反向抽身 (迟滞后仰 — 吸气)
                float reel = MathF.Pow(ACMUtils.Clamp01((t - (coilTime - 12f)) / 12f), 8f);
                if (reel > 0f) {
                    Vector2 away = (NPC.Center - player.Center).SafeNormalize(Vector2.UnitX);
                    orbit += away * reel * 220f;
                }

                float speed = MathHelper.Lerp(24f, 9f, ACMUtils.SineInOut(prog));
                // 远距离拉近 (act2 连锁时前摇短, 用高速趋近替代飞回)
                if (Vector2.Distance(NPC.Center, anchor) > 1000f)
                    speed = 42f;
                SteerTowards(orbit, speed, 0.075f, 0.16f);

                // 汇聚流光 (爆发前 6f 静默切断)
                if (!Main.dedServ && t < coilTime - 6f && Main.rand.NextBool(2)) {
                    Vector2 spawn = NPC.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(120f, 320f);
                    Vector2 vel = (NPC.Center + NPC.velocity * 4f - spawn) * 0.085f;
                    int d = Dust.NewDust(spawn, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[d].noGravity = true;
                }

                // 蜂鸣: 固定提前 36f (玩家可内化的预警常数)
                if ((int)t == (int)(launchT - 36f) && !Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f + cycle * 0.15f, Volume = 0.8f }, NPC.Center);

                // 锁定俯冲线 + 红色路径预警 (30f)
                if ((int)t == (int)(launchT - 30f)) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 lead = player.velocity * 12f;
                        if (lead.Length() > 260f)
                            lead = lead.SafeNormalize(Vector2.Zero) * 260f;
                        Vector2 aim = player.Center + lead;
                        aimX = aim.X;
                        aimY = aim.Y;
                        Vector2 dir = (aim - NPC.Center).SafeNormalize(Vector2.UnitX);
                        Vector2 lineEnd = NPC.Center + dir * 2400f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer, lineEnd.X, lineEnd.Y, 30f);
                        NPC.netUpdate = true;
                    }
                }

                if (t >= launchT) {
                    // 距离拴绳: 玩家太远不冲 (绝不从屏幕外发难), 直接转收招重新趋近
                    if (Vector2.Distance(NPC.Center, player.Center) > 2600f ||
                        Vector2.Distance(NPC.Center, player.Center) < 380f) {
                        NPC.ai[2] = 2;
                        NPC.netUpdate = true;
                    }
                    else {
                        Vector2 dir = (new Vector2(aimX, aimY) - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.velocity = dir * 64f; // 一帧 set, 不是 ramp (发射即雷霆)
                        NPC.ai[2] = 1;
                        NPC.localAI[2] = 0;
                        NPC.netUpdate = true;
                        ribbonBoost = 1f;
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.1f, Pitch = 0.45f }, NPC.Center);
                            ACMUtils.AddScreenShake(6f);
                        }
                    }
                }
            }
            else if (sub == 1) {
                // —— 爆发: 近零转向直线 ——
                NPC.localAI[2]++;
                float dashT = NPC.localAI[2];
                ribbonBoost = 1f;

                // 穿云 punch (爆发中段, 云被冲开)
                if ((int)dashT == 7 && !Main.dedServ)
                    CelestialScreenSystem.PublishCloudPunch(NPC.Center, 0.9f);

                Vector2 aim = new(aimX, aimY);
                bool passed = Vector2.Dot(aim - NPC.Center, NPC.velocity) < 0f &&
                              Vector2.Distance(NPC.Center, aim) > 380f;
                if (dashT >= DiveDashMax || passed) {
                    NPC.ai[2] = 2;
                    NPC.netUpdate = true;
                }
            }
            else {
                // —— 收招: 硬刹 + 回旋爬升 ——
                if (NPC.velocity.Length() > 12f)
                    NPC.velocity *= 0.72f; // 硬刹 = "砸进位置"的读感
                Vector2 recover = player.Center + new Vector2(-side * 760f, -540f);
                SteerTowards(recover, 19f, 0.05f, 0.1f);

                if (Vector2.Distance(NPC.Center, recover) < 260f || t >= launchT + 90f) {
                    int cycles = act == 2 ? 3 : 2;
                    NPC.localAI[1]++;
                    if (NPC.localAI[1] >= cycles) {
                        AdvanceState(act);
                    }
                    else {
                        // 下一循环: 重置计时/子状态, 换边
                        NPC.ai[1] = -1f;
                        NPC.ai[2] = 0;
                        NPC.ai[3] *= -1f;
                        NPC.netUpdate = true;
                    }
                }
            }
        }

        // ============================================================
        //  金鳞游曳: 收缩椭圆 + 体节充能波 + 尾甩逆鳞 (§4.3)
        // ============================================================

        private void RunCircle(Player player, int act) {
            float t = NPC.ai[1];
            // 半径呼吸: 780 → 560 → 780
            float breathe = MathF.Sin(t / 360f * MathF.PI);
            float radius = MathHelper.Lerp(780f, 560f, breathe);
            float angle = t * 0.02f * NPC.ai[3];
            Vector2 targetPos = player.Center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.62f - 140f);
            ApplyMovement(targetPos, 23f, 0.035f, 19f);

            if ((int)t % 20 == 0 && t > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toAim * 9f * WindUpMul(),
                    ModContent.ProjectileType<GoldenEnergy>(), NPC.damage / 4, 3f, Main.myPlayer);
            }

            // 体节充能波的预警提示音 (体节在 SegmentAI 读头部计时点亮)
            if ((int)t % 90 == 28 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.5f }, NPC.Center);

            // 充能波抵达尾端 → 尾甩逆鳞 (波经全身耗时 (SummonMax+1)*4, 波心在窗口中点 +45f)
            float waveArrive = (SummonMax + 1) * 4f + 45f;
            if (t > waveArrive && ((int)(t - waveArrive)) % 90 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC tail = FindTail();
                if (tail != null) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 fling = (player.Center - tail.Center).SafeNormalize(Vector2.UnitX)
                            .RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(7f, 10f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), tail.Center, fling,
                            ModContent.ProjectileType<CelestialScale>(), NPC.damage / 4, 0f, Main.myPlayer, NPC.damage / 4);
                    }
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.9f }, tail.Center);
                }
            }

            // 反风筝早退: 玩家远离 1500px 直接结束 (状态永远有出口)
            if (t >= 360 || (t > 90 && Vector2.Distance(NPC.Center, player.Center) > 1500f && (int)t % 30 == 0))
                AdvanceState(act);
        }

        private NPC FindTail() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.realLife == NPC.whoAmI && npc.ModNPC is CelestialDragonsTail)
                    return npc;
            }
            return null;
        }

        // ============================================================
        //  敕令法标: 依次盖印 + 高空监察 + 周期单发俯冲 (§4.4)
        // ============================================================

        private void RunEdict(Player player, int act) {
            float t = NPC.ai[1];
            float direction = NPC.ai[3];

            // 高空监察巡弋
            int wp = (int)NPC.localAI[2] % 4;
            const float radius = 700f;
            const float baseHeight = 460f;
            Vector2 targetPos = wp switch {
                0 => player.Center + new Vector2(direction * radius, -baseHeight),
                1 => player.Center + new Vector2(0, -baseHeight - 200f),
                2 => player.Center + new Vector2(-direction * radius, -baseHeight),
                _ => player.Center + new Vector2(0, -baseHeight + 100f)
            };
            if (Vector2.Distance(NPC.Center, targetPos) < 180f)
                NPC.localAI[2]++;

            // 周期单发俯冲 (每 240f: 60f 预警 → 16f 掠过 → 回升), 由同步计时驱动
            float swoopPhase = t % 240f;
            bool swooping = sealsSpawned && swoopPhase >= 180f && swoopPhase < 196f;
            if (sealsSpawned && (int)swoopPhase == 120 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 aim = player.Center + player.velocity * 10f;
                aimX = aim.X;
                aimY = aim.Y;
                Vector2 dir = (aim - NPC.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<CelestialPathWarning>(), 0, 0f, Main.myPlayer,
                    NPC.Center.X + dir.X * 2200f, NPC.Center.Y + dir.Y * 2200f, 60f);
                NPC.netUpdate = true;
            }
            if (sealsSpawned && (int)swoopPhase == 180) {
                NPC.velocity = (new Vector2(aimX, aimY) - NPC.Center).SafeNormalize(Vector2.UnitX) * 40f;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = 0.5f }, NPC.Center);
                NPC.netUpdate = true;
            }
            if (!swooping)
                ApplyMovement(targetPos, 17f, 0.03f, 14f);

            // 布标: 4 枚法标沿弧线依次"盖印" (每 14f 一枚)
            if (!sealsSpawned && t >= 60f) {
                int sealIndex = (int)((t - 60f) / 14f);
                if ((int)(t - 60f) % 14 == 0 && sealIndex < 4) {
                    if (sealIndex == 0 && !Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.1f, Volume = 1f }, player.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        float a = MathHelper.TwoPi * sealIndex / 4f + MathHelper.PiOver4;
                        Vector2 pos = player.Center + a.ToRotationVector2() * 720f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<EdictBeacon>(), NPC.damage / 4, 0f, Main.myPlayer, NPC.damage / 4, NPC.whoAmI);
                    }
                    bloomPulse = MathHelper.Max(bloomPulse, 0.5f);
                }
                if (sealIndex >= 3) {
                    sealsSpawned = true;
                    NPC.netUpdate = true;
                }
            }

            // 持续辐射剑气 (中低密度, 朝赐福区/玩家)
            if (sealsSpawned && (int)t % 45 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toAim = (AimPoint(player) - NPC.Center).SafeNormalize(Vector2.Zero);
                for (int i = -1; i <= 1; i++)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toAim.RotatedBy(i * 0.25f) * 12f * WindUpMul(),
                        ModContent.ProjectileType<GoldenSwordAura>(), NPC.damage / 4, 3f, Main.myPlayer);
            }

            // 破标: 全部法标被摧毁 → 雷雨止, 奖励节拍 (Act2 解锁天罚)。
            // 演出各端本地触发; 状态推进只由服务器决定 (弹幕计数以服务器为权威)。
            if (sealsSpawned && !edictBroken && t > 120f && CountSeals() == 0) {
                edictBroken = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                    ACMUtils.AddScreenShake(6f);
                }
                Announce("EdictBroken", TelegraphColors.Holy);
                bloomPulse = MathHelper.Max(bloomPulse, 0.9f);
                ClearEdictRain();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    if (act == 2)
                        fullScreenArmed = true;
                    AdvanceState(act);
                }
                return;
            }

            // 容错超时: 久未破标 → 强制清标并结束 (避免无解卡幕)
            if (sealsSpawned && t >= 900f && Main.netMode != NetmodeID.MultiplayerClient) {
                KillAllSeals();
                ClearEdictRain();
                AdvanceState(act);
            }
        }

        /// <summary>破标瞬间清掉仍在空中的雷雨 (奖励节拍即时可感)。</summary>
        private void ClearEdictRain() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int w = ModContent.ProjectileType<ForkedLightningWarning>();
            int l = ModContent.ProjectileType<ForkedCelestialLightning>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == w || p.type == l))
                    p.Kill();
            }
        }

        // ============================================================
        //  龙珠·天光柱阵: 盘环结阵 + 完整充能语法 (§4.5)
        // ============================================================

        private void RunPearl(Player player, int act) {
            float t = NPC.ai[1];

            // 结阵: 锁定环心 (玩家上方 420px), 之后不追踪
            if ((int)t == 0) {
                Vector2 ringCenter = player.Center + new Vector2(0f, -420f);
                aimX = ringCenter.X;
                aimY = ringCenter.Y;
                Announce("Pronounce", TelegraphColors.Gold);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                NPC.netUpdate = true;
            }

            Vector2 center = new(aimX, aimY);
            // 龙沿环轨道盘旋 (蛇身自然缠成金环)
            float ang = t * 0.035f * NPC.ai[3];
            Vector2 orbit = center + ang.ToRotationVector2() * 460f;
            SteerTowards(orbit, 24f, 0.09f, 0.2f);

            // 龙珠生成 (龙口衔珠 60f 后放出到环心)
            if ((int)t == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                int pillarCount = act >= 2 ? 7 : 5;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), center, Vector2.Zero,
                    ModContent.ProjectileType<CelestialDragonPearlOrb>(), 0, 0f, Main.myPlayer,
                    pillarCount, NPC.damage / 3);
            }

            ribbonBoost = MathHelper.Max(ribbonBoost, MathHelper.Clamp((t - 60f) / 110f, 0f, 0.8f));

            // 60(结阵) + 110(蓄光) + 60(轰落) + 50(收招) ≈ 280
            if (t >= 280)
                AdvanceState(act);
        }

        // ============================================================
        //  天罚·万剑归宗: 中央对称扫出 + 外环神雷收口 (§4.6)
        // ============================================================

        private void RunFullScreen(Player player, int act) {
            float t = NPC.ai[1];
            float direction = NPC.ai[3];

            // 高空定点盘旋
            Vector2 targetPos = player.Center + new Vector2(direction * 300f, -560f);
            targetPos.X += MathF.Sin(t * 0.03f) * 160f;
            ApplyMovement(targetPos, t < 40f ? 12f : 16f, 0.03f, 9f);

            // —— 蓄力 90f: 龙珠语法 (汇聚 72% 切断 + 末段静默) ——
            if (t < 90f) {
                float charge = t / 90f;
                bool quiet = t >= 70f;
                if (!Main.dedServ && !quiet && charge < 0.72f && Main.rand.NextFloat() < 0.2f + MathF.Sqrt(charge) * 0.5f) {
                    Vector2 spawn = NPC.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(150f, 400f);
                    Vector2 vel = (NPC.Center - spawn) * 0.085f;
                    int d = Dust.NewDust(spawn, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.9f);
                    Main.dust[d].noGravity = true;
                }
                if (!quiet)
                    ACMUtils.AddScreenShake(charge * charge * 5f);
                bloomPulse = MathHelper.Max(bloomPulse, charge * 0.8f);
            }

            // —— 顿帧 + 释放 ——
            if ((int)t == 90) {
                // 锁定扫描中心 (释放瞬间玩家位置, 之后不追踪)
                aimX = player.Center.X;
                aimY = player.Center.Y;
                bloomPulse = 1f;
                Announce("Judgment", TelegraphColors.Lethal);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.3f }, NPC.Center);
                    ACMUtils.AddScreenShake(12f);
                    CelestialScreenSystem.PublishFlash(0.9f); // 一场战斗唯一一次全屏白金闪
                }
                NPC.netUpdate = true;
            }

            // 外环神雷收口 (自带 90f 预警)
            if ((int)t == 100 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 c = new(aimX, aimY);
                const int count = 14;
                for (int i = 0; i < count; i++) {
                    float a = MathHelper.TwoPi * i / count;
                    Vector2 strike = c + a.ToRotationVector2() * 900f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), strike + new Vector2(0, -1800f), Vector2.Zero,
                        ModContent.ProjectileType<ForkedLightningWarning>(), NPC.damage / 3, 5f, Main.myPlayer, strike.X, strike.Y);
                }
            }

            // 诛仙剑雨: 从中央向两侧对称扫出 (安全带跟随扫描线之后)
            if (t >= 90f && t <= 200f && (int)t % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 c = new(aimX, aimY);
                float offset = (t - 90f) / 110f * 1500f;
                for (int s = -1; s <= 1; s += 2) {
                    float colX = c.X + s * offset;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(colX, c.Y - 760f), new Vector2(0, 9f),
                        ModContent.ProjectileType<FallingSword>(), NPC.damage / 3, 3f, Main.myPlayer);
                }
            }

            if (t >= 210)
                SetState(StateRecovery);
        }

        // ============================================================
        //  天罚后强制巡航喘息 (§4.6 收尾)
        // ============================================================

        private void RunRecovery(Player player, int act) {
            float direction = NPC.ai[3];
            Vector2 targetPos = player.Center + new Vector2(direction * 1000f, -500f);
            ApplyMovement(targetPos, 20f, 0.03f, 14f);

            if ((int)NPC.ai[1] == 60)
                TrySpawnMandateZone(player, 380f, 260f);

            if (NPC.ai[1] >= 240)
                AdvanceState(act);
        }

        // ============================================================
        //  死亡: 金龙升天 (§5.3, CheckDead 拦截)
        // ============================================================

        public override bool CheckDead() {
            if (NPCWormType != WormType.Head)
                return true;
            if (deathReal)
                return true;
            // 拦截真死, 进入升天演出
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            if ((int)NPC.ai[0] != StateDeath)
                SetState(StateDeath);
            return false;
        }

        private void RunDeath(Player player) {
            NPC.dontTakeDamage = true;
            float t = NPC.ai[1];

            if ((int)t == 0) {
                ClearHostileProjectiles();
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.3f, Pitch = -0.5f }, NPC.Center);
            }

            // 警钟加速鸣响贯穿升天段: 间隔 40→6 递减, 音调递升 (静默段前停止)
            if (t < 180f && (int)t == (int)NPC.localAI[1] && !Main.dedServ) {
                int n = (int)NPC.localAI[2];
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f + n * 0.09f, Volume = 0.9f }, NPC.Center);
                NPC.localAI[1] += MathHelper.Max(6f, 40f - n * 5f);
                NPC.localAI[2]++;
            }

            if (t < 80f) {
                // 昂头缓爬 + 阶梯刹车
                float spd = NPC.velocity.Length();
                if (spd > 12f)
                    NPC.velocity *= 0.94f;
                else if (spd > 5f)
                    NPC.velocity *= 0.985f;
                Vector2 up = NPC.Center + new Vector2(NPC.ai[3] * 60f, -400f);
                SteerTowards(up, MathHelper.Max(spd * 0.98f, 4f), 0.02f, 0.03f);
            }
            else if (t < 180f) {
                // 缓慢上升螺旋 + 隐雷
                float ang = t * 0.045f * NPC.ai[3];
                Vector2 spiral = NPC.Center + new Vector2(MathF.Cos(ang) * 180f, -170f);
                SteerTowards(spiral, 7f, 0.035f, 0.05f);
                float p = (t - 80f) / 100f;
                ACMUtils.AddScreenShake(p * p * 4f);
            }
            else if (t < 192f) {
                // 12f 全静默 (爆发前的吸气 — 无声帧)
                NPC.velocity *= 0.9f;
            }
            else if ((int)t == 192) {
                // 升天爆发
                bloomPulse = 1f;
                if (!Main.dedServ) {
                    CelestialScreenSystem.PublishFlash(1f);
                    CelestialScreenSystem.PublishCloudPunch(NPC.Center, 1f);
                    ACMUtils.AddScreenShake(18f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f, Pitch = -0.4f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.6f }, NPC.Center);
                    for (int i = 0; i < 40; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(10f, 10f);
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 2.6f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }
            else {
                // 溶解上升 (体节自尾向头化金芒, 由 SegmentAI 推导)
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, -3f), 0.05f);
            }

            if (t >= DeathDuration && Main.netMode != NetmodeID.MultiplayerClient) {
                deathReal = true;
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
                NPC.netUpdate = true;
            }
        }

        // ============================================================
        //  表现层 + 工具
        // ============================================================

        private void PublishPresentation(Player player, int act) {
            if (Main.dedServ)
                return;

            int state = (int)NPC.ai[0];
            float t = NPC.ai[1];

            // 金芒底色: 入场攀升 → 随幕加浓 → 死亡拉满
            float targetTint;
            float cloud;
            switch (state) {
                case StateIntro:
                    targetTint = MathHelper.Lerp(0f, 0.35f, ACMUtils.Clamp01(t / 150f)) - (t > 150f ? (t - 150f) / 30f * 0.19f : 0f);
                    cloud = ACMUtils.Clamp01(t / 60f);
                    break;
                case StateDeath:
                    targetTint = MathHelper.Clamp(0.3f + t / 180f * 0.3f, 0f, 0.6f);
                    cloud = 0.75f;
                    break;
                case StateTransition:
                    targetTint = 0.16f + act * 0.14f + 0.2f;
                    cloud = 0.6f;
                    break;
                default:
                    targetTint = 0.16f + storedAct * 0.13f;
                    cloud = 0.25f + storedAct * 0.15f;
                    break;
            }
            tintLevel = MathHelper.Lerp(tintLevel, MathHelper.Max(targetTint, 0f), 0.05f);

            float runic = state == StateEdict && sealsSpawned ? 0.7f : 0f;

            CelestialScreenSystem.Publish(player.Center, tintLevel, runic, 740f, cloud, (float)Main.GlobalTimeWrappedHourly);

            bloomPulse = MathHelper.Lerp(bloomPulse, 0f, 0.06f);
            ribbonBoost = MathHelper.Lerp(ribbonBoost, 0f, 0.08f);
        }

        /// <summary>若玩家正站在某个天命赐福区内, 金龙优先朝该区中心攻击 (风险/回报)。</summary>
        private Vector2 AimPoint(Player player) {
            int type = ModContent.ProjectileType<MandateZone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile mp = Main.projectile[i];
                if (!mp.active || mp.type != type)
                    continue;
                float r = mp.ai[0] <= 0 ? 200f : mp.ai[0];
                if (Vector2.DistanceSquared(player.Center, mp.Center) < r * r)
                    return mp.Center;
            }
            return player.Center;
        }

        private void TrySpawnMandateZone(Player player, float spreadX, float spreadY) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountProjectiles(ModContent.ProjectileType<MandateZone>()) >= 2)
                return;
            Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(spreadX, spreadY);
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<MandateZone>(), 0, 0f, Main.myPlayer, 200f);
        }

        private static int CountProjectiles(int type) {
            int c = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                    c++;
            return c;
        }

        private int CountSeals() {
            int type = ModContent.ProjectileType<EdictBeacon>();
            int c = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type && (int)Main.projectile[i].ai[1] == NPC.whoAmI)
                    c++;
            return c;
        }

        private void KillAllSeals() {
            int type = ModContent.ProjectileType<EdictBeacon>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile sp = Main.projectile[i];
                if (sp.active && sp.type == type && (int)sp.ai[1] == NPC.whoAmI)
                    sp.Kill();
            }
        }

        // ============================================================
        //  体节: 充能波 / 剑气涟漪 / 死亡崩解-溶解 (读头部同步状态确定性推导)
        // ============================================================

        private void SegmentAI() {
            Charging = false;
            NPC head = HeadNPC;
            if (head == null || head.ModNPC is not CelestialDragons) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }

            int state = (int)head.ai[0];
            float headT = head.ai[1];
            int segIdx = SegmentIndex;
            float p = segIdx / (float)(SummonMax + 1); // 0(近头)~1(尾)

            // —— 死亡: 崩解波(头→尾排气) + 升天溶解(尾→头) ——
            if (state == StateDeath) {
                float crumbleFront = ACMUtils.Clamp01(headT / 150f);
                if (!crumbled && p <= crumbleFront) {
                    crumbled = true;
                    if (!Main.dedServ) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 v = Main.rand.NextVector2CircularEdge(4f, 4f);
                            int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 1.7f);
                            Main.dust[d].noGravity = true;
                        }
                        if (segIdx % 8 == 0)
                            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f + p * 0.4f, Volume = 0.6f }, NPC.Center);
                    }
                }
                chargeVis = MathHelper.Lerp(chargeVis, crumbled ? 0.9f : 0.2f, 0.08f);

                // 升天爆发同帧: 每 5 节齐爆
                if ((int)headT == 192 && segIdx % 5 == 0 && !Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 v = Main.rand.NextVector2CircularEdge(7f, 7f);
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, v.X, v.Y, 100, default, 2.2f);
                        Main.dust[d].noGravity = true;
                    }
                }

                // 溶解: 尾→头 化作上升金芒
                if (headT >= 192f) {
                    float dissolveFront = ACMUtils.Clamp01((headT - 192f) / 52f);
                    float target = p >= 1f - dissolveFront ? 1f : 0f;
                    if (target > dissolve && dissolve < 0.1f && !Main.dedServ) {
                        for (int i = 0; i < 4; i++) {
                            int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldCoin,
                                Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(3f, 7f), 100, default, 1.6f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                    dissolve = MathHelper.Lerp(dissolve, target, 0.25f);
                }
                return;
            }
            crumbled = false;
            dissolve = MathHelper.Lerp(dissolve, 0f, 0.2f);

            // —— 剑气长虹: 快段体节涟漪微粒 (视觉波沿身滚动) ——
            if (state == StateSword && head.velocity.Length() > 24f && !Main.dedServ) {
                if ((segIdx * 8 + (int)headT) % 90 < 5 && Main.rand.NextBool(2)) {
                    Vector2 outward = (NPC.Center - head.Center).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    int d = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, outward.X * 3f, outward.Y * 3f, 120, default, 1.2f);
                    Main.dust[d].noGravity = true;
                }
            }

            // —— 金鳞游曳: 充能波沿体节头→尾传递 (0~30 预警 → 30~60 危险 → 60~90 冷却) ——
            if (state != StateCircle) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }
            float local = headT - segIdx * 4f;
            if (local < 0) {
                chargeVis = MathHelper.Lerp(chargeVis, 0f, 0.1f);
                return;
            }
            float cyc = local % 90f;
            Charging = cyc >= 30f && cyc < 60f;
            chargeVis = cyc < 30f ? cyc / 30f : (cyc < 60f ? 1f : MathHelper.Clamp(1f - (cyc - 60f) / 30f, 0f, 1f));

            if (Charging && !Main.dedServ && Main.rand.NextBool(4)) {
                int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                Main.dust[d].noGravity = true;
            }
        }

        // ============================================================
        //  运动原语
        // ============================================================

        /// <summary>确保蠕虫保持最小速度并使用宽转弯 (巡航类平滑运动)。</summary>
        private void ApplyMovement(Vector2 targetPos, float baseSpeed, float turnRate, float minSpeed) {
            Vector2 toTarget = targetPos - NPC.Center;
            float distToTarget = toTarget.Length();

            Vector2 desiredDirection = toTarget.SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX));

            float currentAngle = NPC.velocity.ToRotation();
            float targetAngle = desiredDirection.ToRotation();
            float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
            angleDiff = MathHelper.Clamp(angleDiff, -turnRate, turnRate);

            float newAngle = currentAngle + angleDiff;
            Vector2 newDirection = newAngle.ToRotationVector2();

            float targetSpeed = baseSpeed;
            if (distToTarget < 200f)
                targetSpeed = Math.Max(minSpeed, baseSpeed * 0.8f);

            float currentSpeed = NPC.velocity.Length();
            float newSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, 0.05f);
            newSpeed = Math.Max(newSpeed, minSpeed);

            NPC.velocity = newDirection * newSpeed;
        }

        /// <summary>演出/蓄势用强转向: 更高转率 + 更快的速度插值 (可减速到近停)。</summary>
        private void SteerTowards(Vector2 targetPos, float speed, float turnRate, float accel) {
            Vector2 desired = (targetPos - NPC.Center).SafeNormalize(NPC.velocity.SafeNormalize(Vector2.UnitX));
            float currentAngle = NPC.velocity.ToRotation();
            float angleDiff = MathHelper.WrapAngle(desired.ToRotation() - currentAngle);
            angleDiff = MathHelper.Clamp(angleDiff, -turnRate, turnRate);
            float newSpeed = MathHelper.Lerp(NPC.velocity.Length(), speed, accel);
            NPC.velocity = (currentAngle + angleDiff).ToRotationVector2() * MathHelper.Max(newSpeed, 0.5f);
        }

        // ============================================================
        //  绘制
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 死亡溶解后不再绘制本体 (化作金芒)
            if (dissolve > 0.95f)
                return false;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = texture.Size() / 2;
            SpriteEffects effects = SpriteEffects.None;

            if (NPC.velocity.X < 0)
                effects = SpriteEffects.FlipVertically;

            float fade = 1f - dissolve;

            // —— 头部: 全身金辉条带 (画在本体之下) + 高速残影 ——
            if (NPCWormType == WormType.Head) {
                DrawBodyRibbonLayer();

                float spd = NPC.velocity.Length();
                if (spd > 34f) {
                    // 残影只在真正的高速帧出现 (速度门控 — 常开即噪声)
                    float ghostA = MathHelper.Clamp((spd - 34f) / 26f, 0f, 1f) * 0.55f;
                    for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                        if (NPC.oldPos[i] == Vector2.Zero)
                            continue;
                        float k = 1f - i / (float)NPC.oldPos.Length;
                        Color gc = TelegraphColors.Gold * (ghostA * k);
                        gc.A = 0;
                        Vector2 gpos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                        spriteBatch.Draw(texture, gpos, null, gc, NPC.oldRot[i], origin, NPC.scale * (0.96f - i * 0.02f), effects, 0f);
                    }
                }
            }

            // 发光层 (充能时金芒更盛)
            float glowMul = (0.35f + chargeVis * 0.5f) * fade;
            Color glowColor = Color.Gold * glowMul;
            glowColor.A = 0;
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, glowColor, NPC.rotation, origin,
                NPC.scale * (1.08f + chargeVis * 0.05f), effects, 0f);

            // 本体
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, drawColor * fade, NPC.rotation, origin, NPC.scale, effects, 0f);

            return false;
        }

        /// <summary>头部收集全身链并铺设金辉流光条带; 充能波/死亡白热由头部状态推参数。</summary>
        private void DrawBodyRibbonLayer() {
            int state = (int)NPC.ai[0];
            float t = NPC.ai[1];

            float chargeWave = -1f;
            float breakHeat = 0f;
            float intensity = 0.5f + ribbonBoost * 0.5f;

            switch (state) {
                case StateCircle: {
                    // 首个波心位置: (t-45)/全身传播时长, 循环
                    float span = (SummonMax + 1) * 4f;
                    float front = (t - 45f) / span;
                    if (front > 0f)
                        chargeWave = front % 1f;
                    break;
                }
                case StateTransition:
                    chargeWave = (t * 2f % 75f) / 75f; // 连发充能波 (蓄势可读)
                    break;
                case StateDeath:
                    breakHeat = ACMUtils.Clamp01(t / 180f);
                    intensity = t < 192f ? 0.85f : MathHelper.Max(0f, 0.85f * (1f - (t - 192f) / 52f));
                    break;
                case StateIntro:
                    intensity = 0.35f + ribbonBoost * 0.55f;
                    break;
            }

            CelestialDragonVFX.DrawBodyRibbon(NPC, chargeWave, breakHeat, intensity);
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            if (NPCWormType == WormType.Head) {
                int state = (int)NPC.ai[0];
                float t = NPC.ai[1];

                // —— 入场贯天光柱 (锚点在 aimX/aimY, 前 80f) ——
                if (state == StateIntro && t < 80f) {
                    Vector2 anchor = new(aimX, aimY);
                    float grow = MathHelper.Clamp(t / 12f, 0f, 1f);
                    float fadeOut = t < 55f ? 1f : 1f - (t - 55f) / 25f;
                    CelestialDragonVFX.DrawPillar(anchor + new Vector2(0f, 460f), 2100f, 170f,
                        grow, 0.3f, 0.9f * fadeOut, TelegraphColors.Holy, TelegraphColors.Gold);
                }

                // —— 天罚蓄力: 龙口衔珠 (龙珠语法, 塌缩颤闪) ——
                if (state == StateFullScreen && t < 90f) {
                    float charge = t / 90f;
                    Vector2 mouth = NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitX) * 130f;
                    float radius = 8f + 54f * charge * charge * charge;
                    if (t >= 70f) {
                        float anticipation = (t - 70f) / 20f;
                        radius *= MathHelper.SmoothStep(1f, MathF.Cos((t - 70f) * 0.9f) * 0.07f + 0.42f, anticipation);
                    }
                    CelestialDragonVFX.DrawPearl(mouth, radius, charge, 1f);
                }

                Texture2D sparkTex = ACMAsset.Sparkle;
                if (sparkTex != null) {
                    Color sparkColor = Color.Gold;
                    sparkColor.A = 0;
                    float pulseScale = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.15f;
                    spriteBatch.Draw(sparkTex, NPC.Center - screenPos, null, sparkColor * 0.2f,
                        Main.GlobalTimeWrappedHourly * 0.5f, sparkTex.Size() / 2f, NPC.scale * 0.35f * pulseScale, SpriteEffects.None, 0f);
                }

                // 敕令法标光束系带 (金=权威/安全; 标示"这些法标供给雷雨", 破标即断束)
                if (state == StateEdict) {
                    int type = ModContent.ProjectileType<EdictBeacon>();
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile sp = Main.projectile[i];
                        if (!sp.active || sp.type != type || (int)sp.ai[1] != NPC.whoAmI)
                            continue;
                        ACMShaders.DrawBeam(NPC.Center, sp.Center, 10f, TelegraphColors.Gold, TelegraphColors.Holy, 0.7f, 1.4f, 2.0f);
                    }
                }

                // 金鳞游曳: 尾后逃逸缺口的安全标记 (金白=安全)
                if (state == StateCircle && t > 30f) {
                    NPC tail = FindTail();
                    if (tail != null) {
                        Texture2D glow = ACMAsset.SoftGlow;
                        if (glow != null) {
                            Vector2 gap = tail.Center + (tail.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 200f;
                            Color safe = TelegraphColors.Safe;
                            safe.A = 0;
                            float pulse = 0.35f + MathF.Sin((float)Main.GlobalTimeWrappedHourly * 5f) * 0.12f;
                            spriteBatch.Draw(glow, gap - screenPos, null, safe * pulse, 0f, glow.Size() / 2f, 3.2f, SpriteEffects.None, 0f);
                        }
                    }
                }

                // 金芒径向泛光 - 蓄力/破标/过场/升天瞬间
                if (bloomPulse > 0.02f)
                    ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, MathHelper.Clamp(bloomPulse, 0f, 1f), TelegraphColors.Gold, 12f, 2.4f);
            }
            else if (chargeVis > 0.02f && dissolve < 0.9f) {
                // 体节充能金脉冲 / 死亡白热
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    Color c = Color.Lerp(TelegraphColors.Gold, Color.White, chargeVis * 0.5f);
                    c.A = 0;
                    float sc = NPC.scale * (1.1f + chargeVis * 0.5f);
                    spriteBatch.Draw(glow, NPC.Center - screenPos, null, c * (0.5f * chargeVis * (1f - dissolve)), 0f, glow.Size() / 2f, sc, SpriteEffects.None, 0f);
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3, 3);
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (NPC.life <= 0 && NPCWormType == WormType.Head) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 80; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(12, 12);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                    ACMUtils.AddScreenShake(16f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 1.5f, Pitch = -0.5f }, NPC.Center);
            }
        }

        public override bool CheckActive() => false;

        public override void BossLoot(ref string name, ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }
    }
}

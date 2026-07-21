using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    /// <summary>
    /// 后卿 Hoqing —— V3 全面重做。
    /// 主题：万鬼夜行的送葬仪式（四尸祖之一，瘟疫/尸火/鬼门）。
    /// 结构：
    ///   入场「阴门显形」—— 鬼火收束 → 溶解显形 → 静止凝视 → 怒吼开战；
    ///   幕一「幽火仪仗」(100~70%) —— 手写循环：仪仗布坑 → 三连折线冲(反蓄+硬刹) → 布坑 → 幽火齐射；
    ///   幕二「疫疠扩散」(70~30%) —— 4 招随机不重复(脓雨/尸链/疫风走廊/魂灯环阵) + 每 2 招插入鬼影横掠连接器；
    ///   幕三「万鬼夜行」(30~0) —— 祭坛洗牌循环(瞬掠→绕坛蓄力→释放)×4 → 大招「鬼门开」蛇形波+慢速环；
    ///   死亡「鬼门收葬」—— CheckDead 拦截 ~205f：抽搐外泄 → 鬼门拖拽 → 15f 静默 → 白闪爆发 → 门合真死。
    /// 公平阀门：冲刺伤害速度门控(|v|>22)、走廊缺口服务器先决且以 Safe 色标注、
    /// 释放方向提前锁定、换阶段清弹、瞬移前后双预告、距离栓绳。
    /// </summary>
    [AutoloadBossHead]
    [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hoqings/")]
    internal class Hoqing : ModNPC
    {
        private int frame;
        private int frame2;
        private const int maxFrame = 4;
        internal static Asset<Texture2D> HoqingGlow;
        internal static Asset<Texture2D> HoqingEmmd;

        //====== 阶段状态机 ======
        public enum BossPhase
        {
            Despawn = -1,
            Intro = 0,
            P1_Procession = 1,      //仪仗布坑
            P1_TripleCharge = 2,    //三连折线冲
            P1_LanternVolley = 3,   //幽火齐射仪式
            Transition = 4,         //换阶段 i 帧节拍
            P2_Hover = 5,           //幕二轮替枢纽
            P2_SputumRain = 6,      //脓雨落潭
            P2_CorpseChain = 7,     //尸链复生
            P2_PlagueCorridor = 8,  //疫风走廊
            P2_LanternRing = 9,     //魂灯环阵
            P2_PhantomSweep = 10,   //鬼影横掠连接器
            P3_AltarRush = 11,      //瞬掠上坛
            P3_AltarChannel = 12,   //绕坛蓄力
            P3_AltarRelease = 13,   //祭坛释放
            P3_GhostGate = 14,      //大招·鬼门开
            DeathThroes = 15,       //死亡·鬼门收葬
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float GeneralTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        //====== 同步字段 ======
        private BossPhase pendingNextPhase;
        private bool enteredP2;
        private bool enteredP3;
        private int p1CycleIndex;    //幕一手写循环位置
        private int lastP2Attack;    //幕二上一招（随机不重复）
        private int sweepCounter;    //幕二自上次横掠以来的招数
        private int comboCount;      //幕内循环计数（冲刺轮数/大招波数）
        private int corridorGap;     //疫风走廊缺口列（服务器先决）
        private Vector2 corridorAnchor; //走廊锚点（锁定的中心）
        private Vector2 laneDir;     //冲刺/横掠/尸链方向
        private Vector2 arenaCenter; //幕三锚点中心
        private int altarOrderCode;  //祭坛访问序 Lehmer 编码 (0..23)
        private int altarStep;       //本轮第几座祭坛 (0..3)
        private float releaseAngle;  //蓄力释放方向（提前锁定）
        private Vector2 deathGate;   //死亡演出鬼门位置
        private bool deathFinished;  //死亡演出完成，放行 CheckDead

        //====== 非同步演出（各端各算, 纯本地视觉）======
        private float channelGlow;   //蓄力进度 0~1
        private float plagueAccum;   //幕三疫源累积 0~1 (地纹/经络主控)
        private float fogWarp;       //限视尸雾 0~1 (GenericWarp·fog 全屏后处理)
        private float bodyReveal = 1f; //本体显形度 (DissolveBurn)
        private bool goreBurstDone;  //死亡爆点 gore 只放一次

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = maxFrame;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 400000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            //专属 BGM：更契合"万鬼夜行"主题的地府主题。
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HoqingFireSummon>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedHoqing = true;
            if (!VaultUtils.isServer) {
                PunchCameraModifier modifier = new(NPC.Center,
                    (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 16f, 8f, 45, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)pendingNextPhase);
            writer.Write(enteredP2);
            writer.Write(enteredP3);
            writer.Write(p1CycleIndex);
            writer.Write(lastP2Attack);
            writer.Write(sweepCounter);
            writer.Write(comboCount);
            writer.Write(corridorGap);
            writer.WriteVector2(corridorAnchor);
            writer.WriteVector2(laneDir);
            writer.WriteVector2(arenaCenter);
            writer.Write(altarOrderCode);
            writer.Write(altarStep);
            writer.Write(releaseAngle);
            writer.WriteVector2(deathGate);
            writer.Write(deathFinished);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            pendingNextPhase = (BossPhase)reader.ReadInt32();
            enteredP2 = reader.ReadBoolean();
            enteredP3 = reader.ReadBoolean();
            p1CycleIndex = reader.ReadInt32();
            lastP2Attack = reader.ReadInt32();
            sweepCounter = reader.ReadInt32();
            comboCount = reader.ReadInt32();
            corridorGap = reader.ReadInt32();
            corridorAnchor = reader.ReadVector2();
            laneDir = reader.ReadVector2();
            arenaCenter = reader.ReadVector2();
            altarOrderCode = reader.ReadInt32();
            altarStep = reader.ReadInt32();
            releaseAngle = reader.ReadSingle();
            deathGate = reader.ReadVector2();
            deathFinished = reader.ReadBoolean();
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        private int GetBossDamage(float scaling = 1f) => (int)(NPC.defDamage * scaling);

        private bool InP1Family => Phase is BossPhase.P1_Procession or BossPhase.P1_TripleCharge or BossPhase.P1_LanternVolley;
        private bool InP2Family => Phase is BossPhase.P2_Hover or BossPhase.P2_SputumRain or BossPhase.P2_CorpseChain
            or BossPhase.P2_PlagueCorridor or BossPhase.P2_LanternRing or BossPhase.P2_PhantomSweep;
        private bool InP3Family => Phase is BossPhase.P3_AltarRush or BossPhase.P3_AltarChannel
            or BossPhase.P3_AltarRelease or BossPhase.P3_GhostGate;

        private void SetPhase(BossPhase next) {
            Phase = next;
            PhaseTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private void TransitionTo(BossPhase next) {
            pendingNextPhase = next;
            SetPhase(BossPhase.Transition);
            comboCount = 0;
        }

        //====== 祭坛访问序：Lehmer 编码解码（无分配）======
        private int AltarAt(int step) {
            Span<int> items = stackalloc int[4] { 0, 1, 2, 3 };
            int code = ((altarOrderCode % 24) + 24) % 24;
            step = Math.Clamp(step, 0, 3);
            for (int i = 0; i < 4; i++) {
                int f = i switch { 0 => 6, 1 => 2, _ => 1 };
                int idx = code / f;
                code %= f;
                int val = items[idx];
                for (int j = idx; j < 3 - i; j++) {
                    items[j] = items[j + 1];
                }
                if (i == step) {
                    return val;
                }
            }
            return 0;
        }

        private Vector2 GetAltarPos(int index) {
            float r = 520f;
            return arenaCenter + (MathHelper.PiOver2 * index + MathHelper.PiOver4).ToRotationVector2() * r;
        }

        private Vector2 GatePos => arenaCenter + new Vector2(0, -260);

        public override void AI() {
            //死亡演出优先：不再依赖目标存活，脚本走完为止
            if (Phase == BossPhase.DeathThroes) {
                RunDeathThroes();
                UpdatePresentation();
                GeneralTimer++;
                PhaseTimer++;
                FindFrame(2);
                return;
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives() && Phase != BossPhase.Despawn) {
                    Phase = BossPhase.Despawn;
                    PhaseTimer = 0;
                    NPC.netUpdate = true;
                }
            }

            //疫疠之光：偏冷的尸绿
            Lighting.AddLight(NPC.Center, new Color(80, 200, 110).ToVector3() * NPC.scale * (0.8f + 0.5f * channelGlow));

            if (GeneralTimer == 0 && !VaultUtils.isServer && !SkyManager.Instance[HoqingSky.name].IsActive()) {
                SkyManager.Instance.Activate(HoqingSky.name);
            }

            //接触伤害默认开启, 各状态自行关闭/门控
            NPC.damage = NPC.defDamage;
            int targetFrame = 0;
            bool setNPCRot = true;

            switch (Phase) {
                case BossPhase.Despawn:
                    NPC.damage = 0;
                    NPC.velocity = new Vector2(0, 60);
                    if (PhaseTimer > 180) {
                        NPC.active = false;
                        NPC.netUpdate = true;
                    }
                    break;
                case BossPhase.Intro:
                    RunIntro(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P1_Procession:
                    RunProcession(target, ref targetFrame);
                    break;
                case BossPhase.P1_TripleCharge:
                    RunTripleCharge(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P1_LanternVolley:
                    RunLanternVolley(target, ref targetFrame);
                    break;
                case BossPhase.Transition:
                    RunTransition(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P2_Hover:
                    RunHover(target, ref targetFrame);
                    break;
                case BossPhase.P2_SputumRain:
                    RunSputumRain(target, ref targetFrame);
                    break;
                case BossPhase.P2_CorpseChain:
                    RunCorpseChain(target, ref targetFrame);
                    break;
                case BossPhase.P2_PlagueCorridor:
                    RunPlagueCorridor(target, ref targetFrame);
                    break;
                case BossPhase.P2_LanternRing:
                    RunLanternRing(target, ref targetFrame);
                    break;
                case BossPhase.P2_PhantomSweep:
                    RunPhantomSweep(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P3_AltarRush:
                    RunAltarRush(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P3_AltarChannel:
                    RunAltarChannel(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.P3_AltarRelease:
                    RunAltarRelease(target, ref targetFrame);
                    break;
                case BossPhase.P3_GhostGate:
                    RunGhostGate(target, ref targetFrame);
                    break;
            }

            //HP 门控：过阈值改变规则（带 i 帧过渡节拍），而非加速
            if (!VaultUtils.isClient) {
                if (!enteredP2 && NPC.life <= NPC.lifeMax * 0.7f && InP1Family) {
                    enteredP2 = true;
                    TransitionTo(BossPhase.P2_Hover);
                }
                else if (!enteredP3 && NPC.life <= NPC.lifeMax * 0.3f && InP2Family) {
                    enteredP3 = true;
                    TransitionTo(BossPhase.P3_AltarRush);
                }
            }

            UpdatePresentation();

            GeneralTimer++;
            PhaseTimer++;
            if (setNPCRot) {
                NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.02f, 0.1f);
            }
            FindFrame(targetFrame);
        }

        //========================= 演出标量（各端各算） =========================
        private void UpdatePresentation() {
            //疫源累积: 幕三家族累积, 死亡时快速熄灭
            bool p3 = InP3Family;
            if (Phase == BossPhase.DeathThroes) {
                plagueAccum = MathHelper.Lerp(plagueAccum, 0f, 0.10f);
            }
            else if (p3) {
                plagueAccum = MathHelper.Clamp(plagueAccum + 1f / 540f, 0f, 1f);
            }
            else if (Phase == BossPhase.Transition && pendingNextPhase == BossPhase.P3_AltarRush && PhaseTimer > 50) {
                //进幕三过渡后半段: 祭坛依次点亮的加速累积
                plagueAccum = MathHelper.Clamp(plagueAccum + 1f / 90f, 0f, 0.55f);
            }
            else {
                plagueAccum = MathHelper.Lerp(plagueAccum, 0f, 0.05f);
            }

            //限视尸雾目标强度
            float fogTarget = 0f;
            if (Phase == BossPhase.P3_GhostGate && PhaseTimer is > 60 and < 320) {
                fogTarget = 0.72f;
            }
            else if (p3) {
                fogTarget = 0.45f;
            }
            else if (Phase == BossPhase.DeathThroes && PhaseTimer < 150) {
                fogTarget = 0.5f;
            }
            fogWarp = MathHelper.Lerp(fogWarp, fogTarget, 0.04f);

            //蓄力辉光
            if (Phase == BossPhase.P3_AltarChannel) {
                channelGlow = MathHelper.Clamp(PhaseTimer / 100f, 0f, 1f);
            }
            else if (Phase == BossPhase.P3_GhostGate && PhaseTimer is >= 30 and < 120) {
                channelGlow = (PhaseTimer - 30) / 90f;
            }
            else {
                channelGlow = MathHelper.Lerp(channelGlow, 0f, 0.12f);
            }

            //本体显形度
            bodyReveal = ComputeBodyReveal();

            if (VaultUtils.isServer) {
                return;
            }

            //—— 发布祭坛/经络/蓄力标量 ——
            if (plagueAccum > 0.01f || channelGlow > 0.01f) {
                int activeAltar = AltarAt(altarStep);
                bool isFan = activeAltar % 2 == 0;
                HoqingScreenSystem.Publish(arenaCenter, 520f, plagueAccum,
                    activeAltar, channelGlow, isFan, (float)Main.GlobalTimeWrappedHourly);
            }

            //—— 发布鬼门标量 ——
            float open = 0f, flash = 0f;
            Vector2 gateCenter = Vector2.Zero;
            float gateHalfH = 300f;
            if (Phase == BossPhase.Intro && PhaseTimer < 56) {
                //入场门缝: 一开一合的裂隙
                open = MathF.Sin(MathHelper.Pi * MathHelper.Clamp(PhaseTimer / 56f, 0f, 1f)) * 0.24f;
                gateCenter = NPC.Center;
                gateHalfH = 170f;
            }
            else if (Phase == BossPhase.P3_GhostGate) {
                gateCenter = GatePos;
                gateHalfH = 320f;
                if (PhaseTimer < 30) {
                    open = 0f;
                }
                else if (PhaseTimer < 120) {
                    float p = (PhaseTimer - 30) / 90f;
                    open = p * p * (3f - 2f * p); //smoothstep 撕开
                }
                else if (PhaseTimer < 300) {
                    open = 1f;
                }
                else {
                    open = MathHelper.Clamp(1f - (PhaseTimer - 300) / 45f, 0f, 1f);
                }
            }
            else if (Phase == BossPhase.DeathThroes) {
                gateCenter = deathGate == Vector2.Zero ? NPC.Center : deathGate;
                gateHalfH = 260f;
                if (PhaseTimer is >= 50 and < 140) {
                    open = MathHelper.Clamp((PhaseTimer - 50) / 90f, 0f, 1f);
                }
                else if (PhaseTimer is >= 140 and < 155) {
                    open = 1f;
                }
                else if (PhaseTimer >= 155) {
                    open = MathHelper.Clamp(1f - (PhaseTimer - 155) / 45f, 0f, 1f);
                    flash = MathHelper.Clamp(1f - (PhaseTimer - 155) / 20f, 0f, 1f);
                }
            }
            HoqingScreenSystem.PublishGate(gateCenter, gateHalfH, open, flash);
        }

        private float ComputeBodyReveal() {
            switch (Phase) {
                case BossPhase.Intro:
                    if (PhaseTimer < 40) return 0f;
                    return MathHelper.Clamp((PhaseTimer - 40) / 40f, 0f, 1f);
                case BossPhase.P2_PhantomSweep:
                    if (SubState == 0) return MathHelper.Clamp(1f - PhaseTimer / 20f, 0f, 1f);
                    if (SubState == 1) return MathHelper.Clamp(PhaseTimer / 40f, 0f, 1f);
                    return 1f;
                case BossPhase.DeathThroes:
                    if (PhaseTimer > 155) return MathHelper.Clamp(1f - (PhaseTimer - 155) / 45f, 0f, 1f);
                    return 1f;
                default:
                    return 1f;
            }
        }

        //========================= 入场「阴门显形」 =========================
        private void RunIntro(Player target, ref int targetFrame, ref bool setNPCRot) {
            targetFrame = 2;
            setNPCRot = false;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);

            if (PhaseTimer == 1) {
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.6f, Volume = 0.8f }, NPC.Center);
            }

            //0~80: 悬停原地, 鬼火向心收束
            if (PhaseTimer < 80) {
                NPC.velocity *= 0.9f;
                if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 off = Main.rand.NextVector2CircularEdge(150, 150) * Main.rand.NextFloat(0.7f, 1.3f);
                        Dust d = Dust.NewDustPerfect(NPC.Center + off, DustID.GreenTorch
                            , -off.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f), 120, new Color(120, 255, 140), 1.8f);
                        d.noGravity = true;
                    }
                }
                if (PhaseTimer % 20 == 10) {
                    ACMScreenShakeSystem.Add(1.5f); //低频 rumble
                }
            }
            //80~110: 完全静止凝视（menace is stillness）, 仪仗队现形
            else if (PhaseTimer <= 110) {
                NPC.velocity = Vector2.Zero;
                NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;
                //眼位红尘
                if (!VaultUtils.isServer && PhaseTimer % 5 == 0) {
                    Vector2 eye = NPC.Center + new Vector2(Main.rand.NextBool() ? -26 : 26, -8);
                    Dust d = Dust.NewDustPerfect(eye, DustID.LifeDrain, new Vector2(0, 0.6f), 100, default, 1.3f);
                    d.noGravity = true;
                }
                //仪仗队 6 只依次现形
                if (!VaultUtils.isClient && PhaseTimer >= 82 && PhaseTimer <= 107 && PhaseTimer % 5 == 2) {
                    int idx = (int)(PhaseTimer - 82) / 5;
                    if (!AnyServant(idx)) {
                        NPC.NewNPCDirect(NPC.GetSource_FromAI(), NPC.Center + Main.rand.NextVector2Circular(60, 60)
                            , ModContent.NPCType<GhostFire>(), ai0: NPC.whoAmI, ai1: idx, target: NPC.target);
                    }
                }
                if (PhaseTimer == 110) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    ACMScreenShakeSystem.Add(12f);
                    HoqingSky.TriggerFlash(0.35f);
                }
            }
            //110~150: 缓推入战位
            else {
                Vector2 desired = target.Center + new Vector2(0, -360);
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(desired) * 8f, 0.06f);
            }

            if (PhaseTimer > 150) {
                NPC.dontTakeDamage = false;
                p1CycleIndex = 0;
                comboCount = 0;
                SetPhase(BossPhase.P1_Procession);
            }
        }

        private bool AnyServant(int slot) {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == ModContent.NPCType<GhostFire>() && (int)n.ai[0] == NPC.whoAmI && (int)n.ai[1] == slot) {
                    return true;
                }
            }
            return false;
        }

        //幕一手写循环表：布坑 → 冲撞 → 布坑 → 齐射
        private void AdvanceP1() {
            p1CycleIndex = (p1CycleIndex + 1) % 4;
            comboCount = 0;
            BossPhase next = p1CycleIndex switch {
                1 => BossPhase.P1_TripleCharge,
                3 => BossPhase.P1_LanternVolley,
                _ => BossPhase.P1_Procession,
            };
            SetPhase(next);
        }

        //========================= 幕一 A1: 仪仗布坑 =========================
        private void RunProcession(Player target, ref int targetFrame) {
            targetFrame = 1;

            //仆从被清空时补阵（现形有 32f 溶解预告，期间不开火）
            if (!VaultUtils.isClient && PhaseTimer == 1 && !NPC.AnyNPCs(ModContent.NPCType<GhostFire>())) {
                for (int i = 0; i < 6; i++) {
                    NPC.NewNPCDirect(NPC.GetSource_FromAI(), NPC.Center + Main.rand.NextVector2Circular(80, 80)
                        , ModContent.NPCType<GhostFire>(), ai0: NPC.whoAmI, ai1: i, target: NPC.target);
                }
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.4f }, NPC.Center);
            }

            //侧上方悬停游走，保持压迫
            Vector2 hover = target.Center + new Vector2((target.Center.X < NPC.Center.X ? 1 : -1) * 340, -200);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 10f, 0.055f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //预告尸坑：先预告再喷发（见 CorpsePit）
            if (!VaultUtils.isClient && (PhaseTimer == 40 || PhaseTimer == 110 || PhaseTimer == 180)) {
                Vector2 pit = target.Center + new Vector2(Main.rand.Next(-340, 340), Main.rand.Next(-160, 160));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), pit, Vector2.Zero
                    , ModContent.ProjectileType<CorpsePit>(), GetBossDamage(0.8f), 0f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item104 with { Pitch = -0.3f }, pit);
            }

            if (PhaseTimer > 200) {
                AdvanceP1();
            }
        }

        //========================= 幕一 A2: 三连折线冲 =========================
        private void RunTripleCharge(Player target, ref int targetFrame, ref bool setNPCRot) {
            const int WindupTime = 42;
            const int DashTime = 12;
            const int BrakeTime = 26;

            switch ((int)SubState) {
                case 0: { //蓄势: 就位 → 锁向 → 反向抽离
                    targetFrame = 2;
                    NPC.damage = 0; //蓄势期无接触伤害（防贴脸误伤）
                    if (PhaseTimer < 30) {
                        int side = NPC.Center.X < target.Center.X ? -1 : 1;
                        Vector2 anchor = target.Center + new Vector2(side * 480, -40);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(anchor) * 13f, 0.09f);
                        NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;
                    }
                    else {
                        //反向抽离（late-snap 反蓄, pow(u,6)）
                        float u = (PhaseTimer - 30) / (float)(WindupTime - 30);
                        NPC.velocity = -laneDir * MathF.Pow(MathHelper.Clamp(u, 0f, 1f), 6f) * 24f;
                    }

                    if (PhaseTimer == 6) {
                        //前置警示音（发射前 36f 固定预告缓冲）
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.4f }, NPC.Center);
                    }
                    if (PhaseTimer == 30 && !VaultUtils.isClient) {
                        //锁定冲线（带预判提前量）
                        laneDir = NPC.SafeDirectionTo(target.Center + target.velocity * 12f);
                        NPC.netUpdate = true;
                    }
                    //蓄势尘线（锁向后沿冲线铺设）
                    if (!VaultUtils.isServer && PhaseTimer > 30 && PhaseTimer % 2 == 0) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 p = NPC.Center + laneDir * Main.rand.NextFloat(80f, 1100f);
                            Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, laneDir * 2f, 160, new Color(255, 90, 70), 1.3f);
                            d.noGravity = true;
                        }
                    }

                    if (PhaseTimer >= WindupTime) {
                        SubState = 1;
                        PhaseTimer = 0;
                        NPC.velocity = laneDir * 46f; //launch is a set
                        NPC.oldPos = new Vector2[NPC.oldPos.Length];
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(7f);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { //冲刺: 零转向 + 速度门控伤害 + 定格残影
                    targetFrame = 3;
                    setNPCRot = false;
                    NPC.rotation = laneDir.X * 0.10f;
                    NPC.damage = NPC.velocity.Length() > 22f ? NPC.defDamage : 0;
                    if (!VaultUtils.isClient && PhaseTimer % 4 == 1) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero
                            , ModContent.ProjectileType<HoqingShadow>(), 0, 0f, Main.myPlayer);
                    }
                    if (PhaseTimer >= DashTime || NPC.collideX || NPC.collideY) {
                        SubState = 2;
                        PhaseTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: { //硬刹 + 重锁窗口
                    targetFrame = 1;
                    NPC.damage = NPC.velocity.Length() > 22f ? NPC.defDamage : 0;
                    NPC.velocity *= NPC.velocity.Length() > 7f ? 0.68f : 0.9f;
                    if (PhaseTimer >= BrakeTime) {
                        comboCount++;
                        if (comboCount >= 3) {
                            AdvanceP1();
                        }
                        else {
                            SubState = 0;
                            PhaseTimer = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
            }
        }

        //========================= 幕一 A3: 幽火齐射仪式 =========================
        private void RunLanternVolley(Player target, ref int targetFrame) {
            targetFrame = PhaseTimer < 46 ? 2 : 3;
            NPC.velocity *= 0.9f;
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //收束预告: 密度 ∝ sqrt(t), 76% 处静默（inhale）
            if (!VaultUtils.isServer && PhaseTimer < 35) {
                float p = PhaseTimer / 46f;
                if (Main.rand.NextFloat() < 0.85f * MathF.Sqrt(p)) {
                    Vector2 e = Main.rand.NextVector2CircularEdge(180, 180);
                    Dust d = Dust.NewDustPerfect(NPC.Center + e, DustID.GreenTorch
                        , -e.SafeNormalize(Vector2.Zero) * 5f, 100, new Color(150, 255, 160), 1.7f);
                    d.noGravity = true;
                }
            }

            //Boss 扇形三重速度分层
            if (PhaseTimer == 46 || PhaseTimer == 54 || PhaseTimer == 62) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.1f }, NPC.Center);
                if (!VaultUtils.isClient) {
                    float speed = 8.5f + (PhaseTimer - 46) / 8 * 2f;
                    int n = 7;
                    float baseAng = NPC.SafeDirectionTo(target.Center).ToRotation();
                    float spread = MathHelper.ToRadians(64);
                    for (int i = 0; i < n; i++) {
                        float a = baseAng + MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(n - 1));
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * speed
                            , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.75f), 2f, Main.myPlayer);
                    }
                }
            }

            //仪仗队齐射令
            if (PhaseTimer == 50 && !VaultUtils.isClient) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == ModContent.NPCType<GhostFire>() && (int)n.ai[0] == NPC.whoAmI && n.ai[3] == 0) {
                        n.ai[3] = 1;
                        n.netUpdate = true;
                    }
                }
            }

            if (PhaseTimer > 170) {
                AdvanceP1();
            }
        }

        //========================= 换阶段: i 帧节拍 =========================
        private void RunTransition(Player target, ref int targetFrame, ref bool setNPCRot) {
            targetFrame = 2;
            setNPCRot = false;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            NPC.velocity *= 0.85f;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);
            bool toP3 = pendingNextPhase == BossPhase.P3_AltarRush;
            int duration = toP3 ? 130 : 110;

            if (PhaseTimer == 1) {
                ClearHostileBullets();
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.4f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                fogWarp = MathF.Max(fogWarp, 0.8f); //疠气脉冲, 随 UpdatePresentation 回落
            }
            //疠气爆涌 → 内吸两段式
            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                bool inhale = PhaseTimer > duration * 0.45f;
                for (int i = 0; i < 6; i++) {
                    if (inhale) {
                        Vector2 e = Main.rand.NextVector2CircularEdge(240, 240);
                        Dust d = Dust.NewDustPerfect(NPC.Center + e, DustID.GreenTorch
                            , -e.SafeNormalize(Vector2.Zero) * 6f, 120, new Color(120, 255, 150), 2.0f);
                        d.noGravity = true;
                    }
                    else {
                        Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch
                            , Main.rand.NextVector2Circular(8, 8), 120, new Color(120, 255, 150), 2.2f);
                        d.noGravity = true;
                    }
                }
            }

            //进幕三: 锚定竞技场 + 祭坛洗牌 + 依次鸣钟
            if (toP3) {
                if (PhaseTimer == 50 && !VaultUtils.isClient) {
                    arenaCenter = target.Center;
                    altarStep = 0;
                    altarOrderCode = Main.rand.Next(24);
                    NPC.netUpdate = true;
                }
                if (PhaseTimer > 50 && (PhaseTimer - 50) % 18 == 0 && PhaseTimer <= 122) {
                    int idx = (int)(PhaseTimer - 50) / 18;
                    if (idx < 4) {
                        SoundEngine.PlaySound(SoundID.Item26 with { Pitch = -0.7f + idx * 0.15f }, GetAltarPos(idx));
                    }
                }
                if (PhaseTimer == 60) {
                    HoqingSky.TriggerFlash(0.25f);
                }
            }

            if (PhaseTimer > duration) {
                NPC.dontTakeDamage = false;
                comboCount = 0;
                if (pendingNextPhase == BossPhase.P2_Hover) {
                    sweepCounter = 0;
                    lastP2Attack = -1;
                }
                SetPhase(pendingNextPhase);
            }
        }

        //========================= 幕二: 轮替枢纽 =========================
        private void RunHover(Player target, ref int targetFrame) {
            targetFrame = 1;
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GeneralTimer * 0.03f) * 360f, -300f);
            float pull = NPC.Distance(target.Center) > 1400f ? 0.12f : 0.06f; //距离栓绳
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 14f, pull);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            if (PhaseTimer > 45 && !VaultUtils.isClient) {
                BossPhase next;
                if (sweepCounter >= 2) {
                    sweepCounter = 0;
                    next = BossPhase.P2_PhantomSweep;
                }
                else {
                    int pick;
                    do {
                        pick = Main.rand.Next(4);
                    } while (pick == lastP2Attack);
                    lastP2Attack = pick;
                    sweepCounter++;
                    next = pick switch {
                        0 => BossPhase.P2_SputumRain,
                        1 => BossPhase.P2_CorpseChain,
                        2 => BossPhase.P2_PlagueCorridor,
                        _ => BossPhase.P2_LanternRing,
                    };
                }
                SetPhase(next);
            }
        }

        //========================= 幕二 B1: 脓雨落潭 =========================
        private void RunSputumRain(Player target, ref int targetFrame) {
            targetFrame = PhaseTimer < 50 ? 2 : 1;
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GeneralTimer * 0.03f) * 300f, -320f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 12f, 0.06f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //预告: 5 个呼吸圈锚定玩家当前位置（跟随绘制, 与释放同源）
            if (!VaultUtils.isServer && PhaseTimer < 50 && PhaseTimer % 4 == 0) {
                float p = PhaseTimer / 50f;
                for (int k = 0; k < 5; k++) {
                    Vector2 mark = target.Center + new Vector2((k - 2) * 230, 20);
                    for (int i = 0; i < 6; i++) {
                        Vector2 e = (MathHelper.TwoPi * i / 6 + p * 2f).ToRotationVector2() * (66f * (1.2f - 0.2f * p));
                        Dust d = Dust.NewDustPerfect(mark + e, DustID.GreenTorch, Vector2.Zero, 150, new Color(120, 255, 130), 1.3f);
                        d.noGravity = true;
                    }
                }
            }
            //高空坠球（落点 = 释放帧玩家位置 ± 固定偏移, 与预告同源）
            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Item104, NPC.Center);
                if (!VaultUtils.isClient) {
                    for (int k = 0; k < 5; k++) {
                        Vector2 spawn = new(target.Center.X + (k - 2) * 230 + Main.rand.Next(-18, 18), target.Center.Y - 540);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(0, 8f)
                            , ModContent.ProjectileType<HoqingSputumGlob>(), GetBossDamage(0.7f), 0f, Main.myPlayer
                            , ai0: target.Center.Y + 26, ai1: GetBossDamage(0.7f));
                    }
                }
            }

            if (PhaseTimer > 150) {
                SetPhase(BossPhase.P2_Hover);
            }
        }

        //========================= 幕二 B2: 尸链复生 =========================
        private void RunCorpseChain(Player target, ref int targetFrame) {
            targetFrame = PhaseTimer < 50 ? 2 : 1;
            NPC.velocity *= 0.94f;
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            if (PhaseTimer == 26 && !VaultUtils.isClient) {
                laneDir = NPC.SafeDirectionTo(target.Center);
                NPC.netUpdate = true;
            }
            if (!VaultUtils.isServer && PhaseTimer is > 26 and < 50 && PhaseTimer % 4 == 0) {
                Dust d = Dust.NewDustPerfect(NPC.Center + laneDir * 60, DustID.GreenTorch, laneDir * 3f, 120, new Color(180, 255, 180), 1.6f);
                d.noGravity = true;
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Item102 with { Pitch = -0.3f }, NPC.Center);
                if (!VaultUtils.isClient) {
                    bool canRevive = NPC.CountNPCS(ModContent.NPCType<GhostFire>()) < 4;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, laneDir * 19f
                        , ModContent.ProjectileType<HoqingCorpseChain>(), GetBossDamage(0.9f), 2f
                        , Main.myPlayer, ai0: NPC.whoAmI, ai1: canRevive ? 1f : 0f);
                }
            }

            if (PhaseTimer > 140) {
                SetPhase(BossPhase.P2_Hover);
            }
        }

        //========================= 幕二 B3: 疫风走廊 =========================
        private void RunPlagueCorridor(Player target, ref int targetFrame) {
            targetFrame = 2;

            if (PhaseTimer == 1 && !VaultUtils.isClient) {
                corridorAnchor = target.Center;
                corridorGap = Main.rand.Next(-5, 6);
                NPC.netUpdate = true;
            }

            //Boss 悬于走廊上方压阵
            Vector2 hover = corridorAnchor + new Vector2(MathF.Sin(GeneralTimer * 0.04f) * 200f, -520f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 11f, 0.06f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //两波: 预告(60f/50f) → 服务器生成魂焰柱（柱自带 45f 预警）
            //第二波在第一波柱完全消散(175f)后才落, 控制同屏柱数 ≤ 12
            bool wave1Telegraph = PhaseTimer < 60;
            bool wave2Telegraph = PhaseTimer is >= 130 and < 180;
            if (!VaultUtils.isServer && corridorAnchor != Vector2.Zero
                && (wave1Telegraph || wave2Telegraph) && PhaseTimer % 3 == 0) {
                for (int i = -7; i <= 7; i++) {
                    float x = corridorAnchor.X + i * 130;
                    bool safeCol = Math.Abs(i - corridorGap) <= 1;
                    if (safeCol) {
                        //安全缝: 翠玉色升柱脉冲（明确告知可站位）
                        Vector2 p = new(x + Main.rand.NextFloat(-30, 30), corridorAnchor.Y + Main.rand.NextFloat(-200, 260));
                        Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, new Vector2(0, -2.5f), 130, TelegraphColors.Safe, 1.4f);
                        d.noGravity = true;
                    }
                    else if (Main.rand.NextBool(3)) {
                        //危险列: 顶部魂焰汇聚
                        Vector2 p = new(x + Main.rand.NextFloat(-16, 16), corridorAnchor.Y - 430 + Main.rand.NextFloat(-30, 10));
                        Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, new Vector2(0, 1.6f), 140, new Color(150, 255, 150), 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            if ((PhaseTimer == 60 || PhaseTimer == 180) && !VaultUtils.isClient) {
                for (int i = -7; i <= 7; i++) {
                    if (Math.Abs(i - corridorGap) <= 1) {
                        continue;
                    }
                    Vector2 p = new(corridorAnchor.X + i * 130, corridorAnchor.Y);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), p, Vector2.Zero
                        , ModContent.ProjectileType<HoqingSoulPillar>(), GetBossDamage(0.75f), 2f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f }, corridorAnchor);
            }

            //第二波缺口移位（服务器决定并同步）
            if (PhaseTimer == 128 && !VaultUtils.isClient) {
                int shift = (Main.rand.NextBool() ? 1 : -1) * Main.rand.Next(1, 3);
                corridorGap = Math.Clamp(corridorGap + shift, -5, 5);
                NPC.netUpdate = true;
            }

            if (PhaseTimer > 280) {
                SetPhase(BossPhase.P2_Hover);
            }
        }

        //========================= 幕二 B4: 魂灯环阵 =========================
        private void RunLanternRing(Player target, ref int targetFrame) {
            targetFrame = PhaseTimer < 40 ? 2 : 1;
            Vector2 hover = target.Center + new Vector2(0, -420f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 9f, 0.05f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.6f }, target.Center);
                if (!VaultUtils.isClient) {
                    Vector2 center = target.Center;
                    for (int i = 0; i < 8; i++) {
                        Vector2 pos = center + (MathHelper.TwoPi * i / 8).ToRotationVector2() * 430f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero
                            , ModContent.ProjectileType<HoqingSoulLantern>(), GetBossDamage(0.65f), 2f, Main.myPlayer
                            , ai1: center.X, ai2: center.Y);
                    }
                }
            }

            if (PhaseTimer > 180) {
                SetPhase(BossPhase.P2_Hover);
            }
        }

        //========================= 幕二 C: 鬼影横掠连接器 =========================
        private void RunPhantomSweep(Player target, ref int targetFrame, ref bool setNPCRot) {
            switch ((int)SubState) {
                case 0: { //消隐（残影原地, 无伤害）
                    targetFrame = 2;
                    NPC.damage = 0;
                    NPC.velocity *= 0.8f;
                    if (PhaseTimer == 1 && !VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero
                            , ModContent.ProjectileType<HoqingShadow>(), 0, 0f, Main.myPlayer);
                    }
                    if (PhaseTimer >= 20) {
                        if (!VaultUtils.isClient) {
                            int dir = Main.rand.NextBool() ? 1 : -1;
                            laneDir = new Vector2(dir, 0);
                            NPC.Center = new Vector2(target.Center.X - dir * 880f, target.Center.Y - 10f);
                            NPC.velocity = Vector2.Zero;
                        }
                        SubState = 1;
                        PhaseTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { //现形预告（40f 鬼火凝聚 + 前置警示音 + 水平预告线）
                    targetFrame = 2;
                    setNPCRot = false;
                    NPC.damage = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = 0f;
                    NPC.spriteDirection = NPC.direction = laneDir.X >= 0 ? 1 : -1;
                    if (PhaseTimer == 4) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.4f }, NPC.Center);
                    }
                    if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                        Vector2 off = Main.rand.NextVector2CircularEdge(120, 120);
                        Dust d = Dust.NewDustPerfect(NPC.Center + off, DustID.GreenTorch
                            , -off.SafeNormalize(Vector2.Zero) * 4f, 130, new Color(140, 255, 150), 1.6f);
                        d.noGravity = true;
                    }
                    if (PhaseTimer >= 40) {
                        SubState = 2;
                        PhaseTimer = 0;
                        NPC.velocity = laneDir * 46f;
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(6f);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: { //横掠（零转向 + 沿途播撒漂浮鬼火）
                    targetFrame = 3;
                    setNPCRot = false;
                    NPC.rotation = laneDir.X * 0.08f;
                    NPC.damage = NPC.velocity.Length() > 22f ? NPC.defDamage : 0;
                    if (!VaultUtils.isClient && (PhaseTimer == 6 || PhaseTimer == 14 || PhaseTimer == 22)) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, laneDir * 3f
                            , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.6f), 2f, Main.myPlayer
                            , ai0: 2f);
                    }
                    bool passed = laneDir.X * (NPC.Center.X - target.Center.X) > 500f;
                    if (passed || PhaseTimer > 36) {
                        SubState = 3;
                        PhaseTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 3: { //硬刹收势
                    targetFrame = 1;
                    NPC.damage = NPC.velocity.Length() > 22f ? NPC.defDamage : 0;
                    NPC.velocity *= 0.7f;
                    if (PhaseTimer > 24) {
                        SetPhase(BossPhase.P2_Hover);
                    }
                    break;
                }
            }
        }

        //========================= 幕三 D1: 瞬掠上坛 =========================
        private void RunAltarRush(Player target, ref int targetFrame, ref bool setNPCRot) {
            Vector2 altarPos = GetAltarPos(AltarAt(altarStep));
            Vector2 dir = NPC.SafeDirectionTo(altarPos);

            switch ((int)SubState) {
                case 0: { //反向抽离 8f
                    targetFrame = 2;
                    setNPCRot = false;
                    float u = PhaseTimer / 8f;
                    NPC.velocity = -dir * MathF.Pow(MathHelper.Clamp(u, 0f, 1f), 6f) * 20f;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);
                    if (PhaseTimer >= 8) {
                        SubState = 1;
                        PhaseTimer = 0;
                        NPC.velocity = dir * 52f;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { //冲向祭坛（零转向）
                    targetFrame = 3;
                    setNPCRot = false;
                    NPC.rotation = NPC.velocity.X * 0.006f;
                    NPC.damage = NPC.velocity.Length() > 22f ? NPC.defDamage : 0;
                    if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                        Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch
                            , Main.rand.NextVector2Circular(3, 3), 120, new Color(120, 255, 140), 1.6f);
                        d.noGravity = true;
                    }
                    if (NPC.WithinRange(altarPos, 70f) || PhaseTimer > 26) {
                        NPC.velocity *= 0.25f;
                        SetPhase(BossPhase.P3_AltarChannel);
                    }
                    break;
                }
            }
        }

        //========================= 幕三 D2: 绕坛蓄力 =========================
        private void RunAltarChannel(Player target, ref int targetFrame, ref bool setNPCRot) {
            targetFrame = 2;
            setNPCRot = false;
            int altarIdx = AltarAt(altarStep);
            bool isFan = altarIdx % 2 == 0;
            Vector2 altarPos = GetAltarPos(altarIdx);

            //绕坛慢速盘旋（不死站）
            Vector2 desired = altarPos + (PhaseTimer * 0.06f + altarIdx * 1.7f).ToRotationVector2() * 60f;
            NPC.velocity = (desired - NPC.Center) * 0.25f;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            Color glowColor = isFan ? TelegraphColors.Flame : TelegraphColors.GhostGreen;
            //收束粒子: 密度 ∝ sqrt(t), 76f 处硬切静默
            if (!VaultUtils.isServer && PhaseTimer < 76) {
                float p = PhaseTimer / 76f;
                if (Main.rand.NextFloat() < 0.9f * MathF.Sqrt(p)) {
                    Vector2 e = Main.rand.NextVector2CircularEdge(230, 230) * Main.rand.NextFloat(0.8f, 1.15f);
                    Dust d = Dust.NewDustPerfect(altarPos + e, isFan ? DustID.Torch : DustID.GreenTorch
                        , -e.SafeNormalize(Vector2.Zero) * 5.5f, 100, glowColor, 1.8f);
                    d.noGravity = true;
                }
            }

            //释放方向提前锁定（40f 预告） + 扇形边缘尘弧
            if (PhaseTimer == 60 && !VaultUtils.isClient) {
                releaseAngle = NPC.SafeDirectionTo(target.Center).ToRotation();
                NPC.netUpdate = true;
            }
            if (!VaultUtils.isServer && PhaseTimer > 60 && isFan && PhaseTimer % 2 == 0) {
                float spread = MathHelper.ToRadians(70);
                for (int s = -1; s <= 1; s += 2) {
                    float a = releaseAngle + s * spread / 2;
                    Vector2 p = NPC.Center + a.ToRotationVector2() * Main.rand.NextFloat(90f, 420f);
                    Dust d = Dust.NewDustPerfect(p, DustID.Torch, a.ToRotationVector2() * 2f, 140, TelegraphColors.Lethal, 1.3f);
                    d.noGravity = true;
                }
            }

            //近身蓄力：叠加衰朽
            if (!VaultUtils.isClient && PhaseTimer % 20 == 0) {
                foreach (Player p in Main.ActivePlayers) {
                    if (p.Alives() && p.WithinRange(NPC.Center, 360f)) {
                        p.AddBuff(ModContent.BuffType<Buffs.HoqingDecline>(), 240);
                        p.GetModPlayer<Players.HoqingDeclinePlayer>().AddDecline();
                    }
                }
            }

            if (PhaseTimer == 30) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f }, NPC.Center);
            }
            if (PhaseTimer > 100) {
                SetPhase(BossPhase.P3_AltarRelease);
            }
        }

        //========================= 幕三 D3: 祭坛释放 =========================
        private void RunAltarRelease(Player target, ref int targetFrame) {
            targetFrame = 3;
            NPC.velocity *= 0.92f;
            int altarIdx = AltarAt(altarStep);
            bool isFan = altarIdx % 2 == 0;

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(8f);
                if (!VaultUtils.isClient) {
                    if (isFan) {
                        //扇形: 13 发三速分层（锁定方向 releaseAngle）
                        float spread = MathHelper.ToRadians(70);
                        for (int ring = 0; ring < 3; ring++) {
                            float speed = 9f + ring * 2.5f;
                            int n = ring == 0 ? 5 : 4;
                            for (int i = 0; i < n; i++) {
                                float a = releaseAngle + MathHelper.Lerp(-spread / 2, spread / 2, (i + 0.5f * (ring % 2)) / MathF.Max(n - 1, 1));
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * speed
                                    , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.8f), 2f, Main.myPlayer);
                            }
                        }
                    }
                    else {
                        //全向: 双圈 24 发错位
                        for (int ring = 0; ring < 2; ring++) {
                            int n = 12;
                            float speed = ring == 0 ? 6.5f : 9f;
                            for (int i = 0; i < n; i++) {
                                float a = MathHelper.TwoPi * i / n + ring * MathHelper.Pi / n;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * speed
                                    , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.75f), 2f, Main.myPlayer);
                            }
                        }
                    }
                }
            }

            if (PhaseTimer > 48 && !VaultUtils.isClient) {
                altarStep++;
                if (altarStep >= 4) {
                    comboCount = 0;
                    SetPhase(BossPhase.P3_GhostGate);
                }
                else {
                    SetPhase(BossPhase.P3_AltarRush);
                }
            }
        }

        //========================= 幕三 D4: 大招·鬼门开 =========================
        private void RunGhostGate(Player target, ref int targetFrame) {
            targetFrame = PhaseTimer < 120 ? 2 : 3;
            Vector2 gate = GatePos;
            bool enrage = NPC.life <= NPC.lifeMax * 0.15f;
            int waveCount = enrage ? 8 : 6;

            //0~30: 冲回门前压阵位
            if (PhaseTimer <= 30) {
                Vector2 station = gate + new Vector2(0, 150);
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(station) * MathF.Min(NPC.Distance(station) * 0.1f, 30f), 0.15f);
            }
            else {
                //门前缓移压阵
                Vector2 sway = gate + new Vector2(MathF.Sin(GeneralTimer * 0.025f) * 180f, 150f + MathF.Sin(GeneralTimer * 0.017f) * 40f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(sway) * 7f, 0.05f);
            }
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //30~120: 撕门（rumble 渐强 + 收束粒子 75% 静默）
            if (PhaseTimer is > 30 and < 120) {
                float p = (PhaseTimer - 30) / 90f;
                if (PhaseTimer % 8 == 0) {
                    ACMScreenShakeSystem.Add(p * p * 4f);
                }
                if (!VaultUtils.isServer && PhaseTimer < 98 && Main.rand.NextFloat() < 0.8f * MathF.Sqrt(p)) {
                    Vector2 e = Main.rand.NextVector2CircularEdge(420, 420);
                    Dust d = Dust.NewDustPerfect(gate + e, DustID.GreenTorch
                        , -e.SafeNormalize(Vector2.Zero) * 7f, 110, new Color(140, 255, 150), 1.9f);
                    d.noGravity = true;
                }
                if (PhaseTimer == 31) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.8f }, gate);
                }
            }
            if (PhaseTimer == 118) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.6f }, gate);
                ACMScreenShakeSystem.Add(10f);
            }

            //120~300: 蛇形波 + 慢速环
            if (!VaultUtils.isClient && PhaseTimer >= 120 && PhaseTimer < 300) {
                int t = (int)PhaseTimer - 120;
                int interval = enrage ? 22 : 30; //狂暴期波距收紧, 保证 8 波在窗口内放完
                if (t % interval == 0 && comboCount < waveCount) {
                    comboCount++;
                    NPC.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item104 with { Pitch = 0.2f }, gate);
                    //蛇形波: 12 颗首尾相接, 纵向正弦留缝
                    Vector2 baseDir = (target.Center - gate).SafeNormalize(Vector2.UnitY)
                        .RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-14f, 14f)));
                    float amp = enrage ? 130f : 110f;
                    for (int i = 0; i < 12; i++) {
                        Vector2 spawn = gate - baseDir * (i * 34f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, baseDir * 9f
                            , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.7f), 2f, Main.myPlayer
                            , ai0: 1f, ai1: i * 0.55f, ai2: amp);
                    }
                }
                if (t % 60 == 20) {
                    //慢速环: 16 发背景压力
                    for (int i = 0; i < 16; i++) {
                        float a = MathHelper.TwoPi * i / 16 + t * 0.01f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), gate, a.ToRotationVector2() * 4f
                            , ModContent.ProjectileType<OblivionFireOrb>(), GetBossDamage(0.6f), 2f, Main.myPlayer);
                    }
                }
            }

            //345: 收门 → 洗牌回祭坛循环
            if (PhaseTimer > 345 && !VaultUtils.isClient) {
                altarStep = 0;
                altarOrderCode = Main.rand.Next(24);
                comboCount = 0;
                SetPhase(BossPhase.P3_AltarRush);
            }
        }

        //========================= 死亡「鬼门收葬」 =========================
        public override bool CheckDead() {
            if (deathFinished) {
                return true;
            }
            if (Phase != BossPhase.DeathThroes) {
                Phase = BossPhase.DeathThroes;
                PhaseTimer = 0;
                SubState = 0;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.velocity *= 0.2f;
                if (!VaultUtils.isClient) {
                    ClearHostileBullets();
                    DismissServants();
                }
                NPC.netUpdate = true;
            }
            return false;
        }

        private void DismissServants() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == ModContent.NPCType<GhostFire>() && (int)n.ai[0] == NPC.whoAmI) {
                    n.StrikeInstantKill();
                }
            }
        }

        private void RunDeathThroes() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            Lighting.AddLight(NPC.Center, new Color(120, 255, 140).ToVector3() * (1f + channelGlow));

            //0~50: 抽搐悬停 + 尸火外泄
            if (PhaseTimer <= 50) {
                NPC.velocity *= 0.85f;
                if (PhaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                }
                if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                    float p = PhaseTimer / 50f;
                    for (int i = 0; i < 1 + (int)(p * 3); i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(60, 60), DustID.GreenTorch
                            , new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, 8f) * p), 100, new Color(140, 255, 150), 2.0f);
                        d.noGravity = true;
                    }
                }
                if (PhaseTimer % 14 == 7) {
                    ACMScreenShakeSystem.Add(2f + PhaseTimer / 50f * 3f);
                }
            }

            //50: 锚定鬼门（身后）
            if (PhaseTimer == 50) {
                if (!VaultUtils.isClient) {
                    int dir = NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection;
                    deathGate = NPC.Center + new Vector2(-dir * 240f, -40f);
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.9f, Volume = 1.3f }, NPC.Center);
            }

            //50~140: 鬼门拖拽 + 速度线粒子流入门
            if (PhaseTimer is > 50 and <= 140 && deathGate != Vector2.Zero) {
                float p = (PhaseTimer - 50) / 90f;
                NPC.velocity = Vector2.Zero;
                NPC.Center = Vector2.Lerp(NPC.Center, deathGate, MathHelper.Lerp(0.01f, 0.06f, p * p));
                if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                    Vector2 from = deathGate + Main.rand.NextVector2CircularEdge(500, 500) * Main.rand.NextFloat(0.5f, 1f);
                    Vector2 vel = (deathGate - from).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(9f, 16f);
                    Dust d = Dust.NewDustPerfect(from, DustID.GreenTorch, vel, 100, new Color(130, 255, 150), 1.6f);
                    d.noGravity = true;
                }
                if (PhaseTimer % 16 == 3) {
                    ACMScreenShakeSystem.Add(3.5f);
                }
            }

            //140~155: 全静默 (inhale)
            if (PhaseTimer is > 140 and < 155) {
                NPC.velocity = Vector2.Zero;
            }

            //155: 爆点（白闪 + 震屏 + gore 爆发）
            if ((int)PhaseTimer == 155) {
                ACMScreenShakeSystem.Add(16f);
                HoqingSky.TriggerFlash(1f);
                SoundEngine.PlaySound(SoundID.NPCDeath62 with { Pitch = -0.3f }, NPC.Center);
                if (!VaultUtils.isServer && !goreBurstDone) {
                    goreBurstDone = true;
                    SpawnDeathGore();
                    for (int i = 0; i < 40; i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch
                            , Main.rand.NextVector2Circular(14, 14), 80, new Color(150, 255, 160), 2.6f);
                        d.noGravity = true;
                    }
                }
            }

            //155~205: 门吸余烬后合拢
            if (PhaseTimer > 155) {
                NPC.velocity = Vector2.Zero;
                if (!VaultUtils.isServer && PhaseTimer % 3 == 0 && deathGate != Vector2.Zero) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2Circular(80, 80);
                    Vector2 vel = (deathGate - from).SafeNormalize(Vector2.Zero) * 7f;
                    Dust d = Dust.NewDustPerfect(from, DustID.GreenTorch, vel, 130, new Color(110, 230, 140), 1.4f);
                    d.noGravity = true;
                }
            }

            //205: 真死（CheckDead 二次进入放行 → 掉落与 downed 标记照常）
            if (PhaseTimer >= 205 && !VaultUtils.isClient) {
                deathFinished = true;
                NPC.life = 0;
                NPC.StrikeInstantKill();
            }
        }

        private void SpawnDeathGore() {
            int Hoqing_Buttom = Mod.Find<ModGore>("Hoqing_Buttom").Type;
            int Hoqing_Left = Mod.Find<ModGore>("Hoqing_Left").Type;
            int Hoqing_Nose = Mod.Find<ModGore>("Hoqing_Nose").Type;
            int Hoqing_Top = Mod.Find<ModGore>("Hoqing_Top").Type;
            var entitySource = NPC.GetSource_Death();
            Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Nose);
            for (int i = 0; i < 2; i++) {
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Buttom);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Left);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hoqing_Top);
            }
        }

        private static void ClearHostileBullets() {
            int t1 = ModContent.ProjectileType<OblivionFireOrb>();
            int t2 = ModContent.ProjectileType<GhostFireProj>();
            int t3 = ModContent.ProjectileType<HoqingCorpseChain>();
            int t4 = ModContent.ProjectileType<HoqingSoulPillar>();
            int t5 = ModContent.ProjectileType<HoqingSoulLantern>();
            int t6 = ModContent.ProjectileType<HoqingSputumGlob>();
            int t7 = ModContent.ProjectileType<SputumPool>();
            int t8 = ModContent.ProjectileType<CorpsePit>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == t1 || proj.type == t2 || proj.type == t3 || proj.type == t4
                    || proj.type == t5 || proj.type == t6 || proj.type == t7 || proj.type == t8) {
                    proj.Kill();
                    proj.netUpdate = true;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                return;
            }
            //死亡演出已在爆点帧放过 gore，真死帧只留余烬
            if (Phase == BossPhase.DeathThroes && PhaseTimer > 150) {
                return;
            }
            if (!VaultUtils.isServer) {
                SpawnDeathGore();
            }
        }

        private new void FindFrame(int targetFrame) {
            targetFrame = Math.Clamp(targetFrame, 0, maxFrame - 1);
            if (++NPC.frameCounter > 5) {
                NPC.frameCounter = 0;
                if (frame > targetFrame) {
                    frame--;
                }
                else if (frame < targetFrame) {
                    frame++;
                }
                frame = Math.Clamp(frame, 0, maxFrame - 1);
                if (++frame2 >= maxFrame) {
                    frame2 = 0;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Texture2D glowValue = HoqingGlow.Value;
            Texture2D emmdValue = HoqingEmmd.Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            Rectangle rectangle2 = VaultUtils.GetRectangle(glowValue, frame2, maxFrame);

            //—— 预告光束（画于本体之下）——
            DrawTelegraphBeams();

            //死亡抽搐偏移（纯视觉）
            Vector2 jitter = Vector2.Zero;
            if (Phase == BossPhase.DeathThroes && PhaseTimer < 140) {
                float amp = MathHelper.Lerp(1f, 4.5f, MathHelper.Clamp(PhaseTimer / 120f, 0f, 1f));
                jitter = Main.rand.NextVector2Circular(amp, amp);
            }
            Vector2 drawCenter = NPC.Center + jitter - Main.screenPosition;

            //显形/消隐/崩解: DissolveBurn 接管本体
            if (bodyReveal < 0.98f) {
                WeaponVFX.ApplyDissolveBurn(mainValue, NPC.Center + jitter, rectangle, drawColor,
                    NPC.rotation, rectangle.Size() / 2f, NPC.scale,
                    threshold: 1f - bodyReveal, intensity: 1f,
                    edgeColor: new Color(TelegraphColors.GhostGreen.R, TelegraphColors.GhostGreen.G, TelegraphColors.GhostGreen.B, (byte)230),
                    edgeWidth: 0.12f, noiseScale: 2.6f);
                return false;
            }

            //速度门控拖尾（只在冲刺时显影）
            float speedFade = Utils.GetLerpValue(13f, 30f, NPC.velocity.Length(), true);
            if (speedFade > 0.05f) {
                float sengs = 0.3f * speedFade;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) {
                        continue; //冲刺起手刚重置的空历史点
                    }
                    Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                        , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                    spriteBatch.Draw(glowValue, drawOldPos, rectangle2, Color.White * sengs
                        , 0, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                    sengs *= 0.8f;
                }
            }

            //蓄力辉光底衬
            if (channelGlow > 0.05f && ACMAsset.SoftGlow != null) {
                int altarIdx = AltarAt(altarStep);
                Color glow = (Phase == BossPhase.P3_GhostGate || altarIdx % 2 != 0 ? TelegraphColors.GhostGreen : TelegraphColors.Flame);
                glow.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawCenter, null, glow * (0.55f * channelGlow)
                    , 0, ACMAsset.SoftGlow.Size() / 2, 4.5f + channelGlow * 1.6f, SpriteEffects.None, 0);
            }

            float pulse = 1f + 0.04f * channelGlow * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 9f);
            spriteBatch.Draw(mainValue, drawCenter, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale * pulse, SpriteEffects.None, 0);
            spriteBatch.Draw(glowValue, drawCenter, rectangle2, Color.White
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale * pulse, SpriteEffects.None, 0);
            spriteBatch.Draw(emmdValue, drawCenter, rectangle2, drawColor
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale * pulse, SpriteEffects.None, 0);
            return false;
        }

        //预告光束: 冲刺线（Lethal）/ 横掠线（Lethal）/ 尸链瞄准线（GhostGreen）
        private void DrawTelegraphBeams() {
            if (Main.dedServ) {
                return;
            }

            if (Phase == BossPhase.P1_TripleCharge && SubState == 0 && PhaseTimer > 30) {
                float p = MathHelper.Clamp((PhaseTimer - 30) / 12f, 0f, 1f);
                Color core = TelegraphColors.Lethal;
                core.A = 160;
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + laneDir * 1400f, 5f + 7f * p,
                    core, TelegraphColors.GhostGreen with { A = 60 }, 0.32f + 0.45f * p,
                    flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.4f);
            }
            else if (Phase == BossPhase.P2_PhantomSweep && SubState == 1) {
                float p = MathHelper.Clamp(PhaseTimer / 40f, 0f, 1f);
                Color core = TelegraphColors.Lethal;
                core.A = 150;
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + laneDir * 2400f, 4f + 8f * p,
                    core, TelegraphColors.NetherViolet with { A = 60 }, 0.28f + 0.5f * p,
                    flowSpeed: 2.4f, flowScale: 2.2f, coreSharp: 2.2f);
            }
            else if (Phase == BossPhase.P2_CorpseChain && PhaseTimer is > 26 and < 50) {
                float p = MathHelper.Clamp((PhaseTimer - 26) / 24f, 0f, 1f);
                Color core = TelegraphColors.GhostGreen;
                core.A = 130;
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + laneDir * 1100f, 3f + 3f * p,
                    core, TelegraphColors.NetherViolet with { A = 40 }, 0.25f + 0.3f * p,
                    flowSpeed: 1.6f, flowScale: 2.2f, coreSharp: 2.0f);
            }
        }

        // ===== 全屏 screenTarget 限视尸雾 (GenericWarp · fog) — 占本帧唯一全屏名额 =====
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || fogWarp <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(fogWarp, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uRadius"]?.SetValue(1.0f);
            fx.Parameters["uWarpScale"]?.SetValue(0.75f);
            fx.Parameters["uChroma"]?.SetValue(0.2f);
            fx.Parameters["uRadialPull"]?.SetValue(0f);
            fx.Parameters["uMode"]?.SetValue(2f); // fog
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 0.45f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }
    }
}

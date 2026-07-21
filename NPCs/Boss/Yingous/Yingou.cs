using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
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
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 赢勾 —— 黄泉守将·万刃冥王 (V3 重做)。
    /// 鬼王面具为"心"(本体), 两柄锯齿冥刃为"意"(YingouHand)。
    /// P1 冥刃试锋(双刃剑客) → P2 万刃冥阵(布阵压场) → P3 黄泉万刃(风暴与总劈)。
    /// 编排要点: 手写循环表 + 连接拍呼吸; 反向蓄势→快照爆发→硬刹余摆;
    /// 红色只出现在致命预警终段; 接触伤害窗口严格对齐爆发帧。
    /// </summary>
    [VaultLoaden("AncientChineseMythology/Textures/")]
    [AutoloadBossHead]
    internal class Yingou : ModNPC
    {
        internal static Texture2D GlaciateWave;//一个水平向右的波浪形灰度图，适合做冲击类刀光一类的效果，大小512*512
        internal static Texture2D SoftGlow;//一个模糊发光效果，圆点灰度图大小64*64
        internal static Texture2D StarTexture;//一个星光点的纹理，大小326*326

        //===== 专属着色器 (静态缓存一次, 不注册 ACMShaders) =====
        private static Asset<Effect> bladeRibbonRef;
        private static Asset<Effect> netherRiftRef;

        /// <summary>冥刃条带着色器 (刃迹/刃晕/巨刃辉带)。</summary>
        internal static Effect BladeRibbon {
            get {
                if (Main.dedServ) return null;
                bladeRibbonRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/YingouBladeRibbon", AssetRequestMode.ImmediateLoad);
                return bladeRibbonRef?.Value;
            }
        }

        /// <summary>黄泉裂隙贴花着色器 (入场/闪点/死亡收束)。</summary>
        internal static Effect NetherRift {
            get {
                if (Main.dedServ) return null;
                netherRiftRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/YingouNetherRift", AssetRequestMode.ImmediateLoad);
                return netherRiftRef?.Value;
            }
        }

        //===== 阶段机 =====
        public enum BossPhase
        {
            Intro = 0,
            Reposition,     //连接拍: 归位呼吸
            CrossLunge,     //双刃剪杀
            CorpseFan,      //尸火喷吐
            IaiLine,        //居合·一文字
            ViceClamp,      //冥狱合葬 (P2+)
            BladeMatrix,    //万刃冥阵 (P2+)
            FrenzyPursuit,  //狂暴追猎 (P2+)
            BladeStorm,     //万刃归宗 (P3)
            NetherCleave,   //黄泉三裂 (P3)
            Transition2,    //70% 换阶段演出
            Transition3,    //35% 换阶段演出
            Death,          //死亡演出
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];

        /// <summary>攻击内子状态 (ai[2], 引擎自动同步, 手部零成本读取)。</summary>
        public int SubState {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        /// <summary>子状态计时 (ai[3], 每帧本地自增)。</summary>
        public ref float SubTimer => ref NPC.ai[3];

        public const float Phase2Threshold = 0.70f;
        public const float Phase3Threshold = 0.35f;
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        //===== 同步字段 (SendExtraAI) =====
        private int attackCycleIndex;   //循环表游标
        internal int ComboCount;        //攻击内循环计数 (公开给手部)
        internal float AimAngle;        //服务器裁定的角度/侧别
        internal Vector2 AimPoint;      //服务器裁定的锁定点
        private int currentPattern = -1;//万刃冥阵当前图案
        private int lastPattern = -1;   //上一图案 (防重复)
        private int rampTimer;          //转场后弹速渐升阀 (90→0)
        private bool didT2;
        private bool didT3;
        private bool spawnHands = true;

        /// <summary>转场安全阀: 弹幕速度倍率 0.6→1.0。</summary>
        internal float SpeedRamp => rampTimer > 0 ? MathHelper.Lerp(1f, 0.6f, rampTimer / 90f) : 1f;

        //===== 手部引用 (视觉/指挥, 每端本地解析) =====
        internal YingouHand leftHand;
        internal YingouHand rightHand;

        //===== 本地视觉状态 (不联网, 由已同步态驱动) =====
        private float climaxBloom;              //高潮径向泛光
        private Vector2 climaxBloomCenter;
        private Color climaxBloomColor = TelegraphColors.Gold;
        private float saberTell;                //地纹环强度
        private Vector2 saberTellCenter;
        private float saberTellRadius;
        private Color saberTellColor = TelegraphColors.Lethal;
        private float ambientTint;              //冥刃染屏
        private float chargeGlow;               //蓄力聚光
        private float eyeFlash;                 //双目暴亮
        private float kingGlow;                 //"王"字金辉 (P3 常驻)
        private float bladeHalo;                //P3 幻影刃晕
        private float whiteFlash;               //全屏白闪 (T3 短闪 / 死亡白帧)
        private float trembleAmp;               //面具震颤幅度
        private float dissolveT;                //死亡灼烧进度
        private float rotSpring;                //姿态倾斜弹簧速度
        private int blinkGrace;                 //闪现后接触伤害宽限
        private int nextBeepIdx;                //死亡警报音游标
        private float introRift;                //入场裂隙强度
        private float introRiftCollapse;
        private Vector2 riftCenter;             //裂隙世界坐标 (入场/死亡共用)
        private float deathRift;                //死亡裂隙强度
        private float deathRiftCollapse;

        //死亡警报音手调加速数列 (帧间隔; 前 9 项和 107 ≤ 上浮段 110f 窗口, 十响全落窗内)
        private static readonly int[] DeathBeepGaps = { 22, 18, 15, 13, 11, 9, 8, 6, 5, 4 };

        //===== 循环表 (PACING §2: 攻击序列即编排) =====
        private static readonly BossPhase[] CycleP1 = {
            BossPhase.CrossLunge, BossPhase.CorpseFan, BossPhase.IaiLine,
        };
        private static readonly BossPhase[] CycleP2 = {
            BossPhase.BladeMatrix, BossPhase.FrenzyPursuit, BossPhase.ViceClamp,
            BossPhase.CrossLunge, BossPhase.CorpseFan, BossPhase.IaiLine,
        };
        private static readonly BossPhase[] CycleP3 = {
            BossPhase.BladeStorm, BossPhase.FrenzyPursuit, BossPhase.NetherCleave,
            BossPhase.ViceClamp, BossPhase.BladeMatrix, BossPhase.FrenzyPursuit,
        };

        #region 基础定义

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 110;
            NPC.height = 110;
            NPC.damage = 72;
            NPC.defense = 40;
            NPC.lifeMax = 420000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.Roar;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.npcSlots = 15f;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YingouKnife>()));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<CoffinNailFragment>(), 3));
        }

        public override void OnKill() {
            DownedBossSystem.downedYingou = true;
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            SubState = 0;
            SubTimer = 0;
            riftCenter = NPC.Center;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(attackCycleIndex);
            writer.Write(ComboCount);
            writer.Write(AimAngle);
            writer.WriteVector2(AimPoint);
            writer.Write(currentPattern);
            writer.Write(lastPattern);
            writer.Write(rampTimer);
            writer.Write(didT2);
            writer.Write(didT3);
            writer.WriteVector2(riftCenter);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            attackCycleIndex = reader.ReadInt32();
            ComboCount = reader.ReadInt32();
            AimAngle = reader.ReadSingle();
            AimPoint = reader.ReadVector2();
            currentPattern = reader.ReadInt32();
            lastPattern = reader.ReadInt32();
            rampTimer = reader.ReadInt32();
            didT2 = reader.ReadBoolean();
            didT3 = reader.ReadBoolean();
            riftCenter = reader.ReadVector2();
        }

        public override bool CheckActive() => false;

        /// <summary>演出无敌: 入场落位前 / 换阶段 / 死亡脚本期间不受伤不造成接触伤害。</summary>
        internal bool InCinematic =>
            Phase == BossPhase.Transition2 || Phase == BossPhase.Transition3 || Phase == BossPhase.Death ||
            (Phase == BossPhase.Intro && PhaseTimer < 130);

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return !InCinematic && blinkGrace <= 0;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) return;
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.CorruptTorch, hit.HitDirection * 2f, -1f, 150, default, 1.4f);
                d.noGravity = true;
            }
        }

        #endregion

        #region 阶段调度

        private void TransitionTo(BossPhase next) {
            Phase = next;
            PhaseTimer = 0;
            SubState = 0;
            SubTimer = 0;
            ComboCount = 0;
            NPC.netUpdate = true;
        }

        private void SetSub(int sub) {
            SubState = sub;
            SubTimer = 0;
            NPC.netUpdate = true;
        }

        /// <summary>从当前阶段的循环表取下一招。</summary>
        private void NextAttackFromCycle() {
            BossPhase[] cycle = IsPhase3 ? CycleP3 : (IsPhase2 ? CycleP2 : CycleP1);
            if (attackCycleIndex >= cycle.Length) attackCycleIndex = 0;
            BossPhase next = cycle[attackCycleIndex];
            attackCycleIndex = (attackCycleIndex + 1) % cycle.Length;
            TransitionTo(next);
        }

        /// <summary>血量断点: 70% / 35% 各触发一次换阶段演出 (清弹 + 短无敌 + 弹速阀)。</summary>
        private void CheckPhaseTransition() {
            if (Phase == BossPhase.Intro || Phase == BossPhase.Death ||
                Phase == BossPhase.Transition2 || Phase == BossPhase.Transition3)
                return;
            if (!didT2 && IsPhase2) {
                didT2 = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Transition2);
            }
            else if (!didT3 && IsPhase3) {
                didT3 = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Transition3);
            }
        }

        /// <summary>清空本 Boss 全部敌方弹幕 (换阶段/死亡公平阀)。</summary>
        internal static void ClearHostileProjectiles() {
            if (VaultUtils.isClient) return;
            int fire = ModContent.ProjectileType<YingouFireBall>();
            int hell = ModContent.ProjectileType<SaberHell>();
            int killer = ModContent.ProjectileType<SaberKiller>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (!proj.hostile) continue;
                if (proj.type == fire || proj.type == hell || proj.type == killer)
                    proj.Kill();
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            //生成双刃 (服务器)
            if (spawnHands) {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, 1);
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, -1);
                }
            }

            Main.dayTime = false;

            ResolveHandReferences();

            if (!VaultUtils.isServer && !SkyManager.Instance[YingouSky.name].IsActive())
                SkyManager.Instance.Activate(YingouSky.name);

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives() && Phase != BossPhase.Death) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.velocity *= 0.96f;
                    NPC.velocity.Y -= 0.3f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            SubTimer++;
            if (rampTimer > 0) rampTimer--;
            if (blinkGrace > 0) blinkGrace--;

            //演出期无敌集中管理 (避免各分支残留)
            NPC.dontTakeDamage = InCinematic || Phase == BossPhase.Death;

            //本地视觉衰减 (各阶段代码本帧可再抬高)
            climaxBloom *= 0.9f;
            if (climaxBloom < 0.01f) climaxBloom = 0f;
            saberTell = MathHelper.Lerp(saberTell, 0f, 0.12f);
            ambientTint = MathHelper.Lerp(ambientTint, 0f, 0.05f);
            chargeGlow = MathHelper.Lerp(chargeGlow, 0f, 0.1f);
            eyeFlash *= 0.94f;
            whiteFlash *= 0.86f;
            trembleAmp = MathHelper.Lerp(trembleAmp, 0f, 0.1f);
            if (IsPhase3) {
                kingGlow = MathHelper.Lerp(kingGlow, 1f, 0.03f);
                bladeHalo = MathHelper.Lerp(bladeHalo, 1f, 0.02f);
            }

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Reposition: RunReposition(target); break;
                case BossPhase.CrossLunge: RunCrossLunge(target); break;
                case BossPhase.CorpseFan: RunCorpseFan(target); break;
                case BossPhase.IaiLine: RunIaiLine(target); break;
                case BossPhase.ViceClamp: RunViceClamp(target); break;
                case BossPhase.BladeMatrix: RunBladeMatrix(target); break;
                case BossPhase.FrenzyPursuit: RunFrenzyPursuit(target); break;
                case BossPhase.BladeStorm: RunBladeStorm(target); break;
                case BossPhase.NetherCleave: RunNetherCleave(target); break;
                case BossPhase.Transition2: RunTransition2(target); break;
                case BossPhase.Transition3: RunTransition3(target); break;
                case BossPhase.Death: RunDeath(target); break;
            }

            //姿态倾斜: 速度带动的弹簧侧倾 (面具的"重量")
            float targetRot = MathHelper.Clamp(NPC.velocity.X * 0.006f, -0.22f, 0.22f);
            NPC.rotation = ACMUtils.SpringDamp(NPC.rotation, targetRot, ref rotSpring, 40f, 9f, 1f / 60f);

            Lighting.AddLight(NPC.Center, new Vector3(0.25f, 0.5f, 0.42f) * (0.8f + chargeGlow));

            //发布冥刃染屏氛围 (廉价 overlay, 不占全屏后处理名额)
            if (!VaultUtils.isServer)
                YingouScreenSystem.Publish(ambientTint, (float)Main.GlobalTimeWrappedHourly);
        }

        private void ResolveHandReferences() {
            if (leftHand != null && rightHand != null &&
                leftHand.NPC.active && rightHand.NPC.active &&
                leftHand.NPC.ai[0] == NPC.whoAmI && rightHand.NPC.ai[0] == NPC.whoAmI)
                return;
            leftHand = rightHand = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is YingouHand hand && (int)npc.ai[0] == NPC.whoAmI) {
                    if (npc.ai[1] > 0) rightHand = hand;
                    else leftHand = hand;
                }
            }
        }

        /// <summary>速度转向式悬停移动 (非位置贴附, 保留惯性质感)。</summary>
        private void HoverMove(Vector2 anchor, float maxSpeed, float steer) {
            Vector2 to = anchor - NPC.Center;
            float dist = to.Length();
            Vector2 desired = to.SafeNormalize(Vector2.Zero) * MathF.Min(dist * 0.06f, maxSpeed);
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, steer);
        }

        internal int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = getOrigDamage ? NPC.defDamage : NPC.damage;
            return (int)(num * scaling);
        }

        private void TriggerClimaxBloom(Vector2 worldCenter, Color color) {
            climaxBloom = 1f;
            climaxBloomCenter = worldCenter;
            climaxBloomColor = color;
        }

        private void PlayBeep(float pitch, float volume = 0.75f) {
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, NPC.Center);
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            //裂隙锚点: 出生点上方定格
            if (PhaseTimer <= 2) {
                riftCenter = target.Center + new Vector2(0, -360);
                NPC.Center = riftCenter;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }

            //0~70f: 裂隙撕开, 面具自深处贴脸而来 (fake-Z 三次方)
            if (PhaseTimer <= 70) {
                float t = PhaseTimer / 70f;
                introRift = MathHelper.Lerp(introRift, 1f, 0.08f);
                NPC.Center = riftCenter + new Vector2(0, 44f * ACMUtils.QuadOut(t));
                NPC.velocity = Vector2.Zero;
                ambientTint = Math.Max(ambientTint, t * 0.3f);
                //鬼火自隙口倾泻
                if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 off = Main.rand.NextVector2CircularEdge(150, 150);
                        Dust d = Dust.NewDustDirect(riftCenter + off * Main.rand.NextFloat(0.4f, 1f), 0, 0, DustID.CorruptTorch, 0, 0, 120, default, Main.rand.NextFloat(1.4f, 2.4f));
                        d.noGravity = true;
                        d.velocity = off.SafeNormalize(Vector2.Zero).RotatedBy(0.8f) * 3f + new Vector2(0, -1.5f);
                    }
                }
            }
            //70~130f: 静止拍 — 威慑主要是静止 (双刃在 76/96f 自裂隙刺出, 由手部处理)
            else if (PhaseTimer <= 130) {
                NPC.velocity *= 0.9f;
                ambientTint = Math.Max(ambientTint, 0.32f);
                if (PhaseTimer == 76 || PhaseTimer == 96) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 1.1f }, riftCenter);
                }
            }
            //130f: 双目暴亮 + 低吼 + 定格
            if (PhaseTimer == 130) {
                eyeFlash = 1f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                TriggerClimaxBloom(NPC.Center, TelegraphColors.GhostGreen);
                if (!VaultUtils.isServer) {
                    for (int k = 0; k < 40; k++) {
                        Vector2 vel = Main.rand.NextVector2Circular(11, 11);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.CorruptTorch, vel.X, vel.Y, 120, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }
            //130~180f: 裂隙塌缩, 面具落位
            if (PhaseTimer > 130) {
                introRiftCollapse = MathHelper.Clamp((PhaseTimer - 130) / 40f, 0f, 1f);
                introRift = MathHelper.Lerp(introRift, 1f - introRiftCollapse, 0.2f);
                HoverMove(target.Center + new Vector2(0, -300), 8f, 0.06f);
            }

            if (PhaseTimer > 180) {
                introRift = 0f;
                attackCycleIndex = 0;
                TransitionTo(BossPhase.Reposition);
            }
        }

        #endregion

        #region 连接拍

        private void RunReposition(Player target) {
            //面具滑向玩家侧上方悬点; 双刃归鞘 (手部姿态)
            float side = MathF.Sign(NPC.Center.X - target.Center.X);
            if (side == 0) side = 1;
            Vector2 anchor = target.Center + new Vector2(side * 340, -230);
            HoverMove(anchor, 16f, 0.1f);

            if (PhaseTimer == 6)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f, Volume = 0.45f }, NPC.Center);

            //到位早退 (§3 不为自己的计时表干等)
            if (NPC.Center.Distance(anchor) < 130f || PhaseTimer > 60) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NextAttackFromCycle();
            }
        }

        #endregion

        #region A1 双刃剪杀

        private int CrossLungeTotal => IsPhase2 ? 3 : 2;

        /// <summary>剪杀前摇帧数 (手部共用, 保持两端一致)。</summary>
        internal static int CrossWindupFrames => Main.masterMode ? 36 : (Main.expertMode ? 40 : 44);

        private void RunCrossLunge(Player target) {
            //本体: 悬于上方, 蓄势期减速移动 (公平阀: startup 慢移)
            Vector2 anchor = target.Center + new Vector2(0, -320);
            switch (SubState) {
                case 0: //双刃侧翼蓄势 (手部锚点/预告线由手部推导绘制)
                    HoverMove(anchor, 7f, 0.05f);
                    NPC.velocity *= 0.92f;
                    if (SubTimer == CrossWindupFrames - 36)
                        PlayBeep(-0.1f); //固定 36f 提前量提示音
                    if (SubTimer >= CrossWindupFrames)
                        SetSub(1);
                    break;
                case 1: //爆发窗口: 双刃相隔 6f 先后瞬发 (手部执行)
                    NPC.velocity *= 0.94f;
                    if (SubTimer == 1 || SubTimer == 7) {
                        ACMUtils.AddScreenShake(5f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 1.1f, MaxInstances = 4 }, target.Center);
                    }
                    if (SubTimer >= 26)
                        SetSub(2);
                    break;
                case 2: //收招余摆
                    HoverMove(anchor, 9f, 0.06f);
                    if (SubTimer >= 34) {
                        ComboCount++;
                        NPC.netUpdate = true;
                        if (ComboCount >= CrossLungeTotal)
                            TransitionTo(BossPhase.Reposition);
                        else
                            SetSub(0);
                    }
                    break;
            }
        }

        #endregion

        #region A2 尸火喷吐

        private void RunCorpseFan(Player target) {
            Vector2 aimDir = NPC.SafeDirectionTo(target.Center);
            switch (SubState) {
                case 0: { //蓄力 50f: 面具后仰 (drift-back), 粒子收束 76% 截断
                    float chargeT = MathHelper.Clamp(SubTimer / 50f, 0f, 1f);
                    Vector2 anchor = target.Center + new Vector2(0, -300) - aimDir * (chargeT * chargeT * 200f);
                    HoverMove(anchor, 10f, 0.07f);
                    chargeGlow = Math.Max(chargeGlow, chargeT);
                    ambientTint = Math.Max(ambientTint, 0.25f + chargeT * 0.2f);
                    if (!VaultUtils.isServer && chargeT < 0.76f && Main.rand.NextFloat() < MathF.Sqrt(chargeT)) {
                        //收束鬼火流 (各向异性拉丝)
                        Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(120, 320);
                        Dust d = Dust.NewDustDirect(NPC.Center + off, 0, 0, DustID.CorruptTorch, 0, 0, 130, default, Main.rand.NextFloat(1.2f, 2.2f));
                        d.noGravity = true;
                        d.velocity = -off * 0.055f;
                    }
                    if (SubTimer == 14)
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);
                    if (SubTimer >= 50)
                        SetSub(1);
                    break;
                }
                case 1: { //释放: 三轮扇 + P2 追加环形慢弹, 每轮面具反冲
                    NPC.velocity *= 0.9f;
                    if (SubTimer == 1 || SubTimer == 17 || SubTimer == 33) {
                        int wave = SubTimer <= 1 ? 0 : (SubTimer <= 17 ? 1 : 2);
                        ShootFan(target, 7 + (Main.expertMode ? 2 : 0) + (Main.masterMode ? 2 : 0),
                            70f, 17f, 22f, wave % 2 == 0 ? 1f : -1f);
                        NPC.velocity -= aimDir * 11f; //发射反冲 (质量=反应)
                        ACMUtils.AddScreenShake(4f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f - wave * 0.08f, Volume = 1.05f }, NPC.Center);
                        TriggerClimaxBloom(NPC.Center + aimDir * 40f, TelegraphColors.GhostGreen);
                    }
                    if (IsPhase2 && SubTimer == 49)
                        ShootRing(NPC.Center, 10, 6f);
                    if (SubTimer >= 64)
                        SetSub(2);
                    break;
                }
                case 2: //收招
                    HoverMove(target.Center + new Vector2(0, -300), 8f, 0.05f);
                    if (SubTimer >= 34)
                        TransitionTo(BossPhase.Reposition);
                    break;
            }
        }

        private void ShootFan(Player target, int count, float totalSpreadDeg, float minSpeed, float maxSpeed, float spin) {
            if (VaultUtils.isClient) return;
            float spread = MathHelper.ToRadians(totalSpreadDeg);
            Vector2 muzzle = NPC.Center + NPC.SafeDirectionTo(target.Center) * 42f;
            float baseAngle = (target.Center - muzzle).ToRotation();
            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(count - 1));
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed) * SpeedRamp;
                Vector2 velocity = baseAngle.ToRotationVector2().RotatedBy(angleOffset) * speed;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), muzzle, velocity,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.85f), 2f, Main.myPlayer, 0, 0, spin * (0.5f + i * 0.1f));
            }
        }

        private void ShootRing(Vector2 center, int count, float speed) {
            if (VaultUtils.isClient) return;
            float startRot = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++) {
                float ang = startRot + MathHelper.TwoPi * i / count;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), center, ang.ToRotationVector2() * speed * SpeedRamp,
                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.75f), 2f, Main.myPlayer, 0, 0, Main.rand.NextFloat(-0.6f, 0.6f));
            }
        }

        #endregion

        #region A3 居合·一文字

        private void RunIaiLine(Player target) {
            switch (SubState) {
                case 0: //锁线: 服务器裁定侧别与锁定点
                    if (SubTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        float side = ComboCount % 2 == 0 ? 1f : -1f;
                        if (ComboCount == 0 && Main.rand.NextBool()) side = -1f; //首侧随机, 后续交替
                        AimAngle = side;
                        AimPoint = target.Center + new Vector2(side * 640f, 0f);
                        NPC.netUpdate = true;
                    }
                    HoverMove(target.Center + new Vector2(0, -320), 8f, 0.06f);
                    if (SubTimer >= 6)
                        SetSub(1);
                    break;
                case 1: //预告 42f: 全线蓝→红 (手部绘制), 刃 pow8 反拉
                    HoverMove(target.Center + new Vector2(0, -320), 6f, 0.05f);
                    NPC.velocity *= 0.93f;
                    if (SubTimer == 6)
                        PlayBeep(0.05f);
                    ambientTint = Math.Max(ambientTint, 0.3f);
                    if (SubTimer >= (Main.masterMode ? 34 : 42))
                        SetSub(2);
                    break;
                case 2: //居合斩 10f (手部执行)
                    NPC.velocity *= 0.95f;
                    if (SubTimer == 1) {
                        ACMUtils.AddScreenShake(5f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 1.15f, MaxInstances = 4 }, AimPoint);
                        //P2+: 斩线中点垂直散尸火
                        if (IsPhase2 && Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 mid = new(target.Center.X, AimPoint.Y);
                            for (int i = 0; i < 4; i++) {
                                float vy = (i % 2 == 0 ? -1 : 1) * (6f + i * 1.2f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), mid, new Vector2(0, vy) * SpeedRamp,
                                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.7f), 2f, Main.myPlayer, 0, 0, Main.rand.NextFloat(-0.5f, 0.5f));
                            }
                        }
                    }
                    if (SubTimer >= 10)
                        SetSub(3);
                    break;
                case 3: //收招 + 闪回
                    if (SubTimer >= 24) {
                        ComboCount++;
                        NPC.netUpdate = true;
                        if (ComboCount >= 2)
                            TransitionTo(BossPhase.Reposition);
                        else
                            SetSub(0);
                    }
                    break;
            }
        }

        #endregion

        #region B1 冥狱合葬

        private int ViceHoldTime => ComboCount > 0 ? 46 : (Main.masterMode ? 48 : 58);

        private void RunViceClamp(Player target) {
            switch (SubState) {
                case 0: { //裁定轴向与圆心 → 收缩环 + 双刃梁倒计时
                    if (SubTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        if (ComboCount == 0)
                            AimAngle = Main.rand.NextBool() ? 0f : MathHelper.PiOver2;
                        else
                            AimAngle += MathHelper.PiOver2; //二夹旋轴
                        AimPoint = target.Center + target.velocity * 30f; //锁定预测点
                        NPC.netUpdate = true;
                    }
                    HoverMove(AimPoint + new Vector2(0, -420), 8f, 0.06f);
                    float charge = MathHelper.Clamp(SubTimer / (float)ViceHoldTime, 0f, 1f);
                    //收缩地纹环倒计时
                    saberTell = Math.Max(saberTell, charge * 0.9f);
                    saberTellCenter = AimPoint;
                    saberTellRadius = MathHelper.Lerp(420f, 170f, ACMUtils.QuadIn(charge));
                    saberTellColor = Color.Lerp(TelegraphColors.NetherViolet, TelegraphColors.Lethal, charge);
                    ambientTint = Math.Max(ambientTint, 0.3f + charge * 0.25f);
                    //渐强隆隆 (同帧取 max 不累加)
                    if (!VaultUtils.isServer)
                        ACMUtils.AddScreenShake(charge * charge * 2.2f);
                    if (SubTimer == ViceHoldTime - 36)
                        PlayBeep(-0.2f);
                    if (SubTimer >= ViceHoldTime)
                        SetSub(1);
                    break;
                }
                case 1: //爆前静默 8f: 梁光微暗粒子截断
                    NPC.velocity *= 0.9f;
                    if (SubTimer >= 8)
                        SetSub(2);
                    break;
                case 2: { //合拢 (手部执行); 交会帧刃鸣爆发
                    if (SubTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 1.2f, MaxInstances = 4 }, AimPoint);
                    if (SubTimer == 7) { //双刃交会
                        ACMUtils.AddScreenShake(8f);
                        TriggerClimaxBloom(AimPoint, Color.Lerp(TelegraphColors.GhostGreen, TelegraphColors.Lethal, 0.35f));
                        SoundEngine.PlaySound(SoundID.Item89 with { Pitch = -0.2f, Volume = 1.2f }, AimPoint);
                        //沿夹击轴 4 发飞散 (垂直逃逸方向保持安全)
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 axis = AimAngle.ToRotationVector2();
                            for (int i = 0; i < 4; i++) {
                                Vector2 v = axis * (i < 2 ? 1 : -1) * (13f + (i % 2) * 4f) * SpeedRamp;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), AimPoint, v,
                                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.75f), 2f, Main.myPlayer, 0, 0, Main.rand.NextFloat(-0.4f, 0.4f));
                            }
                        }
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 26; i++) {
                                Vector2 vel = Main.rand.NextVector2Circular(9, 9);
                                Dust d = Dust.NewDustDirect(AimPoint, 0, 0, DustID.CorruptTorch, vel.X, vel.Y, 100, default, 2.2f);
                                d.noGravity = true;
                            }
                        }
                    }
                    if (SubTimer >= 14)
                        SetSub(3);
                    break;
                }
                case 3: //穿过余摆归位
                    HoverMove(target.Center + new Vector2(0, -340), 9f, 0.06f);
                    if (SubTimer >= 54) {
                        ComboCount++;
                        NPC.netUpdate = true;
                        int total = IsPhase3 ? 2 : 1;
                        if (ComboCount >= total)
                            TransitionTo(BossPhase.Reposition);
                        else
                            SetSub(0);
                    }
                    break;
            }
        }

        #endregion

        #region B2 万刃冥阵

        private int MatrixPatternTotal => IsPhase3 ? 4 : 3;

        private void RunBladeMatrix(Player target) {
            ambientTint = Math.Max(ambientTint, 0.45f);
            switch (SubState) {
                case 0: //就位 30f: 面具升位, 双刃结印
                    HoverMove(target.Center + new Vector2(0, -360), 12f, 0.08f);
                    if (SubTimer >= 30)
                        SetSub(1);
                    break;
                case 1: { //图案循环: 每 85f 一图案
                    //重图案期间面具少移动 (公平阀)
                    HoverMove(target.Center + new Vector2(0, -360), 5f, 0.04f);
                    NPC.velocity *= 0.95f;
                    int local = (int)SubTimer % 85;
                    if (local == 1 && ComboCount < MatrixPatternTotal) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            //洗牌: 排除上一图案
                            int pick;
                            do { pick = Main.rand.Next(4); } while (pick == lastPattern);
                            currentPattern = pick;
                            lastPattern = pick;
                            NPC.netUpdate = true;
                            SpawnSaberPattern(pick, target);
                        }
                        //释放拍: 双刃外弹反冲 + 震屏 (全端表现)
                        ACMUtils.AddScreenShake(5f);
                        SoundEngine.PlaySound(SoundID.Item71 with { PitchVariance = 0.2f, Volume = 1f }, target.Center);
                        ComboCount++;
                    }
                    if (ComboCount >= MatrixPatternTotal && local >= 40)
                        SetSub(2);
                    break;
                }
                case 2: //尾拍
                    if (SubTimer >= 40)
                        TransitionTo(BossPhase.Reposition);
                    break;
            }
        }

        /// <summary>刀阵图案 (服务器)。全部走 SaberHell "40f 预告线 → SaberKiller 真刃回扫"机制。</summary>
        private void SpawnSaberPattern(int pattern, Player target) {
            float dmg = 0.9f;
            switch (pattern) {
                case 0: { //环阵收束: 双环向心
                    for (int ring = 0; ring < 2; ring++) {
                        int slice = 6 + ring * 2;
                        for (int i = 0; i < slice; i++) {
                            float ang = MathHelper.TwoPi * i / slice + ring * 0.15f;
                            Vector2 dir = ang.ToRotationVector2();
                            Vector2 spawn = target.Center + dir * (280 + ring * 90);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, -dir * 10f * SpeedRamp,
                                ModContent.ProjectileType<SaberHell>(), GetBossDamage(dmg), 2);
                        }
                    }
                    break;
                }
                case 1: { //十字扫线: 8 线 45° 递增
                    float baseRot = Main.rand.NextBool() ? 0f : MathHelper.PiOver4 * 0.5f;
                    for (int i = 0; i < 8; i++) {
                        float ang = MathHelper.PiOver4 * i + baseRot;
                        Vector2 dir = ang.ToRotationVector2();
                        Vector2 spawn = target.Center + dir * 620;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, -dir * 22f * SpeedRamp,
                            ModContent.ProjectileType<SaberHell>(), GetBossDamage(1f), 2);
                    }
                    break;
                }
                case 2: { //辐射回扑: 短轨道旋转 → 依序指心收束 (轨道圆心走 ai0/ai1 同步)
                    int spokes = 12;
                    float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < spokes; i++) {
                        float ang = baseAng + MathHelper.TwoPi * i / spokes;
                        Vector2 dir = ang.ToRotationVector2();
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center + dir * 150, dir,
                            ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.75f), 2, Main.myPlayer,
                            target.Center.X, target.Center.Y, -30);
                    }
                    break;
                }
                case 3: { //波状点射: 3 波×5 自上方压向玩家
                    for (int w = 0; w < 3; w++) {
                        for (int i = -2; i <= 2; i++) {
                            float offsetAng = i * 0.11f + w * 0.05f;
                            Vector2 dir = NPC.SafeDirectionTo(target.Center).RotatedBy(offsetAng);
                            Vector2 spawn = target.Center + dir.RotatedBy(MathHelper.PiOver2) * (i * 75) + new Vector2(0, -320 - w * 130);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, dir * 21f * SpeedRamp,
                                ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.8f), 2);
                        }
                    }
                    break;
                }
            }
        }

        #endregion

        #region B3 狂暴追猎

        private int PursuitTotal => IsPhase3 ? 4 : 3;

        private void RunFrenzyPursuit(Player target) {
            ambientTint = Math.Max(ambientTint, 0.3f);
            switch (SubState) {
                case 0: { //裂隙闪至玩家远侧 (闪现即距离栓绳; 闪后 8f 宽限)
                    if (SubTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 approach = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                            AimAngle = approach.ToRotation() + Main.rand.NextFloat(-0.45f, 0.45f);
                            AimPoint = target.Center + AimAngle.ToRotationVector2() * 760f;
                            NPC.netUpdate = true;
                        }
                    }
                    if (SubTimer == 3) {
                        //闪点视觉在旧/新位置各留一瞬
                        SpawnBlinkFlash(NPC.Center);
                        NPC.Center = AimPoint;
                        NPC.velocity = Vector2.Zero;
                        blinkGrace = 10;
                        SpawnBlinkFlash(NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.9f, MaxInstances = 4 }, NPC.Center);
                    }
                    if (SubTimer >= 10)
                        SetSub(1);
                    break;
                }
                case 1: { //预告 32f: 红线渐浓 + pow8 反拉
                    if (SubTimer == 1)
                        PlayBeep(0.1f);
                    float t = MathHelper.Clamp(SubTimer / 32f, 0f, 1f);
                    Vector2 aim = NPC.SafeDirectionTo(target.Center);
                    //末段反向收拢 — 出鞘前的吸气
                    Vector2 reel = -aim * MathF.Pow(t, 8) * 130f;
                    HoverMove(AimPoint + reel, 20f, 0.3f);
                    chargeGlow = Math.Max(chargeGlow, t * 0.8f);
                    if (SubTimer >= (Main.masterMode ? 26 : 32))
                        SetSub(2);
                    break;
                }
                case 2: { //冲刺 10f: 瞬发 112px/f 直线
                    if (SubTimer == 1) {
                        Vector2 lead = ACMUtils.LeadTarget(NPC.Center, target.Center, target.velocity, 112f);
                        NPC.velocity = lead * 112f;
                        ACMUtils.AddScreenShake(6f);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.85f, MaxInstances = 4 }, NPC.Center);
                        TriggerClimaxBloom(NPC.Center, TelegraphColors.GhostGreen);
                        //P3: 冲刺途中掷尸火
                        if (IsPhase3 && Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 2; i++) {
                                Vector2 v = NPC.velocity.SafeNormalize(Vector2.UnitX).RotatedBy((i == 0 ? 1 : -1) * 0.9f) * 9f * SpeedRamp;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, v,
                                    ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(0.7f), 2f, Main.myPlayer, 0, 0, i == 0 ? 0.7f : -0.7f);
                            }
                        }
                    }
                    if (SubTimer >= 10)
                        SetSub(3);
                    break;
                }
                case 3: //硬刹 (双刃松弹簧甩过 — 次级运动由手部呈现)
                    NPC.velocity *= 0.66f;
                    if (SubTimer >= 14) {
                        ComboCount++;
                        NPC.netUpdate = true;
                        if (ComboCount >= PursuitTotal)
                            TransitionTo(BossPhase.Reposition);
                        else
                            SetSub(0);
                    }
                    break;
            }
        }

        /// <summary>裂隙锚点 (入场/死亡演出, 供手部读取)。</summary>
        internal Vector2 RiftCenter => riftCenter;

        //闪点视觉 (客户端): 裂隙光斑 + 星芒尘 (手部传送也复用)
        internal void SpawnBlinkFlash(Vector2 pos) {
            if (VaultUtils.isServer) return;
            blinkFlashes[blinkFlashHead] = new BlinkFlash { Pos = pos, Life = 18 };
            blinkFlashHead = (blinkFlashHead + 1) % blinkFlashes.Length;
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.CorruptTorch, vel.X, vel.Y, 120, default, 1.8f);
                d.noGravity = true;
            }
        }

        private struct BlinkFlash { public Vector2 Pos; public int Life; }
        private readonly BlinkFlash[] blinkFlashes = new BlinkFlash[4];
        private int blinkFlashHead;

        #endregion

        #region C1 万刃归宗

        private void RunBladeStorm(Player target) {
            ambientTint = Math.Max(ambientTint, 0.55f);
            switch (SubState) {
                case 0: { //聚势 80f: 风暴边界展开, 收束粒子 76% 截断
                    HoverMove(target.Center + new Vector2(0, -380), 9f, 0.06f);
                    NPC.velocity *= 0.94f;
                    float charge = MathHelper.Clamp(SubTimer / 80f, 0f, 1f);
                    saberTell = Math.Max(saberTell, charge * 0.85f);
                    saberTellCenter = target.Center;
                    saberTellRadius = MathHelper.Lerp(0f, 880f, ACMUtils.SineInOut(charge));
                    saberTellColor = Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, charge * 0.7f);
                    chargeGlow = Math.Max(chargeGlow, charge);
                    if (!VaultUtils.isServer) {
                        ACMUtils.AddScreenShake(charge * charge * charge * 3f);
                        if (charge < 0.76f && Main.rand.NextFloat() < MathF.Sqrt(charge) * 0.8f) {
                            Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(200, 480);
                            Dust d = Dust.NewDustDirect(NPC.Center + off, 0, 0, DustID.GoldFlame, 0, 0, 130, default, Main.rand.NextFloat(1.2f, 2f));
                            d.noGravity = true;
                            d.velocity = -off * 0.05f;
                        }
                    }
                    if (SubTimer == 20)
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                    if (SubTimer >= 80)
                        SetSub(1);
                    break;
                }
                case 1: { //刀环三波: 每波绕玩家成环, 错列脱环收束
                    HoverMove(target.Center + new Vector2(0, -380), 6f, 0.04f);
                    if ((SubTimer == 1 || SubTimer == 81 || SubTimer == 161) && Main.netMode != NetmodeID.MultiplayerClient)
                        SpawnStormWave(target);
                    if (SubTimer == 1 || SubTimer == 81 || SubTimer == 161) {
                        ACMUtils.AddScreenShake(4f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f, Volume = 1f, MaxInstances = 4 }, target.Center);
                    }
                    if (SubTimer >= 240)
                        SetSub(2);
                    break;
                }
                case 2: { //终章: 辐射回扑 + 中心泛光
                    if (SubTimer == 1) {
                        TriggerClimaxBloom(target.Center, TelegraphColors.Gold);
                        ACMUtils.AddScreenShake(8f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.2f }, target.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            SpawnSaberPattern(2, target); //辐射回扑作收尾
                    }
                    if (SubTimer >= 60)
                        TransitionTo(BossPhase.Reposition);
                    break;
                }
            }
        }

        /// <summary>风暴单波 (服务器): 4~5 柄冥刃绕玩家 540px 轨道, 错列延迟依序脱环。</summary>
        private void SpawnStormWave(Player target) {
            int count = 4 + (Main.expertMode ? 1 : 0);
            float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++) {
                float ang = baseAng + MathHelper.TwoPi * i / count;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 spawn = target.Center + dir * 540f;
                //切向初速仅作朝向; ai[2] 传轨道前置时长 (出生同步, MP 一致)
                Vector2 vel = dir.RotatedBy(MathHelper.PiOver2) * 1f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, vel,
                    ModContent.ProjectileType<SaberHell>(), GetBossDamage(0.85f), 2, Main.myPlayer,
                    target.Center.X, target.Center.Y, -(50 + i * 14));
            }
        }

        #endregion

        #region C2 黄泉三裂

        private void RunNetherCleave(Player target) {
            ambientTint = Math.Max(ambientTint, 0.5f);
            switch (SubState) {
                case 0: //合璧 40f: 双刃飞向面具前合拢 (手部执行)
                    HoverMove(target.Center + new Vector2(0, -300), 7f, 0.05f);
                    NPC.velocity *= 0.93f;
                    if (SubTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                    if (SubTimer >= 40)
                        SetSub(1);
                    break;
                case 1: { //预告 46f: 贯穿玩家的粗红线 + 巨刃收势
                    if (SubTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        float baseAng = NPC.SafeDirectionTo(target.Center + target.velocity * 8f).ToRotation();
                        if (ComboCount == 0) {
                            AimAngle = baseAng;
                        }
                        else {
                            //±120° 旋劈, 但仍过玩家当前方位
                            AimAngle = baseAng + (Main.rand.NextBool() ? 1 : -1) * 0.35f;
                        }
                        AimPoint = NPC.Center;
                        NPC.netUpdate = true;
                    }
                    NPC.velocity *= 0.9f;
                    float t = MathHelper.Clamp(SubTimer / 46f, 0f, 1f);
                    chargeGlow = Math.Max(chargeGlow, t);
                    if (SubTimer == 10)
                        PlayBeep(-0.25f);
                    if (!VaultUtils.isServer)
                        ACMUtils.AddScreenShake(t * t * 2f);
                    if (SubTimer >= 46)
                        SetSub(2);
                    break;
                }
                case 2: //总劈 10f (手部执行); 本体反冲
                    if (SubTimer == 1) {
                        NPC.velocity = -AimAngle.ToRotationVector2() * 7f;
                        ACMUtils.AddScreenShake(8f);
                        SoundEngine.PlaySound(SoundID.Item89 with { Pitch = -0.35f, Volume = 1.25f }, NPC.Center);
                        TriggerClimaxBloom(NPC.Center + AimAngle.ToRotationVector2() * 300f, TelegraphColors.Lethal);
                    }
                    if (SubTimer >= 10)
                        SetSub(3);
                    break;
                case 3: //回收 26f
                    NPC.velocity *= 0.92f;
                    if (SubTimer >= 26) {
                        ComboCount++;
                        NPC.netUpdate = true;
                        if (ComboCount >= 3)
                            TransitionTo(BossPhase.Reposition);
                        else
                            SetSub(1);
                    }
                    break;
            }
        }

        #endregion

        #region 换阶段演出

        private void RunTransition2(Player target) {
            NPC.velocity *= 0.9f;
            ambientTint = Math.Max(ambientTint, 0.4f + PhaseTimer / 150f * 0.2f);
            if (PhaseTimer == 1)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.55f, Volume = 1.1f }, NPC.Center);
            //0~100: 双刃绕面急旋 (手部), 隆隆渐强
            if (PhaseTimer < 100) {
                float t = PhaseTimer / 100f;
                trembleAmp = Math.Max(trembleAmp, t * 3f);
                if (!VaultUtils.isServer)
                    ACMUtils.AddScreenShake(t * t * 3f);
            }
            if (PhaseTimer == 100) {
                //冲击释放
                ACMUtils.AddScreenShake(10f);
                eyeFlash = 1f;
                TriggerClimaxBloom(NPC.Center, TelegraphColors.NetherViolet);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 48; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(13, 13);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.CorruptTorch, vel.X, vel.Y, 100, default, 2.4f);
                        d.noGravity = true;
                    }
                }
            }
            if (PhaseTimer >= 150) {
                rampTimer = 90;
                attackCycleIndex = 0;
                TransitionTo(BossPhase.Reposition);
            }
        }

        private void RunTransition3(Player target) {
            NPC.velocity *= 0.9f;
            ambientTint = Math.Max(ambientTint, 0.55f);
            //0~40: 面具震颤, 裂纹燃金
            if (PhaseTimer < 40) {
                trembleAmp = Math.Max(trembleAmp, PhaseTimer / 40f * 4f);
                kingGlow = Math.Max(kingGlow, PhaseTimer / 40f * 0.5f);
            }
            //40~120: 刃晕展开
            if (PhaseTimer >= 40 && PhaseTimer < 120) {
                float t = (PhaseTimer - 40) / 80f;
                bladeHalo = Math.Max(bladeHalo, t);
                if (!VaultUtils.isServer)
                    ACMUtils.AddScreenShake(t * t * 3.5f);
            }
            if (PhaseTimer == 120) {
                whiteFlash = 1f;
                kingGlow = 1f;
                eyeFlash = 1f;
                ACMUtils.AddScreenShake(11f);
                TriggerClimaxBloom(NPC.Center, TelegraphColors.Gold);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.1f, Volume = 1.3f }, NPC.Center);
            }
            if (PhaseTimer >= 170) {
                rampTimer = 90;
                attackCycleIndex = 1; //P3 以 BladeStorm 开幕, 表游标跳过
                TransitionTo(BossPhase.BladeStorm);
            }
        }

        #endregion

        #region 死亡演出

        public override bool CheckDead() {
            if (Phase != BossPhase.Death) {
                //拦截死亡 → 进入死亡脚本
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Death);
                nextBeepIdx = 0;
                riftCenter = NPC.Center + new Vector2(0, -60);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            ambientTint = Math.Max(ambientTint, 0.6f);

            //0~70: 双刃失控碎裂 (44f 右刃 / 70f 左刃, 由手部读表执行)
            if (PhaseTimer < 70) {
                NPC.velocity *= 0.92f;
                trembleAmp = Math.Max(trembleAmp, PhaseTimer / 70f * 2.5f);
            }
            //70~180: 面具上浮 + 灼烧 + 警报加速数列
            else if (PhaseTimer < 180) {
                NPC.velocity = new Vector2(0, -0.35f);
                float t = (PhaseTimer - 70) / 110f;
                dissolveT = t * 0.45f;
                trembleAmp = Math.Max(trembleAmp, 2f + t * 3f);
                if (!VaultUtils.isServer) {
                    ACMUtils.AddScreenShake(t * t * 4f);
                    //收束吸气
                    if (Main.rand.NextFloat() < 0.5f) {
                        Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(150, 380);
                        Dust d = Dust.NewDustDirect(NPC.Center + off, 0, 0, DustID.CorruptTorch, 0, 0, 130, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = -off * 0.05f;
                    }
                }
                //手调加速警报 (前缀和定拍)
                if (nextBeepIdx < DeathBeepGaps.Length) {
                    int beepFrame = 70;
                    for (int i = 0; i < nextBeepIdx; i++) beepFrame += DeathBeepGaps[i];
                    if (PhaseTimer >= beepFrame) {
                        PlayBeep(-0.3f + nextBeepIdx * 0.09f, 0.85f);
                        nextBeepIdx++;
                    }
                }
            }
            //180~192: 爆前静默 — 一切收声, 面具收缩
            else if (PhaseTimer < 192) {
                NPC.velocity = Vector2.Zero;
                trembleAmp = 0f;
            }
            //192: 白帧定格 (全场唯一一次 impact frame)
            if (PhaseTimer == 192) {
                whiteFlash = 1.6f;
                deathRift = 0.2f;
                ACMUtils.AddScreenShake(14f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.3f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item89 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
            }
            //192~290: 拽入裂隙, 灼烧殆尽
            if (PhaseTimer > 192 && PhaseTimer < 290) {
                float t = (float)(PhaseTimer - 192) / 98f;
                deathRift = MathHelper.Lerp(deathRift, 1f, 0.06f);
                deathRiftCollapse = ACMUtils.QuadIn(MathHelper.Clamp((t - 0.6f) / 0.4f, 0f, 1f)) * 0.6f;
                dissolveT = 0.45f + t * 0.55f;
                NPC.Center = Vector2.Lerp(NPC.Center, riftCenter, 0.05f);
                NPC.velocity = Vector2.Zero;
                if (!VaultUtils.isServer && Main.rand.NextFloat() < 0.7f) {
                    //鬼火螺旋外涌
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust d = Dust.NewDustDirect(riftCenter + ang.ToRotationVector2() * 60f, 0, 0, DustID.CorruptTorch, 0, 0, 120, default, 2f);
                    d.noGravity = true;
                    d.velocity = ang.ToRotationVector2().RotatedBy(1.2f) * 5f;
                }
            }
            //290: 裂隙咬合 → 真实死亡结算
            if (PhaseTimer >= 290) {
                deathRift = 0f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.checkDead(); //Phase==Death 且脚本走完 → CheckDead 返回 true → OnKill 掉落照常
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            DrawTelegraphsAndRifts(spriteBatch);

            Texture2D mainValue = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = mainValue.Size() / 2;

            //震颤偏移 (演出用, 纯视觉)
            Vector2 tremble = trembleAmp > 0.05f
                ? new Vector2(Main.rand.NextFloat(-trembleAmp, trembleAmp), Main.rand.NextFloat(-trembleAmp, trembleAmp))
                : Vector2.Zero;
            Vector2 drawCenter = NPC.Center + tremble - Main.screenPosition;

            //入场 fake-Z: 自裂隙深处而来
            float scale = NPC.scale;
            float alpha = 1f;
            if (Phase == BossPhase.Intro) {
                float t = MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f);
                float z = 1f - MathF.Pow(1f - t, 3);
                scale *= MathHelper.Lerp(0.22f, 1f, z);
                alpha = MathHelper.Lerp(0.1f, 1f, z);
            }
            //蓄力鳞胀 + 呼吸
            if (chargeGlow > 0.05f)
                scale *= 1f + chargeGlow * 0.1f;
            if (Phase == BossPhase.Intro && PhaseTimer > 70 && PhaseTimer <= 130)
                scale *= 1f + 0.015f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 6f);
            //死亡静默收缩
            if (Phase == BossPhase.Death && PhaseTimer >= 180 && PhaseTimer < 192)
                scale *= MathHelper.SmoothStep(1f, 0.88f, (PhaseTimer - 180) / 12f) * (1f + MathF.Cos((float)Main.GlobalTimeWrappedHourly * 40f) * 0.02f);
            if (Phase == BossPhase.Death && PhaseTimer > 192)
                scale *= MathHelper.Lerp(1f, 0.42f, MathHelper.Clamp((PhaseTimer - 192) / 98f, 0f, 1f));

            //速度门控残影 (>24px/f 才显 — 快时才有戏)
            float speed = NPC.velocity.Length();
            if (speed > 24f) {
                float ghostAlpha = MathHelper.Clamp((speed - 24f) / 60f, 0f, 0.6f);
                for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                    Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    float fade = ghostAlpha * (1f - i / (float)NPC.oldPos.Length);
                    spriteBatch.Draw(mainValue, drawOldPos, null, new Color(120, 220, 190, 0) * fade, NPC.rotation, origin, scale * (1f - i * 0.015f), SpriteEffects.None, 0);
                }
            }

            //P3 幻影刃晕 (面具背后 6 柄幻影刀扇)
            if (bladeHalo > 0.03f)
                DrawBladeHalo(spriteBatch, drawCenter, scale);

            //蓄力聚光
            if (chargeGlow > 0.05f && SoftGlow != null) {
                for (int i = 0; i < 3; i++) {
                    float gScale = (2.0f + i * 0.5f) * (0.6f + chargeGlow * 0.7f) * scale;
                    Color gCol = Color.Lerp(TelegraphColors.GhostGreen, TelegraphColors.Gold, 0.35f) * (chargeGlow * (0.45f - 0.12f * i));
                    gCol.A = 0;
                    spriteBatch.Draw(SoftGlow, drawCenter, null, gCol, 0, SoftGlow.Size() / 2, gScale, SpriteEffects.None, 0);
                }
            }

            //本体: 死亡期走 DissolveBurn, 平时直绘
            if (dissolveT > 0.01f)
                DrawBodyDissolving(spriteBatch, mainValue, drawCenter, origin, scale, alpha);
            else
                Main.EntitySpriteDraw(mainValue, drawCenter, null, Color.White * alpha, NPC.rotation, origin, scale, SpriteEffects.None);

            //双目暴亮 / "王"字金辉
            DrawFaceGlows(spriteBatch, drawCenter, scale);

            return false;
        }

        private void DrawBodyDissolving(SpriteBatch sb, Texture2D tex, Vector2 drawCenter, Vector2 origin, float scale, float alpha) {
            Effect fx = ACMShaders.DissolveBurn;
            if (fx == null) {
                Main.EntitySpriteDraw(tex, drawCenter, null, Color.White * alpha, NPC.rotation, origin, scale, SpriteEffects.None);
                return;
            }
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(alpha);
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(dissolveT, 0f, 1f));
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 1f));
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f));
            fx.Parameters["uSweepStrength"]?.SetValue(0.35f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawCenter, null, Color.White, NPC.rotation, origin, scale, SpriteEffects.None, 0);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        private void DrawFaceGlows(SpriteBatch sb, Vector2 drawCenter, float scale) {
            //双目暴亮 (入场定格/转场)
            if (eyeFlash > 0.05f && SoftGlow != null) {
                Color eyeCol = new Color(255, 70, 60, 0) * eyeFlash;
                for (int i = 0; i < 2; i++) {
                    float ex = i == 0 ? -26f : 26f;
                    Vector2 eyePos = drawCenter + new Vector2(ex, -12f).RotatedBy(NPC.rotation) * scale;
                    sb.Draw(SoftGlow, eyePos, null, eyeCol, 0, SoftGlow.Size() / 2, 0.7f * scale * (1f + eyeFlash * 0.5f), SpriteEffects.None, 0);
                }
            }
            //额上"王"字燃金 (P3 常驻)
            if (kingGlow > 0.05f && StarTexture != null) {
                float pulse = 1f + 0.12f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 5f);
                Color gold = new Color(255, 215, 110, 0) * (kingGlow * 0.85f);
                Vector2 browPos = drawCenter + new Vector2(0, -48f).RotatedBy(NPC.rotation) * scale;
                sb.Draw(StarTexture, browPos, null, gold, 0, StarTexture.Size() / 2, 0.2f * scale * pulse, SpriteEffects.None, 0);
                sb.Draw(StarTexture, browPos, null, gold * 0.5f, MathHelper.PiOver4, StarTexture.Size() / 2, 0.13f * scale * pulse, SpriteEffects.None, 0);
            }
        }

        //P3 刃晕: 面具背后扇形展开的幻影刀 (加色, 缓摆呼吸)
        private void DrawBladeHalo(SpriteBatch sb, Vector2 drawCenter, float scale) {
            Texture2D bladeTex = TextureAssets.Npc[ModContent.NPCType<YingouHand>()].Value;
            float time = (float)Main.GlobalTimeWrappedHourly;
            int count = 6;
            for (int i = 0; i < count; i++) {
                //扇形 -150°~-30° 均布 + 慢摆
                float baseAng = MathHelper.Lerp(-2.6f, -0.54f, i / (count - 1f));
                float sway = MathF.Sin(time * 1.3f + i * 1.7f) * 0.07f;
                float ang = baseAng + sway;
                float dist = (95f + MathF.Sin(time * 2f + i) * 8f) * scale;
                Vector2 pos = drawCenter + ang.ToRotationVector2() * dist;
                Color c = new Color(150, 110, 235, 0) * (bladeHalo * (0.3f + 0.08f * MathF.Sin(time * 3f + i * 2.2f)));
                sb.Draw(bladeTex, pos, null, c, ang, new Vector2(30, bladeTex.Height / 2f), scale * 0.72f, SpriteEffects.None, 0);
            }
        }

        /// <summary>预警与裂隙贴花 (硬化 ACMShaders 助手, 自管批次; 服务端零绘制)。</summary>
        private void DrawTelegraphsAndRifts(SpriteBatch sb) {
            //1) 地纹环 (夹击倒计时 / 风暴边界)
            if (saberTell > 0.01f) {
                Effect runic = ACMShaders.ArenaRunic;
                if (runic != null) {
                    ACMShaders.WorldDecalParams(saberTellCenter, saberTellRadius, out Vector2 c, out float r, out float aspect);
                    runic.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    runic.Parameters["uCenter"]?.SetValue(c);
                    runic.Parameters["uRadius"]?.SetValue(r);
                    runic.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(saberTell, 0f, 1f));
                    runic.Parameters["uAspect"]?.SetValue(aspect);
                    runic.Parameters["uColorPrimary"]?.SetValue(saberTellColor.ToVector4());
                    runic.Parameters["uColorSecondary"]?.SetValue(new Color(30, 12, 40).ToVector4());
                    runic.Parameters["uRuneFreq"]?.SetValue(13f);
                    runic.Parameters["uMode"]?.SetValue(0f);
                    runic.Parameters["uShape"]?.SetValue(0f);
                    ACMShaders.DrawScreenSpaceDecal(sb, runic, BlendState.NonPremultiplied);
                }
            }

            //2) 追猎冲刺预告线 (冲刺=接触伤害源, 必须可读)
            if (Phase == BossPhase.FrenzyPursuit && SubState == 1) {
                Player tgt = Main.player[NPC.target];
                if (tgt.Alives()) {
                    float ramp = MathHelper.Clamp(SubTimer / 32f, 0f, 1f);
                    Vector2 dir = NPC.SafeDirectionTo(tgt.Center);
                    Color core = Color.Lerp(new Color(150, 220, 200), TelegraphColors.Lethal, ramp);
                    Color edge = new Color(150, 20, 30) { A = 0 };
                    ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1700f, MathHelper.Lerp(4f, 22f, ramp),
                        core, edge, 0.25f + ramp * 0.6f, flowSpeed: 2.6f, flowScale: 2.2f, coreSharp: 2.6f);
                }
            }

            //3) 夹击梁: 两刃之间增厚倒计时 (静默拍变暗)
            if (Phase == BossPhase.ViceClamp && (SubState == 0 || SubState == 1) && leftHand != null && rightHand != null) {
                float charge = SubState == 1 ? 1f : MathHelper.Clamp(SubTimer / (float)ViceHoldTime, 0f, 1f);
                float dim = SubState == 1 ? 0.55f : 1f;
                Color core = Color.Lerp(new Color(140, 190, 255), TelegraphColors.Lethal, charge);
                Color edge = new Color(120, 20, 40) { A = 0 };
                ACMShaders.DrawBeam(leftHand.NPC.Center, rightHand.NPC.Center,
                    MathHelper.Lerp(5f, 26f, charge), core, edge, (0.3f + charge * 0.55f) * dim,
                    flowSpeed: 3f, flowScale: 2f, coreSharp: 2.4f);
            }

            //4) 三裂预告线 (贯穿玩家的粗红线)
            if (Phase == BossPhase.NetherCleave && SubState == 1 && SubTimer > 4) {
                float ramp = MathHelper.Clamp(SubTimer / 46f, 0f, 1f);
                Vector2 dir = AimAngle.ToRotationVector2();
                Color core = Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal, ramp);
                Color edge = new Color(140, 30, 30) { A = 0 };
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 3000f, MathHelper.Lerp(8f, 34f, ramp),
                    core, edge, 0.3f + ramp * 0.6f, flowSpeed: 2.2f, flowScale: 1.8f, coreSharp: 2.2f);
            }

            //5) 裂隙贴花: 入场 / 死亡
            if (introRift > 0.02f)
                DrawRiftDecal(sb, riftCenter, 280f, introRift, introRiftCollapse, 1.2f);
            if (deathRift > 0.02f)
                DrawRiftDecal(sb, riftCenter, 340f, deathRift, deathRiftCollapse, 1.7f);

            //6) 闪点小裂隙 (追猎/居合传送掩护)
            for (int i = 0; i < blinkFlashes.Length; i++) {
                if (blinkFlashes[i].Life <= 0) continue;
                blinkFlashes[i].Life--;
                float lifeT = blinkFlashes[i].Life / 18f;
                DrawRiftDecal(sb, blinkFlashes[i].Pos, 90f + (1f - lifeT) * 40f, lifeT * 0.85f, 1f - lifeT, 2.2f);
            }

            //7) 高潮径向泛光 (占本帧唯一全屏名额)
            if (climaxBloom > 0.01f) {
                ACMShaders.DrawRadialBloomAt(climaxBloomCenter, 0.16f + (1f - climaxBloom) * 0.18f,
                    climaxBloom, climaxBloomColor, rayCount: 9f, falloff: 2.6f);
            }
        }

        /// <summary>黄泉裂隙贴花 (供本体与手部复用; 须在已有活动批的阶段调用)。</summary>
        internal static void DrawRiftDecal(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity, float collapse, float swirl) {
            Effect fx = NetherRift;
            if (fx == null || intensity <= 0.02f)
                return;
            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 c, out float r, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(c);
            fx.Parameters["uRadius"]?.SetValue(r);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
            fx.Parameters["uCollapse"]?.SetValue(MathHelper.Clamp(collapse, 0f, 1f));
            fx.Parameters["uSwirl"]?.SetValue(swirl);
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.NonPremultiplied);
        }

        //白帧定格 / T3 白闪: NPC 层之上全屏白 + 黑剪影
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || whiteFlash <= 0.03f)
                return;
            float w = MathHelper.Clamp(whiteFlash, 0f, 1f);
            //批带 GameViewMatrix 缩放: 以屏心为轴反算全覆盖矩形
            float zoom = MathF.Max(Main.GameViewMatrix.Zoom.X, 0.01f);
            int rw = (int)(Main.screenWidth / zoom) + 8;
            int rh = (int)(Main.screenHeight / zoom) + 8;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)(Main.screenWidth * 0.5f - rw * 0.5f), (int)(Main.screenHeight * 0.5f - rh * 0.5f), rw, rh), Color.White * w);
            //黑剪影 (只在强白帧显)
            if (whiteFlash > 0.5f) {
                Texture2D tex = TextureAssets.Npc[NPC.type].Value;
                spriteBatch.Draw(tex, NPC.Center - Main.screenPosition, null, Color.Black * ((whiteFlash - 0.5f) * 2f),
                    NPC.rotation, tex.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            }
        }

        #endregion
    }
}

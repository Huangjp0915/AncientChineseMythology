using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
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
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 尸骸·枉死万骸之主 —— V3 全面重做 (Docs/BossRedo/Corpses.md)。
    ///
    /// 结构: 巨颅 (本体) + 双判官之手 (<see cref="CorpsesHand"/> 执行器)。
    /// 编排: 手写节拍表 (authored cycle) 驱动, 决策零随机, 多端确定;
    ///   Intro → Score1 (役骸) → [60% 引魂大阵 set-piece] → Score2 (判骸) → [30% 城门闭合] → 死亡演出。
    /// 演出: 入场破土/换阶段清弹公告/死亡崩解白闪三大节拍齐备;
    ///   专属着色器 CorpsesMiasma (全屏尸雾, 走名额契约) / CorpsesBoneRing (预警+冲击 decal) /
    ///   CorpsesSoulFlame (眼焰/魂灯), 本类静态缓存, 不注册进 ACMShaders。
    /// </summary>
    [AutoloadBossHead]
    internal class Corpses : ModNPC
    {
        // ================================================================
        //  阶段与同步状态
        // ================================================================

        public enum BossPhase
        {
            Intro,      // 入场演出 (~250f, 无伤害交互)
            Score1,     // P1 役骸: 单手招式教学循环
            Ritual,     // 引魂大阵 set-piece (60% 单次)
            Score2,     // P2 判骸: 双手错拍 + 旋冢 + 斜轴合掌
            CityGate,   // P3 城门闭合终幕 (30%)
            Death       // 死亡演出 (~240f, 锁血)
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        /// <summary>魂火吐息剩余帧 (头颅招式, 与节拍表并行)。</summary>
        public ref float HeadCastTimer => ref NPC.ai[2];
        /// <summary>万骸旋冢剩余帧。</summary>
        public ref float TombTimer => ref NPC.ai[3];

        // —— 阶段门控 ——
        private bool ritualTriggered;     // 引魂大阵 @60% 单次
        private bool cityGateTriggered;   // 城门闭合 @20%→30%
        private int scoreCycle;           // 节拍表循环数 (交替轴向/左右手用, 同步)

        // —— 引魂大阵 (V2 机制保留) ——
        private int ritualStage;             // 0=脱体起阵 1=施法收缩 2=结算
        private Vector2 ritualCenter;
        private float ritualRadius;
        private float ritualDecalIntensity;  // 纯视觉淡入淡出
        private int ritualGateSeed;
        private float ritualBreakProgress;
        private bool ritualBroken;
        private int vulnerableTimer;         // 头颅破绽窗口
        private const int RitualWindup = 90;
        private const int RitualChannel = 360;
        private const float RitualStartRadius = 560f;
        private const float RitualEndRadius = 200f;
        private const int GateSlotCount = 4;

        // —— 城门闭合 ——
        private Vector2 cityCenter;
        private float cityRadius;
        private const float CityStartRadius = 720f;
        private const float CityEndRadius = 360f;

        // —— 万骸旋冢 ——
        private Vector2 tombCenter;
        private float tombAngle;
        private const int TombTotal = 196;

        // —— 手臂引用 ——
        private CorpsesHand leftHand;
        private CorpsesHand rightHand;
        private bool handsInitialized;

        // —— 纯视觉 (本地, 不同步) ——
        private float miasma;            // 尸雾强度 (随阶段递进)
        private float deathFlash;        // 死亡白化对比帧 (一次性)
        private float eyeFlame = 0f;     // 眼焰亮度 (状态广播)
        private float eyeFlameTarget = 0f;
        private float headBob;           // 受震下沉弹簧 (secondary motion)
        private float headBobVel;
        private Vector2 recoilOffset;    // 吐息后坐 (纯视觉)
        private int impactRingTimer;     // 旋冢合点冲击环残留
        private Vector2 impactRingPos;

        // ================================================================
        //  专属着色器 (静态缓存, 不注册进 ACMShaders)
        // ================================================================

        private static Asset<Effect> miasmaRef;
        private static Asset<Effect> boneRingRef;
        private static Asset<Effect> soulFlameRef;

        private static Effect MiasmaShader {
            get {
                if (Main.dedServ) return null;
                miasmaRef ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/CorpsesMiasma", AssetRequestMode.ImmediateLoad);
                return miasmaRef?.Value;
            }
        }

        private static Effect BoneRingShader {
            get {
                if (Main.dedServ) return null;
                boneRingRef ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/CorpsesBoneRing", AssetRequestMode.ImmediateLoad);
                return boneRingRef?.Value;
            }
        }

        private static Effect SoulFlameShader {
            get {
                if (Main.dedServ) return null;
                soulFlameRef ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/CorpsesSoulFlame", AssetRequestMode.ImmediateLoad);
                return soulFlameRef?.Value;
            }
        }

        /// <summary>
        /// 白骨预警/冲击 decal (CorpsesBoneRing): mode 0=落点光柱 1=冲击环 2=轴线束。
        /// 供本体/手/Marker 共用, 屏幕空间绘制, 不占全屏名额。要求处于活动 SpriteBatch 中。
        /// </summary>
        public static void DrawBoneRingDecal(SpriteBatch sb, int mode, Vector2 worldCenter, float worldRadius,
            float intensity, float progress, Vector2 dir, float halfLenWorld, Color colorMain, Color colorEdge) {
            if (Main.dedServ || intensity < 0.02f)
                return;
            Effect fx = BoneRingShader;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float rFrac, out float aspect);
            float lenFrac = rFrac;
            if (halfLenWorld > 0f)
                ACMShaders.WorldDecalParams(worldCenter, halfLenWorld, out _, out lenFrac, out _);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uMode"]?.SetValue((float)mode);
            fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            fx.Parameters["uDir"]?.SetValue(dir.SafeNormalize(Vector2.UnitX));
            fx.Parameters["uHalfLen"]?.SetValue(lenFrac);
            fx.Parameters["uColorMain"]?.SetValue(colorMain.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(colorEdge.ToVector4());
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        /// <summary>程序化魂火 (CorpsesSoulFlame): 眼焰/魂灯/掌心焰共用。要求处于活动 SpriteBatch 中。</summary>
        public static void DrawSoulFlame(SpriteBatch sb, Vector2 worldPos, float scale, float intensity, float seed) {
            if (Main.dedServ || intensity < 0.02f)
                return;
            Effect fx = SoulFlameShader;
            Texture2D glow = ACMAsset.SoftGlow;
            if (fx == null || glow == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1.4f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uColorCore"]?.SetValue(new Color(225, 244, 222).ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(glow, worldPos - Main.screenPosition, null, Color.White, 0f,
                glow.Size() * 0.5f, scale * 96f / glow.Width, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>手部 impact 回传: 头颅受震下沉 (secondary motion, 纯视觉)。</summary>
        public void NotifyImpactShake(float impulse) {
            headBobVel += impulse;
        }

        // ================================================================
        //  节拍表 (authored cycles) — 攻击序列本身就是编排
        // ================================================================

        private enum BeatAction
        {
            SlamL, SlamR,       // 崩掌拍落 (左/右)
            SweepL, SweepR,     // 白骨横扫
            VolleyL, VolleyR,   // 指骨连环
            ClapH, ClapDiag,    // 合掌夹击 (水平轴 / 斜轴)
            SoulVolley,         // 魂火吐息 (头)
            SpiralTomb,         // 万骸旋冢
            BoneRain,           // 骨雨审判 (Marker ×3)
            ClapAlt, SlamAlt    // 城闭轮替: 按 scoreCycle 奇偶交替轴向/左右
        }

        // P1 役骸: 重-轻-中-重-重-远程, 招间 40~60f 呼吸拍
        private static readonly (int T, BeatAction A)[] Score1 = {
            (30,  BeatAction.SlamR),
            (160, BeatAction.SweepL),
            (270, BeatAction.VolleyR),
            (380, BeatAction.SlamL),
            (500, BeatAction.ClapH),
            (620, BeatAction.SoulVolley),
        };
        private const int Score1Len = 720;

        // P2 判骸: 错拍双拍 → 斜轴合掌 → 交叉扇 → 旋冢 → 吐息
        private static readonly (int T, BeatAction A)[] Score2 = {
            (20,  BeatAction.SlamL),
            (84,  BeatAction.SlamR),
            (220, BeatAction.ClapDiag),
            (370, BeatAction.VolleyL),
            (384, BeatAction.VolleyR),
            (480, BeatAction.SpiralTomb),
            (716, BeatAction.SoulVolley),
        };
        private const int Score2Len = 820;

        // P3 城闭: 高压 3 招轮替 (交替参数保证两循环不完全相同)
        private static readonly (int T, BeatAction A)[] CityScore = {
            (30,  BeatAction.BoneRain),
            (95,  BeatAction.ClapAlt),
            (230, BeatAction.SlamAlt),
        };
        private const int CityScoreLen = 320;

        // ================================================================
        //  ModNPC 基础
        // ================================================================

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 120;
            NPC.defense = 60;
            NPC.lifeMax = 800000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 100000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.aiStyle = -1;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(ritualTriggered);
            writer.Write(cityGateTriggered);
            writer.Write(scoreCycle);
            writer.Write(ritualStage);
            writer.WriteVector2(ritualCenter);
            writer.Write(ritualRadius);
            writer.Write(ritualGateSeed);
            writer.Write(ritualBreakProgress);
            writer.Write(ritualBroken);
            writer.Write(vulnerableTimer);
            writer.WriteVector2(cityCenter);
            writer.Write(cityRadius);
            writer.WriteVector2(tombCenter);
            writer.Write(tombAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ritualTriggered = reader.ReadBoolean();
            cityGateTriggered = reader.ReadBoolean();
            scoreCycle = reader.ReadInt32();
            ritualStage = reader.ReadInt32();
            ritualCenter = reader.ReadVector2();
            ritualRadius = reader.ReadSingle();
            ritualGateSeed = reader.ReadInt32();
            ritualBreakProgress = reader.ReadSingle();
            ritualBroken = reader.ReadBoolean();
            vulnerableTimer = reader.ReadInt32();
            cityCenter = reader.ReadVector2();
            cityRadius = reader.ReadSingle();
            tombCenter = reader.ReadVector2();
            tombAngle = reader.ReadSingle();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.8f);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Corpsefragments>(), 1, 23, 30));
        }

        public override bool CheckActive() => false;

        public int GetBossDamage(float scaling = 1f) => (int)(NPC.damage * scaling);

        // ================================================================
        //  AI 主循环
        // ================================================================

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;

            if (!handsInitialized)
                InitializeHandReferences();

            // 目标选择 (无有效目标 → 撤离)
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.4f;
                    NPC.EncourageDespawn(10);
                    return;
                }
            }

            PhaseTimer++;
            if (vulnerableTimer > 0)
                vulnerableTimer--;

            // 演出期锁伤; 其余可打
            NPC.dontTakeDamage = Phase == BossPhase.Intro || Phase == BossPhase.Death;

            CheckPhaseGates();

            Vector2 prevCenter = NPC.Center;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Score1: RunScore(target, Score1, Score1Len); break;
                case BossPhase.Ritual: RunRitual(target); break;
                case BossPhase.Score2: RunScore(target, Score2, Score2Len); break;
                case BossPhase.CityGate: RunCityGate(target); break;
                case BossPhase.Death: RunDeath(); break;
            }

            // 与节拍表并行的头颅招式/旋冢子程序
            TickSoulVolley(target);
            TickSpiralTomb(target);

            // 位移折算为 velocity 并回退半步, 由引擎统一施加
            // (供速度门控拖尾/联机插值使用, 且避免直设位置与残留 velocity 叠加)
            Vector2 moved = NPC.Center - prevCenter;
            NPC.position -= moved;
            NPC.velocity = moved;

            TickVisualScalars();
        }

        private void InitializeHandReferences() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is CorpsesHand hand && npc.ai[0] == NPC.whoAmI) {
                    if (npc.ai[1] > 0)
                        rightHand = hand;
                    else if (npc.ai[1] < 0)
                        leftHand = hand;
                }
            }
            if (leftHand != null && rightHand != null)
                handsInitialized = true;
        }

        private bool HandsReady => handsInitialized
            && leftHand != null && leftHand.NPC.active
            && rightHand != null && rightHand.NPC.active;

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            HeadCastTimer = 0;
            TombTimer = 0;
            scoreCycle = 0;
            NPC.netUpdate = true;

            if (newPhase == BossPhase.Ritual) {
                ritualStage = 0;
                ritualBreakProgress = 0f;
                ritualBroken = false;
                ritualRadius = RitualStartRadius;
                // 生门方位取确定性种子 (whoAmI + 血量), 各端一致
                ritualGateSeed = (NPC.whoAmI * 7919 + NPC.life) % 100000;
                Player tgt = Main.player[NPC.target];
                ritualCenter = tgt.active ? tgt.Center : NPC.Center;
            }
            else if (newPhase == BossPhase.CityGate) {
                cityRadius = CityStartRadius;
                Player tgt = Main.player[NPC.target];
                cityCenter = tgt.active ? tgt.Center : NPC.Center;
            }
        }

        // —— 血量门控: 60% 引魂大阵 (单次) / 30% 城门闭合 (终幕) ——
        private void CheckPhaseGates() {
            // 城门不打断进行中的引魂大阵 (set-piece 完整性), 结算回 Score2 后自然触发
            if (!cityGateTriggered && NPC.life < NPC.lifeMax * 0.3f
                && (Phase == BossPhase.Score1 || Phase == BossPhase.Score2)) {
                cityGateTriggered = true;
                ClearHostileProjectiles();
                ReleaseHands();
                TransitionTo(BossPhase.CityGate);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.CityGate"),
                    TelegraphColors.Execution);
                SoundEngine.PlaySound(SoundID.DoorClosed with { Pitch = -0.6f, Volume = 1.4f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                return;
            }

            if (!ritualTriggered && NPC.life < NPC.lifeMax * 0.6f
                && (Phase == BossPhase.Score1 || Phase == BossPhase.Score2)) {
                ritualTriggered = true;
                ClearHostileProjectiles();
                ReleaseHands();
                TransitionTo(BossPhase.Ritual);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualStart"),
                    UnderworldField.DecreeColor);
            }
        }

        private void ReleaseHands() {
            if (leftHand != null && leftHand.NPC.active) leftHand.ReleaseToIdle();
            if (rightHand != null && rightHand.NPC.active) rightHand.ReleaseToIdle();
        }

        // 换阶段清弹 (公平阀门): 移除本 Boss 全部存活敌对弹
        private void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int wave = ModContent.ProjectileType<CorpsesClapWave>();
            int bone = ModContent.ProjectileType<CorpsesBoneShower>();
            int orb = ModContent.ProjectileType<CorpsesShadowOrb>();
            int marker = ModContent.ProjectileType<CorpsesBoneRainMarker>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.hostile && (p.type == wave || p.type == bone || p.type == orb || p.type == marker))
                    p.Kill();
            }
        }

        private void AnnounceCenter(string text, Color color) {
            if (Main.dedServ || string.IsNullOrEmpty(text))
                return;
            CombatText.NewText(new Rectangle((int)NPC.Center.X, (int)NPC.Center.Y - 120, 1, 1), color, text, true);
        }

        // 头颅软跟随锚位 + 距离栓绳 (位移在 AI 尾部统一折算为 velocity)
        private void SeekAnchor(Vector2 anchor, float rate) {
            NPC.Center += (anchor - NPC.Center) * rate;
            Player target = Main.player[NPC.target];
            if (target.active && Vector2.Distance(NPC.Center, target.Center) > 1200f)
                NPC.Center = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.Zero) * 1200f;
        }

        // 探地: 从 from 向下找地表 (多端 tile 数据一致 → 确定性落点)
        private static Vector2 FindGroundBelow(Vector2 from, float maxDown = 720f) {
            int tx = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int endY = startY + (int)(maxDown / 16f);
            for (int y = startY; y <= endY; y++) {
                if (!WorldGen.InWorld(tx, y, 8))
                    break;
                Tile t = Framing.GetTileSafely(tx, y);
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return new Vector2(from.X, y * 16f - 6f);
            }
            return from + new Vector2(0f, maxDown * 0.55f); // 无地面: 空中拍击
        }

        // ================================================================
        //  入场演出 (~250f): 尸雾 → 破土 → 双手现身 → 眼焰点燃 → 静止凝视
        // ================================================================

        private void RunIntro(Player target) {
            int t = (int)PhaseTimer;

            if (t < 90) {
                // 蛰伏地下: 尸雾涌起 + 低鸣渐强, 头颅不可见
                NPC.Center = target.Center + new Vector2(0f, 540f);
                NPC.velocity = Vector2.Zero;
                if (t == 20)
                    SoundEngine.PlaySound(SoundID.Zombie40 with { Pitch = -0.8f, Volume = 0.8f }, target.Center);
                if (!Main.dedServ && t > 30 && t % 4 == 0) {
                    // 地面裂隙骨尘预兆
                    Vector2 crack = FindGroundBelow(target.Center + new Vector2(Main.rand.NextFloat(-120f, 120f), -40f));
                    var d = Dust.NewDustPerfect(crack, DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), -2.5f));
                    d.scale = 1.3f;
                }
                ACMUtils.AddScreenShake(t / 90f * 3f); // 渐强低鸣震
            }
            else if (t <= 104) {
                // 破土上冲: poly ease-out, 12 帧走完
                float p = (t - 90) / 14f;
                float e = 1f - MathF.Pow(1f - p, 8f);
                Vector2 from = target.Center + new Vector2(0f, 540f);
                Vector2 to = target.Center + new Vector2(0f, -270f);
                NPC.Center = Vector2.Lerp(from, to, e);
                if (t == 90) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f, Pitch = -0.5f }, target.Center);
                    ACMUtils.AddScreenShake(13f);
                    if (!Main.dedServ) {
                        Vector2 burst = FindGroundBelow(target.Center);
                        for (int i = 0; i < 46; i++) {
                            var d = Dust.NewDustPerfect(burst, DustID.Bone,
                                new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-14f, -4f)));
                            d.scale = Main.rand.NextFloat(1.2f, 2.2f);
                        }
                        for (int i = 0; i < 24; i++) {
                            var d = Dust.NewDustPerfect(burst, DustID.Shadowflame, Main.rand.NextVector2Circular(10f, 10f));
                            d.noGravity = true; d.scale = 2f;
                        }
                    }
                }
            }
            else {
                // 悬停凝视 (menace is stillness)
                SeekAnchor(target.Center + new Vector2(0f, -270f), 0.05f);

                // 双手先后于头颅两侧尸雾中重凝
                if (Main.netMode != NetmodeID.MultiplayerClient && HandsSpawnBeat(t))
                    NPC.netUpdate = true;

                // 眼焰点燃: 三次闪烁后稳定 (视觉标量在 TickVisualScalars 内推进)
                if (t == 150)
                    SoundEngine.PlaySound(SoundID.Item104 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
            }

            if (PhaseTimer >= 250) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.Awaken"),
                    TelegraphColors.NetherViolet);
                TransitionTo(BossPhase.Score1);
            }
        }

        private bool HandsSpawnBeat(int t) {
            if (t == 120 && leftHand == null) {
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, -1);
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].ModNPC is CorpsesHand h)
                    h.BeginMaterialize(NPC.Center + new Vector2(-165f, 20f));
                return true;
            }
            if (t == 140 && rightHand == null) {
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<CorpsesHand>(), 0, NPC.whoAmI, 1);
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].ModNPC is CorpsesHand h)
                    h.BeginMaterialize(NPC.Center + new Vector2(165f, 20f));
                return true;
            }
            return false;
        }

        // ================================================================
        //  节拍表阶段 (Score1 / Score2)
        // ================================================================

        private void RunScore(Player target, (int T, BeatAction A)[] score, int scoreLen) {
            // 头颅锚位: 玩家上方中距 + 呼吸浮沉; 吐息前摇期刻意减速 (§4 slow startup)
            float castSlow = HeadCastTimer > 56 ? 0.35f : 1f;
            Vector2 anchor = target.Center + new Vector2(0f, -300f + MathF.Sin((float)PhaseTimer * 0.03f) * 24f);
            SeekAnchor(anchor, 0.055f * castSlow);

            if (!HandsReady)
                return;

            int t = (int)PhaseTimer % scoreLen;
            if (t == scoreLen - 1) {
                scoreCycle++;
                NPC.netUpdate = true;
            }

            foreach (var beat in score) {
                if (t == beat.T)
                    ExecuteBeat(beat.A, target);
            }
        }

        private void ExecuteBeat(BeatAction action, Player target) {
            bool spray = Phase != BossPhase.Score1; // P2/P3 拍落带溅射
            switch (action) {
                case BeatAction.SlamL:
                    leftHand.CommandPalmSlam(FindGroundBelow(target.Center + target.velocity * 18f), spray);
                    break;
                case BeatAction.SlamR:
                    rightHand.CommandPalmSlam(FindGroundBelow(target.Center + target.velocity * 18f), spray);
                    break;
                case BeatAction.SlamAlt: {
                    var hand = scoreCycle % 2 == 0 ? leftHand : rightHand;
                    hand.CommandPalmSlam(FindGroundBelow(target.Center + target.velocity * 18f), true);
                    break;
                }
                case BeatAction.SweepL:
                    leftHand.CommandBoneSweep(target.Center + target.velocity * 14f, Vector2.UnitX);
                    break;
                case BeatAction.SweepR:
                    rightHand.CommandBoneSweep(target.Center + target.velocity * 14f, -Vector2.UnitX);
                    break;
                case BeatAction.VolleyL:
                    leftHand.CommandBoneVolley(target.Center);
                    break;
                case BeatAction.VolleyR:
                    rightHand.CommandBoneVolley(target.Center);
                    break;
                case BeatAction.ClapH:
                    CommandClap(target, Vector2.UnitX);
                    break;
                case BeatAction.ClapDiag:
                    CommandClap(target, Vector2.UnitX.RotatedBy(scoreCycle % 2 == 0 ? 0.7f : -0.7f));
                    break;
                case BeatAction.ClapAlt:
                    CommandClap(target, scoreCycle % 2 == 0 ? Vector2.UnitX : Vector2.UnitX.RotatedBy(0.7f));
                    break;
                case BeatAction.SoulVolley:
                    if (HeadCastTimer <= 0)
                        HeadCastTimer = 96;
                    break;
                case BeatAction.SpiralTomb:
                    StartSpiralTomb(target);
                    break;
                case BeatAction.BoneRain:
                    SpawnBoneRain(target);
                    break;
            }
        }

        private void CommandClap(Player target, Vector2 axis) {
            if (!leftHand.IsIdle() || !rightHand.IsIdle())
                return; // 两手必须同时受令, 否则跳过本拍 (节拍表间距通常已保证)
            Vector2 meet = target.Center + target.velocity * 20f;
            leftHand.CommandClapPincer(meet, axis);
            rightHand.CommandClapPincer(meet, axis);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
        }

        // ================================================================
        //  魂火吐息 (头颅招式, HeadCastTimer 驱动)
        // ================================================================

        private void TickSoulVolley(Player target) {
            if (HeadCastTimer <= 0)
                return;
            HeadCastTimer--;
            int e = 96 - (int)HeadCastTimer;

            if (e < 40) {
                // 前摇: 眼焰收束 (converging), 亮度即进度条
                eyeFlameTarget = 0.7f + e / 40f * 0.7f;
                if (!Main.dedServ && e > 8 && e < 30 && Main.rand.NextBool(2)) {
                    Vector2 eye = EyeWorldPos();
                    Vector2 off = Main.rand.NextVector2CircularEdge(90f, 90f);
                    var d = Dust.NewDustPerfect(eye + off, DustID.CursedTorch);
                    d.noGravity = true; d.scale = 1.4f;
                    d.velocity = -off * 0.09f;
                }
                // 最后 10 帧粒子熄灭 (pre-silence)
            }
            else if (e == 40 || e == 52 || e == 64) {
                // 三连魂灯: 每发头颅后坐 (recoil, 纯视觉) + 服务器生成
                Vector2 eye = EyeWorldPos();
                Vector2 dir = (target.Center - eye).SafeNormalize(Vector2.UnitY);
                recoilOffset -= dir * 7f;
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.1f, Volume = 0.9f }, eye);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), eye, dir * 2.2f,
                        ModContent.ProjectileType<CorpsesShadowOrb>(), GetBossDamage(0.6f), 2f);
                }
            }
            else if (e > 76) {
                eyeFlameTarget = 0.7f;
            }
        }

        // 眼焰世界坐标 (贴图 120² 蓝眼位置约 (-15,-9); 逻辑坐标, 不含纯视觉偏移)
        private Vector2 EyeWorldPos() =>
            NPC.Center + new Vector2(-15f, -9f).RotatedBy(NPC.rotation) * NPC.scale;

        // ================================================================
        //  万骸旋冢 (TombTimer 驱动)
        // ================================================================

        private void StartSpiralTomb(Player target) {
            if (!HandsReady || TombTimer > 0)
                return;
            TombTimer = TombTotal;
            tombAngle = 0f;
            tombCenter = target.Center;
            NPC.netUpdate = true;
            leftHand.EnterControlled(target.Center + new Vector2(-330f, 0f), false);
            rightHand.EnterControlled(target.Center + new Vector2(330f, 0f), false);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f }, NPC.Center);
        }

        private void TickSpiralTomb(Player target) {
            if (TombTimer <= 0)
                return;
            if (!HandsReady) {
                TombTimer = 0;
                return;
            }

            TombTimer--;
            int e = TombTotal - (int)TombTimer;
            const int Enter = 30, Orbit = 140, Warn = 166, Snap = 184;

            float radius;
            bool canHit;
            if (e <= Enter) {
                // 入轨 (无伤害)
                radius = MathHelper.Lerp(430f, 330f, e / (float)Enter);
                canHit = false;
                tombCenter = target.Center;
            }
            else if (e <= Orbit) {
                // 环绕: 角速渐增, 追踪玩家
                float w = 0.05f + (e - Enter) / (float)(Orbit - Enter) * 0.038f;
                tombAngle += w;
                radius = 330f;
                canHit = true;
                tombCenter = Vector2.Lerp(tombCenter, target.Center, 0.12f);
            }
            else if (e <= Warn) {
                // 收口预警: 中心锁定, 半径微缩, 轴线束变红 (绘制在 PreDraw)
                tombAngle += 0.088f;
                radius = MathHelper.Lerp(330f, 296f, (e - Orbit) / (float)(Warn - Orbit));
                canHit = true;
                if (e == Orbit + 1)
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = -0.2f }, tombCenter);
            }
            else if (e <= Snap) {
                // 收口: poly ease 快速合拢
                float p = (e - Warn) / (float)(Snap - Warn);
                radius = MathHelper.Lerp(296f, 40f, ACMUtils.QuadIn(p));
                canHit = true;

                if (e == Snap) {
                    // 合点冲击
                    impactRingTimer = 14;
                    impactRingPos = tombCenter;
                    leftHand.FlagClapBloom(tombCenter);
                    ACMUtils.AddScreenShake(8f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f }, tombCenter);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int count = 10;
                        for (int i = 0; i < count; i++) {
                            float a = MathHelper.TwoPi * i / count + tombAngle;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tombCenter, a.ToRotationVector2() * 10f,
                                ModContent.ProjectileType<CorpsesClapWave>(), GetBossDamage(0.55f), 3f, Main.myPlayer, 0f, 0f);
                        }
                    }
                }
            }
            else {
                radius = 40f;
                canHit = false;
                if (TombTimer <= 0)
                    ReleaseHands();
            }

            Vector2 lp = tombCenter + tombAngle.ToRotationVector2() * radius;
            Vector2 rp = tombCenter + (tombAngle + MathHelper.Pi).ToRotationVector2() * radius;
            leftHand.DriveControlled(lp, canHit);
            rightHand.DriveControlled(rp, canHit);
        }

        // ================================================================
        //  引魂大阵 set-piece (V2 机制保留, 60% 单次)
        // ================================================================

        private void RunRitual(Player target) {
            Vector2 altarL = ritualCenter + new Vector2(-380f, -120f);
            Vector2 altarR = ritualCenter + new Vector2(380f, -120f);
            SeekAnchor(ritualCenter + new Vector2(0f, -300f), 0.08f);

            if (!HandsReady)
                return;

            if (PhaseTimer < 2) {
                ritualStage = 0;
                LocalRitualReset();
                leftHand.EnterControlled(altarL, false);
                rightHand.EnterControlled(altarR, false);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.3f }, NPC.Center);
            }

            switch (ritualStage) {
                case 0: // 脱体起阵: 双手飞坛, 法阵由虚到实
                    leftHand.DriveControlled(altarL, false);
                    rightHand.DriveControlled(altarR, false);
                    ritualDecalIntensity = MathHelper.Lerp(ritualDecalIntensity, 1f, 0.05f);

                    if (PhaseTimer >= RitualWindup) {
                        ritualStage = 1;
                        leftHand.EnterChanneling(altarL);
                        rightHand.EnterChanneling(altarR);
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: { // 施法收缩: 站生门破阵 / 硬抗
                    leftHand.DriveControlled(altarL, false);
                    rightHand.DriveControlled(altarR, false);
                    ritualDecalIntensity = 1f;

                    float channelTime = PhaseTimer - RitualWindup;
                    float prog = MathHelper.Clamp(channelTime / RitualChannel, 0f, 1f);
                    ritualRadius = MathHelper.Lerp(RitualStartRadius, RitualEndRadius, prog);

                    RitualFieldTick(target);

                    if (ritualBreakProgress >= 1f) {
                        ritualBroken = true;
                        ritualStage = 2;
                        PhaseTimer = RitualWindup + RitualChannel; // 直接进结算段
                        ResolveRitual(target);
                        NPC.netUpdate = true;
                    }
                    else if (channelTime >= RitualChannel) {
                        ritualBroken = false;
                        ritualStage = 2;
                        ResolveRitual(target);
                        NPC.netUpdate = true;
                    }
                    break;
                }

                case 2: // 结算余韵 → 回归 Score2
                    ritualDecalIntensity = MathHelper.Lerp(ritualDecalIntensity, 0f, 0.08f);
                    if (PhaseTimer >= RitualWindup + RitualChannel + 70) {
                        if (!ritualBroken)
                            ReleaseHands(); // 破阵时双手硬直中, 不打扰
                        TransitionTo(BossPhase.Score2);
                    }
                    break;
            }
        }

        private void LocalRitualReset() {
            ritualDecalIntensity = 0f;
            ritualBreakProgress = 0f;
            ritualBroken = false;
            ritualRadius = RitualStartRadius;
        }

        private float GateBaseAngle => (ritualGateSeed % 628) / 100f;
        private const float GateHalfWidth = 0.42f;

        private bool IsInGate(float angle) {
            for (int i = 0; i < GateSlotCount; i++) {
                float ga = GateBaseAngle + i * MathHelper.TwoPi / GateSlotCount;
                if (Math.Abs(MathHelper.WrapAngle(angle - ga)) < GateHalfWidth)
                    return true;
            }
            return false;
        }

        private void RitualFieldTick(Player target) {
            Vector2 rel = target.Center - ritualCenter;
            float dist = rel.Length();
            bool insideArray = dist < ritualRadius + 40f;
            bool inGate = IsInGate(rel.ToRotation());

            if (insideArray && inGate) {
                // 站生门: 累计破阵 (~2.5s 站满即破)
                ritualBreakProgress += 1f / 150f;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.GoldFlame);
                    d.noGravity = true; d.scale = 1.3f; d.velocity = new Vector2(0, -2f);
                }
            }
            else if (insideArray) {
                // 收缩死区: 魂蚀 DoT
                if ((int)PhaseTimer % 18 == 0)
                    UnderworldField.AddSoulErosion(target, 1);
            }
            else {
                ritualBreakProgress = MathHelper.Max(0f, ritualBreakProgress - 1f / 600f);
            }
        }

        private void ResolveRitual(Player target) {
            if (ritualBroken) {
                // 破阵: 双手重伤硬直 + 头颅破绽 ~5s (受伤 ×1.6)
                leftHand.StunHand(300);
                rightHand.StunHand(300);
                vulnerableTimer = 300;
                ACMUtils.AddScreenShake(10f);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualBreak"),
                    TelegraphColors.Safe);
            }
            else {
                // 魂祭已成: 一层冥律 + 一次性可躲镇压波
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    UnderworldField.AddNetherDecree(target, 1);
                    int count = 22;
                    for (int i = 0; i < count; i++) {
                        float a = MathHelper.TwoPi * i / count;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), ritualCenter, a.ToRotationVector2() * 12f,
                            ModContent.ProjectileType<CorpsesClapWave>(), GetBossDamage(0.65f), 3f, Main.myPlayer, 0f, 0f);
                    }
                    for (int i = 0; i < 6; i++) {
                        Vector2 vel = new(MathHelper.Lerp(-6.5f, 6.5f, i / 5f), -10f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), ritualCenter, vel,
                            ModContent.ProjectileType<CorpsesBoneShower>(), GetBossDamage(0.5f), 2f, Main.myPlayer, 0f, 1f);
                    }
                }
                ACMUtils.AddScreenShake(11f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.RitualComplete"),
                    TelegraphColors.Execution);
            }
        }

        // ================================================================
        //  城门闭合终幕 (30%)
        // ================================================================

        private void RunCityGate(Player target) {
            // 城墙缓慢收缩, 中心极缓追随 (边界可读)
            cityRadius = MathHelper.Max(CityEndRadius, cityRadius - 0.35f);
            cityCenter = Vector2.Lerp(cityCenter, target.Center, 0.004f);
            SeekAnchor(cityCenter + new Vector2(0f, -300f), 0.05f);

            // 城墙外: 内推 + 魂蚀 (墙体可见, telegraphed)
            float pd = Vector2.Distance(target.Center, cityCenter);
            if (pd > cityRadius) {
                Vector2 inward = (cityCenter - target.Center).SafeNormalize(Vector2.Zero);
                target.velocity += inward * 0.6f;
                if ((int)PhaseTimer % 16 == 0)
                    UnderworldField.AddSoulErosion(target, 1);
            }

            if (!HandsReady)
                return;

            int t = (int)PhaseTimer % CityScoreLen;
            if (t == CityScoreLen - 1) {
                scoreCycle++;
                NPC.netUpdate = true;
            }
            foreach (var beat in CityScore) {
                if (t == beat.T)
                    ExecuteBeat(beat.A, target);
            }
        }

        private void SpawnBoneRain(Player target) {
            SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    // 一枚咬住玩家当前位置, 两枚铺开; ai0 负起步 → 波次错拍 (随弹幕同步)
                    Vector2 probe = i == 0
                        ? target.Center
                        : cityCenter + new Vector2(Main.rand.NextFloat(-0.8f, 0.8f) * cityRadius, -40f);
                    Vector2 mark = FindGroundBelow(probe + new Vector2(0f, -60f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), mark, Vector2.Zero,
                        ModContent.ProjectileType<CorpsesBoneRainMarker>(), GetBossDamage(0.55f), 0f, Main.myPlayer, -i * 9f);
                }
            }
        }

        // ================================================================
        //  死亡演出 (~240f 锁血)
        // ================================================================

        public override bool CheckDead() {
            if (Phase != BossPhase.Death) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                ClearHostileProjectiles();
                TransitionTo(BossPhase.Death);
                return false;
            }
            return true;
        }

        // 骨裂声递减间隔阵列 (加速心跳)
        private static readonly int[] CrackBeats = { 20, 56, 86, 110, 130, 146, 158, 167, 174, 179 };

        private void RunDeath() {
            int t = (int)PhaseTimer;
            NPC.velocity *= 0.9f;
            NPC.Center += new Vector2(0f, 0.35f); // 缓缓下沉

            // 双手先后崩解坠地
            if (t == 30 && leftHand != null && leftHand.NPC.active)
                leftHand.BeginDeathCollapse();
            if (t == 70 && rightHand != null && rightHand.NPC.active)
                rightHand.BeginDeathCollapse();

            // 骨裂声按递减间隔加密, 音调渐升
            foreach (int beat in CrackBeats) {
                if (t == beat) {
                    float p = beat / 180f;
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.6f + p * 0.8f, Volume = 0.8f + p * 0.4f }, NPC.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 5; i++) {
                            var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(56f, 56f), DustID.Bone,
                                new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-2f, 3f)));
                            d.scale = Main.rand.NextFloat(1f, 1.7f);
                        }
                    }
                    ACMUtils.AddScreenShake(2f + p * 4f);
                }
            }

            // 180~205: 眼焰熄灭 + 完全静默 (miasma 骤降由 TickVisualScalars 处理)
            if (t == 180)
                SoundEngine.PlaySound(SoundID.Zombie40 with { Pitch = -1f, Volume = 0.6f }, NPC.Center);

            // 205: 崩解爆发 (白化对比帧 + 大震 + 骨片喷泉)
            if (t == 205) {
                deathFlash = 1f;
                ACMUtils.AddScreenShake(16f);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                AnnounceCenter(Language.GetTextValue("Mods.AncientChineseMythology.Corpses.Collapse"),
                    TelegraphColors.GhostGreen);
                if (!Main.dedServ) {
                    for (int i = 0; i < 70; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, DustID.Bone, Main.rand.NextVector2Circular(13f, 11f));
                        d.scale = Main.rand.NextFloat(1.3f, 2.4f);
                    }
                    for (int i = 0; i < 40; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(15f, 15f));
                        d.noGravity = true; d.scale = 2.4f;
                    }
                }
            }

            // 240: 真死结算
            if (t >= 240 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
                NPC.netUpdate = true;
            }
        }

        public override void OnKill() {
            // 进度旗标: 枉死城门 (解锁觉醒冥龙等), 不可回退
            if (Main.netMode != NetmodeID.MultiplayerClient)
                DownedBossSystem.downedCorpses = true;

            // 兜底: 双手随头颅消亡
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (leftHand != null && leftHand.NPC.active) { leftHand.NPC.life = 0; leftHand.NPC.active = false; }
                if (rightHand != null && rightHand.NPC.active) { rightHand.NPC.life = 0; rightHand.NPC.active = false; }
            }
        }

        // ================================================================
        //  受击 / 碰撞
        // ================================================================

        // 破绽窗口: 受伤 ×1.6 (破阵奖励正反馈)
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (vulnerableTimer > 0)
                modifiers.FinalDamage *= 1.6f;
        }

        // 演出/施法期零接触伤害 (公平契约)
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return Phase != BossPhase.Intro && Phase != BossPhase.Death
                && Phase != BossPhase.Ritual && vulnerableTimer <= 0;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            UnderworldField.AddSoulErosion(target, 2);
        }

        // ================================================================
        //  纯视觉标量推进 (本地)
        // ================================================================

        private void TickVisualScalars() {
            if (Main.dedServ)
                return;

            // 尸雾目标强度: 随阶段递进
            float miasmaTarget = Phase switch {
                BossPhase.Intro => MathHelper.Clamp(PhaseTimer / 90f, 0f, 1f) * 0.55f,
                BossPhase.Score1 => 0.42f,
                BossPhase.Ritual => 0.75f,
                BossPhase.Score2 => 0.58f,
                BossPhase.CityGate => 0.9f,
                BossPhase.Death => PhaseTimer < 180 ? 0.6f : 0.12f, // 爆发前静默骤降
                _ => 0f
            };
            miasma = MathHelper.Lerp(miasma, miasmaTarget, 0.03f);
            deathFlash *= 0.9f;

            // 眼焰: 入场点燃三闪 → 稳定; 破绽熄灭; 死亡末段熄灭
            if (Phase == BossPhase.Intro) {
                int t = (int)PhaseTimer;
                eyeFlameTarget = t switch {
                    < 120 => 0f,
                    < 200 => (t / 14) % 2 == 0 ? 0.9f : 0.15f, // 点燃闪烁
                    _ => 0.7f
                };
            }
            else if (Phase == BossPhase.Death) {
                eyeFlameTarget = PhaseTimer < 180 ? 0.5f + 0.4f * MathF.Sin((float)PhaseTimer * 0.23f) : 0f;
            }
            else if (vulnerableTimer > 0) {
                eyeFlameTarget = 0.05f; // 破绽: 眼焰熄灭 (状态广播)
            }
            else if (HeadCastTimer <= 0) {
                eyeFlameTarget = 0.7f;
            }
            eyeFlame = MathHelper.Lerp(eyeFlame, eyeFlameTarget, 0.12f);

            // 受震弹簧 + 后坐衰减 (secondary motion)
            headBobVel -= headBob * 0.12f;
            headBobVel *= 0.86f;
            headBob += headBobVel;
            recoilOffset *= 0.88f;

            if (impactRingTimer > 0)
                impactRingTimer--;
        }

        private Vector2 DrawOffset() => new(recoilOffset.X, recoilOffset.Y + headBob * 4f);

        // ================================================================
        //  绘制
        // ================================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 图鉴假体: 直接画主体, 不走演出/批切换路径
            if (NPC.IsABestiaryIconDummy) {
                spriteBatch.Draw(texture, NPC.Center - Main.screenPosition, null, drawColor,
                    NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0);
                return false;
            }

            // set-piece decal 层
            if (Phase == BossPhase.Ritual && ritualDecalIntensity > 0.01f)
                DrawRitualVisuals(spriteBatch);
            else if (Phase == BossPhase.CityGate)
                DrawCityVisuals(spriteBatch);

            // 旋冢收口预警: 合拢轴线束变红
            if (TombTimer > 0) {
                int e = TombTotal - (int)TombTimer;
                if (e is > 140 and <= 184) {
                    float prog = MathHelper.Clamp((e - 140) / 26f, 0f, 1f);
                    DrawBoneRingDecal(spriteBatch, 2, tombCenter, 40f, 0.85f, prog,
                        tombAngle.ToRotationVector2(), 380f, TelegraphColors.Lethal, TelegraphColors.NetherViolet);
                }
            }
            // 旋冢合点冲击环残留
            if (impactRingTimer > 0) {
                float p = 1f - impactRingTimer / 14f;
                DrawBoneRingDecal(spriteBatch, 1, impactRingPos, 320f, 1f, p,
                    Vector2.UnitX, 0f, new Color(225, 240, 220), TelegraphColors.GhostGreen);
            }

            // Intro 前 90 帧蛰伏地下: 不绘制本体
            if (Phase == BossPhase.Intro && PhaseTimer < 90)
                return false;

            Vector2 drawCenter = NPC.Center + DrawOffset() - Main.screenPosition;

            // 拖尾: 仅高速运动时渐显 (速度门控, dressing 不常开)
            float speed = NPC.velocity.Length();
            if (speed > 7f) {
                float trailOpacity = MathHelper.Clamp((speed - 7f) / 10f, 0f, 1f) * 0.4f;
                for (int i = 0; i < NPC.oldPos.Length; i += 2) {
                    Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - Main.screenPosition;
                    float fade = 1f - i / (float)NPC.oldPos.Length;
                    spriteBatch.Draw(texture, drawPos, null, (TelegraphColors.NetherViolet with { A = 0 }) * (trailOpacity * fade),
                        NPC.rotation, origin, NPC.scale * (0.94f + 0.06f * fade), SpriteEffects.None, 0);
                }
            }

            // 主体色: 破绽 Safe 呼吸高光 / 死亡发暗
            Color mainColor = drawColor;
            if (vulnerableTimer > 0) {
                float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);
                mainColor = Color.Lerp(mainColor, TelegraphColors.Safe, 0.4f + 0.3f * pulse);
            }
            if (Phase == BossPhase.Death)
                mainColor = Color.Lerp(mainColor, new Color(50, 42, 60), MathHelper.Clamp(PhaseTimer / 200f, 0f, 0.6f));

            float scale = NPC.scale;
            if (Phase == BossPhase.Intro && PhaseTimer < 110)
                scale *= MathHelper.Lerp(0.85f, 1f, (PhaseTimer - 90f) / 20f);

            spriteBatch.Draw(texture, drawCenter, null, mainColor, NPC.rotation, origin, scale, SpriteEffects.None, 0);

            // 眼焰 (状态广播: 蓄力变亮 / 破绽熄灭)
            if (eyeFlame > 0.03f)
                DrawSoulFlame(spriteBatch, NPC.Center + DrawOffset() + new Vector2(-15f, -9f).RotatedBy(NPC.rotation) * scale,
                    0.85f + eyeFlame * 0.35f, eyeFlame, NPC.whoAmI * 2.13f);

            return false;
        }

        // 引魂大阵: prison-overlay 法阵 + 双手锁链 + 生门安全缝
        private void DrawRitualVisuals(SpriteBatch sb) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                ACMShaders.WorldDecalParams(ritualCenter, ritualRadius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(ritualDecalIntensity, 0f, 1f));
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(UnderworldField.SoulBoundColor.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(12f);
                fx.Parameters["uMode"]?.SetValue(1f);
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(sb, fx);
            }

            if (leftHand != null && leftHand.NPC.active)
                ACMShaders.DrawBeam(leftHand.NPC.Center, ritualCenter, 10f,
                    TelegraphColors.NetherViolet, TelegraphColors.GhostGreen, ritualDecalIntensity, 1.6f, 2.2f);
            if (rightHand != null && rightHand.NPC.active)
                ACMShaders.DrawBeam(rightHand.NPC.Center, ritualCenter, 10f,
                    TelegraphColors.NetherViolet, TelegraphColors.GhostGreen, ritualDecalIntensity, 1.6f, 2.2f);

            // 生门安全缝: 柔白光束 (玩家据此破阵)
            if (ritualStage == 1) {
                for (int i = 0; i < GateSlotCount; i++) {
                    float ga = GateBaseAngle + i * MathHelper.TwoPi / GateSlotCount;
                    Vector2 outer = ritualCenter + ga.ToRotationVector2() * (ritualRadius + 30f);
                    ACMShaders.DrawBeam(ritualCenter, outer, 26f,
                        TelegraphColors.Safe, TelegraphColors.Holy, 0.55f, 0.8f, 1.5f);
                }
            }
        }

        // 城门闭合: prison-overlay 收缩城墙
        private void DrawCityVisuals(SpriteBatch sb) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;
            ACMShaders.WorldDecalParams(cityCenter, cityRadius, out Vector2 uv, out float rFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rFrac);
            fx.Parameters["uIntensity"]?.SetValue(0.9f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(UnderworldField.DecreeColor.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(14f);
            fx.Parameters["uMode"]?.SetValue(1f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx);
        }

        // 全屏尸雾后处理: 唯一全屏件, 严格走名额契约 (强度过低直接让位)
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            if (miasma < 0.01f && deathFlash < 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = MiasmaShader;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(miasma, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(deathFlash, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx, bindNoise: true);
        }
    }
}

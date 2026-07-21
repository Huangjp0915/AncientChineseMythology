using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎 — 西方·金·啸震山河 (V3)。
    ///
    /// V2 的「猎手循环」概念保留，V3 补上它缺失的<b>身体语言与元素身份</b>：
    ///   • 扑杀语言重做 —— 伏低下沉 + 纵向压扁 + <c>pow(t,6)</c> 反向蓄势 late-snap + 一瞬 set 74/88px/f
    ///     + 硬刹(×0.62/f)；蓄势扑击命中后追加两段 Z 字三连杀。速度即对比 (MOTION §2/§3)。
    ///   • 潜行 Stalk 不再恒速绕圈 —— 弧线偏向玩家背后、速度呼吸起伏、两次假扑心理压迫、≤550px 栓绳。
    ///   • 金与音的身份 —— 新招「音啸破阵 Sonic Howl」(三道带旋转缺口的音波环, BaihuSonicRing.fx)；
    ///     铁壁缺口=白虎面朝方向(虎啸宣告)；BaihuMetalSheen.fx 本体金属高光 P2 起常驻；
    ///     爪击/射线接 BaihuClawRend.fx 三道耙痕撕裂。
    ///   • P3「白虎之形」真落地 —— FindGroundY + SpringDamp 弹簧贴地步进；震地踏改为下蹲→跃起→
    ///     悬滞 6 帧→velocity set 40 砸地的完整冲击链 (MOTION §5)。
    ///   • 三大演出 —— 入场「裂空而来」(空中爪痕撕裂→跃出砸地→静止 60 帧凝视)；相变 P2 长啸清弹、
    ///     P3 流星落地；死亡「金断啸终」(CheckDead 拦截→踉跄→力竭一跃→最后一啸→DissolveBurn 溶解向西)。
    /// 伤害窗口与视觉严格对齐：接触伤害仅在扑击/砸地的高速帧生效 (<see cref="CanHitPlayer"/>)。
    /// 震屏一律 <see cref="ACMUtils.AddScreenShake"/> (落地 4–6 / 相变·大招 8–12 / 入场·死亡 ≤16)。
    /// </summary>
    [AutoloadBossHead]
    public class Baihu : SacredBeastBase
    {
        #region 四圣兽身份

        public override SacredElement Element => SacredElement.Metal;
        public override string SkyName => BaihuSky.SkyName;

        #endregion

        #region 状态枚举

        public enum BaihuState
        {
            Intro,
            // 一/二阶段 猎手循环
            Stalk,          // 潜行：纯追踪零弹，蓄势 + 假扑
            Pounce,         // 扑击：唯一高伤窗口(反向蓄势 late-snap)
            ClawSwipe,      // 爪击扇：施加爪痕、积蓄
            MetallicEcho,   // 金属回响：每第三片才真
            IronWall,       // 铁壁：面朝方向宣告缺口(二阶段起)
            PhaseTransition2,
            PhaseTransition3,
            // 三阶段 白虎之形(真落地)
            RiftClaw,       // 裂地灭世爪(签名)
            QuakeStomp,     // 震地踏：跃起→悬滞→砸地双震波
            RendBeams,      // 爪裂射线
            // V3 新增(追加在尾部, 不动既有枚举值)
            PounceCombo,    // 蓄势三连杀：两段 Z 字短冲
            SonicHowl,      // 音啸破阵：三道旋转缺口音波环(P2+)
            Death           // 死亡演出「金断啸终」
        }

        private BaihuState State {
            get => (BaihuState)RawState;
            set => RawState = (int)value;
        }

        #endregion

        #region 字段

        // ---- 同步字段 (SendExtraAI 顺序两端一致) ----
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private int clawCharge;          // 爪痕蓄能(每 3 触发蓄势扑击)
        private bool pounceEmpowered;    // 本次扑击是否蓄势(银爪预兆)
        private Vector2 pounceDir = Vector2.UnitX; // 扑击/三连杀锁定方向
        private float ironGapAngle;      // 铁壁安全缺口朝向(=白虎面朝方向)
        private int comboIndex;          // 三连杀剩余段数
        private bool deathAnimStarted;   // CheckDead 拦截标志

        // ---- 本地运动字段 (各端从同步 ai 确定性推导, 不同步) ----
        private Vector2 springVel;       // SpringDamp 弹簧速度分量
        private Vector2 pounceAnchor;    // 扑击前摇锚点(伏低/后拉参照)
        private float prowlAngle;        // 潜行环绕角
        private Vector2 crackCenter;     // 入场裂痕中心(各端同帧计算)
        private bool slamLanded;         // 本状态砸地是否已触地
        private int slamLandTick;        // 触地时的 AttackTimer
        private float stompGroundY;      // 震地踏地面 Y 缓存(预警圈用)
        private int stepTimer;           // 落地步进节拍

        // ---- 纯视觉字段 (仅客户端消费) ----
        private float glowIntensity = 1f;
        private float quakeFlash;        // 落地震波泛光脉冲
        private float whiteFlash;        // 白闪(流星/终啸)
        private Vector2 bodyScale = Vector2.One;   // 伏低压扁/吸气鼓胀
        private float bodyFlash;         // MetalSheen uFlash 蓄势闪白
        private float sheenIntensity;    // 金属光泽常驻强度(P2 起淡入)
        private float clawFlashAge = 999f;         // 爪击扇耙痕闪现年龄
        private Vector2 clawFlashDir = Vector2.UnitX;
        private float dissolveProgress;  // 死亡溶解进度
        private float runicFlash;        // P3 落地全场裂地纹强度
        private Vector2 runicCenter;
        private readonly float[] fxRingAge = { -1f, -1f, -1f }; // 演出音环(相变清弹/死亡终啸)
        private Vector2 fxRingCenter;
        private bool suppressAmbientDust; // 悬滞/静默段粒子骤停

        // ---- 天幕钩子 (BaihuSky 读取, 静态标量, 参考 Qinlong.s_weatherIntensity 模式) ----
        internal static float s_skyDim;        // 入场啸暗 0~1
        internal static float s_silverHorizon; // P3 落地后地平线银辉 0~1
        internal static float s_skyFlash;      // 流星坠地/死亡终啸白闪 0~1

        // ---- 伤害窗口 (各端由同步状态确定性计算) ----
        private bool contactDamageActive;

        private bool Expert => Main.expertMode;
        private bool OnServer => Main.netMode != NetmodeID.MultiplayerClient;

        #endregion

        #region 确定性轮替

        protected override int[] GetPhaseRotation(int phaseTier) => phaseTier switch {
            1 => new[] {
                (int)BaihuState.Stalk, (int)BaihuState.Pounce, (int)BaihuState.ClawSwipe,
                (int)BaihuState.Stalk, (int)BaihuState.MetallicEcho, (int)BaihuState.Pounce
            },
            // P2: 压制(潜行→扑) → 空间控制(音啸) → 压制(爪击→扑) → 合围(铁壁) → 喘息(潜行→回响)
            2 => new[] {
                (int)BaihuState.Stalk, (int)BaihuState.Pounce, (int)BaihuState.SonicHowl,
                (int)BaihuState.ClawSwipe, (int)BaihuState.Pounce, (int)BaihuState.IronWall,
                (int)BaihuState.Stalk, (int)BaihuState.MetallicEcho
            },
            _ => new[] {
                (int)BaihuState.RiftClaw, (int)BaihuState.QuakeStomp,
                (int)BaihuState.SonicHowl, (int)BaihuState.RendBeams
            }
        };

        private void AdvanceRotation() {
            int next = NextAttack(PhaseTier);
            if (next < 0) next = (int)BaihuState.Stalk;
            TransitionToState(next);
        }

        #endregion

        #region ModNPC 重写

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
            NPC.width = 180;
            NPC.height = 140;
            NPC.damage = 260;
            NPC.defense = 70;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit1;
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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BaihuSpirit>(), 1, 6, 10));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<AurelianCataclysmSmasher>(),
                ModContent.ItemType<ArgentPulseObliterator>(),
                ModContent.ItemType<WhiteTigerClaws>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            State = BaihuState.Intro;
            PhaseTimer = 0;
            if (OnServer) NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            SendSacredBeastAI(writer);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(clawCharge);
            writer.Write(pounceEmpowered);
            writer.Write(ironGapAngle);
            writer.Write(pounceDir.X);
            writer.Write(pounceDir.Y);
            writer.Write(comboIndex);
            writer.Write(deathAnimStarted);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ReceiveSacredBeastAI(reader);
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            clawCharge = reader.ReadInt32();
            pounceEmpowered = reader.ReadBoolean();
            ironGapAngle = reader.ReadSingle();
            pounceDir.X = reader.ReadSingle();
            pounceDir.Y = reader.ReadSingle();
            comboIndex = reader.ReadInt32();
            deathAnimStarted = reader.ReadBoolean();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>接触伤害窗口与视觉严格对齐：只有扑击/三连杀冲刺(速度&gt;30)与砸地坠落帧会撞伤。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => contactDamageActive;

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            BaihuClawMark.Apply(target);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Silver, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Silver, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        /// <summary>
        /// 死亡演出接管：首次致死改为进入「金断啸终」(清弹/无敌/伤害归零)，
        /// 演出结束后由服务器 <see cref="NPC.StrikeInstantKill"/> 真实死亡(掉落/downed 照常)。
        /// </summary>
        public override bool CheckDead() {
            if (!deathAnimStarted) {
                deathAnimStarted = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                contactDamageActive = false;
                if (OnServer) ClearHostileProjectiles();
                TransitionToState((int)BaihuState.Death);
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        public override void OnKill() {
            DownedBossSystem.downedBaihu = true;
            s_skyDim = 0f;
            s_silverHorizon = 0f;
            ACMUtils.AddScreenShake(16f);
        }

        private static void ClearHostileProjectiles() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.hostile && p.damage > 0)
                    p.Kill();
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            // 死亡演出不依赖有效目标(玩家团灭也要把戏演完), 手动推进计时
            if (State == BaihuState.Death) {
                GlobalTime += 1f / 60f;
                PhaseTimer++;
                AttackTimer++;
                UpdateVisualScalars();
                RunDeath();
                Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.92f, 1.0f) * glowIntensity);
                return;
            }

            if (!RunStandardPrologue(out Player target))
                return;

            UpdateVisualScalars();
            CheckPhaseTransition();

            switch (State) {
                case BaihuState.Intro: RunIntro(target); break;
                case BaihuState.Stalk: RunStalk(target); break;
                case BaihuState.Pounce: RunPounce(target); break;
                case BaihuState.PounceCombo: RunPounceCombo(target); break;
                case BaihuState.ClawSwipe: RunClawSwipe(target); break;
                case BaihuState.MetallicEcho: RunMetallicEcho(target); break;
                case BaihuState.IronWall: RunIronWall(target); break;
                case BaihuState.SonicHowl: RunSonicHowl(target); break;
                case BaihuState.PhaseTransition2: RunPhaseTransition2(target); break;
                case BaihuState.PhaseTransition3: RunPhaseTransition3(target); break;
                case BaihuState.RiftClaw: RunRiftClaw(target); break;
                case BaihuState.QuakeStomp: RunQuakeStomp(target); break;
                case BaihuState.RendBeams: RunRendBeams(target); break;
            }

            UpdateContactWindow();
            ApplyLeash(target);
            UpdateFacing(target);
            UpdateAmbientDust();

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.92f, 1.0f) * glowIntensity);
        }

        /// <summary>P2 起本体金属微光呼吸(身份常驻)；悬滞/静默帧骤停 —— 负空间让爆发可读。</summary>
        private void UpdateAmbientDust() {
            if (Main.dedServ || suppressAmbientDust)
                return;
            if (sheenIntensity > 0.25f && Main.rand.NextBool(7)) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.42f, NPC.height * 0.42f);
                Dust d = Dust.NewDustPerfect(dp, DustID.Silver, new Vector2(0, -0.6f), 160, default, 0.9f);
                d.noGravity = true;
            }
        }

        /// <summary>视觉标量推进(各端本地)：闪光衰减、体形缩放回弹、金属光泽淡入、演出音环、天幕钩子。</summary>
        private void UpdateVisualScalars() {
            quakeFlash *= 0.9f;
            whiteFlash = MathF.Max(0f, whiteFlash - 0.03f);
            bodyFlash = MathF.Max(0f, bodyFlash - 0.12f);
            clawFlashAge += 1f;
            runicFlash = MathF.Max(0f, runicFlash - 0.011f);
            suppressAmbientDust = false;

            // 体形回弹(伏低压扁/吸气鼓胀由各招每帧覆写)
            bodyScale = Vector2.Lerp(bodyScale, Vector2.One, 0.18f);

            // 金属光泽: P2 起常驻淡入
            float sheenTarget = didPhase2Transition ? 0.85f : 0f;
            sheenIntensity = MathHelper.Lerp(sheenIntensity, sheenTarget, 0.02f);

            // 演出音环(纯视觉)推进
            for (int i = 0; i < fxRingAge.Length; i++) {
                if (fxRingAge[i] >= 0f) {
                    fxRingAge[i] += 1f;
                    if (fxRingAge[i] > 75f) fxRingAge[i] = -1f;
                }
            }

            // 天幕钩子: 入场啸暗 / P3 银辉 / 白闪衰减
            float dimTarget = State == BaihuState.Intro && (int)SubStateRaw == 0 ? 1f : 0f;
            s_skyDim = MathHelper.Lerp(s_skyDim, dimTarget, dimTarget > s_skyDim ? 0.08f : 0.03f);
            float horizonTarget = didPhase3Transition && State != BaihuState.PhaseTransition3 ? 1f : 0f;
            s_silverHorizon = MathHelper.Lerp(s_silverHorizon, horizonTarget, 0.02f);
            s_skyFlash = MathF.Max(0f, s_skyFlash - 0.035f);
        }

        private void CheckPhaseTransition() {
            if (State == BaihuState.Intro || State == BaihuState.PhaseTransition2 || State == BaihuState.PhaseTransition3)
                return;
            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                TransitionToState((int)BaihuState.PhaseTransition2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                TransitionToState((int)BaihuState.PhaseTransition3);
            }
        }

        /// <summary>接触伤害窗口(各端由同步状态确定性计算)：扑系冲刺 &gt;30px/f、砸地坠落 &gt;24px/f。</summary>
        private void UpdateContactWindow() {
            float spd = NPC.velocity.Length();
            contactDamageActive = State switch {
                BaihuState.Pounce => InStrike && spd > 30f,
                BaihuState.PounceCombo => spd > 30f,
                BaihuState.QuakeStomp => InStrike && NPC.velocity.Y > 24f,
                BaihuState.RiftClaw => InStrike && NPC.velocity.Y > 24f,
                _ => false
            };
        }

        /// <summary>距离栓绳(失败模式防线)：非演出状态远离目标时施加回归加速度, 防止脱战绕圈。</summary>
        private void ApplyLeash(Player target) {
            if (State is BaihuState.Intro or BaihuState.PhaseTransition2 or BaihuState.PhaseTransition3)
                return;
            float dist = Vector2.Distance(NPC.Center, target.Center);
            if (dist > 1400f) {
                Vector2 pull = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                NPC.velocity += pull * MathHelper.Clamp((dist - 1400f) / 400f, 0f, 1f) * 1.1f;
            }
        }

        /// <summary>朝向：默认面向玩家；扑击/三连杀面向冲刺方向；铁壁前摇转身面向缺口(虎啸宣告)。</summary>
        private void UpdateFacing(Player target) {
            int face = target.Center.X >= NPC.Center.X ? 1 : -1;
            if (State == BaihuState.Pounce && !InRecover)
                face = pounceDir.X >= 0f ? 1 : -1;
            else if (State == BaihuState.PounceCombo)
                face = pounceDir.X >= 0f ? 1 : -1;
            else if (State == BaihuState.IronWall && InWindup)
                face = MathF.Cos(ironGapAngle) >= 0f ? 1 : -1;
            NPC.spriteDirection = face;
            NPC.rotation = MathHelper.Clamp(NPC.velocity.X * 0.01f, -0.25f, 0.25f) * NPC.spriteDirection;
        }

        /// <summary>切换 Intro/相变/死亡的演出子阶段(复用 ai[3] 槽位, 清零 AttackTimer)。</summary>
        private void SetCineStage(int stage) {
            SubStateRaw = stage;
            AttackTimer = 0;
            if (OnServer) NPC.netUpdate = true;
        }

        private int CineStage => (int)SubStateRaw;

        #endregion

        #region 地面工具 (P3 真落地)

        /// <summary>向下搜索最近实心地面(世界 Y)。与 Xuanwu 同法, 各端确定性。</summary>
        private static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)(searchStartY / 16f);
            for (int tileY = startTileY; tileY < startTileY + 60; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return searchStartY + 500f;
        }

        /// <summary>
        /// 落地形态步进：垂直弹簧贴地(k=5 快贴合 c=7 微弹跳) + 水平弹簧追踪，
        /// 走动时脚下扬尘 + 步进震感 2 (§6.2 预算内)。
        /// </summary>
        private void GroundedStride(Player target, float xOffset, float maxSpeed = 9f, float crouch = 0f) {
            float groundY = FindGroundY(NPC.Center.X, MathF.Min(NPC.Center.Y, target.Center.Y));
            float anchorY = groundY - NPC.height / 2f + 6f + crouch;
            float newY = ACMUtils.SpringDamp(NPC.Center.Y, anchorY, ref springVel.Y, 5f, 7f, 1f / 60f);
            NPC.velocity.Y = newY - NPC.Center.Y;

            float wantX = target.Center.X + xOffset;
            float newX = ACMUtils.SpringDamp(NPC.Center.X, wantX, ref springVel.X, 1.6f, 3.6f, 1f / 60f);
            NPC.velocity.X = MathHelper.Clamp(newX - NPC.Center.X, -maxSpeed, maxSpeed);

            // 地面步进反馈
            if (MathF.Abs(NPC.velocity.X) > 2.5f) {
                stepTimer++;
                if (stepTimer >= 15) {
                    stepTimer = 0;
                    ACMUtils.AddScreenShake(2f);
                    SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.35f, Pitch = -0.7f }, NPC.Center);
                    if (!Main.dedServ) {
                        Vector2 footPos = NPC.Center + new Vector2(Main.rand.NextFloat(-70, 70), NPC.height / 2f - 8);
                        for (int i = 0; i < 4; i++) {
                            Dust d = Dust.NewDustDirect(footPos, 0, 0, DustID.Smoke,
                                -MathF.Sign(NPC.velocity.X) * 2f, -Main.rand.NextFloat(1f, 3f), 150, default, 1.5f);
                            d.noGravity = false;
                        }
                    }
                }
            }
        }

        /// <summary>本体是否已触地(用于砸地判定)。</summary>
        private bool TouchingGround(out float groundY) {
            groundY = FindGroundY(NPC.Center.X, NPC.Center.Y - 40f);
            return NPC.Center.Y + NPC.height / 2f >= groundY - 4f;
        }

        #endregion

        #region 入场「裂空而来」(~165f)

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true; // 接触伤害由 CanHitPlayer 窗口把关, 无需动 NPC.damage

            switch (CineStage) {
                case 0: // 屏外虎啸(天幕微暗) → 空中三道爪痕错帧亮起
                    if (PhaseTimer == 1) {
                        // 藏到远离视野的高空(裂痕视觉会掩盖之后的瞬移, MOTION 失败模式#3)
                        NPC.Center = target.Center + new Vector2(0, -2200f);
                        NPC.velocity = Vector2.Zero;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.6f }, target.Center);
                    }
                    NPC.velocity = Vector2.Zero;

                    // 裂痕中心: 各端同帧从同步目标位置计算(误差可容忍, 纯演出)
                    if (PhaseTimer == 40) {
                        crackCenter = target.Center + new Vector2(target.direction * 120f, -420f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.1f }, crackCenter);
                    }
                    if (PhaseTimer == 54 || PhaseTimer == 68)
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f + PhaseTimer * 0.002f, Volume = 1.0f }, crackCenter);

                    if (PhaseTimer >= 95) {
                        // 从裂痕跃出 → 120px/f 直坠砸地(中途加入的客户端 crackCenter 未初始化时就地兜底)
                        if (crackCenter == Vector2.Zero)
                            crackCenter = target.Center + new Vector2(target.direction * 120f, -420f);
                        NPC.Center = crackCenter;
                        NPC.velocity = new Vector2(0f, 120f);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.4f }, NPC.Center);
                        SetCineStage(1);
                    }
                    break;

                case 1: // 坠落 → 砸地(shake 12 + 尘暴)
                    if (TouchingGround(out float groundY) || AttackTimer > 50) {
                        NPC.Center = new Vector2(NPC.Center.X, groundY - NPC.height / 2f + 6f);
                        NPC.velocity = Vector2.Zero;
                        springVel = Vector2.Zero;
                        ACMUtils.AddScreenShake(12f);
                        quakeFlash = 1f;
                        bodyScale = new Vector2(1.16f, 0.82f); // 落地压扁一拍
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.5f }, NPC.Center);
                        if (!Main.dedServ) {
                            for (int i = 0; i < 36; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-160, 160), NPC.height / 2f - 10),
                                    0, 0, DustID.Smoke, Main.rand.NextFloat(-6, 6), -Main.rand.NextFloat(2, 8), 120, default, 2.4f);
                                d.noGravity = Main.rand.NextBool();
                            }
                        }
                        SetCineStage(2);
                    }
                    break;

                case 2: // 静止 60 帧凝视玩家(威慑=静止, PACING §6)
                    NPC.velocity = Vector2.Zero;
                    // 呼吸起伏(静中有生命)
                    bodyScale = new Vector2(1f + MathF.Sin(AttackTimer * 0.1f) * 0.015f, 1f - MathF.Sin(AttackTimer * 0.1f) * 0.02f);
                    suppressAmbientDust = true;

                    if (AttackTimer >= 60) {
                        NPC.dontTakeDamage = false;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                        ResetRotation(1);
                        AdvanceRotation();
                    }
                    break;
            }
        }

        #endregion

        #region 猎手循环 (P1/P2)

        // 潜行：低伏弧线偏向玩家背后, 速度呼吸起伏, 两次假扑心理压迫; ≤550px 栓绳
        private void RunStalk(Player target) {
            int dur = IsPhase2 ? 60 : 78;

            // 弧线速度呼吸起伏(非恒速) —— 「orbiting=脱战」失败模式的解药
            float wobble = 0.6f + 0.55f * MathF.Sin(PhaseTimer * 0.11f + NPC.whoAmI);
            prowlAngle += (IsPhase2 ? 0.034f : 0.026f) * wobble;

            // 弧线缓慢偏向玩家背后(猎手绕背)
            float behindAngle = target.direction == 1 ? MathHelper.Pi : 0f;
            prowlAngle += MathHelper.WrapAngle(behindAngle - prowlAngle) * 0.012f;

            float radius = MathHelper.Clamp(350f + MathF.Sin(GlobalTime * 1.5f) * 55f, 0f, 540f); // 栓绳 ≤550
            Vector2 want = target.Center + new Vector2(MathF.Cos(prowlAngle), MathF.Sin(prowlAngle) * 0.35f) * radius
                + new Vector2(0, -46f); // 低伏: 几乎与玩家同水平线
            NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.05f, 0.07f);

            // 两次假扑(2-3 帧 18px 抖向玩家 + 低吼, 无伤害) —— 心理压迫
            if (PhaseTimer == 20 || PhaseTimer == 44) {
                Vector2 lunge = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity += lunge * 7f;
                bodyScale = new Vector2(1.06f, 0.92f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 0.55f }, NPC.Center);
            }
            if (PhaseTimer == 23 || PhaseTimer == 47)
                NPC.velocity *= 0.55f; // 抖动即收, 不真的冲出去

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 160, default, 1.0f);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.1f;
            }

            if (AdvanceTelegraph(0, dur, 0))
                AdvanceRotation();
        }

        // 扑击：伏低下沉 + 压扁 + pow(t,6) 反向蓄势 late-snap → velocity set 74/88 → ×0.62 硬刹
        private void RunPounce(Player target) {
            int windup = pounceEmpowered ? 60 : 34;
            int strike = 10;
            int recover = 24;

            switch (Telegraph) {
                case TelegraphPhase.Windup: {
                    if (PhaseTimer == 1) {
                        pounceEmpowered = clawCharge >= 3;
                        if (pounceEmpowered) clawCharge = 0;
                        windup = pounceEmpowered ? 60 : 34;
                        pounceAnchor = NPC.Center;
                        NPC.velocity *= 0.5f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = pounceEmpowered ? -0.2f : 0.6f }, NPC.Center);
                        if (OnServer) NPC.netUpdate = true;
                    }

                    Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    if (AttackTimer <= windup - 12)
                        pounceDir = toP; // 末 12 tick 锁定, 预告固定可读

                    // 锚点护栏: 中途加入的客户端没经历 PhaseTimer==1, 就地重锚避免瞬移
                    if (pounceAnchor == Vector2.Zero || Vector2.Distance(pounceAnchor, NPC.Center) > 400f)
                        pounceAnchor = NPC.Center;

                    // 伏低语言: 身体下沉 40px(前 60% 缓入) + 纵向压扁 0.92
                    float t = MathHelper.Clamp(AttackTimer / (float)windup, 0f, 1f);
                    float sink = MathF.Sin(MathF.Min(t * 1.6f, 1f) * MathHelper.PiOver2) * 40f;
                    // late-snap 反向蓄势: pow(t,6) —— 大半程纹丝不动, 最后几帧猛然后吸(锐利的吸气)
                    float back = MathF.Pow(t, 6f) * (pounceEmpowered ? 110f : 90f);
                    Vector2 desired = pounceAnchor + new Vector2(0f, sink) - pounceDir * back;
                    NPC.velocity = (desired - NPC.Center) * 0.45f;
                    bodyScale = new Vector2(MathHelper.Lerp(1f, 1.05f, t), MathHelper.Lerp(1f, 0.92f, MathF.Min(t * 1.6f, 1f)));

                    // 发射前 6 帧: 全身银白闪(MetalSheen uFlash) + 啸声 beep
                    if (AttackTimer == windup - 6) {
                        bodyFlash = 1f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.85f, Volume = 0.6f }, NPC.Center);
                    }
                    if (AttackTimer > windup - 6)
                        bodyFlash = 1f;
                    break;
                }

                case TelegraphPhase.Strike: {
                    if (AttackTimer == 1) {
                        // 爆发: velocity 直接 set(launch is a set, not a ramp)
                        float speed = pounceEmpowered ? 88f : 74f;
                        NPC.velocity = pounceDir * speed;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(pounceEmpowered ? 9f : 6f);
                        if (pounceEmpowered && OnServer) {
                            // 蓄势扑击: 沿扑击路径甩出两道银爪裂脉
                            float ang = pounceDir.ToRotation();
                            BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), NPC.Center, ang + 0.18f, 1100f, 10, 34, NPC.damage / 4);
                            BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), NPC.Center, ang - 0.18f, 1100f, 10, 34, NPC.damage / 4);
                        }
                    }
                    // 爆发 10 帧全速 —— 不衰减(速度即对比); 残影涂抹由绘制层按速度门控
                    bodyScale = new Vector2(1.08f, 0.94f);
                    break;
                }

                default: // Recover: ×0.62/f 硬刹 + 滑地尘土
                    NPC.velocity *= 0.62f;
                    if (!Main.dedServ && NPC.velocity.Length() > 8f && Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-60, 60), NPC.height * 0.4f),
                            0, 0, DustID.Smoke, -NPC.velocity.X * 0.15f, -1.5f, 140, default, 1.6f);
                        d.noGravity = false;
                    }
                    break;
            }

            if (AdvanceTelegraph(windup, strike, recover)) {
                bool goCombo = pounceEmpowered;
                pounceEmpowered = false;
                if (goCombo) {
                    // 蓄势扑击结束 → 追加两段 Z 字三连杀
                    comboIndex = 2;
                    TransitionToState((int)BaihuState.PounceCombo);
                }
                else {
                    AdvanceRotation();
                }
            }
        }

        // 蓄势三连杀：两段 Z 字短冲(每段 8 帧红线预告 + 9 帧 50px/f + 18 帧刹车); 总位移 ≤1400px
        private void RunPounceCombo(Player target) {
            const int Predict = 8, Dash = 9, SegLen = 35; // 8+9+18
            int seg = (int)((PhaseTimer - 1) / SegLen);   // 0 / 1
            int segT = (int)((PhaseTimer - 1) % SegLen) + 1;

            if (seg >= 2 || comboIndex <= 0) {
                comboIndex = 0;
                AdvanceRotation();
                return;
            }

            if (segT == 1) {
                // Z 字锁向: 交替 ±0.5rad 侧偏, 穿玩家身位
                Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                pounceDir = toP.RotatedBy(seg == 0 ? 0.5f : -0.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.9f, Volume = 0.5f }, NPC.Center);
                if (OnServer) NPC.netUpdate = true;
            }

            if (segT <= Predict) {
                // 短预告: 硬刹 + 微伏低
                NPC.velocity *= 0.78f;
                bodyScale = new Vector2(1.04f, 0.93f);
                if (segT == Predict) bodyFlash = 0.8f;
            }
            else if (segT == Predict + 1) {
                NPC.velocity = pounceDir * 50f;
                ACMUtils.AddScreenShake(4f);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f, Volume = 0.9f }, NPC.Center);
            }
            else if (segT > Predict + Dash) {
                NPC.velocity *= 0.62f;
            }

            if (seg == 1 && segT >= SegLen) {
                comboIndex = 0;
                AdvanceRotation();
            }
        }

        // 爪击扇：可读扇形银爪 + Strike 帧三道耙痕闪现 + 本体后坐 14px
        private void RunClawSwipe(Player target) {
            int windup = 32, strike = 8, recover = 18;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    NPC.velocity *= 0.9f;
                    Vector2 toP = (target.Center - NPC.Center);
                    if (toP.Length() > 360f)
                        NPC.velocity = Vector2.Lerp(NPC.velocity, toP.SafeNormalize(Vector2.UnitX) * 16f, 0.1f);
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                        ACMUtils.AddScreenShake(4f);
                        clawCharge++;
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        // 挥爪后坐(重量=反作用, MOTION §4): 本体向后弹 14px
                        NPC.velocity = -dir * 14f;
                        clawFlashAge = 0f;
                        clawFlashDir = dir;
                        if (OnServer) {
                            int count = Expert ? 9 : 7;
                            float spread = MathHelper.ToRadians(70f);
                            for (int layer = 0; layer < 2; layer++) {
                                for (int i = 0; i < count; i++) {
                                    float a = -spread / 2 + spread / (count - 1) * i;
                                    Vector2 vel = dir.RotatedBy(a) * (15f + layer * 5f);
                                    int pr = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                        ModContent.ProjectileType<BaihuMetalShard>(), NPC.damage / 4, 2f, Main.myPlayer);
                                    if (pr >= 0 && pr < Main.maxProjectiles)
                                        Main.projectile[pr].timeLeft = 95;
                                }
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    NPC.velocity *= 0.86f;
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 金属回响：每第三片才是真的, 2/3 为去饱和闪烁虚影(真片带微金光)
        private void RunMetallicEcho(Player target) {
            int windup = 46, strike = 6, recover = 22;
            Vector2 hover = target.Center + new Vector2(0, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            if (Telegraph == TelegraphPhase.Strike && AttackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f }, NPC.Center);
                clawCharge++;
                if (OnServer) {
                    int count = 18;
                    float radius = 620f;
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < count; i++) {
                        float a = baseAngle + MathHelper.TwoPi / count * i;
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 11f;
                        if (i % 3 == 0) {
                            int pr = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<BaihuMetalShard>(), NPC.damage / 4, 0f, Main.myPlayer);
                            if (pr >= 0 && pr < Main.maxProjectiles)
                                Main.projectile[pr].timeLeft = 120;
                        }
                        else {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<BaihuEchoDecoy>(), 0, 0f, Main.myPlayer);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 铁壁：缺口方向=白虎面朝方向(转身→虎啸宣告→音波扇形从口中扩出指向缺口)
        private void RunIronWall(Player target) {
            int windup = 52, strike = 6, recover = 24;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        // 服务器定缺口: 偏离"玩家→圈心"轴一侧(必须移动才能穿), 白虎转身面向该方位宣告
                        if (OnServer) {
                            float baseA = (target.Center - NPC.Center).ToRotation();
                            ironGapAngle = MathHelper.WrapAngle(baseA + (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(1.3f, 2.2f));
                            NPC.netUpdate = true;
                        }
                    }
                    if (PhaseTimer == 14)
                        // 转身完成 → 面向缺口长啸(音画双通道方位提示)
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
                    NPC.velocity *= 0.92f;
                    // 啸向姿态: 胸腔展开
                    bodyScale = new Vector2(0.97f, 1.05f);
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        ACMUtils.AddScreenShake(5f);
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        if (OnServer) {
                            int count = Expert ? 34 : 28;
                            float radius = 560f;
                            float gapHalf = MathHelper.ToRadians(30f);
                            for (int i = 0; i < count; i++) {
                                float a = MathHelper.TwoPi / count * i;
                                float diff = MathF.Abs(MathHelper.WrapAngle(a - ironGapAngle));
                                if (diff < gapHalf) continue; // 虎形缺口
                                Vector2 pos = target.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                                Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 9f;
                                int pr = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                    ModContent.ProjectileType<BaihuMetalShard>(), NPC.damage / 4, 0f, Main.myPlayer);
                                if (pr >= 0 && pr < Main.maxProjectiles)
                                    Main.projectile[pr].timeLeft = 150;
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 音啸破阵 (P2+ 新招)：吸气收束→尾段 8 帧静默→三道旋转缺口音波环(pitch 递升)
        private void RunSonicHowl(Player target) {
            int windup = 40, strike = 60, recover = 22;

            switch (Telegraph) {
                case TelegraphPhase.Windup: {
                    if (PhaseTimer == 1)
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 0.8f }, NPC.Center);

                    if (IsPhase3)
                        GroundedStride(target, target.Center.X >= NPC.Center.X ? -120f : 120f, 6f);
                    else
                        NPC.velocity *= 0.88f;

                    // 吸气: 粒子向口收束; 末 8 帧硬切静默(爆发前吸气, MOTION §6)
                    float t = AttackTimer / (float)windup;
                    bodyScale = new Vector2(MathHelper.Lerp(1f, 0.95f, t), MathHelper.Lerp(1f, 1.07f, t));
                    if (AttackTimer < windup - 8) {
                        if (!Main.dedServ) {
                            Vector2 mouth = MouthPos();
                            for (int i = 0; i < 2; i++) {
                                Vector2 dp = mouth + Main.rand.NextVector2Circular(170, 130);
                                Dust d = Dust.NewDustPerfect(dp, DustID.Silver, (mouth - dp) * 0.09f, 130, default, 1.3f);
                                d.noGravity = true;
                            }
                        }
                    }
                    else {
                        suppressAmbientDust = true; // 静默 = 爆发前的负空间
                    }
                    break;
                }

                case TelegraphPhase.Strike: {
                    // 三道音波环: 间隔 20 帧; 每环发出瞬间 pitch 递升 beep
                    if (AttackTimer == 1 || AttackTimer == 21 || AttackTimer == 41) {
                        int ringIdx = (int)AttackTimer / 20;
                        float pitch = -0.1f + ringIdx * 0.28f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = pitch, Volume = 1.25f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = pitch + 0.4f, Volume = 0.8f }, NPC.Center);
                        ACMUtils.AddScreenShake(5f + ringIdx);
                        bodyScale = new Vector2(1.1f, 0.95f); // 啸出瞬间胸腔泄压
                        if (OnServer) {
                            float gap = (target.Center - NPC.Center).ToRotation(); // 缺口初始朝向玩家(公平), 随后旋转
                            BaihuSonicRoar.Spawn(NPC.GetSource_FromAI(), NPC.Center, 720f, NPC.damage / 4, gap);
                        }
                    }
                    if (IsPhase3)
                        GroundedStride(target, target.Center.X >= NPC.Center.X ? -120f : 120f, 3f);
                    else
                        NPC.velocity *= 0.9f;
                    break;
                }

                default:
                    NPC.velocity *= 0.9f;
                    break;
            }

            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        /// <summary>口部世界坐标(音啸收束/铁壁扇形的锚点)。</summary>
        private Vector2 MouthPos() => NPC.Center + new Vector2(NPC.spriteDirection * NPC.width * 0.32f, -12f);

        #endregion

        #region 阶段转换

        // P2 (~90f): 仰天长啸 → 三环音波外扩清弹 → 金属尘沙向鬃毛汇聚(MetalSheen 常驻淡入)
        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            // 仰啸姿态
            float t = MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f);
            bodyScale = new Vector2(MathHelper.Lerp(1f, 0.95f, t), MathHelper.Lerp(1f, 1.08f, t));

            // 金属尘沙向鬃毛(上身)汇聚
            if (!Main.dedServ && PhaseTimer < 84) {
                Vector2 mane = NPC.Center + new Vector2(NPC.spriteDirection * 30f, -NPC.height * 0.3f);
                for (int i = 0; i < 6; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = MathF.Max(30f, 300f - PhaseTimer * 2.5f);
                    Vector2 dp = mane + a.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dp, 0, 0, Main.rand.NextBool(3) ? DustID.GoldCoin : DustID.Silver, 0, 0, 50, default, 2.2f);
                    d.noGravity = true;
                    d.velocity = (mane - dp).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if (PhaseTimer == 50) {
                // 长啸: 清弹 + 三道纯视觉音环(间隔 14f) + 光泽上身
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.6f, Pitch = 0.1f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                fxRingCenter = NPC.Center;
                fxRingAge[0] = 0f;
                bodyFlash = 1f;
                if (OnServer) ClearHostileProjectiles();
            }
            if (PhaseTimer == 64) fxRingAge[1] = 0f;
            if (PhaseTimer == 78) fxRingAge[2] = 0f;

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                ResetRotation(2);
                AdvanceRotation();
            }
        }

        // P3「白虎落地」(~120f): 跃至高空 → 0.5s 悬滞静默 → 流星坠地(白闪+shake 14+全场裂地纹) → 40 帧低吼蓄势
        private void RunPhaseTransition3(Player target) {
            NPC.dontTakeDamage = true;

            switch (CineStage) {
                case 0: // 跃空 (30f)
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.6f }, NPC.Center);
                        NPC.velocity = new Vector2(0f, -20f);
                    }
                    NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, -14f, 0.1f);
                    NPC.velocity.X *= 0.9f;
                    if (!Main.dedServ) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.Silver, 0, 2f, 100, default, 1.6f);
                        d.noGravity = true;
                    }
                    if (AttackTimer >= 30)
                        SetCineStage(1);
                    break;

                case 1: // 悬滞 30f (0.5s): 粒子骤停静默 —— 爆发前的负空间
                    NPC.velocity *= 0.72f;
                    suppressAmbientDust = true;
                    if (AttackTimer >= 30) {
                        NPC.velocity = new Vector2(0f, 46f); // 流星: velocity set
                        whiteFlash = 0.7f;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                        SetCineStage(2);
                    }
                    break;

                case 2: // 流星坠地
                    if (TouchingGround(out float groundY) || AttackTimer > 45) {
                        NPC.Center = new Vector2(NPC.Center.X, groundY - NPC.height / 2f + 6f);
                        NPC.velocity = Vector2.Zero;
                        springVel = Vector2.Zero;
                        ACMUtils.AddScreenShake(14f);
                        whiteFlash = 1f;
                        s_skyFlash = 1f;
                        quakeFlash = 1f;
                        runicFlash = 1f;
                        runicCenter = NPC.Center;
                        bodyScale = new Vector2(1.18f, 0.8f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.6f }, NPC.Center);
                        if (OnServer) ClearHostileProjectiles();
                        if (!Main.dedServ) {
                            for (int i = 0; i < 44; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-260, 260), NPC.height / 2f - 8),
                                    0, 0, Main.rand.NextBool(3) ? DustID.GoldCoin : DustID.Smoke,
                                    Main.rand.NextFloat(-8, 8), -Main.rand.NextFloat(3, 10), 110, default, 2.4f);
                                d.noGravity = Main.rand.NextBool();
                            }
                        }
                        SetCineStage(3);
                    }
                    break;

                case 3: // 落地低吼蓄势 40f → 入落地形态
                    NPC.velocity = Vector2.Zero;
                    bodyScale = new Vector2(1.05f, 0.9f); // 低伏蓄势
                    if (AttackTimer == 6)
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.75f, Volume = 1.1f }, NPC.Center);
                    if (!Main.dedServ && AttackTimer % 4 == 0) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(100, 60), 0, 0, DustID.Silver, 0, -2f, 80, default, 1.8f);
                        d.noGravity = true;
                    }
                    if (AttackTimer >= 40) {
                        NPC.dontTakeDamage = false;
                        NPC.defense += 15;
                        NPC.damage = (int)(NPC.damage * 1.3f);
                        glowIntensity = 2f;
                        ResetRotation(3);
                        AdvanceRotation();
                    }
                    break;
            }
        }

        #endregion

        #region 白虎之形 (P3 真落地)

        // 裂地灭世爪 RiftClaw — 跃高空, 平行爪痕预告 → 流星落地银脉爆裂; 站在爪痕之间的缝
        private void RunRiftClaw(Player target) {
            int windup = 76, strike = 30, recover = 40;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        slamLanded = false;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.4f }, NPC.Center);
                        if (OnServer) {
                            // 平行竖向爪痕: 在玩家两侧布数道竖脉, 留出可站的缝; 预告时长≈windup
                            float[] off = Expert
                                ? new[] { -640f, -440f, -240f, 240f, 440f, 640f }
                                : new[] { -540f, -300f, 300f, 540f };
                            foreach (float x in off) {
                                Vector2 top = new(target.Center.X + x, target.Center.Y - 760);
                                BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), top, MathHelper.PiOver2, 1500f, windup, strike, NPC.damage / 3);
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    // 跃至高空(悬浮蓄势)
                    {
                        Vector2 want = target.Center + new Vector2(0, -440);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.06f, 0.08f);
                    }
                    // 渐强震屏(处决级预告)
                    ACMUtils.AddScreenShake(2f + 6f * (AttackTimer / (float)windup));
                    break;

                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        NPC.velocity = new Vector2(0, 40f); // 流星落地: velocity set
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f }, NPC.Center);
                    }
                    if (!slamLanded && (TouchingGround(out float gY) || AttackTimer > 22)) {
                        // 落地帧冲击链: 顿帧(velocity 归零 1 帧) → 次帧反弹
                        slamLanded = true;
                        slamLandTick = (int)AttackTimer;
                        NPC.Center = new Vector2(NPC.Center.X, gY - NPC.height / 2f + 6f);
                        NPC.velocity = Vector2.Zero;
                        springVel = Vector2.Zero;
                        ACMUtils.AddScreenShake(12f);
                        quakeFlash = 1f;
                        bodyScale = new Vector2(1.18f, 0.8f);
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.4f }, NPC.Center);
                        if (!Main.dedServ) {
                            for (int i = 0; i < 24; i++) {
                                Dust d = Dust.NewDustDirect(target.Center + new Vector2(Main.rand.NextFloat(-700, 700), 40), 0, 0, DustID.Smoke, Main.rand.NextFloat(-4, 4), -Main.rand.NextFloat(2, 7), 120, default, 2f);
                                d.noGravity = true;
                            }
                        }
                    }
                    else if (slamLanded && AttackTimer == slamLandTick + 1) {
                        NPC.velocity = new Vector2(0, -6f); // 反弹
                    }
                    else if (slamLanded) {
                        NPC.velocity.Y *= 0.82f;
                    }
                    break;

                default:
                    GroundedStride(target, target.Center.X >= NPC.Center.X ? -300f : 300f, 5f);
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 震地踏 QuakeStomp — 伏低下蹲 → 跃起 → 悬滞 6 帧(粒子骤停) → velocity set 40 砸地 → 双震波环 + 碎片扇
        private void RunQuakeStomp(Player target) {
            int windup = 40, strike = 64, recover = 26;
            switch (Telegraph) {
                case TelegraphPhase.Windup: {
                    if (PhaseTimer == 1) {
                        slamLanded = false;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);
                    }
                    // 伏低下蹲: 贴地 + 下沉 20px + 压扁
                    GroundedStride(target, target.Center.X >= NPC.Center.X ? -220f : 220f, 8f, crouch: 20f);
                    stompGroundY = FindGroundY(NPC.Center.X, NPC.Center.Y - 40f);
                    float t = AttackTimer / (float)windup;
                    bodyScale = new Vector2(MathHelper.Lerp(1f, 1.08f, t), MathHelper.Lerp(1f, 0.86f, t));
                    break;
                }

                case TelegraphPhase.Strike: {
                    if (AttackTimer == 1) {
                        // 跃起 ~200px
                        NPC.velocity = new Vector2(MathF.Sign(target.Center.X - NPC.Center.X) * 3f, -26f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                        bodyScale = new Vector2(0.92f, 1.12f); // 起跳拉伸
                    }
                    else if (AttackTimer <= 12 && !slamLanded) {
                        NPC.velocity.Y += 1.9f; // 上升减速到近停
                    }
                    else if (AttackTimer <= 18 && !slamLanded) {
                        // 悬滞 6 帧: 速度归零 + 粒子骤停(暴风雨前的死寂)
                        NPC.velocity *= 0.4f;
                        suppressAmbientDust = true;
                        bodyScale = new Vector2(0.94f, 1.1f);
                    }
                    else if (AttackTimer == 19 && !slamLanded) {
                        NPC.velocity = new Vector2(0f, 40f); // 砸地: velocity set
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 1.1f }, NPC.Center);
                    }

                    if (!slamLanded && AttackTimer > 19 && (TouchingGround(out float gY) || AttackTimer > 44)) {
                        // 砸地冲击链: 顿帧 → 双震波环(间隔 14f) + 落点碎片扇
                        slamLanded = true;
                        slamLandTick = (int)AttackTimer;
                        NPC.Center = new Vector2(NPC.Center.X, gY - NPC.height / 2f + 6f);
                        NPC.velocity = Vector2.Zero;
                        springVel = Vector2.Zero;
                        ACMUtils.AddScreenShake(10f);
                        quakeFlash = 1f;
                        bodyScale = new Vector2(1.2f, 0.78f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, NPC.Center);
                        if (OnServer) {
                            SpawnShockRing(NPC.Center, 460f, NPC.damage / 4);
                            // 落点碎片扇: 向上扇形银片
                            int shards = Expert ? 7 : 5;
                            for (int i = 0; i < shards; i++) {
                                float a = -MathHelper.PiOver2 + MathHelper.ToRadians(-52f + 104f / (shards - 1) * i);
                                Vector2 vel = a.ToRotationVector2() * Main.rand.NextFloat(11f, 15f);
                                int pr = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, -20), vel,
                                    ModContent.ProjectileType<BaihuMetalShard>(), NPC.damage / 4, 1f, Main.myPlayer);
                                if (pr >= 0 && pr < Main.maxProjectiles)
                                    Main.projectile[pr].timeLeft = 90;
                            }
                            NPC.netUpdate = true;
                        }
                        if (!Main.dedServ) {
                            for (int i = 0; i < 26; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-140, 140), NPC.height / 2f - 8),
                                    0, 0, DustID.Smoke, Main.rand.NextFloat(-6, 6), -Main.rand.NextFloat(2, 8), 130, default, 2.2f);
                                d.noGravity = Main.rand.NextBool();
                            }
                        }
                    }
                    else if (slamLanded) {
                        if (AttackTimer == slamLandTick + 1)
                            NPC.velocity = new Vector2(0, -5f); // 顿帧后反弹
                        else
                            NPC.velocity.Y *= 0.8f;
                        // 第二震波环: 延迟 14 帧
                        if (AttackTimer == slamLandTick + 14 && OnServer)
                            SpawnShockRing(NPC.Center, 620f, NPC.damage / 4);
                        // 早退: 双环放完即收招(不等满时长, PACING §3 无死等待)
                        if (AttackTimer > slamLandTick + 18 && AttackTimer < strike)
                            AttackTimer = strike;
                    }
                    break;
                }

                default:
                    GroundedStride(target, target.Center.X >= NPC.Center.X ? -260f : 260f, 4f);
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        private void SpawnShockRing(Vector2 center, float maxRadius, int damage) {
            BaihuSonicRoar.Spawn(NPC.GetSource_FromAI(), center, maxRadius, damage); // 无缺口震波
        }

        // 爪裂射线 RendBeams — 落地步进中甩出方向可读的耙痕射线(错帧点火), 释放帧后坐
        private void RunRendBeams(Player target) {
            int windup = 50, strike = 16, recover = 26;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);
                        if (OnServer) {
                            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                            float baseAng = dir.ToRotation();
                            int beams = Expert ? 5 : 3;
                            for (int i = 0; i < beams; i++) {
                                float a = baseAng + MathHelper.ToRadians(-24f + 48f / (beams - 1) * i);
                                BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), NPC.Center, a, 1700f, windup - 6 + i * 4, strike, NPC.damage / 3);
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    GroundedStride(target, target.Center.X >= NPC.Center.X ? -380f : 380f, 7f);
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        ACMUtils.AddScreenShake(8f);
                        SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
                        // 释放后坐 14px: 本体向后滑(发射的反作用, MOTION §4)
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.velocity = -dir * 14f;
                        clawFlashAge = 0f;
                        clawFlashDir = dir;
                    }
                    NPC.velocity *= 0.85f;
                    break;
                default:
                    GroundedStride(target, target.Center.X >= NPC.Center.X ? -340f : 340f, 5f);
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        #endregion

        #region 死亡演出「金断啸终」(~185f)

        private void RunDeath() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            contactDamageActive = false;
            NPC.rotation *= 0.92f;

            Player target = NPC.target >= 0 && NPC.target < Main.maxPlayers ? Main.player[NPC.target] : null;
            Vector2 toPlayer = target != null && target.active
                ? (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX)
                : new Vector2(NPC.spriteDirection, 0f);

            switch (CineStage) {
                case 0: // 踉跄两步 (36f): 速度扰动 + 银火花
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.NPCDeath62 with { Pitch = -0.3f, Volume = 1.4f }, NPC.Center);
                        NPC.velocity *= 0.3f;
                    }
                    // 两次踉跄(重心失衡的偏摆)
                    if (AttackTimer == 8 || AttackTimer == 26) {
                        NPC.velocity = new Vector2((AttackTimer == 8 ? -1 : 1) * NPC.spriteDirection * 7f, 2f);
                        ACMUtils.AddScreenShake(3f);
                        SoundEngine.PlaySound(SoundID.NPCHit42 with { Pitch = -0.5f, Volume = 0.7f }, NPC.Center);
                        bodyScale = new Vector2(1.08f, 0.9f);
                    }
                    NPC.velocity *= 0.88f;
                    if (!Main.dedServ && Main.rand.NextBool(2)) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 60), 0, 0, DustID.Silver,
                            Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 1), 60, default, 1.6f);
                        d.noGravity = true;
                    }
                    if (AttackTimer >= 36)
                        SetCineStage(1);
                    break;

                case 1: // 向玩家方向奋力一跃 → 中途力竭坠地滑行 (≤44f, 无伤害)
                    if (AttackTimer == 1) {
                        NPC.velocity = toPlayer * 20f + new Vector2(0f, -13f);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.15f, Volume = 1.1f }, NPC.Center);
                        bodyScale = new Vector2(0.94f, 1.1f);
                    }
                    else {
                        // 力竭: 推力衰减 + 重力接管
                        NPC.velocity.X *= 0.94f;
                        NPC.velocity.Y += 1.5f;
                        if (NPC.velocity.Y > 0f && TouchingGround(out float gY)) {
                            // 坠地滑行
                            NPC.Center = new Vector2(NPC.Center.X, gY - NPC.height / 2f + 8f);
                            NPC.velocity.Y = 0f;
                            NPC.velocity.X *= 0.9f;
                            bodyScale = new Vector2(1.1f, 0.86f);
                            if (!Main.dedServ && MathF.Abs(NPC.velocity.X) > 2f && Main.rand.NextBool(2)) {
                                Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(0, NPC.height * 0.4f), 0, 0, DustID.Smoke,
                                    -NPC.velocity.X * 0.2f, -1f, 150, default, 1.7f);
                                d.noGravity = false;
                            }
                            if (MathF.Abs(NPC.velocity.X) < 1.5f && AttackTimer > 16) {
                                ACMUtils.AddScreenShake(4f);
                                SetCineStage(2);
                            }
                        }
                    }
                    if (AttackTimer >= 44 && CineStage == 1)
                        SetCineStage(2);
                    break;

                case 2: // 昂首最后一啸 (48f): 三重纯视觉音环 + 白闪
                    NPC.velocity = Vector2.Zero;
                    // 昂首(胸腔缓缓撑起)
                    {
                        float t = MathHelper.Clamp(AttackTimer / 14f, 0f, 1f);
                        bodyScale = new Vector2(MathHelper.Lerp(1.05f, 0.94f, t), MathHelper.Lerp(0.9f, 1.1f, t));
                    }
                    if (AttackTimer == 14) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 2f }, NPC.Center);
                        ACMUtils.AddScreenShake(16f);
                        whiteFlash = 1f;
                        s_skyFlash = 1f;
                        bodyFlash = 1f;
                        fxRingCenter = MouthPos();
                        fxRingAge[0] = 0f;
                    }
                    if (AttackTimer == 28) fxRingAge[1] = 0f;
                    if (AttackTimer == 42) fxRingAge[2] = 0f;
                    if (AttackTimer >= 48)
                        SetCineStage(3);
                    break;

                case 3: // DissolveBurn 溶解成银色碎片流向西方 → 真实死亡
                    NPC.velocity = Vector2.Zero;
                    dissolveProgress = MathHelper.Clamp(AttackTimer / 54f, 0f, 1f);
                    glowIntensity = MathHelper.Lerp(glowIntensity, 0.4f, 0.05f);

                    // 银色碎片流向西方(西方金位归位)
                    if (!Main.dedServ && dissolveProgress < 0.95f && AttackTimer % 2 == 0) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.45f, NPC.height * 0.45f);
                        Dust d = Dust.NewDustPerfect(dp, DustID.Silver,
                            new Vector2(-Main.rand.NextFloat(2.5f, 6.5f), -Main.rand.NextFloat(0.2f, 1.4f)), 80, default, 1.9f);
                        d.noGravity = true;
                    }

                    if (AttackTimer >= 64 && OnServer) {
                        // 真实死亡: 掉落/downedBaihu 照常(CheckDead 二次放行)
                        NPC.dontTakeDamage = false;
                        NPC.StrikeInstantKill();
                    }
                    break;
            }
        }

        #endregion

        #region 绘制 (含预警)

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            DrawCinematics(spriteBatch);

            // 入场阶段 0: 本体尚在"裂痕之后", 只画天上的爪痕撕裂
            if (State == BaihuState.Intro && CineStage == 0) {
                DrawIntroCracks();
                return false;
            }

            DrawTelegraphs(spriteBatch);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingRight = NPC.spriteDirection == 1;
            SpriteEffects effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRotation = facingRight ? NPC.rotation : -NPC.rotation;
            Vector2 scale = new Vector2(NPC.scale) * bodyScale;

            float speed = NPC.velocity.Length();

            // —— 速度门控拉伸残影涂抹: 仅冲刺(>30px/f)时出现, 沿速度方向 4 份、scale.X 1.4→1.0、透明度递减 ——
            if (speed > 30f) {
                Vector2 velNorm = NPC.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 4; i >= 1; i--) {
                    float f = i / 4f;
                    Vector2 smearPos = NPC.Center - velNorm * (i * 30f) - screenPos;
                    Vector2 smearScale = scale * new Vector2(1.4f - 0.4f * (1f - f) - 0.25f * f, 0.94f);
                    Color c = new Color(205, 218, 240) * (0.34f * (1f - f * 0.72f));
                    spriteBatch.Draw(texture, smearPos, frame, c, drawRotation, origin, smearScale, effects, 0f);
                }
            }
            else if (speed > 9f) {
                // 常速残影(旧拖尾, 弱化)
                for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i -= 2) {
                    Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    float alpha = 0.22f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                    spriteBatch.Draw(texture, trailPos, frame, drawColor * alpha, drawRotation, origin, scale, effects, 0f);
                }
            }

            Vector2 drawPos = NPC.Center - screenPos;

            // —— 本体: 死亡溶解 > 金属光泽重绘 > 平绘 三选一 ——
            if (State == BaihuState.Death && CineStage >= 3) {
                WeaponVFX.ApplyDissolveBurn(texture, NPC.Center, frame, drawColor, drawRotation, origin,
                    NPC.scale, dissolveProgress, 1f, new Color(230, 240, 255, 220),
                    edgeWidth: 0.1f, noiseScale: 2.6f, direction: new Vector2(-1f, 0f), sweepStrength: 0.7f, effects: effects);
            }
            else if (sheenIntensity > 0.02f || bodyFlash > 0.02f) {
                float sheenPos = (GlobalTime * 0.45f) % 1.6f - 0.3f; // 高光带周期扫过
                BaihuVFX.DrawMetalSheenBody(texture, NPC.Center, frame, drawColor, drawRotation, origin,
                    scale, effects, sheenIntensity, sheenPos, bodyFlash);
            }
            else {
                spriteBatch.Draw(texture, drawPos, frame, drawColor, drawRotation, origin, scale, effects, 0f);
            }

            return false;
        }

        /// <summary>入场空中三道爪痕撕裂(错帧亮起, ClawRend 银白释放模式)。</summary>
        private void DrawIntroCracks() {
            if (PhaseTimer < 40 || crackCenter == Vector2.Zero)
                return;
            // 三道错帧: 40/54/68 亮起
            float[] starts = { 40f, 54f, 68f };
            Vector2[] offsets = { new(-90f, -52f), new(0f, 0f), new(90f, 52f) };
            for (int i = 0; i < 3; i++) {
                float age = (float)PhaseTimer - starts[i];
                if (age < 0f) continue;
                float grow = MathHelper.Clamp(age / 12f, 0f, 1f);
                float fade = 1f - MathHelper.Clamp((age - 34f) / 26f, 0f, 0.55f);
                Vector2 mid = crackCenter + offsets[i];
                Vector2 dir = new Vector2(MathF.Cos(2.28f), MathF.Sin(2.28f)); // 斜向下的撕裂角
                BaihuVFX.DrawClawRend(mid - dir * 230f, mid + dir * 230f, 26f,
                    0.9f * fade, grow, release: true);
            }
            // 裂痕中心透光
            float glow = MathHelper.Clamp(((float)PhaseTimer - 40f) / 40f, 0f, 1f) * 0.5f;
            if (glow > 0.02f)
                ACMShaders.DrawRadialBloomAt(crackCenter, 0.14f, glow, TelegraphColors.WhiteTiger, rayCount: 8f);
        }

        /// <summary>演出层通用视觉：白闪、纯视觉音环、P3 全场裂地纹、爪击耙痕闪现。</summary>
        private void DrawCinematics(SpriteBatch sb) {
            // 演出音环(相变清弹 / 死亡终啸)
            for (int i = 0; i < fxRingAge.Length; i++) {
                if (fxRingAge[i] < 0f) continue;
                float age = fxRingAge[i];
                float radius = 40f + age * 10f;
                float alpha = (1f - age / 75f) * 0.85f;
                BaihuVFX.DrawSonicRing(fxRingCenter, radius, 22f, alpha, -999f, 0f);
            }

            // 爪击扇释放帧: 三道耙痕闪现(短促, ~12 帧)
            if (clawFlashAge < 12f) {
                float t = clawFlashAge / 12f;
                float baseAng = clawFlashDir.ToRotation();
                for (int i = -1; i <= 1; i++) {
                    float a = baseAng + i * 0.24f;
                    Vector2 start = NPC.Center + a.ToRotationVector2() * 40f;
                    Vector2 end = NPC.Center + a.ToRotationVector2() * (300f + 60f * t);
                    BaihuVFX.DrawClawRend(start, end, 18f, (1f - t) * 0.95f, 1f, release: true);
                }
            }

            // P3 落地全场裂地纹(ArenaRunic, 白银主题, 随 runicFlash 淡出)
            if (runicFlash > 0.02f) {
                Effect fx = ACMShaders.ArenaRunic;
                if (fx != null) {
                    ACMShaders.WorldDecalParams(runicCenter, 860f, out Vector2 uv, out float radFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(runicFlash, 0f, 1f) * 0.8f);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uRadius"]?.SetValue(radFrac);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uShape"]?.SetValue(0f);
                    fx.Parameters["uMode"]?.SetValue(0f);
                    fx.Parameters["uRuneFreq"]?.SetValue(11f);
                    fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.WhiteTiger.ToVector4());
                    fx.Parameters["uColorSecondary"]?.SetValue(new Color(255, 216, 140).ToVector4());
                    ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
                }
            }

            // 白闪(流星坠地/终啸): 径向泛光, 内部自动占全屏名额
            if (whiteFlash > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.4f, whiteFlash, Color.White, rayCount: 0f, falloff: 1.8f);

            // 落地震波泛光(裂地灭世爪/震地踏/相变)
            if (quakeFlash > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, quakeFlash, TelegraphColors.WhiteTiger, rayCount: 14f);
        }

        // 攻击预警(银白主题/红致命), 服务端零绘制; 红只给致命扑击线/耙痕预告
        private void DrawTelegraphs(SpriteBatch sb) {
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers)
                return;
            Player target = Main.player[NPC.target];
            if (target == null || !target.active)
                return;

            switch (State) {
                case BaihuState.Pounce when InWindup: {
                    int windup = pounceEmpowered ? 60 : 34;
                    float prog = MathHelper.Clamp(AttackTimer / (float)windup, 0f, 1f);
                    Vector2 end = NPC.Center + pounceDir * 1000f;
                    ACMShaders.DrawBeam(NPC.Center, end, (pounceEmpowered ? 8f : 5f) * (0.4f + 0.6f * prog),
                        TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, 0.25f + 0.55f * prog,
                        flowSpeed: 1f, flowScale: 3f, coreSharp: 3f);
                    if (pounceEmpowered)
                        ElementBloom(sb, 0.4f + 0.4f * prog, 150f); // 银爪预兆辉
                    break;
                }
                case BaihuState.PounceCombo: {
                    // Z 字段内 8 帧短预告红线
                    const int SegLen = 35, Predict = 8;
                    int segT = (int)((PhaseTimer - 1) % SegLen) + 1;
                    if (segT <= Predict) {
                        float prog = segT / (float)Predict;
                        Vector2 end = NPC.Center + pounceDir * 760f;
                        ACMShaders.DrawBeam(NPC.Center, end, 5f * (0.5f + 0.5f * prog),
                            TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, 0.3f + 0.5f * prog,
                            flowSpeed: 1.4f, flowScale: 3f, coreSharp: 3f);
                    }
                    break;
                }
                case BaihuState.ClawSwipe when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / 32f, 0f, 1f);
                    Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    float baseAng = dir.ToRotation();
                    float spread = MathHelper.ToRadians(70f);
                    for (int i = -1; i <= 1; i++) {
                        float a = baseAng + spread * 0.5f * i;
                        Vector2 end = NPC.Center + a.ToRotationVector2() * 420f;
                        ACMShaders.DrawBeam(NPC.Center, end, 3f, TelegraphColors.WhiteTiger,
                            TelegraphColors.WhiteTiger * 0.3f, 0.3f * prog, flowSpeed: 1.5f, flowScale: 2.5f);
                    }
                    break;
                }
                case BaihuState.MetallicEcho when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / 46f, 0f, 1f);
                    ElementTelegraphCircle(sb, target.Center, 620f * prog, 0.5f * prog, false);
                    break;
                }
                case BaihuState.IronWall when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / 52f, 0f, 1f);
                    ElementTelegraphCircle(sb, target.Center, 560f, 0.4f * prog, false);
                    // 安全缺口: 翠玉射线指明可逃方向
                    Vector2 gapEnd = target.Center + ironGapAngle.ToRotationVector2() * 560f;
                    ACMShaders.DrawBeam(target.Center, gapEnd, 10f, TelegraphColors.Safe,
                        TelegraphColors.Safe * 0.4f, 0.5f * prog, flowSpeed: 0.6f, flowScale: 2f);
                    // 音波扇形从口中扩出指向缺口(啸向宣告): 局部弧 = 全环 + 大缺口反选
                    if (AttackTimer > 14) {
                        for (int i = 0; i < 3; i++) {
                            float r = 46f + ((AttackTimer - 14) * 5.5f + i * 62f) % 190f;
                            float a = (1f - r / 190f) * 0.55f * prog;
                            BaihuVFX.DrawSonicRing(MouthPos(), r, 9f, a,
                                MathHelper.WrapAngle(ironGapAngle + MathHelper.Pi), MathHelper.Pi - 0.5f);
                        }
                    }
                    break;
                }
                case BaihuState.SonicHowl when InWindup: {
                    // 吸气泛光(末 8 帧静默时反而收小 —— 爆发前的坍缩)
                    float prog = MathHelper.Clamp(AttackTimer / 40f, 0f, 1f);
                    float collapse = AttackTimer > 32 ? 1f - (AttackTimer - 32) / 8f * 0.55f : 1f;
                    ElementBloom(sb, 0.45f * prog * collapse, 120f * collapse);
                    break;
                }
                case BaihuState.QuakeStomp when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / 40f, 0f, 1f);
                    DrawGroundDecal(sb, new Vector2(NPC.Center.X, stompGroundY), 480f, 0.55f * prog);
                    break;
                }
            }
        }

        // 地纹(ArenaRunic) 落点圈 —— 缺着色器自动跳过(只是少一层装饰)
        private void DrawGroundDecal(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity) {
            if (intensity <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;
            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uShape"]?.SetValue(0f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.WhiteTiger.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.Lethal.ToVector4());
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        #endregion
    }
}

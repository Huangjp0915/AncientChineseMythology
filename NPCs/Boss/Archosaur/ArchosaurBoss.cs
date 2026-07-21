using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂蠕虫基类 (V3) — 段节继承宿主头的无敌/接触伤害门控/演出层,
    /// 并承载鞭波次级运动与死亡波/挂弧/溶解等段节绘制。
    /// </summary>
    public abstract class ArchosaurBoss : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/" + Name;

        public override bool IsUseSpriteDirection => true;

        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>段节接触伤害门控倍率 (头部类覆写发布; 段节每帧继承)。</summary>
        public virtual float SegmentContactMult => 1f;

        /// <summary>难度缩放后的接触伤害基准 (首帧从 NPC.damage 捕获, 之后每帧以门控倍率覆写)。</summary>
        protected int scaledContact = -1;

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.height = 80;
            NPC.lifeMax = 500000;
            NPC.damage = 200;   // V3: 基准接触伤害, 按状态门控 (旧版 1000 常开剐蹭即秒杀)
            NPC.defense = 300;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0;
            SummonMax = 80;
        }

        protected ArchosaurHead HostHead {
            get {
                if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                    NPC h = Main.npc[NPC.realLife];
                    if (h.active && h.ModNPC is ArchosaurHead head)
                        return head;
                }
                return null;
            }
        }

        protected CloneBossHead HostGhost {
            get {
                if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                    NPC h = Main.npc[NPC.realLife];
                    if (h.active && h.ModNPC is CloneBossHead ghost)
                        return ghost;
                }
                return null;
            }
        }

        public override void AI() {
            base.AI();
            if (scaledContact < 0)
                scaledContact = Math.Max(1, NPC.damage);

            // 段节: 继承宿主头的无敌帧与接触伤害门控 (相变 i-frame 由头部驱动)
            if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                NPC h = Main.npc[NPC.realLife];
                if (h.active && h.ModNPC is ArchosaurBoss hostBoss) {
                    NPC.dontTakeDamage = h.dontTakeDamage;
                    NPC.damage = (int)(scaledContact * hostBoss.SegmentContactMult);
                }
            }

            // 段节环境反馈 (纯客户端): 死亡波爆花 / 溶解金屑
            ArchosaurHead head = HostHead;
            if (!Main.dedServ && head != null) {
                float flash = head.SegmentDeathFlash(SummonCount);
                if (flash > 0.85f && Main.rand.NextBool(2)) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(10f, 10f),
                            DustID.Electric, Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.4f);
                        d.noGravity = true;
                    }
                }
                if (head.DissolveVisual > 0.05f && Main.rand.NextBool(7)) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-2.6f, -1.1f)), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 鞭波次级运动: 头部冲量 (WhipWave) 沿链传播的横向行波, 叠加在链约束解之上。
        /// 逐段小偏移会天然向后级放大, 形成有机甩尾; 振幅 ≤12px 不破坏碰撞可读性。
        /// </summary>
        public override void ChangePos() {
            base.ChangePos();
            ArchosaurHead head = HostHead;
            if (head != null && head.WhipWave > 0.02f && FatherNPC != null) {
                Vector2 toFather = FatherNPC.Center - NPC.Center;
                Vector2 norm = new Vector2(-toFather.Y, toFather.X).SafeNormalize(Vector2.UnitY);
                float phase = SummonCount * 0.5f - head.WhipClock * 0.32f;
                float amp = head.WhipWave * 11f * Math.Min(SummonCount * 0.12f, 1.3f);
                NPC.Center += norm * (MathF.Sin(phase) * amp);
            }
        }

        /// <summary>
        /// V2 破绽窗口机制 (保留): 宿主本体在幻影存活时受伤减半, 幻影被破后的逆雷/破绽窗口期受伤加成。
        /// 幻影蠕虫的 realLife 非 ArchosaurHead → 不减伤, 可被快速击破。
        /// </summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            NPC host = NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs ? Main.npc[NPC.realLife] : NPC;
            if (host.active && host.ModNPC is ArchosaurHead head)
                modifiers.FinalDamage *= head.DamageTakenMult;
        }

        /// <summary>统一段节贴图绘制 (自定义 origin 逻辑), 供基类与头部类复用。</summary>
        protected void DrawSegmentSprite(SpriteBatch spriteBatch, Vector2 screenPos, Color color) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new(NPC.spriteDirection == -1 ? 0 : tex.Width, 20);
            if (NPCWormType == WormType.Head) {
                origin.Y += 34;
                origin.X = NPC.spriteDirection == -1 ? (tex.Width / 4) : (tex.Width / 4 * 3);
            }
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, color, NPC.rotation, origin, NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            ArchosaurHead head = HostHead;
            CloneBossHead ghost = HostGhost;
            Color col = drawColor;
            float alpha = 1f;

            if (ghost != null) {
                // 幻影段节: 灰蓝半透 + 确定性静电闪烁 + 出生/吸收溶解
                float flicker = 0.72f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + SummonCount * 0.9f);
                col = Color.Lerp(drawColor, ArchosaurVFX.PhantomBlue, 0.55f);
                alpha = flicker * MathHelper.Clamp(1f - ghost.DissolveVisual, 0f, 1f) * 0.92f;
                if (alpha <= 0.03f)
                    return false;
            }
            else if (head != null) {
                float flash = head.SegmentDeathFlash(SummonCount);
                if (flash > 0f)
                    col = Color.Lerp(col, Color.White, Math.Min(flash, 1f));
                if (head.WindowVisual > 0.05f)
                    col = Color.Lerp(col, ArchosaurVFX.GoldSoul, head.WindowVisual * 0.35f);
                if (head.DissolveVisual > 0f) {
                    col = Color.Lerp(col, Color.White, head.DissolveVisual * 0.7f);
                    alpha = MathHelper.Clamp(1f - head.DissolveVisual, 0f, 1f);
                    if (alpha <= 0.03f)
                        return false;
                }
            }

            DrawSegmentSprite(spriteBatch, screenPos, col * alpha);

            if (head != null) {
                // 死亡波辉光
                float flash = head.SegmentDeathFlash(SummonCount);
                Texture2D glow = ACMAsset.SoftGlow;
                if (flash > 0.05f && glow != null) {
                    spriteBatch.Draw(glow, NPC.Center - screenPos, null,
                        (TelegraphColors.Lightning with { A = 0 }) * Math.Min(flash, 1f),
                        0f, glow.Size() * 0.5f, 0.7f, SpriteEffects.None, 0f);
                }
                // P3 全身挂弧: 段序错相位, 每帧只有少数段亮一条电弧 (预算受控)
                if (head.ArcVisual > 0.05f) {
                    Texture2D arcs = ACMAsset.ElectricArcSheet;
                    int bucket = (int)(Main.GlobalTimeWrappedHourly * 9f);
                    if (arcs != null && (SummonCount * 5 + bucket) % 9 == 0) {
                        int rowH = arcs.Height / 4;
                        int row = (SummonCount + bucket) % 4;
                        Rectangle src = new(0, row * rowH, arcs.Width, rowH);
                        float s = 62f / arcs.Width;
                        spriteBatch.Draw(arcs, NPC.Center - screenPos, src,
                            (TelegraphColors.Lightning with { A = 0 }) * (0.55f * head.ArcVisual),
                            NPC.rotation, src.Size() * 0.5f, s, SpriteEffects.None, 0f);
                    }
                }
                // 尾雷预波: 电荷沿脊柱行进的段节微闪
                float pulse = head.SegmentTailPulse(SummonCount);
                if (pulse > 0.05f && glow != null) {
                    spriteBatch.Draw(glow, NPC.Center - screenPos, null,
                        (ArchosaurVFX.BoltCore with { A = 0 }) * (0.6f * pulse),
                        0f, glow.Size() * 0.5f, 0.45f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    [AutoloadBossHead]
    public class ArchosaurHead : ArchosaurBoss
    {
        private static readonly SoundStyle SummonSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurSummon") { Volume = 1f, PitchVariance = .12f, MaxInstances = 5 };
        private static readonly SoundStyle DeathSfx =
            new("AncientChineseMythology/Sounds/Archosaur/ArchosaurDeath") { Volume = 1f, PitchVariance = .04f, MaxInstances = 3 };

        private const string BattleMusicPath = "AncientChineseMythology/Sounds/Archosaur/ArchosaurBattle";

        public override WormType NPCWormType => WormType.Head;
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur_Head";

        // ===== 演出层 (纯本地视觉; 供段节/StormSystem 读取) =====
        public static int ActiveHead = -1;
        public float StormVisual { get; private set; }
        public float WindowVisual { get; private set; }
        public float ChargeGlow { get; private set; }
        public float EyeGlow { get; private set; }
        public float ArcVisual { get; private set; }
        public float DissolveVisual { get; private set; }
        public float WhipWave { get; private set; }
        public float WhipClock { get; private set; }

        /// <summary>当前受伤倍率 (破绽窗口机制): 1=常态, 0.5=幻影存活, &gt;1=逆雷/破绽窗口。</summary>
        public float DamageTakenMult { get; private set; } = 1f;

        /// <summary>宿主是否处于低压招式 (幻影单 striker 节流阀门: 仅此时允许幻影起手俯冲)。</summary>
        public bool InLowPressureState => (int)NPC.ai[2] is A_Roam or A_Volley or A_Nest;

        private float segContactMult = 0.45f;
        public override float SegmentContactMult => segContactMult;

        // ===== 攻击编号 (NPC.ai[2]); ai[0]=Phase(0/1/2), ai[1]=SubSignal, ai[3]=幻影 whoAmI =====
        private const int A_Intro = 0, A_Roam = 1, A_Volley = 2, A_Pierce = 3, A_TailStorm = 4, A_Nest = 5,
            A_TwinCross = 6, A_Reverse = 7, A_Window = 8, A_PhaseSplit = 9, A_PhaseMerge = 10,
            A_Spiral = 11, A_HeavenPierce = 12, A_Death = 13;

        // 时序常量 (tick)
        private const int IntroTicks = 210;
        private const int VolleyCharge = 72, VolleyRecover = 26;
        private const int PierceLoopTicks = 94, PierceLaunch = 66;
        private const int TailWarm = 42, TailInterval = 8, TailMax = 12, TailTelegraph = 40;
        private const int NestCastTicks = 64;
        private const int CrossLaunch = 56, CrossTotal = 110;
        private const int ReverseTicks = 100;
        private const int WindowTicks = 300;
        private const int SplitTicks = 118, SplitTear = 78;
        private const int MergeTicksLong = 110, MergeTicksShort = 50;
        private const int SpiralOrbitStart = 40, SpiralOrbitEnd = 440, SpiralTotal = 470;
        private const int DeathTicks = 230, DeathBolt = 140;
        private const int CloneCooldownTicks = 60 * 11;

        // 同步状态
        private ref float Phase => ref NPC.ai[0];        // 0=P1 1=P2 2=P3
        private ref float SubSignal => ref NPC.ai[1];    // 状态内同步信号 (相变短/长版等)
        private ref float Attack => ref NPC.ai[2];       // AttackId
        private ref float CloneIdx => ref NPC.ai[3];     // 幻影头 whoAmI (-1 无)
        // 本地状态 (非同步; 转移由 ai[]+netUpdate 驱动, 计时器在各端确定性自增)
        private ref float StateTimer => ref NPC.localAI[0];
        private ref float Fig8 => ref NPC.localAI[1];

        private int lastAttack = -1;
        private bool cloneWasAlive;
        private int cloneCooldown;
        private int iFrames;
        private float headContactMult;

        // —— 洗牌袋 (服务器专有) ——
        private readonly List<int> attackBag = new(6);
        private int bagPhase = -1;
        private int lastBagPick = -1;

        // —— 招式瞬态 (本地) ——
        private Vector2 diveDir = Vector2.UnitX;
        private float pierceSide = 1f;     // 本轮俯冲锚点侧向 (循环起始帧锁定, 防穿过玩家时锚点镜像振荡)
        private Vector2 nestCenter;        // 雷巢阵心 (吐球帧锁定, 链电端点与球位一致)
        private float dashTele;            // 冲刺预警线强度
        private Vector2 dashTeleDir = Vector2.UnitX;
        private List<int> tailSegments;
        private int tailFired;
        private int tailNextTimer;
        private float tailPulseFront = -999f;   // 尾雷预波波前 (段序)
        private Vector2 orbitCenter;
        private float spiralAngle;
        private float spiralRadius = 640f;
        private float safeSectorAngle;
        private float safeSectorVis;
        private int pierceMarkIndex = -1;       // 贯天标记 (服务器)
        private bool pierceImpactDone;
        private float diveLightning;            // 贯下期间"龙体即闪电"强度
        private float impactFlash;
        private Vector2 impactPos;
        private float windowRing;               // 破绽窗口节拍环
        private float deathWave = -999f;        // 死亡白闪波前 (按 SummonCount 比较)

        public override void SetStaticDefaults() {
            NPCID.Sets.ShouldBeCountedAsBoss[NPC.type] = true;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
            NPCID.Sets.TrailingMode[NPC.type] = 1;
            NPCID.Sets.TrailCacheLength[NPC.type] = 10;
            Music = MusicLoader.GetMusicSlot(Mod, BattleMusicPath);
            SceneEffectPriority = SceneEffectPriority.BossHigh;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.damage = 260;
        }

        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody2>();

        public override void OnSpawn(IEntitySource source) => SoundEngine.PlaySound(SummonSfx, NPC.Center);

        public override void OnKill() {
            DownedBossSystem.downedArchosaur = true;
            ActiveHead = -1;
            SoundEngine.PlaySound(DeathSfx, NPC.Center);
            ACMUtils.AddScreenShake(14f);
        }

        /// <summary>死亡演出拦截: 首次致死转入 A_Death 剧本, 剧本末尾才真正死亡。</summary>
        public override bool CheckDead() {
            if ((int)Attack != A_Death) {
                NPC.life = Math.Max(NPC.life, 1);
                NPC.dontTakeDamage = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Attack = A_Death;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
                return false;
            }
            return true;
        }

        private bool IsCinematic(int attack) => attack is A_Intro or A_PhaseSplit or A_PhaseMerge or A_Death;

        // ===========================================================
        //  主循环
        // ===========================================================
        public override void AI() {
            ActiveHead = NPC.whoAmI;
            Player target = Target;
            bool server = Main.netMode != NetmodeID.MultiplayerClient;

            // 全员阵亡 → 升空脱战
            if ((!target.active || target.dead) && (int)Attack != A_Death) {
                NPC.velocity.Y -= 0.4f;
                NPC.velocity.X *= 0.98f;
                NPC.EncourageDespawn(30);
                UpdateVisuals();
                return;
            }

            // 客户端: 攻击同步切换时本地复位计时器/瞬态 (localAI 不走同步)
            if ((int)Attack != lastAttack) {
                lastAttack = (int)Attack;
                StateTimer = 0;
                tailSegments = null;
                pierceImpactDone = false;
                dashTele = 0f;
                // 公平阀: 状态被打断时不留冲刺残速 (防旧速度把下一招变成无预警冲撞)
                if (NPC.velocity.Length() > 30f)
                    NPC.velocity *= 0.5f;
            }

            int atk = (int)Attack;

            // 相变检测 (演出/死亡期间锁定)
            if (!IsCinematic(atk)) {
                if (Phase < 2f && NPC.life <= NPC.lifeMax * 0.25f) {
                    Phase = 2f;
                    iFrames = 30;
                    BeginCinematicTransition(server, A_PhaseMerge);
                    // 长/短版: 幻影在场走归一吸收, 否则自聚短版 (SubSignal 同步)
                    if (server)
                        SubSignal = CloneAlive(out _) ? 1f : 0f;
                }
                else if (Phase < 1f && NPC.life <= NPC.lifeMax * 0.6f) {
                    Phase = 1f;
                    iFrames = 30;
                    BeginCinematicTransition(server, A_PhaseSplit);
                }
            }

            // P2 幻影循环 (破绽钥匙); 演出/P3 不参与
            if ((int)Phase == 1 && !IsCinematic((int)Attack))
                HandleCloneCycle(server);

            StateTimer++;
            RunAttack(server, target);

            UpdateDamageMult();
            UpdateContactGate();
            UpdateVisuals();

            // 无敌帧: 演出态常开, 相变余量倒计
            bool cinematic = IsCinematic((int)Attack);
            if (iFrames > 0)
                iFrames--;
            NPC.dontTakeDamage = cinematic || iFrames > 0;
        }

        private void BeginCinematicTransition(bool server, int attack) {
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f }, NPC.Center);
            ACMUtils.AddScreenShake(8f);
            if (server) {
                Attack = attack;
                StateTimer = 0;
                ClearOwnedProjectiles();
                NPC.netUpdate = true;
            }
        }

        // ===========================================================
        //  P2 幻影循环
        // ===========================================================
        private void HandleCloneCycle(bool server) {
            int atk = (int)Attack;
            bool cloneAlive = CloneAlive(out _);
            if (cloneAlive) {
                cloneWasAlive = true;
                cloneCooldown = CloneCooldownTicks;
                return;
            }
            if (cloneWasAlive) {
                // 幻影刚被击破 → 逆雷 + 破绽窗口
                cloneWasAlive = false;
                ACMUtils.AddScreenShake(8f);
                ArchosaurStormSystem.AddFlash(0.55f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f }, NPC.Center);
                if (server) {
                    CloneIdx = -1;
                    Attack = A_Reverse;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
                return;
            }
            // 无幻影常态: 倒计时后重新撕出 (不在逆雷/破绽/对冲期间)
            if (atk != A_Reverse && atk != A_Window && atk != A_TwinCross) {
                if (cloneCooldown > 0)
                    cloneCooldown--;
                else if (server)
                    SpawnClone();
            }
        }

        private bool CloneAlive(out NPC clone) {
            clone = null;
            int i = (int)CloneIdx;
            if (i < 0 || i >= Main.maxNPCs)
                return false;
            NPC n = Main.npc[i];
            if (n.active && n.ModNPC is CloneBossHead) {
                clone = n;
                return true;
            }
            return false;
        }

        // ===========================================================
        //  攻击选择 (洗牌袋 + 反重复; 服务器专有)
        // ===========================================================
        private void RefillBag() {
            attackBag.Clear();
            switch ((int)Phase) {
                case 0: attackBag.AddRange(new[] { A_Volley, A_Pierce, A_TailStorm, A_Nest }); break;
                case 1: attackBag.AddRange(new[] { A_Volley, A_Pierce, A_TailStorm, A_Nest, A_TwinCross }); break;
                default: attackBag.AddRange(new[] { A_Spiral, A_Pierce, A_Volley, A_TailStorm, A_HeavenPierce }); break;
            }
            for (int i = attackBag.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
            }
            if (attackBag.Count > 1 && attackBag[0] == lastBagPick)
                (attackBag[0], attackBag[^1]) = (attackBag[^1], attackBag[0]);
        }

        private int NextFromBag() {
            if (bagPhase != (int)Phase) {
                bagPhase = (int)Phase;
                attackBag.Clear();
            }
            if (attackBag.Count == 0)
                RefillBag();
            int pick = attackBag[0];
            attackBag.RemoveAt(0);
            // 对冲需要幻影在场; 缺席则顶替为贯穿俯冲
            if (pick == A_TwinCross && !CloneAlive(out _))
                pick = A_Pierce;
            lastBagPick = pick;
            return pick;
        }

        // ===========================================================
        //  攻击状态机
        // ===========================================================
        private void RunAttack(bool server, Player target) {
            switch ((int)Attack) {
                case A_Intro: DoIntro(server, target); break;
                case A_Roam: DoRoam(server, target); break;
                case A_Volley: DoVolley(server, target); break;
                case A_Pierce: DoPierce(server, target); break;
                case A_TailStorm: DoTailStorm(server, target); break;
                case A_Nest: DoNest(server, target); break;
                case A_TwinCross: DoTwinCross(server, target); break;
                case A_Reverse: DoReverse(server, target); break;
                case A_Window: DoWindow(server, target); break;
                case A_PhaseSplit: DoPhaseSplit(server, target); break;
                case A_PhaseMerge: DoPhaseMerge(server, target); break;
                case A_Spiral: DoSpiral(server, target); break;
                case A_HeavenPierce: DoHeavenPierce(server, target); break;
                case A_Death: DoDeath(server, target); break;
                default: DoRoam(server, target); break;
            }
        }

        // —— 入场: 雷暴降临 → 巨雷贯下 → 静止凝视 → 仰啸开战 ——
        private void DoIntro(bool server, Player target) {
            float t = StateTimer;

            if (t == 1) {
                // 出生点上移到玩家上空 (雷从天降的因果起点)
                if (server) {
                    NPC.Center = target.Center + new Vector2(0f, -1150f);
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
            }
            if (!Main.dedServ) {
                // 前奏: 两道远雷劈地 → 第三道巨雷正劈出生点
                if (t == 8) {
                    Vector2 p = target.Center + new Vector2(-720f, 0f);
                    ArchosaurStormSystem.AddSkyBolt(new Vector2(p.X, ArchosaurFX.FindGroundY(p)), 0.8f, 0.3f);
                }
                if (t == 22) {
                    Vector2 p = target.Center + new Vector2(640f, 0f);
                    ArchosaurStormSystem.AddSkyBolt(new Vector2(p.X, ArchosaurFX.FindGroundY(p)), 0.9f, 0.35f);
                }
                if (t == 30)
                    ArchosaurStormSystem.AddSkyBolt(NPC.Center, 1.7f, 1f);
            }

            if (t == 30) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                NPC.velocity = new Vector2(0f, 46f);
            }
            else if (t > 30 && t < 84) {
                // S 形贯下: 目标为玩家侧上方悬停位; 到位早退 (不空等)
                Vector2 anchor = target.Center + new Vector2(MathF.Sign(NPC.Center.X - target.Center.X + 0.01f) * 360f, -330f);
                Vector2 want = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * 44f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.08f);
                NPC.velocity.X += MathF.Sin((t - 30f) * 0.17f) * 4.6f;
                if (NPC.Distance(anchor) < 90f && StateTimer < 84)
                    StateTimer = 84;
                FaceVelocity();
            }
            else if (t >= 84 && t < 102) {
                NPC.velocity *= 0.72f;   // 硬刹
                FaceVelocity();
            }
            else if (t >= 102 && t < 162) {
                // 60f 完全静止凝视 — 威压 = 静止 (仅剩呼吸)
                NPC.velocity *= 0.82f;
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
                EyeGlow = Math.Max(EyeGlow, (t - 102f) / 60f);
                if (t == 150)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.6f, Volume = 0.7f }, NPC.Center);
            }
            else if (t >= 162) {
                if (t == 162) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f }, NPC.Center);
                    SoundEngine.PlaySound(SummonSfx, NPC.Center);
                    ACMUtils.AddScreenShake(9f);
                    ArchosaurStormSystem.AddFlash(0.65f);
                    WhipWave = 1f;
                }
                HoverMovement(target, 0.5f);
                if (StateTimer >= IntroTicks)
                    EndToRoam(server);
            }
        }

        // —— 盘旋连接拍 (呼吸) → 洗牌袋选下一招 ——
        private void DoRoam(bool server, Player target) {
            HoverMovement(target, 1f);
            int roam = (int)Phase switch { 0 => 55, 1 => 45, _ => 38 };
            if (server && StateTimer >= roam) {
                Attack = NextFromBag();
                StateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        // —— 残雷齐射: 聚能 (72% 处静默) → 扇形直射 + 后坐; P2/P3 二连射 ——
        private void DoVolley(bool server, Player target) {
            bool twin = Phase >= 1f;
            if (StateTimer < VolleyCharge) {
                HoverMovement(target, 0.45f);
                float p = StateTimer / (float)VolleyCharge;
                ChargeGlow = Math.Max(ChargeGlow, 0.2f + 0.8f * p * p * p);
                if (p < 0.72f)
                    ConvergeParticles(p);
            }
            if (StateTimer == VolleyCharge) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.2f }, NPC.Center);
                ACMUtils.AddScreenShake(5f);
                // 后坐: 发射器也要挨一脚
                NPC.velocity -= (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 7f;
                if (server)
                    FireVolley(target, 0f);
            }
            if (twin && StateTimer == VolleyCharge + 14) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.05f }, NPC.Center);
                NPC.velocity -= (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 5f;
                if (server)
                    FireVolley(target, 0.5f);
            }
            if (StateTimer > VolleyCharge) {
                HoverMovement(target, 0.7f);
                if (StateTimer >= VolleyCharge + VolleyRecover + (twin ? 14 : 0))
                    EndToRoam(server);
            }
        }

        // —— 贯穿俯冲: 占位 → 倒吸蓄势 + 红线 → 瞬发冲刺 (×1.03 复利) → 硬刹 + 鞭波; ×2 (P3 ×3) ——
        private void DoPierce(bool server, Player target) {
            int loops = Phase >= 2f ? 3 : 2;
            float speedMul = Phase >= 2f ? 1.15f : 1f;
            int lt = ((int)StateTimer - 1) % PierceLoopTicks;
            int loopIndex = ((int)StateTimer - 1) / PierceLoopTicks;

            if (loopIndex >= loops || StateTimer > loops * PierceLoopTicks + 20) {
                EndToRoam(server);
                return;
            }

            if (lt == 0)
                pierceSide = (loopIndex % 2 == 0 ? 1f : -1f) * MathF.Sign(NPC.Center.X - target.Center.X + 0.01f);
            Vector2 anchor = target.Center + new Vector2(pierceSide * 480f, -300f);

            if (lt < 30) {
                // 占位 (快而果断)
                Vector2 want = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * 30f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.14f);
                FaceVelocity();
            }
            else if (lt < PierceLaunch) {
                // 倒吸蓄势: pow8 后缩 (nothing…nothing…NOW) + 红线 telegraph
                float bt = (lt - 30) / (float)(PierceLaunch - 30);
                if (lt < PierceLaunch - 8)
                    diveDir = (target.Center + target.velocity * 12f - NPC.Center).SafeNormalize(Vector2.UnitX);
                Vector2 reel = anchor - diveDir * MathF.Pow(bt, 8f) * 240f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (reel - NPC.Center) * 0.2f, 0.3f);
                dashTele = MathHelper.Clamp((lt - 30) / 30f, 0f, 1f);
                dashTeleDir = diveDir;
                NPC.spriteDirection = diveDir.X >= 0 ? 1 : -1;
                if (lt == 34)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
            }
            else if (lt == PierceLaunch) {
                // 瞬发 (公平阀: 距离过近先不提速)
                float launchSpeed = NPC.Distance(target.Center) < 340f ? 40f : 52f;
                NPC.velocity = diveDir * launchSpeed * speedMul;
                dashTele = 0f;
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 1f, Pitch = -0.15f }, NPC.Center);
                ACMUtils.AddScreenShake(7f);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocity();
            }
            else if (lt < PierceLaunch + 10) {
                NPC.velocity *= 1.03f;   // 冲刺复利加速
                FaceVelocity();
            }
            else {
                if (lt == PierceLaunch + 10)
                    WhipWave = 1f;       // 硬刹瞬间的甩尾鞭波
                NPC.velocity *= 0.68f;
                HoverMovement(target, 0.35f);
            }
        }

        // —— 尾雷行波: 电荷沿脊柱尾→头行进 (预波) → 段节依序落雷柱 ——
        private void DoTailStorm(bool server, Player target) {
            HoverMovement(target, 0.85f);
            if (StateTimer <= 1 || tailSegments == null) {
                tailSegments = server ? GatherSegments() : null;
                tailFired = 0;
                tailNextTimer = 0;
            }

            // 预波: 波前从尾 (高段序) 行进到头
            if (StateTimer >= 22 && StateTimer < TailWarm + 30)
                tailPulseFront = MathHelper.Lerp(SummonMax + 4f, 0f, MathHelper.Clamp((StateTimer - 22f) / 26f, 0f, 1f));
            else if (StateTimer >= TailWarm + 30)
                tailPulseFront = -999f;

            if (server && StateTimer >= TailWarm) {
                if (--tailNextTimer <= 0 && tailFired < TailMax && tailSegments != null && tailSegments.Count > 0) {
                    tailNextTimer = TailInterval;
                    // 尾→头方向依序取段 (与预波同向, 因果闭环)
                    float step = tailSegments.Count / (float)TailMax;
                    int idx = tailSegments.Count - 1 - (int)(tailFired * step);
                    idx = Math.Clamp(idx, 0, tailSegments.Count - 1);
                    int seg = tailSegments[idx];
                    if (seg >= 0 && seg < Main.maxNPCs && Main.npc[seg].active) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), Main.npc[seg].Center, Vector2.Zero,
                            ModContent.ProjectileType<ArchosaurTailBolt>(), 120, 0f, Main.myPlayer, seg, TailTelegraph);
                    }
                    tailFired++;
                }
                if (tailFired >= TailMax && StateTimer >= TailWarm + TailInterval * TailMax + 30)
                    EndToRoam(server);
            }
            // 安全超时: 即便无可用段节也不会卡死本状态
            if (server && StateTimer > TailWarm + TailInterval * (TailMax + 4) + 40)
                EndToRoam(server);
        }

        // —— 雷巢: 龙口吐球弧线布阵 → 三角链电 (中点可破); P2 幻影同步布六芒假阵 ——
        private void DoNest(bool server, Player target) {
            HoverMovement(target, 0.9f);
            if (StateTimer == 6) {
                ACMUtils.AddScreenShake(4f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = 0.3f }, NPC.Center);
                if (server)
                    SpawnNestOrbs(target);
            }
            if (StateTimer == 6 + ArchosaurNestOrb.FlightTicks && server)
                SpawnNestLinks(target);
            if (StateTimer >= NestCastTicks)
                EndToRoam(server);
        }

        // —— 双龙对冲: 真幻异高对角就位 → 双红线 → 错拍 14f 交叉冲刺 ——
        private void DoTwinCross(bool server, Player target) {
            if (StateTimer == 2 && server && CloneAlive(out NPC clone)) {
                // 命令幻影进入对冲脚本 (它以自己的计时器错拍执行)
                clone.ai[0] = CloneBossHead.S_Cross;
                clone.netUpdate = true;
            }

            float side = MathF.Sign(NPC.Center.X - target.Center.X + 0.01f);
            Vector2 anchor = target.Center + new Vector2(side * 520f, -260f);

            if (StateTimer < 36) {
                Vector2 want = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * 30f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.15f);
                FaceVelocity();
            }
            else if (StateTimer < CrossLaunch) {
                float bt = (StateTimer - 36f) / (CrossLaunch - 36f);
                if (StateTimer < CrossLaunch - 8)
                    diveDir = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                Vector2 reel = anchor - diveDir * MathF.Pow(bt, 8f) * 200f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (reel - NPC.Center) * 0.2f, 0.3f);
                dashTele = MathHelper.Clamp((StateTimer - 36f) / 16f, 0f, 1f);
                dashTeleDir = diveDir;
                NPC.spriteDirection = diveDir.X >= 0 ? 1 : -1;
            }
            else if (StateTimer == CrossLaunch) {
                NPC.velocity = diveDir * 52f;
                dashTele = 0f;
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 1f, Pitch = -0.2f }, NPC.Center);
                ACMUtils.AddScreenShake(7f);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocity();
            }
            else if (StateTimer < CrossLaunch + 14) {
                NPC.velocity *= 1.02f;
                FaceVelocity();
            }
            else {
                if (StateTimer == CrossLaunch + 14)
                    WhipWave = 1f;
                NPC.velocity *= 0.7f;
                HoverMovement(target, 0.35f);
                if (StateTimer >= CrossTotal)
                    EndToRoam(server);
            }
        }

        // —— 逆雷 (幻影被破触发): 外环向心汇聚, 躲位向外 ——
        private void DoReverse(bool server, Player target) {
            HoverMovement(target, 0.45f);
            WindowVisual = Math.Max(WindowVisual, 0.4f);
            if (StateTimer == 2) {
                if (server)
                    SpawnReverse(target);
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(7f);
            }
            if (StateTimer >= ReverseTicks && server) {
                Attack = A_Window;
                StateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        // —— 破绽窗口: 金光暴露 (受伤 ×1.6) + 魂光节拍环; 末 60f 闪烁加速预告关闭 ——
        private void DoWindow(bool server, Player target) {
            HoverMovement(target, 0.4f);
            ChargeGlow = Math.Max(ChargeGlow, 0.6f);
            EyeGlow = Math.Max(EyeGlow, 0.9f);
            if ((int)StateTimer % 45 == 1) {
                windowRing = 1f;   // 视觉节拍器: 窗口还开着
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.4f }, NPC.Center);
            }
            if (StateTimer == WindowTicks - 8) {
                ArchosaurStormSystem.AddFlash(0.35f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.3f }, NPC.Center);
            }
            if (StateTimer >= WindowTicks)
                EndToRoam(server);
        }

        // —— 相变 P2: 定身痉挛 → 静默 → 撕魂帧 (幻影被拽出) ——
        private void DoPhaseSplit(bool server, Player target) {
            float t = StateTimer;
            if (t == 2) {
                NPC.velocity *= 0.5f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
            }
            if (t < SplitTear - 12) {
                // 痉挛: 密度 ∝ √t 的电弧爆花 + 渐强低鸣
                NPC.velocity *= 0.9f;
                float p = MathF.Sqrt(MathHelper.Clamp(t / SplitTear, 0f, 1f));
                if (!Main.dedServ && Main.rand.NextFloat() < p * 0.8f) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(46f, 46f),
                        DustID.Electric, Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.5f);
                    d.noGravity = true;
                }
                if ((int)t % 16 == 0)
                    ACMUtils.AddScreenShake(1.5f);
                ChargeGlow = Math.Max(ChargeGlow, p * 0.8f);
            }
            else if (t < SplitTear) {
                NPC.velocity *= 0.9f;   // 12f 静默 — 尖叫前的吸气
            }
            else if (t == SplitTear) {
                // 撕裂帧
                ArchosaurStormSystem.AddFlash(1f);
                if (!Main.dedServ)
                    ArchosaurStormSystem.AddSkyBolt(NPC.Center, 1.4f, 0.9f);
                ACMUtils.AddScreenShake(11f);
                SoundEngine.PlaySound(SoundID.NPCDeath56 with { Volume = 1f, Pitch = -0.2f }, NPC.Center);
                WhipWave = 1f;
                NPC.velocity = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX) * 8f;   // 反冲
                if (server)
                    SpawnClone();
            }
            else {
                NPC.velocity *= 0.93f;
                if (StateTimer >= SplitTicks) {
                    iFrames = 45;
                    cloneCooldown = CloneCooldownTicks;
                    EndToRoam(server);
                }
            }
        }

        // —— 相变 P3: 幻影化光归一 (长版) / 自聚雷光 (短版) → 金光暴涨 ——
        private void DoPhaseMerge(bool server, Player target) {
            bool longVer = SubSignal >= 1f;
            int total = longVer ? MergeTicksLong : MergeTicksShort;
            float t = StateTimer;

            if (t == 2) {
                NPC.velocity *= 0.6f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                if (server && CloneAlive(out NPC clone)) {
                    clone.ai[0] = CloneBossHead.S_Merge;
                    clone.netUpdate = true;
                }
            }
            NPC.velocity *= 0.94f;

            int burstAt = longVer ? 80 : 30;
            if (t < burstAt) {
                // 金色汇聚 (幻影方向 / 自身四周)
                ChargeGlow = Math.Max(ChargeGlow, 0.3f + 0.7f * t / burstAt);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(180f, 180f);
                    Dust d = Dust.NewDustPerfect(from, DustID.GoldCoin, (NPC.Center - from) * 0.05f, 0, default, 1.2f);
                    d.noGravity = true;
                }
            }
            if (t == burstAt) {
                // 吸收爆闪: P3 全身挂弧点亮
                ArchosaurStormSystem.AddFlash(0.85f);
                ACMUtils.AddScreenShake(9f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.4f }, NPC.Center);
                ArcVisual = 1f;
                EyeGlow = 1f;
                WhipWave = 0.8f;
            }
            if (StateTimer >= total) {
                iFrames = 45;
                EndToRoam(server);
            }
        }

        // —— P3 雷渊螺旋: 绕玩家收缩盘旋 + 段节落雷 (旋转安全扇区标注) → 甩尾破圈 ——
        private void DoSpiral(bool server, Player target) {
            float t = StateTimer;
            if (t == 2) {
                orbitCenter = target.Center;
                spiralAngle = (NPC.Center - target.Center).ToRotation();
                safeSectorAngle = spiralAngle + MathHelper.Pi;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f }, NPC.Center);
                ACMUtils.AddScreenShake(6f);
            }

            if (t < SpiralOrbitEnd) {
                // 软栓绳轨道中心 + 收缩半径
                orbitCenter = Vector2.Lerp(orbitCenter, target.Center, 0.02f);
                float op = MathHelper.Clamp((t - SpiralOrbitStart) / (SpiralOrbitEnd - SpiralOrbitStart), 0f, 1f);
                spiralRadius = MathHelper.Lerp(640f, 400f, op);
                spiralAngle += 0.028f;
                safeSectorAngle += 0.006f;
                safeSectorVis = MathHelper.Clamp(t / 60f, 0f, 1f);

                Vector2 desired = orbitCenter + spiralAngle.ToRotationVector2() * spiralRadius;
                Vector2 want = desired - NPC.Center;
                float cap = Math.Min(want.Length() * 0.25f, 40f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, want.SafeNormalize(Vector2.Zero) * cap, 0.2f);
                FaceVelocity();

                // 段节落雷波 (跳过安全扇区)
                if (server && t >= 60 && (int)t % 36 == 0) {
                    List<int> segs = GatherSegments();
                    if (segs.Count > 0) {
                        for (int i = 0; i < 3; i++) {
                            int idx = (int)(segs.Count * (i + 0.5f) / 3f);
                            idx = Math.Clamp(idx, 0, segs.Count - 1);
                            NPC segN = Main.npc[segs[idx]];
                            if (!segN.active)
                                continue;
                            float segAng = (segN.Center - orbitCenter).ToRotation();
                            float diff = MathHelper.WrapAngle(segAng - safeSectorAngle);
                            if (Math.Abs(diff) < MathHelper.ToRadians(38f))
                                continue;   // 公平阀: 安全扇区不落雷
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), segN.Center, Vector2.Zero,
                                ModContent.ProjectileType<ArchosaurTailBolt>(), 120, 0f, Main.myPlayer, segs[idx], TailTelegraph);
                        }
                    }
                }
                // 环境雷 (纯视觉)
                if (!Main.dedServ && (int)t % 44 == 0 && t > 40)
                    ArchosaurStormSystem.AddSkyBolt(orbitCenter + Main.rand.NextVector2CircularEdge(900f, 700f), 0.85f, 0.3f);
            }
            else {
                // 破圈: 甩尾向外散开
                if (t == SpiralOrbitEnd) {
                    WhipWave = 1f;
                    NPC.velocity = (NPC.Center - orbitCenter).SafeNormalize(Vector2.UnitX) * 30f;
                    ACMUtils.AddScreenShake(6f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f }, NPC.Center);
                }
                safeSectorVis = MathHelper.Lerp(safeSectorVis, 0f, 0.1f);
                NPC.velocity *= 0.93f;
                FaceVelocity();
                if (StateTimer >= SpiralTotal)
                    EndToRoam(server);
            }
        }

        // —— P3 贯天雷柱 (处决级华彩): 标记锁定 → 冲天离场 → 静默 → 携雷贯下 + 次生双柱 ——
        private void DoHeavenPierce(bool server, Player target) {
            float t = StateTimer;

            if (t == 2) {
                pierceImpactDone = false;
                if (server) {
                    pierceMarkIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<ArchosaurPierceMark>(), 0, 0f, Main.myPlayer, 75f, target.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
            }

            if (t < 30) {
                // 升到玩家侧上方远处
                Vector2 anchor = target.Center + new Vector2(MathF.Sign(NPC.Center.X - target.Center.X + 0.01f) * 240f, -540f);
                Vector2 want = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * 26f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.12f);
                FaceVelocity();
            }
            else if (t < 90) {
                // 蓄力: 反向下沉蓄势 + 汇聚粒子 (72% 硬切)
                float p = (t - 30f) / 60f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0f, 2.2f + p * 3f), 0.1f);
                ChargeGlow = Math.Max(ChargeGlow, 0.2f + 0.8f * p * p);
                if (p < 0.72f)
                    ConvergeParticles(p);
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            }
            else if (t == 90) {
                // 冲天离场 (可见的"离开", 不是瞬移消失)
                NPC.velocity = new Vector2(0f, -70f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = 0.25f }, NPC.Center);
                ACMUtils.AddScreenShake(6f);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocity();
            }
            else if (t < 126) {
                FaceVelocity();   // 高速升空中
            }
            else if (t == 126) {
                // 屏外整体瞬移至标记正上方 (合法屏外重定位)
                Vector2 markPos = GetMarkPosition(target);
                if (server) {
                    TeleportBodyBy(new Vector2(markPos.X, markPos.Y - 1500f) - NPC.Center);
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
            }
            else if (t < 150) {
                NPC.velocity *= 0.8f;   // 25f 静默 — 屏上只剩标记脉冲
            }
            else if (t == 150) {
                Vector2 markPos = GetMarkPosition(target);
                // 公平阀: 玩家已远离标记 → 放弃贯下
                if (Vector2.Distance(target.Center, markPos) > 2000f) {
                    if (server)
                        KillMark();
                    EndToRoam(server);
                    return;
                }
                NPC.velocity = new Vector2(0f, 78f);
                diveLightning = 1f;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1.2f, Pitch = -0.3f }, NPC.Center);
                if (server)
                    NPC.netUpdate = true;
                FaceVelocity();
            }
            else if (t > 150) {
                Vector2 markPos = GetMarkPosition(target);
                if (!pierceImpactDone && NPC.Center.Y >= markPos.Y - 40f) {
                    // 触地帧: 冲击 + 次生双柱 (锁定起已画线预警 ~75f)
                    pierceImpactDone = true;
                    impactFlash = 1f;
                    impactPos = markPos;
                    ACMUtils.AddScreenShake(13f);
                    ArchosaurStormSystem.AddFlash(0.8f);
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.1f, Pitch = -0.3f }, markPos);
                    WhipWave = 1f;
                    if (server) {
                        for (int s = -1; s <= 1; s += 2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                new Vector2(markPos.X + s * ArchosaurPierceMark.SidePillarOffset, markPos.Y - 700f),
                                Vector2.Zero, ModContent.ProjectileType<ArchosaurTailBolt>(), 120, 0f, Main.myPlayer, -1f, 14f);
                        }
                        KillMark();
                    }
                }
                if (pierceImpactDone) {
                    if (NPC.Center.Y > impactPos.Y + 420f)
                        NPC.velocity *= 0.8f;   // 贯过后减速回升
                    diveLightning = Math.Max(diveLightning - 0.03f, 0f);
                    HoverMovement(target, 0.3f);
                }
                else {
                    FaceVelocity();
                }
                if (StateTimer >= 240 || (pierceImpactDone && StateTimer >= 150 + 90))
                    EndToRoam(server);
            }
            // 总超时保底
            if (StateTimer > 300) {
                if (server)
                    KillMark();
                EndToRoam(server);
            }
        }

        // —— 死亡剧本: 段节死亡波 → 仰天雷暴 → 巨雷贯体 → 化光屑散去 ——
        private void DoDeath(bool server, Player target) {
            float t = StateTimer;
            NPC.dontTakeDamage = true;

            if (t == 1) {
                if (server) {
                    ClearOwnedProjectiles();
                    if (CloneAlive(out NPC clone)) {
                        clone.ai[0] = CloneBossHead.S_Vanish;
                        clone.netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.1f }, NPC.Center);
            }

            if (t < 40) {
                // 定身抽搐 + 段节死亡波 (尾→头)
                NPC.velocity *= 0.85f;
                deathWave = 84f - t * 2.2f;
            }
            else if (t < DeathBolt) {
                deathWave = -999f;
                // 仰天缓升, 天雷频率拉满
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(MathF.Sin(t * 0.05f) * 2f, -2.6f), 0.05f);
                FaceVelocity();
                if (!Main.dedServ && (int)t % 18 == 0)
                    ArchosaurStormSystem.AddSkyBolt(NPC.Center + Main.rand.NextVector2Circular(650f, 350f), 1f, 0.4f);
                EyeGlow = 1f;
            }
            else if (t == DeathBolt) {
                // 全场唯一的最重拍: 巨雷贯体
                if (!Main.dedServ)
                    ArchosaurStormSystem.AddSkyBolt(NPC.Center, 2.6f, 1f);
                ArchosaurStormSystem.AddFlash(1f);
                ACMUtils.AddScreenShake(16f);
                impactFlash = 1f;
                impactPos = NPC.Center;
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.3f, Pitch = -0.5f }, NPC.Center);
            }
            else {
                // 化光屑散去
                DissolveVisual = MathHelper.Clamp((t - DeathBolt) / 60f, 0f, 1f);
                NPC.velocity *= 0.9f;
                if (server && StateTimer >= DeathTicks) {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.checkDead();   // CheckDead 此时放行 → OnKill (downed/掉落/清单不回退)
                }
            }
        }

        private void EndToRoam(bool server) {
            if (!server)
                return;
            Attack = A_Roam;
            StateTimer = 0;
            NPC.netUpdate = true;
        }

        // ===========================================================
        //  生成器 (服务器权威)
        // ===========================================================
        private void FireVolley(Player target, float slotOffset) {
            const int count = 9;
            Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float spread = MathHelper.ToRadians(64f);
            // 公平阀: 贴脸时扇形放大, 留出逃逸缝
            if (NPC.Distance(target.Center) < 200f)
                spread *= 1.35f;
            float gap = spread / (count - 1);
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 dir = toP.RotatedBy(MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, t) + slotOffset * gap);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 13f,
                    ModContent.ProjectileType<ArchosaurStormOrb>(), 90, 0f, Main.myPlayer);
            }
        }

        private void SpawnNestOrbs(Player target) {
            nestCenter = target.Center;
            Vector2 center = nestCenter;
            // 真阵: 龙口吐出 3 球
            for (int i = 0; i < 3; i++) {
                float ang = MathHelper.PiOver2 + i * MathHelper.TwoPi / 3f;
                Vector2 slot = center + ang.ToRotationVector2() * 270f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurNestOrb>(), 0, 0f, Main.myPlayer, slot.X, slot.Y, 0f);
            }
            // P2 幻影在场: 从幻影处吐出旋转 60° 的假阵 (读法训练: 灰蓝无节点=假)
            if (Phase >= 1f && CloneAlive(out NPC clone)) {
                for (int i = 0; i < 3; i++) {
                    float ang = MathHelper.PiOver2 + MathHelper.Pi / 3f + i * MathHelper.TwoPi / 3f;
                    Vector2 slot = center + ang.ToRotationVector2() * 270f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), clone.Center, Vector2.Zero,
                        ModContent.ProjectileType<ArchosaurNestOrb>(), 0, 0f, Main.myPlayer, slot.X, slot.Y, 1f);
                }
            }
        }

        private void SpawnNestLinks(Player target) {
            // 用吐球帧锁定的阵心, 保证链电端点与已入位的球重合
            SpawnLinkTriangle(nestCenter, MathHelper.PiOver2, 0f);
            if (Phase >= 1f && CloneAlive(out _))
                SpawnLinkTriangle(nestCenter, MathHelper.PiOver2 + MathHelper.Pi / 3f, 2f);
        }

        private void SpawnLinkTriangle(Vector2 center, float baseAng, float mode) {
            Vector2[] pts = new Vector2[3];
            for (int i = 0; i < 3; i++)
                pts[i] = center + (baseAng + i * MathHelper.TwoPi / 3f).ToRotationVector2() * 270f;
            for (int i = 0; i < 3; i++) {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % 3];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 half = (b - a) * 0.5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), mid, Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurNestLink>(), 130, 0f, Main.myPlayer, half.X, half.Y, mode);
            }
        }

        private void SpawnReverse(Player target) {
            Vector2 center = target.Center;
            const int count = 11;
            const float radius = 740f;
            for (int i = 0; i < count; i++) {
                float ang = i * MathHelper.TwoPi / count + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 pos = center + ang.ToRotationVector2() * radius;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<ArchosaurReverseBolt>(), 110, 0f, Main.myPlayer,
                    center.X, center.Y, 36f);
            }
        }

        /// <summary>幻影从本体位置被撕出 (出生行进由幻影自身的 Birth 态承担, 因果可见)。</summary>
        private void SpawnClone() {
            int id = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<CloneBossHead>());
            if (id >= 0 && id < Main.maxNPCs) {
                Main.npc[id].ai[3] = NPC.whoAmI;
                CloneIdx = id;
                cloneWasAlive = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: id);
            }
            NPC.netUpdate = true;
        }

        private void ClearOwnedProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int t1 = ModContent.ProjectileType<ArchosaurStormOrb>();
            int t2 = ModContent.ProjectileType<ArchosaurTailBolt>();
            int t3 = ModContent.ProjectileType<ArchosaurNestOrb>();
            int t4 = ModContent.ProjectileType<ArchosaurNestLink>();
            int t5 = ModContent.ProjectileType<ArchosaurReverseBolt>();
            int t6 = ModContent.ProjectileType<ArchosaurPierceMark>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == t1 || p.type == t2 || p.type == t3 || p.type == t4 || p.type == t5 || p.type == t6))
                    p.Kill();
            }
        }

        private void KillMark() {
            if (pierceMarkIndex >= 0 && pierceMarkIndex < Main.maxProjectiles) {
                Projectile p = Main.projectile[pierceMarkIndex];
                if (p.active && p.type == ModContent.ProjectileType<ArchosaurPierceMark>())
                    p.Kill();
            }
            pierceMarkIndex = -1;
        }

        /// <summary>读取贯天标记的当前位置 (各端从活跃标记读取; 无标记时以玩家脚下地面兜底)。</summary>
        private Vector2 GetMarkPosition(Player target) {
            int t = ModContent.ProjectileType<ArchosaurPierceMark>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == t)
                    return p.Center;
            }
            Vector2 fallback = target.Center;
            fallback.Y = ArchosaurFX.FindGroundY(fallback) - 8f;
            return fallback;
        }

        /// <summary>整链平移 (贯天屏外重定位): 头与全部段节同 delta 位移, 保持链形。</summary>
        private void TeleportBodyBy(Vector2 delta) {
            NPC.Center += delta;
            NPC.netUpdate = true;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.realLife != NPC.whoAmI || i == NPC.whoAmI)
                    continue;
                if (n.ModNPC is ArchosaurBoss) {
                    n.Center += delta;
                    n.netUpdate = true;
                }
            }
        }

        // ===========================================================
        //  辅助
        // ===========================================================
        private List<int> GatherSegments() {
            List<int> list = new();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.realLife != NPC.whoAmI)
                    continue;
                if (n.ModNPC is ArchosaurBoss bw && bw.NPCWormType != WormType.Head)
                    list.Add(i);
            }
            list.Sort((x, y) => {
                int sx = (Main.npc[x].ModNPC as BasicWorm)?.SummonCount ?? 0;
                int sy = (Main.npc[y].ModNPC as BasicWorm)?.SummonCount ?? 0;
                return sx.CompareTo(sy);
            });
            return list;
        }

        private void UpdateDamageMult() {
            float m = 1f;
            if ((int)Phase >= 1) {
                if ((int)Attack == A_Window) m = 1.6f;
                else if ((int)Attack == A_Reverse) m = 1.2f;
                else if ((int)Phase == 1 && CloneAlive(out _)) m = 0.5f;
            }
            DamageTakenMult = m;
        }

        /// <summary>接触伤害门控: 伤害窗口与视觉严格对齐 (冲刺才有全额接触, 演出/窗口零接触)。</summary>
        private void UpdateContactGate() {
            // 头部不走 base.AI, 在此捕获难度缩放后的基准
            if (scaledContact < 0)
                scaledContact = Math.Max(1, NPC.damage);
            int atk = (int)Attack;
            float speed = NPC.velocity.Length();
            float headMult, segMult;
            if (IsCinematic(atk) || atk == A_Window) {
                headMult = 0f;
                segMult = 0f;
            }
            else if (atk is A_Pierce or A_TwinCross or A_HeavenPierce) {
                bool striking = speed > 24f;
                headMult = striking ? 1f : 0.35f;
                segMult = striking ? 0.75f : 0.3f;
            }
            else if (atk == A_Spiral) {
                headMult = 0.6f;
                segMult = 0.6f;   // 身体是笼壁不是刃
            }
            else {
                headMult = 0.45f;
                segMult = 0.35f;
            }
            headContactMult = headMult;
            segContactMult = segMult;
            NPC.damage = (int)(scaledContact * headContactMult);
        }

        private void UpdateVisuals() {
            int atk = (int)Attack;

            // 雷暴强度: 阶段基准 / 入场爬升 / 窗口与死亡拉满
            float stormTarget = (int)Phase switch { 0 => 0.45f, 1 => 0.7f, _ => 0.85f };
            if (atk == A_Intro) stormTarget = MathHelper.Clamp(StateTimer / 30f, 0f, 1f) * 0.85f;
            if (atk == A_Window || atk == A_Death) stormTarget = 1f;
            StormVisual = MathHelper.Lerp(StormVisual, stormTarget, 0.03f);

            WindowVisual = MathHelper.Lerp(WindowVisual, atk == A_Window ? 1f : 0f, 0.05f);
            ChargeGlow = MathHelper.Lerp(ChargeGlow, 0f, 0.1f);

            float eyeTarget = (int)Phase switch { 0 => 0.25f, 1 => 0.45f, _ => 0.75f };
            if (atk == A_Window) eyeTarget = 1f;
            if (atk == A_Intro) eyeTarget = Math.Min(EyeGlow, 1f);   // 入场由剧本推
            EyeGlow = MathHelper.Lerp(EyeGlow, eyeTarget, 0.03f);

            ArcVisual = MathHelper.Lerp(ArcVisual, (int)Phase == 2 ? 0.8f : 0f, 0.04f);

            WhipWave *= 0.94f;
            WhipClock++;
            dashTele *= 0.85f;
            impactFlash *= 0.86f;
            windowRing *= 0.955f;
            diveLightning = atk == A_HeavenPierce ? diveLightning : 0f;
            if (atk != A_Spiral)
                safeSectorVis = MathHelper.Lerp(safeSectorVis, 0f, 0.1f);
            if (atk != A_TailStorm)
                tailPulseFront = -999f;
            if (atk != A_Death) {
                deathWave = -999f;
                DissolveVisual = 0f;
            }

            if (!Main.dedServ)
                ArchosaurStormSystem.Publish(StormVisual, WindowVisual);
        }

        /// <summary>死亡波: 段节按 SummonCount 尾→头依次白闪 (供段节绘制/AI 读取)。</summary>
        public float SegmentDeathFlash(int summonCount) {
            if (deathWave <= -900f)
                return 0f;
            float since = (summonCount - deathWave) * 0.5f;
            return since > 0f ? MathF.Exp(-since * 0.35f) : 0f;
        }

        /// <summary>尾雷预波: 电荷沿脊柱行进的段节微闪强度。</summary>
        public float SegmentTailPulse(int summonCount) {
            if (tailPulseFront <= -900f)
                return 0f;
            float d = Math.Abs(summonCount - tailPulseFront);
            return d < 7f ? 1f - d / 7f : 0f;
        }

        private void ConvergeParticles(float p) {
            if (Main.dedServ)
                return;
            // 双族汇聚: 径向吸入 + 切向环绕 (密度 ∝ √p)
            int n = 1 + (int)(MathF.Sqrt(p) * 4);
            for (int i = 0; i < n; i++) {
                Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(150f, 150f) * (1.15f - p * 0.6f);
                Vector2 pull = (NPC.Center - from) * 0.07f;
                bool orbit = i % 2 == 1;
                Dust d = Dust.NewDustPerfect(from, DustID.Electric,
                    orbit ? pull.RotatedBy(MathHelper.PiOver2) * 0.8f : pull, 80, default, 1.2f);
                d.noGravity = true;
            }
        }

        // 8 字 Lissajous 盘绕; speed 调制跟随刚度与角速度
        private void HoverMovement(Player target, float speed) {
            const float R = 300f, r = 150f, h = 400f, baseW = 0.03f;
            Fig8 += baseW * MathHelper.Clamp(speed, 0.25f, 1.5f);
            if (Fig8 > MathHelper.TwoPi) Fig8 -= MathHelper.TwoPi;
            float ox = R * MathF.Cos(Fig8);
            float oy = r * MathF.Sin(Fig8 * 2f);
            Vector2 desired = target.Center + new Vector2(ox, -h + oy);
            Vector2 toGoal = desired - NPC.Center;
            NPC.velocity = (NPC.velocity * 89f + (toGoal / 8f) * speed) / 90f;
            FaceVelocity();
        }

        private void FaceVelocity() {
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;
        }

        // ===========================================================
        //  绘制 (头部)
        // ===========================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            int atk = (int)Attack;
            Texture2D glow = ACMAsset.SoftGlow;

            // —— 冲刺预警线 (红 = 致命) ——
            if (dashTele > 0.03f) {
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dashTeleDir * 1150f,
                    MathHelper.Lerp(2f, 6f, dashTele), TelegraphColors.Lethal, TelegraphColors.Lethal * 0.3f,
                    dashTele * 0.85f, flowSpeed: 2.6f, flowScale: 2.6f);
            }

            // —— 雷渊螺旋: 安全扇区标注 (玉色 = 安全) ——
            if (safeSectorVis > 0.05f) {
                Vector2 dir = safeSectorAngle.ToRotationVector2();
                ACMShaders.DrawBeam(orbitCenter, orbitCenter + dir * spiralRadius,
                    36f, TelegraphColors.Safe, TelegraphColors.Safe * 0.15f, safeSectorVis * 0.3f,
                    flowSpeed: 0.8f, flowScale: 1.5f, coreSharp: 1.2f);
                if (glow != null) {
                    spriteBatch.Draw(glow, orbitCenter + dir * (spiralRadius * 0.6f) - screenPos, null,
                        (TelegraphColors.Safe with { A = 0 }) * (0.5f * safeSectorVis),
                        0f, glow.Size() * 0.5f, 1.1f, SpriteEffects.None, 0f);
                }
            }

            // —— 贯天: 龙体即闪电 ——
            if (diveLightning > 0.05f && NPC.velocity.Y > 30f) {
                ArchosaurVFX.DrawLightningStrip(NPC.Center - new Vector2(0f, 950f), NPC.Center + new Vector2(0f, 220f),
                    54f, ArchosaurVFX.BoltCore, TelegraphColors.Lightning, diveLightning, 0.37f, jagAmp: 0.4f, flicker: 0.5f);
            }

            // —— 触地冲击泛光 (占全屏名额, 内部自走契约) ——
            if (impactFlash > 0.04f)
                ACMShaders.DrawRadialBloomAt(impactPos, 0.2f, impactFlash, TelegraphColors.Lightning, rayCount: 9f);

            // —— 残影 (速度门控: 只在真正快时出现) ——
            float speed = NPC.velocity.Length();
            if (speed > 24f) {
                Texture2D tex = TextureAssets.Npc[Type].Value;
                Vector2 origin = new(NPC.spriteDirection == -1 ? 0 : tex.Width, 20);
                origin.Y += 34;
                origin.X = NPC.spriteDirection == -1 ? (tex.Width / 4) : (tex.Width / 4 * 3);
                Color trailCol = (TelegraphColors.Lightning with { A = 0 }) * 0.55f;
                for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                    float fade = 1f - i / (float)NPC.oldPos.Length;
                    spriteBatch.Draw(tex, NPC.oldPos[i] + NPC.Size * 0.5f - screenPos, null, trailCol * (fade * 0.6f),
                        NPC.rotation, origin, NPC.scale, NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                }
            }

            // —— 蓄能辉光 ——
            if (ChargeGlow > 0.02f && glow != null) {
                Color c = Color.Lerp(TelegraphColors.Lightning, ArchosaurVFX.GoldSoul, WindowVisual) with { A = 0 };
                float pulse = 1.4f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
                spriteBatch.Draw(glow, NPC.Center - screenPos, null, c * ChargeGlow, 0f, glow.Size() * 0.5f,
                    pulse * (0.5f + ChargeGlow), SpriteEffects.None, 0f);
            }

            // —— 破绽窗口节拍环 (金色魂光, 45f 一拍; 末 60f 闪烁加速) ——
            if (windowRing > 0.03f && glow != null) {
                float expand = 1f - windowRing;
                float flickerMul = 1f;
                if (atk == A_Window && StateTimer > WindowTicks - 60)
                    flickerMul = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 40f);
                spriteBatch.Draw(glow, NPC.Center - screenPos, null,
                    (ArchosaurVFX.GoldSoul with { A = 0 }) * (windowRing * 0.7f * flickerMul),
                    0f, glow.Size() * 0.5f, 0.8f + expand * 4.2f, SpriteEffects.None, 0f);
            }

            // —— 本体贴图 (死亡溶解: 转白渐隐) ——
            Color col = drawColor;
            float alpha = 1f;
            if (WindowVisual > 0.05f)
                col = Color.Lerp(col, ArchosaurVFX.GoldSoul, WindowVisual * 0.35f);
            float selfFlash = SegmentDeathFlash(0);
            if (selfFlash > 0f)
                col = Color.Lerp(col, Color.White, Math.Min(selfFlash, 1f));
            if (DissolveVisual > 0f) {
                col = Color.Lerp(col, Color.White, DissolveVisual * 0.7f);
                alpha = MathHelper.Clamp(1f - DissolveVisual, 0f, 1f);
            }
            if (alpha > 0.03f)
                DrawSegmentSprite(spriteBatch, screenPos, col * alpha);

            // —— 鎏金瞳 (真身识别读法) ——
            if (EyeGlow > 0.04f && glow != null && alpha > 0.03f) {
                Vector2 forward = NPC.velocity.SafeNormalize(new Vector2(NPC.spriteDirection, 0f));
                Vector2 eyePos = NPC.Center + forward * 24f - new Vector2(0f, 6f);
                spriteBatch.Draw(glow, eyePos - screenPos, null, (ArchosaurVFX.GoldSoul with { A = 0 }) * EyeGlow,
                    0f, glow.Size() * 0.5f, 0.28f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, eyePos - screenPos, null, (Color.White with { A = 0 }) * (EyeGlow * 0.7f),
                    0f, glow.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
            }

            // —— P3 头部挂弧 ——
            if (ArcVisual > 0.05f && alpha > 0.03f) {
                Texture2D arcs = ACMAsset.ElectricArcSheet;
                int bucket = (int)(Main.GlobalTimeWrappedHourly * 9f);
                if (arcs != null && bucket % 3 == 0) {
                    int rowH = arcs.Height / 4;
                    Rectangle src = new(0, (bucket / 3) % 4 * rowH, arcs.Width, rowH);
                    spriteBatch.Draw(arcs, NPC.Center - screenPos, src,
                        (TelegraphColors.Lightning with { A = 0 }) * (0.6f * ArcVisual),
                        NPC.rotation, src.Size() * 0.5f, 78f / arcs.Width, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    public class ArchosaurBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
            NPC.height = 50;
        }
    }
    public class ArchosaurBody2 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<ArchosaurBody2>();
            if (SummonCount == SummonMax / 3 * 2 || SummonCount == 15)
                SummonNPCType = ModContent.NPCType<ArchosaurBody1>();
            if (SummonCount > SummonMax - 15)
                SummonNPCType = ModContent.NPCType<ArchosaurBody3>();
        }
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 15;
        }
    }
    public class ArchosaurBody3 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurBody4>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class ArchosaurBody4 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<ArchosaurTail>();
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class ArchosaurTail : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Tail;
        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
}

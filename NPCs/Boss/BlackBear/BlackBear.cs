using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Weapons.Bows;
using AncientChineseMythology.Items.Weapons.SummoningStaffs;
using AncientChineseMythology.Items.Weapons.Swords;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精 Black Bear —— 黑风山黑风大王 (V3 全面重做)。早期地面型 Boss, 核心课题"重量感":
    /// 每一步扬尘、每次落地震屏、每招都有 anticipation → burst → recovery 波形与反冲/挤压次级运动。
    ///
    /// 主题三意象: <b>重</b> (巨兽体魄) / <b>黑风</b> (妖风领域: 专属 Sky + BlackBearDarkWind 全屏风扭曲) /
    /// <b>金</b> (袈裟因缘: 金冠光环、死亡被金光"收服"而非死亡)。
    ///
    /// 阶段: Intro 入场砸落 → P1 五招基础池 (防重复选招) → 50% Enrage 相变 (清弹+i-frame 咆哮, 黑风漫天)
    /// → P2 升级+新招 (黑风连环冲/蜜雨咆哮/金冠光环) → &lt;25% 一场一次"黑风怒嚎"+疲劳窗口 → 收服演出。
    ///
    /// 伤害契约: 仅攻击激活帧有接触伤害 (其余 <see cref="NPC.damage"/>=0); 伤害窗口与视觉严格对齐。
    /// 多人安全: FSM 全部走 <see cref="NPC.ai"/> + SendExtraAI; 弹幕仅服务器生成; 视觉字段仅客户端消费。
    /// </summary>
    [AutoloadBossHead]
    public class BlackBear : ModNPC
    {
        private enum BearState
        {
            Intro = 0,      // 入场演出
            Chase = 1,      // 地面追击 (连接组织)
            Swipe = 2,      // 重踏挥击 (近)
            Toss = 3,       // 蜜蜡投掷 (中远)
            BearHug = 4,    // 熊抱冲撞 (中)
            Slam = 5,       // 立地震地 (远)
            Pounce = 6,     // 跃扑 (中)
            FuryRush = 7,   // P2 黑风连环冲
            HoneyRoar = 8,  // P2 蜜雨咆哮
            TempestHowl = 9,// P2<25% 一场一次大招
            Enrage = 10,    // 半血相变演出
            Dying = 11      // 收服演出
        }

        #region 同步状态 (ai[] + SendExtraAI)

        private BearState State {
            get => (BearState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        private ref float StateTimer => ref NPC.ai[1];   // 当前状态帧计时
        private ref float StateParam => ref NPC.ai[2];   // 状态参数 (冲撞方向 / 子段计数 / 落地标志 / 风暴缺口)
        private ref float AttackCooldown => ref NPC.ai[3];

        private bool furyTriggered;   // 已触发半血相变
        private bool tempestUsed;     // 已用过黑风怒嚎
        private bool allowRealDeath;  // 收服演出结束, 允许真实死亡结算
        private int lastAttack = -1;  // 防重复选招
        private int prevAttack = -1;

        #endregion

        #region 本地字段 (不参与同步)

        private int baseContactDamage;      // 首帧捕获难度缩放后的接触伤害
        private int haloTimer = 240;        // P2 金冠光环调度 (仅服务器消费)
        private bool wasAirborne;           // 落地检测
        private float prevFallSpeed;

        // —— 重量感视觉 (仅客户端绘制消费) ——
        private float leanVisual;           // 身体前倾/后仰 (rad, 绕脚底)
        private float leanTarget;
        private float leanSnap = 0.08f;     // 倾斜追赶速度 (蓄力慢, 爆发快)
        private float squashVisual = 1f;    // 纵向挤压 (<1 压扁, >1 拉伸)
        private float squashTarget = 1f;

        // —— 演出强度 (客户端) ——
        private float slamFlash;            // 爆发泛光
        private float whiteFlash;           // 死亡金光白闪 (身体过曝)
        private float windPulse;            // 黑风脉冲 (入场/相变/怒嚎瞬间)
        private float windDraw;             // 全屏黑风当前强度 (平滑)
        private float goldDraw;             // 全屏金光当前强度 (平滑)
        private float crownGlint;           // 头顶金冠闪

        #endregion

        private const float RunSpeed = 11.5f;
        private const int LungeStart = 50;    // BearHug 前摇长度
        private const int SlamChargeEnd = 128; // Slam 蓄力结束帧

        private bool Fury => NPC.life * 2 < NPC.lifeMax;
        private bool LowHP => NPC.life * 4 < NPC.lifeMax;

        // —— 音效 (复用现有资源) ——
        private static readonly SoundStyle RoarSound = new("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar");
        private static readonly SoundStyle SwipeSound = new("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1");
        private static readonly SoundStyle TossSound = new("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_2");

        // —— 精灵图 (静态缓存, 客户端惰性加载) ——
        private static Texture2D texIdle, texIdleFury, texRun, texRunFury;
        private static Texture2D texAtk1, texAtk1Fury, texAtk2, texAtk2Fury, texDie;

        // —— 专属黑风着色器 (静态缓存, 不注册 ACMShaders) ——
        private static Asset<Effect> _darkWindRef;
        private static Effect DarkWindFx {
            get {
                if (Main.dedServ)
                    return null;
                _darkWindRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BlackBearDarkWind", AssetRequestMode.ImmediateLoad);
                return _darkWindRef?.Value;
            }
        }

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear";
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1; // 全部自绘 (多张分离贴图)
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 220;
            NPC.height = 280;
            NPC.damage = 55;
            NPC.defense = 20;
            NPC.lifeMax = 8888;
            NPC.knockBackResist = 0f;
            NPC.value = Item.buyPrice(0, 10, 0, 0);
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.HitSound = SoundID.NPCHit1;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/BlackBear_Music");
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (spawnInfo.Player.ZoneJungle && spawnInfo.Player.ZoneOverworldHeight && Main.dayTime)
                return 0.005f;
            return 0f;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            State = BearState.Intro;
            StateTimer = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(furyTriggered);
            writer.Write(tempestUsed);
            writer.Write(allowRealDeath);
            writer.Write((sbyte)lastAttack);
            writer.Write((sbyte)prevAttack);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            furyTriggered = reader.ReadBoolean();
            tempestUsed = reader.ReadBoolean();
            allowRealDeath = reader.ReadBoolean();
            lastAttack = reader.ReadSByte();
            prevAttack = reader.ReadSByte();
        }

        public override void BossLoot(ref int potionType) => potionType = ItemID.LesserHealingPotion;

        public override void OnKill() => DownedBossSystem.downedBlackBear = true;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 5, 10));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SkyKey>(), 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearStaff>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearSword>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearBow>(), 10, 1));
        }

        // ============================================================
        //  AI 主循环
        // ============================================================

        public override void AI() {
            // 首帧捕获难度缩放后的接触伤害 (之后每帧默认清零, 仅激活帧恢复)
            if (baseContactDamage <= 0)
                baseContactDamage = Math.Max(NPC.damage, 1);

            UpdateVisualDecay();

            // 无敌规则由状态确定 (各端一致, 无需同步 flag)
            NPC.dontTakeDamage = State is BearState.Intro or BearState.Enrage or BearState.Dying;

            if (State == BearState.Dying) {
                RunDying();
                return;
            }

            if (NPC.target < 0 || NPC.target >= Main.maxPlayers || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                NPC.TargetClosest(false);
            Player player = Main.player[NPC.target];

            if (player.dead || !player.active) {
                RunPlayerGoneDrift();
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.timeLeft = 300;

            // 脱战渐隐后玩家复活 → 恢复不透明 (Intro 自管淡入)
            if (State != BearState.Intro && State != BearState.Dying && NPC.alpha > 0)
                NPC.alpha = Math.Max(0, NPC.alpha - 10);

            bool onGround = NPC.collideY && NPC.velocity.Y >= 0f;
            HandleLandingFeedback(onGround);

            // 伤害契约: 默认无接触伤害, 各招激活帧自行开启
            NPC.damage = 0;

            // —— 半血相变触发 (非演出状态时) ——
            if (Fury && !furyTriggered && State is not (BearState.Intro or BearState.Enrage or BearState.TempestHowl)) {
                ClearOwnProjectiles();
                EnterState(BearState.Enrage);
                furyTriggered = true;
                NPC.velocity.X = 0f;
                ACMScreenShakeSystem.Add(8f);
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                NPC.netUpdate = true;
            }

            // —— P2 金冠光环调度 (背景机制, 服务器) ——
            if (Fury && Main.netMode != NetmodeID.MultiplayerClient &&
                State is not (BearState.Intro or BearState.Enrage or BearState.TempestHowl or BearState.Dying)) {
                if (--haloTimer <= 0) {
                    haloTimer = 480;
                    SpawnHalo(player);
                }
            }

            switch (State) {
                case BearState.Intro: RunIntro(player, onGround); break;
                case BearState.Chase: RunChase(player, onGround); break;
                case BearState.Swipe: RunSwipe(player); break;
                case BearState.Toss: RunToss(player); break;
                case BearState.BearHug: RunBearHug(player); break;
                case BearState.Slam: RunSlam(player, onGround); break;
                case BearState.Pounce: RunPounce(player, onGround); break;
                case BearState.FuryRush: RunFuryRush(player); break;
                case BearState.HoneyRoar: RunHoneyRoar(player); break;
                case BearState.TempestHowl: RunTempestHowl(player); break;
                case BearState.Enrage: RunEnrage(player); break;
                default: EnterState(BearState.Chase); break;
            }

            // 重力 (Intro 空中悬停段自管; 跃扑/砸落用更重的下坠 → 巨兽的弧线)
            bool skipGravity = State == BearState.Intro && StateTimer < 48;
            if (!skipGravity && !(NPC.collideY && NPC.velocity.Y >= 0f)) {
                float grav = State == BearState.Pounce && StateParam == 1 ? 0.42f
                    : State == BearState.Intro ? 0.9f
                    : State == BearState.Slam && StateParam == 0 && StateTimer >= SlamChargeEnd ? 0.9f
                    : 0.3f;
                NPC.velocity.Y += grav;
            }
            if (NPC.velocity.Y > 30f)
                NPC.velocity.Y = 30f;

            PublishAtmosphere();
            StateTimer++;
        }

        private void UpdateVisualDecay() {
            slamFlash *= 0.90f;
            whiteFlash *= 0.93f;
            windPulse *= 0.94f;
            crownGlint *= 0.95f;
            leanVisual = MathHelper.Lerp(leanVisual, leanTarget, leanSnap);
            squashVisual = MathHelper.Lerp(squashVisual, squashTarget, 0.14f);
            // 默认回正 (状态每帧覆写目标)
            leanTarget = 0f;
            squashTarget = 1f;
            leanSnap = 0.08f;
        }

        /// <summary>落地反馈: 坠速转化为尘土/震屏/挤压 (质量=反作用)。</summary>
        private void HandleLandingFeedback(bool onGround) {
            if (wasAirborne && onGround && prevFallSpeed > 5f) {
                float power = MathHelper.Clamp(prevFallSpeed / 14f, 0.3f, 1.3f);
                squashVisual = 1f - 0.20f * power;
                ACMScreenShakeSystem.Add(3.5f * power);
                if (!Main.dedServ) {
                    for (int i = 0; i < (int)(10 * power); i++) {
                        Vector2 v = new(Main.rand.NextFloat(-4f, 4f) * power, -Main.rand.NextFloat(1f, 3.5f));
                        Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), 0), DustID.Dirt, v);
                        d.scale = 1.2f;
                    }
                }
            }
            wasAirborne = !onGround;
            prevFallSpeed = NPC.velocity.Y;
        }

        // ============================================================
        //  状态基础设施
        // ============================================================

        private void EnterState(BearState s) {
            State = s;
            StateTimer = 0;
            StateParam = 0;
            NPC.netUpdate = true;
        }

        private void EndAttack() {
            AttackCooldown = Fury ? 66 : 90;
            EnterState(BearState.Chase);
        }

        /// <summary>带滞回的面向: 玩家越过身后 40px 才转身, 避免贴脸抽搐 (转身 = 有重量的决定)。</summary>
        private void FaceTargetHeavy(Player player) {
            float dx = player.Center.X - NPC.Center.X;
            if (NPC.direction == 0)
                NPC.direction = dx > 0 ? 1 : -1;
            if (dx > 40f) NPC.direction = 1;
            else if (dx < -40f) NPC.direction = -1;
            NPC.spriteDirection = NPC.direction;
        }

        private void ClearOwnProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int[] mine = [
                ModContent.ProjectileType<BlackBear_Proj1>(), ModContent.ProjectileType<BlackBear_Proj2>(),
                ModContent.ProjectileType<BlackBear_Proj3>(), ModContent.ProjectileType<BlackBear_Proj4>(),
                ModContent.ProjectileType<BlackBearGroundShock>(), ModContent.ProjectileType<BlackBearHoneyDrip>()
            ];
            foreach (Projectile p in Main.ActiveProjectiles) {
                for (int t = 0; t < mine.Length; t++) {
                    if (p.type == mine[t]) { p.Kill(); break; }
                }
            }
        }

        /// <summary>找 NPC 正下方地面顶 Y (演出定位用)。</summary>
        private float GroundYBelow(float worldX, float worldY) {
            int tileX = (int)MathHelper.Clamp(worldX / 16f, 1, Main.maxTilesX - 2);
            int startY = (int)MathHelper.Clamp(worldY / 16f, 1, Main.maxTilesY - 2);
            for (int y = startY; y < Main.maxTilesY - 1; y++) {
                Tile t = Main.tile[tileX, y];
                if (t != null && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return worldY + 600f;
        }

        // ============================================================
        //  入场演出 — 黑风前兆 → 砸落 → 静止凝视 → 咆哮
        // ============================================================

        private void RunIntro(Player player, bool onGround) {
            NPC.damage = 0;

            if (StateTimer == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 挪到玩家侧上空 (真实坠落入场, 非瞬移刷脸)
                int side = Main.rand.NextBool() ? 1 : -1;
                NPC.position = player.Center + new Vector2(side * 380f, -560f) - NPC.Size / 2f;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }

            if (StateTimer < 48f) {
                // 黑风前兆: 悬空隐形 (各端由状态推导, 不依赖同步 alpha), 落点尘旋渐密 + 天幕压暗
                NPC.alpha = 255;
                NPC.velocity = Vector2.Zero;
                windPulse = Math.Max(windPulse, 0.35f);
                if (!Main.dedServ && StateTimer % 2 == 0) {
                    float gy = GroundYBelow(NPC.Center.X, player.Center.Y - 60f);
                    float prog = StateTimer / 48f;
                    Vector2 p = new(NPC.Center.X + Main.rand.NextFloat(-160f, 160f) * (1f - prog * 0.6f), gy - Main.rand.NextFloat(0f, 30f));
                    Dust d = Dust.NewDustPerfect(p, DustID.Smoke, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f) * (0.5f + prog)), 150, new Color(60, 45, 90), 1.6f);
                    d.noGravity = true;
                }
            }
            else if (StateParam == 0) {
                // 砸落: 现身 + 全力坠落 (重力 0.9 在主循环)
                NPC.alpha = Math.Max(0, NPC.alpha - 45);
                if (NPC.velocity.Y < 14f && StateTimer < 52f)
                    NPC.velocity.Y = 14f;
                FaceTargetHeavy(player);

                if (onGround && StateTimer > 50f) {
                    // 着陆冲击链: 一帧内 裂地波+尘暴+震屏+风脉冲
                    StateParam = 1;
                    StateTimer = 100;
                    NPC.velocity = Vector2.Zero;
                    squashVisual = 0.72f;
                    ACMScreenShakeSystem.Add(10f);
                    windPulse = 0.9f;
                    SoundEngine.PlaySound(SwipeSound with { Pitch = -0.4f }, NPC.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, Vector2.Zero,
                            ModContent.ProjectileType<BlackBear_Proj1>(), 0, 0f, Main.myPlayer, 1.2f);
                        NPC.netUpdate = true;
                    }
                    if (!Main.dedServ) {
                        for (int i = 0; i < 26; i++) {
                            float vx = Main.rand.NextFloat(3f, 9f) * (i % 2 == 0 ? 1 : -1);
                            Dust d = Dust.NewDustPerfect(NPC.Bottom, DustID.Dirt, new Vector2(vx, -Main.rand.NextFloat(2f, 6f)));
                            d.scale = 1.4f;
                        }
                    }
                }
                // 保底: 长时间没落地 (悬崖/深坑) 直接开战
                if (StateTimer > 96f && StateParam == 0) {
                    StateParam = 1;
                    StateTimer = 100;
                }
            }
            else if (StateTimer < 154f) {
                // 落地后凝视: 完全静止 (威压来自静止本身)
                NPC.velocity.X = 0f;
            }
            else if (StateTimer < 178f) {
                // 缓缓抬头
                NPC.velocity.X = 0f;
                leanTarget = -0.05f;
                leanSnap = 0.04f;
            }
            else {
                if (StateTimer == 178f) {
                    // 咆哮宣战
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                    slamFlash = 0.8f;
                    crownGlint = 1f;
                    windPulse = 0.5f;
                }
                if (StateTimer >= 200f) {
                    AttackCooldown = 30;
                    EnterState(BearState.Chase);
                }
            }
        }

        // ============================================================
        //  追击 (连接组织): 惯性起步/刹车 + 脚步反馈
        // ============================================================

        private void RunChase(Player player, bool onGround) {
            FaceTargetHeavy(player);

            // 惯性坡: 起步/刹车/转身都要时间 —— 重量在起停里
            float accel = onGround ? 0.55f : 0.25f;
            float targetVX = NPC.direction * RunSpeed;
            NPC.velocity.X += MathHelper.Clamp(targetVX - NPC.velocity.X, -accel, accel);

            // 脚步: 尘 + 距离衰减微震 + 交替闷响
            if (onGround && Math.Abs(NPC.velocity.X) > 5f && (int)StateTimer % 12 == 0) {
                if (!Main.dedServ) {
                    Vector2 foot = NPC.Bottom + new Vector2(NPC.direction * Main.rand.NextFloat(-30f, 60f), 0f);
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustPerfect(foot, DustID.Dirt, new Vector2(-NPC.direction * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(0.8f, 2.2f)));
                        d.scale = 1.1f;
                    }
                    float distFade = 1f - MathHelper.Clamp(Vector2.Distance(NPC.Center, Main.LocalPlayer.Center) / 900f, 0f, 1f);
                    if (distFade > 0.05f)
                        ACMScreenShakeSystem.Add(1.2f * distFade);
                    if ((int)StateTimer % 24 == 0)
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.22f, Pitch = -0.9f }, NPC.Bottom);
                }
            }

            // 身体前倾入速度
            leanTarget = MathHelper.Clamp(NPC.velocity.X * 0.008f, -0.09f, 0.09f);

            HandleTerrain(player, onGround);

            if (AttackCooldown > 0)
                AttackCooldown--;

            if (Main.netMode != NetmodeID.MultiplayerClient && AttackCooldown <= 0) {
                if (onGround)
                    ChooseAttack(player);
                else if (StateTimer > 240f)
                    EnterState(BearState.Toss); // 保底出口: 悬空过久也要出招
            }
        }

        /// <summary>平台下穿 / 卡墙与高台起跳 / 蜂蜜液体密度 — 地面 Boss 的地形合同。</summary>
        private void HandleTerrain(Player player, bool onGround) {
            int checkDistance = 2;
            int tileX = (int)((NPC.position.X + (NPC.direction == 1 ? NPC.width : 0)) / 16) + (NPC.direction == 1 ? checkDistance : -checkDistance);
            int tileY = (int)((NPC.position.Y + NPC.height) / 16);
            tileX = (int)MathHelper.Clamp(tileX, 1, Main.maxTilesX - 2);
            tileY = (int)MathHelper.Clamp(tileY, 1, Main.maxTilesY - 2);

            bool isOnPlatform = Main.tile[tileX, tileY] != null && Main.tile[tileX, tileY].HasTile &&
                Main.tile[tileX, tileY].TileType == TileID.Platforms;

            NPC.noTileCollide = false;
            if (Math.Abs(player.Center.Y - NPC.Center.Y) < 800 &&
                player.Center.Y > NPC.Center.Y + NPC.height / 2 && isOnPlatform) {
                NPC.velocity.Y += 0.1f;
                NPC.noTileCollide = true;
            }

            if (NPC.wet) {
                if (NPC.honeyWet) {
                    NPC.GravityMultiplier /= NPC.GravityWetMultipliers[LiquidID.Honey];
                    NPC.MaxFallSpeedMultiplier /= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey];
                }
                else if (!NPC.lavaWet && !NPC.shimmerWet) {
                    NPC.GravityMultiplier *= NPC.GravityWetMultipliers[LiquidID.Honey] / NPC.GravityWetMultipliers[LiquidID.Water];
                    NPC.MaxFallSpeedMultiplier *= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey] / NPC.MaxFallSpeedWetMultipliers[LiquidID.Water];
                }
            }

            if (onGround) {
                if (NPC.collideX)
                    NPC.velocity.Y = -11f;
                if (player.Center.Y < NPC.Center.Y - 80f && Math.Abs(player.Center.X - NPC.Center.X) < 220f)
                    NPC.velocity.Y = -12f - MathHelper.Clamp((NPC.Center.Y - player.Center.Y) / 30f, 0f, 4f);
                if (NPC.position == NPC.oldPosition)
                    NPC.velocity.Y = -10f;
            }
            if (NPC.velocity.Y < -10f)
                NPC.noTileCollide = true;
        }

        // ============================================================
        //  选招 (距离过滤 + 双重防重复; 仅服务器)
        // ============================================================

        private void ChooseAttack(Player player) {
            // 一场一次大招优先
            if (Fury && LowHP && !tempestUsed) {
                tempestUsed = true;
                EnterState(BearState.TempestHowl);
                return;
            }

            float dist = Vector2.Distance(NPC.Center, player.Center);
            Span<int> pool = stackalloc int[6];
            int n = 0;

            if (dist < 300f) {
                pool[n++] = (int)BearState.Swipe;
                pool[n++] = (int)BearState.BearHug;
                pool[n++] = (int)BearState.Pounce;
                if (Fury) pool[n++] = (int)BearState.FuryRush;
            }
            else if (dist < 800f) {
                pool[n++] = (int)BearState.BearHug;
                pool[n++] = (int)BearState.Toss;
                pool[n++] = (int)BearState.Pounce;
                pool[n++] = (int)BearState.Slam;
                if (Fury) { pool[n++] = (int)BearState.FuryRush; pool[n++] = (int)BearState.HoneyRoar; }
            }
            else {
                // 远距: 偏置拉近手段, 防止拉扯风筝
                pool[n++] = (int)BearState.Slam;
                pool[n++] = (int)BearState.Toss;
                pool[n++] = (int)BearState.Pounce;
                if (Fury) { pool[n++] = (int)BearState.FuryRush; pool[n++] = (int)BearState.HoneyRoar; }
            }

            // 防重复: 池够大时剔除上一招与上上招
            int pick = pool[Main.rand.Next(n)];
            for (int tries = 0; tries < 12 && n >= 2 && (pick == lastAttack || (n >= 3 && pick == prevAttack)); tries++)
                pick = pool[Main.rand.Next(n)];

            prevAttack = lastAttack;
            lastAttack = pick;
            EnterState((BearState)pick);
        }

        // ============================================================
        //  Swipe — 重踏挥击: 28f 后仰蓄力 → 6f 猛砸 → 20f 回正
        // ============================================================

        private void RunSwipe(Player player) {
            const int windup = 28, strikeEnd = 34, activeEnd = 42, total = 54;

            if (StateTimer < windup) {
                NPC.velocity.X *= 0.8f;
                FaceTargetHeavy(player);
                // 后仰蓄力 (anticipation): 慢速追赶 → 可读
                leanTarget = -NPC.direction * 0.11f;
                leanSnap = 0.06f;
                if (!Main.dedServ && (int)StateTimer % 4 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(NPC.direction * 70f, -8f), DustID.Dirt,
                        new Vector2(NPC.direction * 0.6f, -Main.rand.NextFloat(0.8f, 1.8f)));
                    d.scale = 1.15f;
                }
            }
            else if (StateTimer < strikeEnd) {
                // 猛砸: 高次幂追赶 → 一拍到位; 身体随之前冲
                leanTarget = NPC.direction * 0.14f;
                leanSnap = 0.55f;
                if (StateTimer == windup) {
                    SoundEngine.PlaySound(SwipeSound, NPC.Center);
                    ACMScreenShakeSystem.Add(5f);
                    NPC.velocity.X = NPC.direction * 7f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom + new Vector2(NPC.direction * 90f, 0f),
                            Vector2.Zero, ModContent.ProjectileType<BlackBear_Proj1>(), 0, 0f, Main.myPlayer, 0.8f);
                    }
                }
            }
            else {
                NPC.velocity.X *= 0.85f;
                leanTarget = NPC.direction * 0.05f;
            }

            // 伤害窗口与挥砍视觉严格对齐
            if (StateTimer >= windup && StateTimer <= activeEnd)
                NPC.damage = baseContactDamage;

            if (StateTimer >= total)
                EndAttack();
        }

        // ============================================================
        //  Toss — 蜜蜡投掷: 30f 上仰 → 1f 甩投 (反冲) → 收招
        // ============================================================

        private void RunToss(Player player) {
            const int windup = 30, total = 64;

            if (StateTimer < windup) {
                NPC.velocity.X *= 0.82f;
                FaceTargetHeavy(player);
                leanTarget = -NPC.direction * 0.09f;
                leanSnap = 0.06f;
                squashTarget = 1.04f; // 直立仰身
            }

            if (StateTimer == windup) {
                SoundEngine.PlaySound(TossSound, NPC.Center);
                ACMScreenShakeSystem.Add(3f);
                NPC.velocity.X = -NPC.direction * 3.5f; // 反冲: 掷出必有反作用
                leanTarget = NPC.direction * 0.10f;
                leanSnap = 0.5f;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = Fury ? 5 : 4;
                    int type = ModContent.ProjectileType<BlackBear_Proj2>();
                    Vector2 mouth = NPC.Center + new Vector2(NPC.direction * 60f, -70f);
                    for (int i = 0; i < count; i++) {
                        // 弹道解算: 均匀散布落点 + 略异步的滞空时间 → 弧线各不相同但都可预判
                        float targetX = player.Center.X + MathHelper.Lerp(-170f, 170f, count <= 1 ? 0.5f : i / (float)(count - 1)) + Main.rand.NextFloat(-25f, 25f);
                        float T = 42f + i * 4f;
                        float vx = (targetX - mouth.X) / T;
                        float vy = (player.Center.Y - mouth.Y) / T - 0.5f * 0.30f * T;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), mouth, new Vector2(vx, vy), type, 22, 0f, Main.myPlayer);
                    }
                }
            }

            if (StateTimer > windup)
                NPC.velocity.X *= 0.88f;

            if (StateTimer >= total)
                EndAttack();
        }

        // ============================================================
        //  BearHug — 熊抱冲撞: 50f 刨地后退蓄力 → 1f 点火直冲 → 硬刹
        // ============================================================

        private void RunBearHug(Player player) {
            const int brakeStart = 84, total = 110;

            if (StateTimer < LungeStart) {
                // 蓄力: 前 30f 允许转向, 之后锁死 (公平: 给出确定的躲避轴)
                if (StateTimer < 30f) {
                    FaceTargetHeavy(player);
                    StateParam = NPC.direction;
                }
                float dir = StateParam == 0 ? NPC.direction : StateParam;
                NPC.direction = NPC.spriteDirection = (int)dir;

                // 刨地后退 (counter-motion): 三次幂 → 前段几乎不动, 末段明显后吸
                float t = StateTimer / (float)LungeStart;
                NPC.velocity.X = -dir * t * t * t * 3.5f;
                leanTarget = -dir * 0.13f;
                leanSnap = 0.05f;

                // 后爪刨土向后喷
                if (!Main.dedServ && (int)StateTimer % 3 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(-dir * 60f, 0f), DustID.Dirt,
                        new Vector2(-dir * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1f, 3f)));
                    d.scale = 1.3f;
                }
                // 出发前 8f 憋气 (pre-silence)
                if (StateTimer > LungeStart - 8)
                    NPC.velocity.X *= 0.5f;
            }
            else if (StateTimer < brakeStart) {
                float dir = StateParam == 0 ? NPC.direction : StateParam;
                if (StateTimer == LungeStart) {
                    // 点火: 一帧 set 到位 + 声震同帧
                    NPC.velocity.X = dir * 22f;
                    SoundEngine.PlaySound(SwipeSound with { Pitch = -0.15f }, NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                    squashVisual = 1.14f; // 横向蹿出的拉伸
                    if (!Main.dedServ) {
                        for (int i = 0; i < 12; i++) {
                            Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(-dir * 40f, 0f), DustID.Smoke,
                                new Vector2(-dir * Main.rand.NextFloat(2f, 7f), -Main.rand.NextFloat(0.5f, 2.5f)), 120, new Color(70, 55, 100), 1.5f);
                            d.noGravity = true;
                        }
                    }
                }
                NPC.velocity.X *= 1.012f; // 复利微加速: 冲刺持续升级
                leanTarget = dir * 0.09f;

                // 早退出: 撞墙 (自伤反馈) / 冲过头 → 立即转入刹车
                bool passed = (dir > 0 && NPC.Center.X > player.Center.X + 240f) || (dir < 0 && NPC.Center.X < player.Center.X - 240f);
                if (NPC.collideX || passed) {
                    if (NPC.collideX) {
                        ACMScreenShakeSystem.Add(7f);
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.6f }, NPC.Center);
                        squashVisual = 0.78f;
                    }
                    StateTimer = brakeStart;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 硬刹: ×0.72/f — "撞进位置"的止动感
                NPC.velocity.X *= 0.72f;
                if (!Main.dedServ && Math.Abs(NPC.velocity.X) > 4f && (int)StateTimer % 2 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Bottom, DustID.Smoke,
                        new Vector2(NPC.velocity.X * 0.15f, -Main.rand.NextFloat(0.5f, 1.5f)), 130, new Color(80, 70, 60), 1.2f);
                    d.noGravity = true;
                }
            }

            // 伤害窗口 = 实际高速时段 (含刹车初段的惯性滑撞)
            if (StateTimer >= LungeStart && Math.Abs(NPC.velocity.X) > 12f)
                NPC.damage = baseContactDamage;

            if (StateTimer >= total)
                EndAttack();
        }

        // ============================================================
        //  Slam — 立地震地: 108f 站桩蓄力 (输出窗口) → 小跳砸地 → 双向岩浪
        // ============================================================

        private void RunSlam(Player player, bool onGround) {
            const int impactAt = 160, total = 186;

            if (StateTimer < 20f)
                FaceTargetHeavy(player);

            if (StateTimer < SlamChargeEnd) {
                // 蓄力: 站桩 = 给远程的公平输出窗口
                NPC.velocity.X *= 0.5f;
                float prog = StateTimer / (float)SlamChargeEnd;
                squashTarget = 1f - 0.06f * prog; // 下沉蓄势
                ACMScreenShakeSystem.Add(prog * prog * 2f); // 低鸣渐强 (取 max 不累加)

                // 汇聚尘: 密度 ∝ √t, 72% 后静默 (爆发前的吸气)
                if (!Main.dedServ && prog < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(prog) * 0.7f) {
                    Vector2 from = NPC.Bottom + new Vector2(Main.rand.NextFloat(-260f, 260f), Main.rand.NextFloat(-30f, 0f));
                    Dust d = Dust.NewDustPerfect(from, DustID.Torch, (NPC.Bottom - from) * 0.05f, 100, Color.OrangeRed, 1.2f);
                    d.noGravity = true;
                }
            }
            else if (StateParam == 0) {
                if (StateTimer == SlamChargeEnd) {
                    // 小跳: 把体重抬起来再砸下去 (重坠重力 0.9 在主循环)
                    NPC.velocity.Y = -11f;
                    NPC.velocity.X = NPC.direction * 1.5f;
                    squashVisual = 1.10f;
                    SoundEngine.PlaySound(SwipeSound with { Pitch = 0.1f }, NPC.Center);
                }
                // 落地 (或超时保底) → 冲击
                if ((onGround && StateTimer > SlamChargeEnd + 4) || StateTimer > impactAt - 4) {
                    StateParam = 1;
                    StateTimer = impactAt;
                    SlamImpact();
                }
            }
            else {
                NPC.velocity.X *= 0.8f;
            }

            if (StateTimer >= total)
                EndAttack();
        }

        private void SlamImpact() {
            slamFlash = 1f;
            squashVisual = 0.74f;
            ACMScreenShakeSystem.Add(9f);
            SoundEngine.PlaySound(SwipeSound with { Pitch = -0.3f }, NPC.Center);
            SoundEngine.PlaySound(RoarSound, NPC.Center);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int type = ModContent.ProjectileType<BlackBearGroundShock>();
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, new Vector2(dir * 8.5f, 0f), type, 34, 6f, Main.myPlayer, dir);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, new Vector2(dir * 13f, 0f), type, 34, 6f, Main.myPlayer, dir);
                }
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, Vector2.Zero,
                    ModContent.ProjectileType<BlackBear_Proj1>(), 0, 0f, Main.myPlayer, 1.15f);
                NPC.netUpdate = true;
            }
        }

        // ============================================================
        //  Pounce — 跃扑: 30f 蹲伏 → 弹道跳预测点 → 落地硬直 (可惩罚)
        // ============================================================

        private void RunPounce(Player player, bool onGround) {
            const int leapAt = 30, landLagAt = 120, total = 150;

            if (StateParam == 0) {
                // 蹲伏 (剪影预警): 压低 + 聚土
                NPC.velocity.X *= 0.75f;
                FaceTargetHeavy(player);
                float t = MathHelper.Clamp(StateTimer / (float)leapAt, 0f, 1f);
                squashTarget = 1f - 0.15f * t;
                if (!Main.dedServ && (int)StateTimer % 3 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(Main.rand.NextFloat(-70f, 70f), 0f), DustID.Dirt,
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)));
                    d.scale = 1.1f;
                }

                if (StateTimer >= leapAt) {
                    // 起跳: 只取此刻的预测点, 空中不再追踪 (公平: 位移即解)
                    StateParam = 1;
                    Vector2 predicted = player.Center + player.velocity * 12f;
                    float dx = predicted.X - NPC.Center.X;
                    float dy = predicted.Y - NPC.Center.Y;
                    float T = MathHelper.Clamp(MathF.Abs(dx) / 14f, 26f, 46f);
                    float vx = dx / T;
                    float vy = dy / T - 0.5f * 0.42f * T;
                    NPC.velocity = new Vector2(vx, MathHelper.Clamp(vy, -21f, 2f));
                    NPC.direction = NPC.spriteDirection = dx > 0 ? 1 : -1;
                    squashVisual = 1.16f;
                    ACMScreenShakeSystem.Add(3f);
                    SoundEngine.PlaySound(SwipeSound with { Pitch = 0.2f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (StateParam == 1) {
                // 空中: 上升穿台, 下坠着地; 伤害在腾空 6f 后开启 (防贴脸秒)
                NPC.noTileCollide = NPC.velocity.Y < 0f;
                leanTarget = NPC.direction * MathHelper.Clamp(NPC.velocity.Y * 0.012f + 0.10f, -0.12f, 0.18f);
                if (StateTimer > leapAt + 6)
                    NPC.damage = baseContactDamage;

                if ((onGround && StateTimer > leapAt + 8) || StateTimer >= landLagAt) {
                    // 落地 (或超时保底): 冲击 + 30f 硬直 = 惩罚窗口
                    StateParam = 2;
                    StateTimer = landLagAt;
                    NPC.velocity.X *= 0.2f;
                    squashVisual = 0.72f;
                    ACMScreenShakeSystem.Add(6f);
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.7f }, NPC.Bottom);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, Vector2.Zero,
                            ModContent.ProjectileType<BlackBear_Proj1>(), 0, 0f, Main.myPlayer, 0.55f);
                        NPC.netUpdate = true;
                    }
                }
            }
            else {
                // 落地硬直: 无伤害, 站桩喘息
                NPC.velocity.X *= 0.7f;
            }

            if (StateTimer >= total)
                EndAttack();
        }

        // ============================================================
        //  FuryRush — P2 黑风连环冲: (24f 转身蓄力 → 14f 冲刺 → 甩风刃) ×3 → 40f 大喘气
        // ============================================================

        private void RunFuryRush(Player player) {
            const int windup = 24, dashEnd = 38, segTotal = 40, pantTotal = 44;
            int segment = (int)StateParam;

            if (segment >= 3) {
                // 大喘气: 三连冲后的可惩罚窗口
                NPC.velocity.X *= 0.78f;
                squashTarget = 1f - 0.04f * MathF.Sin((float)StateTimer * 0.22f); // 喘息起伏
                if (!Main.dedServ && (int)StateTimer % 6 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(NPC.direction * 80f, -40f), DustID.Smoke,
                        new Vector2(NPC.direction * 1.2f, -0.6f), 160, new Color(120, 110, 130), 1.1f);
                    d.noGravity = true;
                }
                if (StateTimer >= pantTotal)
                    EndAttack();
                return;
            }

            if (StateTimer < windup) {
                // 每段重新瞄准 + 重新画红线 (读方向的公平阀门)
                NPC.velocity.X *= 0.8f;
                FaceTargetHeavy(player);
                leanTarget = -NPC.direction * 0.10f;
                leanSnap = 0.07f;
                if (!Main.dedServ && (int)StateTimer % 2 == 0) {
                    Vector2 from = NPC.Center + new Vector2(-NPC.direction * Main.rand.NextFloat(60f, 200f), Main.rand.NextFloat(-60f, 60f));
                    Dust d = Dust.NewDustPerfect(from, DustID.Smoke, (NPC.Center - from) * 0.06f, 140, new Color(60, 45, 90), 1.3f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer < dashEnd) {
                if (StateTimer == windup) {
                    NPC.velocity.X = NPC.direction * 26f;
                    ACMScreenShakeSystem.Add(5f);
                    SoundEngine.PlaySound(SwipeSound with { Pitch = -0.1f + segment * 0.12f }, NPC.Center);
                    squashVisual = 1.13f;
                }
                NPC.velocity.X *= 1.008f;
                leanTarget = NPC.direction * 0.09f;
                NPC.damage = baseContactDamage;
            }
            else {
                // 段收: 甩出黑风爪痕, 递进下一段
                if (StateTimer == dashEnd) {
                    NPC.velocity.X *= 0.5f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int type = ModContent.ProjectileType<BlackBear_Proj4>();
                        float dir = NPC.direction;
                        // 两道风刃向后上方甩出, 弧线扣向冲刺走廊 (惩罚贴身尾随)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, -50f),
                            new Vector2(-dir * 4.5f, -7.5f), type, 26, 0f, Main.myPlayer, dir * 0.018f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, -30f),
                            new Vector2(-dir * 2f, -9f), type, 26, 0f, Main.myPlayer, dir * 0.026f);
                    }
                }
                if (StateTimer >= segTotal) {
                    StateParam = segment + 1;
                    StateTimer = 0;
                    NPC.netUpdate = true;
                }
            }
        }

        // ============================================================
        //  HoneyRoar — P2 蜜雨咆哮: 36f 仰天蓄力 → 两波蜜雨 (各自地影预警) → 收招
        // ============================================================

        private void RunHoneyRoar(Player player) {
            const int roarAt = 36, wave2At = 66, holdEnd = 100, total = 140;

            NPC.velocity.X *= 0.8f;

            if (StateTimer < roarAt) {
                FaceTargetHeavy(player);
                leanTarget = -NPC.direction * 0.12f;
                leanSnap = 0.05f;
                squashTarget = 1.06f; // 仰天挺立
                if (!Main.dedServ && (int)StateTimer % 4 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Top + new Vector2(Main.rand.NextFloat(-40f, 40f), -10f), DustID.Honey,
                        new Vector2(0f, -Main.rand.NextFloat(1f, 2.5f)), 80, default, 1.2f);
                    d.noGravity = true;
                }
            }

            if (StateTimer == roarAt || StateTimer == wave2At) {
                if (StateTimer == roarAt) {
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ACMScreenShakeSystem.Add(5f);
                    crownGlint = 0.8f;
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<BlackBearHoneyDrip>();
                    int drops = 5;
                    for (int i = 0; i < drops; i++) {
                        float tx = player.Center.X + MathHelper.Lerp(-360f, 360f, i / (float)(drops - 1)) + Main.rand.NextFloat(-45f, 45f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(tx, player.Center.Y - 600f),
                            new Vector2(0f, 0.1f), type, 26, 0f, Main.myPlayer);
                    }
                }
            }

            if (StateTimer > roarAt && StateTimer < holdEnd) {
                // 咆哮保持: 站桩 (输出窗口), 身体微颤
                leanTarget = -NPC.direction * (0.10f + 0.02f * MathF.Sin((float)StateTimer * 0.5f));
                leanSnap = 0.3f;
            }

            if (StateTimer >= total)
                EndAttack();
        }

        // ============================================================
        //  TempestHowl — 一场一次大招: 90f 黑风汇聚 → 12f 全静默 → 环形风暴 → 90f 疲劳
        // ============================================================

        private void RunTempestHowl(Player player) {
            const int chargeEnd = 90, releaseAt = 102, ring2At = 126, fatigueEnd = 192;

            NPC.velocity.X *= 0.6f;

            if (StateTimer < chargeEnd) {
                // 蓄力: 屏幕渐暗即全局预警 (uIntensity ∝ prog)
                FaceTargetHeavy(player);
                float prog = StateTimer / (float)chargeEnd;
                squashTarget = 1f - 0.08f * prog;
                windPulse = Math.Max(windPulse, 0.4f + 0.6f * prog);
                ACMScreenShakeSystem.Add(prog * prog * 3f);

                // 黑风汇聚: 径向抽入 + 切向旋绕两族粒子
                if (!Main.dedServ && Main.rand.NextFloat() < 0.35f + prog * 0.5f) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(340f, 300f);
                    Vector2 pull = (NPC.Center - from) * 0.055f;
                    if (Main.rand.NextBool())
                        pull = pull.RotatedBy(MathHelper.PiOver2 * 0.6f); // 切向族: 汇聚有旋涡感
                    Dust d = Dust.NewDustPerfect(from, DustID.Smoke, pull, 130, new Color(55, 40, 90), 1.5f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer < releaseAt) {
                // 全静默 12f: 无尘无震 — 尖啸前的吸气
                NPC.velocity.X = 0f;
                squashTarget = 0.90f;
            }
            else if (StateTimer < fatigueEnd) {
                if (StateTimer == releaseAt) {
                    // 爆发: 环形风暴刃 (留缺口) + 双向岩浪 + 满级风脉冲
                    ACMScreenShakeSystem.Add(12f);
                    SoundEngine.PlaySound(RoarSound with { Pitch = -0.2f }, NPC.Center);
                    windPulse = 1f;
                    slamFlash = 1f;
                    squashVisual = 1.12f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        StateParam = Main.rand.Next(12); // 缺口槽位 (同步给各端画风刃)
                        SpawnHowlRing((int)StateParam, 7.5f);
                        int gsType = ModContent.ProjectileType<BlackBearGroundShock>();
                        for (int dir = -1; dir <= 1; dir += 2)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, new Vector2(dir * 10f, 0f), gsType, 34, 6f, Main.myPlayer, dir);
                        NPC.netUpdate = true;
                    }
                }
                if (StateTimer == ring2At && Main.netMode != NetmodeID.MultiplayerClient) {
                    // 第二环: 缺口对侧偏移 6 槽 — 玩家沿缝穿行
                    SpawnHowlRing(((int)StateParam + 6) % 12, 9f);
                }
                // 疲劳: 深喘 (被打的奖励窗口)
                if (StateTimer > ring2At) {
                    squashTarget = 1f - 0.05f * MathF.Sin((float)StateTimer * 0.18f);
                    if (!Main.dedServ && (int)StateTimer % 8 == 0) {
                        Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(NPC.direction * 90f, -30f), DustID.Smoke,
                            new Vector2(NPC.direction * 1f, -0.5f), 170, new Color(130, 120, 140), 1.2f);
                        d.noGravity = true;
                    }
                }
            }
            else {
                AttackCooldown = 40;
                EnterState(BearState.Chase);
            }
        }

        /// <summary>环形风暴刃: 12 槽跳过缺口槽及其相邻槽 (固定安全缝)。</summary>
        private void SpawnHowlRing(int gapSlot, float speed) {
            int type = ModContent.ProjectileType<BlackBear_Proj4>();
            Vector2 origin = NPC.Center + new Vector2(0, -40f);
            for (int i = 0; i < 12; i++) {
                if (i == gapSlot || i == (gapSlot + 1) % 12)
                    continue;
                float ang = MathHelper.TwoPi * i / 12f;
                Vector2 vel = ang.ToRotationVector2() * speed;
                float curve = (i % 2 == 0 ? 1f : -1f) * 0.004f; // 交替微弯: 风暴感而非弹幕墙
                Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, vel, type, 26, 0f, Main.myPlayer, curve);
            }
        }

        // ============================================================
        //  Enrage — 半血相变: 跪伏汇聚 → 起身怒嚎 (全程 i-frame, 已清弹)
        // ============================================================

        private void RunEnrage(Player player) {
            const int riseAt = 50, total = 90;

            NPC.velocity.X *= 0.6f;
            NPC.damage = 0;

            if (StateTimer < riseAt) {
                // 跪伏: 黑风从四周涌入体内
                FaceTargetHeavy(player);
                float t = StateTimer / (float)riseAt;
                squashTarget = 1f - 0.10f * t;
                leanTarget = NPC.direction * 0.06f;
                windPulse = Math.Max(windPulse, 0.3f + 0.4f * t);

                if (!Main.dedServ && t < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(t)) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(300f, 260f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Smoke, (NPC.Center - from) * 0.06f, 140, new Color(55, 40, 90), 1.4f);
                    d.noGravity = true;
                }
            }
            else {
                if (StateTimer == riseAt) {
                    // 起身怒嚎: 黑风漫天从此常驻
                    ACMScreenShakeSystem.Add(10f);
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    windPulse = 1f;
                    slamFlash = 1f;
                    crownGlint = 1f;
                    squashVisual = 1.12f;
                    if (!Main.dedServ) {
                        for (int i = 0; i < 22; i++) {
                            Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Smoke,
                                Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 10f), 120, new Color(70, 50, 110), 1.6f);
                            d.noGravity = true;
                        }
                    }
                }
                squashTarget = 1.05f;
                leanTarget = -NPC.direction * 0.08f;
            }

            if (StateTimer >= total) {
                AttackCooldown = 45; // 相变后首招延迟 (防 telefrag)
                haloTimer = 120;
                EnterState(BearState.Chase);
            }
        }

        // ============================================================
        //  P2 金冠光环 (复发背景机制)
        // ============================================================

        private void SpawnHalo(Player player) {
            int type = ModContent.ProjectileType<BlackBear_Proj3>();
            Vector2 head = NPC.Center + new Vector2(0f, -NPC.height * 0.45f);
            const int orbs = 6;
            for (int i = 0; i < orbs; i++) {
                float ang = MathHelper.TwoPi * i / orbs;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), head, Vector2.Zero, type, 24, 0f, Main.myPlayer, ang, 0f, NPC.target);
            }
            crownGlint = 1f;
        }

        // ============================================================
        //  Dying — 收服演出: 踉跄 → 金缚 → 静默 → 金光爆发 → 消散
        // ============================================================

        private void RunDying() {
            const int bindAt = 60, silenceAt = 120, burstAt = 132, finishAt = 200;

            NPC.damage = 0;
            NPC.dontTakeDamage = true;
            NPC.noTileCollide = false;

            // 空中被击杀时先落回地面 (爆发前保持普通重力)
            if (StateTimer < burstAt && !(NPC.collideY && NPC.velocity.Y >= 0f)) {
                NPC.velocity.Y += 0.5f;
                if (NPC.velocity.Y > 18f)
                    NPC.velocity.Y = 18f;
            }

            if (StateTimer < bindAt) {
                // 踉跄: 交错后退小步 + 妖气丝丝逸散
                int step = (int)StateTimer % 12;
                NPC.velocity.X = step < 6 ? -NPC.direction * 2.2f : NPC.velocity.X * 0.8f;
                if (step == 0) {
                    ACMScreenShakeSystem.Add(2f);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 4; i++) {
                            Dust d = Dust.NewDustPerfect(NPC.Bottom + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f), DustID.Dirt,
                                new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2f)));
                            d.scale = 1.1f;
                        }
                    }
                }
                if (!Main.dedServ && (int)StateTimer % 3 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.4f, NPC.height * 0.4f),
                        DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.5f)), 150, new Color(55, 40, 90), 1.3f);
                    d.noGravity = true;
                }
                goldDraw = MathHelper.Lerp(goldDraw, 0.22f, 0.03f);
            }
            else if (StateTimer < silenceAt) {
                // 金缚: 三道金光带缠身 (PreDraw 绘制), 挣扎抖动渐弱
                NPC.velocity.X = 0f;
                float t = (StateTimer - bindAt) / (float)(silenceAt - bindAt);
                leanTarget = MathF.Sin((float)StateTimer * 0.7f) * 0.05f * (1f - t);
                leanSnap = 0.4f;
                if (StateTimer == bindAt)
                    SoundEngine.PlaySound(RoarSound with { Pitch = 0.3f, Volume = 0.9f }, NPC.Center);
                goldDraw = MathHelper.Lerp(goldDraw, 0.25f + 0.4f * t, 0.05f);
                crownGlint = Math.Max(crownGlint, t);
            }
            else if (StateTimer < burstAt) {
                // 12f 全静默: 光带定格, 万籁俱寂
                NPC.velocity.X = 0f;
                leanTarget = 0f;
            }
            else if (StateTimer < finishAt) {
                if (StateTimer == burstAt) {
                    // 金光爆发: 一场唯一的最大节拍
                    ACMScreenShakeSystem.Add(14f);
                    SoundEngine.PlaySound(RoarSound with { Pitch = 0.45f }, NPC.Center);
                    whiteFlash = 1f;
                    slamFlash = 1f;
                    goldDraw = 1f;
                    if (!Main.dedServ) {
                        for (int i = 0; i < 30; i++) {
                            Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GoldFlame,
                                Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 12f), 100, default, 1.6f);
                            d.noGravity = true;
                        }
                    }
                }
                // 消散: 身体上浮 + 金尘升天
                NPC.velocity = new Vector2(0f, -0.55f);
                NPC.alpha = Math.Min(255, NPC.alpha + 4);
                goldDraw = MathHelper.Lerp(goldDraw, 0.5f, 0.02f);
                if (!Main.dedServ && (int)StateTimer % 2 == 0) {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.5f, NPC.height * 0.5f),
                        DustID.GoldFlame, new Vector2(0f, -Main.rand.NextFloat(1.5f, 4f)), 80, default, 1.3f);
                    d.noGravity = true;
                }
            }
            else if (Main.netMode != NetmodeID.MultiplayerClient) {
                // 真实死亡结算: 走标准 checkDead → OnKill (downed 标记) + 掉落
                allowRealDeath = true;
                NPC.dontTakeDamage = false;
                NPC.life = 0;
                NPC.checkDead();
            }

            PublishAtmosphere();
            StateTimer++;
        }

        public override bool CheckDead() {
            if (!allowRealDeath) {
                // 拦截死亡 → 收服演出
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (State != BearState.Dying) {
                    ClearOwnProjectiles();
                    EnterState(BearState.Dying);
                }
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        // ============================================================
        //  玩家离场 / 脱战 — 渐隐离场, 不掉落 (修复 V2 白送掉落 bug)
        // ============================================================

        private void RunPlayerGoneDrift() {
            NPC.velocity.X *= 0.9f;
            NPC.noTileCollide = false;
            NPC.EncourageDespawn(90);
            if (NPC.timeLeft < 60)
                NPC.alpha = Math.Min(255, NPC.alpha + 5);
            PublishAtmosphere(); // 黑风随离场平滑退潮
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            // 冲刺中略减伤: 鼓励躲招后打硬直/喘气窗口
            bool dashing = (State == BearState.BearHug && StateTimer >= LungeStart) ||
                           (State == BearState.FuryRush && StateParam < 3);
            if (dashing)
                modifiers.FinalDamage *= 0.85f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.35f, NPC.height * 0.35f),
                    DustID.Smoke, new Vector2(hit.HitDirection * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f)), 120, new Color(50, 40, 70), 1.2f);
                d.noGravity = true;
            }
        }

        // ============================================================
        //  氛围发布 (Sky / 全屏黑风) — 每帧目标值, 平滑在绘制端
        // ============================================================

        private void PublishAtmosphere() {
            if (Main.dedServ)
                return;

            // 常驻底: P2 黑风漫天; 演出脉冲叠加; 收服演出中黑风退潮、金光接管
            float baseWind = furyTriggered ? 0.30f : 0f;
            float stormTarget = State == BearState.Dying ? 0f : MathHelper.Clamp(baseWind + windPulse * 0.7f, 0f, 1f);
            float goldTarget = State == BearState.Dying ? goldDraw : crownGlint * 0.15f;

            windDraw = MathHelper.Lerp(windDraw, stormTarget, stormTarget > windDraw ? 0.10f : 0.04f);

            BlackBearSky.PublishStorm(stormTarget, goldTarget);
        }

        // ============================================================
        //  绘制 — 帧映射 / 重量姿态 / 预警 / 全屏黑风
        // ============================================================

        private static void EnsureTextures() {
            if (texIdle != null)
                return;
            static Texture2D Load(string name) =>
                ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/" + name, AssetRequestMode.ImmediateLoad).Value;
            texIdle = Load("idle_344");
            texIdleFury = Load("idle_344_1");
            texRun = Load("run_332");
            texRunFury = Load("run_332_1");
            texAtk1 = Load("attack_328_1");
            texAtk1Fury = Load("attack_328_1_1");
            texAtk2 = Load("attack_328_2");
            texAtk2Fury = Load("attack_328_2_2");
            texDie = Load("die_328");
        }

        /// <summary>按状态与进度精确映射帧号 (frame-by-progress: 动画永远与判定对齐)。</summary>
        private void SelectFrame(out Texture2D tex, out int frame, out int frameHeight, out int totalFrames) {
            bool fury = Fury;
            Texture2D atk1 = fury ? texAtk1Fury : texAtk1;
            Texture2D atk2 = fury ? texAtk2Fury : texAtk2;
            Texture2D idle = fury ? texIdleFury : texIdle;
            Texture2D run = fury ? texRunFury : texRun;
            float t = StateTimer;

            switch (State) {
                case BearState.Swipe:
                    tex = atk1; frameHeight = 328; totalFrames = 10;
                    frame = t < 28f ? (int)(t / 28f * 4f)                       // 蓄力 0..3
                        : t < 34f ? 4 + (int)((t - 28f) / 2f)                   // 猛砸 4..6 (快)
                        : 7 + Math.Min(2, (int)((t - 34f) / 7f));               // 回正 7..9
                    break;
                case BearState.BearHug:
                    tex = atk1; frameHeight = 328; totalFrames = 10;
                    frame = t < LungeStart ? (int)(t / LungeStart * 5f)          // 张臂蓄力 0..4
                        : t < 84f ? 5 + ((int)t / 4) % 2                        // 冲刺 5/6 交替
                        : 7 + Math.Min(2, (int)((t - 84f) / 9f));               // 刹车 7..9
                    break;
                case BearState.FuryRush: {
                    tex = atk1; frameHeight = 328; totalFrames = 10;
                    if (StateParam >= 3) { tex = idle; frameHeight = 344; totalFrames = 4; frame = ((int)t / 14) % 2; }
                    else frame = t < 24f ? (int)(t / 24f * 4f) : 5 + ((int)t / 3) % 2;
                    break;
                }
                case BearState.Toss:
                    tex = atk2; frameHeight = 328; totalFrames = 10;
                    frame = t < 30f ? (int)(t / 30f * 5f) : 5 + Math.Min(4, (int)((t - 30f) / 7f));
                    break;
                case BearState.Slam:
                    tex = atk2; frameHeight = 328; totalFrames = 10;
                    frame = t < SlamChargeEnd ? (int)(t / SlamChargeEnd * 4f)   // 深蓄 0..3
                        : StateParam == 0 ? 4 + ((int)t / 4) % 2                // 腾空 4/5
                        : 6 + Math.Min(3, (int)((t - 160f) / 7f));              // 砸地→收 6..9
                    break;
                case BearState.HoneyRoar:
                    tex = atk2; frameHeight = 328; totalFrames = 10;
                    frame = t < 36f ? (int)(t / 36f * 5f)
                        : t < 100f ? 5 + ((int)t / 5) % 2                       // 咆哮保持 5/6
                        : 7 + Math.Min(2, (int)((t - 100f) / 14f));
                    break;
                case BearState.TempestHowl:
                    tex = atk2; frameHeight = 328; totalFrames = 10;
                    frame = t < 90f ? (int)(t / 90f * 4f)
                        : t < 102f ? 3                                          // 静默定格
                        : t < 126f ? 5 + ((int)t / 4) % 2                       // 爆发咆哮
                        : ((int)t / 12) % 2;                                    // 疲劳喘 0/1
                    if (t >= 126f) { tex = idle; frameHeight = 344; totalFrames = 4; }
                    break;
                case BearState.Enrage:
                    tex = atk2; frameHeight = 328; totalFrames = 10;
                    frame = t < 50f ? (int)(t / 50f * 3f) : 5 + ((int)t / 4) % 2;
                    break;
                case BearState.Pounce:
                    if (StateParam == 1) { tex = run; frameHeight = 332; totalFrames = 6; frame = 3; } // 空中定格
                    else { tex = idle; frameHeight = 344; totalFrames = 4; frame = 0; }
                    break;
                case BearState.Chase:
                    tex = run; frameHeight = 332; totalFrames = 6;
                    frame = ((int)t / 7) % 6;
                    break;
                case BearState.Dying:
                    tex = texDie; frameHeight = 328; totalFrames = 6;
                    frame = t < 60f ? (int)(t / 24f)                            // 踉跄 0..2
                        : t < 120f ? 3 : t < 132f ? 4 : 5;                      // 金缚 3 / 静默 4 / 消散 5
                    break;
                case BearState.Intro:
                    if (t >= 48f && StateParam == 0) { tex = run; frameHeight = 332; totalFrames = 6; frame = 3; } // 坠落
                    else if (t >= 154f && t < 178f) { tex = idle; frameHeight = 344; totalFrames = 4; frame = Math.Min(3, (int)((t - 154f) / 8f)); }
                    else if (t >= 178f) { tex = atk2; frameHeight = 328; totalFrames = 10; frame = 5 + ((int)t / 4) % 2; } // 咆哮
                    else { tex = idle; frameHeight = 344; totalFrames = 4; frame = 0; }
                    break;
                default:
                    tex = idle; frameHeight = 344; totalFrames = 4;
                    frame = ((int)Main.GameUpdateCount / 12) % 4;
                    break;
            }
            frame = Math.Clamp(frame, 0, totalFrames - 1);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;
            EnsureTextures();

            DrawTelegraphs(spriteBatch);

            SelectFrame(out Texture2D texture, out int frame, out int frameHeight, out int totalFrames);
            Rectangle src = new(0, frame * frameHeight, texture.Width, frameHeight);
            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new(texture.Width / 2f, frameHeight); // 脚底锚点: 倾斜/挤压绕脚转
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 挤压保体积: Y 压则 X 涨
            Vector2 scale = new(2f - squashVisual, squashVisual);
            scale.X = MathHelper.Clamp(scale.X, 0.8f, 1.25f);

            // 高速残影 (冲撞/连环冲/跃扑, 速度门控)
            if (Math.Abs(NPC.velocity.X) > 14f || (State == BearState.Pounce && StateParam == 1)) {
                for (int i = 1; i < NPC.oldPos.Length; i += 1) {
                    float fade = 1f - i / (float)NPC.oldPos.Length;
                    Vector2 old = NPC.oldPos[i] + new Vector2(NPC.width / 2f, NPC.height) - screenPos;
                    Color ghost = new Color(60, 45, 100) * (0.32f * fade * NPC.Opacity);
                    ghost.A = 0;
                    spriteBatch.Draw(texture, old, src, ghost, leanVisual, origin, scale, effects, 0f);
                }
            }

            // 狂怒描边 (暖橙脉冲)
            if (Fury && State != BearState.Dying) {
                float pulse = 0.35f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
                Color glow = TelegraphColors.Flame * (pulse * NPC.Opacity);
                glow.A = 0;
                for (int i = 0; i < 4; i++) {
                    Vector2 off = (MathHelper.PiOver2 * i).ToRotationVector2() * 3f;
                    spriteBatch.Draw(texture, drawPos + off, src, glow, leanVisual, origin, scale, effects, 0f);
                }
            }

            spriteBatch.Draw(texture, drawPos, src, drawColor * NPC.Opacity, leanVisual, origin, scale, effects, 0f);

            // 金冠闪 (头顶亮点) + 死亡金缚白闪
            if (crownGlint > 0.03f && ACMAsset.SoftGlow != null) {
                Color c = TelegraphColors.Gold * crownGlint;
                c.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, drawPos - new Vector2(-NPC.direction * 14f, frameHeight * squashVisual - 18f), null,
                    c, 0f, ACMAsset.SoftGlow.Size() / 2f, 0.6f * crownGlint + 0.2f, SpriteEffects.None, 0f);
            }
            if (whiteFlash > 0.03f) {
                Color flash = new Color(255, 240, 190) * (whiteFlash * NPC.Opacity);
                flash.A = 0;
                spriteBatch.Draw(texture, drawPos, src, flash, leanVisual, origin, scale, effects, 0f);
            }

            return false;
        }

        private void DrawTelegraphs(SpriteBatch sb) {
            // 熊抱冲撞 / 连环冲: 红色冲撞线 (蓄力渐强, 每段重画)
            bool hugTell = State == BearState.BearHug && StateTimer < LungeStart && StateTimer > 6f;
            bool rushTell = State == BearState.FuryRush && StateParam < 3 && StateTimer < 24f;
            if (hugTell || rushTell) {
                float prog = hugTell ? MathHelper.Clamp(StateTimer / LungeStart, 0f, 1f)
                    : MathHelper.Clamp(StateTimer / 24f, 0f, 1f);
                float dir = State == BearState.BearHug && StateParam != 0 ? StateParam : NPC.direction;
                Vector2 from = NPC.Center + new Vector2(dir * 40f, 10f);
                Vector2 to = from + new Vector2(dir, 0f) * (hugTell ? 760f : 460f);
                ACMShaders.DrawBeam(from, to, 6f * (0.4f + 0.6f * prog),
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.35f, 0.22f + 0.55f * prog,
                    flowSpeed: 1.2f, flowScale: 3f, coreSharp: 3f);
            }

            // 立地震地: 红色裂地地纹圈 (渐强)
            if (State == BearState.Slam && StateTimer < SlamChargeEnd) {
                float prog = MathHelper.Clamp(StateTimer / (float)SlamChargeEnd, 0f, 1f);
                DrawGroundDecal(sb, NPC.Bottom, 500f * (0.5f + 0.5f * prog), 0.55f * prog);
            }

            // 怒嚎蓄力: 大范围地纹 (全屏渐暗由 DarkWind 承担)
            if (State == BearState.TempestHowl && StateTimer < 102f) {
                float prog = MathHelper.Clamp(StateTimer / 102f, 0f, 1f);
                DrawGroundDecal(sb, NPC.Bottom, 640f * (0.4f + 0.6f * prog), 0.5f * prog);
            }

            // 死亡金缚: 三道金光带自天垂落
            if (State == BearState.Dying && StateTimer >= 60f && StateTimer < 200f) {
                float t = MathHelper.Clamp((StateTimer - 60f) / 60f, 0f, 1f);
                float strength = StateTimer >= 132f ? MathHelper.Clamp(1f - (StateTimer - 132f) / 68f, 0f, 1f) : t;
                for (int i = 0; i < 3; i++) {
                    float off = (i - 1) * 46f;
                    Vector2 top = NPC.Center + new Vector2(off * 2.2f, -900f);
                    Vector2 bottom = NPC.Center + new Vector2(off, 20f);
                    ACMShaders.DrawBeam(top, bottom, 5f + 3f * strength,
                        TelegraphColors.Gold, TelegraphColors.Holy * 0.4f, 0.5f * strength,
                        flowSpeed: 0.8f, flowScale: 2.2f, coreSharp: 2.6f);
                }
            }

            // 爆发泛光: 黑风未主导时走 RadialBloom (与全屏名额互斥, 见 PostDraw)
            if (slamFlash > 0.03f && windDraw <= 0.12f)
                ACMShaders.DrawRadialBloomAt(NPC.Bottom - new Vector2(0, 40f), 0.22f, slamFlash,
                    State == BearState.Dying ? TelegraphColors.Gold : TelegraphColors.Flame, rayCount: 12f);
        }

        // 地纹 (ArenaRunic) 落点圈 —— 缺着色器自动跳过
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
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Flame.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.Lethal.ToVector4());
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        /// <summary>全屏黑风后处理 (名额契约): 风扭曲 + 压暗 + 金染。强度不足时静默降级。</summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // 金染仅收服演出使用 (平时金冠闪走 PreDraw 贴图辉光, 不耗全屏名额)
            float gold = State == BearState.Dying ? goldDraw : 0f;
            if (windDraw <= 0.01f && gold <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = DarkWindFx;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(windDraw, 0f, 1f));
            fx.Parameters["uGold"]?.SetValue(MathHelper.Clamp(gold, 0f, 1f));
            fx.Parameters["uWindDir"]?.SetValue(new Vector2(1f, -0.08f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx, bindNoise: true);
        }
    }
}

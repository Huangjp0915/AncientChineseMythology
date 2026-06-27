using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
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

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    // 将臣 Jiangcen —— 飞僵尸将 / 雷锤主题 Boss 重做
    // 设计：阶段枚举 + 每攻击带可读预告（telegraph）的子状态机；
    //       六柄环绕重锤变为功能性武器（蓄力变红→沿径向猛砸）；
    //       50% 进入「雷狱」阶段：边界雷霆 + 锚点链式闪电；
    //       移除「距离=狂暴」反模式，玩家过远改用僵尸跳近身。
    [AutoloadBossHead]
    internal class Jiangcen : ModNPC
    {
        public enum BossPhase
        {
            Intro = 0,
            Phase1 = 1,
            Transition = 2, //雷狱化演出
            Phase2 = 3,
        }

        public enum Attack
        {
            Reposition = 0, //过渡 / 选招中枢
            HammerSlam = 1, //重锤蓄力猛砸
            JiangshiHop = 2, //僵尸三连跳 + 落地震波
            ThunderHammerThrow = 3, //雷锤回旋投掷
            CorpseRain = 4, //尸坟 → 尸手上抓
            GeneralsOrder = 5, //将令：冻结 + 镜像锤魂
            ChainLightning = 6, //雷狱：三锚点链式闪电
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        public Attack CurrentAttack {
            get => (Attack)(int)NPC.ai[1];
            set => NPC.ai[1] = (int)value;
        }
        public ref float SubStep => ref NPC.ai[2];
        public ref float AttackTimer => ref NPC.ai[3];

        //供环绕重锤读取的同步字段
        public float HammerOrbit;
        public bool HammersChanneling;
        public Vector2 ArenaCenter;

        private bool enteredPhase2;
        private int attackIndex;
        private float generalTimer;
        private bool spawnedHammers;
        private bool didIntroShock;
        private float introAppear;

        //子计数器（仅服务器/单机运行 AI，安全使用字段）
        private int hopCount;
        private Vector2 storePos;
        private readonly Vector2[] graveMarks = new Vector2[5];
        private int phase2HazardTimer;

        //V2 演出标量(纯本地视觉, 平滑过渡): 雷牢可见度 / 雷暴压暗
        private float prisonVis;
        private float stormVis;

        private const float BoundaryRadius = 1300f;

        private static readonly Attack[] Phase1Rotation = {
            Attack.HammerSlam, Attack.JiangshiHop, Attack.ThunderHammerThrow,
            Attack.CorpseRain, Attack.GeneralsOrder,
        };
        private static readonly Attack[] Phase2Rotation = {
            Attack.HammerSlam, Attack.ChainLightning, Attack.JiangshiHop,
            Attack.CorpseRain, Attack.ThunderHammerThrow, Attack.GeneralsOrder,
        };

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 420000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<JiangcenHammerItem>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedJiangcen = true;
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

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(HammerOrbit);
            writer.Write(HammersChanneling);
            writer.WriteVector2(ArenaCenter);
            writer.Write(enteredPhase2);
            writer.Write(attackIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            HammerOrbit = reader.ReadSingle();
            HammersChanneling = reader.ReadBoolean();
            ArenaCenter = reader.ReadVector2();
            enteredPhase2 = reader.ReadBoolean();
            attackIndex = reader.ReadInt32();
        }

        internal int GetBossDamage(float scaling = 1f) => Math.Max(1, (int)(NPC.damage * scaling));

        private void SetAttack(Attack a) {
            CurrentAttack = a;
            SubStep = 0;
            AttackTimer = 0;
            hopCount = 0;
            HammersChanneling = a == Attack.HammerSlam;
            NPC.netUpdate = true;
        }

        public override void AI() {
            generalTimer++;

            //出场：天空、生成六锤、记录场地中心
            if (!spawnedHammers) {
                spawnedHammers = true;
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 6; i++) {
                        NPC.NewNPCDirect(NPC.GetSource_FromAI(), NPC.Center, ModContent.NPCType<JiangcenHammer>(), NPC.whoAmI, NPC.whoAmI, i);
                    }
                }
                for (int i = 0; i < 50; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 150, Color.DarkRed, 2f);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.position);
            }

            if (!VaultUtils.isServer && !SkyManager.Instance[JiangcenSky.name].IsActive()) {
                SkyManager.Instance.Activate(JiangcenSky.name);
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.velocity *= 0.96f;
                    HammersChanneling = false;
                    HammerOrbit += 0.03f;
                    return;
                }
            }

            //环绕重锤公转：仅在非引导时推进（引导时锤悬停以便阅读）
            if (!HammersChanneling) HammerOrbit += 0.03f;

            AttackTimer++;

            //雷狱阶段的边界雷霆（站墙=被劈）
            if (Phase == BossPhase.Phase2) {
                Phase2BoundaryHazard(target);
            }

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Transition:
                    RunTransition(target);
                    break;
                default:
                    RunCombat(target);
                    break;
            }

            PublishPresentation();
        }

        // ===== V2 演出发布: 雷牢可见度 / 雷暴压暗 平滑过渡 + 每帧发布给屏幕系统 =====
        private void PublishPresentation() {
            if (Main.dedServ)
                return;

            float prisonTarget = 0f;
            float stormTarget = 0f;
            if (Phase == BossPhase.Transition) {
                //雷牢降临: 随过渡演出推进合拢
                prisonTarget = MathHelper.Clamp(AttackTimer / 90f, 0f, 1f);
                stormTarget = prisonTarget;
            }
            else if (Phase == BossPhase.Phase2) {
                prisonTarget = 1f;
                stormTarget = 1f;
            }

            prisonVis = MathHelper.Lerp(prisonVis, prisonTarget, 0.06f);
            stormVis = MathHelper.Lerp(stormVis, stormTarget, 0.05f);

            JiangcenThunderPrisonSystem.Publish(
                ArenaCenter == Vector2.Zero ? NPC.Center : ArenaCenter,
                BoundaryRadius * 0.72f, prisonVis, stormVis,
                Phase == BossPhase.Phase2, (float)Main.GlobalTimeWrappedHourly);
        }

        private void RunIntro(Player target) {
            if (ArenaCenter == Vector2.Zero) ArenaCenter = target.Center;

            introAppear = MathHelper.Clamp(introAppear + 1f / 120f, 0, 1);
            Vector2 desired = target.Center + new Vector2(0, -320);
            NPC.Center += (desired - NPC.Center) * 0.08f;
            NPC.velocity *= 0.85f;

            if (!VaultUtils.isServer && generalTimer % 4 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(90, 90) * (1 - introAppear);
                    int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Shadowflame, 0, 0, 150, Color.DarkRed, Main.rand.NextFloat(1.2f, 2.6f));
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 2.4f;
                }
            }

            if (!didIntroShock && introAppear > 0.9f) {
                didIntroShock = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
                JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.7f, new Color(180, 40, 50));
            }

            if (AttackTimer > 140) {
                Phase = BossPhase.Phase1;
                attackIndex = -1;
                SetAttack(Attack.Reposition);
            }
        }

        private void RunCombat(Player target) {
            switch (CurrentAttack) {
                case Attack.Reposition:
                    Run_Reposition(target);
                    break;
                case Attack.HammerSlam:
                    Run_HammerSlam(target);
                    break;
                case Attack.JiangshiHop:
                    Run_JiangshiHop(target);
                    break;
                case Attack.ThunderHammerThrow:
                    Run_ThunderHammerThrow(target);
                    break;
                case Attack.CorpseRain:
                    Run_CorpseRain(target);
                    break;
                case Attack.GeneralsOrder:
                    Run_GeneralsOrder(target);
                    break;
                case Attack.ChainLightning:
                    Run_ChainLightning(target);
                    break;
            }
        }

        // ===== 中枢：过渡 + 选招 =====
        private void Run_Reposition(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -300);
            NPC.Center += (hover - NPC.Center) * 0.08f;
            NPC.velocity *= 0.9f;

            if (AttackTimer > 36) {
                //阶段转换：50% 一次性进入雷狱（改规则，而非加速）
                if (!enteredPhase2 && NPC.life <= NPC.lifeMax * 0.5f) {
                    Phase = BossPhase.Transition;
                    SubStep = 0;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    return;
                }

                attackIndex++;
                Attack[] rotation = enteredPhase2 ? Phase2Rotation : Phase1Rotation;
                Attack next = rotation[((attackIndex % rotation.Length) + rotation.Length) % rotation.Length];

                //玩家过远：用僵尸跳贴近（位置博弈，而非提高 DPS）
                if (Vector2.Distance(target.Center, NPC.Center) > 1400f) {
                    next = Attack.JiangshiHop;
                }
                SetAttack(next);
            }
        }

        // ===== 雷狱化演出 =====
        private void RunTransition(Player target) {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;

            if (AttackTimer == 1) {
                ArenaCenter = target.Center;
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                //雷牢降临: 一次性大震 + Boss 中心雷暴泛光冲击波
                ACMScreenShakeSystem.Add(12f);
                JiangcenThunderPrisonSystem.Pulse(NPC.Center, 1f, TelegraphColors.Lightning);
            }

            //边界裂纹环：标出雷狱场地范围
            if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edge = ArenaCenter + ang.ToRotationVector2() * BoundaryRadius;
                int d = Dust.NewDust(edge, 0, 0, DustID.Electric, 0, 0, 100, default, 2.2f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (ArenaCenter - edge).SafeNormalize(Vector2.Zero) * 3f;
            }

            if (AttackTimer % 18 == 0 && !VaultUtils.isClient) {
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 3, 60);
            }

            if (AttackTimer > 150) {
                enteredPhase2 = true;
                NPC.dontTakeDamage = false;
                Phase = BossPhase.Phase2;
                attackIndex = -1;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                SetAttack(Attack.Reposition);
            }
        }

        // ===== 攻击 1：重锤蓄力猛砸（功能化环绕锤）=====
        private void Run_HammerSlam(Player target) {
            HammersChanneling = true;
            Vector2 hover = target.Center + new Vector2(0, -260);
            NPC.Center += (hover - NPC.Center) * 0.07f;
            NPC.velocity *= 0.9f;

            int slamCount = Main.masterMode ? 5 : (Main.expertMode ? 4 : 3);
            //空间分散的触发顺序，使猛砸扇面铺开
            int[] order = { 0, 3, 1, 4, 2, 5 };

            if (AttackTimer == 6) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.4f }, NPC.Center);
            }

            //每 46t 引导一柄锤进入蓄力（变红）→ 其自身蓄力 ~120t 后径向猛砸
            int triggerStep = 46;
            for (int k = 0; k < slamCount; k++) {
                if (AttackTimer == 10 + k * triggerStep) {
                    CommandHammerSlam(order[k % 6]);
                }
            }

            //充能粒子
            if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                Vector2 off = Main.rand.NextVector2CircularEdge(120, 120);
                int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.8f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 3f;
            }

            if (AttackTimer > 10 + slamCount * triggerStep + 170) {
                HammersChanneling = false;
                SetAttack(Attack.Reposition);
            }
        }

        private void CommandHammerSlam(int hammerIndex) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is JiangcenHammer && (int)npc.ai[0] == NPC.whoAmI && (int)npc.ai[1] == hammerIndex && npc.ai[2] == 0) {
                    npc.ai[2] = 1; //charge
                    npc.ai[3] = 0;
                    npc.netUpdate = true;
                    break;
                }
            }
        }

        // ===== 攻击 2：僵尸三连跳 + 落地震波 =====
        private void Run_JiangshiHop(Player target) {
            HammersChanneling = false;

            //SubStep: 0 预告(落点标线), 1 跳跃, 2 落地震波/恢复
            if (SubStep == 0) {
                NPC.velocity *= 0.8f;
                if (AttackTimer == 1) {
                    //预测落点 + 落地预告
                    storePos = target.Center + target.velocity * 14f;
                    storePos.Y = GetGroundY(storePos.X, target.Center.Y - 200) - NPC.height * 0.5f - 30;
                    SoundEngine.PlaySound(SoundID.DD2_GoblinBomberThrow with { Pitch = -0.3f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), storePos, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 0, 50);
                    }
                }
                //蓄势下蹲
                if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                    int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 0), NPC.width, 8, DustID.Shadowflame, 0, -2f, 120, Color.DarkRed, 1.6f);
                    Main.dust[d].noGravity = true;
                }
                if (AttackTimer > 46) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                //快速腾跃至落点
                NPC.Center = Vector2.Lerp(NPC.Center, storePos, 0.28f);
                NPC.velocity *= 0.6f;
                if (AttackTimer > 16 || Vector2.Distance(NPC.Center, storePos) < 40f) {
                    SubStep = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    //落地砸击：震波环
                    SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(11f);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Bottom, 0.6f, new Color(200, 50, 40));
                    if (!VaultUtils.isClient) {
                        int bolts = Main.masterMode ? 16 : (Main.expertMode ? 12 : 9);
                        for (int i = 0; i < bolts; i++) {
                            float a = MathHelper.TwoPi * i / bolts;
                            Vector2 v = a.ToRotationVector2() * (Main.expertMode ? 11f : 9f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, v,
                                ModContent.ProjectileType<JiangcenShockBolt>(), GetBossDamage(0.8f), 2f, Main.myPlayer);
                        }
                    }
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 26; i++) {
                            int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 8), NPC.width, 14, DustID.Shadowflame, 0, 0, 100, Color.DarkRed, 2.4f);
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-7, 7), Main.rand.NextFloat(-9, -2));
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.85f;
                if (AttackTimer > 24) {
                    hopCount++;
                    if (hopCount >= 3) {
                        SetAttack(Attack.Reposition);
                    }
                    else {
                        SubStep = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                }
            }
        }

        // ===== 攻击 3：雷锤回旋投掷 =====
        private void Run_ThunderHammerThrow(Player target) {
            HammersChanneling = false;
            if (SubStep == 0) {
                Vector2 hover = target.Center + new Vector2(target.Center.X < NPC.Center.X ? 260 : -260, -180);
                NPC.Center += (hover - NPC.Center) * 0.08f;
                NPC.velocity *= 0.9f;
                if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(70, 70);
                    int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Electric, 0, 0, 120, default, 1.7f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 3.5f;
                }
                if (AttackTimer == 6) SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);
                if (AttackTimer > 42) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(8f);
                    if (!VaultUtils.isClient) {
                        int hammers = Main.masterMode ? 3 : (Main.expertMode ? 2 : 1);
                        for (int i = 0; i < hammers; i++) {
                            float spread = MathHelper.ToRadians((i - (hammers - 1) / 2f) * 16f);
                            Vector2 v = NPC.DirectionTo(target.Center).RotatedBy(spread) * 19f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, v,
                                ModContent.ProjectileType<JiangcenThrownHammer>(), GetBossDamage(1.1f), 3f, Main.myPlayer, NPC.whoAmI);
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.92f;
                Vector2 hover = target.Center + new Vector2(0, -300);
                NPC.Center += (hover - NPC.Center) * 0.04f;
                if (AttackTimer > 150) SetAttack(Attack.Reposition);
            }
        }

        // ===== 攻击 4：尸坟 → 尸手上抓（垂直命中区，非随机天降）=====
        private void Run_CorpseRain(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(0, -340);
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Pitch = -0.3f }, NPC.Center);
                    float spacing = 230f;
                    float startX = target.Center.X - spacing * 2f;
                    for (int i = 0; i < 5; i++) {
                        float gx = startX + spacing * i + Main.rand.NextFloat(-40, 40);
                        float gy = GetGroundY(gx, target.Center.Y - 100);
                        graveMarks[i] = new Vector2(gx, gy);
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 1, 70);
                        }
                    }
                }
                if (AttackTimer > 62) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.NPCDeath2 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 5; i++) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenCorpseHand>(), GetBossDamage(1f), 2f, Main.myPlayer);
                        }
                    }
                }
            }
            else {
                if (AttackTimer > 90) SetAttack(Attack.Reposition);
            }
        }

        // ===== 攻击 5：将令——冻结 + 镜像锤魂 =====
        private void Run_GeneralsOrder(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(0, -320);
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    ArenaCenter = target.Center; //以当前为镜像中心
                    SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(9f);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.55f, new Color(190, 70, 200));
                    //冻结约 1.5s（将令定身，作预告/布阵）
                    target.AddBuff(BuffID.Frozen, 90);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenHammerGhost>(), GetBossDamage(1.2f), 2f, Main.myPlayer, 0, NPC.whoAmI);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenHammerGhost>(), GetBossDamage(1.2f), 2f, Main.myPlayer, 1, NPC.whoAmI);
                    }
                }
                //持续给予短冻结，维持定身窗口
                if (AttackTimer % 20 == 0 && AttackTimer < 90) target.AddBuff(BuffID.Frozen, 24);
                if (AttackTimer > 90) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                //锤魂自行镜像→突袭；Boss 等待整个流程结束
                if (AttackTimer > 170) SetAttack(Attack.Reposition);
            }
        }

        // ===== 攻击 6（雷狱）：三锚点链式闪电 =====
        private void Run_ChainLightning(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(0, -320);
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f }, NPC.Center);
                    float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 3; i++) {
                        float a = baseRot + MathHelper.TwoPi * i / 3f;
                        graveMarks[i] = target.Center + a.ToRotationVector2() * 430f;
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 2, 75);
                        }
                    }
                }
                if (AttackTimer > 56) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item94 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 a = graveMarks[i];
                            Vector2 b = graveMarks[(i + 1) % 3];
                            Vector2 mid = (a + b) * 0.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), mid, b - a,
                                ModContent.ProjectileType<JiangcenChainArc>(), GetBossDamage(1f), 2f, Main.myPlayer);
                        }
                    }
                }
            }
            else {
                if (AttackTimer > 100) SetAttack(Attack.Reposition);
            }
        }

        // ===== 雷狱边界雷霆：玩家离场地中心过远→被劈（位置型危险）=====
        private void Phase2BoundaryHazard(Player target) {
            //边界裂纹视觉
            if (!VaultUtils.isServer && generalTimer % 2 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edge = ArenaCenter + ang.ToRotationVector2() * BoundaryRadius;
                if (Vector2.Distance(edge, Main.LocalPlayer.Center) < 1400f) {
                    int d = Dust.NewDust(edge, 0, 0, DustID.Electric, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = (ArenaCenter - edge).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);
                }
            }

            phase2HazardTimer++;
            if (phase2HazardTimer >= 55) {
                phase2HazardTimer = 0;
                if (Vector2.Distance(target.Center, ArenaCenter) > BoundaryRadius * 0.72f) {
                    SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.3f }, target.Center);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(target.Center.X, target.Center.Y),
                            Vector2.Zero, ModContent.ProjectileType<JiangcenLightningStrike>(), GetBossDamage(1.1f), 2f, Main.myPlayer);
                    }
                }
            }
        }

        private static float GetGroundY(float x, float startY) {
            int tx = (int)(x / 16f);
            int ty = (int)(startY / 16f);
            for (int y = ty; y < ty + 80; y++) {
                Tile t = Framing.GetTileSafely(tx, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !TileID.Sets.Platforms[t.TileType]) {
                    return y * 16f;
                }
            }
            return startY + 700f;
        }

        internal static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.0001f) return Vector2.Distance(p, a);
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);

            DrawPrisonWallBeams();
            return false;
        }

        // ===== 雷牢墙体闪电: 沿环形雷牢边界的流动发光弧段(BeamGrad), 与 ArenaRunic 牢笼罩叠出"雷墙"质感 =====
        private void DrawPrisonWallBeams() {
            if (Main.dedServ || prisonVis < 0.25f || ArenaCenter == Vector2.Zero)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            float r = BoundaryRadius * 0.72f;
            float time = (float)Main.GlobalTimeWrappedHourly;
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            const int segs = 10;
            float baseAng = time * 0.12f;

            for (int i = 0; i < segs; i++) {
                //每段独立频闪, 同帧约半数点亮(雷墙跳动感, 省批)
                float flick = (float)Math.Sin(time * 9f + i * 2.3f) * (float)Math.Sin(time * 3.1f + i * 5.7f);
                if (flick < 0.4f)
                    continue;

                float a0 = baseAng + MathHelper.TwoPi * i / segs;
                float a1 = a0 + MathHelper.TwoPi / segs * 0.9f;
                Vector2 p0 = ArenaCenter + a0.ToRotationVector2() * r;
                Vector2 p1 = ArenaCenter + a1.ToRotationVector2() * r;

                if (Vector2.Distance((p0 + p1) * 0.5f, screenCenter) > Main.screenWidth)
                    continue;

                float intensity = MathHelper.Clamp(prisonVis * (0.4f + 0.6f * flick), 0f, 1f) * 0.8f;
                ACMShaders.DrawBeam(p0, p1, 5f + 4f * flick,
                    TelegraphColors.Lightning, new Color(40, 90, 180, 0), intensity, 2.2f, 2.5f);
            }
        }
    }

    // ===== 环绕重锤：从纯装饰变为功能性武器 =====
    // ai[0]=Boss whoAmI, ai[1]=序号(0..5), ai[2]=状态(0公转/1蓄力/2猛砸/3收回), ai[3]=状态计时
    internal class JiangcenHammer : ModNPC
    {
        private const int ChargeTime = 120; //~2s 变红蓄力
        private const int SlamTime = 34;
        private const int RecoverTime = 38;
        private const float OrbitRadius = 150f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 76;
            NPC.height = 76;
            NPC.damage = 0;
            NPC.defense = 20;
            NPC.lifeMax = 60000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCHit4;
            NPC.value = 0f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override bool CheckActive() => false;

        public override void AI() {
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.Alives() || boss.ModNPC is not Jiangcen jc) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }
            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;

            ref float state = ref NPC.ai[2];
            ref float timer = ref NPC.ai[3];
            float index = NPC.ai[1];

            Vector2 orbitPos = boss.Center + (jc.HammerOrbit * 0.6f + MathHelper.TwoPi / 6f * index).ToRotationVector2() * OrbitRadius;
            timer++;

            if (state == 0) { //公转（待命，无伤害）
                NPC.damage = 0;
                NPC.Center = orbitPos;
                NPC.velocity = Vector2.Zero;
                NPC.rotation = boss.AngleTo(NPC.Center);
            }
            else if (state == 1) { //蓄力变红（悬停于径向，可读）
                NPC.damage = 0;
                NPC.Center = orbitPos; //引导期间公转冻结→锤悬停
                NPC.velocity = Vector2.Zero;
                NPC.rotation = boss.AngleTo(NPC.Center);
                if (!VaultUtils.isServer && timer % 4 == 0) {
                    int d = Dust.NewDust(NPC.Center, 0, 0, DustID.RedTorch, 0, 0, 100, default, 1.6f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = Main.rand.NextVector2Circular(2, 2);
                }
                if (timer >= ChargeTime) {
                    state = 2;
                    timer = 0;
                    Vector2 dir = (NPC.Center - boss.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = dir * (Main.expertMode ? 40f : 33f);
                    NPC.rotation = dir.ToRotation();
                    SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(8f);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.65f, TelegraphColors.Lethal);
                    NPC.netUpdate = true;
                }
            }
            else if (state == 2) { //径向猛砸（长矩形扫掠命中区）
                NPC.damage = jc.GetBossDamage(1.3f);
                NPC.velocity *= 0.985f;
                if (!VaultUtils.isServer) {
                    int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, 0, 0, 100, Color.DarkRed, 2f);
                    Main.dust[d].noGravity = true;
                }
                if (timer >= SlamTime) {
                    state = 3;
                    timer = 0;
                    NPC.damage = 0;
                    NPC.netUpdate = true;
                }
            }
            else { //收回
                NPC.damage = 0;
                NPC.Center = Vector2.Lerp(NPC.Center, orbitPos, 0.12f);
                NPC.velocity *= 0.9f;
                NPC.rotation = boss.AngleTo(NPC.Center);
                if (timer >= RecoverTime || Vector2.Distance(NPC.Center, orbitPos) < 24f) {
                    state = 0;
                    timer = 0;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();

            DrawSlamTelegraph();

            //蓄力期间逐渐变红预告
            Color tint = drawColor;
            if (NPC.ai[2] == 1) {
                float t = MathHelper.Clamp(NPC.ai[3] / ChargeTime, 0, 1);
                float flash = 0.5f + 0.5f * (float)Math.Sin(NPC.ai[3] * (0.2f + t * 0.4f));
                tint = Color.Lerp(drawColor, new Color(255, 40, 40) * (0.7f + 0.3f * flash), 0.4f + 0.6f * t);
            }
            else if (NPC.ai[2] == 2) {
                tint = Color.Lerp(drawColor, new Color(255, 70, 60), 0.7f);
            }

            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, tint * sengs
                    , NPC.oldRot[i] + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, tint
                , NPC.rotation + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }

        // ===== 猛砸径向预告线: 蓄力期间沿"对穿走廊"渐强的致命红线(BeamGrad), 让径向猛砸可读 =====
        private void DrawSlamTelegraph() {
            if (Main.dedServ || NPC.ai[2] != 1)
                return;
            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.Alives())
                return;

            float t = MathHelper.Clamp(NPC.ai[3] / ChargeTime, 0f, 1f);
            Vector2 dir = (NPC.Center - boss.Center).SafeNormalize(Vector2.UnitX);
            Vector2 start = NPC.Center;
            Vector2 end = NPC.Center + dir * 1000f; //径向猛砸扫掠方向

            //命中前渐强红线(红只留给真正的伤害源 — 猛砸路径)
            float intensity = 0.15f + 0.7f * t;
            float w = 4f + 12f * t;
            ACMShaders.DrawBeam(start, end, w,
                TelegraphColors.Lethal, new Color(120, 10, 15, 0), intensity, 1.6f, 2.0f);
        }
    }

    // ===== 预告标记：落点 / 尸坟 / 锚点 / 边界（无伤害纯视觉）=====
    // ai[0]=样式(0落点环,1尸坟,2锚点,3边界), ai[1]=寿命
    internal class JiangcenTelegraphMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 80;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0 && Projectile.ai[1] > 0) {
                Projectile.timeLeft = (int)Projectile.ai[1];
            }
            Projectile.localAI[0]++;
            if (!VaultUtils.isServer && Projectile.localAI[0] % 4 == 0) {
                int style = (int)Projectile.ai[0];
                bool warm = style == 0 || style == 1; //落点/尸坟=暖红粒子, 锚点/边界=雷青粒子
                int dustType = warm ? DustID.Shadowflame : DustID.Electric;
                Color col = warm ? Color.DarkRed : default;
                float r = style == 1 ? 18f : 34f;
                Vector2 off = Main.rand.NextVector2CircularEdge(r, r);
                int d = Dust.NewDust(Projectile.Center + off, 0, 0, dustType, 0, 0, 120, col, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = off.SafeNormalize(Vector2.Zero) * 0.6f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int style = (int)Projectile.ai[0];
            float life = Math.Max(1f, Projectile.ai[1]);
            float prog = MathHelper.Clamp(Projectile.localAI[0] / life, 0, 1);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Projectile.localAI[0] * 0.25f);

            //统一预警配色(§2.1): 落点=致命红 / 尸坟=暖暗红 / 锚点=雷青 / 边界=低饱和脉动
            Color baseCol = style switch {
                0 => TelegraphColors.Lethal,            //僵尸跳落点(致命猛砸)
                1 => new Color(190, 45, 35),            //尸坟(暖暗红)
                2 => TelegraphColors.Lightning,         //链电锚点(雷青)
                3 => new Color(110, 150, 200),          //雷牢边界(低饱和)
                _ => TelegraphColors.Lightning
            };
            baseCol.A = 0;

            Vector2 pos = Projectile.Center - Main.screenPosition;

            //环形扩张/收束的预告圈
            float ringScale = MathHelper.Lerp(2.6f, 0.7f, prog) * (style == 3 ? 0.6f : 1f);
            Main.spriteBatch.Draw(tex, pos, null,
                baseCol * (0.5f + 0.5f * prog) * pulse, 0f, tex.Size() / 2, ringScale, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(tex, pos, null,
                baseCol * 0.5f, 0f, tex.Size() / 2, 1.1f * pulse, SpriteEffects.None, 0);

            //锁定式内核: 落点/锚点临近命中时收束变亮(可读的"就是这里")
            if (style == 0 || style == 2) {
                float lockT = prog * prog;
                Main.spriteBatch.Draw(tex, pos, null,
                    baseCol * (0.3f + 0.7f * lockT), 0f, tex.Size() / 2,
                    MathHelper.Lerp(0.9f, 0.35f, prog) * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    // ===== 落地震波弹：雷主题径向电弹 =====
    internal class JiangcenShockBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 80;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.velocity *= 0.985f;
            Projectile.rotation += 0.3f;
            if (!VaultUtils.isServer) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 120, default, 1.3f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(tex, op, null, new Color(120, 180, 255, 0) * f * 0.5f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 尸手：从尸坟向上抓起的垂直命中柱 =====
    internal class JiangcenCorpseHand : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SlashBurst";

        private const int WarnTime = 26;
        private const int RiseTime = 30;
        private const int ActiveTime = 46;
        private const float ColumnHeight = 320f;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = (int)ColumnHeight;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = WarnTime + RiseTime + ActiveTime + 10;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            //锚定于坟口，命中柱自下而上
            if (Projectile.ai[1] == 0) Projectile.ai[1] = Projectile.Center.Y;
            Projectile.Center = new Vector2(Projectile.Center.X, Projectile.ai[1] - ColumnHeight / 2f);

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == WarnTime) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer) {
                if (Projectile.localAI[0] < WarnTime) {
                    int d = Dust.NewDust(new Vector2(Projectile.Center.X - 20, Projectile.ai[1] - 8), 40, 8, DustID.Shadowflame, 0, -1f, 120, Color.DarkRed, 1.5f);
                    Main.dust[d].noGravity = true;
                }
                else {
                    int d = Dust.NewDust(new Vector2(Projectile.Center.X - 24, Projectile.Center.Y), 48, (int)ColumnHeight, DustID.Shadowflame, 0, -3f, 120, Color.DarkRed, 1.8f);
                    Main.dust[d].noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.localAI[0] >= WarnTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float t = Projectile.localAI[0];
            float rise = MathHelper.Clamp((t - WarnTime) / RiseTime, 0f, 1f);
            float warnAlpha = MathHelper.Clamp(t / WarnTime, 0f, 1f);
            //SlashBurst 自底向上发散：原点取底部中心
            Vector2 bottom = new Vector2(Projectile.Center.X, Projectile.ai[1]) - Main.screenPosition;
            float scaleY = (ColumnHeight / tex.Height) * (t < WarnTime ? 0.25f : rise);
            float scaleX = 64f / tex.Width;
            Color col = (t < WarnTime ? new Color(120, 10, 10) * warnAlpha * 0.5f : new Color(200, 30, 30));
            col.A = 0;
            Main.spriteBatch.Draw(tex, bottom, null, col, 0f, new Vector2(tex.Width / 2f, tex.Height), new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 雷锤回旋投掷：飞出再回返，需躲两段 =====
    // ai[0]=Boss whoAmI
    internal class JiangcenThrownHammer : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int OutTime = 42;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 70;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.rotation += 0.45f;
            Projectile.ai[1]++;

            if (Projectile.ai[1] < OutTime) {
                Projectile.velocity *= 0.965f; //飞出减速
            }
            else {
                //回返至 Boss
                NPC boss = Main.npc[(int)Projectile.ai[0]];
                Vector2 dest = boss.Alives() ? boss.Center : Projectile.Center;
                Vector2 dir = (dest - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.06f);
                if (boss.Alives() && Vector2.Distance(Projectile.Center, dest) < 70f) {
                    Projectile.Kill();
                }
            }

            if (!VaultUtils.isServer) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 120, default, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.4f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(tex, op, null, new Color(120, 170, 255, 0) * f * 0.4f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 镜像锤魂：镜像玩家走位后突袭 =====
    // ai[0]=类型(0点对称,1水平镜像), ai[1]=Boss whoAmI
    internal class JiangcenHammerGhost : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int MirrorTime = 120;
        private const int StrikeTime = 46;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 70;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = MirrorTime + StrikeTime + 40;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            NPC boss = Main.npc[(int)Projectile.ai[1]];
            if (!boss.Alives() || boss.ModNPC is not Jiangcen jc) {
                Projectile.Kill();
                return;
            }
            Player target = Main.player[boss.target];
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];
            Projectile.rotation += 0.2f;

            if (t < MirrorTime) {
                //镜像玩家相对场地中心的位置
                Vector2 mirror;
                if ((int)Projectile.ai[0] == 0) {
                    mirror = jc.ArenaCenter * 2f - target.Center; //点对称
                }
                else {
                    mirror = new Vector2(jc.ArenaCenter.X * 2f - target.Center.X, target.Center.Y); //水平镜像
                }
                Projectile.Center = Vector2.Lerp(Projectile.Center, mirror, 0.25f);
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isServer && t % 3 == 0) {
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.4f);
                    Main.dust[d].noGravity = true;
                }
            }
            else if (t == MirrorTime) {
                Projectile.localAI[1] = 1; //记录已捕捉
                Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * 26f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 1.1f }, Projectile.Center);
            }
            else {
                Projectile.velocity *= 0.992f;
                if (!VaultUtils.isServer) {
                    int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.localAI[0] >= MirrorTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            bool striking = Projectile.localAI[0] >= MirrorTime;
            //与本体异色(紫红): 标明"这是你的影子", 强化与自己走位对抗的体验
            Color tint = striking ? new Color(225, 75, 205) : new Color(150, 60, 175) * 0.75f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(tex, op, null, tint * f * 0.4f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, tint, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);

            //突袭起手的致命突进线(红=真正伤害源): 命中前可读"影子从哪扑来"
            float strikeT = Projectile.localAI[0] - MirrorTime;
            if (striking && strikeT < 16f && Projectile.velocity.LengthSquared() > 1f) {
                float fade = 1f - strikeT / 16f;
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * 520f, 6f + 6f * fade,
                    TelegraphColors.Lethal, new Color(150, 20, 90, 0), 0.85f * fade, 1.8f, 2.2f);
            }
            return false;
        }
    }

    // ===== 雷狱垂直落雷：边界位置型危险（自带预告）=====
    internal class JiangcenLightningStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightningBranch";

        private const int WarnTime = 42;
        private const int ActiveTime = 22;
        private const float ColumnHeight = 1100f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = (int)ColumnHeight;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = WarnTime + ActiveTime + 8;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == WarnTime) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 1.2f }, Projectile.Center);
                ACMScreenShakeSystem.Add(7f);
                JiangcenThunderPrisonSystem.Pulse(Projectile.Center, 0.5f, TelegraphColors.Lightning);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 24; i++) {
                        int d = Dust.NewDust(new Vector2(Projectile.Center.X - 8, Projectile.Center.Y - ColumnHeight / 2), 16, (int)ColumnHeight, DustID.Electric, 0, 0, 100, default, 1.8f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-2, 2), Main.rand.NextFloat(-2, 2));
                    }
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.localAI[0] >= WarnTime && Projectile.localAI[0] < WarnTime + ActiveTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float t = Projectile.localAI[0];
            bool active = t >= WarnTime;
            float warnAlpha = MathHelper.Clamp(t / WarnTime, 0, 1);
            float flick = 0.6f + 0.4f * (float)Main.rand.NextFloat();
            Color col = active ? new Color(180, 220, 255) * flick : new Color(80, 140, 255) * warnAlpha * 0.4f;
            col.A = 0;
            float scaleY = ColumnHeight / tex.Height;
            float scaleX = (active ? 1f : 0.35f) * 46f / tex.Width;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, col, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f), new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 雷狱链式闪电：锚点之间的线段命中（自带预告）=====
    // 生成：position=中点, velocity=(B-A)；首帧存半向量于 ai[0],ai[1]
    internal class JiangcenChainArc : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightningBranch";

        private const int WarnTime = 44;
        private const int ActiveTime = 30;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = WarnTime + ActiveTime + 8;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.localAI[1] == 0) {
                Projectile.localAI[1] = 1;
                Vector2 half = Projectile.velocity * 0.5f;
                Vector2 mid = Projectile.Center;
                Projectile.ai[0] = half.X;
                Projectile.ai[1] = half.Y;
                Projectile.velocity = Vector2.Zero;
                //扩张 AABB 作为宽相位包围盒（精确判定见 Colliding）
                Projectile.width = (int)Math.Max(40, Math.Abs(half.X) * 2 + 40);
                Projectile.height = (int)Math.Max(40, Math.Abs(half.Y) * 2 + 40);
                Projectile.Center = mid;
                Projectile.netUpdate = true;
            }
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == WarnTime) {
                SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && Projectile.localAI[0] >= WarnTime) {
                Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                Vector2 a = Projectile.Center - half;
                Vector2 b = Projectile.Center + half;
                Vector2 p = Vector2.Lerp(a, b, Main.rand.NextFloat());
                int d = Dust.NewDust(p, 0, 0, DustID.Electric, 0, 0, 100, default, 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.localAI[0] < WarnTime || Projectile.localAI[0] >= WarnTime + ActiveTime) return false;
            Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Vector2 a = Projectile.Center - half;
            Vector2 b = Projectile.Center + half;
            Vector2 c = targetHitbox.Center.ToVector2();
            return Jiangcen.DistanceToSegment(c, a, b) < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Vector2 a = Projectile.Center - half - Main.screenPosition;
            Vector2 b = Projectile.Center + half - Main.screenPosition;
            float len = (b - a).Length();
            float rot = (b - a).ToRotation() - MathHelper.PiOver2;
            float t = Projectile.localAI[0];
            bool active = t >= WarnTime;
            float warnAlpha = MathHelper.Clamp(t / WarnTime, 0, 1);
            float flick = 0.6f + 0.4f * Main.rand.NextFloat();
            Color col = active ? new Color(180, 220, 255) * flick : new Color(80, 140, 255) * warnAlpha * 0.4f;
            col.A = 0;
            float scaleY = len / tex.Height;
            float scaleX = (active ? 0.9f : 0.3f) * 40f / tex.Width;
            Main.spriteBatch.Draw(tex, a, null, col, rot, new Vector2(tex.Width / 2f, 0f), new Vector2(scaleX, scaleY), SpriteEffects.None, 0);

            //激活段升格为发光雷电 beam(流动 UV), 锚点间链电更有存在感
            if (active) {
                Vector2 aWorld = Projectile.Center - half;
                Vector2 bWorld = Projectile.Center + half;
                ACMShaders.DrawBeam(aWorld, bWorld, 8f + 6f * flick,
                    TelegraphColors.Lightning, new Color(40, 90, 180, 0), 0.5f + 0.5f * flick, 2.6f, 3.0f);
            }
            return false;
        }
    }

    internal class JiangcenSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float maxIntensity = 0.6f;
        private Color skyColor;
        internal static string name;
        public static void LoadInstance() {
            name = "AncientChineseMythology:JiangcenSky";
            SkyManager.Instance[name] = new JiangcenSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override Color OnTileColor(Color inColor) {
            return inColor * (1f - intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            NPC boss = GetBoss();
            Vector2 pullShake = Vector2.Zero;

            if (boss != null) {
                float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;
                Vector2 jitter = new Vector2(
                    (float)Math.Sin(time * 6f),
                    (float)Math.Cos(time * 4.2f)
                ) * (1.5f * intensity);

                pullShake = (boss.Center - Main.LocalPlayer.Center)
                    .SafeNormalize(Vector2.Zero) * (2f * intensity) + jitter;
            }

            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            Color finalColor = skyColor * (intensity * pulse);

            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle((int)pullShake.X, (int)pullShake.Y, Main.screenWidth, Main.screenHeight),
                finalColor
            );
        }

        public override void Update(GameTime gameTime) {
            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);
                t *= t;

                Color nearRed = new Color(160, 0, 20);
                if (Main.GlobalTimeWrappedHourly % 1f < 0.5f)
                    nearRed = Color.Lerp(nearRed, new Color(200, 20, 40), 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f));

                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(15, 8, 30),
                    new Color(20, 50, 50),
                    nearRed
                );

                intensity = MathHelper.Min(maxIntensity, intensity + 0.02f);
                active = true;
            }
            else {
                intensity = MathHelper.Max(0f, intensity - 0.015f);
                if (intensity <= 0f) {
                    Deactivate();
                }
            }
        }

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Jiangcen>())
                    return npc;
            }
            return null;
        }
    }
}

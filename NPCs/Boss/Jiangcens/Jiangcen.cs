using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    // ============================================================
    // 将臣 Jiangcen —— 四大僵尸始祖之首 / 尸将军 V3 重做
    // 主题: 尸将军・军令・天罚雷狱。攻击语言统一为"军阵指令":
    //   点将(六锤受命猛砸) / 布阵(尸坟唤将) / 将令(雷印点名) / 天罚(雷狱・万雷点将)
    // 移动语言 = 僵尸跳(蹲伏→弹射→空中僵直→直线下砸), 不飞行追击。
    // 三大演出: 天雷显形入场 / 雷狱降临换阶段 / 天罚轰顶死亡(CheckDead 拦截)。
    // 弹幕组见 JiangcenProjectiles.cs, 环绕重锤见 JiangcenHammer.cs,
    // 屏幕氛围见 JiangcenThunderPrisonSystem.cs, 着色器工具见 JiangcenVFX.cs。
    // ============================================================
    [AutoloadBossHead]
    internal class Jiangcen : ModNPC
    {
        public enum BossPhase
        {
            Intro = 0,
            Phase1 = 1,
            Transition = 2, //雷狱降临演出
            Phase2 = 3,
            Death = 4,      //死亡演出(CheckDead 拦截)
        }

        public enum Attack
        {
            Reposition = 0,        //连接拍 / 选招中枢
            HammerSlam = 1,        //点将猛砸
            JiangshiHop = 2,       //僵尸三连跳 + 落地震波
            ThunderHammerThrow = 3,//雷锤回旋投掷
            CorpseRain = 4,        //尸坟唤将(波浪尸手)
            GeneralsOrder = 5,     //将令雷印(点名) + 镜像锤魂
            ChainLightning = 6,    //雷狱链电(4锚菱形电网)
            ThunderRollCall = 7,   //万雷点将(终章走廊落雷)
            HammerPrison = 8,      //六锤连狱(弦线对穿)
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

        //供环绕重锤/弹幕读取的同步字段
        public float HammerOrbit;
        public bool HammersChanneling;
        public Vector2 ArenaCenter;
        public float ChordBaseAngle;   //六锤连狱的边界基准角
        public bool InPhase2 => enteredPhase2;
        public float PrisonRadius => BoundaryRadius * 0.72f;

        private bool enteredPhase2;
        private int attackIndex;
        private float generalTimer;
        private int despawnTimer;

        //子状态数据(状态切换时 netUpdate 同步)
        private Vector2 jumpTarget;
        private int hopCount;
        private readonly Vector2[] graveMarks = new Vector2[6];
        private int phase2HazardTimer;
        private int safeLaneIndex;

        //纯本地视觉标量
        private float prisonVis;
        private float stormVis;
        private float instabilityVis;
        private float introDissolve = 1f;  //入场显形: 1=未显形
        private float deathDissolve;       //死亡崩解: 1=完全消散
        private float eyeGlow;
        private float bodyArcGlow;
        private float armSnap;             //点将抬臂 snap
        private Vector2 bodySquash = Vector2.One;

        private const float BoundaryRadius = 1300f;

        //手写节奏数组: 压制(跳) ↔ 布阵(坟/链电) ↔ 爆发(锤) 交替, 攻击序列本身就是编排
        private static readonly Attack[] Phase1Rotation = {
            Attack.JiangshiHop, Attack.HammerSlam, Attack.ThunderHammerThrow,
            Attack.JiangshiHop, Attack.CorpseRain, Attack.GeneralsOrder,
        };
        private static readonly Attack[] Phase2Rotation = {
            Attack.JiangshiHop, Attack.ChainLightning, Attack.HammerSlam,
            Attack.JiangshiHop, Attack.GeneralsOrder, Attack.HammerPrison,
            Attack.CorpseRain, Attack.ThunderHammerThrow,
        };
        //终章(<18%): 万雷点将领衔, 全部强招入池
        private static readonly Attack[] FinalRotation = {
            Attack.ThunderRollCall, Attack.JiangshiHop, Attack.ChainLightning,
            Attack.HammerSlam, Attack.JiangshiHop, Attack.HammerPrison,
            Attack.GeneralsOrder, Attack.JiangshiHop,
        };

        private bool IsFinalPhase => enteredPhase2 && NPC.life <= NPC.lifeMax * 0.18f;

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
            NPC.defense = 36;
            NPC.damage = 70;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 480000;
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

        // ===== 死亡拦截: 尸祖之死必须是一场天罚 =====
        public override bool CheckDead() {
            if (Phase != BossPhase.Death) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                Phase = BossPhase.Death;
                SubStep = 0;
                AttackTimer = 0;
                HammersChanneling = false;
                if (!VaultUtils.isClient) {
                    ClearHostileProjectiles();
                }
                NPC.netUpdate = true;
                return false;
            }
            return true;
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
            writer.Write(ChordBaseAngle);
            writer.Write(enteredPhase2);
            writer.Write(attackIndex);
            writer.WriteVector2(jumpTarget);
            writer.Write(hopCount);
            writer.Write(safeLaneIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            HammerOrbit = reader.ReadSingle();
            HammersChanneling = reader.ReadBoolean();
            ArenaCenter = reader.ReadVector2();
            ChordBaseAngle = reader.ReadSingle();
            enteredPhase2 = reader.ReadBoolean();
            attackIndex = reader.ReadInt32();
            jumpTarget = reader.ReadVector2();
            hopCount = reader.ReadInt32();
            safeLaneIndex = reader.ReadInt32();
        }

        internal int GetBossDamage(float scaling = 1f) => Math.Max(1, (int)(NPC.defDamage * scaling));

        private void SetAttack(Attack a) {
            CurrentAttack = a;
            SubStep = 0;
            AttackTimer = 0;
            hopCount = 0;
            HammersChanneling = a == Attack.HammerSlam;
            NPC.netUpdate = true;
        }

        private void ClearHostileProjectiles() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.hostile && p.damage > 0) {
                    p.Kill();
                }
            }
        }

        private void Announce(string key, Color color) {
            if (Main.dedServ)
                return;
            string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.Jiangcen." + key);
            CombatText.NewText(NPC.getRect(), color, text, true);
        }

        // ===== 锤指令 =====
        private void CommandHammer(int index, int state, bool onlyIdle = false) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is JiangcenHammer && (int)npc.ai[0] == NPC.whoAmI && (int)npc.ai[1] == index) {
                    if (onlyIdle && npc.ai[2] != 0)
                        return;
                    npc.ai[2] = state;
                    npc.ai[3] = 0;
                    npc.netUpdate = true;
                    return;
                }
            }
        }

        private void CommandAllHammers(int state) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is JiangcenHammer && (int)npc.ai[0] == NPC.whoAmI && npc.ai[2] < 7) {
                    npc.ai[2] = state;
                    npc.ai[3] = 0;
                    npc.netUpdate = true;
                }
            }
        }

        public override void AI() {
            generalTimer++;

            //天幕激活(纯客户端)
            if (!VaultUtils.isServer && !SkyManager.Instance[JiangcenSky.name].IsActive()) {
                SkyManager.Instance.Activate(JiangcenSky.name);
            }

            //死亡演出优先于一切(无目标也照常播完)
            if (Phase == BossPhase.Death) {
                RunDeath();
                PublishPresentation();
                return;
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    //无人可战: 收势升空离场
                    NPC.damage = 0;
                    HammersChanneling = false;
                    HammerOrbit += 0.03f;
                    if (++despawnTimer > 120) {
                        NPC.velocity.Y -= 0.4f;
                        NPC.velocity.X *= 0.98f;
                        NPC.EncourageDespawn(20);
                    }
                    else {
                        NPC.velocity *= 0.96f;
                    }
                    PublishPresentation();
                    return;
                }
            }
            despawnTimer = 0;

            //接触伤害默认关闭, 只有僵尸跳下砸段开启(伤害窗与视觉对齐)
            NPC.damage = 0;

            //环绕重锤公转：仅在非引导时推进（引导时锤悬停以便阅读）
            if (!HammersChanneling)
                HammerOrbit += 0.03f;

            AttackTimer++;

            //雷狱阶段的边界雷霆（出界=被劈）
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

            //视觉标量衰减
            armSnap = Math.Max(0f, armSnap - 1f);
            bodySquash = Vector2.Lerp(bodySquash, Vector2.One, 0.12f);

            PublishPresentation();
        }

        // ===== 演出标量发布: 平滑过渡 + 每帧发布给屏幕系统/天幕 =====
        private void PublishPresentation() {
            if (Main.dedServ)
                return;

            float prisonTarget = 0f;
            float stormTarget = 0f;
            float instabilityTarget = 0f;
            float skyPhase = enteredPhase2 ? 1f : 0f;
            float skyDeath = 0f;

            if (Phase == BossPhase.Transition) {
                //军鼓推进雷暴, 牢体在 impact 拍才点亮(SubStep 3 直接 snap prisonVis)
                stormTarget = MathHelper.Clamp((SubStep + 1) * 0.25f, 0f, 1f);
                prisonTarget = SubStep >= 3 ? 1f : 0f;
                skyPhase = 1f;
            }
            else if (Phase == BossPhase.Phase2) {
                prisonTarget = 1f;
                stormTarget = 1f;
                if (IsFinalPhase)
                    instabilityTarget = 0.35f;
                if (CurrentAttack == Attack.ThunderRollCall)
                    instabilityTarget = 0.5f;
            }
            else if (Phase == BossPhase.Death) {
                //坠地→挣扎期雷牢失稳衰减, 天罚后彻底熄灭
                bool annihilated = SubStep >= 4;
                prisonTarget = annihilated ? 0f : 0.55f;
                stormTarget = annihilated ? 0.25f : 0.7f;
                instabilityTarget = annihilated ? 1f : MathHelper.Clamp(0.3f + (float)SubStep * 0.2f, 0f, 1f);
                skyDeath = annihilated ? 0.35f : MathHelper.Clamp((float)SubStep * 0.3f, 0f, 0.9f);
                skyPhase = 1f;
            }

            prisonVis = MathHelper.Lerp(prisonVis, prisonTarget, prisonTarget > prisonVis ? 0.5f : 0.06f);
            stormVis = MathHelper.Lerp(stormVis, stormTarget, 0.05f);
            instabilityVis = MathHelper.Lerp(instabilityVis, instabilityTarget, 0.06f);

            //红目/缠电辉光
            float eyeTarget = Phase == BossPhase.Intro ? MathHelper.Clamp((AttackTimer - 80f) / 60f, 0f, 1f) : 1f;
            if (Phase == BossPhase.Death)
                eyeTarget = SubStep >= 4 ? 0f : 0.8f;
            eyeGlow = MathHelper.Lerp(eyeGlow, eyeTarget, 0.05f);

            float arcTarget = 0.18f;
            if (enteredPhase2) arcTarget = 0.42f;
            if (Phase == BossPhase.Transition) arcTarget = 0.3f + (float)SubStep * 0.2f;
            if (CurrentAttack == Attack.ThunderRollCall && Phase == BossPhase.Phase2) arcTarget = 0.9f;
            if (Phase == BossPhase.Death) arcTarget = SubStep >= 4 ? 0f : 0.85f;
            if (Phase == BossPhase.Intro) arcTarget = eyeGlow * 0.25f;
            bodyArcGlow = MathHelper.Lerp(bodyArcGlow, arcTarget, 0.07f);

            JiangcenThunderPrisonSystem.Publish(
                ArenaCenter == Vector2.Zero ? NPC.Center : ArenaCenter,
                PrisonRadius, prisonVis, stormVis,
                Phase == BossPhase.Phase2, (float)Main.GlobalTimeWrappedHourly, instabilityVis);
            JiangcenSky.PublishState(skyPhase, skyDeath);
        }

        // ============================================================
        //  入场: 雷暴压城 → 天雷显形 → 静立亮目 → 六锤点兵 → 长啸
        // ============================================================
        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;

            if (AttackTimer == 1) {
                ArenaCenter = target.Center;
                //显形点: 玩家侧上方
                jumpTarget = target.Center + new Vector2(target.direction * -240f, -220f);
                NPC.Center = jumpTarget;
                NPC.velocity = Vector2.Zero;
                introDissolve = 1f;
                NPC.netUpdate = true;
            }

            NPC.velocity *= 0.9f;

            //远景雷暴前兆
            if (AttackTimer == 22 || AttackTimer == 48) {
                JiangcenSky.TriggerFlash(0.45f);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.55f, Pitch = -0.4f }, target.Center);
            }

            //t=70: 天雷轰落, 尸将军在雷柱中显形
            if (AttackTimer == 70) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.1f, Pitch = -0.15f }, NPC.Center);
                ACMScreenShakeSystem.Add(9f);
                JiangcenThunderPrisonSystem.FlashWhite(0.4f);
                JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.8f, TelegraphColors.Lightning);
                JiangcenSky.TriggerFlash(1f);
                if (!VaultUtils.isClient) {
                    //纯视觉显形雷柱
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<JiangcenLightningStrike>(), 0, 0f, Main.myPlayer, 0, 2, 1500);
                }
            }

            //显形: 溶解阈值 1→0 (雷青灼边)
            if (AttackTimer > 70) {
                introDissolve = Math.Max(0f, introDissolve - 1f / 30f);
            }

            //静立期: 只有低鸣与细尘(menace is stillness)
            if (!VaultUtils.isServer && AttackTimer > 70 && AttackTimer < 150 && generalTimer % 5 == 0) {
                Vector2 off = Main.rand.NextVector2CircularEdge(90, 90);
                int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Shadowflame, 0, 0, 150, Color.DarkRed, 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 1.6f;
            }

            //六锤点兵: 每 8 帧一柄自脚下拔出
            for (int i = 0; i < 6; i++) {
                if (AttackTimer == 150 + i * 8) {
                    SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.4f + i * 0.08f, Volume = 0.9f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        NPC.NewNPCDirect(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, 60), ModContent.NPCType<JiangcenHammer>(), NPC.whoAmI, NPC.whoAmI, i);
                    }
                    if (!VaultUtils.isServer) {
                        for (int k = 0; k < 8; k++) {
                            int d = Dust.NewDust(NPC.Center + new Vector2(0, 50), 10, 10, DustID.Electric, 0, -2f, 100, default, 1.5f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                }
            }

            //长啸开战
            if (AttackTimer == 198) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.45f, Volume = 1.25f }, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
                JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.7f, JiangcenVFX.CorpseRed);
                JiangcenSky.TriggerFlash(1.1f);
            }

            if (AttackTimer > 215) {
                NPC.dontTakeDamage = false;
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
                case Attack.ThunderRollCall:
                    Run_ThunderRollCall(target);
                    break;
                case Attack.HammerPrison:
                    Run_HammerPrison(target);
                    break;
            }
        }

        //雷狱内的悬停锚点(栓绳: 不许把玩家引出牢)
        private Vector2 ClampToPrison(Vector2 pos, float margin = 160f) {
            if (!enteredPhase2 || ArenaCenter == Vector2.Zero)
                return pos;
            Vector2 rel = pos - ArenaCenter;
            float maxR = PrisonRadius - margin;
            if (rel.LengthSquared() > maxR * maxR)
                pos = ArenaCenter + rel.SafeNormalize(Vector2.Zero) * maxR;
            return pos;
        }

        // ===== 连接拍: 僵直漂浮 + 选招 =====
        private void Run_Reposition(Player target) {
            Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -300));
            NPC.Center += (hover - NPC.Center) * 0.07f;
            NPC.velocity *= 0.9f;

            int wait = IsFinalPhase ? 14 : 24;
            if (AttackTimer > wait) {
                //阶段转换：50% 一次性进入雷狱（改规则，而非加数值）
                if (!enteredPhase2 && NPC.life <= NPC.lifeMax * 0.5f) {
                    Phase = BossPhase.Transition;
                    SubStep = 0;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                    return;
                }

                attackIndex++;
                Attack[] rotation = IsFinalPhase ? FinalRotation : (enteredPhase2 ? Phase2Rotation : Phase1Rotation);
                Attack next = rotation[((attackIndex % rotation.Length) + rotation.Length) % rotation.Length];

                //玩家过远：用僵尸跳贴近（位置博弈，而非提高 DPS）
                if (Vector2.Distance(target.Center, NPC.Center) > 1500f) {
                    next = Attack.JiangshiHop;
                }
                SetAttack(next);
            }
        }

        // ============================================================
        //  雷狱降临(50% 换阶段演出): 收锤升空 → 三声军鼓钉雷矛 → 静默 → 合拢 impact
        // ============================================================
        private void RunTransition(Player target) {
            NPC.dontTakeDamage = true;
            HammersChanneling = true;
            HammerOrbit += 0.09f; //收拢锤加速环转

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    ArenaCenter = target.Center;
                    if (!VaultUtils.isClient)
                        ClearHostileProjectiles();
                    CommandAllHammers(4);
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                    Announce("ThunderPrison", TelegraphColors.Lightning);
                    NPC.netUpdate = true;
                }
                //升空至场心上方
                Vector2 rise = ArenaCenter + new Vector2(0, -340);
                NPC.Center += (rise - NPC.Center) * 0.07f;
                NPC.velocity *= 0.9f;
                if (AttackTimer > 60) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                NPC.velocity *= 0.9f;
                //三声军鼓: 每 40 帧一声, 每声在边界钉入 4 根雷矛(共 12 根 30° 均布)
                for (int drum = 0; drum < 3; drum++) {
                    if (AttackTimer == 1 + drum * 40) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.5f + drum * 0.15f, Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(5f + drum * 2f);
                        JiangcenSky.TriggerFlash(0.5f + drum * 0.25f);
                        JiangcenThunderPrisonSystem.Pulse(ArenaCenter, 0.4f + drum * 0.15f, TelegraphColors.Lightning);
                        if (!VaultUtils.isClient) {
                            for (int k = 0; k < 4; k++) {
                                float ang = MathHelper.ToRadians(drum * 30f + k * 90f + 15f);
                                Vector2 spot = ArenaCenter + ang.ToRotationVector2() * PrisonRadius;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), spot, Vector2.Zero,
                                    ModContent.ProjectileType<JiangcenLightningStrike>(), 0, 0f, Main.myPlayer, 0, 2, 1600);
                            }
                        }
                    }
                }
                if (AttackTimer > 125) {
                    SubStep = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 2) {
                //静默收拢: 尖啸前的吸气, 一切熄灭
                NPC.velocity *= 0.85f;
                if (AttackTimer > 30) {
                    SubStep = 3;
                    AttackTimer = 0;
                    //雷牢合拢 impact
                    prisonVis = 1f;
                    enteredPhase2 = true;
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(14f);
                    JiangcenThunderPrisonSystem.FlashWhite(0.75f);
                    JiangcenThunderPrisonSystem.Pulse(ArenaCenter, 1f, TelegraphColors.Lightning);
                    JiangcenSky.TriggerFlash(1.4f);
                    NPC.netUpdate = true;
                }
            }
            else {
                //落回战位, 给玩家缓冲
                Vector2 back = ClampToPrison(target.Center + new Vector2(0, -300));
                NPC.Center += (back - NPC.Center) * 0.06f;
                NPC.velocity *= 0.9f;
                if (AttackTimer == 30) {
                    CommandAllHammers(0);
                    HammersChanneling = false;
                }
                if (AttackTimer > 55) {
                    NPC.dontTakeDamage = false;
                    Phase = BossPhase.Phase2;
                    attackIndex = -1;
                    SetAttack(Attack.Reposition);
                    AttackTimer = -30; //首招额外缓冲
                }
            }
        }

        // ============================================================
        //  死亡演出: 坠地 → 六锤失能 → 挣扎 → 天罚预兆 → 静默 → 六雷轰顶 → 溶解
        // ============================================================
        private void RunDeath() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            AttackTimer++;

            if (SubStep == 0) {
                //失能坠地(重力), 落地弹跳一次
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    NPC.velocity = new Vector2(NPC.velocity.X * 0.3f, -3f);
                }
                NPC.velocity.X *= 0.97f;
                NPC.velocity.Y = Math.Min(NPC.velocity.Y + 0.5f, 13f);
                float groundY = GetGroundY(NPC.Center.X, NPC.Center.Y - 60);
                if (NPC.Bottom.Y >= groundY - 4 && NPC.velocity.Y > 0) {
                    if (NPC.localAI[0] == 0) {
                        //第一次触地: 弹跳
                        NPC.localAI[0] = 1;
                        NPC.velocity.Y = -4.5f;
                        NPC.velocity.X *= 0.5f;
                        ACMScreenShakeSystem.Add(7f);
                        SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.6f }, NPC.Center);
                        bodySquash = new Vector2(1.25f, 0.72f);
                        SpawnGroundDust(18);
                    }
                    else {
                        //落定
                        NPC.velocity = Vector2.Zero;
                        NPC.Bottom = new Vector2(NPC.Bottom.X, groundY);
                        SubStep = 1;
                        AttackTimer = 0;
                        bodySquash = new Vector2(1.15f, 0.82f);
                        NPC.netUpdate = true;
                    }
                }
                if (AttackTimer > 120) { //高空打死的保底
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                //挣扎: 两次缓慢撑起又跪落(呼吸感), 电弧失控
                NPC.velocity = Vector2.Zero;
                float t = AttackTimer / 90f;
                float rise = JiangcenVFX.Bump(t * 2f % 1f) * 14f;
                bodySquash = Vector2.Lerp(bodySquash, new Vector2(1f - rise * 0.006f, 1f + rise * 0.006f), 0.2f);
                if (!VaultUtils.isServer && generalTimer % 3 == 0) {
                    int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0, 0, 120, default, Main.rand.NextFloat(1f, 2f));
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = Main.rand.NextVector2Circular(3, 3);
                }
                if (AttackTimer == 30 || AttackTimer == 72) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.6f, Volume = 1.1f }, NPC.Center);
                }
                if (AttackTimer > 90) {
                    SubStep = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 2) {
                //天罚预兆: 收束预告 + 递升轰鸣
                NPC.velocity = Vector2.Zero;
                float t = MathHelper.Clamp(AttackTimer / 60f, 0f, 1f);
                ACMScreenShakeSystem.Add(t * t * 5f);
                if (AttackTimer % 12 == 0) {
                    JiangcenSky.TriggerFlash(0.3f + t * 0.5f);
                }
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                }
                if (AttackTimer > 60) {
                    SubStep = 3;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 3) {
                //静默 12 帧(一切熄灭)
                NPC.velocity = Vector2.Zero;
                if (AttackTimer > 12) {
                    SubStep = 4;
                    AttackTimer = 0;
                    //六雷轰顶 impact
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(16f);
                    JiangcenThunderPrisonSystem.FlashWhite(0.95f);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Center, 1f, Color.White);
                    JiangcenSky.TriggerFlash(1.5f);
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 off = new Vector2((i - 2.5f) * 34f, 0);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + off, Vector2.Zero,
                                ModContent.ProjectileType<JiangcenLightningStrike>(), 0, 0f, Main.myPlayer, 0, 2, 1600);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                //溶解崩解 → 真死
                NPC.velocity = Vector2.Zero;
                deathDissolve = MathHelper.Clamp(AttackTimer / 82f, 0f, 1f);
                if (!VaultUtils.isServer && generalTimer % 2 == 0 && deathDissolve < 1f) {
                    //灰烬上升
                    int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Ash, 0, -2.5f, 130, default, Main.rand.NextFloat(1f, 1.8f));
                    Main.dust[d].noGravity = true;
                    int e = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0, -1f, 150, default, 1.2f);
                    Main.dust[e].noGravity = true;
                }
                if (AttackTimer > 88 && !VaultUtils.isClient) {
                    NPC.life = 0;
                    NPC.checkDead(); //Phase==Death → CheckDead 放行 → OnKill/掉落
                    NPC.netUpdate = true;
                }
            }
        }

        private void SpawnGroundDust(int count) {
            if (VaultUtils.isServer)
                return;
            for (int i = 0; i < count; i++) {
                int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 8), NPC.width, 14, DustID.Smoke, 0, 0, 120, default, 2f);
                Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-6, -1));
            }
        }

        // ============================================================
        //  攻击 1: 点将猛砸 —— 本体逐柄点将, 六锤受命猛砸
        // ============================================================
        private void Run_HammerSlam(Player target) {
            HammersChanneling = true;
            Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -260));
            NPC.Center += (hover - NPC.Center) * 0.07f;
            NPC.velocity *= 0.9f;

            int slamCount = enteredPhase2 ? 6 : 4;
            int cadence = enteredPhase2 ? 30 : 40;
            bool pairs = IsFinalPhase; //终章两两齐发
            int[] order = { 0, 3, 1, 4, 2, 5 };

            if (AttackTimer == 6) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.4f }, NPC.Center);
            }

            for (int k = 0; k < slamCount; k++) {
                int fireAt = pairs ? 10 + (k / 2) * (cadence + 14) : 10 + k * cadence;
                if (AttackTimer == fireAt) {
                    CommandHammer(order[k % 6], 1, onlyIdle: true);
                    armSnap = 10f; //点将抬臂 snap
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
                }
            }

            //蓄力汇聚粒子
            if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                Vector2 off = Main.rand.NextVector2CircularEdge(120, 120);
                int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.8f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 3f;
            }

            int chargeTime = enteredPhase2 ? 70 : 90;
            int lastFire = pairs ? 10 + ((slamCount - 1) / 2) * (cadence + 14) : 10 + (slamCount - 1) * cadence;
            if (AttackTimer > lastFire + chargeTime + 110) {
                HammersChanneling = false;
                SetAttack(Attack.Reposition);
            }
        }

        // ============================================================
        //  攻击 2: 僵尸三连跳 —— 蹲伏→弹射→空中僵直→直线下砸→震波
        //  SubStep: 0 蹲伏预告 / 1 腾空 / 2 空中定格 / 3 下砸(接触伤害窗) / 4 落地恢复
        // ============================================================
        private void Run_JiangshiHop(Player target) {
            HammersChanneling = false;
            bool bigHop = hopCount == 2; //第三跳是"帅跳"

            if (SubStep == 0) {
                int crouchTime = bigHop ? 48 : 40;
                NPC.velocity *= 0.8f;
                if (AttackTimer == 1) {
                    //预测落点(锁定于起跳前, 空中不追踪)
                    jumpTarget = target.Center + target.velocity * 18f;
                    jumpTarget = ClampToPrison(jumpTarget, 120f);
                    jumpTarget.Y = GetGroundY(jumpTarget.X, Math.Min(target.Center.Y, NPC.Center.Y) - 200) - NPC.height * 0.5f + 6;
                    SoundEngine.PlaySound(SoundID.DD2_GoblinBomberThrow with { Pitch = -0.3f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), jumpTarget, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 0, crouchTime + 48);
                    }
                    NPC.netUpdate = true;
                }
                //蹲伏压缩(蓄势), 末 8 帧 late-snap 下沉
                float ct = AttackTimer / (float)crouchTime;
                bodySquash = Vector2.Lerp(bodySquash, new Vector2(1.12f, 0.84f), 0.15f);
                if (ct > 0.8f) {
                    float sink = MathF.Pow((ct - 0.8f) / 0.2f, 3f);
                    bodySquash = new Vector2(1.12f + sink * 0.08f, 0.84f - sink * 0.06f);
                }
                if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                    int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 0), NPC.width, 8, DustID.Shadowflame, 0, -2f, 120, Color.DarkRed, 1.6f);
                    Main.dust[d].noGravity = true;
                }
                if (AttackTimer > crouchTime) {
                    SubStep = 1;
                    AttackTimer = 0;
                    //1 帧 set 起跳(launch is a set, not a ramp)
                    float dx = jumpTarget.X - NPC.Center.X;
                    NPC.velocity = new Vector2(MathHelper.Clamp(dx / 34f, -26f, 26f), bigHop ? -34f : -28f);
                    bodySquash = new Vector2(0.84f, 1.18f);
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(4f);
                    SpawnGroundDust(10);
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                //腾空: 手动重力, 后仰
                NPC.velocity.Y += 0.9f;
                NPC.velocity.X *= 0.995f;
                if (NPC.velocity.Y > -3f || AttackTimer > 40) {
                    SubStep = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 2) {
                //空中僵直定格(menace 拍): 悬停一瞬, 对准落点
                NPC.velocity *= 0.62f;
                bodySquash = Vector2.Lerp(bodySquash, Vector2.One, 0.2f);
                if (AttackTimer > 10) {
                    SubStep = 3;
                    AttackTimer = 0;
                    //直线下砸(1 帧 set), 伤害窗开启
                    NPC.velocity = (jumpTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * (bigHop ? 50f : 44f);
                    bodySquash = new Vector2(0.8f, 1.24f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 1.1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 3) {
                //下砸: 全程接触伤害(伤害窗与视觉严格对齐)
                NPC.damage = GetBossDamage(1f);
                if (Vector2.Distance(NPC.Center, jumpTarget) < 48f || NPC.Center.Y >= jumpTarget.Y - 10f || AttackTimer > 36) {
                    SubStep = 4;
                    AttackTimer = 0;
                    NPC.Center = jumpTarget;
                    NPC.velocity = Vector2.Zero;
                    bodySquash = new Vector2(1.3f, 0.7f);
                    //落地震波
                    SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(bigHop ? 12f : 10f);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Bottom, 0.6f, JiangcenVFX.CorpseRed);
                    if (!VaultUtils.isClient) {
                        int bolts = Main.masterMode ? 14 : (Main.expertMode ? 11 : 8);
                        float baseSpeed = Main.expertMode ? 10.5f : 9f;
                        //双层交错震波环(内快外慢, 缝隙可读)
                        for (int i = 0; i < bolts; i++) {
                            float a = MathHelper.TwoPi * i / bolts;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, a.ToRotationVector2() * baseSpeed,
                                ModContent.ProjectileType<JiangcenShockBolt>(), GetBossDamage(0.8f), 2f, Main.myPlayer, baseSpeed);
                            float b = a + MathHelper.Pi / bolts;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, b.ToRotationVector2() * baseSpeed * 0.62f,
                                ModContent.ProjectileType<JiangcenShockBolt>(), GetBossDamage(0.75f), 2f, Main.myPlayer, baseSpeed * 0.62f);
                        }
                        //帅跳(第三跳)在雷狱阶段追加十字短震波
                        if (bigHop && enteredPhase2) {
                            for (int i = 0; i < 4; i++) {
                                float a = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, a.ToRotationVector2() * baseSpeed * 1.35f,
                                    ModContent.ProjectileType<JiangcenShockBolt>(), GetBossDamage(0.85f), 2f, Main.myPlayer, baseSpeed * 1.35f);
                            }
                        }
                    }
                    if (!VaultUtils.isServer) {
                        SpawnGroundDust(22);
                        for (int i = 0; i < 20; i++) {
                            int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 8), NPC.width, 14, DustID.Shadowflame, 0, 0, 100, Color.DarkRed, 2.4f);
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-7, 7), Main.rand.NextFloat(-9, -2));
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                //落地后 6 帧仍有接触伤害(碾在落点上的判定与视觉一致), 之后恢复
                NPC.damage = AttackTimer <= 6 ? GetBossDamage(1f) : 0;
                NPC.velocity *= 0.85f;
                if (AttackTimer > 22) {
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

        // ============================================================
        //  攻击 3: 雷锤回旋 —— 反向拉弓蓄势 → 掷锤反冲 → 两段躲
        // ============================================================
        private void Run_ThunderHammerThrow(Player target) {
            HammersChanneling = false;
            if (SubStep == 0) {
                //拉弓: 悬停点随蓄力向玩家反方向漂移(drift-back)
                float t = MathHelper.Clamp(AttackTimer / 46f, 0f, 1f);
                float side = target.Center.X < NPC.Center.X ? 1f : -1f;
                Vector2 hover = ClampToPrison(target.Center + new Vector2(side * (260f + t * t * 160f), -180));
                NPC.Center += (hover - NPC.Center) * 0.08f;
                NPC.velocity *= 0.9f;

                //末 6 帧静默(inhale); 其余时间电荷汇聚
                bool silent = AttackTimer > 40;
                if (!VaultUtils.isServer && !silent && AttackTimer % 3 == 0) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(70, 70);
                    int d = Dust.NewDust(NPC.Center + off, 0, 0, DustID.Electric, 0, 0, 120, default, 1.7f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = -off.SafeNormalize(Vector2.Zero) * 3.5f;
                }
                if (AttackTimer == 6)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);

                if (AttackTimer > 46) {
                    SubStep = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(8f);
                    Vector2 dir = NPC.DirectionTo(target.Center);
                    //掷锤反冲(mass is reaction)
                    NPC.velocity = -dir * 7f;
                    armSnap = 10f;
                    if (!VaultUtils.isClient) {
                        int hammers = enteredPhase2 ? 3 : 2;
                        for (int i = 0; i < hammers; i++) {
                            float spread = MathHelper.ToRadians((i - (hammers - 1) / 2f) * 16f);
                            Vector2 v = dir.RotatedBy(spread) * 19f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, v,
                                ModContent.ProjectileType<JiangcenThrownHammer>(), GetBossDamage(1.1f), 3f, Main.myPlayer, NPC.whoAmI, enteredPhase2 ? 1 : 0);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -300));
                NPC.Center += (hover - NPC.Center) * 0.04f;
                //接锤时刻的小顿(约在回程末尾)
                if (AttackTimer == 96)
                    armSnap = 8f;
                if (AttackTimer > 130)
                    SetAttack(Attack.Reposition);
            }
        }

        // ============================================================
        //  攻击 4: 尸坟唤将 —— 军鼓两声 → 尸坟依次点亮 → 尸手波浪抓出
        // ============================================================
        private void Run_CorpseRain(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -340));
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                //两声军鼓(点兵的仪式感)
                if (AttackTimer == 6 || AttackTimer == 30) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = AttackTimer == 6 ? -0.4f : -0.2f, Volume = 1.2f }, NPC.Center);
                    ACMScreenShakeSystem.Add(3f);
                    armSnap = 8f;
                }
                if (AttackTimer == 1) {
                    float spacing = 230f;
                    float startX = target.Center.X - spacing * 2f;
                    for (int i = 0; i < 5; i++) {
                        float gx = startX + spacing * i + Main.rand.NextFloat(-40, 40);
                        float gy = GetGroundY(gx, target.Center.Y - 100);
                        graveMarks[i] = new Vector2(gx, gy);
                    }
                }
                //尸坟标记依次点亮(每 6 帧一座, 军列感)
                for (int i = 0; i < 5; i++) {
                    if (AttackTimer == 34 + i * 6) {
                        SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Pitch = -0.3f + i * 0.06f, Volume = 0.8f }, graveMarks[i]);
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 1, 60 - i * 6);
                        }
                    }
                }
                if (AttackTimer > 86) {
                    SubStep = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.NPCDeath2 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 5; i++) {
                            //波浪时序: 第 i 座延迟 i*9 帧(ripple, 玩家可沿波前奔跑)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenCorpseHand>(), GetBossDamage(1f), 2f, Main.myPlayer,
                                i * 9, 0, enteredPhase2 ? 400f : 320f);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                if (AttackTimer > 150)
                    SetAttack(Attack.Reposition);
            }
        }

        // ============================================================
        //  攻击 5: 将令雷印 —— 点名(跟随→锁定→落雷) + 镜像锤魂, 全程可操作
        // ============================================================
        private void Run_GeneralsOrder(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -320));
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    ArenaCenter = enteredPhase2 ? ArenaCenter : target.Center; //雷狱期沿用牢心为镜像中心
                    SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                    armSnap = 10f;
                    Announce("SealOrder", JiangcenVFX.GeneralGold);
                    JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.5f, JiangcenVFX.GeneralGold);
                    if (!VaultUtils.isClient) {
                        //雷印点名(取代旧版冻结): P1 一枚跟随本人, P2 追加镜像枚
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Bottom, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenSealMark>(), GetBossDamage(1.1f), 2f, Main.myPlayer, 0, NPC.whoAmI);
                        if (enteredPhase2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Bottom, Vector2.Zero,
                                ModContent.ProjectileType<JiangcenSealMark>(), GetBossDamage(1.1f), 2f, Main.myPlayer, 1, NPC.whoAmI);
                        }
                        //镜像锤魂(影子突袭)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenHammerGhost>(), GetBossDamage(1.2f), 2f, Main.myPlayer, 0, NPC.whoAmI);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenHammerGhost>(), GetBossDamage(1.2f), 2f, Main.myPlayer, 1, NPC.whoAmI);
                    }
                    NPC.netUpdate = true;
                }
                if (AttackTimer > 100) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                //锤魂突袭 + 雷印引爆自行走完; 本体等待收势
                if (AttackTimer > 90)
                    SetAttack(Attack.Reposition);
            }
        }

        // ============================================================
        //  攻击 6(雷狱): 链电电网 —— 4 锚菱形, 边错拍点亮, 再点对角线
        // ============================================================
        private void Run_ChainLightning(Player target) {
            HammersChanneling = false;
            NPC.velocity *= 0.9f;
            Vector2 hover = ClampToPrison(target.Center + new Vector2(0, -320));
            NPC.Center += (hover - NPC.Center) * 0.06f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f }, NPC.Center);
                    float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 4; i++) {
                        float a = baseRot + MathHelper.PiOver2 * i;
                        graveMarks[i] = ClampToPrison(target.Center + a.ToRotationVector2() * 470f, 60f);
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), graveMarks[i], Vector2.Zero,
                                ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 2, 60);
                        }
                    }
                }
                //锚点依次"钉入"脉冲(每 8 帧一个)
                for (int i = 0; i < 4; i++) {
                    if (AttackTimer == 20 + i * 8) {
                        SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.3f + i * 0.08f, Volume = 0.7f }, graveMarks[i]);
                        ACMScreenShakeSystem.Add(2f);
                        JiangcenThunderPrisonSystem.Pulse(graveMarks[i], 0.3f, TelegraphColors.Lightning);
                    }
                }
                if (AttackTimer > 56) {
                    SubStep = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item94 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        //四边错拍(边 i 延迟 i*12): 电网像风车一样依次通电, 玩家沿熄灭的边穿行
                        for (int i = 0; i < 4; i++) {
                            Vector2 a = graveMarks[i];
                            Vector2 b = graveMarks[(i + 1) % 4];
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), (a + b) * 0.5f, b - a,
                                ModContent.ProjectileType<JiangcenChainArc>(), GetBossDamage(1f), 2f, Main.myPlayer, 0, 0, i * 12);
                        }
                        //第二轮: 两条对角线(× 封口), 在四边跑完后点亮
                        for (int i = 0; i < 2; i++) {
                            Vector2 a = graveMarks[i];
                            Vector2 b = graveMarks[i + 2];
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), (a + b) * 0.5f, b - a,
                                ModContent.ProjectileType<JiangcenChainArc>(), GetBossDamage(1f), 2f, Main.myPlayer, 0, 0, 68 + i * 12);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                if (AttackTimer > 200)
                    SetAttack(Attack.Reposition);
            }
        }

        // ============================================================
        //  攻击 7(终章): 万雷点将 —— 走廊落雷三波, 安全缝提前标出
        // ============================================================
        private void Run_ThunderRollCall(Player target) {
            HammersChanneling = true;
            HammerOrbit += 0.06f;

            if (SubStep == 0) {
                //升至牢心上空 + 充能
                Vector2 podium = ArenaCenter + new Vector2(0, -380);
                NPC.Center += (podium - NPC.Center) * 0.08f;
                NPC.velocity *= 0.9f;
                if (AttackTimer == 1) {
                    Announce("RollCall", JiangcenVFX.GeneralGold);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = -0.25f, Volume = 1.2f }, NPC.Center);
                    CommandAllHammers(4);
                }
                float chargeT = MathHelper.Clamp(AttackTimer / 70f, 0f, 1f);
                ACMScreenShakeSystem.Add(chargeT * chargeT * 4f);
                if (AttackTimer % 14 == 0)
                    JiangcenSky.TriggerFlash(0.3f + chargeT * 0.4f);
                if (AttackTimer > 70) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                NPC.velocity *= 0.9f;
                float laneW = PrisonRadius * 2f / 7f;
                float laneX(int i) => ArenaCenter.X - PrisonRadius + laneW * (i + 0.5f);

                //波次 1: 奇数列
                if (AttackTimer == 1 && !VaultUtils.isClient) {
                    for (int i = 1; i < 7; i += 2)
                        SpawnCorridorStrike(laneX(i));
                }
                //波次 2: 偶数列
                if (AttackTimer == 88 && !VaultUtils.isClient) {
                    for (int i = 0; i < 7; i += 2)
                        SpawnCorridorStrike(laneX(i));
                }
                //波次 3 预告: 选定玩家最近列为安全缝, 提前 60 帧翠色标出
                if (AttackTimer == 176) {
                    safeLaneIndex = (int)MathHelper.Clamp(MathF.Round((target.Center.X - (ArenaCenter.X - PrisonRadius)) / laneW - 0.5f), 0, 6);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(laneX(safeLaneIndex), ArenaCenter.Y), Vector2.Zero,
                            ModContent.ProjectileType<JiangcenTelegraphMark>(), 0, 0f, Main.myPlayer, 4, 140);
                    }
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 1f }, target.Center);
                    NPC.netUpdate = true;
                }
                //波次 3: 全列齐落, 只留安全缝
                if (AttackTimer == 236) {
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 7; i++) {
                            if (i == safeLaneIndex)
                                continue;
                            SpawnCorridorStrike(laneX(i));
                        }
                    }
                    JiangcenSky.TriggerFlash(1.2f);
                }
                if (AttackTimer > 330) {
                    CommandAllHammers(0);
                    HammersChanneling = false;
                    SetAttack(Attack.Reposition);
                }
            }
        }

        private void SpawnCorridorStrike(float x) {
            Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, ArenaCenter.Y), Vector2.Zero,
                ModContent.ProjectileType<JiangcenLightningStrike>(), GetBossDamage(1.05f), 2f, Main.myPlayer, 0, 1, 1500);
        }

        // ============================================================
        //  攻击 8(雷狱): 六锤连狱 —— 六锤边界就位, 依次沿弦线对穿雷牢
        // ============================================================
        private void Run_HammerPrison(Player target) {
            HammersChanneling = true; //锤由指令驱动, 公转冻结

            //本体镇守牢心上空(channel 姿态)
            Vector2 podium = ArenaCenter + new Vector2(0, -360);
            NPC.Center += (podium - NPC.Center) * 0.07f;
            NPC.velocity *= 0.9f;

            if (SubStep == 0) {
                if (AttackTimer == 1) {
                    ChordBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    CommandAllHammers(5); //飞往边界槽位
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                    armSnap = 10f;
                    NPC.netUpdate = true;
                }
                if (AttackTimer > 44) {
                    SubStep = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubStep == 1) {
                //每 14 帧点一柄: 弦线预告(36f) + 对穿
                for (int i = 0; i < 6; i++) {
                    if (AttackTimer == 1 + i * 14) {
                        CommandHammer(i, 6);
                        armSnap = 8f;
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.1f + i * 0.06f, Volume = 0.7f }, NPC.Center);
                    }
                }
                if (AttackTimer > 1 + 5 * 14 + 36 + 110) {
                    SubStep = 2;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                if (AttackTimer > 30) {
                    CommandAllHammers(0);
                    HammersChanneling = false;
                    SetAttack(Attack.Reposition);
                }
            }
        }

        // ===== 雷狱边界雷霆：出界=被劈（位置型危险）=====
        private void Phase2BoundaryHazard(Player target) {
            //边界裂纹视觉
            if (!VaultUtils.isServer && generalTimer % 2 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edge = ArenaCenter + ang.ToRotationVector2() * PrisonRadius;
                if (Vector2.Distance(edge, Main.LocalPlayer.Center) < 1400f) {
                    int d = Dust.NewDust(edge, 0, 0, DustID.Electric, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 2.2f));
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = (ArenaCenter - edge).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);
                }
            }

            phase2HazardTimer++;
            if (phase2HazardTimer >= 55) {
                phase2HazardTimer = 0;
                if (Vector2.Distance(target.Center, ArenaCenter) > PrisonRadius) {
                    SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.3f }, target.Center);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center, Vector2.Zero,
                            ModContent.ProjectileType<JiangcenLightningStrike>(), GetBossDamage(1.1f), 2f, Main.myPlayer, 0, 0, 1100);
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

        // ============================================================
        //  绘制: squash&stretch + 速度门控残影 + 红目 + 缠电 + 溶解显形/崩解
        // ============================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();

            //点将抬臂 snap: 短促上抬回落的倾角
            float snapRot = -0.14f * JiangcenVFX.Bump(armSnap / 10f);
            float drawRot = NPC.rotation + snapRot + MathHelper.Clamp(NPC.velocity.X * 0.008f, -0.16f, 0.16f);
            Vector2 scaleVec = bodySquash * NPC.scale;

            //溶解态(仅入场显形 / 死亡崩解两个阶段; 其余阶段必须完整可见 — 兼容中途加入的客户端)
            float dissolve = Phase == BossPhase.Death ? deathDissolve
                : (Phase == BossPhase.Intro ? introDissolve : 0f);
            if (dissolve > 0.001f) {
                if (dissolve < 0.999f) {
                    DrawDissolveBody(spriteBatch, mainValue, rectangle, drawRot, scaleVec, dissolve);
                }
                //完全未显形/已消散: 不画本体
                DrawEyeGlow(spriteBatch);
                return false;
            }

            //速度门控残影(只有真的快才有影)
            if (NPC.velocity.LengthSquared() > 196f) {
                float sengs = 0.25f;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    spriteBatch.Draw(mainValue, drawOldPos, rectangle, (JiangcenVFX.CorpseRed with { A = 0 }) * sengs
                        , drawRot, rectangle.Size() / 2, scaleVec, SpriteEffects.None, 0);
                    sengs *= 0.78f;
                }
            }

            //尸气红边(rim): 微偏移的加性轮廓
            Color rim = JiangcenVFX.CorpseRed with { A = 0 } * (0.16f + 0.1f * eyeGlow);
            for (int i = 0; i < 4; i++) {
                Vector2 off = (MathHelper.PiOver2 * i).ToRotationVector2() * 3f;
                spriteBatch.Draw(mainValue, NPC.Center + off - Main.screenPosition, rectangle, rim,
                    drawRot, rectangle.Size() / 2, scaleVec, SpriteEffects.None, 0);
            }

            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , drawRot, rectangle.Size() / 2, scaleVec, SpriteEffects.None, 0);

            //周身缠电(蓄力/雷狱/终章)
            if (bodyArcGlow > 0.05f && MythologyConfig.Trail != TrailQualityLevel.Off) {
                JiangcenVFX.DrawBodyArcs(spriteBatch, NPC.Center, 84f, bodyArcGlow, NPC.whoAmI);
            }

            DrawEyeGlow(spriteBatch);
            DrawDeathOmen(spriteBatch);
            DrawPrisonWallArcs();
            return false;
        }

        //红目辉光(将臣血目 — 尸祖的身份灯)
        private void DrawEyeGlow(SpriteBatch sb) {
            if (eyeGlow <= 0.03f)
                return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;
            float pulse = 0.8f + 0.2f * (float)Math.Sin(generalTimer * 0.11f);
            Vector2 eyePos = NPC.Center + new Vector2(NPC.direction * 16f, -30f) * bodySquash;
            sb.Draw(glow, eyePos - Main.screenPosition, null,
                new Color(255, 40, 45, 0) * eyeGlow * pulse, 0f, glow.Size() / 2, 0.34f, SpriteEffects.None, 0);
            sb.Draw(glow, eyePos - Main.screenPosition, null,
                new Color(255, 150, 140, 0) * eyeGlow * pulse * 0.7f, 0f, glow.Size() / 2, 0.15f, SpriteEffects.None, 0);
        }

        //死亡天罚预兆: 六个收束预告环压向本体
        private void DrawDeathOmen(SpriteBatch sb) {
            if (Phase != BossPhase.Death || SubStep != 2)
                return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;
            float t = MathHelper.Clamp(AttackTimer / 60f, 0f, 1f);
            for (int i = 0; i < 6; i++) {
                float ang = MathHelper.TwoPi * i / 6f + generalTimer * 0.02f;
                float dist = MathHelper.Lerp(420f, 60f, t) ;
                Vector2 p = NPC.Center + ang.ToRotationVector2() * dist;
                Color c = TelegraphColors.Lightning with { A = 0 } * (0.25f + 0.55f * t);
                sb.Draw(glow, p - Main.screenPosition, null, c, 0f, glow.Size() / 2, 0.5f + 0.4f * t, SpriteEffects.None, 0);
            }
        }

        //溶解显形/崩解: DissolveBurn 专用批
        private void DrawDissolveBody(SpriteBatch sb, Texture2D tex, Rectangle src, float rot, Vector2 scaleVec, float dissolve) {
            Effect fx = ACMShaders.DissolveBurn;
            if (fx == null || !MythologyConfig.FullscreenShadersEnabled) {
                //降级: 淡入淡出
                sb.Draw(tex, NPC.Center - Main.screenPosition, src, Color.White * (1f - dissolve),
                    rot, src.Size() / 2, scaleVec, SpriteEffects.None, 0);
                return;
            }

            Color edge = Phase == BossPhase.Death ? new Color(255, 120, 90) : TelegraphColors.Lightning;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(dissolve);
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.4f);
            fx.Parameters["uEdgeColor"]?.SetValue(edge.ToVector4());
            fx.Parameters["uDirection"]?.SetValue(Phase == BossPhase.Death ? new Vector2(0f, -1f) : new Vector2(0f, 1f));
            fx.Parameters["uSweepStrength"]?.SetValue(0.35f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(tex, NPC.Center - Main.screenPosition, src, Color.White, rot, src.Size() / 2, scaleVec, SpriteEffects.None, 0);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        // ===== 雷牢墙体电弧: 沿环形边界的跳变电弧段(专属着色器), 失稳期闪断 =====
        private void DrawPrisonWallArcs() {
            if (Main.dedServ || prisonVis < 0.25f || ArenaCenter == Vector2.Zero)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            float r = PrisonRadius;
            float time = (float)Main.GlobalTimeWrappedHourly;
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            const int segs = 12;
            float baseAng = time * 0.1f;

            JiangcenVFX.BeginArcs(Main.spriteBatch);
            for (int i = 0; i < segs; i++) {
                //每段独立频闪, 同帧约半数点亮(雷墙跳动感, 省批); 失稳期整段断电
                float flick = (float)Math.Sin(time * 9f + i * 2.3f) * (float)Math.Sin(time * 3.1f + i * 5.7f);
                if (flick < 0.35f - instabilityVis * 0.3f)
                    continue;
                if (instabilityVis > 0.05f && (float)Math.Sin(time * 17f + i * 3.1f) > 0.85f - instabilityVis * 0.5f)
                    continue; //失稳闪断

                float a0 = baseAng + MathHelper.TwoPi * i / segs;
                float a1 = a0 + MathHelper.TwoPi / segs * 0.94f;
                Vector2 p0 = ArenaCenter + a0.ToRotationVector2() * r;
                Vector2 p1 = ArenaCenter + a1.ToRotationVector2() * r;

                if (Vector2.Distance((p0 + p1) * 0.5f, screenCenter) > Main.screenWidth)
                    continue;

                float intensity = MathHelper.Clamp(prisonVis * (0.4f + 0.6f * flick), 0f, 1f) * 0.85f;
                Color core = Color.Lerp(TelegraphColors.Lightning, JiangcenVFX.CorpseRed, instabilityVis * 0.5f) with { A = 220 };
                JiangcenVFX.Arc(p0, p1, 20f + 8f * flick, core, JiangcenVFX.ArcBlue with { A = 80 },
                    intensity, i * 7.3f, 0.26f, 9f);
            }
            JiangcenVFX.EndArcs(Main.spriteBatch);
        }
    }

    // ============================================================
    //  将臣雷暴天幕(V3 重写): JiangcenStormSky 程序化雷暴云 + 云内放电 + 远景闪电
    //  注册名与 LoadInstance 签名保持不变(ACMMod.Load 调用)
    // ============================================================
    internal class JiangcenSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float globalTime;
        private float phaseAmt;   //0=常态雷暴 1=雷狱相
        private float deathDim;
        private float flash;

        //Boss 每帧发布的目标标量(客户端)
        private static float phaseTarget;
        private static float deathTarget;
        private static float pendingFlash;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:JiangcenSky";
            SkyManager.Instance[name] = new JiangcenSky();
        }

        /// <summary>大节拍天幕亮拍(军鼓/合拢/天罚), 取 max 待消费。</summary>
        public static void TriggerFlash(float amount) {
            if (amount > pendingFlash)
                pendingFlash = amount;
        }

        /// <summary>由 Boss 每帧发布雷狱相/死亡压暗目标。</summary>
        public static void PublishState(float phase, float death) {
            phaseTarget = phase;
            deathTarget = death;
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = Math.Max(intensity, 0.01f);
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() {
            return active || intensity > 0.01f;
        }

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //消费亮拍并衰减
            if (pendingFlash > flash)
                flash = pendingFlash;
            pendingFlash = 0f;
            flash = MathHelper.Lerp(flash, 0f, 0.075f);

            NPC boss = GetBoss();
            if (boss != null) {
                intensity = MathHelper.Min(1f, intensity + 0.02f);
                phaseAmt = MathHelper.Lerp(phaseAmt, phaseTarget, 0.03f);
                deathDim = MathHelper.Lerp(deathDim, deathTarget, 0.05f);
                active = true;
            }
            else {
                intensity = MathHelper.Max(0f, intensity - 0.012f);
                deathDim = MathHelper.Lerp(deathDim, 0f, 0.02f);
                if (intensity <= 0f) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            float darken = intensity * (0.38f + phaseAmt * 0.14f + deathDim * 0.1f);
            return inColor * (1f - darken);
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.85f;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //仅最远背景层
            if (!(maxDepth >= 0 && minDepth < 0) || intensity <= 0.01f)
                return;

            Effect fx = MythologyConfig.FullscreenShadersEnabled ? JiangcenVFX.SkyEffect : null;
            if (fx == null) {
                DrawFallback(spriteBatch);
                return;
            }

            //Boss 屏幕归一化坐标(血晕锚点)
            Vector2 bossUV = new(0.5f, 0.42f);
            NPC boss = GetBoss();
            if (boss != null) {
                Vector2 sp = boss.Center - Main.screenPosition;
                bossUV = new Vector2(
                    MathHelper.Clamp(sp.X / Main.screenWidth, -0.5f, 1.5f),
                    MathHelper.Clamp(sp.Y / Main.screenHeight, -0.5f, 1.5f));
            }

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uPhase"]?.SetValue(MathHelper.Clamp(phaseAmt, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            fx.Parameters["uBossUV"]?.SetValue(bossUV);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1.5f));
            fx.Parameters["uDeath"]?.SetValue(MathHelper.Clamp(deathDim, 0f, 1f));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        }

        //着色器不可用时的纯色降级(旧版观感)
        private void DrawFallback(SpriteBatch spriteBatch) {
            float pulse = 0.85f + 0.15f * (float)Math.Sin(globalTime * 2f);
            Color col = Color.Lerp(new Color(15, 8, 30), new Color(96, 14, 20), 0.4f + phaseAmt * 0.2f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                col * (intensity * 0.55f * pulse));
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

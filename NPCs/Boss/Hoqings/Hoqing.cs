using AncientChineseMythology.Buffs;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Players;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    /// <summary>
    /// 后卿 Hoqing —— 旱魃换皮的结构性重写。
    /// 主题：瘟疫 / 尸火 / 万鬼夜行（旱灾之疫的亡灵神祇）。
    /// 三幕脚本化 HP 门控，每一幕改变战斗规则而非加速喷弹：
    ///   幕一 幽火列阵：角色化幽火仆从（枪兵/爆兵/疫医） + 预告尸坑 + 列阵冲撞。
    ///   幕二 疫疠扩散（≤70%）：移动战，三套预告弹幕轮转（脓雨潭 / 尸链复生 / 疫风间隙）。
    ///   幕三 万鬼夜行（≤30%）：四祭坛锚点瞬掠 + 蓄力（祭坛辉光预告扇形/360） + 近身衰朽叠层。
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

        //====== 幕 / 阶段系统 ======
        public enum BossPhase
        {
            Despawn = -1,
            Intro = 0,
            GhostArray = 1,     //幽火列阵
            Transition = 2,     //过渡（i 帧节拍）
            PlagueSpread = 3,   //疫疠扩散
            NightMarch = 4,     //万鬼夜行
        }

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float GeneralTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        //同步字段
        private BossPhase pendingNextPhase;
        private bool enteredP2;
        private bool enteredP3;
        private int patternIndex;   //P2 当前弹幕图案
        private int altarIndex;     //P3 当前祭坛
        private int comboCount;     //通用幕内循环计数
        private Vector2 arenaCenter; //P3 锚点中心
        private Vector2 laneDir;     //P1 列阵冲撞方向

        //非同步演出 (各端各算, 纯本地视觉)
        private float channelGlow;
        private float plagueAccum;   //幕三疫源累积 0~1 (地纹/经络主控)
        private float fogWarp;       //幕三限视尸雾 0~1 (GenericWarp·fog 全屏后处理)

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
            //专属 BGM：停止复用旱魃曲，改用更契合"万鬼夜行"主题的地府主题。
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HoqingFireSummon>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedHoqing = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)pendingNextPhase);
            writer.Write(enteredP2);
            writer.Write(enteredP3);
            writer.Write(patternIndex);
            writer.Write(altarIndex);
            writer.Write(comboCount);
            writer.WriteVector2(arenaCenter);
            writer.WriteVector2(laneDir);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            pendingNextPhase = (BossPhase)reader.ReadInt32();
            enteredP2 = reader.ReadBoolean();
            enteredP3 = reader.ReadBoolean();
            patternIndex = reader.ReadInt32();
            altarIndex = reader.ReadInt32();
            comboCount = reader.ReadInt32();
            arenaCenter = reader.ReadVector2();
            laneDir = reader.ReadVector2();
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

        private int GetBossDamage(float scaling = 1f) => (int)(NPC.damage * scaling);

        private void TransitionTo(BossPhase next) {
            Phase = BossPhase.Transition;
            pendingNextPhase = next;
            PhaseTimer = 0;
            SubState = 0;
            comboCount = 0;
            NPC.netUpdate = true;
        }

        public override void AI() {
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
            Lighting.AddLight(NPC.Center, new Color(80, 200, 110).ToVector3() * NPC.scale);

            if (GeneralTimer == 0 && !VaultUtils.isServer && !SkyManager.Instance[HoqingSky.name].IsActive()) {
                SkyManager.Instance.Activate(HoqingSky.name);
            }

            NPC.damage = NPC.defDamage;
            int targetFrame = 0;
            bool setNPCRot = true;

            switch (Phase) {
                case BossPhase.Despawn:
                    NPC.velocity = new Vector2(0, 60);
                    PhaseTimer++;
                    if (PhaseTimer > 180) {
                        NPC.active = false;
                        NPC.netUpdate = true;
                    }
                    break;
                case BossPhase.Intro:
                    RunIntro(target, ref targetFrame);
                    break;
                case BossPhase.GhostArray:
                    RunGhostArray(target, ref targetFrame);
                    break;
                case BossPhase.Transition:
                    RunTransition(target, ref targetFrame, ref setNPCRot);
                    break;
                case BossPhase.PlagueSpread:
                    RunPlagueSpread(target, ref targetFrame);
                    break;
                case BossPhase.NightMarch:
                    RunNightMarch(target, ref targetFrame, ref setNPCRot);
                    break;
            }

            //HP 门控：过阈值改变规则（带 i 帧过渡节拍），而非加速
            if (!VaultUtils.isClient) {
                if (!enteredP2 && NPC.life <= NPC.lifeMax * 0.7f
                    && (Phase == BossPhase.GhostArray)) {
                    enteredP2 = true;
                    TransitionTo(BossPhase.PlagueSpread);
                }
                else if (!enteredP3 && NPC.life <= NPC.lifeMax * 0.3f
                    && (Phase == BossPhase.PlagueSpread)) {
                    enteredP3 = true;
                    TransitionTo(BossPhase.NightMarch);
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

        //========================= V2 演出标量 (幕三万鬼夜行高潮) =========================
        //各端各算的本地视觉标量; 幕三推进时累积疫源地纹/经络/尸雾, 并发布给 HoqingScreenSystem。
        private void UpdatePresentation() {
            bool nightMarch = Phase == BossPhase.NightMarch;
            if (nightMarch) {
                plagueAccum = MathHelper.Clamp(plagueAccum + 1f / 540f, 0f, 1f); //~9s 满
                fogWarp = MathHelper.Lerp(fogWarp, 0.55f, 0.03f);
            }
            else {
                plagueAccum = MathHelper.Lerp(plagueAccum, 0f, 0.05f);
                fogWarp = MathHelper.Lerp(fogWarp, 0f, 0.05f);
            }

            if (!VaultUtils.isServer && nightMarch) {
                bool isFan = altarIndex % 2 == 0;
                HoqingScreenSystem.Publish(arenaCenter, 520f, plagueAccum,
                    altarIndex, channelGlow, isFan, (float)Main.GlobalTimeWrappedHourly);
            }
        }

        //========================= 幕零：入场 =========================
        private void RunIntro(Player target, ref int targetFrame) {
            targetFrame = 3;
            NPC.dontTakeDamage = true; //入场 i 帧
            Vector2 desired = target.Center + new Vector2(0, -360);
            NPC.Center = Vector2.Lerp(NPC.Center, desired, 0.08f);
            NPC.velocity *= 0.9f;

            if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(90, 90);
                    Dust d = Dust.NewDustPerfect(NPC.Center + off, DustID.GreenTorch
                        , -off.SafeNormalize(Vector2.Zero) * 3f, 120, new Color(120, 255, 140), 1.8f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                ACMScreenShakeSystem.Add(9f);
            }

            if (PhaseTimer > 120) {
                NPC.dontTakeDamage = false;
                Phase = BossPhase.GhostArray;
                PhaseTimer = 0;
                SubState = 0;
                comboCount = 0;
                NPC.netUpdate = true;
            }
        }

        //========================= 幕一：幽火列阵 =========================
        //角色化幽火仆从 + 预告尸坑 + 列阵冲撞（沿预告线冲）。攻击轮转，70% 血进入幕二。
        private void RunGhostArray(Player target, ref int targetFrame) {
            //保证仆从存在（枪兵/爆兵/疫医）。被清空时补阵。
            if (!VaultUtils.isClient && PhaseTimer == 1 && !NPC.AnyNPCs(ModContent.NPCType<GhostFire>())) {
                int count = 6;
                for (int i = 0; i < count; i++) {
                    NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center
                        , ModContent.NPCType<GhostFire>(), ai0: NPC.whoAmI, ai1: i, target: NPC.target);
                }
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.4f }, NPC.Center);
            }

            //SubState：0 布坑+游走  →  1 列阵冲撞（telegraph→charge）→ 回到 0
            switch ((int)SubState) {
                case 0: {
                    targetFrame = 1;
                    //缓慢逼近游走，保持压迫
                    Vector2 hover = target.Center + new Vector2((target.Center.X < NPC.Center.X ? 1 : -1) * 320, -180);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 9f, 0.05f);
                    NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

                    //预告尸坑：在玩家落点附近播种（先预告再喷发，见 CorpsePit）
                    if (!VaultUtils.isClient && PhaseTimer % 80 == 40) {
                        Vector2 pit = target.Center + new Vector2(Main.rand.Next(-360, 360), Main.rand.Next(-200, 200));
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pit, Vector2.Zero
                            , ModContent.ProjectileType<CorpsePit>(), GetBossDamage(0.8f), 0f, Main.myPlayer);
                        SoundEngine.PlaySound(SoundID.Item104 with { Pitch = -0.3f }, pit);
                    }

                    if (PhaseTimer > 200) {
                        SubState = 1;
                        PhaseTimer = 0;
                        if (!VaultUtils.isClient) {
                            laneDir = NPC.SafeDirectionTo(target.Center);
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
                case 1: {
                    //列阵冲撞：先沿冲撞线铺设尘线预告，再沿线高速冲（取代随机冲刺）
                    if (PhaseTimer < 45) {
                        targetFrame = 3;
                        NPC.velocity *= 0.86f;
                        //尘线预告
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 26; i++) {
                                Vector2 p = NPC.Center + laneDir * (i * 70f);
                                Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, Vector2.Zero, 150, new Color(150, 255, 160), 1.6f);
                                d.noGravity = true;
                                d.velocity = laneDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1f, 1f);
                            }
                        }
                        if (PhaseTimer == 1) {
                            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                        }
                    }
                    else if (PhaseTimer == 45) {
                        NPC.velocity = laneDir * 40f;
                        NPC.oldPos = new Vector2[NPC.oldPos.Length];
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(7f);
                    }
                    else {
                        targetFrame = 4;
                        NPC.velocity *= 0.97f;
                        //冲撞残影
                        if (!VaultUtils.isClient && PhaseTimer % 4 == 0) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero
                                , ModContent.ProjectileType<HoqingShadow>(), GetBossDamage(0.5f), 1f, Main.myPlayer, 0, 0, NPC.whoAmI);
                        }
                        if (PhaseTimer > 95 || NPC.collideX || NPC.collideY) {
                            SubState = 0;
                            PhaseTimer = 0;
                            comboCount++;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
            }
        }

        //========================= 过渡：i 帧节拍 =========================
        private void RunTransition(Player target, ref int targetFrame, ref bool setNPCRot) {
            targetFrame = 3;
            setNPCRot = false;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);

            if (PhaseTimer == 1) {
                ClearHostileBullets();
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.4f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
            }
            //疠气爆涌的演出
            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 8; i++) {
                    Vector2 v = Main.rand.NextVector2Circular(7, 7);
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch, v, 120, new Color(120, 255, 150), 2.2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer > 80) {
                NPC.dontTakeDamage = false;
                Phase = pendingNextPhase;
                PhaseTimer = 0;
                SubState = 0;
                comboCount = 0;
                if (pendingNextPhase == BossPhase.NightMarch && !VaultUtils.isClient) {
                    arenaCenter = target.Center;
                    altarIndex = 0;
                }
                NPC.netUpdate = true;
            }
        }

        //========================= 幕二：疫疠扩散 =========================
        //移动战，三套预告弹幕轮转：脓雨潭 / 尸链复生 / 疫风间隙。30% 血进入幕三。
        private void RunPlagueSpread(Player target, ref int targetFrame) {
            targetFrame = 1;
            //悬浮于玩家上方，持续侧移
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GeneralTimer * 0.03f) * 360f, -300f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hover) * 14f, 0.06f);
            NPC.spriteDirection = NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            //每套图案占 150 tick：前 50 预告，50 释放，50 收尾
            const int patternLen = 150;
            int localT = (int)PhaseTimer % patternLen;

            if (PhaseTimer % patternLen == 0 && !VaultUtils.isClient) {
                patternIndex = (patternIndex + 1) % 3;
                NPC.netUpdate = true;
            }

            switch (patternIndex) {
                case 0:
                    Pattern_SputumRain(target, localT, ref targetFrame);
                    break;
                case 1:
                    Pattern_CorpseChain(target, localT, ref targetFrame);
                    break;
                case 2:
                    Pattern_PlagueWind(target, localT, ref targetFrame);
                    break;
            }
        }

        //脓雨潭：高空预告落点 → 落下脓潭（持续绿池），强迫走位
        private void Pattern_SputumRain(Player target, int localT, ref int targetFrame) {
            if (localT < 50) {
                targetFrame = 3;
                //预告落点尘环
                if (!VaultUtils.isServer && localT % 5 == 0) {
                    for (int k = 0; k < 3; k++) {
                        Vector2 mark = target.Center + new Vector2((k - 1) * 260, 0);
                        for (int i = 0; i < 10; i++) {
                            Vector2 e = (MathHelper.TwoPi * i / 10).ToRotationVector2() * 70f;
                            Dust d = Dust.NewDustPerfect(mark + e, DustID.GreenTorch, Vector2.Zero, 150, new Color(120, 255, 130), 1.4f);
                            d.noGravity = true;
                        }
                    }
                }
            }
            else if (localT == 50) {
                SoundEngine.PlaySound(SoundID.Item104, NPC.Center);
                if (!VaultUtils.isClient) {
                    for (int k = 0; k < 5; k++) {
                        Vector2 mark = target.Center + new Vector2((k - 2) * 230, Main.rand.Next(-40, 40));
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), mark, Vector2.Zero
                            , ModContent.ProjectileType<SputumPool>(), GetBossDamage(0.7f), 0f, Main.myPlayer);
                    }
                }
            }
        }

        //尸链复生：朝玩家位置投出尸链，命中则在该处复生一名幽火仆从
        private void Pattern_CorpseChain(Player target, int localT, ref int targetFrame) {
            if (localT < 50) {
                targetFrame = 3;
                NPC.velocity *= 0.95f;
                if (!VaultUtils.isServer && localT % 4 == 0) {
                    Vector2 dir = NPC.SafeDirectionTo(target.Center);
                    Dust d = Dust.NewDustPerfect(NPC.Center + dir * 60, DustID.GreenTorch, dir * 2f, 120, new Color(180, 255, 180), 1.6f);
                    d.noGravity = true;
                }
            }
            else if (localT == 50) {
                SoundEngine.PlaySound(SoundID.Item102 with { Pitch = -0.3f }, NPC.Center);
                if (!VaultUtils.isClient) {
                    //仅在仆从不过载时尝试复生
                    bool canRevive = NPC.CountNPCS(ModContent.NPCType<GhostFire>()) < 4;
                    Vector2 vel = NPC.SafeDirectionTo(target.Center) * 17f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel
                        , ModContent.ProjectileType<HoqingCorpseChain>(), GetBossDamage(0.9f), 2f
                        , Main.myPlayer, ai0: NPC.whoAmI, ai1: canRevive ? 1f : 0f);
                }
            }
        }

        //疫风间隙：横向疫风墙带一个缺口，站进缺口躲避
        private void Pattern_PlagueWind(Player target, int localT, ref int targetFrame) {
            if (localT < 50) {
                targetFrame = 3;
                NPC.velocity *= 0.9f;
                //预告墙位与缺口
                if (!VaultUtils.isServer && localT % 4 == 0) {
                    float gapX = target.Center.X + ((localT / 4) % 2 == 0 ? 1 : -1) * 0f;
                    for (int i = -7; i <= 7; i++) {
                        Vector2 p = new Vector2(target.Center.X + i * 130, NPC.Center.Y + 80);
                        Dust d = Dust.NewDustPerfect(p, DustID.GreenTorch, Vector2.UnitY * 2f, 150, new Color(140, 255, 150), 1.3f);
                        d.noGravity = true;
                    }
                }
            }
            else if (localT == 50) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f }, NPC.Center);
                if (!VaultUtils.isClient) {
                    int gap = Main.rand.Next(-5, 6); //缺口列
                    float baseY = NPC.Center.Y + 60;
                    for (int i = -7; i <= 7; i++) {
                        if (Math.Abs(i - gap) <= 1) {
                            continue; //留出可站立缝隙
                        }
                        Vector2 p = new Vector2(target.Center.X + i * 130, baseY - 700);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), p, new Vector2(0, 9f)
                            , ModContent.ProjectileType<OblivionFireOrb>(), GetBossDamage(0.7f), 2f, Main.myPlayer);
                    }
                }
            }
        }

        //========================= 幕三：万鬼夜行 =========================
        //四祭坛锚点瞬掠（快速位移而非瞬移）→ 2s 蓄力（祭坛辉光颜色预告扇形/360）→ 释放。
        //蓄力时近身玩家叠加"衰朽"debuff。NOT 加速喷弹。
        private void RunNightMarch(Player target, ref int targetFrame, ref bool setNPCRot) {
            //altarIndex 偶数 → 扇形（红橙辉光），奇数 → 360 脉冲（绿辉光）
            bool isFan = altarIndex % 2 == 0;
            Vector2 altarPos = GetAltarPos(altarIndex);

            switch ((int)SubState) {
                case 0: { //锚点瞬掠（快速位移）
                    setNPCRot = false;
                    targetFrame = 4;
                    NPC.Center = Vector2.Lerp(NPC.Center, altarPos, 0.25f);
                    NPC.velocity *= 0.8f;
                    if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                        for (int i = 0; i < 4; i++) {
                            Dust d = Dust.NewDustPerfect(NPC.Center, DustID.GreenTorch, Main.rand.NextVector2Circular(4, 4), 120, new Color(120, 255, 140), 1.6f);
                            d.noGravity = true;
                        }
                    }
                    if (PhaseTimer > 24 || NPC.WithinRange(altarPos, 40f)) {
                        NPC.Center = altarPos;
                        SubState = 1;
                        PhaseTimer = 0;
                        channelGlow = 0f;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: { //2 秒蓄力（120 tick），祭坛辉光预告
                    targetFrame = 3;
                    setNPCRot = false;
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);
                    channelGlow = MathHelper.Clamp(channelGlow + 1f / 120f, 0f, 1f);

                    Color glowColor = isFan ? new Color(255, 90, 40) : new Color(90, 255, 120);
                    if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                        Vector2 e = Main.rand.NextVector2CircularEdge(220, 220) * channelGlow;
                        Dust d = Dust.NewDustPerfect(NPC.Center + e, isFan ? DustID.Torch : DustID.GreenTorch
                            , -e.SafeNormalize(Vector2.Zero) * 5f, 100, glowColor, 1.8f);
                        d.noGravity = true;
                    }

                    //近身蓄力：叠加衰朽
                    if (!VaultUtils.isClient && PhaseTimer % 20 == 0) {
                        foreach (Player p in Main.ActivePlayers) {
                            if (p.Alives() && p.WithinRange(NPC.Center, 360f)) {
                                p.AddBuff(ModContent.BuffType<HoqingDecline>(), 240);
                                p.GetModPlayer<HoqingDeclinePlayer>().AddDecline();
                            }
                        }
                    }

                    if (PhaseTimer == 60) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f }, NPC.Center);
                    }
                    if (PhaseTimer > 120) {
                        SubState = 2;
                        PhaseTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: { //释放
                    targetFrame = 4;
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        if (!VaultUtils.isClient) {
                            if (isFan) {
                                int n = 11;
                                float baseAng = NPC.SafeDirectionTo(target.Center).ToRotation();
                                float spread = MathHelper.ToRadians(70);
                                for (int i = 0; i < n; i++) {
                                    float a = baseAng + MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(n - 1));
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * 12f
                                        , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.8f), 2f, Main.myPlayer);
                                }
                            }
                            else {
                                int n = 26;
                                for (int i = 0; i < n; i++) {
                                    float a = MathHelper.TwoPi * i / n;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * 10f
                                        , ModContent.ProjectileType<GhostFireProj>(), GetBossDamage(0.8f), 2f, Main.myPlayer);
                                }
                            }
                        }
                    }
                    if (PhaseTimer > 50) {
                        SubState = 0;
                        PhaseTimer = 0;
                        if (!VaultUtils.isClient) {
                            altarIndex = (altarIndex + 1) % 4;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
            }
        }

        private Vector2 GetAltarPos(int index) {
            float r = 520f;
            return arenaCenter + (MathHelper.PiOver2 * index + MathHelper.PiOver4).ToRotationVector2() * r;
        }

        private static void ClearHostileBullets() {
            int t1 = ModContent.ProjectileType<OblivionFireOrb>();
            int t2 = ModContent.ProjectileType<GhostFireProj>();
            int t3 = ModContent.ProjectileType<HoqingCorpseChain>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.hostile && (proj.type == t1 || proj.type == t2 || proj.type == t3)) {
                    proj.Kill();
                    proj.netUpdate = true;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                return;
            }
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

        private new void FindFrame(int targetFrame) {
            if (++NPC.frameCounter > 5) {
                NPC.frameCounter = 0;
                if (frame > targetFrame) {
                    frame--;
                }
                else if (frame < targetFrame) {
                    frame++;
                }
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
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                spriteBatch.Draw(glowValue, drawOldPos, rectangle2, Color.White * sengs
                    , 0, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            spriteBatch.Draw(glowValue, NPC.Center - Main.screenPosition, rectangle2, Color.White
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            spriteBatch.Draw(emmdValue, NPC.Center - Main.screenPosition, rectangle2, drawColor
                , NPC.rotation, rectangle2.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }

        // ===== 全屏 screenTarget 限视尸雾 (GenericWarp · fog) — 占本帧唯一全屏名额 (§C.4#2) =====
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

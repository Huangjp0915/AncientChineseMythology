using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items;
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

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙 (V2) — 东方·木风雷·苍龙降世。继承四圣兽共享骨架 <see cref="SacredBeastBase"/>:
    /// 确定性轮替 (<see cref="GetPhaseRotation"/> 替代旧随机 GetRandomPhaseN/Phase3_FuryPatrol hub) +
    /// 预警子状态机 (Windup→Strike→Recover, 预告时长 ∝ 伤害) + 五行(木)主题接线。
    ///
    /// 签名 set-piece (风雷之流, 全部 telegraph; 相变改规则不改数值; 全屏后处理 ≤1 走 RequestFullscreenSlot):
    ///  · <b>风域天罚 Stormfield Judgment</b> (P3 招牌): 3 个非伤害风域漩涡 (<see cref="QinglongWindDomain"/>)
    ///    持续推搡走位, 缝隙间落下红色预告→青白雷柱 (<see cref="QinglongThunderColumn"/>)。「逆风站位别被推进雷里」。
    ///  · <b>天气抉择 Weather Deck</b> (P2+): 公开蓄力后承诺 Wind/Storm 窗口, 屏幕/天幕/龙尾随之染色, 改变后续招式规则。
    ///  · <b>苍龙升腾 Azure Ascent</b>: 8 字 (lemniscate) 飞行, 仅在节点处出招 (无节点间喷弹)。
    ///  · <b>盘龙锁径 Dragon Coil</b>: 锁定半径环绕成笼, 节点向内放风刃环并在「龙所在处」留安全缝。
    ///  · <b>觉醒预兆 Awakening Foreshadow</b> (§5.7 低血): 一次性无敌蓄力定格 → 切入终曲风域天罚。
    /// </summary>
    [AutoloadBossHead]
    public class Qinlong : SacredBeastBase
    {
        #region 五行身份 / 阈值

        public override SacredElement Element => SacredElement.Wood;
        public override string SkyName => QinglongSky.SkyName;
        // 阶段阈值沿用骨架默认 (P2=0.60 / P3=0.30)。

        #endregion

        #region 状态枚举

        public enum QlState
        {
            Intro,
            Patrol,             // 巡游枢纽 (无喷弹, 仅重定位后派发确定性轮替)
            WindBladeFan,       // 风刃扇 (telegraph 锥)
            ThunderColumns,     // 天罚雷柱阵 (带安全缝)
            DragonCoil,         // 盘龙锁径 set-piece
            AzureAscent,        // 苍龙升腾 8 字 (节点出招)
            WeatherDeck,        // 天气抉择 (承诺 Wind/Storm 窗口)
            StormfieldJudgment, // 风域天罚 (招牌)
            AwakeningForeshadow,// §5.7 觉醒预兆
            PhaseTransition2,
            PhaseTransition3
        }

        public QlState State {
            get => (QlState)RawState;
            set => RawState = (int)value;
        }

        #endregion

        #region 字段

        // 同步逻辑字段
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool didAwaken;
        private int weatherMode;       // 0=中性 1=风 2=雷暴
        private int weatherTimer;      // 当前天气剩余 tick
        private int weatherCommitCount; // 确定性 Wind/Storm 交替

        // 本地视觉/瞬态 (无需同步)
        private float glowIntensity = 1f;
        private float weatherFlash;
        private Vector2 setpieceAnchor;
        private int lastNode = -1;
        private bool spawnedSetpiece;

        // 供 QinglongSky / 天气滤镜读取的本帧天气快照
        internal static float s_weatherIntensity;
        internal static int s_weatherMode;

        private static readonly Vector2[] DomainOffsets = { new(-470f, -20f), new(470f, -20f), new(0f, -300f) };

        private bool Server => Main.netMode != NetmodeID.MultiplayerClient;

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 160;
            NPC.height = 160;
            NPC.damage = 220;
            NPC.defense = 80;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit56;
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
            // 灵材 + AzureTorrentBlades 武器桥 (保留 V1 掉落契约)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<QingLongSpirit>(), 1, 6, 10));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<AzureTorrentBlades>(),
                ModContent.ItemType<WindserpentDao>(),
                ModContent.ItemType<ThunderclapLongbow>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            ResetAllRotations();
            weatherMode = 0;
            weatherTimer = 0;
            weatherCommitCount = 0;
            didAwaken = false;
            didPhase2Transition = false;
            didPhase3Transition = false;
            GoTo(QlState.Intro);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            SendSacredBeastAI(writer);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didAwaken);
            writer.Write(weatherMode);
            writer.Write(weatherTimer);
            writer.Write(weatherCommitCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ReceiveSacredBeastAI(reader);
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didAwaken = reader.ReadBoolean();
            weatherMode = reader.ReadInt32();
            weatherTimer = reader.ReadInt32();
            weatherCommitCount = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedQinlong = true;
            s_weatherIntensity = 0f;
            s_weatherMode = 0;
            ACMUtils.AddScreenShake(14f); // §6.2 死亡定格 (统一预算, 非裸 PunchCamera)
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            if (!RunStandardPrologue(out Player target))
                return;

            CheckPhaseTransition();

            switch (State) {
                case QlState.Intro: RunIntro(target); break;
                case QlState.Patrol: RunPatrol(target); break;
                case QlState.WindBladeFan: RunWindBladeFan(target); break;
                case QlState.ThunderColumns: RunThunderColumns(target); break;
                case QlState.DragonCoil: RunDragonCoil(target); break;
                case QlState.AzureAscent: RunAzureAscent(target); break;
                case QlState.WeatherDeck: RunWeatherDeck(target); break;
                case QlState.StormfieldJudgment: RunStormfield(target); break;
                case QlState.AwakeningForeshadow: RunAwakening(target); break;
                case QlState.PhaseTransition2: RunPhaseTransition2(target); break;
                case QlState.PhaseTransition3: RunPhaseTransition3(target); break;
            }

            UpdateRotation();
            UpdateWeather();
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.9f, 0.4f) * glowIntensity);
        }

        /// <summary>切到新状态 + 清掉本地每招瞬态标记。</summary>
        private void GoTo(QlState s) {
            lastNode = -1;
            spawnedSetpiece = false;
            TransitionToState((int)s);
        }

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateWeather() {
            if (weatherTimer > 0)
                weatherTimer--;
            else
                weatherMode = 0;

            s_weatherMode = weatherMode;
            float targetI = weatherMode != 0 ? MathHelper.Clamp(weatherTimer / 90f, 0f, 1f) * 0.32f : 0f;
            s_weatherIntensity = MathHelper.Lerp(s_weatherIntensity, targetI, 0.05f);

            if (weatherFlash > 0f)
                weatherFlash -= 0.045f;
            glowIntensity = MathHelper.Lerp(glowIntensity, IsPhase3 ? 1.8f : 1f, 0.03f);
        }

        private void CheckPhaseTransition() {
            if (State is QlState.Intro or QlState.PhaseTransition2 or QlState.PhaseTransition3 or QlState.AwakeningForeshadow)
                return;

            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                GoTo(QlState.PhaseTransition2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                GoTo(QlState.PhaseTransition3);
            }
        }

        #endregion

        #region 确定性轮替 (替代随机 hub)

        private static readonly int[] P1Rotation = {
            (int)QlState.WindBladeFan, (int)QlState.ThunderColumns, (int)QlState.DragonCoil,
            (int)QlState.AzureAscent, (int)QlState.WindBladeFan
        };
        private static readonly int[] P2Rotation = {
            (int)QlState.WeatherDeck, (int)QlState.AzureAscent, (int)QlState.ThunderColumns,
            (int)QlState.WindBladeFan, (int)QlState.DragonCoil
        };
        private static readonly int[] P3Rotation = {
            (int)QlState.WeatherDeck, (int)QlState.StormfieldJudgment, (int)QlState.AzureAscent,
            (int)QlState.ThunderColumns, (int)QlState.DragonCoil
        };

        protected override int[] GetPhaseRotation(int phaseTier) => phaseTier switch {
            1 => P1Rotation,
            2 => P2Rotation,
            _ => P3Rotation
        };

        #endregion

        #region 入场 / 巡游

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -800);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar, target.Center);
            }

            Vector2 dest = target.Center + new Vector2(0, -340);
            NPC.Center = Vector2.Lerp(NPC.Center, dest, 0.03f);
            NPC.velocity *= 0.95f;

            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 150, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer >= 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                ACMUtils.AddScreenShake(14f);
                ResetAllRotations();
                GoTo(QlState.Patrol);
            }
        }

        private void RunPatrol(Player target) {
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GlobalTime * 0.8f) * 180f, -340f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.08f);

            if (PhaseTimer >= 42) {
                // §5.7 觉醒预兆: 三阶段低血一次性切入终曲
                if (IsPhase3 && NPC.life < NPC.lifeMax * 0.12f && !didAwaken) {
                    GoTo(QlState.AwakeningForeshadow);
                    return;
                }
                int next = NextAttack(PhaseTier);
                GoTo(next < 0 ? QlState.WindBladeFan : (QlState)next);
            }
        }

        #endregion

        #region 攻击: 风刃扇

        private void RunWindBladeFan(Player target) {
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GlobalTime * 1.6f) * 140f, -360f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.06f, 0.1f);

            bool done = AdvanceTelegraph(30, 66, 22);

            if (InStrike) {
                int interval = Main.expertMode ? 14 : 18;
                if (AttackTimer % interval == 1)
                    FireWindFan(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void FireWindFan(Player target) {
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = 5 + (weatherMode == 1 ? 2 : 0);
            float spread = MathHelper.ToRadians(9f);
            for (int i = -count / 2; i <= count / 2; i++)
                NewWindBlade(NPC.Center, dir.RotatedBy(i * spread) * 15f, NPC.damage / 4);

            if (weatherMode == 2)
                SpawnThunderColumn(target.Center, ThreatTier.Medium);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
        }

        #endregion

        #region 攻击: 天罚雷柱阵 (安全缝)

        private void RunThunderColumns(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -440);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.08f);

            bool done = AdvanceTelegraph(10, 120, 26);

            if (InStrike) {
                const int volleyInterval = 38;
                if (AttackTimer % volleyInterval == 1)
                    FireColumnVolley(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void FireColumnVolley(Player target) {
            int cols = 5 + (weatherMode == 2 ? 2 : 0);
            float span = 820f;
            int volley = (int)(AttackTimer / 38);
            int safe = volley % cols; // 旋转的安全缝, 玩家有可读逃生位
            for (int i = 0; i < cols; i++) {
                if (i == safe)
                    continue;
                float x = MathHelper.Lerp(-span, span, cols == 1 ? 0.5f : (float)i / (cols - 1));
                SpawnThunderColumn(new Vector2(target.Center.X + x, target.Center.Y), ThreatTier.Medium);
            }
        }

        #endregion

        #region 攻击: 盘龙锁径 set-piece

        private void RunDragonCoil(Player target) {
            const float radius = 360f;
            float ang = PhaseTimer * 0.055f; // 锁定半径环绕成笼
            Vector2 orbit = target.Center + ang.ToRotationVector2() * radius + new Vector2(0, -40);
            NPC.velocity = (orbit - NPC.Center) * 0.2f;

            // 节点向内放风刃环, 在「龙所在角度」留安全缝
            const int nodePeriod = 34;
            if ((int)PhaseTimer % nodePeriod == nodePeriod - 1)
                FireCoilRing(target, ang, radius);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
                d.velocity = NPC.velocity * 0.1f;
            }

            if (PhaseTimer > 312)
                GoTo(QlState.Patrol);
        }

        private void FireCoilRing(Player target, float bossAngle, float radius) {
            int count = 18;
            float gapHalf = MathHelper.ToRadians(34f);
            for (int i = 0; i < count; i++) {
                float a = MathHelper.TwoPi / count * i;
                if (MathF.Abs(MathHelper.WrapAngle(a - bossAngle)) < gapHalf)
                    continue; // 安全缝 = 龙所在处, 「站到龙那边」
                Vector2 pos = target.Center + a.ToRotationVector2() * radius;
                Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 7f;
                NewWindBlade(pos, vel, NPC.damage / 4);
            }
            SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
        }

        #endregion

        #region 攻击: 苍龙升腾 8 字 (节点出招)

        private void RunAzureAscent(Player target) {
            Vector2 anchor = target.Center + new Vector2(0, -220);
            const float speed = 0.05f;
            float theta = PhaseTimer * speed;
            // Lemniscate of Gerono: 8 字
            Vector2 off = new(MathF.Cos(theta) * 420f, MathF.Sin(theta) * MathF.Cos(theta) * 300f * 2f);
            Vector2 dest = anchor + off;
            NPC.velocity = (dest - NPC.Center) * 0.25f;

            int node = (int)(theta / MathHelper.PiOver2);
            if (node > lastNode) {
                lastNode = node;
                FireAscentNode(target);
            }

            if (theta >= MathHelper.TwoPi * 2f)
                GoTo(QlState.Patrol);
        }

        private void FireAscentNode(Player target) {
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int n = 4;
            float spread = MathHelper.ToRadians(11f);
            for (int i = -n / 2; i <= n / 2; i++)
                NewWindBlade(NPC.Center, dir.RotatedBy(i * spread) * 16f, NPC.damage / 4);

            if (weatherMode == 2 || IsPhase3)
                SpawnThunderColumn(target.Center, ThreatTier.Medium);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f }, NPC.Center);
        }

        #endregion

        #region 攻击: 天气抉择 Weather Deck

        private void RunWeatherDeck(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.1f);
            NPC.velocity *= 0.92f;

            bool done = AdvanceTelegraph(64, 6, 24);

            if (InStrike && !spawnedSetpiece) {
                spawnedSetpiece = true;
                CommitWeather();
            }

            if (!Main.dedServ && InWindup && PhaseTimer % 2 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * Main.rand.NextFloat(160, 320);
                int dustId = (weatherCommitCount % 2 == 0) ? DustID.GreenTorch : DustID.Electric;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustId, 0, 0, 120, default, 2f);
                d.noGravity = true;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void CommitWeather() {
            weatherMode = (weatherCommitCount % 2 == 0) ? 1 : 2;
            weatherCommitCount++;
            weatherTimer = 600;
            weatherFlash = 1f;
            glowIntensity = 1.7f;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
            ACMUtils.AddScreenShake(10f);
            if (Server)
                NPC.netUpdate = true;
        }

        #endregion

        #region 攻击: 风域天罚 Stormfield Judgment (招牌)

        private void RunStormfield(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -460);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.03f, 0.06f);
            setpieceAnchor = target.Center;

            bool done = AdvanceTelegraph(78, 210, 26);

            if (InStrike) {
                if (!spawnedSetpiece) {
                    spawnedSetpiece = true;
                    SpawnWindDomains(target);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = -0.4f }, NPC.Center);
                    ACMUtils.AddScreenShake(11f);
                    weatherFlash = 1f;
                }
                const int interval = 30;
                if (AttackTimer % interval == 1)
                    RainGapColumns(target);
            }

            if (done)
                GoTo(QlState.Patrol);
        }

        private void SpawnWindDomains(Player target) {
            if (!Server)
                return;
            for (int i = 0; i < DomainOffsets.Length; i++) {
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center + DomainOffsets[i], Vector2.Zero,
                    ModContent.ProjectileType<QinglongWindDomain>(), 0, 0f, Main.myPlayer, 250f, i % 2 == 0 ? 1f : -1f);
                if (p >= 0 && p < Main.maxProjectiles)
                    Main.projectile[p].timeLeft = 200;
            }
        }

        private void RainGapColumns(Player target) {
            float[] xs = { -235f, 235f, 0f };
            int volley = (int)(AttackTimer / 30);
            float jx = xs[volley % xs.Length];
            SpawnThunderColumn(new Vector2(target.Center.X + jx, target.Center.Y), ThreatTier.Medium);
            if (IsPhase3)
                SpawnThunderColumn(new Vector2(target.Center.X - jx, target.Center.Y), ThreatTier.Medium);
        }

        #endregion

        #region 攻击: 觉醒预兆 (§5.7)

        private void RunAwakening(Player target) {
            didAwaken = true; // 立刻置位, 杜绝重复触发
            NPC.dontTakeDamage = true;
            Vector2 hover = target.Center + new Vector2(0, -320);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.1f);
            glowIntensity = MathHelper.Lerp(glowIntensity, 2.3f, 0.04f);

            if (!Main.dedServ && PhaseTimer % 2 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * MathF.Max(80f, 520f - PhaseTimer * 3f);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.Electric, 0, 0, 60, default, 3f);
                d.noGravity = true;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 9f;
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.3f }, NPC.Center);
                ACMUtils.AddScreenShake(16f);
                weatherFlash = 1.4f;
                weatherMode = 2;       // 终曲锁定雷暴天气
                weatherTimer = 1200;
                weatherCommitCount++;
                if (Server)
                    NPC.netUpdate = true;
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                ResetRotation(3);
                GoTo(QlState.StormfieldJudgment);
            }
        }

        #endregion

        #region 阶段过渡 (i-frame 节拍)

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true;

            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi / 8 * i + GlobalTime * 3f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (200 - PhaseTimer);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                ACMUtils.AddScreenShake(9f);
                weatherFlash = 0.9f;
            }

            if (PhaseTimer >= 84) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.15f);
                ResetRotation(2);
                GoTo(QlState.Patrol);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (!Main.dedServ) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi / 12 * i + GlobalTime * 5f;
                    float dist = MathF.Max(50, 300 - PhaseTimer * 2);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                weatherFlash = 1.1f;
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.25f);
                glowIntensity = 1.8f;
                ResetRotation(3);
                GoTo(QlState.Patrol);
            }
        }

        #endregion

        #region 弹幕施放助手 (server 权威)

        private void NewWindBlade(Vector2 pos, Vector2 vel, int dmg) {
            if (!Server)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<QinglongWindBlade>(), dmg, 0f, Main.myPlayer);
        }

        private void SpawnThunderColumn(Vector2 worldPos, ThreatTier tier) {
            if (!Server)
                return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), worldPos, Vector2.Zero,
                ModContent.ProjectileType<QinglongThunderColumn>(), NPC.damage / 3, 0f, Main.myPlayer,
                TelegraphColors.TelegraphTicks(tier));
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingLeft = MathF.Abs(NPC.rotation) > MathHelper.PiOver2;
            SpriteEffects effects = facingLeft ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Color weatherTail = WeatherColor();

            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float alpha = 0.5f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                Color trailColor = Color.Lerp(drawColor, weatherTail, weatherMode != 0 ? 0.45f : 0.15f) * alpha;
                trailColor.G = (byte)Math.Min(trailColor.G * 1.3f, 255);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin,
                    NPC.scale * (1f - i * 0.015f), effects, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            Player target = (NPC.target >= 0 && NPC.target < Main.maxPlayers) ? Main.player[NPC.target] : null;

            // 蓄力/承诺/觉醒 爆闪 (经 RequestFullscreenSlot 仲裁的单全屏泛光)
            if (weatherFlash > 0.05f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, MathHelper.Clamp(weatherFlash, 0f, 1f), WeatherColor(), 12f, 2.2f);

            switch (State) {
                case QlState.WindBladeFan:
                    if (InWindup && target != null)
                        DrawConeTelegraph(target);
                    break;
                case QlState.AzureAscent:
                    if (target != null)
                        DrawAimBeam(target, 0.35f);
                    break;
                case QlState.DragonCoil:
                    if (target != null)
                        DrawArenaRing(target.Center, 360f, TelegraphColors.AzureDragon, CoilRingIntensity());
                    break;
                case QlState.WeatherDeck:
                    if (InWindup)
                        DrawDeckCharge();
                    break;
                case QlState.StormfieldJudgment:
                    if (InWindup)
                        DrawDomainPreviews();
                    break;
                case QlState.AwakeningForeshadow:
                    DrawAwakenCharge();
                    break;
            }
        }

        private Color WeatherColor() => weatherMode == 2 ? new Color(120, 200, 255) : new Color(80, 235, 150);

        private float CoilRingIntensity() {
            const int nodePeriod = 34;
            float ph = (PhaseTimer % nodePeriod) / (float)nodePeriod;
            return 0.40f + 0.45f * ph; // 节点逼近时渐亮 = 节奏可读
        }

        private void DrawConeTelegraph(Player target) {
            float prog = MathHelper.Clamp(AttackTimer / 30f, 0f, 1f);
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            int count = 5 + (weatherMode == 1 ? 2 : 0);
            float half = count / 2 * MathHelper.ToRadians(9f);
            Color core = TelegraphColors.AzureDragon;
            Color edge = TelegraphColors.AzureDragon * 0.3f;
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir.RotatedBy(-half) * 900f, 3f, core, edge, 0.5f * prog);
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir.RotatedBy(half) * 900f, 3f, core, edge, 0.5f * prog);
        }

        private void DrawAimBeam(Player target, float intensity) {
            ACMShaders.DrawBeam(NPC.Center, target.Center, 2.5f,
                TelegraphColors.AzureDragon, TelegraphColors.AzureDragon * 0.3f, intensity);
        }

        private void DrawDeckCharge() {
            float prog = MathHelper.Clamp(AttackTimer / 64f, 0f, 1f);
            Color c = (weatherCommitCount % 2 == 0) ? new Color(80, 235, 150) : new Color(120, 200, 255);
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.10f + 0.10f * prog, 0.25f + 0.6f * prog, c, 14f, 2.4f);
        }

        private void DrawDomainPreviews() {
            float prog = MathHelper.Clamp(AttackTimer / 78f, 0f, 1f);
            for (int i = 0; i < DomainOffsets.Length; i++)
                ElementTelegraphCircle(Main.spriteBatch, setpieceAnchor + DomainOffsets[i], 250f, 0.5f * prog, false);
        }

        private void DrawAwakenCharge() {
            float prog = MathHelper.Clamp(PhaseTimer / 70f, 0f, 1f);
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.15f + 0.18f * prog, prog, new Color(120, 200, 255), 14f, 2.0f);
        }

        private void DrawArenaRing(Vector2 center, float worldRadius, Color primary, float intensity) {
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null || intensity <= 0.01f)
                return;
            ACMShaders.WorldDecalParams(center, worldRadius, out Vector2 uv, out float rf, out float asp);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rf);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(asp);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(16, 90, 64).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }

        #endregion
    }

    /// <summary>
    /// 青龙天气滤镜 — 把 Weather Deck 承诺的 Wind/Storm 窗口外溢为屏幕元素染色 (ElementalScreenTint)。
    /// 走 PostDrawTiles (无活动批, 在实体之前) 当氛围底色, 不读 screenTarget 故不占全屏后处理名额 (§C.4#2);
    /// 受 MythologyConfig 降级开关与天气强度快照驱动, 服务端/截图/无 Boss 时零绘制。
    /// </summary>
    public class QinglongWeatherSystem : ModSystem
    {
        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;
            float intensity = Qinlong.s_weatherIntensity;
            if (intensity <= 0.01f)
                return;
            if (!NPC.AnyNPCs(ModContent.NPCType<Qinlong>()))
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            bool storm = Qinlong.s_weatherMode == 2;
            Color tint = storm ? new Color(70, 150, 230) : new Color(40, 170, 120);
            Color tint2 = storm ? new Color(18, 38, 88) : new Color(8, 58, 40);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.5f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(tint2.ToVector3(), 1f));
            fx.Parameters["uVignette"]?.SetValue(0.35f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        public override void OnWorldUnload() {
            Qinlong.s_weatherIntensity = 0f;
            Qinlong.s_weatherMode = 0;
        }
    }
}

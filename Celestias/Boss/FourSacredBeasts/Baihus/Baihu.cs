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

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎 — 西方·金·神虎降世 (V2)。
    ///
    /// V1 是「假近战、真喷弹」：随机 hub + 每 5/10 tick 喷金属环/音波。V2 改为 <see cref="SacredBeastBase"/>
    /// 确定性轮替 + 预警子状态机的<b>真·猎杀者</b>：
    ///   • 一/二阶段「猎手循环」——<b>潜行 Stalk</b>(纯追踪零弹，蓄势) 与 <b>扑击 Pounce</b>(只在扑击窗口高伤) 交替；
    ///     爪痕蓄能满 → <b>蓄势扑击</b>(1s 银爪预兆 glowing-claw tell)。
    ///   • 「金属回响 Metallic Echo」环：每第三片才是真的(<see cref="BaihuEchoDecoy"/> 虚影)，以可读取代密度。
    ///   • 「铁壁 Iron Wall」：以虎啸朝向宣告的虎形安全缺口。
    ///   • 三阶段「白虎之形」落地形态：三招——裂地灭世爪 RiftClaw / 震地踏 QuakeStomp / 爪裂射线 RendBeams。
    /// 表现走硬化 <see cref="ACMShaders"/>：爪裂 <see cref="ACMShaders.DrawBeam"/>、震波 <see cref="ACMShaders.DrawRadialBloomAt"/>、
    /// 地纹 <see cref="ACMShaders.ArenaRunic"/>+<see cref="ACMShaders.WorldDecalParams"/>；红=致命唯一色，震屏走 <see cref="ACMScreenShakeSystem"/>。
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
            Stalk,          // 潜行：纯追踪零弹，蓄势
            Pounce,         // 扑击：唯一高伤窗口
            ClawSwipe,      // 爪击扇：施加爪痕、积蓄
            MetallicEcho,   // 金属回响：每第三片才真
            IronWall,       // 铁壁：虎形安全缺口(二阶段起)
            PhaseTransition2,
            PhaseTransition3,
            // 三阶段 白虎之形(落地)
            RiftClaw,       // 裂地灭世爪(签名)
            QuakeStomp,     // 震地踏：可跳/可读震波
            RendBeams       // 爪裂射线
        }

        private BaihuState State {
            get => (BaihuState)RawState;
            set => RawState = (int)value;
        }

        #endregion

        #region 字段

        private bool didPhase2Transition;
        private bool didPhase3Transition;

        private int clawCharge;          // 爪痕蓄能(每 3 触发蓄势扑击)
        private bool pounceEmpowered;    // 本次扑击是否蓄势(银爪预兆)
        private Vector2 pounceDir = Vector2.UnitX;
        private float ironGapAngle;      // 铁壁安全缺口朝向
        private float prowlAngle;
        private float glowIntensity = 1f;
        private float quakeFlash;        // 落地震波视觉脉冲(本地)

        private bool Expert => Main.expertMode;
        private bool OnServer => Main.netMode != NetmodeID.MultiplayerClient;

        #endregion

        #region 确定性轮替

        protected override int[] GetPhaseRotation(int phaseTier) => phaseTier switch {
            1 => new[] {
                (int)BaihuState.Stalk, (int)BaihuState.Pounce, (int)BaihuState.ClawSwipe,
                (int)BaihuState.Stalk, (int)BaihuState.MetallicEcho, (int)BaihuState.Pounce
            },
            2 => new[] {
                (int)BaihuState.Stalk, (int)BaihuState.Pounce, (int)BaihuState.IronWall,
                (int)BaihuState.ClawSwipe, (int)BaihuState.MetallicEcho, (int)BaihuState.Pounce
            },
            _ => new[] {
                (int)BaihuState.RiftClaw, (int)BaihuState.QuakeStomp, (int)BaihuState.RendBeams
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
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

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

        public override void OnKill() {
            DownedBossSystem.downedBaihu = true;
            ACMScreenShakeSystem.Add(16f);
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            if (!RunStandardPrologue(out Player target))
                return;

            quakeFlash *= 0.9f;
            CheckPhaseTransition();

            switch (State) {
                case BaihuState.Intro: RunIntro(target); break;
                case BaihuState.Stalk: RunStalk(target); break;
                case BaihuState.Pounce: RunPounce(target); break;
                case BaihuState.ClawSwipe: RunClawSwipe(target); break;
                case BaihuState.MetallicEcho: RunMetallicEcho(target); break;
                case BaihuState.IronWall: RunIronWall(target); break;
                case BaihuState.PhaseTransition2: RunPhaseTransition2(target); break;
                case BaihuState.PhaseTransition3: RunPhaseTransition3(target); break;
                case BaihuState.RiftClaw: RunRiftClaw(target); break;
                case BaihuState.QuakeStomp: RunQuakeStomp(target); break;
                case BaihuState.RendBeams: RunRendBeams(target); break;
            }

            // 朝向玩家
            NPC.spriteDirection = target.Center.X >= NPC.Center.X ? 1 : -1;
            NPC.rotation = MathHelper.Clamp(NPC.velocity.X * 0.01f, -0.25f, 0.25f) * NPC.spriteDirection;

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.92f, 1.0f) * glowIntensity);
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

        private void Shake(float amt) => ACMScreenShakeSystem.Add(amt);

        #endregion

        #region 入场

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                float side = Main.rand.NextBool() ? -1 : 1;
                NPC.Center = target.Center + new Vector2(side * 700, -260);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, target.Center);
            }

            Vector2 want = target.Center + new Vector2(0, -250);
            NPC.Center = Vector2.Lerp(NPC.Center, want, 0.045f);
            NPC.velocity *= 0.9f;

            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(160, 160);
                    Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.Silver, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            if (PhaseTimer >= 95) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                Shake(12f);
                ResetRotation(1);
                AdvanceRotation();
            }
        }

        #endregion

        #region 猎手循环 (P1/P2)

        // 潜行：纯追踪零弹，缓慢逼近积蓄气势
        private void RunStalk(Player target) {
            int dur = IsPhase2 ? 60 : 78;
            prowlAngle += IsPhase2 ? 0.03f : 0.022f;
            float radius = 360f + MathF.Sin(GlobalTime * 1.5f) * 40f;
            Vector2 want = target.Center + new Vector2(MathF.Cos(prowlAngle), MathF.Sin(prowlAngle) * 0.4f) * radius + new Vector2(0, -110);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.045f, 0.06f);

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 160, default, 1.0f);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.1f;
            }

            if (AdvanceTelegraph(0, dur, 0))
                AdvanceRotation();
        }

        // 扑击：唯一高伤窗口；爪痕蓄满则蓄势(银爪预兆+更长预告)
        private void RunPounce(Player target) {
            int windup = pounceEmpowered ? 60 : 34;
            int strike = 26;
            int recover = 22;

            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        pounceEmpowered = clawCharge >= 3;
                        if (pounceEmpowered) clawCharge = 0;
                        windup = pounceEmpowered ? 60 : 34;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = pounceEmpowered ? -0.2f : 0.6f }, NPC.Center);
                        if (OnServer) NPC.netUpdate = true;
                    }
                    Vector2 toP = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    if (AttackTimer <= windup - 12)
                        pounceDir = toP; // 末 12 tick 锁定，预告固定可读
                    NPC.velocity = Vector2.Lerp(NPC.velocity, -toP * (pounceEmpowered ? 7f : 4f), 0.1f);
                    break;

                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        float speed = (IsPhase2 ? 46f : 40f) + (pounceEmpowered ? 12f : 0f);
                        NPC.velocity = pounceDir * speed;
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f }, NPC.Center);
                        Shake(pounceEmpowered ? 9f : 6f);
                        if (pounceEmpowered && OnServer) {
                            // 蓄势扑击：沿扑击路径甩出两道银爪裂脉
                            float ang = pounceDir.ToRotation();
                            BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), NPC.Center, ang + 0.18f, 1100f, 10, 34, NPC.damage / 4);
                            BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), NPC.Center, ang - 0.18f, 1100f, 10, 34, NPC.damage / 4);
                        }
                    }
                    if (AttackTimer > 14)
                        NPC.velocity *= 0.93f;
                    if (!Main.dedServ) {
                        for (int i = 0; i < 3; i++) {
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 80, default, 1.8f);
                            d.noGravity = true;
                            d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                        }
                    }
                    break;

                default: // Recover
                    NPC.velocity *= 0.9f;
                    break;
            }

            if (AdvanceTelegraph(windup, strike, recover)) {
                pounceEmpowered = false;
                AdvanceRotation();
            }
        }

        // 爪击扇：可读扇形银爪，施加爪痕，积蓄蓄能
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
                        Shake(4f);
                        clawCharge++;
                        if (OnServer) {
                            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
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
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 金属回响：每第三片才是真的，2/3 为无害虚影
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

        // 铁壁：以虎啸朝向宣告虎形安全缺口的金属合围
        private void RunIronWall(Player target) {
            int windup = 52, strike = 6, recover = 24;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        // 缺口朝向：偏离玩家当前位置一侧(可逃可读)，由虎啸宣告
                        ironGapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.2f }, NPC.Center);
                        if (OnServer) NPC.netUpdate = true;
                    }
                    NPC.velocity *= 0.92f;
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        Shake(5f);
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

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = MathF.Max(30f, 300f - PhaseTimer * 2.5f);
                    Vector2 dp = NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * dist;
                    Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.Silver, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.5f }, NPC.Center);
                Shake(10f);
            }

            if (PhaseTimer >= 88) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                ResetRotation(2);
                AdvanceRotation();
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);
            glowIntensity = MathHelper.Lerp(glowIntensity, 2f, 0.03f);

            if (!Main.dedServ) {
                for (int i = 0; i < 12; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 280);
                    Vector2 dp = NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * dist;
                    Dust d = Dust.NewDustDirect(dp, 0, 0, Main.rand.NextBool() ? DustID.Silver : DustID.GoldCoin, 0, 0, 50, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 2f }, NPC.Center);
                Shake(12f);
                quakeFlash = 1f;
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 2f;
                ResetRotation(3);
                AdvanceRotation();
            }
        }

        #endregion

        #region 白虎之形 (P3 落地)

        // 三阶段落地定位：贴近玩家水平线略偏前
        private void GroundedHover(Player target, float xOffset, float yOffset, float ease = 0.06f) {
            Vector2 want = target.Center + new Vector2(xOffset, yOffset);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.06f, ease);
        }

        // 裂地灭世爪 RiftClaw — 跃高空，地面平行爪痕预告→落地银脉爆裂；站在爪痕之间的缝
        private void RunRiftClaw(Player target) {
            int windup = 76, strike = 30, recover = 40;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.4f }, NPC.Center);
                        if (OnServer) {
                            // 平行竖向爪痕：在玩家两侧布数道竖脉，留出可站的缝；预告时长≈windup
                            float[] off = Expert
                                ? new[] { -640f, -440f, -240f, 240f, 440f, 640f }
                                : new[] { -540f, -300f, 300f, 540f };
                            foreach (float x in off) {
                                Vector2 top = new Vector2(target.Center.X + x, target.Center.Y - 760);
                                BaihuRendBeam.Spawn(NPC.GetSource_FromAI(), top, MathHelper.PiOver2, 1500f, windup, strike, NPC.damage / 3);
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    // 跃至高空
                    GroundedHover(target, 0, -440, 0.08f);
                    // 渐强震屏
                    Shake(2f + 6f * (AttackTimer / (float)windup));
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        // 落地：重砸 + 震波泛光
                        NPC.velocity = new Vector2(0, 36f);
                        Shake(12f);
                        quakeFlash = 1f;
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.4f }, NPC.Center);
                        if (!Main.dedServ) {
                            for (int i = 0; i < 24; i++) {
                                Dust d = Dust.NewDustDirect(target.Center + new Vector2(Main.rand.NextFloat(-700, 700), 40), 0, 0, DustID.Smoke, Main.rand.NextFloat(-4, 4), -Main.rand.NextFloat(2, 7), 120, default, 2f);
                                d.noGravity = true;
                            }
                        }
                    }
                    NPC.velocity.Y *= 0.9f;
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        // 震地踏 QuakeStomp — 落地踏出可读扩张震波(可跳/绕)
        private void RunQuakeStomp(Player target) {
            int windup = 40, strike = 22, recover = 26;
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    GroundedHover(target, target.Center.X >= NPC.Center.X ? -260 : 260, -40);
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        Shake(10f);
                        quakeFlash = 1f;
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, NPC.Center);
                        if (OnServer)
                            SpawnShockRing(NPC.Center, 460f, NPC.damage / 4);
                        NPC.velocity = Vector2.Zero;
                    }
                    if (AttackTimer == 14 && OnServer)
                        SpawnShockRing(NPC.Center, 620f, NPC.damage / 4);
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        private void SpawnShockRing(Vector2 center, float maxRadius, int damage) {
            int pr = Projectile.NewProjectile(NPC.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<BaihuSonicRoar>(), damage, 0f, Main.myPlayer, 0f, maxRadius);
            if (pr >= 0 && pr < Main.maxProjectiles)
                Main.projectile[pr].netUpdate = true;
        }

        // 爪裂射线 RendBeams — 一组方向可读的银爪射线扫过
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
                    GroundedHover(target, target.Center.X >= NPC.Center.X ? -380 : 380, -120);
                    break;
                case TelegraphPhase.Strike:
                    if (AttackTimer == 1) {
                        Shake(8f);
                        SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
                    }
                    NPC.velocity *= 0.85f;
                    break;
                default:
                    NPC.velocity *= 0.9f;
                    break;
            }
            if (AdvanceTelegraph(windup, strike, recover))
                AdvanceRotation();
        }

        #endregion

        #region 绘制 (含预警)

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawTelegraphs(spriteBatch);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingRight = NPC.spriteDirection == 1;
            SpriteEffects effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRotation = facingRight ? NPC.rotation : -NPC.rotation;

            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float alpha = 0.35f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                spriteBatch.Draw(texture, trailPos, frame, drawColor * alpha, drawRotation, origin, NPC.scale, effects, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, drawColor, drawRotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        // 攻击预警(银白主题/红致命)，服务端零绘制；红只给致命扑击线
        private void DrawTelegraphs(SpriteBatch sb) {
            if (Main.dedServ)
                return;
            Player target = Main.player[NPC.target];
            if (target == null || !target.active)
                return;

            switch (State) {
                case BaihuState.Pounce when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / (float)(pounceEmpowered ? 60 : 34), 0f, 1f);
                    Vector2 end = NPC.Center + pounceDir * 1000f;
                    ACMShaders.DrawBeam(NPC.Center, end, (pounceEmpowered ? 8f : 5f) * (0.4f + 0.6f * prog),
                        TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, 0.25f + 0.55f * prog,
                        flowSpeed: 1f, flowScale: 3f, coreSharp: 3f);
                    if (pounceEmpowered)
                        ElementBloom(sb, 0.4f + 0.4f * prog, 150f); // 银爪预兆辉
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
                    // 安全缺口：翠玉射线指明可逃方向
                    Vector2 gapEnd = target.Center + ironGapAngle.ToRotationVector2() * 560f;
                    ACMShaders.DrawBeam(target.Center, gapEnd, 10f, TelegraphColors.Safe,
                        TelegraphColors.Safe * 0.4f, 0.5f * prog, flowSpeed: 0.6f, flowScale: 2f);
                    break;
                }
                case BaihuState.QuakeStomp when InWindup: {
                    float prog = MathHelper.Clamp(AttackTimer / 40f, 0f, 1f);
                    DrawGroundDecal(sb, NPC.Center, 460f, 0.5f * prog);
                    break;
                }
            }

            // 落地震波泛光(裂地灭世爪/震地踏/相变)
            if (quakeFlash > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.22f, quakeFlash, TelegraphColors.WhiteTiger, rayCount: 14f);
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

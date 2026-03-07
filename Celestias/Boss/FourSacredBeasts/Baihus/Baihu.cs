using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎 - 西方神兽，金/风属性
    /// 巨型白虎，高速近战Boss，以凶猛的扑击和金属利刃为特色
    /// 一阶段：铁虎猎杀，利爪与金属碎片
    /// 二阶段：钢虎狂暴，狂暴冲刺与大地震击
    /// 三阶段：神虎降世，金属风暴与灭世之爪
    /// </summary>
    [AutoloadBossHead]
    public class Baihu : ModNPC
    {
        #region 常量定义

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.30f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Prowl,
            Phase1_MetalClaw,
            Phase1_Pounce,
            Phase1_BladeStorm,
            Phase1_TigerRoar,
            PhaseTransition_2,
            Phase2_FrenzyCharge,
            Phase2_MetalRain,
            Phase2_EarthShatter,
            Phase2_IronWall,
            Phase2_FuryCombo,
            PhaseTransition_3,
            Phase3_MetalTempest,
            Phase3_WhiteGoldBeam,
            Phase3_ExtinctionClaw,
            Phase3_TigerGodsFury,
            Phase3_FuryProwl
        }

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        private int chargeCount;
        private int comboStep;
        private float prowlAngle;
        private float glowIntensity = 1f;

        #endregion

        #region ModNPC重写

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
            NPC.lifeMax = 5000000;
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
            // 掉落占位
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(chargeCount);
            writer.Write(comboStep);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            chargeCount = reader.ReadInt32();
            comboStep = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Silver, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
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
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            globalTime += 1f / 60f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Prowl: RunPhase1Prowl(target); break;
                case BossPhase.Phase1_MetalClaw: RunPhase1MetalClaw(target); break;
                case BossPhase.Phase1_Pounce: RunPhase1Pounce(target); break;
                case BossPhase.Phase1_BladeStorm: RunPhase1BladeStorm(target); break;
                case BossPhase.Phase1_TigerRoar: RunPhase1TigerRoar(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_FrenzyCharge: RunPhase2FrenzyCharge(target); break;
                case BossPhase.Phase2_MetalRain: RunPhase2MetalRain(target); break;
                case BossPhase.Phase2_EarthShatter: RunPhase2EarthShatter(target); break;
                case BossPhase.Phase2_IronWall: RunPhase2IronWall(target); break;
                case BossPhase.Phase2_FuryCombo: RunPhase2FuryCombo(target); break;
                case BossPhase.PhaseTransition_3: RunPhaseTransition3(target); break;
                case BossPhase.Phase3_MetalTempest: RunPhase3MetalTempest(target); break;
                case BossPhase.Phase3_WhiteGoldBeam: RunPhase3WhiteGoldBeam(target); break;
                case BossPhase.Phase3_ExtinctionClaw: RunPhase3ExtinctionClaw(target); break;
                case BossPhase.Phase3_TigerGodsFury: RunPhase3TigerGodsFury(target); break;
                case BossPhase.Phase3_FuryProwl: RunPhase3FuryProwl(target); break;
            }

            // 白虎面向玩家
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.02f;

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.9f, 1.0f) * glowIntensity);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.PhaseTransition_3 && Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_3);
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            chargeCount = 0;
            comboStep = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_MetalClaw,
                1 => (int)BossPhase.Phase1_Pounce,
                2 => (int)BossPhase.Phase1_BladeStorm,
                _ => (int)BossPhase.Phase1_TigerRoar
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_FrenzyCharge,
                1 => (int)BossPhase.Phase2_MetalRain,
                2 => (int)BossPhase.Phase2_EarthShatter,
                3 => (int)BossPhase.Phase2_IronWall,
                _ => (int)BossPhase.Phase2_FuryCombo
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase3_MetalTempest,
                1 => (int)BossPhase.Phase3_WhiteGoldBeam,
                2 => (int)BossPhase.Phase3_ExtinctionClaw,
                _ => (int)BossPhase.Phase3_TigerGodsFury
            });
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                // 白虎从侧方闪现
                float side = Main.rand.NextBool() ? -1 : 1;
                NPC.Center = target.Center + new Vector2(side * 600, -200);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, target.Center);
            }

            // 缓慢逼近，虎威压制
            Vector2 targetPos = target.Center + new Vector2(0, -250);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.025f);

            if (Main.netMode != NetmodeID.Server) {
                // 金属光芒粒子
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(150, 150);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Silver, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            if (PhaseTimer >= 100) {
                // 虎啸震屏
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 18f, 8f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                TransitionTo(BossPhase.Phase1_Prowl);
            }
        }

        #endregion

        #region 一阶段：铁虎

        private void RunPhase1Prowl(Player target) {
            // 像猎食者一样围绕玩家，时而加速，时而减速
            prowlAngle += 0.03f;
            float radius = 350f + MathF.Sin(globalTime * 2f) * 50f;

            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(prowlAngle), MathF.Sin(prowlAngle) * 0.4f) * radius;
            orbitPos.Y -= 150f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitPos - NPC.Center) * 0.07f, 0.1f);

            // 虎气粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 150, default, 1.2f);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.1f;
            }

            if (PhaseTimer > 120) TransitionTo(GetRandomPhase1Attack());
        }

        private void RunPhase1MetalClaw(Player target) {
            // 快速接近后释放金属爪击弹幕
            if (SubState == 0) {
                // 急速接近玩家
                Vector2 toPlayer = (target.Center - NPC.Center);
                if (toPlayer.Length() > 200f) {
                    NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer.SafeNormalize(Vector2.Zero) * 20f, 0.12f);
                }
                else {
                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f }, NPC.Center);

                    // 扇形爪击弹幕
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        int clawCount = Main.expertMode ? 7 : 5;
                        float totalSpread = MathHelper.ToRadians(60f);
                        for (int i = 0; i < clawCount; i++) {
                            float angle = -totalSpread / 2 + totalSpread / (clawCount - 1) * i;
                            Vector2 vel = dir.RotatedBy(angle) * 16f;
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 2f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) {
                                Main.projectile[proj].friendly = false;
                                Main.projectile[proj].hostile = true;
                                Main.projectile[proj].tileCollide = false;
                                Main.projectile[proj].timeLeft = 80;
                            }
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX), 8f, 5f, 15, 800f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (AttackTimer > 45) TransitionTo(BossPhase.Phase1_Prowl);
            }
        }

        private void RunPhase1Pounce(Player target) {
            // 白虎扑击：蓄力后高速扑向玩家
            if (SubState == 0) {
                // 蓄力：后退一点
                Vector2 away = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 5f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, away, 0.1f);

                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Silver, 0, 0, 100, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 3f;
                }

                if (AttackTimer > 35) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 32f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                // 扑击阶段 - 留下金属尾迹
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 80, default, 2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                    }
                }

                if (AttackTimer > 25) NPC.velocity *= 0.92f;
                if (AttackTimer > 40) {
                    chargeCount++;
                    if (chargeCount < 3) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(BossPhase.Phase1_Prowl);
                }
            }
        }

        private void RunPhase1BladeStorm(Player target) {
            // 金属刃环：在自身周围释放旋转金属刃
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.1f);

            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int bladeCount = Main.expertMode ? 12 : 8;
                float baseAngle = globalTime * 2f;
                for (int i = 0; i < bladeCount; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / bladeCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 1f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 150;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase1_Prowl);
        }

        private void RunPhase1TigerRoar(Player target) {
            // 虎啸：音波攻击 + 减速Debuff
            NPC.velocity *= 0.9f;

            if (AttackTimer == 30) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 12f, 6f, 25, 1500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 环形音波弹幕
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int ring = 0; ring < 2; ring++) {
                        int count = 16;
                        for (int i = 0; i < count; i++) {
                            float angle = MathHelper.TwoPi / count * i + ring * MathHelper.ToRadians(11.25f);
                            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + ring * 4f);
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ProjectileID.DD2SquireSonicBoom, NPC.damage / 5, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) {
                                Main.projectile[proj].friendly = false;
                                Main.projectile[proj].hostile = true;
                                Main.projectile[proj].tileCollide = false;
                                Main.projectile[proj].timeLeft = 120;
                            }
                        }
                    }
                }
            }

            // 音波粒子
            if (Main.netMode != NetmodeID.Server && AttackTimer >= 25 && AttackTimer <= 40) {
                for (int i = 0; i < 8; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = (AttackTimer - 25) * 15f;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Silver, 0, 0, 100, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                }
            }

            if (AttackTimer > 80) TransitionTo(BossPhase.Phase1_Prowl);
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true;

            // 金属碎片向内聚集
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 300 - PhaseTimer * 2;
                    if (dist < 30) dist = 30;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Silver, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 45, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                TransitionTo(BossPhase.Phase2_FrenzyCharge);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(5, 5);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 15; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 250);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.GoldCoin;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 金属爆发
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 14f;
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) {
                            Main.projectile[proj].friendly = false;
                            Main.projectile[proj].hostile = true;
                            Main.projectile[proj].tileCollide = false;
                            Main.projectile[proj].timeLeft = 150;
                        }
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 2f;
                TransitionTo(BossPhase.Phase3_FuryProwl);
            }
        }

        #endregion

        #region 二阶段：钢虎

        private void RunPhase2FrenzyCharge(Player target) {
            // 极速连续冲刺
            if (SubState == 0) {
                NPC.velocity *= 0.8f;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);
                if (AttackTimer > 20) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 38f;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Silver, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                    }
                }
                if (AttackTimer > 20) NPC.velocity *= 0.9f;
                if (AttackTimer > 35) {
                    chargeCount++;
                    if (chargeCount < 6) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2MetalRain(Player target) {
            // 金属雨：从天空落下金属碎片
            Vector2 hoverPos = target.Center + new Vector2(0, -500);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.08f);

            int interval = Main.expertMode ? 5 : 8;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int shardCount = Main.expertMode ? 3 : 2;
                for (int i = 0; i < shardCount; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), -600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(15f, 22f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 180;
                    }
                }
            }

            if (AttackTimer > 150) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2EarthShatter(Player target) {
            // 大地震击：冲向地面制造冲击波
            if (SubState == 0) {
                // 飞到高处
                Vector2 highPos = target.Center + new Vector2(0, -500);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (highPos - NPC.Center) * 0.06f, 0.1f);
                if (AttackTimer > 40) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = new Vector2(0, 40f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // 下砸中
                if (AttackTimer > 30 || NPC.Center.Y > target.Center.Y + 50) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;

                    // 冲击波弹幕
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 10; i++) {
                            float speed = 8f + i * 1.5f;
                            int projR = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(speed, -4f),
                                ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 0f, Main.myPlayer);
                            int projL = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(-speed, -4f),
                                ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 0f, Main.myPlayer);
                            void SetupProj(int p) {
                                if (p >= 0 && p < Main.maxProjectiles) {
                                    Main.projectile[p].friendly = false;
                                    Main.projectile[p].hostile = true;
                                    Main.projectile[p].tileCollide = false;
                                    Main.projectile[p].timeLeft = 120;
                                }
                            }
                            SetupProj(projR);
                            SetupProj(projL);
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitY, 20f, 10f, 30, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);

                        for (int i = 0; i < 30; i++) {
                            Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-100, 100), 0), 0, 0, DustID.Smoke, Main.rand.NextFloat(-5, 5), -Main.rand.NextFloat(3, 8), 100, default, 2f);
                            d.noGravity = true;
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (AttackTimer > 60) TransitionTo(GetRandomPhase2Attack());
            }
        }

        private void RunPhase2IronWall(Player target) {
            // 铁壁：召唤环绕金属护盾同时攻击
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 2f) * 200f, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.1f);

            // 旋转的金属弹幕围绕boss
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int orbCount = 6;
                for (int i = 0; i < orbCount; i++) {
                    float angle = MathHelper.TwoPi / orbCount * i + globalTime * 3f;
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 10f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 120;
                    }
                }
            }

            // 防御增强期间受伤减少
            NPC.defense = IsPhase3 ? 120 : 100;

            if (AttackTimer > 180) {
                NPC.defense = IsPhase3 ? 85 : 70;
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        private void RunPhase2FuryCombo(Player target) {
            // 连击组合：扑击 → 爪击 → 虎啸
            switch (comboStep) {
                case 0: // 扑击
                    if (SubState == 0) {
                        NPC.velocity *= 0.85f;
                        if (AttackTimer > 20) {
                            SubState = 1;
                            AttackTimer = 0;
                            NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 35f;
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
                            NPC.netUpdate = true;
                        }
                    }
                    else {
                        if (AttackTimer > 20) NPC.velocity *= 0.9f;
                        if (AttackTimer > 35) {
                            comboStep = 1;
                            SubState = 0;
                            AttackTimer = 0;
                        }
                    }
                    break;

                case 1: // 爪击
                    NPC.velocity *= 0.8f;
                    if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        for (int i = -3; i <= 3; i++) {
                            Vector2 vel = dir.RotatedBy(i * MathHelper.ToRadians(10f)) * 18f;
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) {
                                Main.projectile[proj].friendly = false;
                                Main.projectile[proj].hostile = true;
                                Main.projectile[proj].tileCollide = false;
                                Main.projectile[proj].timeLeft = 90;
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                    }
                    if (AttackTimer > 35) {
                        comboStep = 2;
                        AttackTimer = 0;
                    }
                    break;

                case 2: // 虎啸
                    NPC.velocity *= 0.85f;
                    if (AttackTimer == 20) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 20; i++) {
                                float angle = MathHelper.TwoPi / 20 * i;
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 0f, Main.myPlayer);
                                if (proj >= 0 && proj < Main.maxProjectiles) {
                                    Main.projectile[proj].friendly = false;
                                    Main.projectile[proj].hostile = true;
                                    Main.projectile[proj].tileCollide = false;
                                    Main.projectile[proj].timeLeft = 120;
                                }
                            }
                        }
                        if (Main.netMode != NetmodeID.Server) {
                            PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 10f, 5f, 20, 1000f, FullName);
                            Main.instance.CameraModifiers.Add(modifier);
                        }
                    }
                    if (AttackTimer > 50) TransitionTo(GetRandomPhase2Attack());
                    break;
            }
        }

        #endregion

        #region 三阶段：神虎

        private void RunPhase3FuryProwl(Player target) {
            prowlAngle += 0.05f;
            float radius = 300f + MathF.Sin(globalTime * 3f) * 60f;
            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(prowlAngle), MathF.Sin(prowlAngle) * 0.3f) * radius;
            orbitPos.Y -= 180f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitPos - NPC.Center) * 0.1f, 0.12f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 80) TransitionTo(GetRandomPhase3Attack());
        }

        private void RunPhase3MetalTempest(Player target) {
            // 金属风暴：大量旋转金属弹幕
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float baseAngle = AttackTimer * 0.15f;
                int count = 6;
                for (int i = 0; i < count; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / count * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ProjectileID.DD2SquireSonicBoom, NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 180;
                    }
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase3_FuryProwl);
        }

        private void RunPhase3WhiteGoldBeam(Player target) {
            // 白金射线：锁定方向释放密集射线
            if (SubState == 0) {
                // 蓄力瞄准
                NPC.velocity *= 0.9f;
                NPC.Center += Main.rand.NextVector2Circular(2, 2);

                if (Main.netMode != NetmodeID.Server) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + toPlayer * Main.rand.NextFloat(50, 200), 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = toPlayer * 5f;
                    }
                }

                if (AttackTimer > 50) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                // 发射连续射线
                int beamInterval = Main.expertMode ? 3 : 4;
                if (AttackTimer % beamInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 vel = toPlayer * 22f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(5f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toPlayer * 50f, vel,
                        ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 120;
                    }
                }

                if (AttackTimer > 90) TransitionTo(BossPhase.Phase3_FuryProwl);
            }
        }

        private void RunPhase3ExtinctionClaw(Player target) {
            // 灭世之爪：超大范围爪击
            if (SubState == 0) {
                NPC.velocity *= 0.85f;
                NPC.Center += Main.rand.NextVector2Circular(4, 4);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(100, 100), 0, 0, DustID.GoldCoin, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }

                if (AttackTimer > 60) {
                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);

                    // 三方向巨型爪击弹幕
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        float[] angles = { -MathHelper.ToRadians(30f), 0, MathHelper.ToRadians(30f) };
                        foreach (float a in angles) {
                            for (int i = 0; i < 8; i++) {
                                Vector2 vel = dir.RotatedBy(a) * (12f + i * 2f);
                                vel = vel.RotatedByRandom(MathHelper.ToRadians(3f));
                                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 2f, Main.myPlayer);
                                if (proj >= 0 && proj < Main.maxProjectiles) {
                                    Main.projectile[proj].friendly = false;
                                    Main.projectile[proj].hostile = true;
                                    Main.projectile[proj].tileCollide = false;
                                    Main.projectile[proj].timeLeft = 150;
                                }
                            }
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX), 20f, 12f, 40, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                if (AttackTimer > 60) TransitionTo(BossPhase.Phase3_FuryProwl);
            }
        }

        private void RunPhase3TigerGodsFury(Player target) {
            // 虎神之怒：终极攻击
            if (SubState == 0) {
                NPC.velocity *= 0.9f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(5, 5);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 12; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(50, 350);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.GoldCoin;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                    }
                }

                if (AttackTimer > 80) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 2f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    // 全方位金属爆发 + 超高速冲刺组合
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int wave = 0; wave < 4; wave++) {
                            int count = 16;
                            for (int i = 0; i < count; i++) {
                                float angle = MathHelper.TwoPi / count * i + wave * MathHelper.ToRadians(11.25f);
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + wave * 4f);
                                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ProjectileID.DD2SquireSonicBoom, NPC.damage / 3, 0f, Main.myPlayer);
                                if (proj >= 0 && proj < Main.maxProjectiles) {
                                    Main.projectile[proj].friendly = false;
                                    Main.projectile[proj].hostile = true;
                                    Main.projectile[proj].tileCollide = false;
                                    Main.projectile[proj].timeLeft = 180;
                                }
                            }
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // 爆发后超高速冲刺
                if (AttackTimer == 10) {
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 45f;
                }
                if (AttackTimer > 30) NPC.velocity *= 0.9f;
                if (AttackTimer > 50) {
                    SubState = 2;
                    AttackTimer = 0;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                if (AttackTimer > 40) TransitionTo(BossPhase.Phase3_FuryProwl);
            }
        }

        #endregion
    }
}

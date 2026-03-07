using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    /// <summary>
    /// 朱雀 - 南方神兽，火/日属性
    /// 凤凰形态的Boss，华丽的火焰攻击
    /// 一阶段：炎鸟翱翔，火球与火柱
    /// 二阶段：太阳之翼，凤凰俯冲与烈焰漩涡
    /// 三阶段：涅槃朱雀，浴火重生与终极火雨
    /// </summary>
    [AutoloadBossHead]
    public class Suzaku : ModNPC
    {
        #region 常量定义

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.30f;
        public const float RebirthThreshold = 0.10f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Soar,
            Phase1_FireballBarrage,
            Phase1_FlamePillar,
            Phase1_FeatherRain,
            Phase1_HeatWave,
            PhaseTransition_2,
            Phase2_PhoenixDive,
            Phase2_SunCircle,
            Phase2_SolarFlare,
            Phase2_FlameTornado,
            Phase2_WingStorm,
            PhaseTransition_3,
            Phase3_NirvanaFlight,
            Phase3_VermillionRain,
            Phase3_SolarJudgment,
            Phase3_PhoenixDance,
            Phase3_NirvanaFlames,
            Phase3_Rebirth
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
        private bool didRebirth;

        private int diveCount;
        private float soarAngle;
        private float glowIntensity = 1f;
        private float wingSpread;

        #endregion

        #region ModNPC重写

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
            NPC.width = 170;
            NPC.height = 170;
            NPC.damage = 240;
            NPC.defense = 65;
            NPC.lifeMax = 5000000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 5);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;
            NPC.lavaImmune = true;

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
            writer.Write(didRebirth);
            writer.Write(diveCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didRebirth = reader.ReadBoolean();
            diveCount = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, hit.HitDirection * 2f, -1f, 100, default, 2f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.SolarFlare, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedSuzaku = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 25f, 12f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override bool CheckDead() {
            // 涅槃重生机制：第一次到达0HP时恢复20%生命
            if (!didRebirth && IsPhase3) {
                didRebirth = true;
                NPC.life = (int)(NPC.lifeMax * 0.20f);
                NPC.dontTakeDamage = true;
                TransitionTo(BossPhase.Phase3_Rebirth);
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        #endregion

        #region AI主循环

        public override void AI() {
            globalTime += 1f / 60f;
            wingSpread = 0.8f + MathF.Sin(globalTime * 4f) * 0.2f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 1f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Soar: RunPhase1Soar(target); break;
                case BossPhase.Phase1_FireballBarrage: RunPhase1FireballBarrage(target); break;
                case BossPhase.Phase1_FlamePillar: RunPhase1FlamePillar(target); break;
                case BossPhase.Phase1_FeatherRain: RunPhase1FeatherRain(target); break;
                case BossPhase.Phase1_HeatWave: RunPhase1HeatWave(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_PhoenixDive: RunPhase2PhoenixDive(target); break;
                case BossPhase.Phase2_SunCircle: RunPhase2SunCircle(target); break;
                case BossPhase.Phase2_SolarFlare: RunPhase2SolarFlare(target); break;
                case BossPhase.Phase2_FlameTornado: RunPhase2FlameTornado(target); break;
                case BossPhase.Phase2_WingStorm: RunPhase2WingStorm(target); break;
                case BossPhase.PhaseTransition_3: RunPhaseTransition3(target); break;
                case BossPhase.Phase3_NirvanaFlight: RunPhase3NirvanaFlight(target); break;
                case BossPhase.Phase3_VermillionRain: RunPhase3VermillionRain(target); break;
                case BossPhase.Phase3_SolarJudgment: RunPhase3SolarJudgment(target); break;
                case BossPhase.Phase3_PhoenixDance: RunPhase3PhoenixDance(target); break;
                case BossPhase.Phase3_NirvanaFlames: RunPhase3NirvanaFlames(target); break;
                case BossPhase.Phase3_Rebirth: RunPhase3Rebirth(target); break;
            }

            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.015f;

            // 火焰光源
            float fireIntensity = IsPhase3 ? 2f : (IsPhase2 ? 1.5f : 1f);
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.4f, 0.1f) * glowIntensity * fireIntensity);

            // 常态火焰粒子
            if (Main.netMode != NetmodeID.Server && Phase != BossPhase.Intro) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, -2f, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity += -NPC.velocity * 0.05f;
                }
            }
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
            diveCount = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_FireballBarrage,
                1 => (int)BossPhase.Phase1_FlamePillar,
                2 => (int)BossPhase.Phase1_FeatherRain,
                _ => (int)BossPhase.Phase1_HeatWave
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_PhoenixDive,
                1 => (int)BossPhase.Phase2_SunCircle,
                2 => (int)BossPhase.Phase2_SolarFlare,
                3 => (int)BossPhase.Phase2_FlameTornado,
                _ => (int)BossPhase.Phase2_WingStorm
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase3_VermillionRain,
                1 => (int)BossPhase.Phase3_SolarJudgment,
                2 => (int)BossPhase.Phase3_PhoenixDance,
                _ => (int)BossPhase.Phase3_NirvanaFlames
            });
        }

        private int FireProjectile(Vector2 pos, Vector2 vel, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ProjectileID.InfernoFriendlyBlast, damage, 0f, Main.myPlayer);
            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].friendly = false;
                Main.projectile[proj].hostile = true;
                Main.projectile[proj].tileCollide = false;
                Main.projectile[proj].timeLeft = 180;
            }
            return proj;
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                // 朱雀从天空降临，烈焰环绕
                NPC.Center = target.Center + new Vector2(0, -800);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, target.Center);
            }

            Vector2 targetPos = target.Center + new Vector2(0, -350);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.02f);

            // 降临火焰特效
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.SolarFlare, 0, -3f, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity += (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer >= 110) {
                // 展翅爆发
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 1.5f }, NPC.Center);

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 15f, 8f, 35, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 30; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 3f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(15, 15);
                    }
                }

                TransitionTo(BossPhase.Phase1_Soar);
            }
        }

        #endregion

        #region 一阶段：炎鸟

        private void RunPhase1Soar(Player target) {
            // 优雅的8字翱翔
            soarAngle += 0.02f;
            float xRadius = 400f;
            float yRadius = 200f;
            Vector2 soarPos = target.Center + new Vector2(MathF.Sin(soarAngle * 2f) * xRadius, MathF.Sin(soarAngle) * yRadius - 300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (soarPos - NPC.Center) * 0.06f, 0.08f);

            if (PhaseTimer > 130) TransitionTo(GetRandomPhase1Attack());
        }

        private void RunPhase1FireballBarrage(Player target) {
            // 火球连射：朝玩家连续发射火球
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 2f) * 250f, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            int interval = Main.expertMode ? 8 : 12;
            if (AttackTimer % interval == 0) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float speed = 14f + Main.rand.NextFloat(0f, 3f);
                Vector2 vel = toPlayer.RotatedByRandom(MathHelper.ToRadians(8f)) * speed;
                FireProjectile(NPC.Center, vel, NPC.damage / 4);
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase1_Soar);
        }

        private void RunPhase1FlamePillar(Player target) {
            // 火柱：在玩家周围从下方升起火柱
            NPC.velocity = Vector2.Lerp(NPC.velocity, (target.Center + new Vector2(0, -400) - NPC.Center) * 0.03f, 0.06f);

            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int pillarCount = Main.expertMode ? 5 : 3;
                for (int i = 0; i < pillarCount; i++) {
                    float xOff = Main.rand.NextFloat(-400, 400);
                    Vector2 pos = target.Center + new Vector2(xOff, 400);
                    Vector2 vel = new Vector2(0, -18f);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ProjectileID.InfernoFriendlyBlast, NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 120;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.2f }, target.Center);
            }

            if (AttackTimer > 130) TransitionTo(BossPhase.Phase1_Soar);
        }

        private void RunPhase1FeatherRain(Player target) {
            // 羽雨：燃烧的羽毛从朱雀身上散落
            Vector2 hoverPos = target.Center + new Vector2(0, -450);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);
            NPC.velocity.X += MathF.Sin(globalTime * 3f) * 2f;

            int interval = Main.expertMode ? 6 : 10;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int featherCount = Main.expertMode ? 4 : 2;
                for (int i = 0; i < featherCount; i++) {
                    Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(100, 40);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(6f, 12f));
                    FireProjectile(pos, vel, NPC.damage / 5);
                }
            }

            if (AttackTimer > 140) TransitionTo(BossPhase.Phase1_Soar);
        }

        private void RunPhase1HeatWave(Player target) {
            // 热浪：向外扩展的火焰环
            NPC.velocity *= 0.92f;

            if (AttackTimer == 20) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.3f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = Main.expertMode ? 20 : 14;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        FireProjectile(NPC.Center, vel, NPC.damage / 5);
                    }
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 20; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10, 10);
                    }
                }
            }

            if (AttackTimer > 70) TransitionTo(BossPhase.Phase1_Soar);
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            // 火焰向内聚集，凤凰涅槃强化
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 400 - PhaseTimer * 3;
                    if (dist < 40) dist = 40;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Torch;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 45, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                glowIntensity = 1.5f;
                TransitionTo(BossPhase.Phase2_PhoenixDive);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 18; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 300);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 火焰爆发
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 24; i++) {
                        float angle = MathHelper.TwoPi / 24 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                        FireProjectile(NPC.Center, vel, NPC.damage / 3);
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 2.5f;
                TransitionTo(BossPhase.Phase3_NirvanaFlight);
            }
        }

        #endregion

        #region 二阶段：太阳之翼

        private void RunPhase2PhoenixDive(Player target) {
            // 凤凰俯冲：从空中急速俯冲玩家，留下火焰轨迹
            if (SubState == 0) {
                // 飞高蓄力
                Vector2 highPos = target.Center + new Vector2(Main.rand.NextFloat(-200, 200), -600);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (highPos - NPC.Center) * 0.06f, 0.1f);

                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 3f;
                }

                if (AttackTimer > 40) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 35f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                // 俯冲中，释放火焰尾迹
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 80, default, 3f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                    }
                }

                // 沿途释放火弹
                if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    FireProjectile(NPC.Center + perpDir * 30f, perpDir * 6f, NPC.damage / 5);
                    FireProjectile(NPC.Center - perpDir * 30f, -perpDir * 6f, NPC.damage / 5);
                }

                if (AttackTimer > 25) NPC.velocity *= 0.9f;
                if (AttackTimer > 40) {
                    diveCount++;
                    if (diveCount < 4) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2SunCircle(Player target) {
            // 太阳环：围绕玩家创造一圈火焰
            NPC.velocity = Vector2.Lerp(NPC.velocity, (target.Center + new Vector2(0, -400) - NPC.Center) * 0.03f, 0.06f);

            if (AttackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 24 : 16;
                float radius = 300f;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i;
                    Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ProjectileID.InfernoFriendlyBlast, NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].tileCollide = false;
                        Main.projectile[proj].timeLeft = 120;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.2f }, target.Center);
            }

            // 第二波更紧密的环
            if (AttackTimer == 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 24 : 16;
                float radius = 180f;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i + MathHelper.ToRadians(7.5f);
                    Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 8f;
                    FireProjectile(pos, vel, NPC.damage / 4);
                }
            }

            if (AttackTimer > 130) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2SolarFlare(Player target) {
            // 太阳耀斑：释放追踪火球
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 2f) * 300f, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int flareCount = Main.expertMode ? 3 : 2;
                for (int i = 0; i < flareCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 80f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 10f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(15f));
                    FireProjectile(pos, vel, NPC.damage / 4);
                }
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f }, NPC.Center);
            }

            if (AttackTimer > 120) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2FlameTornado(Player target) {
            // 火焰漩涡：在玩家方向释放旋转的火焰柱
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.06f);

            if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float spiralAngle = AttackTimer * 0.12f;
                float radius = 50f + AttackTimer * 2f;
                Vector2 pos = target.Center + new Vector2(MathF.Cos(spiralAngle), MathF.Sin(spiralAngle)) * radius;
                Vector2 vel = new Vector2(MathF.Cos(spiralAngle + MathHelper.PiOver2), MathF.Sin(spiralAngle + MathHelper.PiOver2)) * 3f;
                FireProjectile(pos, vel, NPC.damage / 5);
            }

            if (AttackTimer > 150) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2WingStorm(Player target) {
            // 翼风暴：快速扇动翅膀释放大量火焰弹幕
            NPC.velocity *= 0.92f;
            NPC.Center += Main.rand.NextVector2Circular(3, 3);

            int interval = Main.expertMode ? 5 : 8;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                int count = 5;
                float spread = MathHelper.ToRadians(40f);
                for (int i = 0; i < count; i++) {
                    float angle = -spread / 2 + spread / (count - 1) * i;
                    Vector2 vel = dir.RotatedBy(angle) * Main.rand.NextFloat(12f, 18f);
                    FireProjectile(NPC.Center, vel, NPC.damage / 4);
                }
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.5f, Volume = 0.4f }, NPC.Center);
            }

            if (AttackTimer > 100) TransitionTo(GetRandomPhase2Attack());
        }

        #endregion

        #region 三阶段：涅槃朱雀

        private void RunPhase3NirvanaFlight(Player target) {
            soarAngle += 0.04f;
            float radius = 350f;
            Vector2 soarPos = target.Center + new Vector2(MathF.Cos(soarAngle) * radius, MathF.Sin(soarAngle * 2f) * 150f - 300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (soarPos - NPC.Center) * 0.08f, 0.1f);

            // 涅槃火焰尾迹
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 50), 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = -NPC.velocity * 0.1f;
                }
            }

            if (PhaseTimer > 90) TransitionTo(GetRandomPhase3Attack());
        }

        private void RunPhase3VermillionRain(Player target) {
            // 朱雀火雨：全屏火焰倾泻
            Vector2 hoverPos = target.Center + new Vector2(0, -500);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            int interval = Main.expertMode ? 3 : 5;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 4 : 3;
                for (int i = 0; i < count; i++) {
                    float x = target.Center.X + Main.rand.NextFloat(-600, 600);
                    Vector2 pos = new Vector2(x, target.Center.Y - 700);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(14f, 20f));
                    FireProjectile(pos, vel, NPC.damage / 4);
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase3_NirvanaFlight);
        }

        private void RunPhase3SolarJudgment(Player target) {
            // 太阳审判：生成巨型火球压向玩家
            if (SubState == 0) {
                NPC.velocity *= 0.9f;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(120, 120), 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }

                if (AttackTimer > 70) {
                    SubState = 1;
                    AttackTimer = 0;

                    SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

                    // 大量火球向玩家方向发射
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        for (int ring = 0; ring < 3; ring++) {
                            int count = 12;
                            for (int i = 0; i < count; i++) {
                                float angle = MathHelper.TwoPi / count * i + ring * MathHelper.ToRadians(15f);
                                Vector2 vel = dir * (10f + ring * 5f) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                                FireProjectile(NPC.Center, vel, NPC.damage / 3);
                            }
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY), 15f, 10f, 30, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (AttackTimer > 60) TransitionTo(BossPhase.Phase3_NirvanaFlight);
            }
        }

        private void RunPhase3PhoenixDance(Player target) {
            // 凤凰之舞：高速连续俯冲，每次留下火焰十字弹幕
            if (SubState == 0) {
                NPC.velocity *= 0.8f;
                if (AttackTimer > 15) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 40f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1.2f, Volume = 0.8f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                    }
                }

                // 冲刺中途释放十字弹幕
                if (AttackTimer == 12 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2[] crossDirs = { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };
                    foreach (Vector2 d in crossDirs) {
                        for (int i = 1; i <= 3; i++) {
                            FireProjectile(NPC.Center, d * (6f + i * 3f), NPC.damage / 4);
                        }
                    }
                }

                if (AttackTimer > 20) NPC.velocity *= 0.9f;
                if (AttackTimer > 30) {
                    diveCount++;
                    if (diveCount < 6) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(BossPhase.Phase3_NirvanaFlight);
                }
            }
        }

        private void RunPhase3NirvanaFlames(Player target) {
            // 涅槃之焰：终极攻击
            if (SubState == 0) {
                NPC.velocity *= 0.88f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(5, 5);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(60, 400);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                    }
                }

                if (AttackTimer > 90) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;

                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 2f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    // 四波旋转火焰弹幕 + 火雨
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int wave = 0; wave < 5; wave++) {
                            int count = 18;
                            for (int i = 0; i < count; i++) {
                                float angle = MathHelper.TwoPi / count * i + wave * MathHelper.ToRadians(10f);
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (6f + wave * 3f);
                                FireProjectile(NPC.Center, vel, NPC.damage / 3);
                            }
                        }
                        // 额外火雨
                        for (int i = 0; i < 15; i++) {
                            float x = target.Center.X + Main.rand.NextFloat(-500, 500);
                            Vector2 pos = new Vector2(x, target.Center.Y - 600);
                            Vector2 vel = new Vector2(0, Main.rand.NextFloat(12f, 18f));
                            FireProjectile(pos, vel, NPC.damage / 4);
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                if (AttackTimer > 80) TransitionTo(BossPhase.Phase3_NirvanaFlight);
            }
        }

        private void RunPhase3Rebirth(Player target) {
            // 涅槃重生动画
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);

            if (Main.netMode != NetmodeID.Server) {
                float intensity = PhaseTimer / 120f;
                for (int i = 0; i < (int)(20 * intensity); i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 200);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Torch;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f + intensity);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (5f + intensity * 5f);
                }
            }

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 50; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 4f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(20, 20);
                    }
                }

                // 重生爆发弹幕
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi / 30 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 14f;
                        FireProjectile(NPC.Center, vel, NPC.damage / 3);
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                glowIntensity = 3f;
                TransitionTo(BossPhase.Phase3_NirvanaFlight);
            }
        }

        #endregion

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            // 纹理正向朝右，朝左飞行时使用垂直翻转来修正旋转导致的上下颠倒
            bool facingLeft = MathF.Abs(NPC.rotation) > MathHelper.PiOver2;
            SpriteEffects effects = NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 龙形残影
            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float trailRot = NPC.oldRot[i];
                bool trailLeft = MathF.Abs(trailRot) > MathHelper.PiOver2;
                SpriteEffects trailFx = effects;
                float alpha = 0.5f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                Color trailColor = drawColor * alpha;
                trailColor.G = (byte)Math.Min(trailColor.G * 1.3f, 255);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin,
                    NPC.scale * (1f - i * 0.015f), trailFx, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }
    }
}

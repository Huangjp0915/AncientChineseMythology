using AncientChineseMythology.Celestias.Boss.AncestralDragonSouls.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂头部 — V2 作者化战斗:
    /// 阶段1(>70%): 基础机动 + 多种已预警弹幕。
    /// 半血(50%): 真分裂双子 (主=符文/场地控制 · 副=交叉火力/机动), 脚本化 双链→交叉 开场。
    /// 残血(双龙合计 <25%): **双魂回拢终曲** —— ~i-frame 回拢过场把双子合体为更巨大的「太初真身」(单一血池),
    ///   随后进入**确定性终曲循环**: 刀魂碎片场 (谜题窗口) → 喘息 → 阴阳超载 (强制机制) → 喘息 →
    ///   螺旋俯冲+掠袭冲锋 编排终极 → 喘息 → 循环。**狂暴是一幕戏, 不是加速档**。
    /// 每个大招后强制无弹幕喘息拍; 移除了 V1 的 aggressionBuildup 加速 hack。
    /// </summary>
    [AutoloadBossHead]
    public class AncestralDragonSoulHead : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Head;

        public enum AIState : int
        {
            Intro,
            Patrol,
            ScaleBarrage,
            SigilEruption,
            YinYangBind,
            DragonBeam,
            SpiralDive,
            RakingCharge,
            SplitTransition,
            TwinLink,
            TwinCrossfire,
            TwinPressure,
            Recovery,
            EnrageTransition,
            EnrageDaoField,
            EnrageYinYang,
            EnrageUltimate
        }

        public AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
        public ref float StateTimer => ref NPC.ai[1];
        public ref float SubTimer => ref NPC.ai[2];
        public ref float StateData => ref NPC.ai[3];

        public const int TwinId = 1;
        public const int MainId = 0;

        private bool didSplit;
        private bool didEnrage;
        private bool merged;
        private int enrageStep;
        private int partnerIndex = -1;
        private int attackSelectCooldown;

        // 纯本地表现层标量 (不参与同步)
        private float transientBloom;   // 大节拍泛光 (合体/解锁/释放), 逐帧衰减
        private float warpIntensity;    // GenericWarp(太初雾) 全屏后处理强度
        private bool showChargeTele;    // 终极掠袭蓄力红线预警
        private Vector2 chargeTeleTarget;

        /// <summary>是否已合体为太初真身 (驱动视觉放大, 全客户端同步)。</summary>
        public bool Merged => merged;
        /// <summary>是否处于刀魂碎片场的"布场谜题"子阶段 (碎片节点据此判断存活)。</summary>
        public bool DaoFieldArming => State == AIState.EnrageDaoField && StateData == 0f;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.value = Item.buyPrice(gold: 10);
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AncestralDragonSoulBody>();
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
            if (!IsTwin) {
                State = AIState.Intro;
                StateTimer = 0;
                SubTimer = 0;
            }
            else {
                State = AIState.Patrol;
                NPC.life = NPC.lifeMax = Main.npc[(int)StateData].life;
            }
            segmentIndex = 0;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(didSplit);
            writer.Write(didEnrage);
            writer.Write(merged);
            writer.Write(IsTwin);
            writer.Write(partnerIndex);
            writer.Write(enrageStep);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            didSplit = reader.ReadBoolean();
            didEnrage = reader.ReadBoolean();
            merged = reader.ReadBoolean();
            IsTwin = reader.ReadBoolean();
            partnerIndex = reader.ReadInt32();
            enrageStep = reader.ReadInt32();
        }

        public override bool CheckActive() => false;

        public override void AI() {
            base.AI();

            if (!Target.Alives()) {
                NPC.TargetClosest();
                if (!Target.Alives()) {
                    NPC.velocity.Y -= 0.2f;
                    NPC.EncourageDespawn(10);
                    return;
                }
            }

            // 半血分裂: 只对主龙触发一次
            if (!IsTwin && !didSplit && NPC.life < NPC.lifeMax * 0.5f && State != AIState.Intro) {
                TransitionTo(AIState.SplitTransition);
                didSplit = true;
            }

            // 残血(双龙合计 <25%): 进入双魂回拢终曲。只对主龙触发一次, 真正成为一幕"act"。
            if (!IsTwin && !didEnrage && CombinedFraction() < 0.25f
                && State != AIState.Intro && State != AIState.SplitTransition) {
                didEnrage = true;
                TransitionTo(AIState.EnrageTransition);
                CommandTwin(AIState.EnrageTransition, 0f);
                transientBloom = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 14f, 8f, 50, 2000f, "AncestralDragonEnrage"));
                    ACMUtils.AddScreenShake(12f);
                    AncestralDragonSky.TriggerFlash(1f);
                }
            }

            // 头部自管无敌: 各状态每帧按需置 true; 默认可被击中。(身体段继承 realLife 头部的 dontTakeDamage)
            NPC.dontTakeDamage = false;
            showChargeTele = false;

            switch (State) {
                case AIState.Intro: UpdateIntro(); break;
                case AIState.Patrol: UpdatePatrol(); break;
                case AIState.ScaleBarrage: UpdateScaleBarrage(); break;
                case AIState.SigilEruption: UpdateSigilEruption(); break;
                case AIState.YinYangBind: UpdateYinYangBind(); break;
                case AIState.DragonBeam: UpdateDragonBeam(); break;
                case AIState.SpiralDive: UpdateSpiralDive(); break;
                case AIState.RakingCharge: UpdateRakingCharge(); break;
                case AIState.SplitTransition: UpdateSplitTransition(); break;
                case AIState.TwinLink: UpdateTwinLink(); break;
                case AIState.TwinCrossfire: UpdateTwinCrossfire(); break;
                case AIState.TwinPressure: UpdateTwinPressure(); break;
                case AIState.Recovery: UpdateRecovery(); break;
                case AIState.EnrageTransition: UpdateEnrageTransition(); break;
                case AIState.EnrageDaoField: UpdateEnrageDaoField(); break;
                case AIState.EnrageYinYang: UpdateEnrageYinYang(); break;
                case AIState.EnrageUltimate: UpdateEnrageUltimate(); break;
            }

            transientBloom = MathHelper.Lerp(transientBloom, 0f, 0.05f);
            PublishScreenFx();

            StateTimer++;
            SubTimer++;
        }

        private void TransitionTo(AIState newState) {
            State = newState;
            StateTimer = 0;
            SubTimer = 0;
            StateData = 0;
            NPC.netUpdate = true;
        }

        #region 协同/狂暴 辅助

        private NPC GetTwin() {
            if (IsTwin) return null;
            if (partnerIndex < 0 || partnerIndex >= Main.maxNPCs) return null;
            NPC t = Main.npc[partnerIndex];
            if (t.active && t.type == Type && t.ModNPC is AncestralDragonSoulHead h && h.IsTwin) return t;
            return null;
        }

        /// <summary>双龙合计血量比例 (用于"合计 &lt;25%"判定); 无双子时即自身比例。</summary>
        private float CombinedFraction() {
            float life = NPC.life, max = NPC.lifeMax;
            NPC t = GetTwin();
            if (t != null) { life += t.life; max += t.lifeMax; }
            return max <= 0f ? 0f : life / max;
        }

        /// <summary>(服务器/SP) 命令副本龙切到指定状态, 用于脚本化协同节拍。</summary>
        private void CommandTwin(AIState s, float data) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            NPC t = GetTwin();
            if (t != null && t.ModNPC is AncestralDragonSoulHead th) {
                th.State = s;
                th.StateTimer = 0;
                th.SubTimer = 0;
                th.StateData = data;
                t.netUpdate = true;
            }
        }

        private static AIState EnrageStateFor(int step) => (step % 3) switch {
            0 => AIState.EnrageDaoField,
            1 => AIState.EnrageYinYang,
            _ => AIState.EnrageUltimate
        };

        /// <summary>大招收尾统一走喘息拍 (无弹幕), 由 <see cref="UpdateRecovery"/> 决定下一拍。</summary>
        private void BeginRecovery() => TransitionTo(AIState.Recovery);

        #endregion

        #region 机动辅助

        private void FlyToward(Vector2 destination, float acceleration, float maxSpeed, float turnAccel = -1f) {
            if (turnAccel < 0) turnAccel = acceleration * 0.6f;
            Vector2 toDest = destination - NPC.Center;
            float dist = toDest.Length();
            if (dist < 1f) return;
            Vector2 desiredDir = toDest / dist;
            Vector2 currentDir = NPC.velocity.SafeNormalize(desiredDir);
            Vector2 newDir = Vector2.Lerp(currentDir, desiredDir, 0.08f).SafeNormalize(desiredDir);
            float curSpeed = NPC.velocity.Length();
            curSpeed += acceleration;
            if (curSpeed > maxSpeed) curSpeed = maxSpeed;
            NPC.velocity = newDir * curSpeed;
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
        }

        private Vector2 LissajousPoint(float phase, float rx, float ry) {
            return new Vector2(rx * MathF.Cos(phase), ry * MathF.Sin(phase * 2f));
        }

        #endregion

        #region 基础状态

        private void UpdateIntro() {
            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -3f), 0.05f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, -MathHelper.PiOver2, 0.05f);
            mistAlpha = MathHelper.Lerp(mistAlpha, 1.2f, 0.02f);

            if (Main.netMode != NetmodeID.Server && StateTimer % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * 120f;
                int dust = Dust.NewDust(pos, 0, 0, DustID.WhiteTorch, 0, 0, 100, Color.White, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -ang.ToRotationVector2() * 4f;
            }

            if (StateTimer > 120) {
                TransitionTo(AIState.Patrol);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 10f, 6f, 30, 2000f, "AncestralIntro"));
                    ACMUtils.AddScreenShake(10f);
                }
            }
        }

        private void UpdatePatrol() {
            float phase = globalTime * 0.9f;
            Vector2 orbit = LissajousPoint(phase, 520f, 260f);
            Vector2 anchor = Target.Center + orbit + new Vector2(0, -60f);
            FlyToward(anchor, 0.55f, 16f);

            if (StateTimer > 90 && attackSelectCooldown <= 0) {
                PickNextAttack();
            }
            else if (attackSelectCooldown > 0) {
                attackSelectCooldown--;
            }
        }

        private void PickNextAttack() {
            AIState[] pool;
            if (didSplit) {
                // 互补角色: 主龙=符文/场地控制; 副龙=交叉火力/机动
                pool = IsTwin
                    ? new[] { AIState.ScaleBarrage, AIState.TwinCrossfire, AIState.SpiralDive, AIState.RakingCharge, AIState.DragonBeam }
                    : new[] { AIState.SigilEruption, AIState.YinYangBind, AIState.DragonBeam, AIState.TwinPressure, AIState.ScaleBarrage };
            }
            else if (NPC.life < NPC.lifeMax * 0.7f) {
                pool = new[] {
                    AIState.ScaleBarrage, AIState.SigilEruption, AIState.YinYangBind,
                    AIState.DragonBeam, AIState.SpiralDive, AIState.RakingCharge
                };
            }
            else {
                pool = new[] {
                    AIState.ScaleBarrage, AIState.SpiralDive, AIState.RakingCharge, AIState.DragonBeam
                };
            }

            AIState pick;
            do {
                pick = pool[Main.rand.Next(pool.Length)];
            } while (pick == (AIState)StateData && pool.Length > 1);
            float lastPick = (int)pick;
            attackSelectCooldown = 60;
            TransitionTo(pick);
            StateData = lastPick;
        }

        private void UpdateScaleBarrage() {
            Vector2 orbitPos = Target.Center + new Vector2(MathF.Cos(globalTime * 1.4f) * 550f, MathF.Sin(globalTime * 1.8f) * 260f - 80f);
            FlyToward(orbitPos, 0.8f, 20f);

            if (StateTimer % 22 == 0 && StateTimer > 25 && StateTimer < 160 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * 0.18f) * 6f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DragonScaleShard>(), NPC.damage / 3, 1f);
                }
                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (StateTimer > 200) BeginRecovery();
        }

        private void UpdateSigilEruption() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime * 1.2f) * 300f, -360f);
            FlyToward(hover, 0.5f, 14f);

            int casts = didSplit ? 6 : 4;
            int castInterval = 24;
            if (StateTimer > 40 && StateTimer < 40 + casts * castInterval && (StateTimer - 40) % castInterval == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int idx = (int)((StateTimer - 40) / castInterval);
                    float spread = (idx - (casts - 1) / 2f) * 160f;
                    Vector2 pos = Target.Center + new Vector2(spread + Main.rand.NextFloat(-40f, 40f), 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulSigil>(), NPC.damage / 2, 0f);
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (StateTimer > 40 + casts * castInterval + 160) BeginRecovery();
        }

        private void UpdateYinYangBind() {
            Vector2 hover = Target.Center + new Vector2(0, -420f);
            FlyToward(hover, 0.5f, 13f);

            if (StateTimer > 40 && StateTimer < 220 && (StateTimer - 40) % 55 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 center = NPC.Center + (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 120f;
                    float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
                    int a = Projectile.NewProjectile(NPC.GetSource_FromAI(), center + baseAng.ToRotationVector2() * 40f, Vector2.Zero,
                        ModContent.ProjectileType<YinYangBinderOrb>(), NPC.damage / 3, 0f, -1, -1, 0f);
                    int b = Projectile.NewProjectile(NPC.GetSource_FromAI(), center - baseAng.ToRotationVector2() * 40f, Vector2.Zero,
                        ModContent.ProjectileType<YinYangBinderOrb>(), NPC.damage / 3, 0f, -1, -1, MathHelper.Pi);
                    if (a >= 0 && b >= 0) {
                        Main.projectile[a].ai[0] = b;
                        Main.projectile[b].ai[0] = a;
                        Main.projectile[a].netUpdate = true;
                        Main.projectile[b].netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = -0.3f, Volume = 0.7f }, NPC.Center);
            }

            if (StateTimer > 260) BeginRecovery();
        }

        private void UpdateDragonBeam() {
            Vector2 hover = Target.Center + new Vector2(Target.Center.X > NPC.Center.X ? -600f : 600f, -200f);
            FlyToward(hover, 0.45f, 11f);

            if (StateTimer == 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<AncestralDragonBeam>(), NPC.damage / 2, 0f, -1, NPC.whoAmI,
                    (Target.Center - NPC.Center).ToRotation());
                SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
            }

            if (StateTimer > 200) BeginRecovery();
        }

        private void UpdateSpiralDive() {
            float t = StateTimer / 180f;
            float radius = MathHelper.Lerp(600f, 120f, t);
            float angularSpeed = MathHelper.Lerp(0.04f, 0.12f, t);
            StateData += angularSpeed;
            Vector2 orbit = Target.Center + StateData.ToRotationVector2() * radius;
            FlyToward(orbit, 1.2f, 28f);

            if (StateTimer % 10 == 0 && StateTimer > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                for (int i = -1; i <= 1; i += 2) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 5f,
                        ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.5f);
                }
            }

            if (StateTimer > 200) BeginRecovery();
        }

        private void UpdateRakingCharge() {
            const int charges = 3;
            const int windUp = 40;
            const int chargeDur = 45;
            int cycleLen = windUp + chargeDur;
            int currentCharge = (int)(StateTimer / cycleLen);
            int localT = (int)StateTimer % cycleLen;

            if (currentCharge >= charges) {
                BeginRecovery();
                return;
            }

            if (localT < windUp) {
                float side = (currentCharge % 2 == 0) ? 1f : -1f;
                Vector2 windPos = Target.Center + new Vector2(side * 780f, Main.rand.NextFloat(-80f, 80f));
                FlyToward(windPos, 1f, 20f);
                if (localT == windUp - 1) {
                    Vector2 dir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = dir * 38f;
                    NPC.netUpdate = true;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(6f);
                    }
                }
            }
            else {
                NPC.velocity *= 0.995f;
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;

                if (localT == windUp + 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 6f,
                            ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                    }
                }
            }
        }

        /// <summary>大招后强制喘息拍: 无弹幕, 缓飞, 给玩家输出/呼吸窗口。狂暴期则推进确定性循环。</summary>
        private void UpdateRecovery() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime) * 220f, -300f);
            FlyToward(hover, 0.35f, 9f);

            float dur = didEnrage ? 70f : 80f;
            if (StateTimer > dur) {
                if (didEnrage) {
                    enrageStep++;
                    TransitionTo(EnrageStateFor(enrageStep));
                }
                else {
                    TransitionTo(AIState.Patrol);
                }
            }
        }

        #endregion

        #region 半血分裂 / 双龙协同

        private void UpdateSplitTransition() {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (StateTimer == 1) {
                transientBloom = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.2f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 18f, 10f, 90, 2000f, "AncestralSplit"));
                    ACMUtils.AddScreenShake(12f);
                    AncestralDragonSky.TriggerFlash(0.9f);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                int particles = 8;
                for (int i = 0; i < particles; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = NPC.Center + ang.ToRotationVector2() * Main.rand.NextFloat(180f, 280f);
                    int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan, 0, 0, 100, Color.White, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if (StateTimer == 120 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnTwinDragon();
            }

            if (StateTimer > 180) {
                NPC.dontTakeDamage = false;
                // 脚本化开场: 主/副同入 双链(intro=1) → 交叉火力
                TransitionTo(AIState.TwinLink);
                StateData = 1f;
                NPC.netUpdate = true;
                CommandTwin(AIState.TwinLink, 1f);
            }
        }

        private void SpawnTwinDragon() {
            int halfLife = Math.Max(1, NPC.life / 2);
            NPC.life = halfLife;
            NPC.lifeMax = halfLife;
            NPC.netUpdate = true;

            Vector2 twinPos = NPC.Center + new Vector2(-400f, -200f);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)twinPos.X, (int)twinPos.Y, Type, NPC.whoAmI + 1);
            if (idx < Main.maxNPCs) {
                NPC twin = Main.npc[idx];
                if (twin.ModNPC is AncestralDragonSoulHead twinHead) {
                    twinHead.IsTwin = true;
                    twinHead.StateData = NPC.whoAmI;
                    twinHead.didSplit = true;
                    twinHead.partnerIndex = NPC.whoAmI;
                    twin.life = twin.lifeMax = halfLife;
                    twin.target = NPC.target;
                    partnerIndex = idx;
                    twin.netUpdate = true;
                    NetMessage.SendData(MessageID.SyncNPC, number: idx);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 80; i++) {
                    float ang = MathHelper.TwoPi * i / 80f;
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud, vel.X, vel.Y, 100, Color.White, 3f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        private void UpdateTwinLink() {
            if (!IsTwin) {
                Vector2 myPos = Target.Center + new Vector2(-500f, -240f + MathF.Sin(globalTime) * 80f);
                FlyToward(myPos, 0.5f, 15f);

                if (StateTimer == 30 && partnerIndex >= 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<SoulTetherChain>(), NPC.damage / 3, 0f, -1, NPC.whoAmI, partnerIndex);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                }
            }
            else {
                Vector2 myPos = Target.Center + new Vector2(500f, -240f + MathF.Sin(globalTime + MathHelper.Pi) * 80f);
                FlyToward(myPos, 0.5f, 15f);
            }

            if (StateTimer > 300) {
                if (StateData == 1f) {
                    // 脚本化开场链 → 交叉火力
                    if (!IsTwin) {
                        TransitionTo(AIState.TwinCrossfire);
                        CommandTwin(AIState.TwinCrossfire, 0f);
                    }
                    else {
                        TransitionTo(AIState.TwinCrossfire);
                    }
                }
                else {
                    BeginRecovery();
                }
            }
        }

        private void UpdateTwinCrossfire() {
            float sideSign = IsTwin ? 1f : -1f;
            Vector2 myPos = Target.Center + new Vector2(sideSign * 620f, MathF.Sin(globalTime * 1.5f + (IsTwin ? MathHelper.Pi : 0)) * 160f - 100f);
            FlyToward(myPos, 0.55f, 15f);

            int phaseOffset = IsTwin ? 15 : 0;
            if ((StateTimer + phaseOffset) % 30 == 0 && StateTimer > 40 && StateTimer < 260 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * 0.14f) * 7.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.3f);
                }
            }

            if (StateTimer > 300) BeginRecovery();
        }

        private void UpdateTwinPressure() {
            float dir = IsTwin ? -1f : 1f;
            StateData += 0.07f * dir;
            float radius = MathHelper.Lerp(520f, 200f, MathHelper.Clamp(StateTimer / 180f, 0f, 1f));
            Vector2 orbit = Target.Center + StateData.ToRotationVector2() * radius;
            FlyToward(orbit, 0.9f, 24f);

            if (!IsTwin && StateTimer == 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = -2; i <= 2; i++) {
                    Vector2 pos = Target.Center + new Vector2(i * 180f, 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulSigil>(), NPC.damage / 2, 0f);
                }
            }

            if (StateTimer > 220) BeginRecovery();
        }

        #endregion

        #region 狂暴终曲: 双魂回拢 + 刀魂碎片场 + 阴阳超载 + 编排终极

        /// <summary>
        /// 双魂回拢过场 (~6s i-frame 镜头拍)。双子向竞技场中心对冲, 灵链处决扫线 (PrimordialRecallBeam X 形),
        /// 合体闪光 → 太初真身。**i-frame 仅限过场拍 (避免被秒过场); 真正的改变是后续终曲循环, 而非加速。**
        /// 副本龙不存在则单龙直接进入终曲 (不合体)。
        /// </summary>
        private void UpdateEnrageTransition() {
            NPC.dontTakeDamage = true;

            Vector2 arenaCenter = Target.Center + new Vector2(0, -200f);

            if (!IsTwin) {
                // 主龙: 编排回拢
                FlyToward(arenaCenter + new Vector2(-160f, 0f), 0.5f, 14f);

                if (StateTimer == 30 && GetTwin() != null && Main.netMode != NetmodeID.MultiplayerClient) {
                    // X 形处决扫线
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), arenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<PrimordialRecallBeam>(), NPC.damage / 2, 0f, -1, 0f, 1f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), arenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<PrimordialRecallBeam>(), NPC.damage / 2, 0f, -1, MathHelper.PiOver2, -1f);
                }

                if (Main.netMode != NetmodeID.Server && StateTimer % 4 == 0) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = arenaCenter + a.ToRotationVector2() * Main.rand.NextFloat(300f, 480f);
                    int dust = Dust.NewDust(pos, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Clentaminator_Cyan, 0, 0, 100, Color.White, 2.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (arenaCenter - pos).SafeNormalize(Vector2.Zero) * 7f;
                }

                // 合体帧
                if (StateTimer == 210) {
                    MergeDragons();
                    transientBloom = 1f;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.3f }, NPC.Center);
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 22f, 12f, 60, 2000f, "AncestralMerge"));
                        ACMUtils.AddScreenShake(16f);
                        AncestralDragonSky.TriggerFlash(1.2f);
                        for (int i = 0; i < 90; i++) {
                            float ang = MathHelper.TwoPi * i / 90f;
                            Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(10f, 22f);
                            int dust = Dust.NewDust(NPC.Center, 0, 0, Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud, vel.X, vel.Y, 80, Color.White, 3.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                }

                if (StateTimer > 210)
                    FlyToward(arenaCenter, 0.4f, 10f);

                if (StateTimer > 360) {
                    NPC.dontTakeDamage = false;
                    enrageStep = 0;
                    TransitionTo(EnrageStateFor(0));
                }
            }
            else {
                // 副本龙: 向中心回拢 (合体帧由主龙移除)
                FlyToward(arenaCenter + new Vector2(160f, 0f), 0.5f, 14f);
            }
        }

        /// <summary>(服务器/SP) 双子合体: 合计血量为单一血池, 移除副本龙, 标记 merged (视觉放大)。</summary>
        private void MergeDragons() {
            NPC t = GetTwin();
            if (t == null) return;

            NPC.lifeMax += t.lifeMax;
            NPC.life += t.life;
            if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;
            merged = true;

            if (t.ModNPC is AncestralDragonSoulHead th) th.partnerIndex = -1;
            t.life = 0;
            t.active = false; // 段位经 FatherNPC 失活级联消失; 直接 active=false 不触发 OnKill/掉落/旗标

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: partnerIndex);

            partnerIndex = -1;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 刀魂碎片场 (谜题窗口): 8 颗碎片环绕真身, 每颗须被击中一次方可消解; 全部消解前不吃伤。
        /// 解谜成功 → 受创窗口 (玩家输出); 超时 → 强制引爆成弹幕并开放窗口。
        /// </summary>
        private void UpdateEnrageDaoField() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime * 0.8f) * 140f, -300f);
            FlyToward(hover, 0.35f, 8f);

            if (StateData == 0f) {
                NPC.dontTakeDamage = true;

                if (StateTimer == 1) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) SpawnDaoField();
                    if (Main.netMode != NetmodeID.Server)
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                }

                // 解谜/超时判定 (服务器/SP 权威, netUpdate 同步)
                if (Main.netMode != NetmodeID.MultiplayerClient && StateTimer > 20) {
                    int remaining = CountDaoFragments();
                    if (remaining == 0) {
                        StateData = 1f; SubTimer = 0; transientBloom = 1f; NPC.netUpdate = true;
                    }
                    else if (StateTimer >= 720) {
                        DetonateDaoField();
                        StateData = 1f; SubTimer = 0; NPC.netUpdate = true;
                    }
                }
            }
            else {
                // 受创窗口: 无弹幕, 强泛光提示"现在输出"
                NPC.dontTakeDamage = false;
                if (SubTimer == 1) {
                    transientBloom = 1f;
                    if (Main.netMode != NetmodeID.Server) {
                        AncestralDragonSky.TriggerFlash(0.7f);
                        ACMUtils.AddScreenShake(6f);
                    }
                }
                if (SubTimer > 240) BeginRecovery();
            }
        }

        private void SpawnDaoField() {
            const int n = 8;
            for (int i = 0; i < n; i++) {
                float ang = MathHelper.TwoPi * i / n;
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * 300f;
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                    ModContent.NPCType<AncestralDaoFragment>(), 0, NPC.whoAmI, ang, 300f);
            }
        }

        private int CountDaoFragments() {
            int type = ModContent.NPCType<AncestralDaoFragment>();
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) count++;
            }
            return count;
        }

        private void DetonateDaoField() {
            int type = ModContent.NPCType<AncestralDaoFragment>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.type != type || (int)n.ai[0] != NPC.whoAmI) continue;
                // 超时惩罚: 引爆成可读雾弹环
                for (int j = 0; j < 8; j++) {
                    float a = MathHelper.TwoPi * j / 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), n.Center, a.ToRotationVector2() * 7f,
                        ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                }
                n.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
            }
        }

        /// <summary>阴阳超载 (强制机制): 单控制弹张开阴阳环, 蓄满后全屏魂蚀脉冲; 不在安全缝者被抽走百分比血量。</summary>
        private void UpdateEnrageYinYang() {
            NPC.dontTakeDamage = false;
            Vector2 hover = Target.Center + new Vector2(0, -360f);
            FlyToward(hover, 0.4f, 10f);

            const int windup = 70;
            if (StateTimer == windup && Main.netMode != NetmodeID.MultiplayerClient) {
                float gapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), Target.Center, Vector2.Zero,
                    ModContent.ProjectileType<YinYangOverdrivePulse>(), NPC.damage / 3, 0f, -1, gapAngle);
            }
            if (StateTimer == windup - 1 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0f, Volume = 0.9f }, NPC.Center);

            if (StateTimer > windup + 215) BeginRecovery();
        }

        /// <summary>编排终极: 螺旋俯冲 → 必接掠袭冲锋 (固定序列, 非 RNG)。</summary>
        private void UpdateEnrageUltimate() {
            NPC.dontTakeDamage = false;

            if (StateData == 0f) {
                // —— 子拍0: 螺旋俯冲 ——
                float t = SubTimer / 160f;
                float radius = MathHelper.Lerp(620f, 150f, t);
                float angularSpeed = MathHelper.Lerp(0.045f, 0.13f, t);
                NPC.localAI[0] += angularSpeed;
                Vector2 orbit = Target.Center + NPC.localAI[0].ToRotationVector2() * radius;
                FlyToward(orbit, 1.2f, 28f);

                if (SubTimer % 9 == 0 && SubTimer > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 5.5f,
                            ModContent.ProjectileType<SpiralSoulFragment>(), NPC.damage / 3, 0.5f);
                    }
                }

                if (SubTimer > 160) { StateData = 1f; SubTimer = 0; NPC.netUpdate = true; }
            }
            else {
                // —— 子拍1: 必接掠袭冲锋 (2 次) ——
                const int charges = 2;
                const int windUp = 42;
                const int chargeDur = 46;
                int cycleLen = windUp + chargeDur;
                int idx = (int)(SubTimer / cycleLen);
                int localT = (int)SubTimer % cycleLen;

                if (idx >= charges) { BeginRecovery(); return; }

                if (localT < windUp) {
                    float side = (idx % 2 == 0) ? 1f : -1f;
                    Vector2 windPos = Target.Center + new Vector2(side * 820f, Main.rand.NextFloat(-70f, 70f));
                    FlyToward(windPos, 1f, 20f);
                    // 红线预警 (致命冲刺路径)
                    showChargeTele = true;
                    chargeTeleTarget = Target.Center;
                    if (localT == windUp - 1) {
                        Vector2 dir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.velocity = dir * 40f;
                        NPC.netUpdate = true;
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.1f }, NPC.Center);
                            ACMUtils.AddScreenShake(8f);
                        }
                    }
                }
                else {
                    NPC.velocity *= 0.995f;
                    NPC.rotation = NPC.velocity.ToRotation();
                    NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
                    if (localT == windUp + 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                        for (int i = -1; i <= 1; i += 2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 6f,
                                ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                        }
                    }
                }
            }
        }

        #endregion

        #region 表现层

        /// <summary>每帧发布太初屏幕演出标量 (仅主龙, 纯本地视觉) 并平滑 GenericWarp 强度。</summary>
        private void PublishScreenFx() {
            if (Main.dedServ || IsTwin) return;

            float tint = 0f, runic = 0f;
            switch (State) {
                case AIState.EnrageTransition: tint = 0.9f; break;
                case AIState.EnrageDaoField:
                    tint = 0.7f;
                    runic = StateData == 0f ? 1f : 0.25f;
                    break;
                case AIState.EnrageYinYang: tint = 0.6f; break;
                case AIState.EnrageUltimate: tint = 0.6f; break;
                case AIState.Recovery: tint = didEnrage ? 0.5f : 0f; break;
                default: tint = didSplit ? 0.2f : 0f; break;
            }

            float warpTarget = 0f;
            if (State == AIState.EnrageTransition)
                warpTarget = 0.3f + MathHelper.Clamp(StateTimer / 210f, 0f, 1f) * 0.35f;
            else if (didEnrage)
                warpTarget = 0.15f;
            warpIntensity = MathHelper.Lerp(warpIntensity, warpTarget, 0.06f);

            AncestralSoulScreenSystem.Publish(NPC.Center, tint, runic, transientBloom, globalTime);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 终极掠袭蓄力红线预警 (致命冲刺路径, 唯一红)
            if (showChargeTele && !Main.dedServ) {
                ACMShaders.DrawBeam(NPC.Center, chargeTeleTarget, 6f,
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, 0.55f);
            }

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float soulPulse = (1f + MathF.Sin(soulPulsePhase) * 0.1f) * MergeScaleMul();

            DrawMysticalGlow(spriteBatch, screenPos, tex, origin, soulPulse);
            DrawEtherealTrail(spriteBatch, screenPos, tex, origin);

            Color mistColor;
            if (IsTwin) {
                mistColor = Color.Lerp(drawColor, new Color(200, 230, 255), 0.6f);
                mistColor = Color.Lerp(mistColor, new Color(220, 235, 255), 0.4f);
            }
            else {
                mistColor = Color.Lerp(drawColor, new Color(240, 248, 255), 0.5f);
                mistColor = Color.Lerp(mistColor, Color.White, 0.35f);
            }

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * soulPulse, effects, 0f);

            Color innerGlow = new Color(255, 255, 255) * 0.35f * soulPulse;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.85f * MergeScaleMul(), effects, 0f);

            DrawDragonEyes(spriteBatch, screenPos);
            return false;
        }

        /// <summary>太初雾全屏扭曲 (GenericWarp · fog 主题), 走单一全屏后处理名额; 强度&lt;0.01 早退。</summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || IsTwin || warpIntensity <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(warpIntensity, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.95f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.1f);
            fx.Parameters["uChroma"]?.SetValue(0.35f);
            fx.Parameters["uRadialPull"]?.SetValue(0.2f);
            fx.Parameters["uMode"]?.SetValue(2f); // fog
            fx.Parameters["uTint"]?.SetValue(new Vector4(new Color(196, 214, 232).ToVector3(), 0.45f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 25f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.8f + MathF.Sin(globalTime * 4f) * 0.2f;
            Color eyeColor = (didEnrage ? new Color(255, 200, 220) : new Color(255, 255, 255)) * eyePulse * 0.6f;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.6f * eyePulse, SpriteEffects.None, 0f);
        }

        #endregion

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<ArchosaurFerrara>(),
                ModContent.ItemType<ArchosaurBow>(),
                ModContent.ItemType<ArchosaurStaff>()
            ));
        }

        public override void BossLoot(ref string name, ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override bool CheckDead() {
            if (IsTwin) {
                NPC.active = false;
                return false;
            }
            return true;
        }

        public override void OnKill() {
            base.OnKill();

            if (!IsTwin && partnerIndex >= 0 && partnerIndex < Main.maxNPCs) {
                NPC twin = Main.npc[partnerIndex];
                if (twin.active && twin.type == Type) {
                    twin.life = 0;
                    twin.checkDead();
                    twin.active = false;
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, number: partnerIndex);
                    }
                }
            }

            for (int i = 0; i < 100; i++) {
                float angle = MathHelper.TwoPi * i / 100;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5, 15);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.5f }, NPC.Center);
        }
    }
}

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
    /// 祖龙残魂头部,全新AI状态机:
    /// 阶段1(>70%): 基础机动+多种弹幕
    /// 阶段2(50%~70%): 分裂预告,追加高压招式
    /// 半血触发一次性分裂:主龙保留血量,额外召唤一条"双子龙"共同作战
    /// 阶段3(<40%): 双龙协同,出现灵链封锁
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
            Enraged
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
        private int partnerIndex = -1;
        private int attackSelectCooldown;
        private float aggressionBuildup;

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
            writer.Write(IsTwin);
            writer.Write(partnerIndex);
            writer.Write(aggressionBuildup);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            didSplit = reader.ReadBoolean();
            didEnrage = reader.ReadBoolean();
            IsTwin = reader.ReadBoolean();
            partnerIndex = reader.ReadInt32();
            aggressionBuildup = reader.ReadSingle();
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

            // 半血分裂:只对主龙触发一次
            if (!IsTwin && !didSplit && NPC.life < NPC.lifeMax * 0.5f && State != AIState.Intro) {
                TransitionTo(AIState.SplitTransition);
                didSplit = true;
            }

            // 低血狂暴
            if (!didEnrage && NPC.life < NPC.lifeMax * 0.25f && State != AIState.SplitTransition && State != AIState.Intro) {
                didEnrage = true;
                aggressionBuildup = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 14f, 8f, 40, 2000f, "AncestralDragonEnrage"));
                }
            }

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
                case AIState.Enraged: UpdateEnraged(); break;
            }

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

        #region 机动辅助

        /// <summary>朝目标位置缓动飞行,带速度上限</summary>
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

        /// <summary>Lissajous 8字轨道机动</summary>
        private Vector2 LissajousPoint(float phase, float rx, float ry) {
            return new Vector2(rx * MathF.Cos(phase), ry * MathF.Sin(phase * 2f));
        }

        #endregion

        #region 状态实现

        private void UpdateIntro() {
            // 上升聚气
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
                }
            }
        }

        /// <summary>巡航态,选择下一个招式</summary>
        private void UpdatePatrol() {
            // 在玩家上方做Lissajous机动
            float phase = globalTime * 0.9f;
            Vector2 orbit = LissajousPoint(phase, 520f, 260f);
            Vector2 anchor = Target.Center + orbit + new Vector2(0, -60f);
            FlyToward(anchor, 0.55f, 16f + aggressionBuildup * 4f);

            if (StateTimer > 90 && attackSelectCooldown <= 0) {
                PickNextAttack();
            }
            else if (attackSelectCooldown > 0) {
                attackSelectCooldown--;
            }
        }

        private void PickNextAttack() {
            // 根据阶段挑选技能,降低重复概率
            bool inTwinPhase = didSplit;
            AIState[] pool;
            if (inTwinPhase) {
                pool = new[] {
                    AIState.ScaleBarrage, AIState.SigilEruption, AIState.YinYangBind,
                    AIState.DragonBeam, AIState.SpiralDive, AIState.RakingCharge,
                    AIState.TwinLink, AIState.TwinCrossfire, AIState.TwinPressure
                };
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
            StateData = (int)pick;
            attackSelectCooldown = 60;
            TransitionTo(pick);
        }

        /// <summary>龙鳞弹幕:高速扫过玩家时抛出6颗延时龙鳞</summary>
        private void UpdateScaleBarrage() {
            float phase = StateTimer / 180f;
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

            if (StateTimer > 200) TransitionTo(AIState.Patrol);
        }

        /// <summary>符文爆发:在玩家周围地面布下4~6个符文,延时爆发能量柱</summary>
        private void UpdateSigilEruption() {
            Vector2 hover = Target.Center + new Vector2(MathF.Sin(globalTime * 1.2f) * 300f, -360f);
            FlyToward(hover, 0.5f, 14f);

            int casts = didEnrage ? 6 : 4;
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

            if (StateTimer > 40 + casts * castInterval + 160) TransitionTo(AIState.Patrol);
        }

        /// <summary>阴阳双珠:发射3对绑定灵珠,延时分离追踪</summary>
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

            if (StateTimer > 260) TransitionTo(AIState.Patrol);
        }

        /// <summary>龙吐息激光:缓慢追踪大型激光</summary>
        private void UpdateDragonBeam() {
            Vector2 hover = Target.Center + new Vector2(Target.Center.X > NPC.Center.X ? -600f : 600f, -200f);
            FlyToward(hover, 0.45f, 11f);

            if (StateTimer == 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<AncestralDragonBeam>(), NPC.damage / 2, 0f, -1, NPC.whoAmI,
                    (Target.Center - NPC.Center).ToRotation());
                SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
            }

            if (StateTimer > 200) TransitionTo(AIState.Patrol);
        }

        /// <summary>螺旋俯冲:龙头绕玩家螺旋俯冲,期间洒落螺旋碎片</summary>
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

            if (StateTimer > 200) TransitionTo(AIState.Patrol);
        }

        /// <summary>掠袭冲锋:蓄力+高速横扫,龙体变成鞭刀</summary>
        private void UpdateRakingCharge() {
            const int charges = 3;
            const int windUp = 40;
            const int chargeDur = 45;
            int cycleLen = windUp + chargeDur;
            int currentCharge = (int)(StateTimer / cycleLen);
            int localT = (int)StateTimer % cycleLen;

            if (currentCharge >= charges) {
                TransitionTo(AIState.Patrol);
                return;
            }

            if (localT < windUp) {
                // 蓄力:侧向绕到玩家外围
                float side = (currentCharge % 2 == 0) ? 1f : -1f;
                Vector2 windPos = Target.Center + new Vector2(side * 780f, Main.rand.NextFloat(-80f, 80f));
                FlyToward(windPos, 1f, 20f);
                if (localT == windUp - 1) {
                    Vector2 dir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = dir * 38f;
                    NPC.netUpdate = true;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = 0.2f }, NPC.Center);
                    }
                }
            }
            else {
                // 冲锋:保持高速
                NPC.velocity *= 0.995f;
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;

                // 冲锋途中撒2颗雾弹
                if (localT == windUp + 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = NPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * i * 6f,
                            ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.5f);
                    }
                }
            }
        }

        /// <summary>半血分裂过渡</summary>
        private void UpdateSplitTransition() {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (StateTimer == 1 && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.2f }, NPC.Center);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, Vector2.UnitY, 18f, 10f, 90, 2000f, "AncestralSplit"));
            }

            // 聚能粒子
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

            // 120帧后触发分裂
            if (StateTimer == 120 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnTwinDragon();
            }

            if (StateTimer > 180) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.TwinLink);
            }
        }

        private void SpawnTwinDragon() {
            // 血量减半,分给双子龙
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

        /// <summary>双龙灵链:主/副龙之间产生致命连接链</summary>
        private void UpdateTwinLink() {
            // 主龙指挥,副龙跟随;保持横向对位
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

            if (StateTimer > 360) {
                TransitionTo(AIState.Patrol);
            }
        }

        /// <summary>双龙交叉火力:两龙从对角发射弹幕</summary>
        private void UpdateTwinCrossfire() {
            float sideSign = IsTwin ? 1f : -1f;
            Vector2 myPos = Target.Center + new Vector2(sideSign * 620f, MathF.Sin(globalTime * 1.5f + (IsTwin ? MathHelper.Pi : 0)) * 160f - 100f);
            FlyToward(myPos, 0.55f, 15f);

            // 错位发射
            int phaseOffset = IsTwin ? 15 : 0;
            if ((StateTimer + phaseOffset) % 30 == 0 && StateTimer > 40 && StateTimer < 260 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * 0.14f) * 7.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<AncestralMistBolt>(), NPC.damage / 3, 0.3f);
                }
            }

            if (StateTimer > 300) TransitionTo(AIState.Patrol);
        }

        /// <summary>双龙压迫:两龙反向螺旋俯冲,同时符文爆发封锁</summary>
        private void UpdateTwinPressure() {
            float dir = IsTwin ? -1f : 1f;
            StateData += 0.07f * dir;
            float radius = MathHelper.Lerp(520f, 200f, MathHelper.Clamp(StateTimer / 180f, 0f, 1f));
            Vector2 orbit = Target.Center + StateData.ToRotationVector2() * radius;
            FlyToward(orbit, 0.9f, 24f);

            // 主龙负责布符文
            if (!IsTwin && StateTimer == 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = -2; i <= 2; i++) {
                    Vector2 pos = Target.Center + new Vector2(i * 180f, 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulSigil>(), NPC.damage / 2, 0f);
                }
            }

            if (StateTimer > 220) TransitionTo(AIState.Patrol);
        }

        /// <summary>狂暴态:提升所有速度与频率,并触发一次大招</summary>
        private void UpdateEnraged() {
            aggressionBuildup = MathHelper.Lerp(aggressionBuildup, 1f, 0.05f);
            TransitionTo(AIState.Patrol);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float soulPulse = 1f + MathF.Sin(soulPulsePhase) * 0.1f;

            DrawMysticalGlow(spriteBatch, screenPos, tex, origin, soulPulse);
            DrawEtherealTrail(spriteBatch, screenPos, tex, origin);

            // 副本龙偏青色,本体偏白色
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
                NPC.rotation, origin, NPC.scale * 0.85f, effects, 0f);

            DrawDragonEyes(spriteBatch, screenPos);
            return false;
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
            // 副本龙死亡不算Boss战结束,仅消失
            if (IsTwin) {
                NPC.active = false;
                return false;
            }
            return true;
        }

        public override void OnKill() {
            base.OnKill();

            // 若主龙死亡,清理副本龙
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

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
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    /// <summary>
    /// 朱雀 Suzaku —— 南方·火·涅槃凤凰（四圣兽 V2）。
    ///
    /// V2 重做要点（见 docs/BOSS_REDO_V2/03_CELESTIAS_OTHERS_V2 §4.5）：
    ///  ● 继承 <see cref="SacredBeastBase"/>：确定性轮替（<see cref="GetPhaseRotation"/>）替代 V1 随机 hub，
    ///    杀掉 NirvanaFlight 纯加速巡逻与 VermillionRain 每 2~4 帧喷弹。
    ///  ● 占位弹清除：V1 的 <c>ProjectileID.InfernoFriendlyBlast</c> 全部换成自定义焰弹
    ///    （<see cref="SuzakuEmber"/> 快窄 / <see cref="SuzakuFeather"/> 慢宽 / <see cref="SuzakuSunPillar"/> 火柱 /
    ///    <see cref="SuzakuSolarBeam"/> 审判光束）。
    ///  ● 签名 set-piece「涅槃重生」：<see cref="CheckDead"/> 首次坠亡 → 清场 → 灰烬沉默(PaletteLUT 灰)
    ///    → 爆燃复生(PaletteLUT 赤 + RadialBloom + 竞技场点燃) → 进入"涅槃形态"凤凰循环（PhoenixDance + SolarJudgment）直至真正死亡。
    ///  ● 表现层全部走硬化 <see cref="ACMShaders"/>：DrawBeam 光束、DrawRadialBloomAt 太阳泛光、
    ///    ArenaRunic 太阳法阵地纹、ElementalScreenTint 火幕、PaletteLUT 涅槃灰↔赤。
    /// </summary>
    [AutoloadBossHead]
    public class Suzaku : SacredBeastBase
    {
        #region 五行身份 / 阈值

        public override SacredElement Element => SacredElement.Fire;
        public override string SkyName => SuzakuSky.SkyName;

        // 供天幕等无实例引用的血量阈值常量（基类虚属性据此返回）。
        public const float HpPhase2 = 0.60f;
        public const float HpPhase3 = 0.30f;

        public override float Phase2Threshold => HpPhase2;
        public override float Phase3Threshold => HpPhase3;

        #endregion

        #region 状态枚举（写入 RawState=ai[0]）

        public enum St
        {
            Intro,
            Hub,                 // 确定性轮替枢纽（按当前档位选下一招）
            P1_FeatherFan,
            P1_EmberBarrage,
            P1_SunPillars,
            Trans2,
            P2_PhoenixDive,
            P2_SolarBeams,
            P2_FeatherStorm,
            P2_SunPillars,
            Trans3,
            P3_SolarJudgment,
            P3_PhoenixDance,
            P3_SunPillarChess,
            Rebirth              // 涅槃重生签名
        }

        private St State => (St)RawState;
        private void Goto(St s) => TransitionToState((int)s);

        #endregion

        #region 持久字段（SendExtraAI 同步）

        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool didRebirth;
        private bool nirvanaForm;     // 涅槃形态（重生后）
        private int diveCount;
        private Vector2 diveTarget;   // 锁定俯冲落点（固定，非逐帧追踪）
        private float glowIntensity = 1f;

        #endregion

        #region 本地视觉（不需同步）

        private int frameCounter;
        private float fxBloom;        // RadialBloom 瞬态
        private float fxRunic;        // ArenaRunic 法阵
        private float rebirthLut;     // PaletteLUT 强度
        private float rebirthSat = 1f;
        private Vector4 rebirthShadow;
        private Vector4 rebirthHi;

        private const int DiveWindup = 40;     // 固定 40 帧地面影子预警（§6.3）
        private const int AshEnd = 80;          // 涅槃·灰烬沉默结束
        private const int RebirthEnd = 160;     // 涅槃·复生结束

        #endregion

        #region SetDefaults / 静态

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 4;
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
            NPC.lifeMax = 2000000;
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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SuzakuSpirit>(), 1, 6, 10));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<StarfireAnnihilator>(),
                ModContent.ItemType<SolarisEternalVerdict>(),
                ModContent.ItemType<PhoenixFlameStaff>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            Goto(St.Intro);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            SendSacredBeastAI(writer);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didRebirth);
            writer.Write(nirvanaForm);
            writer.Write(diveCount);
            writer.WriteVector2(diveTarget);
            writer.Write(glowIntensity);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ReceiveSacredBeastAI(reader);
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didRebirth = reader.ReadBoolean();
            nirvanaForm = reader.ReadBoolean();
            diveCount = reader.ReadInt32();
            diveTarget = reader.ReadVector2();
            glowIntensity = reader.ReadSingle();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        #endregion

        #region 涅槃重生机制（保留 CheckDead）

        public override bool CheckDead() {
            // 首次坠亡 → 涅槃重生（V1 认可的核心概念，V2 升格为签名 set-piece）
            if (!didRebirth) {
                didRebirth = true;
                nirvanaForm = true;
                NPC.life = (int)(NPC.lifeMax * 0.22f);
                NPC.dontTakeDamage = true;

                // 清场：抹去所有敌意弹幕（"重生时刻"的留白）
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.hostile && p.damage > 0) p.Kill();
                    }
                }

                ResetRotation(3);
                diveCount = 0;
                Goto(St.Rebirth);
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        #endregion

        #region OnKill / HitEffect

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, hit.HitDirection * 2f, -1f, 100, default, 2f);
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

        #endregion

        #region AI 主循环

        public override void AI() {
            fxBloom *= 0.9f;
            fxRunic = MathHelper.Lerp(fxRunic, 0f, 0.05f);

            if (!RunStandardPrologue(out Player target))
                return;

            if (State != St.Intro && State != St.Trans2 && State != St.Trans3 && State != St.Rebirth)
                CheckPhaseTransition();

            switch (State) {
                case St.Intro: RunIntro(target); break;
                case St.Hub: RunHub(target); break;
                case St.P1_FeatherFan: RunFeatherFan(target); break;
                case St.P1_EmberBarrage: RunEmberBarrage(target); break;
                case St.P1_SunPillars: RunSunPillars(target, 4, 130); break;
                case St.Trans2: RunTransition2(target); break;
                case St.P2_PhoenixDive: RunDiveAttack(target, 3); break;
                case St.P2_SolarBeams: RunSolarBeams(target, 3); break;
                case St.P2_FeatherStorm: RunFeatherStorm(target); break;
                case St.P2_SunPillars: RunSunPillars(target, 6, 150); break;
                case St.Trans3: RunTransition3(target); break;
                case St.P3_SolarJudgment: RunSolarJudgment(target); break;
                case St.P3_PhoenixDance: RunDiveAttack(target, nirvanaForm ? 5 : 4); break;
                case St.P3_SunPillarChess: RunSunPillarChess(target); break;
                case St.Rebirth: RunRebirth(target); break;
            }

            UpdateRebirthGrade();

            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.015f;

            float fireMul = nirvanaForm ? 2.4f : IsPhase3 ? 2f : IsPhase2 ? 1.5f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.4f, 0.1f) * glowIntensity * fireMul);

            if (!Main.dedServ && State != St.Intro) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, -2f, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity += -NPC.velocity * 0.05f;
                }
            }

            // 发布屏幕氛围标量（赤焰火幕 + 瞬态泛光/法阵）
            float tint = (nirvanaForm || IsPhase3) ? 0.62f : IsPhase2 ? 0.5f : 0.38f;
            if (State == St.Intro || State == St.Rebirth) tint *= 0.6f;
            SuzakuScreenSystem.Publish(NPC.Center, tint, MathHelper.Clamp(fxBloom, 0f, 1f), MathHelper.Clamp(fxRunic, 0f, 1f), GlobalTime);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                Goto(St.Trans2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                Goto(St.Trans3);
            }
        }

        // —— 确定性轮替表 ——
        protected override int[] GetPhaseRotation(int phaseTier) {
            if (nirvanaForm)
                return [(int)St.P3_PhoenixDance, (int)St.P3_SolarJudgment, (int)St.P3_SunPillarChess];
            return phaseTier switch {
                1 => [(int)St.P1_FeatherFan, (int)St.P1_EmberBarrage, (int)St.P1_SunPillars],
                2 => [(int)St.P2_PhoenixDive, (int)St.P2_SolarBeams, (int)St.P2_FeatherStorm, (int)St.P2_SunPillars],
                _ => [(int)St.P3_SolarJudgment, (int)St.P3_PhoenixDance, (int)St.P3_SunPillarChess],
            };
        }

        #endregion

        #region 弹幕助手

        private int Fire(Vector2 pos, Vector2 vel, int type, int dmg, float ai0 = 0f, float ai1 = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            return Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel, type, dmg, 0f, Main.myPlayer, ai0, ai1);
        }

        private int EmberType => ModContent.ProjectileType<SuzakuEmber>();
        private int FeatherType => ModContent.ProjectileType<SuzakuFeather>();
        private int PillarType => ModContent.ProjectileType<SuzakuSunPillar>();
        private int BeamType => ModContent.ProjectileType<SuzakuSolarBeam>();

        private int EmberDmg => NPC.damage / 5;
        private int FeatherDmg => NPC.damage / 4;
        private int PillarDmg => NPC.damage / 3;
        private int BeamDmg => NPC.damage / 3;

        /// <summary>在玩家脚下一带生成一根自预警火柱（落点 = 玩家所在水平 + 偏移）。</summary>
        private void SpawnPillarAt(Player target, float xOffset) {
            Vector2 ground = new(target.Center.X + xOffset, target.Center.Y + 230f);
            // SunPillar 以 Bottom 锚地：Center 上移半高
            Vector2 center = ground + new Vector2(0, -280f);
            Fire(center, Vector2.Zero, PillarType, PillarDmg);
        }

        #endregion

        #region 入场

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -800);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, target.Center);
            }

            Vector2 targetPos = target.Center + new Vector2(0, -350);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.02f);

            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.SolarFlare, 0, -3f, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity += (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 1.5f }, NPC.Center);
                ShakeScreen(12f, 8f, 35);
                fxBloom = 0.7f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 30; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 3f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(15, 15);
                    }
                }
                ResetRotation(1);
                Goto(St.Hub);
            }
        }

        #endregion

        #region 轮替枢纽

        private void RunHub(Player target) {
            int tier = nirvanaForm ? 3 : PhaseTier;
            int window = nirvanaForm ? 38 : tier == 1 ? 70 : tier == 2 ? 58 : 52;

            // 凤翔环绕（无伤位移，危险全部交给被轮替到的招式）
            float t = GlobalTime;
            float xR = nirvanaForm ? 300f : 360f;
            Vector2 soar = target.Center + new Vector2(MathF.Sin(t * 1.6f) * xR, MathF.Sin(t * 1.1f) * 130f - 300f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (soar - NPC.Center) * 0.06f, 0.08f);

            if (PhaseTimer >= window) {
                int next = NextAttack(tier);
                if (next >= 0) {
                    diveCount = 0;
                    Goto((St)next);
                }
            }
        }

        #endregion

        #region 一阶段招式

        // 焰羽扇（慢宽弹，中等预告）
        private void RunFeatherFan(Player target) {
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GlobalTime * 2f) * 150f, -330);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.1f);

            if (AttackTimer < 35) {
                // 预告：聚焰
                if (!Main.dedServ && AttackTimer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 4f;
                }
                if (AttackTimer == 30) fxBloom = 0.4f;
            }
            else if (AttackTimer == 35 || AttackTimer == 50 || AttackTimer == 65) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, NPC.Center);
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 7 : 5;
                float spread = MathHelper.ToRadians(42f);
                for (int i = 0; i < count; i++) {
                    float a = -spread / 2 + spread / (count - 1) * i;
                    Fire(NPC.Center, dir.RotatedBy(a) * 7.5f, FeatherType, FeatherDmg);
                }
            }

            if (AttackTimer > 88) Goto(St.Hub);
        }

        // 余烬弹幕（快窄弹，小预告）
        private void RunEmberBarrage(Player target) {
            Vector2 hover = target.Center + new Vector2(MathF.Sin(GlobalTime * 2.5f) * 250f, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.1f);

            if (AttackTimer == 20) fxBloom = 0.3f;
            if (AttackTimer == 30) {
                // 同步两根火柱牵制（自预警）
                SpawnPillarAt(target, -260);
                SpawnPillarAt(target, 260);
            }

            if (AttackTimer > 20 && AttackTimer < 78 && AttackTimer % 6 == 0) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 3 : 2;
                for (int i = 0; i < count; i++) {
                    Fire(NPC.Center, dir.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * (16f + Main.rand.NextFloat(0, 3f)), EmberType, EmberDmg);
                }
                if (Main.rand.NextBool(3)) SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f }, NPC.Center);
            }

            if (AttackTimer > 90) Goto(St.Hub);
        }

        // 火柱阵（自预警地面太阳符）
        private void RunSunPillars(Player target, int count, int duration) {
            Vector2 hover = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.07f);
            fxRunic = MathHelper.Max(fxRunic, 0.4f);

            if (AttackTimer == 20) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.1f }, target.Center);
                float span = 520f;
                for (int i = 0; i < count; i++) {
                    float x = -span + (span * 2f) * i / (count - 1) + Main.rand.NextFloat(-40, 40);
                    SpawnPillarAt(target, x);
                }
            }
            // 余烬牵制
            if (AttackTimer > 30 && AttackTimer % 18 == 0) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                Fire(NPC.Center, dir * 14f, EmberType, EmberDmg);
            }

            if (AttackTimer > duration) Goto(St.Hub);
        }

        #endregion

        #region 阶段过渡

        private void RunTransition2(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            fxBloom = MathHelper.Max(fxBloom, PhaseTimer / 90f * 0.6f);

            if (!Main.dedServ) {
                for (int i = 0; i < 12; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = MathF.Max(40f, 400 - PhaseTimer * 3);
                    Vector2 dp = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dp, 0, 0, Main.rand.NextBool() ? DustID.SolarFlare : DustID.Torch, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 8f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 1.5f }, NPC.Center);
                ShakeScreen(12f, 10f, 45);
                fxBloom = 0.9f;
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                glowIntensity = 1.5f;
                ResetRotation(2);
                Goto(St.Hub);
            }
        }

        private void RunTransition3(Player target) {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);
            fxBloom = MathHelper.Max(fxBloom, PhaseTimer / 120f * 0.7f);

            if (!Main.dedServ) {
                for (int i = 0; i < 18; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 300);
                    Vector2 dp = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                ShakeScreen(12f, 15f, 60, 3000f);
                fxBloom = 1f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20 * i;
                        Fire(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f, FeatherType, FeatherDmg);
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 2.5f;
                ResetRotation(3);
                Goto(St.Hub);
            }
        }

        #endregion

        #region 凤凰俯冲（固定 40 帧地面影子预警）

        private void RunDiveAttack(Player target, int maxDives) {
            if (SubStateRaw == 0) {
                // —— 蓄力 / 锁定落点 ——
                if (AttackTimer == 1) {
                    diveTarget = target.Center;   // 固定落点（非逐帧追踪）
                }
                Vector2 apex = diveTarget + new Vector2(0, -520);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (apex - NPC.Center) * 0.08f, 0.12f);

                if (!Main.dedServ) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 3f;
                }

                if (AttackTimer >= DiveWindup) {
                    SubStateRaw = 1;
                    AttackTimer = 0;
                    NPC.velocity = (diveTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * 42f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f }, NPC.Center);
                    ShakeScreen(6f, 8f, 20);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        NPC.netUpdate = true;
                }
            }
            else {
                // —— 俯冲 ——
                if (!Main.dedServ) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 80, default, 3f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                    }
                }

                // 焰羽尾迹
                if (AttackTimer % 3 == 0) {
                    Vector2 perp = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Fire(NPC.Center + perp * 40f, perp * 6f, FeatherType, FeatherDmg);
                    Fire(NPC.Center - perp * 40f, -perp * 6f, FeatherType, FeatherDmg);
                }

                bool reached = NPC.Center.Y >= diveTarget.Y - 20f || AttackTimer > 28;
                if (AttackTimer > 18) NPC.velocity *= 0.9f;

                if (reached) {
                    fxBloom = 0.8f;
                    ShakeScreen(8f, 9f, 22);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, NPC.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int n = nirvanaForm ? 14 : 10;
                        for (int i = 0; i < n; i++) {
                            float angle = MathHelper.TwoPi / n * i;
                            Fire(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f, EmberType, EmberDmg);
                        }
                    }
                    diveCount++;
                    if (diveCount < maxDives) {
                        SubStateRaw = 0;
                        AttackTimer = 0;
                    }
                    else Goto(St.Hub);
                }
            }
        }

        #endregion

        #region 赤日审判光束（DrawBeam）

        // 二阶段：少量扇形扫掠光束
        private void RunSolarBeams(Player target, int beamCount) {
            Vector2 hover = target.Center + new Vector2(0, -360);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer == 25) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f }, NPC.Center);
                fxBloom = 0.5f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 baseDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    float spread = MathHelper.ToRadians(28f);
                    for (int i = 0; i < beamCount; i++) {
                        float a = beamCount == 1 ? 0 : -spread + (spread * 2f) * i / (beamCount - 1);
                        Vector2 dir = baseDir.RotatedBy(a);
                        float sweep = (i % 2 == 0 ? 1f : -1f) * 0.006f;
                        Fire(NPC.Center, dir, BeamType, BeamDmg, NPC.whoAmI, sweep);
                    }
                }
            }

            if (AttackTimer > 120) Goto(St.Hub);
        }

        // 三阶段签名：径向审判（处决级 75 帧预告）
        private void RunSolarJudgment(Player target) {
            if (SubStateRaw == 0) {
                NPC.velocity *= 0.9f;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);
                fxRunic = MathHelper.Max(fxRunic, AttackTimer / 75f * 0.8f);
                fxBloom = MathHelper.Max(fxBloom, AttackTimer / 75f * 0.6f);

                if (!Main.dedServ && AttackTimer % 5 == 0) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(160, 160);
                        Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }
                if (AttackTimer % 20 == 0 && AttackTimer > 0)
                    ShakeScreen(MathHelper.Clamp(4f + AttackTimer / 20f, 0f, 12f), 8f, 18);

                if (AttackTimer >= 75) {
                    SubStateRaw = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                    ShakeScreen(12f, 11f, 30, 2500f);
                    fxBloom = 1f;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int beams = nirvanaForm ? 8 : 6;
                        for (int i = 0; i < beams; i++) {
                            float a = MathHelper.TwoPi / beams * i;
                            float sweep = (i % 2 == 0 ? 1f : -1f) * 0.008f;
                            Fire(NPC.Center, a.ToRotationVector2(), BeamType, BeamDmg, NPC.whoAmI, sweep);
                        }
                        // 环绕落点火柱（审判落地）
                        for (int i = 0; i < 5; i++)
                            SpawnPillarAt(target, -480 + 240 * i);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                fxRunic = MathHelper.Max(fxRunic, 0.4f);
                if (AttackTimer > 105) Goto(St.Hub);
            }
        }

        #endregion

        #region 焰羽风暴 / 火柱棋局

        private void RunFeatherStorm(Player target) {
            NPC.velocity *= 0.93f;
            NPC.Center += Main.rand.NextVector2Circular(2, 2);

            if (AttackTimer == 25) fxBloom = 0.4f;

            if (AttackTimer > 30 && AttackTimer < 105 && AttackTimer % 14 == 0) {
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.6f }, NPC.Center);
                int ring = 10;
                float baseA = GlobalTime * 2f;
                for (int i = 0; i < ring; i++) {
                    float a = baseA + MathHelper.TwoPi / ring * i;
                    Fire(NPC.Center, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 6.5f, FeatherType, FeatherDmg);
                }
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++)
                    Fire(NPC.Center, dir.RotatedBy(i * MathHelper.ToRadians(12)) * 15f, EmberType, EmberDmg);
            }

            if (AttackTimer > 118) Goto(St.Hub);
        }

        // 火柱"棋局"：交错太阳符，留安全格
        private void RunSunPillarChess(Player target) {
            Vector2 hover = target.Center + new Vector2(0, -420);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.04f, 0.07f);
            fxRunic = MathHelper.Max(fxRunic, 0.7f);

            const float spacing = 200f;
            // 第一波：偶数格
            if (AttackTimer == 20) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.1f }, target.Center);
                for (int i = -3; i <= 3; i += 2)
                    SpawnPillarAt(target, i * spacing);
            }
            // 第二波：奇数格（错位 → 形成棋盘可走缝）
            if (AttackTimer == 20 + SuzakuSunPillar.WindupTicks + SuzakuSunPillar.StrikeTicks) {
                for (int i = -2; i <= 2; i += 2)
                    SpawnPillarAt(target, i * spacing);
            }

            if (AttackTimer > 170) Goto(St.Hub);
        }

        #endregion

        #region 涅槃重生 set-piece

        private void RunRebirth(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(3, 3);

            if (PhaseTimer < AshEnd) {
                // —— 灰烬沉默 ——
                if (!Main.dedServ && Main.rand.NextBool()) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Ash, Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(-2, 0), 120, default, 1.6f);
                    d.noGravity = false;
                }
            }
            else if (PhaseTimer == AshEnd) {
                // —— 爆燃复生：竞技场点燃 ——
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 2f }, NPC.Center);
                ShakeScreen(12f, 15f, 60, 3000f);
                fxBloom = 1f;
                fxRunic = 1f;
                glowIntensity = 3f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 60; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 4f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(22, 22);
                    }
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 双层同心焰环
                    for (int i = 0; i < 30; i++) {
                        float a = MathHelper.TwoPi / 30 * i;
                        Fire(NPC.Center, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 13f, FeatherType, FeatherDmg);
                    }
                    for (int i = 0; i < 22; i++) {
                        float a = MathHelper.TwoPi / 22 * i + MathHelper.ToRadians(8f);
                        Fire(NPC.Center, new Vector2(MathF.Cos(a), MathF.Sin(a)) * 8f, EmberType, EmberDmg);
                    }
                    // 竞技场点燃：环形火柱
                    for (int i = 0; i < 6; i++)
                        SpawnPillarAt(target, -600 + 240 * i);
                }
            }
            else {
                // —— 复生余波（火焰自玩家外缘升腾）——
                fxRunic = MathHelper.Max(fxRunic, 0.5f);
                if (!Main.dedServ && PhaseTimer % 4 == 0) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(220, 220);
                        Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = new Vector2(0, -Main.rand.NextFloat(2, 6));
                    }
                }
            }

            if (PhaseTimer >= RebirthEnd) {
                NPC.dontTakeDamage = false;
                ResetRotation(3);
                diveCount = 0;
                Goto(St.Hub);
            }
        }

        private void UpdateRebirthGrade() {
            if (State == St.Rebirth) {
                if (PhaseTimer <= AshEnd) {
                    float g = MathHelper.Clamp(PhaseTimer / AshEnd, 0f, 1f);
                    rebirthLut = g;
                    rebirthSat = MathHelper.Lerp(1f, 0.1f, g);
                    rebirthShadow = new Vector4(new Color(64, 60, 58).ToVector3(), 0.6f * g);
                    rebirthHi = new Vector4(new Color(120, 116, 112).ToVector3(), 0.6f * g);
                }
                else {
                    float g = MathHelper.Clamp((PhaseTimer - AshEnd) / (RebirthEnd - AshEnd), 0f, 1f);
                    rebirthLut = MathHelper.Lerp(1f, 0.85f, g);
                    rebirthSat = MathHelper.Lerp(0.1f, 1.5f, g);
                    rebirthShadow = new Vector4(new Color(120, 18, 12).ToVector3(), 0.7f);
                    rebirthHi = new Vector4(new Color(255, 150, 70).ToVector3(), 0.7f);
                }
            }
            else {
                rebirthLut = MathHelper.Lerp(rebirthLut, 0f, 0.08f);
            }
        }

        #endregion

        #region 绘制

        public override void FindFrame(int frameHeight) {
            bool dashing = (State == St.P2_PhoenixDive || State == St.P3_PhoenixDance) && SubStateRaw == 1;
            if (dashing) {
                NPC.frame.Y = 0;
                frameCounter = 0;
                return;
            }

            bool slow = State == St.Intro || State == St.Trans2 || State == St.Trans3 || State == St.Rebirth;
            int rate = slow ? 10 : (NPC.velocity.LengthSquared() > 100f ? 4 : 6);
            frameCounter++;
            if (frameCounter >= rate) {
                frameCounter = 0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * 4)
                    NPC.frame.Y = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 俯冲蓄力：固定落点地面影子预警（非致命赤 → 末段转金）
            DrawDiveTelegraph(spriteBatch, screenPos);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingRight = NPC.spriteDirection >= 0;
            SpriteEffects effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRotation = facingRight ? NPC.rotation : -NPC.rotation;

            // 涅槃灰烬：本体压暗 → 复生回明
            Color bodyColor = drawColor;
            if (State == St.Rebirth && PhaseTimer < AshEnd) {
                float g = MathHelper.Clamp(PhaseTimer / (float)AshEnd, 0f, 1f);
                bodyColor = Color.Lerp(drawColor, new Color(70, 65, 62), g);
            }

            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float alpha = 0.5f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                Color trailColor = bodyColor * alpha;
                trailColor.G = (byte)Math.Min(trailColor.G * 1.1f, 255);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, drawRotation, origin,
                    NPC.scale * (1f - i * 0.015f), effects, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, bodyColor, drawRotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        private void DrawDiveTelegraph(SpriteBatch sb, Vector2 screenPos) {
            if (Main.dedServ) return;
            bool windup = (State == St.P2_PhoenixDive || State == St.P3_PhoenixDance) && SubStateRaw == 0 && AttackTimer >= 1;
            if (!windup) return;

            float grow = MathHelper.Clamp(AttackTimer / (float)DiveWindup, 0f, 1f);
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 pos = diveTarget - screenPos;
            Vector2 go = glow.Size() / 2f;

            // 非致命赤 → 临击转金（提示"即将致命"）
            Color c = Color.Lerp(TelegraphColors.Vermilion, TelegraphColors.Gold, grow * grow);
            c.A = 0;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            // 椭圆地影（横向压扁）
            sb.Draw(glow, pos, null, c * (0.4f + grow * 0.5f), 0f, go, new Vector2(2.0f, 0.7f) * (0.5f + grow * 0.6f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, c * 0.6f, 0f, go, new Vector2(1.1f, 0.4f) * (0.4f + grow * 0.5f), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // PaletteLUT 涅槃灰↔赤 全屏调色（单一全屏后处理名额）
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || rebirthLut <= 0.01f) return;
            if (!MythologyConfig.FullscreenShadersEnabled) return;
            if (!ACMShaders.RequestFullscreenSlot()) return;

            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null) return;

            fx.Parameters["uTime"]?.SetValue(GlobalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(rebirthLut, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uSaturation"]?.SetValue(rebirthSat);
            fx.Parameters["uHueShift"]?.SetValue(0f);
            fx.Parameters["uShadowTint"]?.SetValue(rebirthShadow);
            fx.Parameters["uHighlightTint"]?.SetValue(rebirthHi);
            fx.Parameters["uSplit"]?.SetValue(0f);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion
    }
}

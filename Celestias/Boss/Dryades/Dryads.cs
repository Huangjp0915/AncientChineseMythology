using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
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

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精 - 大椿的弱化版本，月后初期Boss
    /// 固定在原地不动的树类Boss，核心机制：潜入地下→靠近玩家的地面冒出
    /// 贴图尺寸：520×552，底部60像素为树根需埋入地下
    /// </summary>
    [AutoloadBossHead]
    public class Dryads : ModNPC
    {
        #region 常量

        public const int TextureWidth = 520;
        public const int TextureHeight = 552;
        public const int RootBuryOffset = 60; // 树根埋入地下的像素偏移
        public const float Phase2Threshold = 0.50f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Idle,
            Phase1_RootBurst,       // 根须爆发 - 地面涌出根须攻击玩家
            Phase1_AcanthoToss,     // 刺球投掷 - 抛出多个刺球弹幕
            Phase1_LeafBarrage,     // 落叶弹幕 - 叶片从上方洒落
            Phase1_Burrow,          // 潜地转移 - 潜入地下，从玩家附近地面冒出
            Phase1_VineLash,        // 藤蔓抽打 - 快速藤蔓弹幕
            PhaseTransition_2,
            Phase2_Idle,
            Phase2_RootStorm,       // 根须风暴 - 强化版根须爆发
            Phase2_AcanthoBarrage,  // 刺球弹幕 - 密集刺球攻击
            Phase2_Burrow,          // 强化潜地 - 更频繁更快速
            Phase2_NatureWrath,     // 自然之怒 - 综合型攻击
            Phase2_SpikeField,      // 刺球领域 - 环绕Boss的刺球防御圈
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

        private float globalTime;
        private bool didPhase2Transition;
        private Vector2 anchorPosition;     // 当前固定位置（树精站桩点）
        private float introRiseOffset;      // 入场上升偏移
        private bool isRising;              // 是否正在上升
        private bool isBurrowing;           // 是否正在潜入/冒出中
        private float burrowProgress;       // 潜地进度 0=完全在地面, 1=完全在地下
        private int attackCycle;
        private Vector2 burrowTargetPos;    // 潜地后的目标冒出位置

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 0;
            NPCID.Sets.TrailCacheLength[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 160;
            NPC.height = 200;
            NPC.damage = 150;
            NPC.defense = 60;
            NPC.lifeMax = 1000000;
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.3f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = -0.6f };
            NPC.value = Item.buyPrice(platinum: 3);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.3f);
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
            // 活木 - 掉落15~25个
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Livinglog>(), minimumDropped: 45, maximumDropped: 60));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            anchorPosition = FindGroundPosition(NPC.Center);
            introRiseOffset = TextureHeight + 100f;
            isRising = true;
            isBurrowing = false;
            burrowProgress = 0f;
            NPC.Center = anchorPosition + new Vector2(0, introRiseOffset);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        /// <summary>
        /// 从给定位置向下扫描找到固体地面
        /// 树精底部对齐地面，但树根60像素需埋入地下
        /// 所以Center = 地面Y - TextureHeight/2 + RootBuryOffset
        /// </summary>
        private Vector2 FindGroundPosition(Vector2 startPos) {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);

            int groundTileY = startTileY;
            for (int y = startTileY; y < startTileY + 150 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    groundTileY = y;
                    break;
                }
            }

            float groundWorldY = groundTileY * 16f;
            // Center位置：地面 - 纹理半高 + 树根埋入偏移
            float centerY = groundWorldY - TextureHeight / 2f + RootBuryOffset;
            return new Vector2(startPos.X, centerY);
        }

        /// <summary>
        /// 从玩家位置附近找到最近的地面，用于潜地冒出
        /// </summary>
        private Vector2 FindGroundNearPlayer(Player player) {
            float offsetX = Main.rand.NextFloat(-300, 300);
            Vector2 searchStart = player.Center + new Vector2(offsetX, -400);
            return FindGroundPosition(searchStart);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(anchorPosition.X);
            writer.Write(anchorPosition.Y);
            writer.Write(introRiseOffset);
            writer.Write(isRising);
            writer.Write(isBurrowing);
            writer.Write(burrowProgress);
            writer.Write(attackCycle);
            writer.Write(burrowTargetPos.X);
            writer.Write(burrowTargetPos.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            anchorPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            introRiseOffset = reader.ReadSingle();
            isRising = reader.ReadBoolean();
            isBurrowing = reader.ReadBoolean();
            burrowProgress = reader.ReadSingle();
            attackCycle = reader.ReadInt32();
            burrowTargetPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (isRising || burrowProgress > 0.8f) return false;
            return null;
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index) {
            if (isRising || isBurrowing)
                Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.WoodFurniture, hit.HitDirection * 2f, -2f, 150, default, 1.5f);
                d.noGravity = false;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.GrassBlades, hit.HitDirection * 1.5f, -1f, 100, default, 1.8f);
                d.noGravity = true;
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.WoodFurniture, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.GrassBlades, 0, 0, 80, default, 2f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedDryads = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center,
                    (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                    18f, 10f, 60, 2500f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            globalTime += 1f / 60f;

            NPC.velocity = Vector2.Zero;

            // 位置跟随当前锚点（考虑入场/潜地偏移）
            if (anchorPosition != Vector2.Zero) {
                if (isRising)
                    NPC.Center = anchorPosition + new Vector2(0, introRiseOffset);
                else if (isBurrowing) {
                    float burrowOffset = burrowProgress * (TextureHeight + 60f);
                    NPC.Center = anchorPosition + new Vector2(0, burrowOffset);
                } else
                    NPC.Center = anchorPosition;
            }

            NPC.behindTiles = isRising || isBurrowing;
            if (isRising) {
                for (int i = 0; i < 60; i++)
                    Lighting.AddLight(NPC.Center + Main.rand.NextVector2Circular(300, 300),
                        Color.ForestGreen.ToVector3() * 6);
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 潜地中不受攻击
            NPC.dontTakeDamage = isRising || burrowProgress > 0.5f;

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Idle: RunPhase1Idle(target); break;
                case BossPhase.Phase1_RootBurst: RunPhase1RootBurst(target); break;
                case BossPhase.Phase1_AcanthoToss: RunPhase1AcanthoToss(target); break;
                case BossPhase.Phase1_LeafBarrage: RunPhase1LeafBarrage(target); break;
                case BossPhase.Phase1_Burrow: RunBurrow(target, false); break;
                case BossPhase.Phase1_VineLash: RunPhase1VineLash(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_Idle: RunPhase2Idle(target); break;
                case BossPhase.Phase2_RootStorm: RunPhase2RootStorm(target); break;
                case BossPhase.Phase2_AcanthoBarrage: RunPhase2AcanthoBarrage(target); break;
                case BossPhase.Phase2_Burrow: RunBurrow(target, true); break;
                case BossPhase.Phase2_NatureWrath: RunPhase2NatureWrath(target); break;
                case BossPhase.Phase2_SpikeField: RunPhase2SpikeField(target); break;
            }

            // 自然光辉
            float glow = 0.4f + MathF.Sin(globalTime * 2f) * 0.15f;
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.45f, 0.1f) * glow);

            // 持续落叶粒子
            if (Main.netMode != NetmodeID.Server && !isBurrowing && Main.rand.NextBool(4)) {
                Vector2 leafPos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-TextureWidth / 2, TextureWidth / 2),
                    Main.rand.NextFloat(-TextureHeight / 2, TextureHeight / 4));
                Dust d = Dust.NewDustDirect(leafPos, 0, 0, DustID.GrassBlades,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f), 100, default, 1.3f);
                d.noGravity = false;
                d.fadeIn = 1.1f;
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro &&
                !isBurrowing) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            attackCycle++;
            // 每3轮强制一次潜地
            if (attackCycle % 3 == 0)
                return BossPhase.Phase1_Burrow;

            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_RootBurst,
                1 => (int)BossPhase.Phase1_AcanthoToss,
                2 => (int)BossPhase.Phase1_LeafBarrage,
                _ => (int)BossPhase.Phase1_VineLash,
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            attackCycle++;
            // 每3轮强制一次潜地
            if (attackCycle % 3 == 0)
                return BossPhase.Phase2_Burrow;

            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase2_RootStorm,
                1 => (int)BossPhase.Phase2_AcanthoBarrage,
                2 => (int)BossPhase.Phase2_NatureWrath,
                _ => (int)BossPhase.Phase2_SpikeField,
            });
        }

        #endregion

        #region 入场演出

        private const int IntroRumbleEnd = 50;
        private const int IntroRiseEnd = 170;
        private const int IntroEruptEnd = 210;
        private const int IntroFinish = 240;

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            float groundWorldY = anchorPosition.Y + TextureHeight / 2f - RootBuryOffset;

            // ========== 地面震动预兆（0-50）==========
            if (PhaseTimer <= IntroRumbleEnd) {
                if (PhaseTimer == 1)
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.5f },
                        new Vector2(anchorPosition.X, groundWorldY));

                if (PhaseTimer % 5 == 0 && Main.netMode != NetmodeID.Server) {
                    float shakeStr = MathHelper.Lerp(1f, 5f, PhaseTimer / (float)IntroRumbleEnd);
                    PunchCameraModifier modifier = new(
                        new Vector2(anchorPosition.X, groundWorldY),
                        Vector2.UnitY.RotatedByRandom(0.3f),
                        shakeStr, 3f, 4, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                if (Main.netMode != NetmodeID.Server) {
                    int crackIntensity = (int)MathHelper.Lerp(1, 4, PhaseTimer / (float)IntroRumbleEnd);
                    for (int i = 0; i < crackIntensity; i++) {
                        Vector2 crackPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-200, 200),
                            groundWorldY + Main.rand.NextFloat(-6, 6));
                        Dust d = Dust.NewDustDirect(crackPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f),
                            150, default, Main.rand.NextFloat(1f, 1.8f));
                        d.noGravity = false;
                    }
                    if (PhaseTimer > 15) {
                        for (int i = 0; i < crackIntensity / 2 + 1; i++) {
                            Vector2 glowPos = new(
                                anchorPosition.X + Main.rand.NextFloat(-150, 150),
                                groundWorldY + Main.rand.NextFloat(-4, 4));
                            Dust d = Dust.NewDustDirect(glowPos, 0, 0, DustID.GreenTorch,
                                0, Main.rand.NextFloat(-2f, -0.5f), 80, default, 1.5f);
                            d.noGravity = true;
                        }
                    }
                }

                Lighting.AddLight(new Vector2(anchorPosition.X, groundWorldY),
                    new Vector3(0.15f, 0.4f, 0.08f) * (PhaseTimer / (float)IntroRumbleEnd));
            }

            // ========== 从地下上升（50-170）==========
            if (PhaseTimer > IntroRumbleEnd && PhaseTimer <= IntroRiseEnd) {
                float riseProgress = (PhaseTimer - IntroRumbleEnd) / (float)(IntroRiseEnd - IntroRumbleEnd);
                float easedProgress = riseProgress < 0.5f
                    ? 4f * riseProgress * riseProgress * riseProgress
                    : 1f - MathF.Pow(-2f * riseProgress + 2f, 3f) / 2f;

                float totalRise = TextureHeight + 100f;
                introRiseOffset = totalRise * (1f - easedProgress);

                if (PhaseTimer == IntroRumbleEnd + 1)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.7f },
                        new Vector2(anchorPosition.X, groundWorldY));

                if (Main.netMode != NetmodeID.Server) {
                    float riseSpeed = (riseProgress > 0.1f && riseProgress < 0.9f) ? 1.2f : 0.4f;
                    for (int i = 0; i < (int)(3 * riseSpeed); i++) {
                        Vector2 dirtPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-250, 250),
                            groundWorldY + Main.rand.NextFloat(-15, 8));
                        Dust d = Dust.NewDustDirect(dirtPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1.5f),
                            120, default, Main.rand.NextFloat(1.2f, 2f));
                        d.noGravity = false;
                    }

                    if (riseProgress > 0.2f) {
                        for (int i = 0; i < 2; i++) {
                            Vector2 vinePos = new(
                                anchorPosition.X + Main.rand.NextFloat(-280, 280),
                                groundWorldY - Main.rand.NextFloat(0, 20));
                            Dust d = Dust.NewDustDirect(vinePos, 0, 0, DustID.JungleGrass,
                                Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f),
                                100, default, 1.6f);
                            d.noGravity = true;
                        }
                    }

                    if (PhaseTimer % 7 == 0) {
                        float shakeStr = 2f + riseSpeed * 3f;
                        PunchCameraModifier modifier = new(
                            NPC.Center, Vector2.UnitY.RotatedByRandom(0.3f),
                            shakeStr, 3f, 5, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                }

                float lightStr = MathHelper.Lerp(0.2f, 0.6f, riseProgress);
                Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.45f, 0.1f) * lightStr);
            }

            // ========== 破土爆发（170）==========
            if (PhaseTimer == IntroRiseEnd) {
                introRiseOffset = 0f;
                isRising = false;

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        20f, 10f, 50, 2500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 30; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(3f, 10f);
                        Vector2 debrisPos = new(anchorPosition.X + Main.rand.NextFloat(-150, 150),
                            groundWorldY + Main.rand.NextFloat(-20, 8));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 2f,
                            100, default, Main.rand.NextFloat(1.5f, 3f));
                        d.noGravity = false;
                    }
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20 * i;
                        float speed = Main.rand.NextFloat(5f, 10f);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed,
                            60, default, 2f);
                        d.noGravity = true;
                    }
                }

                NPC.netUpdate = true;
            }

            // 余波（170-210）
            if (PhaseTimer > IntroRiseEnd && PhaseTimer <= IntroEruptEnd) {
                float eruptFade = 1f - (PhaseTimer - IntroRiseEnd) / (float)(IntroEruptEnd - IntroRiseEnd);
                if (Main.netMode != NetmodeID.Server) {
                    int waveCount = (int)(5 * eruptFade);
                    for (int i = 0; i < waveCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(80, 350);
                        Vector2 ePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(ePos, 0, 0, DustID.GreenTorch,
                            0, -1f, 80, default, 1.5f * eruptFade);
                        d.noGravity = true;
                    }
                }
                Lighting.AddLight(NPC.Center, new Vector3(0.35f, 0.6f, 0.2f) * eruptFade);
            }

            // ========== 进入战斗（240）==========
            if (PhaseTimer >= IntroFinish) {
                NPC.dontTakeDamage = false;
                attackCycle = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                TransitionTo(BossPhase.Phase1_Idle);
            }
        }

        #endregion

        #region 潜地机制（核心特色）

        /// <summary>
        /// 潜地转移 - 树精的核心机制
        /// 过程：下沉入地 → 地下移动（不可见）→ 从玩家附近地面冒出
        /// </summary>
        private const int BurrowSinkDuration = 80;
        private const int BurrowUndergroundWait = 60;
        private const int BurrowRiseDuration = 80;
        private const int BurrowP2SinkDuration = 55;
        private const int BurrowP2UndergroundWait = 40;
        private const int BurrowP2RiseDuration = 55;

        private void RunBurrow(Player target, bool isPhase2) {
            int sinkDur = isPhase2 ? BurrowP2SinkDuration : BurrowSinkDuration;
            int waitDur = isPhase2 ? BurrowP2UndergroundWait : BurrowUndergroundWait;
            int riseDur = isPhase2 ? BurrowP2RiseDuration : BurrowRiseDuration;
            int totalSinkAndWait = sinkDur + waitDur;
            int totalDuration = totalSinkAndWait + riseDur;

            float groundWorldY = anchorPosition.Y + TextureHeight / 2f - RootBuryOffset;

            // ========== 阶段1：下沉 ==========
            if (PhaseTimer <= sinkDur) {
                isBurrowing = true;
                float sinkT = PhaseTimer / (float)sinkDur;
                float easedT = sinkT * sinkT;
                burrowProgress = easedT;

                if (PhaseTimer == 1)
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.6f }, NPC.Center);

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 debrisPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-200, 200),
                            groundWorldY + Main.rand.NextFloat(-10, 10));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-3f, -0.5f),
                            130, default, 1.5f);
                        d.noGravity = false;
                    }
                }

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 8 == 0) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        Vector2.UnitY, 3f + sinkT * 3f, 3f, 6, 1500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            // ========== 阶段2：地下（不可见）==========
            if (PhaseTimer == sinkDur + 1) {
                burrowProgress = 1f;
                burrowTargetPos = FindGroundNearPlayer(target);
                NPC.netUpdate = true;
            }

            if (PhaseTimer > sinkDur && PhaseTimer <= totalSinkAndWait) {
                burrowProgress = 1f;

                int warningTimer = (int)PhaseTimer - sinkDur;
                if (warningTimer > waitDur / 3 && Main.netMode != NetmodeID.Server) {
                    float targetGroundY = burrowTargetPos.Y + TextureHeight / 2f - RootBuryOffset;
                    float warningIntensity = (float)(warningTimer - waitDur / 3) / (waitDur * 2f / 3);
                    int dustCount = (int)(4 * warningIntensity) + 1;
                    for (int i = 0; i < dustCount; i++) {
                        Vector2 warnPos = new(
                            burrowTargetPos.X + Main.rand.NextFloat(-180, 180),
                            targetGroundY + Main.rand.NextFloat(-8, 8));
                        Dust d = Dust.NewDustDirect(warnPos, 0, 0, DustID.GreenTorch,
                            Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -0.5f),
                            80, default, 1.5f * warningIntensity);
                        d.noGravity = true;
                    }

                    if (warningTimer % 6 == 0) {
                        PunchCameraModifier modifier = new(
                            new Vector2(burrowTargetPos.X, targetGroundY),
                            Vector2.UnitY.RotatedByRandom(0.2f),
                            2f * warningIntensity, 2f, 4, 1500f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    Lighting.AddLight(new Vector2(burrowTargetPos.X, targetGroundY),
                        new Vector3(0.2f, 0.5f, 0.1f) * warningIntensity);
                }
            }

            // ========== 阶段3：从新位置冒出 ==========
            if (PhaseTimer == totalSinkAndWait + 1) {
                anchorPosition = burrowTargetPos;
                NPC.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 0.9f },
                    anchorPosition);
            }

            if (PhaseTimer > totalSinkAndWait && PhaseTimer <= totalDuration) {
                float riseT = (PhaseTimer - totalSinkAndWait) / (float)riseDur;
                float easedRise = 1f - (1f - riseT) * (1f - riseT);
                burrowProgress = 1f - easedRise;

                groundWorldY = anchorPosition.Y + TextureHeight / 2f - RootBuryOffset;

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 debrisPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-250, 250),
                            groundWorldY + Main.rand.NextFloat(-15, 8));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1f),
                            120, default, Main.rand.NextFloat(1.2f, 2.2f));
                        d.noGravity = false;
                    }
                    if (riseT > 0.3f) {
                        for (int i = 0; i < 2; i++) {
                            Vector2 vinePos = new(
                                anchorPosition.X + Main.rand.NextFloat(-200, 200),
                                groundWorldY - Main.rand.NextFloat(0, 15));
                            Dust d = Dust.NewDustDirect(vinePos, 0, 0, DustID.JungleGrass,
                                Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.5f, -0.5f),
                                100, default, 1.4f);
                            d.noGravity = true;
                        }
                    }
                }

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 6 == 0) {
                    float shakeStr = 2f + riseT * 4f;
                    PunchCameraModifier modifier = new(NPC.Center,
                        Vector2.UnitY.RotatedByRandom(0.3f),
                        shakeStr, 3f, 5, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            // ========== 冒出完成 ==========
            if (PhaseTimer == totalDuration) {
                burrowProgress = 0f;
                isBurrowing = false;

                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);

                // 冒出时爆发藤蔓
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int spikeCount = isPhase2 ? 8 : 5;
                    for (int i = 0; i < spikeCount; i++) {
                        float angle = MathHelper.TwoPi / spikeCount * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (isPhase2 ? 7f : 5f);
                        vel.Y -= 3f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                }

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        15f, 8f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 20; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(3f, 8f);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed,
                            60, default, 2f);
                        d.noGravity = true;
                    }
                }

                NPC.netUpdate = true;
            }

            if (PhaseTimer > totalDuration + 30) {
                TransitionTo(isPhase2 ? BossPhase.Phase2_Idle : BossPhase.Phase1_Idle);
            }
        }

        #endregion

        #region 一阶段攻击

        private void RunPhase1Idle(Player target) {
            if (PhaseTimer % 50 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 6f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(15f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 5, 0.5f, Main.myPlayer);
            }

            if (PhaseTimer > 100) TransitionTo(GetRandomPhase1Attack());
        }

        /// <summary>
        /// 根须爆发 - 从玩家身边地面涌出根须
        /// </summary>
        private void RunPhase1RootBurst(Player target) {
            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int rootCount = Main.expertMode ? 5 : 4;
                for (int i = 0; i < rootCount; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-400, 400), 300);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(8f, 14f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.5f, Volume = 0.7f }, target.Center);
            }

            if (Main.netMode != NetmodeID.Server && AttackTimer % 25 >= 15) {
                for (int i = 0; i < 3; i++) {
                    Vector2 warnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-400, 400), 250 + Main.rand.NextFloat(0, 50));
                    Dust d = Dust.NewDustDirect(warnPos, 0, 0, DustID.GreenTorch,
                        0, Main.rand.NextFloat(-2f, -0.5f), 80, default, 1.3f);
                    d.noGravity = true;
                }
            }

            if (AttackTimer > 160) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 刺球投掷 - 向玩家方向投掷多个刺球
        /// </summary>
        private void RunPhase1AcanthoToss(Player target) {
            if ((AttackTimer == 20 || AttackTimer == 60 || AttackTimer == 100) &&
                Main.netMode != NetmodeID.MultiplayerClient) {
                int tossCount = Main.expertMode ? 5 : 4;
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

                for (int i = 0; i < tossCount; i++) {
                    float spread = MathHelper.ToRadians(15f);
                    Vector2 vel = toPlayer.RotatedBy(Main.rand.NextFloat(-spread, spread)) *
                        Main.rand.NextFloat(7f, 12f);
                    vel.Y -= 3f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toPlayer * 80f, vel,
                        ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);
            }

            if (AttackTimer > 140) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 落叶弹幕 - 从Boss上方洒落刺球
        /// </summary>
        private void RunPhase1LeafBarrage(Player target) {
            int wave = (int)(AttackTimer / 40);
            int leafPerTick = 2 + wave;
            if (leafPerTick > 5) leafPerTick = 5;

            if (AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < leafPerTick; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-500, 500), -400 - Main.rand.NextFloat(0, 150));
                    Vector2 vel = new Vector2(
                        Main.rand.NextFloat(-2.5f, 2.5f),
                        Main.rand.NextFloat(5f, 10f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 5, 0.5f, Main.myPlayer);
                }
            }

            if (AttackTimer % 40 == 0 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = 0.2f }, target.Center);

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 藤蔓抽打 - 快速扇形刺球
        /// </summary>
        private void RunPhase1VineLash(Player target) {
            int lashInterval = Main.expertMode ? 16 : 20;

            if (AttackTimer % lashInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int lashCount = Main.expertMode ? 5 : 4;
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                float spreadAngle = MathHelper.ToRadians(10f);

                for (int i = -lashCount / 2; i <= lashCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 14f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1.5f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.2f, Volume = 0.7f }, NPC.Center);
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase1_Idle);
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi / 8 * i + globalTime * 3f;
                    float dist = 300 - PhaseTimer * 2f;
                    if (dist < 60) dist = 60;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch,
                        0, 0, 80, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.JungleGrass,
                        0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        20f, 10f, 50, 2500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }
            }

            if (PhaseTimer >= 80) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                attackCycle = 0;
                TransitionTo(BossPhase.Phase2_Idle);
            }
        }

        #endregion

        #region 二阶段攻击

        private void RunPhase2Idle(Player target) {
            if (PhaseTimer % 35 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 8f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 5, 0.5f, Main.myPlayer);
            }

            if (PhaseTimer > 80) TransitionTo(GetRandomPhase2Attack());
        }

        /// <summary>
        /// 根须风暴 - 强化版根须爆发
        /// </summary>
        private void RunPhase2RootStorm(Player target) {
            if (AttackTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int rootCount = Main.expertMode ? 7 : 6;
                for (int i = 0; i < rootCount; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-500, 500), 350);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(10f, 16f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 3, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.6f, Volume = 0.8f }, target.Center);
            }

            if (AttackTimer % 30 == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-400, 400), -500);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(6f, 10f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 4, 0.5f, Main.myPlayer);
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase2_Idle);
        }

        /// <summary>
        /// 刺球弹幕 - 密集旋转散射
        /// </summary>
        private void RunPhase2AcanthoBarrage(Player target) {
            float rotOffset = AttackTimer * 0.06f;

            if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int armCount = Main.expertMode ? 4 : 3;
                for (int arm = 0; arm < armCount; arm++) {
                    float angle = rotOffset + MathHelper.TwoPi / armCount * arm;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer % 35 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 12f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 3, 1.5f, Main.myPlayer);
            }

            if (AttackTimer > 160) TransitionTo(BossPhase.Phase2_Idle);
        }

        /// <summary>
        /// 自然之怒 - 综合攻击模式
        /// </summary>
        private void RunPhase2NatureWrath(Player target) {
            if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                int ringCount = Main.expertMode ? 12 : 10;
                for (int i = 0; i < ringCount; i++) {
                    float angle = MathHelper.TwoPi / ringCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.5f }, NPC.Center);
            }

            if (AttackTimer > 50 && AttackTimer < 120 && AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 4; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-450, 450), 350);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(9f, 14f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer == 130 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6 * i;
                    Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f;
                    Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 10f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 3, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > 170) TransitionTo(BossPhase.Phase2_Idle);
        }

        /// <summary>
        /// 刺球领域 - 收缩型刺球牢笼
        /// </summary>
        private void RunPhase2SpikeField(Player target) {
            if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int ring = 0; ring < 2; ring++) {
                    int spikeCount = 10 + ring * 4;
                    float ringRadius = 350f + ring * 150f;
                    int gapStart = Main.rand.Next(spikeCount);
                    int gapSize = 3;
                    for (int i = 0; i < spikeCount; i++) {
                        if (i >= gapStart && i < gapStart + gapSize) continue;
                        if (gapStart + gapSize > spikeCount && i < (gapStart + gapSize) % spikeCount) continue;

                        float angle = MathHelper.TwoPi / spikeCount * i + ring * MathHelper.ToRadians(8f);
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (2f + ring * 0.5f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 0.5f, Main.myPlayer);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 1f }, target.Center);
            }

            if (AttackTimer > 60 && AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 11f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(15f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 1f, Main.myPlayer);
            }

            if (AttackTimer > 160) TransitionTo(BossPhase.Phase2_Idle);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = new Vector2(TextureWidth / 2f, TextureHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            float glowPulse = 0f;
            if (IsPhase2) {
                glowPulse = 0.12f + MathF.Sin(globalTime * 3.5f) * 0.08f;
            }

            if (glowPulse > 0) {
                Color greenGlow = new Color(100, 255, 80, 0) * glowPulse;
                spriteBatch.Draw(texture, drawPos, frame, greenGlow, 0f, origin,
                    NPC.scale * 1.04f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, drawPos, frame, drawColor, 0f, origin,
                NPC.scale, SpriteEffects.None, 0f);

            return false;
        }

        #endregion
    }
}

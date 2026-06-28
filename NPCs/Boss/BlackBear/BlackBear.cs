using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Weapons.Bows;
using AncientChineseMythology.Items.Weapons.SummoningStaffs;
using AncientChineseMythology.Items.Weapons.Swords;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精 Black Bear —— 早期丛林试炼熊 (Tutorial Bruiser, V2)。
    /// 设计目标: 当好"Boss 会进入新阶段"的入门教学样板 —— P2 是<b>换规则而非加数值</b>。
    ///
    /// P1 (100~50%): 地面追击 + 近战挥击 (Attack_1) + 扑投 (Attack_2) + 招牌"熊抱冲撞" (BearHug:
    ///   张臂蓄力 1s → 直线前扑, 玩家向侧面闪)。
    /// 半血 i-frame 节拍 (Enrage): 一次咆哮砸地短演出 (裂地 ArenaRunic + 泛光 + 屏震 + 短暂无敌), 不被秒过场。
    /// P2 "狂怒" (换规则): 场地蜂蜜滴落灾害 (落点地影预警的下落弹) + 头饰光环 (原 Proj3 改为环绕玩家 3s 后向内收拢的复发光环)。
    /// 新接入 Attack_3 "立地震地" (站立蓄力 2s → 地面冲击波锥), 给远程玩家输出窗口。
    ///
    /// 伤害契约 (King Slime 式): <b>仅攻击激活帧</b>有接触伤害 (其余帧 <see cref="NPC.damage"/>=0); 移除旧"扩大碰撞框每 0.5s 强制扣血"垃圾。
    /// 表现 (早期 Boss, 克制用量): telegraph 走 <see cref="TelegraphColors"/> + <see cref="ACMShaders.ArenaRunic"/> 地纹 / <see cref="ACMShaders.DrawBeam"/> 冲撞线;
    ///   砸地泛光 <see cref="ACMShaders.DrawRadialBloomAt"/>; 屏震 <see cref="ACMScreenShakeSystem"/>。红=致命唯一色, 服务端零绘制, 遵从 <see cref="MythologyConfig"/>。
    /// </summary>
    [AutoloadBossHead]
    public class BlackBear : ModNPC
    {
        private enum BearState
        {
            Idle,
            Run,
            Attack_1,   // 近战挥击
            Attack_2,   // 扑投弹幕
            Attack_3,   // 立地震地 (蓄力 → 地面冲击波锥)
            BearHug,    // 熊抱冲撞 (张臂蓄力 → 直线前扑)
            Enrage,     // 半血相变节拍
            Die
        }

        private BearState currentState = BearState.Idle;

        // —— 动画帧控制 ——
        private int currentFrame = 0;
        private int frameTimer = 0;

        // —— FSM ——
        private int stateTimer = 0;          // 当前状态已持续帧数
        private int attackCooldown = 90;     // 距下次出招的冷却
        private bool furyTriggered = false;  // 是否已触发过半血相变
        private float lungeDirX = 1f;        // 熊抱冲撞锁定的水平方向

        // —— 接触伤害 (仅激活帧) ——
        private const int ContactDamage = 55;

        // —— P2 狂怒灾害节流 ——
        private int honeyTimer = 0;
        private int haloTimer = 0;

        // —— 演出强度 (供 PreDraw 读取; 在 AI 中衰减) ——
        private float slamFlash = 0f;
        private float enrageFlash = 0f;

        // —— 运动 ——
        private float runSpeed = 11.0f;
        private bool onGround = false;

        // —— 精灵图 ——
        private Texture2D dieTexture;
        private Texture2D attackTexture_1;
        private Texture2D attackTexture_1_1;
        private Texture2D attackTexture_2;
        private Texture2D attackTexture_2_2;
        private Texture2D runTexture;
        private Texture2D runTexture_1;
        private Texture2D idleTexture;
        private Texture2D idleTexture_1;

        private int deathIdleTimer = 0;

        // —— 死亡 ——
        private bool isDying = false;
        private int dieTimer = 0;

        // —— 追踪/脱战 ——
        private const int LostSightThreshold = 600; // ≈10 秒
        private int lostSightTimer = 0;

        private bool Fury => NPC.life * 2 < NPC.lifeMax; // <50%

        // 使用静态占位图
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear";
        // Boss 头像
        public override string BossHeadTexture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss";

        public override void SetDefaults() {
            dieTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/die_328").Value;
            attackTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_1").Value;
            attackTexture_2 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_2").Value;
            attackTexture_1_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_1_1").Value;
            attackTexture_2_2 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/attack_328_2_2").Value;

            runTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/run_332").Value;
            runTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/run_332_1").Value;
            idleTexture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/idle_344").Value;
            idleTexture_1 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/idle_344_1").Value;

            NPC.width = 220;
            NPC.height = 280;
            NPC.damage = ContactDamage;
            NPC.defense = 20;
            NPC.lifeMax = 8888;
            NPC.knockBackResist = 0f;
            NPC.value = Item.buyPrice(0, 10, 0, 0);
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.boss = true;
            NPC.HitSound = SoundID.NPCHit1;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/BlackBear_Music");
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (spawnInfo.Player.ZoneJungle && spawnInfo.Player.ZoneOverworldHeight && Main.dayTime) {
                return 0.005f;
            }
            return 0f;
        }

        public override void AI() {
            NPC.TargetClosest(true);
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers) {
                DespawnLogic();
                return;
            }
            Player player = Main.player[NPC.target];

            // 演出强度衰减
            slamFlash *= 0.9f;
            enrageFlash *= 0.92f;

            if (player.dead) {
                HandlePlayerDeadIdle();
                return;
            }
            deathIdleTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.timeLeft = 300;

            onGround = NPC.collideY && NPC.velocity.Y >= 0f;

            if (isDying) {
                HandleDeath(player);
                UpdateAnimation();
                return;
            }

            // 默认: 无接触伤害 (King Slime 式, 仅攻击激活帧开启)
            NPC.damage = 0;

            // —— 半血相变节拍 (i-frame beat) ——
            if (Fury && !furyTriggered && currentState != BearState.Enrage) {
                EnterState(BearState.Enrage);
                furyTriggered = true;
                NPC.dontTakeDamage = true;
                enrageFlash = 1f;
                NPC.velocity.X = 0f;
                NPC.netUpdate = true;
                ACMScreenShakeSystem.Add(10f);
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), NPC.Center);
            }

            // —— P2 狂怒环境灾害 (换规则: 蜂蜜滴落 + 复发光环) ——
            if (Fury && currentState != BearState.Enrage)
                UpdateFuryHazards(player);

            switch (currentState) {
                case BearState.Run: DoRun(player); break;
                case BearState.Attack_1: DoSwipe(player); break;
                case BearState.Attack_2: DoToss(player); break;
                case BearState.Attack_3: DoSlam(player); break;
                case BearState.BearHug: DoBearHug(player); break;
                case BearState.Enrage: DoEnrage(player); break;
                case BearState.Idle:
                default: DoIdle(player); break;
            }

            if (!onGround)
                NPC.velocity.Y += 0.3f;
            if (NPC.velocity.Y > 18f)
                NPC.velocity.Y = 18f;

            UpdateDespawnSight(player);
            UpdateAnimation();
            stateTimer++;
        }

        // ============================================================
        //  状态处理
        // ============================================================

        private void EnterState(BearState s) {
            currentState = s;
            stateTimer = 0;
            currentFrame = 0;
            frameTimer = 0;
        }

        private void FaceTarget(Player player) {
            NPC.direction = NPC.spriteDirection = (player.Center.X > NPC.Center.X) ? 1 : -1;
        }

        private void DoIdle(Player player) {
            FaceTarget(player);
            NPC.velocity.X *= 0.8f;
            NPC.noTileCollide = false;
            if (attackCooldown > 0)
                attackCooldown--;
            if (stateTimer >= 18)
                EnterState(BearState.Run);
        }

        private void DoRun(Player player) {
            DoChaseMovement(player);
            if (attackCooldown > 0)
                attackCooldown--;

            if (attackCooldown <= 0 && onGround)
                ChooseAttack(player);
        }

        private void ChooseAttack(Player player) {
            float dist = Vector2.Distance(NPC.Center, player.Center);
            float roll = Main.rand.NextFloat();

            if (dist < 280f) {
                // 近身: 挥击为主, 偶尔熊抱
                EnterState(roll < 0.55f ? BearState.Attack_1 : BearState.BearHug);
            }
            else if (dist < 750f) {
                // 中距: 熊抱冲撞 / 扑投 / 偶尔震地
                if (roll < 0.40f) EnterState(BearState.BearHug);
                else if (roll < 0.75f) EnterState(BearState.Attack_2);
                else EnterState(BearState.Attack_3);
            }
            else {
                // 远距: 震地 (给远程窗口) 或扑投
                EnterState(roll < 0.6f ? BearState.Attack_3 : BearState.Attack_2);
            }
        }

        private void EndAttack() {
            attackCooldown = Fury ? 65 : 90; // P2 节奏略快, 但每招仍有清晰前摇 (非加跑速)
            EnterState(BearState.Idle);
        }

        // —— Attack_1: 近战挥击 (短前摇 + 激活帧地面冲击波) ——
        private void DoSwipe(Player player) {
            NPC.velocity.X *= 0.6f;
            const int windup = 24;
            const int activeStart = 24;
            const int activeEnd = 38;
            const int total = 52;

            if (stateTimer < windup)
                FaceTarget(player);

            // 激活帧: 开接触伤害 + 一次地面冲击波弹
            if (stateTimer >= activeStart && stateTimer <= activeEnd)
                NPC.damage = ContactDamage;

            if (stateTimer == activeStart) {
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1"), NPC.Center);
                ACMScreenShakeSystem.Add(5f);
                // 地面冲击波为可读地纹/视觉 (damage=0); 实际伤害走激活帧接触
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<BlackBear_Proj1>();
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(-NPC.width / 6f, -NPC.height / 12f),
                        Vector2.Zero, type, 0, 0f, Main.myPlayer);
                }
            }

            if (stateTimer >= total)
                EndAttack();
        }

        // —— Attack_2: 扑投蜂窝弹 ——
        private void DoToss(Player player) {
            NPC.velocity.X *= 0.85f;
            const int windup = 30;
            const int total = 56;

            if (stateTimer < windup)
                FaceTarget(player);

            if (stateTimer == windup) {
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), player.Center);
                ACMScreenShakeSystem.Add(3f);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = Main.rand.Next(7, 11);
                    int type = ModContent.ProjectileType<BlackBear_Proj2>();
                    for (int i = 0; i < count; i++) {
                        Vector2 spawn = NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width / 2f, NPC.width / 2f), -NPC.height / 2f);
                        // owner = 目标玩家索引: 供 Proj2 在 OnSpawn 朝该玩家瞄准 (MP 安全)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, Vector2.Zero, type, 20, 0f, NPC.target);
                    }
                }
            }

            if (stateTimer >= total)
                EndAttack();
        }

        // —— Attack_3: 立地震地 (2s 蓄力 → 地面冲击波锥) ——
        private void DoSlam(Player player) {
            NPC.velocity.X *= 0.5f; // 站立不动 → 远程输出窗口
            const int charge = 120;   // 2s 蓄力
            const int total = 150;

            if (stateTimer < 20)
                FaceTarget(player);

            // 蓄力期 dust 蓄势 (越接近释放越密)
            if (!Main.dedServ && stateTimer < charge && stateTimer % 3 == 0) {
                float prog = stateTimer / (float)charge;
                Vector2 p = NPC.Bottom + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f);
                Dust d = Dust.NewDustPerfect(p, DustID.Torch, new Vector2(0, -Main.rand.NextFloat(2f, 5f) * (0.4f + prog)), 100, Color.OrangeRed, 1.2f);
                d.noGravity = true;
            }

            if (stateTimer == charge) {
                slamFlash = 1f;
                ACMScreenShakeSystem.Add(9f);
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1"), NPC.Center);
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<BlackBearGroundShock>();
                    // 双向地面冲击波锥 (各两道不同速度, 玩家跳跃可躲)
                    foreach (int dir in new[] { -1, 1 }) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, new Vector2(dir * 9f, 0f), type, 40, 6f, Main.myPlayer, dir);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Bottom, new Vector2(dir * 13f, 0f), type, 40, 6f, Main.myPlayer, dir);
                    }
                }
            }

            if (stateTimer >= total)
                EndAttack();
        }

        // —— BearHug: 熊抱冲撞 (张臂蓄力 1s → 直线前扑) ——
        private void DoBearHug(Player player) {
            const int windup = 60;     // 1s 张臂蓄力 (锁方向)
            const int lungeEnd = 95;   // 前扑窗口
            const int total = 120;

            if (stateTimer < windup) {
                NPC.velocity.X *= 0.7f;
                FaceTarget(player);
                lungeDirX = NPC.direction;
                // 蓄力 dust
                if (!Main.dedServ && stateTimer % 4 == 0) {
                    Vector2 p = NPC.Center + new Vector2(lungeDirX * NPC.width * 0.4f, Main.rand.NextFloat(-40f, 40f));
                    Dust d = Dust.NewDustPerfect(p, DustID.GoldFlame, new Vector2(lungeDirX * 2f, 0f), 100, default, 1.3f);
                    d.noGravity = true;
                }
            }
            else if (stateTimer < lungeEnd) {
                // 前扑: 高速直线 + 接触伤害激活
                if (stateTimer == windup) {
                    SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1"), NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                }
                NPC.direction = NPC.spriteDirection = (int)lungeDirX;
                NPC.velocity.X = lungeDirX * 18f;
                NPC.damage = ContactDamage;
            }
            else {
                NPC.velocity.X *= 0.85f; // 收势
            }

            if (stateTimer >= total)
                EndAttack();
        }

        // —— Enrage: 半血相变咆哮 (短无敌, 不被秒过场) ——
        private void DoEnrage(Player player) {
            NPC.velocity.X *= 0.6f;
            const int total = 70;
            FaceTarget(player);

            if (stateTimer == 30) {
                slamFlash = 1f;
                ACMScreenShakeSystem.Add(8f);
            }

            if (stateTimer >= total) {
                NPC.dontTakeDamage = false;
                attackCooldown = 30;
                honeyTimer = 40;
                haloTimer = 90;
                EnterState(BearState.Idle);
            }
        }

        // ============================================================
        //  P2 狂怒环境灾害
        // ============================================================
        private void UpdateFuryHazards(Player player) {
            // 蜂蜜滴落: 落点地影预警的下落弹
            if (honeyTimer > 0)
                honeyTimer--;
            if (honeyTimer <= 0) {
                honeyTimer = 55;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<BlackBearHoneyDrip>();
                    int drips = Main.rand.Next(1, 3);
                    for (int i = 0; i < drips; i++) {
                        float targetX = player.Center.X + Main.rand.NextFloat(-360f, 360f);
                        Vector2 spawn = new Vector2(targetX, player.Center.Y - 620f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, new Vector2(0f, 0.1f), type, 28, 0f, Main.myPlayer);
                    }
                }
            }

            // 头饰光环: 环绕玩家 3s 后向内收拢 (复发, 非一次性)
            if (haloTimer > 0)
                haloTimer--;
            if (haloTimer <= 0) {
                haloTimer = 360;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<BlackBear_Proj3>();
                    const int orbs = 6;
                    for (int i = 0; i < orbs; i++) {
                        float ang = MathHelper.TwoPi * i / orbs;
                        // owner = 目标玩家索引, 供光环环绕该玩家 (MP 安全)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), player.Center, Vector2.Zero, type, 30, 0f, NPC.target, ang);
                    }
                }
            }
        }

        // ============================================================
        //  地面追击运动 (保留物理, 统一速度: P2 不加跑速)
        // ============================================================
        private void DoChaseMovement(Player player) {
            FaceTarget(player);

            float targetVX = NPC.direction * runSpeed;
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, targetVX, 0.09f);

            // 前方/底部方块检测 (平台跟随 + 卡墙起跳)
            int checkDistance = 2;
            int tileX = (int)((NPC.position.X + (NPC.direction == 1 ? NPC.width : 0)) / 16) + (NPC.direction == 1 ? checkDistance : -checkDistance);
            int tileY = (int)((NPC.position.Y + NPC.height) / 16);

            bool isOnPlatform = false;
            if (Main.tile[tileX, tileY] != null && Main.tile[tileX, tileY].HasTile && Main.tile[tileX, tileY].TileType == TileID.Platforms)
                isOnPlatform = true;

            // 玩家在下方且脚下是平台 → 下穿
            NPC.noTileCollide = false;
            if (Math.Abs(player.Center.Y - NPC.Center.Y) < 800 &&
                player.Center.Y > NPC.Center.Y + NPC.height / 2 && isOnPlatform) {
                NPC.velocity.Y += 0.1f;
                NPC.noTileCollide = true;
            }

            // 蜂蜜/水中按蜂蜜下落速率, 避免漂浮
            if (NPC.wet) {
                if (NPC.honeyWet) {
                    NPC.GravityMultiplier /= NPC.GravityWetMultipliers[LiquidID.Honey];
                    NPC.MaxFallSpeedMultiplier /= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey];
                }
                else if (!NPC.lavaWet && !NPC.shimmerWet) {
                    NPC.GravityMultiplier *= NPC.GravityWetMultipliers[LiquidID.Honey] / NPC.GravityWetMultipliers[LiquidID.Water];
                    NPC.MaxFallSpeedMultiplier *= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey] / NPC.MaxFallSpeedWetMultipliers[LiquidID.Water];
                }
            }

            // 卡墙 / 玩家在上方 → 起跳
            if (onGround) {
                if (NPC.collideX)
                    NPC.velocity.Y = -11f;
                if (player.Center.Y < NPC.Center.Y - 80f && Math.Abs(player.Center.X - NPC.Center.X) < 220f)
                    NPC.velocity.Y = -12f - MathHelper.Clamp((NPC.Center.Y - player.Center.Y) / 30f, 0f, 4f);
                // 防卡死: 原地不动则小跳
                if (NPC.position == NPC.oldPosition)
                    NPC.velocity.Y = -10f;
            }
            if (NPC.velocity.Y < -10f)
                NPC.noTileCollide = true;
        }

        // ============================================================
        //  生命周期辅助
        // ============================================================
        private void HandlePlayerDeadIdle() {
            NPC.noTileCollide = false;
            currentState = BearState.Idle;
            NPC.velocity.X = 0f;
            frameTimer++;
            if (frameTimer > 10) { frameTimer = 0; currentFrame++; }
            deathIdleTimer++;
            if (deathIdleTimer > 300) {
                NPC.alpha += 5;
                if (NPC.alpha >= 255)
                    NPC.active = false;
            }
        }

        private void HandleDeath(Player player) {
            NPC.damage = 0;
            if (dieTimer == 0) {
                currentFrame = 0;
                currentState = BearState.Die;
            }
            NPC.velocity.X = 0f;
            dieTimer++;
            int totalDie = 6;
            if (dieTimer > (totalDie * 13) * 2) {
                DownedBossSystem.downedBlackBear = true;
                SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar"), player.Center);
                NPC.NPCLoot();
                NPC.active = false;
            }
        }

        private void UpdateDespawnSight(Player player) {
            bool canSeePlayer = !player.dead && player.active &&
                Vector2.Distance(NPC.Center, player.Center) < 2000f &&
                Collision.CanHitLine(NPC.Center, 1, 1, player.Center, 1, 1);

            if (canSeePlayer) {
                lostSightTimer = 0;
            }
            else {
                lostSightTimer++;
                if (lostSightTimer >= LostSightThreshold) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC.life = 0;
                        NPC.HitEffect();
                        NPC.checkDead();
                        NPC.active = false;
                    }
                    else {
                        NPC.timeLeft = 0;
                    }
                }
            }
        }

        private void UpdateAnimation() {
            frameTimer++;
            bool isAttack = currentState == BearState.Attack_1 || currentState == BearState.Attack_2 ||
                            currentState == BearState.Attack_3 || currentState == BearState.BearHug;
            if ((currentState == BearState.Idle && frameTimer >= 12) ||
                (currentState == BearState.Enrage && frameTimer >= 8) ||
                (isAttack && frameTimer >= 6) ||
                (currentState == BearState.Run && frameTimer >= 8) ||
                (currentState == BearState.Die && frameTimer >= 10 && currentFrame < 5)) {
                frameTimer = 0;
                currentFrame++;
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (!isDying && NPC.life <= 0) {
                isDying = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.netUpdate = true;
                currentState = BearState.Die;
            }
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            // 冲撞/扑击中略减伤, 鼓励玩家躲招后再输出
            if (currentState == BearState.BearHug || currentState == BearState.Run)
                modifiers.FinalDamage *= 0.85f;
        }

        private void DespawnLogic() {
            NPC.velocity.X = 0f;
            NPC.velocity.Y -= 0.2f;
            if (NPC.timeLeft > 10)
                NPC.timeLeft = 10;
        }

        // ============================================================
        //  绘制 (含预警)
        // ============================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            DrawTelegraphs(spriteBatch);

            Texture2D texture;
            int frameHeight;
            int totalFrames;
            bool fury = Fury;

            switch (currentState) {
                case BearState.Die:
                    texture = dieTexture; frameHeight = 328; totalFrames = 6; break;
                case BearState.Attack_1:
                case BearState.BearHug:
                    texture = fury ? attackTexture_1_1 : attackTexture_1; frameHeight = 328; totalFrames = 10; break;
                case BearState.Attack_2:
                case BearState.Attack_3:
                    texture = fury ? attackTexture_2_2 : attackTexture_2; frameHeight = 328; totalFrames = 10; break;
                case BearState.Run:
                    texture = fury ? runTexture_1 : runTexture; frameHeight = 332; totalFrames = 6; break;
                default:
                    texture = fury ? idleTexture_1 : idleTexture; frameHeight = 344; totalFrames = 4; break;
            }

            int index = currentFrame % totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, index * frameHeight, texture.Width, frameHeight);

            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight);
            SpriteEffects effects = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 狂怒态: 暖橙发光描边 (轻量, 让新手一眼看出"它进阶了")
            if (fury) {
                float pulse = 0.35f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);
                Color glow = TelegraphColors.Flame * pulse;
                glow.A = 0;
                for (int i = 0; i < 4; i++) {
                    Vector2 off = (MathHelper.PiOver2 * i).ToRotationVector2() * 3f;
                    spriteBatch.Draw(texture, drawPos + off, sourceRectangle, glow, 0f, origin, 1f, effects, 0f);
                }
            }

            spriteBatch.Draw(texture, drawPos, sourceRectangle, drawColor * NPC.Opacity, 0f, origin, 1f, effects, 0f);
            return false;
        }

        private void DrawTelegraphs(SpriteBatch sb) {
            // 熊抱冲撞: 红色冲撞预警线 (蓄力期渐强)
            if (currentState == BearState.BearHug && stateTimer < 60) {
                float prog = MathHelper.Clamp(stateTimer / 60f, 0f, 1f);
                Vector2 end = NPC.Center + new Vector2(lungeDirX, 0f) * 700f;
                ACMShaders.DrawBeam(NPC.Center, end, 6f * (0.4f + 0.6f * prog),
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.35f, 0.25f + 0.55f * prog,
                    flowSpeed: 1.2f, flowScale: 3f, coreSharp: 3f);
            }

            // 立地震地: 裂地地纹 (红, 蓄力渐强) + 蓄力泛光
            if (currentState == BearState.Attack_3 && stateTimer < 120) {
                float prog = MathHelper.Clamp(stateTimer / 120f, 0f, 1f);
                DrawGroundDecal(sb, NPC.Bottom, 480f * (0.5f + 0.5f * prog), 0.55f * prog);
                if (prog > 0.15f)
                    ACMShaders.DrawRadialBloomAt(NPC.Center, 0.10f + 0.10f * prog, 0.35f * prog, TelegraphColors.Flame, rayCount: 8f);
            }

            // 砸地/震地释放泛光
            if (slamFlash > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Bottom, 0.22f, slamFlash, TelegraphColors.Flame, rayCount: 12f);

            // 半血相变: 裂地 + 泛光
            if (enrageFlash > 0.02f) {
                DrawGroundDecal(sb, NPC.Bottom, 520f, enrageFlash * 0.6f);
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.24f, enrageFlash, TelegraphColors.Flame, rayCount: 14f);
            }
        }

        // 地纹 (ArenaRunic) 落点圈 —— 缺着色器自动跳过 (只少一层装饰)
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
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Flame.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.Lethal.ToVector4());
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 5, 10));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SkyKey>(), 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearStaff>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearSword>(), 10, 1));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlackBearBow>(), 10, 1));
        }
    }
}

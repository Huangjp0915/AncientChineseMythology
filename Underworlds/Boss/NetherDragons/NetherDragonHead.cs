using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙头部 - 具备AI和攻击能力
    /// </summary>
    [AutoloadBossHead]
    public class NetherDragonHead : NetherDragon
    {
        public override WormType NPCWormType => WormType.Head;

        // AI状态
        private enum AIState
        {
            CircleAround,   // 环绕玩家
            Hover,          // 巡空
            Charge,         // 冲刺
            PortalTeleport, // 幽冥传送
            LaserSweep      // 激光横扫
        }

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        // 幽冥火喷射计时器
        private int flameTimer = 0;
        private const int FlameInterval = 90; // 每1.5秒喷一次火

        // 状态计时器
        private int stateTimer = 0;

        // 上一帧位置（用于绘制时的雾气效果）
        private Vector2 lastPosition = Vector2.Zero;

        // 传送门相关
        private int entrancePortalIndex = -1;
        private int exitPortalIndex = -1;
        private bool isTeleporting = false;

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<NetherDragonBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 50;
            NPC.height = 50;
            NPC.lifeMax = 120000;
            NPC.damage = 100;
            NPC.defense = 40;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);

            // 激活雾气系统
            if (Main.netMode != NetmodeID.Server) {
                NetherDragonFogSystem.Activate(NPC.whoAmI);
            }
        }

        public override void AI() {
            base.AI();
            UnderworldPlayer.UnderworldEffect = true;
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 初始化
            if (NPC.localAI[0] == 0f) {
                CurrentState = AIState.CircleAround;
                stateTimer = 300;
                flameTimer = FlameInterval;
                NPC.localAI[0] = 1f;
                lastPosition = NPC.Center;
            }

            // 幽冥火喷射
            if (--flameTimer <= 0) {
                flameTimer = FlameInterval;
                ShootNetherFlames();

                // 喷火时创建涟漪（表示能量爆发）
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.2f);
                }
            }

            // AI状态机
            stateTimer--;
            switch (CurrentState) {
                case AIState.CircleAround:
                    CircleAroundMovement();
                    if (stateTimer <= 0) {
                        // 随机选择下一个状态
                        int nextState = Main.rand.Next(3);
                        switch (nextState) {
                            case 0:
                                CurrentState = AIState.Hover;
                                stateTimer = 240;
                                break;
                            case 1:
                                CurrentState = AIState.PortalTeleport;
                                stateTimer = 180;
                                break;
                            case 2:
                                CurrentState = AIState.LaserSweep;
                                stateTimer = 300;
                                break;
                        }
                    }
                    break;

                case AIState.Hover:
                    HoverMovement();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.Charge;
                        stateTimer = 60;
                    }
                    break;

                case AIState.Charge:
                    ChargeMovement();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.CircleAround;
                        stateTimer = 300;
                    }
                    break;

                case AIState.PortalTeleport:
                    PortalTeleportAttack();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.CircleAround;
                        stateTimer = 240;
                    }
                    break;

                case AIState.LaserSweep:
                    LaserSweepAttack();
                    if (stateTimer <= 0) {
                        CurrentState = AIState.CircleAround;
                        stateTimer = 280;
                    }
                    break;
            }

            // 旋转和朝向
            NPC.rotation = NPC.velocity.ToRotation();
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            if (NPC.spriteDirection == -1)
                NPC.rotation += MathHelper.Pi;

            lastPosition = NPC.Center;
        }

        /// <summary>
        /// 环绕玩家移动
        /// </summary>
        private void CircleAroundMovement() {
            const float radius = 400f;
            const float speed = 0.05f;

            NPC.ai[1] += speed;
            if (NPC.ai[1] > MathHelper.TwoPi)
                NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 200f
            );

            Vector2 toTarget = targetPos - NPC.Center;
            const float inertia = 20f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 10f) / inertia;
        }

        /// <summary>
        /// 巡空移动
        /// </summary>
        private void HoverMovement() {
            const float hoverHeight = 350f;
            Vector2 targetPos = Target.Center - new Vector2(0, hoverHeight);

            // 随机左右飘移
            targetPos.X += MathF.Sin(NPC.ai[1] * 0.02f) * 200f;
            NPC.ai[1] += 1f;

            Vector2 toTarget = targetPos - NPC.Center;
            const float inertia = 30f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 15f) / inertia;
        }

        /// <summary>
        /// 冲刺攻击
        /// </summary>
        private void ChargeMovement() {
            if (stateTimer == 60) {
                // 冲刺开始，计算方向
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = toPlayer * 18f;

                // 冲刺开始时创建冲击涟漪（表示蓄力爆发）
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.8f);
                }
            }
            else if (stateTimer < 30) {
                // 减速
                NPC.velocity *= 0.95f;
            }
        }

        /// <summary>
        /// 幽冥传送
        /// </summary>
        private void PortalTeleportAttack() {
            if (stateTimer == 180) {
                // 创建入口传送门在Boss面前
                Vector2 portalOffset = NPC.velocity.SafeNormalize(Vector2.UnitY) * 150f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    entrancePortalIndex = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center + portalOffset,
                        Vector2.Zero,
                        ModContent.ProjectileType<NetherPortal>(),
                        0,
                        0f
                    );
                }

                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);

                // 开始减速准备进入传送门
                NPC.velocity *= 0.8f;
            }

            if (stateTimer < 180 && stateTimer > 150) {
                // 持续减速，产生蓄力感
                NPC.velocity *= 0.92f;

                // 蓄力粒子特效 - 被传送门吸引
                if (Main.rand.NextBool(2) && Main.netMode != NetmodeID.Server) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100f, 100f);
                    Vector2 toPortal = Vector2.Zero;

                    if (entrancePortalIndex >= 0 && entrancePortalIndex < Main.maxProjectiles) {
                        Projectile portal = Main.projectile[entrancePortalIndex];
                        if (portal.active) {
                            toPortal = (portal.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                        }
                    }

                    int dust = Dust.NewDust(dustPos, 1, 1, DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = toPortal;
                }
            }

            if (stateTimer == 150) {
                // 创建强烈的吸入音效
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen, NPC.Center);
            }

            if (stateTimer < 150 && stateTimer > 120) {
                // 加速冲向传送门
                if (entrancePortalIndex >= 0 && entrancePortalIndex < Main.maxProjectiles) {
                    Projectile portal = Main.projectile[entrancePortalIndex];
                    if (portal.active) {
                        Vector2 toPortal = (portal.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, toPortal * 15f, 0.15f);
                    }
                }

                // 身体段跟随加速
                NPC current = NPC;
                while (current.ai[2] > 0 && current.ai[2] < Main.maxNPCs) {
                    NPC segment = Main.npc[(int)current.ai[2]];
                    if (segment.active && segment.ModNPC is NetherDragon) {
                        segment.velocity = Vector2.Lerp(segment.velocity, current.velocity, 0.1f);
                    }
                    current = segment;
                }
            }

            if (stateTimer == 120) {
                // 确定出口位置 - 在玩家后方或侧方
                Vector2 playerDirection = Target.velocity.SafeNormalize(Vector2.UnitX * Target.direction);
                float angle = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                Vector2 teleportOffset = playerDirection.RotatedBy(angle + MathHelper.Pi) * Main.rand.Next(500, 700);
                Vector2 exitPosition = Target.Center + teleportOffset;

                // 创建出口传送门
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    exitPortalIndex = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        exitPosition,
                        Vector2.Zero,
                        ModContent.ProjectileType<NetherPortal>(),
                        0,
                        0f
                    );
                }

                // Boss和所有身体段一起传送
                TeleportWholeBody(exitPosition);

                // 震撼的传送音效
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Pitch = 0.3f,
                    Volume = 1.2f,
                    MaxInstances = 3
                }, exitPosition);

                // 创建大范围涟漪
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(exitPosition, 3f);

                    // 屏幕震动效果（如果玩家在附近）
                    if (Vector2.Distance(Target.Center, exitPosition) < 800f) {
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>()?.ShakeScreen(8f, 20);
                    }
                }
            }

            if (stateTimer == 119) {
                // 传送完成后的爆发特效
                for (int i = 0; i < 60; i++) {
                    Vector2 velocity = Main.rand.NextVector2CircularEdge(8f, 8f);
                    int dust = Dust.NewDust(NPC.Center, NPC.width, NPC.height, DustID.BlueTorch, 0, 0, 100, Color.Cyan, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = velocity;
                }

                // 沿着身体创建传送痕迹
                NPC current = NPC;
                int segmentCount = 0;
                while (current.ai[2] > 0 && current.ai[2] < Main.maxNPCs && segmentCount < 20) {
                    NPC segment = Main.npc[(int)current.ai[2]];
                    if (segment.active && segment.ModNPC is NetherDragon) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                            int dust = Dust.NewDust(segment.Center, segment.width, segment.height,
                                DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = velocity;
                        }
                        current = segment;
                        segmentCount++;
                    }
                    else break;
                }
            }

            if (stateTimer == 110) {
                // 从传送门猛烈冲出
                Vector2 chargeDirection = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = chargeDirection * 25f;

                // 冲刺音效
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.2f }, NPC.Center);

                // 冲刺涟漪
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 2.5f);
                }
            }

            if (stateTimer < 110 && stateTimer > 80) {
                // 保持高速追击，拖尾效果
                NPC.velocity *= 0.98f;

                // 强化拖尾粒子
                if (Main.rand.NextBool()) {
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height,
                        DustID.BlueTorch, 0, 0, 100, Color.Cyan, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -NPC.velocity * 0.3f;
                }
            }

            if (stateTimer == 80) {
                // 冲刺结束，急刹车
                NPC.velocity *= 0.6f;
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
            }

            if (stateTimer < 80 && stateTimer > 40) {
                // 平稳悬停
                Vector2 hoverTarget = Target.Center - new Vector2(0, 350f);
                Vector2 toTarget = hoverTarget - NPC.Center;
                NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.12f);
            }

            if (stateTimer == 40) {
                // 关闭传送门
                ClosePortals();
            }
        }

        /// <summary>
        /// 传送整个虫子身体
        /// </summary>
        private void TeleportWholeBody(Vector2 exitPosition) {
            // 计算整个虫子的长度和方向
            Vector2 bodyDirection = NPC.velocity.SafeNormalize(Vector2.UnitY);

            // 传送头部
            Vector2 headOffset = NPC.Center - exitPosition;
            NPC.Center = exitPosition;
            NPC.netUpdate = true;

            // 传送所有身体段，保持相对位置
            NPC current = NPC;
            int segmentIndex = 0;
            const int maxSegments = 100; // 防止无限循环

            while (current.ai[2] > 0 && current.ai[2] < Main.maxNPCs && segmentIndex < maxSegments) {
                NPC segment = Main.npc[(int)current.ai[2]];

                if (!segment.active || segment.ModNPC is not NetherDragon) {
                    break;
                }

                // 保持段之间的相对位置
                Vector2 oldOffset = segment.Center - (current.Center + headOffset);
                segment.Center = current.Center + oldOffset;
                segment.netUpdate = true;

                current = segment;
                segmentIndex++;
            }
        }

        /// <summary>
        /// 激光横扫攻击 - 上下游动发射横向激光
        /// </summary>
        private void LaserSweepAttack() {
            if (stateTimer == 300) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, NPC.Center);
            }

            // Boss做上下波浪移动
            float waveFrequency = 0.03f;

            if (stateTimer > 200) {
                // 移动到玩家侧面
                Vector2 targetPos = Target.Center + new Vector2(
                    Target.direction == 1 ? -500f : 500f,
                    MathF.Sin((300 - stateTimer) * waveFrequency) * 200f
                );

                Vector2 toTarget = targetPos - NPC.Center;
                NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.08f, 0.1f);
            }
            else {
                // 波浪游动
                float targetY = Target.Center.Y + MathF.Sin((300 - stateTimer) * waveFrequency * 2f) * 300f;
                Vector2 waveTarget = new Vector2(NPC.Center.X, targetY);

                NPC.velocity.Y = (waveTarget.Y - NPC.Center.Y) * 0.08f;
                NPC.velocity.X *= 0.95f;

                // 发射激光
                if ((stateTimer - 200) % 40 == 0 && stateTimer < 200) {
                    ShootLaser();
                }
            }

            // 旋转朝向
            if (NPC.velocity.Length() > 1f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }
        }

        /// <summary>
        /// 发射横向激光束
        /// </summary>
        private void ShootLaser() {
            if (!NPC.HasValidTarget || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = 50;
            if (Main.expertMode)
                damage = 75;
            if (Main.masterMode)
                damage = 95;

            // 向左右两侧发射激光
            float baseAngle = MathF.Sign(Target.Center.X - NPC.Center.X) > 0 ? 0f : MathHelper.Pi;

            for (int i = 0; i < 2; i++) {
                float angleOffset = (i == 0 ? -0.2f : 0.2f);
                float laserAngle = baseAngle + angleOffset;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<NetherLaserBeam>(),
                    damage,
                    0f,
                    ai0: laserAngle
                );
            }

            SoundEngine.PlaySound(SoundID.Item33, NPC.Center);

            // 发射时创建涟漪
            if (Main.netMode != NetmodeID.Server) {
                NetherDragonFogSystem.CreateRipple(NPC.Center, 1f);
            }
        }

        /// <summary>
        /// 关闭传送门
        /// </summary>
        private void ClosePortals() {
            if (entrancePortalIndex >= 0 && entrancePortalIndex < Main.maxProjectiles) {
                Projectile portal = Main.projectile[entrancePortalIndex];
                if (portal.active && portal.type == ModContent.ProjectileType<NetherPortal>()) {
                    (portal.ModProjectile as NetherPortal)?.StartClosing();
                }
            }

            if (exitPortalIndex >= 0 && exitPortalIndex < Main.maxProjectiles) {
                Projectile portal = Main.projectile[exitPortalIndex];
                if (portal.active && portal.type == ModContent.ProjectileType<NetherPortal>()) {
                    (portal.ModProjectile as NetherPortal)?.StartClosing();
                }
            }

            entrancePortalIndex = -1;
            exitPortalIndex = -1;
        }

        /// <summary>
        /// 喷射幽冥火
        /// </summary>
        private void ShootNetherFlames() {
            if (!NPC.HasValidTarget)
                return;

            int damage = 40;
            if (Main.expertMode)
                damage = 60;
            if (Main.masterMode)
                damage = 75;

            int count = 23;
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-5f, 5f));
                Vector2 direction = toPlayer.RotatedBy(angleOffset);
                float speed = 10f + Main.rand.NextFloat(-3f, 3f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 40f,
                    direction * speed,
                    ModContent.ProjectileType<NetherFlameProjectile>(),
                    damage,
                    0f
                );
            }

            SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
        }

        public override void OnKill() {
            base.OnKill();

            // 关闭所有传送门
            ClosePortals();

            // 死亡时停用雾气系统，创建爆炸涟漪
            if (Main.netMode != NetmodeID.Server) {
                // 死亡大爆炸涟漪
                for (int i = 0; i < 3; i++) {
                    float delay = i * 0.1f;
                    Vector2 ripplePos = NPC.Center + Main.rand.NextVector2Circular(50f, 50f);
                    NetherDragonFogSystem.CreateRipple(ripplePos, 2.5f - delay);
                }

                NetherDragonFogSystem.Deactivate();
            }

            // 触发幽冥矿生成
            NetherDragonDownedSystem.OnNetherDragonKilled();
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            // 根据雾气密度微调颜色（轻微效果）
            float fogDensity = 0f;
            if (Main.netMode != NetmodeID.Server && NetherDragonFogSystem.IsActive) {
                fogDensity = NetherDragonFogSystem.GetFogDensityAt(NPC.Center);
            }

            Color netherColor = Color.Lerp(drawColor, new Color(100, 150, 255), 0.5f);
            // 在浓雾中时颜色略微加深
            if (fogDensity > 0.6f) {
                netherColor = Color.Lerp(netherColor, new Color(80, 120, 200), fogDensity * 0.2f);
            }

            // 绘制拖尾
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                float fade = 0.3f * (1f - i / (float)NPC.oldPos.Length);
                spriteBatch.Draw(tex, pos, null, netherColor * fade, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale,
                    NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            }

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);

            return false;
        }
    }
}

using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙真身·头部 — 招式实现 (V3 重做)。
    /// 蛇形运动文法: 盘旋蓄势(anticipation) → 直线穿刺(burst) → 甩尾回旋(recovery)。
    /// 每招保证出口 (完成或超时); 预警遵守 TelegraphColors (致命红只在命中前出现)。
    /// </summary>
    public partial class AzureDragonHead
    {
        #region 入场演出 — 云中显形 → 破云 → 凝视

        private void RunIntro(Player target) {
            float t = MathHelper.Clamp(StateTimer / 140f, 0f, 1f);

            if (SubState == 0) {
                // 云层深处绕大圆巡游 (假 Z: 立方收敛, 结尾猛然贴近镜头)
                float z = t * t * t;
                VisualScale = MathHelper.Lerp(0.35f, 1f, z);
                VisualFade = MathHelper.Lerp(0.5f, 1f, z);

                orbitAngle += 0.024f * orbitDir;
                Vector2 ringPos = target.Center + new Vector2(0, -560f) + orbitAngle.ToRotationVector2() * 340f;
                SteerToward(ringPos, 22f, 0.08f);
                undulationAmpTarget = 14f;

                // 龙身周围云雾缭绕
                if (!VaultUtils.isServer && StateTimer % 5 == 0)
                    EmitMist(NPC.Center, 320f * VisualScale, 0.5f, 2.6f);

                // 远雷两声 (听觉铺垫)
                if (StateTimer == 30 || StateTimer == 90) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f, Volume = 0.5f }, NPC.Center);
                    SkyFlash = 0.3f;
                }

                if (StateTimer >= 140) {
                    // 破云: 天闪 + 吼叫 + 雾环炸开
                    SubState = 1;
                    AttackTimer = 0;
                    SkyFlash = 1f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.8f }, NPC.Center);
                    ACMUtils.AddScreenShake(12f);
                    InjectWhip(1f);
                    if (!VaultUtils.isServer) {
                        EmitMist(NPC.Center, 520f, 1f, 3.4f);
                        for (int i = 0; i < 50; i++) {
                            Vector2 vel = Main.rand.NextVector2CircularEdge(11f, 11f);
                            int d = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 80, default, 2.4f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                }
                return;
            }

            VisualScale = MathHelper.Lerp(VisualScale, 1f, 0.2f);
            VisualFade = MathHelper.Lerp(VisualFade, 1f, 0.2f);

            if (StateTimer < 200) {
                // 破云后骄傲的 8 字巡游 (行进的快移, 非瞬移)
                orbitAngle += 0.06f * orbitDir;
                Vector2 disp = target.Center + new Vector2(
                    MathF.Sin(orbitAngle) * 300f + orbitDir * 120f,
                    -250f + MathF.Sin(orbitAngle * 2f) * 90f);
                SteerToward(disp, 42f, 0.12f);
                undulationAmpTarget = 15f;
            }
            else {
                // 凝视静止 30f — 威压主要是静止
                NPC.velocity *= 0.9f;
                NPC.velocity += NPC.SafeDirectionTo(target.Center) * 0.25f;
                undulationAmpTarget = 12f;
            }

            if (StateTimer > 230)
                TransitionTo(AIState.Glide);
        }

        #endregion

        #region 游弋连接段 — 段落标点 + 保底喘息

        private void RunGlide(Player target) {
            int duration = IsPhase3 ? 38 : (IsPhase2 ? 45 : 55);

            float side = NPC.Center.X < target.Center.X ? -1f : 1f;
            Vector2 anchor = target.Center + new Vector2(side * 520f, -230f);

            float dist = Vector2.Distance(NPC.Center, target.Center);
            float maxSpeed = dist > 2200f ? 42f : 26f;   // 距离栓绳: 太远就快速归位
            SteerToward(anchor, maxSpeed, 0.09f);
            SerpentineSway(1.3f);
            undulationAmpTarget = 14f;

            bool arrived = Vector2.DistanceSquared(NPC.Center, anchor) < 150f * 150f;
            if (StateTimer >= duration || (StateTimer > 24 && arrived))
                TransitionTo(PickNextAttack());
        }

        #endregion

        #region 盘旋穿刺 — 盘(蓄) → 锁(反蓄) → 击(爆发) → 回(收) [P1] / 甩尾回旋 [P2]

        private void RunCoilPierce(Player target, bool chained) {
            if (StateTimer == 1)
                pierceCount = 0;

            int maxPierces = chained ? (Main.expertMode ? 4 : 3) : (Main.expertMode ? 3 : 2);
            float dashSpeed = chained ? (Main.expertMode ? 80f : 72f) : (Main.expertMode ? 70f : 62f);
            int coilTime = chained ? 26 : (pierceCount == 0 ? 50 : 30);
            int lockTime = chained ? 18 : 26;

            switch ((int)SubState) {
                case 0: { // 盘: 绕玩家收紧圆环, 蓄势可读
                    if (AttackTimer == 1) {
                        orbitAngle = (NPC.Center - target.Center).ToRotation();
                        Vector2 toPlayer = target.Center - NPC.Center;
                        float cross = NPC.velocity.X * toPlayer.Y - NPC.velocity.Y * toPlayer.X;
                        orbitDir = cross >= 0f ? 1 : -1;
                    }

                    float t = MathHelper.Clamp(AttackTimer / coilTime, 0f, 1f);
                    float radius = chained ? MathHelper.Lerp(360f, 280f, t) : MathHelper.Lerp(420f, 300f, t);
                    orbitAngle += orbitDir * MathHelper.Lerp(0.045f, 0.075f, t);
                    Vector2 ringPos = target.Center + orbitAngle.ToRotationVector2() * radius;
                    SteerToward(ringPos, 34f, 0.16f);
                    SerpentineSway(0.8f);
                    undulationAmpTarget = 13f;

                    // 口部电尘汇聚: 密度 ∝ √t, 75% 处静默截止 (蓄力语法)
                    if (!VaultUtils.isServer && t < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(t) * 0.7f) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(130f, 130f);
                        int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 100, default, 1.8f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 7f;
                    }
                    if (!VaultUtils.isServer && AttackTimer % 9 == 0)
                        EmitMist(target.Center, 420f, 0.22f * t, 1.8f);

                    // 玩家跑远: 中断回游弋 (防脱屏绕圈)
                    if (Vector2.DistanceSquared(NPC.Center, target.Center) > 1900f * 1900f) {
                        TransitionTo(AIState.Glide);
                        return;
                    }

                    if (AttackTimer >= coilTime) {
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 1: { // 锁: 硬刹 + 瞄准线 + 尾段后吸 (反向蓄势)
                    NPC.velocity *= 0.86f;
                    undulationAmpTarget = 5f;

                    if (AttackTimer == 4) {
                        // 最小发射距离阀门: 玩家钻进怀里 → 放弃这次穿刺 (奖励贴身冒险)
                        if (Vector2.DistanceSquared(NPC.Center, target.Center) < 260f * 260f) {
                            TransitionTo(AIState.Glide);
                            return;
                        }
                        chargeDirection = ACMUtils.LeadTarget(NPC.Center, target.Center, target.velocity, dashSpeed * 0.9f);
                        chargeTarget = target.Center;
                        NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.65f, Volume = 0.8f }, NPC.Center);
                    }

                    // 尾 8 帧沿瞄准线反向后吸 (pow 曲线: 静…静…猛地一吸)
                    if (AttackTimer > lockTime - 8) {
                        float rt = (AttackTimer - (lockTime - 8)) / 8f;
                        NPC.velocity -= chargeDirection * (rt * rt * 4.5f);
                    }

                    if (AttackTimer >= lockTime) {
                        NPC.velocity = chargeDirection * dashSpeed;
                        dashGlow = 1f;
                        InjectWhip(1f);
                        SubState = 2;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.55f, Volume = 0.9f }, NPC.Center);
                        ACMUtils.AddScreenShake(6f);
                    }
                    break;
                }
                case 2: { // 击: 一帧设速直线穿刺, 无转向 (直=快)
                    undulationAmpTarget = 2f;

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dp = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (30f + i * 26f)
                                + Main.rand.NextVector2Circular(24f, 24f);
                            int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 60, default, 2.4f);
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = -NPC.velocity * 0.08f;
                        }
                    }

                    float passDepth = Vector2.Dot(NPC.Center - chargeTarget, chargeDirection);
                    if (AttackTimer >= 26 || passDepth > (chained ? 640f : 500f)) {
                        SubState = 3;
                        AttackTimer = 0;
                        if (!chained)
                            InjectWhip(1f);
                    }
                    break;
                }
                case 3: {
                    if (chained)
                        RunLoopTurn(target, maxPierces);
                    else
                        RunPierceRecover(maxPierces);
                    break;
                }
            }
        }

        /// <summary>P1 收招: 硬刹车"撞进位置", 鞭波沿身传播。</summary>
        private void RunPierceRecover(int maxPierces) {
            undulationAmpTarget = 12f;
            if (NPC.velocity.Length() > 10f)
                NPC.velocity *= 0.68f;
            else
                NPC.velocity *= 0.96f;

            if (AttackTimer >= 28) {
                pierceCount++;
                if (pierceCount >= maxPierces) {
                    TransitionTo(AIState.Glide);
                }
                else {
                    SubState = 0;
                    AttackTimer = 0;
                }
            }
        }

        /// <summary>P2 甩尾回旋: 匀角速空中回环, 面向拦截点即早退 — 消灭"飞回来"死时间。</summary>
        private void RunLoopTurn(Player target, int maxPierces) {
            undulationAmpTarget = 8f;

            if (AttackTimer == 1) {
                Vector2 toPlayer = target.Center - NPC.Center;
                float cross = NPC.velocity.X * toPlayer.Y - NPC.velocity.Y * toPlayer.X;
                orbitDir = cross >= 0f ? 1 : -1;
                InjectWhip(0.7f);
            }

            float speed = MathF.Max(30f, NPC.velocity.Length() * 0.94f);
            NPC.velocity = NPC.velocity.RotatedBy(orbitDir * MathHelper.TwoPi / 34f).SafeNormalize(Vector2.UnitX) * speed;

            // 面向"预测拦截点" ±0.16rad 即出环
            Vector2 intercept = target.Center + target.velocity * 12f;
            float facingError = MathF.Abs(MathHelper.WrapAngle(NPC.velocity.ToRotation() - (intercept - NPC.Center).ToRotation()));
            if ((AttackTimer > 8 && facingError < 0.16f) || AttackTimer > 40) {
                pierceCount++;
                if (pierceCount >= maxPierces) {
                    TransitionTo(AIState.Glide);
                }
                else {
                    SubState = 1;
                    AttackTimer = 0;
                }
            }
        }

        #endregion

        #region 龙息扫射 — 扇形弹幕墙横扫

        private void RunBreathSweep(Player target) {
            switch ((int)SubState) {
                case 0: { // 占位: 飞至玩家侧上方
                    Vector2 anchor = target.Center + new Vector2(orbitDir * 520f, -180f);
                    SteerToward(anchor, 26f, 0.1f);
                    SerpentineSway(1f);
                    undulationAmpTarget = 13f;

                    bool arrived = Vector2.DistanceSquared(NPC.Center, anchor) < 90f * 90f;
                    if (AttackTimer >= 40 || (AttackTimer > 10 && arrived)) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                    }
                    break;
                }
                case 1: { // 蓄: 刹停 + 汇聚 + 后漂 (身体离开自己的武器)
                    NPC.velocity *= 0.88f;
                    undulationAmpTarget = 5f;

                    if (AttackTimer == 1) {
                        chargeDirection = NPC.SafeDirectionTo(target.Center, Vector2.UnitX);
                        NPC.netUpdate = true;
                    }
                    NPC.velocity -= chargeDirection * 0.8f;

                    float ct = AttackTimer / 32f;
                    if (!VaultUtils.isServer && ct < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(ct)) {
                        Vector2 mouth = NPC.Center + chargeDirection * 46f;
                        Vector2 dp = mouth + Main.rand.NextVector2CircularEdge(150f, 150f);
                        int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 90, default, 2f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = (mouth - dp) * 0.085f;
                    }

                    if (AttackTimer >= 32) {
                        SubState = 2;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.45f, Volume = 1.3f }, NPC.Center);
                        ACMUtils.AddScreenShake(5f);
                    }
                    break;
                }
                case 2: { // 扫: 扇面 -40°→+40° 匀速横扫, 每发后坐
                    undulationAmpTarget = 7f;
                    NPC.velocity *= 0.9f;

                    float sweepT = MathHelper.Clamp(AttackTimer / 76f, 0f, 1f);
                    float angle = chargeDirection.ToRotation() + orbitDir * MathHelper.Lerp(-0.7f, 0.7f, sweepT);
                    Vector2 aim = angle.ToRotationVector2();

                    int fireInterval = Main.expertMode ? 4 : 5;
                    if (AttackTimer % fireInterval == 0) {
                        NPC.velocity -= aim * 2.2f;   // 后坐: 每次喷吐把头顶回去
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                NPC.Center + aim * 46f, aim * 13.5f,
                                ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f,
                                ai0: 12f);
                        }
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 3; i++) {
                                Vector2 dv = aim.RotatedByRandom(0.35f) * Main.rand.NextFloat(5f, 11f);
                                int d = Dust.NewDust(NPC.Center + aim * 40f, 0, 0, DustID.BlueTorch, dv.X, dv.Y, 80, default, 2f);
                                Main.dust[d].noGravity = true;
                            }
                        }
                    }

                    if (AttackTimer >= 76) {
                        SubState = 3;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 3: { // 收
                    NPC.velocity *= 0.92f;
                    undulationAmpTarget = 12f;
                    if (AttackTimer >= 16)
                        TransitionTo(AIState.Glide);
                    break;
                }
            }
        }

        #endregion

        #region 雷柱阵 — 可读落雷 (V2 保留 + 重调)

        private void RunThunderRods(Player target) {
            switch ((int)SubState) {
                case 0: { // 升空蓄力
                    Vector2 hover = target.Center + new Vector2(0f, -430f);
                    SteerToward(hover, 28f, 0.1f);
                    SerpentineSway(0.9f);
                    undulationAmpTarget = 12f;

                    if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 150, default, 1.9f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = Main.rand.NextVector2Circular(3f, 3f);
                    }

                    if (AttackTimer >= 40) {
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(4f);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int rodCount = Main.expertMode ? 6 : 5;
                            float spacing = 230f;
                            float baseX = target.Center.X + Main.rand.NextFloat(-115f, 115f);
                            for (int i = 0; i < rodCount; i++) {
                                float x = baseX + (i - (rodCount - 1) / 2f) * spacing;
                                SpawnThunderRod(new Vector2(x, target.Center.Y), 80 + i * 7, 16);
                            }
                        }
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 1: { // 头顶缓弧巡游 (存在感), 等待落雷解算
                    orbitAngle += 0.03f * orbitDir;
                    Vector2 arc = target.Center + new Vector2(MathF.Sin(orbitAngle) * 520f, -430f + MathF.Cos(orbitAngle) * 60f);
                    SteerToward(arc, 20f, 0.07f);
                    SerpentineSway(1.1f);

                    if (AttackTimer >= 150)
                        TransitionTo(AIState.Glide);
                    break;
                }
            }
        }

        /// <summary>生成一根雷霆落雷柱 (服务器权威; angle=0 为竖直, PiOver2 为水平)。</summary>
        private void SpawnThunderRod(Vector2 strikePos, int telegraphTicks, int strikeActive, float angle = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(), strikePos, Vector2.Zero,
                ModContent.ProjectileType<AzureThunderRod>(), NPC.damage / 4, 3f,
                ai0: telegraphTicks, ai1: strikeActive, ai2: angle);
        }

        #endregion

        #region 换阶段演出

        private void RunTransition2(Player target) {
            if (StateTimer == 1) {
                ClearOwnProjectiles();
                attackIndexP2 = 0;
                chargeTarget = target.Center + new Vector2(0f, -360f);
                NPC.netUpdate = true;
            }

            undulationAmpTarget = 15f;
            if (StateTimer < 50) {
                SteerToward(chargeTarget, 30f, 0.12f);
            }
            else {
                // 盘成一圈聚雷
                orbitAngle += 0.085f * orbitDir;
                SteerToward(chargeTarget + orbitAngle.ToRotationVector2() * 250f, 30f, 0.18f);
            }

            if (!VaultUtils.isServer && StateTimer > 20 && Main.rand.NextBool(2)) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(240f, 240f);
                int dt = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                int d = Dust.NewDust(dp, 0, 0, dt, 0, 0, 90, default, 2.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 11f;
            }

            if (StateTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0f, Volume = 2f }, NPC.Center);
                SkyFlash = 1f;
                ACMUtils.AddScreenShake(12f);
                EmitMist(chargeTarget, 460f, 0.9f, 3.2f);
            }

            if (StateTimer == 96 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 解环新星: 8 发慢速雷弹, 弹速爬升 (换阶段公平阀门)
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 8.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 30f);
                }
            }

            if (StateTimer > 150)
                TransitionTo(AIState.Glide);
        }

        private void RunTransition3(Player target) {
            if (StateTimer == 1) {
                ClearOwnProjectiles();
                attackIndexP3 = 1;   // 开幕直接进审判庭, 循环指针跳过它
                ArenaCenter = target.Center;
                NPC.netUpdate = true;
            }

            SteerToward(ArenaCenter + new Vector2(0f, -620f), 34f, 0.1f);
            SerpentineSway(1.2f);
            undulationAmpTarget = 15f;

            if (StateTimer == 40 || StateTimer == 80) {
                SkyFlash = 1f;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 1.1f }, NPC.Center);
            }
            if (StateTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 2.2f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
                EmitMist(NPC.Center, 520f, 1f, 3f);
            }

            if (!VaultUtils.isServer && StateTimer > 40 && Main.rand.NextBool(2)) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(300f, 300f);
                int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 60, default, 2.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 13f;
            }

            if (StateTimer > 170)
                TransitionTo(AIState.P3_Tribunal);
        }

        #endregion

        #region 雷霆审判庭 — P3 招牌 set-piece (V2 保留 + 升级)

        private void RunTribunal(Player target) {
            if (StateTimer == 1) {
                ArenaCenter = target.Center;
                NPC.netUpdate = true;
            }

            // 庭上空高波动 8 字巡游 (存在感; 审判庭期间头部零弹幕 — 可读性阀门)
            orbitAngle += 0.035f * orbitDir;
            Vector2 anchor = ArenaCenter + new Vector2(MathF.Sin(orbitAngle) * 300f, -560f + MathF.Sin(orbitAngle * 2f) * 80f);
            SteerToward(anchor, 24f, 0.08f);
            SerpentineSway(1.4f);
            undulationAmpTarget = 15f;

            ApplyWindField();

            const int gridColumns = 11;
            const float colSpacing = 165f;
            int telegraph = Main.expertMode ? 70 : 85;
            const int strikeActive = 16;
            int wavePeriod = telegraph + strikeActive + 40;

            int wave = (int)SubState / 2;
            bool waiting = ((int)SubState & 1) == 1;

            if (!waiting) { // 蓄力 → 投放一波危险列
                if (AttackTimer >= 26) {
                    SkyFlash = 0.8f;
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                    ACMUtils.AddScreenShake(5f);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        SpawnTribunalWave(wave, gridColumns, colSpacing, telegraph, strikeActive);
                    SubState++;
                    AttackTimer = 0;
                }
            }
            else if (AttackTimer >= wavePeriod) {
                if (wave + 1 >= 3) {
                    // 波次限幅: 强制转入移动招式
                    TransitionTo(AIState.Glide);
                }
                else {
                    SubState++;
                    AttackTimer = 0;
                }
            }
        }

        /// <summary>投放一波审判庭危险列 (按波次切换图案: 横扫 / 梳齿 / 向心收束)。</summary>
        private void SpawnTribunalWave(int wave, int columns, float spacing, int telegraph, int strikeActive) {
            float originX = ArenaCenter.X - (columns - 1) / 2f * spacing;
            float strikeY = ArenaCenter.Y;
            int pattern = wave % 3;

            for (int c = 0; c < columns; c++) {
                float x = originX + c * spacing;
                int delay;
                bool strike;
                switch (pattern) {
                    case 0: // 横扫: 从一端逐列推进, 追着走位
                        strike = true;
                        delay = c * 9;
                        break;
                    case 1: // 梳齿: 奇偶两小波, 安全列在另一组
                        strike = true;
                        delay = (c % 2) * 40;
                        break;
                    default: // 向心收束: 两端向中央夹击, 正中央安全
                        strike = c != columns / 2;
                        delay = (int)(MathF.Abs(c - (columns - 1) / 2f) * -9 + (columns / 2) * 9);
                        break;
                }
                if (!strike)
                    continue;
                SpawnThunderRod(new Vector2(x, strikeY), telegraph + Math.Max(0, delay), strikeActive);
            }
        }

        /// <summary>风域: 周期性横向推动本地玩家 (MP 安全: 每端只推自己; 风线粒子提示方向)。</summary>
        private void ApplyWindField() {
            if (Main.dedServ)
                return;
            Player p = Main.LocalPlayer;
            if (!p.active || p.dead)
                return;
            if (Vector2.DistanceSquared(p.Center, ArenaCenter) > (ArenaRadius * 1.6f) * (ArenaRadius * 1.6f))
                return;

            float force = WindDir * 0.32f;
            p.velocity.X += force;

            if (Main.rand.NextBool(2)) {
                Vector2 dp = p.Center + new Vector2(Main.rand.NextFloat(-560f, 560f), Main.rand.NextFloat(-320f, 320f));
                int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, MathF.Sign(force) * 7f, 0, 130, default, 1.1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = new Vector2(MathF.Sign(force) * Main.rand.NextFloat(5f, 10f), 0f);
            }
        }

        #endregion

        #region 雷网律令 — 轴向弹幕墙 (天闪宣告)

        private void RunLightningLattice(Player target) {
            bool horizontal = latticeAxisCounter % 2 == 0;

            if (StateTimer == 1) {
                latticeAxisCounter++;
                horizontal = latticeAxisCounter % 2 == 0;
                SkyFlash = 1f;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);
                ACMUtils.AddScreenShake(4f);
                NPC.netUpdate = true;
            }

            // 龙沿网轴外侧缓慢巡游 (横排 → 侧方; 纵列 → 上方)
            Vector2 anchor = horizontal
                ? target.Center + new Vector2(720f * orbitDir, MathF.Sin(globalTime * 1.3f) * 140f)
                : target.Center + new Vector2(MathF.Sin(globalTime * 1.3f) * 260f, -560f);
            SteerToward(anchor, 22f, 0.08f);
            SerpentineSway(1.2f);
            undulationAmpTarget = 13f;

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                int lanes = Main.expertMode ? 7 : 6;
                const float laneSpacing = 190f;
                for (int i = 0; i < lanes; i++) {
                    float off = (i - (lanes - 1) / 2f) * laneSpacing;
                    int stagger = (i % 2) * 26;   // 奇偶两小波: 安全缝交替出现
                    if (horizontal)
                        SpawnThunderRod(new Vector2(target.Center.X, target.Center.Y + off), 55 + stagger, 12, MathHelper.PiOver2);
                    else
                        SpawnThunderRod(new Vector2(target.Center.X + off, target.Center.Y), 55 + stagger, 12);
                }
            }

            if (AttackTimer >= 115)
                TransitionTo(AIState.Glide);
        }

        #endregion

        #region 风暴合围 — 高速环布风暴雷珠

        private void RunStormRing(Player target) {
            switch ((int)SubState) {
                case 0: { // 逼近至环半径
                    Vector2 ringEntry = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX) * 640f;
                    SteerToward(ringEntry, 34f, 0.14f);
                    undulationAmpTarget = 12f;

                    if (AttackTimer >= 30) {
                        orbitAngle = (NPC.Center - target.Center).ToRotation();
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
                    }
                    break;
                }
                case 1: { // 环布: ~55px/f 高速画圆, 沿途匀布风暴雷珠
                    orbitAngle += orbitDir * MathHelper.TwoPi / 72f;
                    Vector2 ringPos = target.Center + orbitAngle.ToRotationVector2() * 640f;
                    SteerToward(ringPos, 60f, 0.28f);
                    undulationAmpTarget = 9f;
                    dashGlow = MathF.Max(dashGlow, 0.55f);   // 高速环布自带流光

                    int orbIndex = (int)(AttackTimer / 7.2f);
                    if (AttackTimer % 7 == 0 && orbIndex < 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<AzureStormOrb>(), NPC.damage / 5, 1f,
                            ai0: 46f + orbIndex * 6f);
                    }

                    if (AttackTimer >= 72) {
                        SubState = 2;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 2: { // 悬滞旁观 (雷珠解算, 玩家专注走位的喘息)
                    Vector2 hover = target.Center + new Vector2(0f, -520f);
                    SteerToward(hover, 18f, 0.06f);
                    SerpentineSway(1.2f);
                    undulationAmpTarget = 14f;

                    if (AttackTimer >= 120)
                        TransitionTo(AIState.Glide);
                    break;
                }
            }
        }

        #endregion

        #region 龙身放电 — 身体即竞技场 (小型 set-piece)

        private void RunBodyDischarge(Player target) {
            if (StateTimer == 1) {
                chargeTarget = target.Center;
                NPC.netUpdate = true;
            }

            switch ((int)SubState) {
                case 0: { // 盘场: 收拢成环, 电荷从尾推进到头 (全程可读)
                    float t = MathHelper.Clamp(AttackTimer / 70f, 0f, 1f);
                    orbitAngle += 0.052f * orbitDir;
                    Vector2 ringPos = chargeTarget + orbitAngle.ToRotationVector2() * 560f;
                    SteerToward(ringPos, 46f, 0.2f);
                    undulationAmpTarget = 9f;

                    chargeSweep = 1f - t;   // ribbon 电荷带: 尾(1) → 头(0)
                    chargeGlow = 0.4f + 0.6f * t;

                    if (!VaultUtils.isServer && AttackTimer % 4 == 0)
                        EmitMist(chargeTarget, 620f, 0.3f * t, 1.4f);

                    if (AttackTimer >= 70) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.1f, Volume = 1.2f }, NPC.Center);
                    }
                    break;
                }
                case 1: { // 三次放电脉冲: 白闪预警 20f → 间隔体节沿法线放弹
                    orbitAngle += 0.03f * orbitDir;
                    SteerToward(chargeTarget + orbitAngle.ToRotationVector2() * 560f, 34f, 0.15f);
                    undulationAmpTarget = 6f;
                    chargeSweep = 0.04f;
                    chargeGlow = 1f;

                    float local = (AttackTimer - 1) % 68;
                    int pulse = (int)((AttackTimer - 1) / 68);

                    if (pulse >= 3) {
                        SubState = 2;
                        AttackTimer = 0;
                        chargeSweep = -1f;
                        chargeGlow = 0f;
                        dischargeWarnOffset = -1;
                        break;
                    }

                    if (local == 1) {
                        dischargeWarnOffset = (pulse * 3) % 8;
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 0.8f }, chargeTarget);
                    }
                    if (local < 20)
                        dischargeWarn01 = local / 20f;

                    if (local == 20) {
                        ACMUtils.AddScreenShake(4f);
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 1f }, chargeTarget);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            FireDischargeSegments(dischargeWarnOffset);
                    }

                    // 公平阀门: 玩家离场则提前解环 (不围栏不硬留人)
                    if (Vector2.DistanceSquared(target.Center, chargeTarget) > 760f * 760f) {
                        SubState = 2;
                        AttackTimer = 0;
                        chargeSweep = -1f;
                        chargeGlow = 0f;
                        dischargeWarnOffset = -1;
                    }
                    break;
                }
                case 2: { // 解环
                    Vector2 tangent = (NPC.Center - chargeTarget).SafeNormalize(Vector2.UnitX).RotatedBy(orbitDir * MathHelper.PiOver2);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, tangent * 34f, 0.1f);
                    undulationAmpTarget = 13f;

                    if (AttackTimer >= 30)
                        TransitionTo(AIState.Glide);
                    break;
                }
            }
        }

        /// <summary>放电脉冲: 每第 8 节 (轮转起始) 沿自身法线向内外各放 1 发雷弹 (服务器权威)。</summary>
        private void FireDischargeSegments(int offset) {
            int bodyType = ModContent.NPCType<AzureDragonBody>();
            int dmg = Math.Max(1, NPC.damage / 5);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.type != bodyType || n.realLife != NPC.whoAmI)
                    continue;
                if (n.ModNPC is not AzureDragonBody body || body.SummonCount % 8 != offset)
                    continue;
                Vector2 perp = n.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                Projectile.NewProjectile(n.GetSource_FromAI(), n.Center, perp * 8.5f,
                    ModContent.ProjectileType<AzureBolt>(), dmg, 1f, ai0: 18f);
                Projectile.NewProjectile(n.GetSource_FromAI(), n.Center, -perp * 8.5f,
                    ModContent.ProjectileType<AzureBolt>(), dmg, 1f, ai0: 18f);
            }
        }

        #endregion

        #region 腾云俯冲 — 消失于风暴 → 天眼锁定 → 破云神龙

        private void RunSkyDive(Player target) {
            if (StateTimer == 1)
                diveCount = 0;

            switch ((int)SubState) {
                case 0: { // 腾云: 螺旋升天, 雾涡吞没
                    SteerToward(target.Center + new Vector2(MathF.Sin(globalTime * 3f) * 300f, -1500f), 46f, 0.06f);
                    undulationAmpTarget = 10f;
                    VisualFade = MathHelper.Lerp(VisualFade, 0f, 0.06f);
                    VisualScale = MathHelper.Lerp(VisualScale, 0.55f, 0.06f);

                    if (!VaultUtils.isServer && AttackTimer % 4 == 0)
                        EmitMist(NPC.Center, 380f, 0.7f, 3f);

                    if (AttackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.4f }, NPC.Center);

                    if (AttackTimer >= 55) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            NPC.Center = target.Center + new Vector2(0f, -1700f);
                            NPC.velocity = Vector2.Zero;
                            NPC.netUpdate = true;
                        }
                        clientScanX = target.Center.X;
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 1: { // 天眼锁定: 扫描线追踪 → 46f 锁死转红
                    NPC.velocity *= 0.9f;
                    VisualFade = MathHelper.Lerp(VisualFade, 0f, 0.3f);

                    clientScanX = MathHelper.Lerp(clientScanX, target.Center.X, 0.06f);
                    ACMUtils.AddScreenShake(MathHelper.Lerp(0.5f, 2.5f, AttackTimer / 66f));

                    if (AttackTimer == 46) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            chargeTarget = new Vector2(target.Center.X + target.velocity.X * 14f, target.Center.Y);
                            chargeDirection = Vector2.UnitY.RotatedBy(Main.rand.NextFloat(-0.21f, 0.21f));
                            NPC.netUpdate = true;
                        }
                        SkyFlash = 0.5f;
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.8f, Volume = 1f }, target.Center);
                    }

                    if (AttackTimer >= 66) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            NPC.Center = chargeTarget - chargeDirection * 1500f;
                            NPC.velocity = chargeDirection * 80f;
                            NPC.netUpdate = true;
                        }
                        VisualFade = 0.35f;
                        VisualScale = 0.5f;
                        dashGlow = 1f;
                        InjectWhip(1f);
                        SubState = 2;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f, Volume = 1.8f }, target.Center);
                        ACMUtils.AddScreenShake(7f);
                    }
                    break;
                }
                case 2: { // 神龙俯冲: 破云 + 缩放过冲 + 雷电走廊
                    undulationAmpTarget = 2f;
                    VisualFade = MathHelper.Lerp(VisualFade, 1f, 0.25f);
                    float ot = MathHelper.Clamp(AttackTimer / 14f, 0f, 1f);
                    VisualScale = 1f + 0.15f * MathF.Sin(ot * MathHelper.Pi);

                    if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perp = chargeDirection.RotatedBy(MathHelper.PiOver2);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * 9f,
                            ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 18f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -perp * 9f,
                            ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 18f);
                    }
                    if (!VaultUtils.isServer && AttackTimer % 3 == 0)
                        EmitMist(NPC.Center, 260f, 0.5f, 2.4f);

                    bool passed = Vector2.Dot(NPC.Center - chargeTarget, chargeDirection) > 230f;
                    if (passed || AttackTimer > 60) {
                        if (passed && Main.netMode != NetmodeID.MultiplayerClient) {
                            // 落点地面扇: 150° 反向张开
                            float baseAng = (-chargeDirection).ToRotation();
                            for (int i = 0; i < 8; i++) {
                                float ang = baseAng + MathHelper.Lerp(-1.3f, 1.3f, i / 7f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                    ang.ToRotationVector2() * 8.5f,
                                    ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 20f);
                            }
                        }
                        impactFlash = 1f;
                        ACMUtils.AddScreenShake(10f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                        EmitMist(NPC.Center, 480f, 1f, 3.4f);
                        NPC.velocity *= 0.45f;
                        SubState = 3;
                        AttackTimer = 0;
                    }
                    break;
                }
                case 3: { // 回正
                    NPC.velocity *= 0.85f;
                    NPC.velocity.Y -= 0.5f;
                    undulationAmpTarget = 12f;

                    if (AttackTimer >= 26) {
                        diveCount++;
                        if (diveCount >= 2) {
                            TransitionTo(AIState.Glide);
                        }
                        else {
                            SubState = 0;
                            AttackTimer = 0;
                        }
                    }
                    break;
                }
            }
        }

        #endregion

        #region 狂龙缠身 — 收紧螺旋 + 豁口新星

        private void RunFurySpiral(Player target) {
            if (StateTimer == 1)
                orbitAngle = (NPC.Center - target.Center).ToRotation();

            if (StateTimer <= 210) { // 收紧螺旋 (最小半径 240, 不贴脸)
                float t = StateTimer / 210f;
                float radius = MathHelper.Lerp(620f, 240f, ACMUtils.SineInOut(t));
                orbitAngle += (0.075f + 0.02f * t) * orbitDir;
                SteerToward(target.Center + orbitAngle.ToRotationVector2() * radius, 58f, 0.25f);
                SerpentineSway(0.6f);
                undulationAmpTarget = MathHelper.Lerp(13f, 6f, t);
                dashGlow = MathF.Max(dashGlow, 0.4f);

                // 只向外抛切向雷弹 — 环内保持清洁, 逃生通道 = 龙圈间隙本身
                if (StateTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 dirOut = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dirOut.RotatedBy(0.35f) * 8f,
                        ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 16f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dirOut.RotatedBy(-0.35f) * 8f,
                        ModContent.ProjectileType<AzureBolt>(), NPC.damage / 5, 1f, ai0: 16f);
                }
            }
            else if (StateTimer <= 234) { // 收束预警: 汇聚粒子 75% 截止 + 最后静默
                orbitAngle += 0.05f * orbitDir;
                SteerToward(target.Center + orbitAngle.ToRotationVector2() * 240f, 40f, 0.2f);
                undulationAmpTarget = 4f;

                float ct = (StateTimer - 210f) / 24f;
                if (StateTimer == 211)
                    SkyFlash = 0.3f;
                if (!VaultUtils.isServer && ct < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(ct)) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(170f, 170f);
                    int d = Dust.NewDust(dp, 0, 0, DustID.Electric, 0, 0, 60, default, 2f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = (NPC.Center - dp) * 0.085f;
                }

                if (StateTimer == 234) {
                    // 豁口新星: 14 向, 空出两个对置安全扇区; 弹速慢升给读弹时间
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int gap = Main.rand.Next(14);
                        for (int i = 0; i < 14; i++) {
                            if (i == gap || i == (gap + 7) % 14)
                                continue;
                            Vector2 vel = (MathHelper.TwoPi * i / 14f).ToRotationVector2() * 7.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<AzureBolt>(), NPC.damage / 4, 2f, ai0: 40f);
                        }
                    }
                    impactFlash = 0.6f;
                    ACMUtils.AddScreenShake(8f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.8f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.1f, Volume = 1.2f }, NPC.Center);
                }
            }
            else { // 解旋离场
                Vector2 tangent = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX).RotatedBy(orbitDir * MathHelper.PiOver2);
                NPC.velocity = Vector2.Lerp(NPC.velocity, tangent * 36f, 0.1f);
                undulationAmpTarget = 12f;

                if (StateTimer >= 260)
                    TransitionTo(AIState.Glide);
            }
        }

        #endregion

        #region 死亡演出 — 挣扎升天 → 力竭静默 → 坠落连环爆 → 雨霁

        // 体节爆点时刻 (加速节拍: 间隔 36,28,22,17,13,9)
        private static readonly int[] DeathPops = [66, 102, 130, 152, 169, 182, 191];

        private const int DeathPhaseFall = 216;
        private const int DeathFinale = 296;
        private const int DeathEnd = 330;

        private void RunDeathCinematic() {
            NPC.dontTakeDamage = true;
            undulationAmpTarget = MathHelper.Lerp(10f, 3f, MathHelper.Clamp(StateTimer / 190f, 0f, 1f));

            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 2f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
            }

            if (StateTimer < 60) { // 定身
                NPC.velocity *= 0.9f;
            }
            else if (StateTimer < 192) { // 挣扎升天: 无力的螺旋上升
                float bt = (StateTimer - 60f) / 132f;
                NPC.velocity.X = MathF.Sin(StateTimer * 0.06f) * MathHelper.Lerp(2f, 6f, bt);
                NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, -4.5f, 0.05f);

                // 加速爆点: 音调渐升, 体节从尾至头依次炸响
                for (int p = 0; p < DeathPops.Length; p++) {
                    if ((int)StateTimer == DeathPops[p]) {
                        float pitch = MathHelper.Lerp(-0.4f, 0.7f, p / (float)(DeathPops.Length - 1));
                        SoundEngine.PlaySound(SoundID.Item94 with { Pitch = pitch, Volume = 0.9f }, NPC.Center);
                        ACMUtils.AddScreenShake(3f);
                        PopSegmentVisual(SummonMax - p * 10 - 6);
                    }
                }

                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    int d = Dust.NewDust(NPC.Center + Main.rand.NextVector2Circular(60f, 60f), 0, 0,
                        DustID.Electric, 0, 0, 80, default, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }
            else if (StateTimer < DeathPhaseFall) { // 力竭静默 — 爆发前的收气
                NPC.velocity *= 0.85f;
            }
            else if (StateTimer < DeathFinale) { // 坠落 + 尾→头连环爆
                NPC.velocity.X *= 0.985f;
                NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.34f, 16f);

                if ((int)StateTimer % 4 == 0) {
                    float ft = (StateTimer - DeathPhaseFall) / (float)(DeathFinale - DeathPhaseFall);
                    PopSegmentVisual((int)((1f - ft) * SummonMax));
                    if ((int)StateTimer % 8 == 0)
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f, Volume = 0.6f }, NPC.Center);
                    ACMUtils.AddScreenShake(3f);
                }
            }
            else if (StateTimer < DeathEnd) { // 头部终爆 → 雨霁
                if ((int)StateTimer == DeathFinale) {
                    impactFlash = 1.2f;
                    SkyFlash = 1f;
                    ACMUtils.AddScreenShake(15f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                    EmitMist(NPC.Center, 620f, 1f, 3.8f);
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 80; i++) {
                            Vector2 vel = Main.rand.NextVector2CircularEdge(16f, 16f) * Main.rand.NextFloat(0.4f, 1f);
                            int dt = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                            int d = Dust.NewDust(NPC.Center, 0, 0, dt, vel.X, vel.Y, 30, default, Main.rand.NextFloat(2f, 3.6f));
                            Main.dust[d].noGravity = true;
                        }
                    }
                }
                NPC.velocity *= 0.9f;
            }
            else {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 演出完毕 → 真正死亡 (CheckDead 放行, 掉落与 downed 标记照常)
                    deathCinematicDone = true;
                    NPC.netUpdate = true;
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.checkDead();
                }
            }
        }

        /// <summary>体节爆裂的纯视觉新星 (按 SummonCount 定位体节)。</summary>
        private void PopSegmentVisual(int summonIndex) {
            if (VaultUtils.isServer)
                return;
            int bodyType = ModContent.NPCType<AzureDragonBody>();
            int tailType = ModContent.NPCType<AzureDragonTail>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || (n.type != bodyType && n.type != tailType) || n.realLife != NPC.whoAmI)
                    continue;
                if (n.ModNPC is not BasicWorm worm || Math.Abs(worm.SummonCount - summonIndex) > 1)
                    continue;
                for (int k = 0; k < 14; k++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(9f, 9f) * Main.rand.NextFloat(0.4f, 1f);
                    int dt = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                    int d = Dust.NewDust(n.Center, 0, 0, dt, vel.X, vel.Y, 40, default, Main.rand.NextFloat(1.6f, 2.8f));
                    Main.dust[d].noGravity = true;
                }
                break;
            }
        }

        #endregion
    }
}

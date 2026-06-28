using AncientChineseMythology.Underworlds.Boss.NetherDragons.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙头部 — V2《掘墓的冥龙》。三阶段 HP 门 + 全 telegraphed 攻击, 去除常驻喷火,
    /// 全部火焰只系于特定 telegraphed 状态(吐息锥 / 传送门后爆发 / 暴怒)。接线地府身份层
    /// (<see cref="UnderworldField"/>: 魂蚀 DoT + 怨念账)。
    ///
    /// 阶段 (按 life 比例确定, 无需额外同步):
    ///   ● P1 巡墓 (&gt;60%): 冥雾限视 + Hover 吐息锥; 龙身分段沿途留驻留幽火 DoT 残痕(可读空隙)。
    ///   ● P2 裂土 (60–30%): 传送门对成为核心 — 入口预告 2s, 出口落在玩家移动反向, 出口尾鞭甩出**一道**横扫魂束 + 一次 telegraphed 后爆发火幕。
    ///   ● P3 噬墓 (≤30%): 蜕 2–3 片怨念龙鳞绕玩家公转; 限时内击毁可阻止**暴怒**(暴怒 = 移动更快, 非喷火更密)。
    /// </summary>
    [AutoloadBossHead]
    public class NetherDragonHead : NetherDragon
    {
        public override WormType NPCWormType => WormType.Head;

        private enum AIState
        {
            CircleAround,
            HoverBreath,
            Charge,
            PortalTeleport
        }

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        private int stateTimer;
        private Vector2 lastPosition = Vector2.Zero;

        // —— 阶段 ——
        private int lastPhase = 0;
        private int phaseInvuln = 0;

        // —— 传送门 ——
        private int entrancePortalIndex = -1;
        private int exitPortalIndex = -1;
        private Vector2 exitPosition;

        // —— 吐息锥 telegraph ——
        private Vector2 breathDir = Vector2.UnitY;

        // —— 怨念账 / 暴怒 ——
        private int lastLife = -1;
        private int scaleState = 0;   // 0=待蜕 1=窗口期 2=冷却
        private int scaleTimer = 0;
        private int enrageTimer = 0;
        private int enrageBreathWindup = 0;
        private const int ScaleWindow = 360;
        private const int ScaleCooldown = 240;
        private const int EnrageDuration = 300;

        // —— 演出标量 (纯本地视觉) ——
        private float fogWarp = 0f;       // GenericWarp · fog (限视冥雾)
        private float riftWarp = 0f;      // GenericWarp · rift (传送门裂隙)
        private float breathBloom = 0f;   // RadialBloom (吐息泛光)
        private float runic = 0f;         // ArenaRunic (出口落点预警)
        private Vector2 runicCenter;
        private float runicRadius = 360f;
        private bool runicLethal = false;

        // ===== 阶段读取 (供龙身分段判断 P1 留痕) =====
        public int Phase {
            get {
                float r = NPC.lifeMax > 0 ? NPC.life / (float)NPC.lifeMax : 1f;
                if (r > 0.6f) return 1;
                if (r > 0.3f) return 2;
                return 3;
            }
        }

        private int FlameDamage => Main.masterMode ? 70 : (Main.expertMode ? 55 : 40);
        private int LaserDamage => Main.masterMode ? 95 : (Main.expertMode ? 75 : 50);

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
            UnderworldField.SetGrudgeMax(NPC, (int)(NPC.lifeMax * 0.7f));
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherDragonScale>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<NetherStaff>(),
                ModContent.ItemType<Netherlayer>(),
                ModContent.ItemType<Netherthrower>(),
                ModContent.ItemType<NetherSutom>()
            ));
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            base.OnSpawn(source);
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.Activate(NPC.whoAmI);
        }

        public override void AI() {
            base.AI();
            UnderworldPlayer.UnderworldEffect = true;
            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            if (NPC.localAI[0] == 0f) {
                CurrentState = AIState.CircleAround;
                stateTimer = 240;
                NPC.localAI[0] = 1f;
                lastPosition = NPC.Center;
                lastLife = NPC.life;
                lastPhase = 1;
            }

            // —— 怨念账: 按头部血量损失累计(段无关), 供 P3 蜕鳞规模 ——
            if (lastLife < 0) lastLife = NPC.life;
            int lost = lastLife - NPC.life;
            if (lost > 0) UnderworldField.AddGrudge(NPC, lost);
            lastLife = NPC.life;

            HandlePhaseTransition();

            // 演出标量目标
            float speedMul = enrageTimer > 0 ? 1.55f : 1f;
            UpdatePresentationTargets();

            // —— 主状态机 ——
            stateTimer--;
            switch (CurrentState) {
                case AIState.CircleAround: CircleAroundMovement(speedMul); AdvanceFromCircle(); break;
                case AIState.HoverBreath: HoverBreathState(speedMul); break;
                case AIState.Charge: ChargeState(speedMul); break;
                case AIState.PortalTeleport: PortalTeleportState(speedMul); break;
            }

            // —— P3 怨念龙鳞控制器 (独立于状态机, 每帧推进) ——
            if (Phase == 3)
                UpdateScaleController();
            else
                scaleState = 0;

            // —— 暴怒吐息 telegraph → 释放 ——
            if (enrageTimer > 0) enrageTimer--;
            if (enrageBreathWindup > 0) {
                enrageBreathWindup--;
                breathDir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                breathBloom = Math.Max(breathBloom, 1f - enrageBreathWindup / 45f);
                if (!Main.dedServ)
                    DrawConeTelegraph(NPC.Center, breathDir, 0.7f, 720f, TelegraphColors.Lethal, 6);
                if (enrageBreathWindup == 0) {
                    float grudge = UnderworldField.GetGrudgeNormalized(NPC);
                    BreathCone(breathDir, 9 + (int)(grudge * 6f), 0.7f + grudge * 0.25f, 11f, FlameDamage);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    ACMUtils.AddScreenShake(11f);
                    breathBloom = 1f;
                }
            }

            // 相变/蜕鳞短暂无敌 (传播到全段, 见 NetherDragon.AI); 期满务必清除
            NPC.dontTakeDamage = phaseInvuln > 0;
            if (phaseInvuln > 0)
                phaseInvuln--;

            // 朝向
            if (NPC.velocity.Length() > 0.5f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }

            lastPosition = NPC.Center;

            // 发布演出标量
            if (!Main.dedServ) {
                NetherDragonScreenSystem.Publish(breathBloom, NPC.Center,
                    runic, runicCenter, runicRadius, runicLethal, (float)Main.GlobalTimeWrappedHourly);
            }
        }

        // ============================================================
        //  阶段 / 演出
        // ============================================================

        private void HandlePhaseTransition() {
            int p = Phase;
            if (p == lastPhase)
                return;
            lastPhase = p;

            phaseInvuln = 45;            // 短暂无敌, 避免被秒过场 (§6.3)
            ACMUtils.AddScreenShake(9f);
            riftWarp = Math.Max(riftWarp, 0.6f);
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.CreateRipple(NPC.Center, 2.4f);
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);

            if (p == 2) {
                CurrentState = AIState.PortalTeleport;
                stateTimer = 260;
                ResetPortalState();
            }
            else if (p == 3) {
                // 进入噬墓: 立即蜕鳞
                scaleState = 0;
                scaleTimer = 0;
                CurrentState = AIState.CircleAround;
                stateTimer = 120;
            }
        }

        private void UpdatePresentationTargets() {
            // 冥雾限视 (P1 最浓), 全屏 fog 扭曲 (PostDraw 应用, 保守强度不破可读)
            float fogTarget = Phase == 1 ? 0.42f : (Phase == 2 ? 0.32f : 0.30f);
            fogWarp = MathHelper.Lerp(fogWarp, fogTarget, 0.02f);

            // 非持续标量自然衰减 (各状态会按需抬升)
            riftWarp = MathHelper.Lerp(riftWarp, 0f, 0.05f);
            breathBloom = MathHelper.Lerp(breathBloom, 0f, 0.08f);
            runic = MathHelper.Lerp(runic, 0f, 0.06f);
        }

        // ============================================================
        //  状态机
        // ============================================================

        private void AdvanceFromCircle() {
            if (stateTimer > 0)
                return;
            int p = Phase;
            if (p == 1) {
                // 巡墓: 吐息锥 / 冲刺 循环
                if (Main.rand.NextBool()) { CurrentState = AIState.HoverBreath; stateTimer = 210; }
                else { CurrentState = AIState.Charge; stateTimer = 70; }
            }
            else {
                // 裂土 / 噬墓: 传送门为核心, 偶插冲刺
                if (Main.rand.NextBool(3)) { CurrentState = AIState.Charge; stateTimer = 70; }
                else { CurrentState = AIState.PortalTeleport; stateTimer = 260; ResetPortalState(); }
            }
        }

        private void CircleAroundMovement(float speedMul) {
            const float radius = 400f;
            NPC.ai[1] += 0.05f * speedMul;
            if (NPC.ai[1] > MathHelper.TwoPi) NPC.ai[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.ai[1]) * radius,
                MathF.Sin(NPC.ai[1]) * radius * 0.6f - 200f);

            Vector2 toTarget = targetPos - NPC.Center;
            float inertia = 20f / speedMul;
            NPC.velocity = (NPC.velocity * (inertia - 1) + toTarget / 10f) / inertia;
        }

        /// <summary>P1 签名: telegraphed 吐息锥 (限视雾中的可读威胁)。</summary>
        private void HoverBreathState(float speedMul) {
            // 悬停到玩家上方并瞄准
            Vector2 hoverTarget = Target.Center - new Vector2(0, 340f);
            hoverTarget.X += MathF.Sin(stateTimer * 0.05f) * 160f;
            Vector2 toHover = hoverTarget - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.06f * speedMul, 0.1f);

            if (stateTimer > 110)
                breathDir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            // telegraph 窗口 [110..70]: 红色锥线渐强 + 蓄力泛光
            if (stateTimer <= 110 && stateTimer > 70) {
                breathBloom = Math.Max(breathBloom, (110 - stateTimer) / 40f * 0.8f);
                if (!Main.dedServ)
                    DrawConeTelegraph(NPC.Center, breathDir, 0.55f, 620f, TelegraphColors.Lethal, 4);
                if (stateTimer == 105)
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = 0.3f }, NPC.Center);
            }

            // 释放: 单次锥形吐息 (非常驻)
            if (stateTimer == 70) {
                BreathCone(breathDir, 7, 0.55f, 10.5f, FlameDamage);
                breathBloom = 1f;
                ACMUtils.AddScreenShake(7f);
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server)
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.4f);
            }

            if (stateTimer <= 0) { CurrentState = AIState.CircleAround; stateTimer = Phase == 1 ? 200 : 150; }
        }

        private void ChargeState(float speedMul) {
            if (stateTimer == 70) {
                Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = toPlayer * 18f * speedMul;
                breathDir = toPlayer;
                if (Main.netMode != NetmodeID.Server)
                    NetherDragonFogSystem.CreateRipple(NPC.Center, 1.8f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.2f }, NPC.Center);
            }
            else if (stateTimer > 40) {
                // 冲刺收尾 telegraph: 锥线预告(红)
                if (!Main.dedServ && stateTimer < 60)
                    DrawConeTelegraph(NPC.Center, NPC.velocity.SafeNormalize(breathDir), 0.4f, 460f, TelegraphColors.Lethal, 3);
            }
            else if (stateTimer == 40) {
                // 冲刺收尾的一次性锥形火幕 (telegraphed)
                Vector2 dir = NPC.velocity.SafeNormalize(breathDir);
                BreathCone(dir, 5, 0.4f, 10f, FlameDamage);
                breathBloom = Math.Max(breathBloom, 0.7f);
                ACMUtils.AddScreenShake(5f);
            }
            else {
                NPC.velocity *= 0.95f;
            }

            if (stateTimer <= 0) { CurrentState = AIState.CircleAround; stateTimer = Phase == 1 ? 220 : 150; }
        }

        /// <summary>P2 签名: 入口预告 2s → 出口落玩家移动反向 → 尾鞭横扫魂束 + 后爆发火幕 (rift-warp)。</summary>
        private void PortalTeleportState(float speedMul) {
            // [260..175] 入口预告 (~1.4s 减速被吸入) + riftWarp 渐强
            if (stateTimer == 260) {
                Vector2 portalOffset = NPC.velocity.SafeNormalize(Vector2.UnitY) * 150f;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    entrancePortalIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        NPC.Center + portalOffset, Vector2.Zero, ModContent.ProjectileType<NetherPortal>(), 0, 0f);
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
            }
            if (stateTimer <= 260 && stateTimer > 175) {
                NPC.velocity *= 0.9f;
                riftWarp = Math.Max(riftWarp, (260 - stateTimer) / 85f * 0.55f);
                if (entrancePortalIndex >= 0 && entrancePortalIndex < Main.maxProjectiles) {
                    Projectile portal = Main.projectile[entrancePortalIndex];
                    if (portal.active)
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (portal.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 6f, 0.08f);
                }
            }

            // [175] 决定出口: 玩家移动反向后方 (穿墓落点)
            if (stateTimer == 175) {
                Vector2 moveDir = Target.velocity.LengthSquared() > 4f
                    ? Target.velocity.SafeNormalize(Vector2.UnitX * Target.direction)
                    : new Vector2(Target.direction, 0f);
                exitPosition = Target.Center - moveDir * Main.rand.Next(420, 560) + new Vector2(0, Main.rand.Next(-120, -40));
                runicCenter = exitPosition;
                runicRadius = 320f;
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen, NPC.Center);
            }

            // [175..140] 出口落点预警 (向心收口; 最后 35t 切红致命)
            if (stateTimer <= 175 && stateTimer > 140) {
                runic = Math.Max(runic, 0.4f + (175 - stateTimer) / 35f * 0.6f);
                runicCenter = exitPosition;
                runicLethal = stateTimer < 158;
            }

            // [140] 整虫贯穿到出口
            if (stateTimer == 140) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    exitPortalIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        exitPosition, Vector2.Zero, ModContent.ProjectileType<NetherPortal>(), 0, 0f);
                TeleportWholeBody(exitPosition);
                riftWarp = 0.85f;
                ACMUtils.AddScreenShake(10f);
                if (Main.netMode != NetmodeID.Server) {
                    NetherDragonFogSystem.CreateRipple(exitPosition, 3f);
                    for (int i = 0; i < 50; i++) {
                        int d = Dust.NewDust(NPC.Center, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 110, new Color(110, 230, 150), 2.3f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = Main.rand.NextVector2CircularEdge(7f, 7f);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 1.2f }, exitPosition);
            }

            // [134] 出口尾鞭: 一道横扫魂束 (lateral) + telegraphed 后爆发火幕
            if (stateTimer == 134) {
                Vector2 chargeDir = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = chargeDir * 22f * speedMul;
                breathDir = chargeDir;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 后爆发火幕 (telegraphed by flame travel + bloom)
                    BreathCone(chargeDir, 6, 0.5f, 10.5f, FlameDamage);
                    // 一道横扫魂束: 朝玩家一侧的水平方向 (lateral), 自带红线 windup
                    float lateral = Target.Center.X >= NPC.Center.X ? 0f : MathHelper.Pi;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<NetherLaserBeam>(), LaserDamage, 0f, ai0: lateral);
                }
                breathBloom = Math.Max(breathBloom, 0.8f);
                ACMUtils.AddScreenShake(8f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.2f }, NPC.Center);
            }

            // [134..50] 突出 / 减速
            if (stateTimer < 134 && stateTimer > 50)
                NPC.velocity *= 0.985f;

            // [50] 关门收尾
            if (stateTimer == 50)
                ClosePortals();

            if (stateTimer <= 0) { CurrentState = AIState.CircleAround; stateTimer = 150; }
        }

        // ============================================================
        //  P3 怨念龙鳞 / 暴怒
        // ============================================================

        private void UpdateScaleController() {
            switch (scaleState) {
                case 0: // 蜕鳞
                    ShedScaleOrbs();
                    scaleState = 1;
                    scaleTimer = ScaleWindow;
                    break;
                case 1: // 窗口期: 玩家须击毁
                    scaleTimer--;
                    if (scaleTimer <= 0) {
                        int survivors = CountScaleOrbs();
                        if (survivors > 0) {
                            // 清账失败 → 暴怒 (移动更快 + 一道 telegraphed 暴怒吐息)
                            enrageTimer = EnrageDuration;
                            enrageBreathWindup = 45;
                            ACMUtils.AddScreenShake(9f);
                            SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.4f }, NPC.Center);
                        }
                        KillScaleOrbs();
                        scaleState = 2;
                        scaleTimer = ScaleCooldown;
                    }
                    break;
                case 2: // 冷却后重蜕
                    scaleTimer--;
                    if (scaleTimer <= 0)
                        scaleState = 0;
                    break;
            }
        }

        private void ShedScaleOrbs() {
            phaseInvuln = Math.Max(phaseInvuln, 20);
            riftWarp = Math.Max(riftWarp, 0.5f);
            ACMUtils.AddScreenShake(7f);
            if (Main.netMode != NetmodeID.Server)
                NetherDragonFogSystem.CreateRipple(NPC.Center, 2f);
            SoundEngine.PlaySound(SoundID.NPCDeath39 with { Pitch = -0.3f }, NPC.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
                return;

            float grudge = UnderworldField.GetGrudgeNormalized(NPC);
            int count = grudge > 0.5f ? 3 : 2;           // 怨念越重蜕越多
            int orbType = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(0.3f);
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, orbType,
                    0, NPC.whoAmI, angle, NPC.target);
                if (idx >= 0 && idx < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: idx);
            }
        }

        private int CountScaleOrbs() {
            int c = 0;
            int t = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == t && (int)n.ai[0] == NPC.whoAmI)
                    c++;
            }
            return c;
        }

        private void KillScaleOrbs() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int t = ModContent.NPCType<NetherScaleOrb>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == t && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        // ============================================================
        //  攻击 / 工具
        // ============================================================

        private void BreathCone(Vector2 dir, int count, float spreadRad, float speed, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.HasValidTarget)
                return;
            dir = dir.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float ang = MathHelper.Lerp(-spreadRad, spreadRad, t);
                Vector2 v = dir.RotatedBy(ang) * (speed + Main.rand.NextFloat(-1.2f, 1.2f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + dir * 40f, v,
                    ModContent.ProjectileType<NetherFlameProjectile>(), damage, 0f);
            }
            SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
        }

        private static void DrawConeTelegraph(Vector2 center, Vector2 dir, float spreadRad, float length, Color color, int density) {
            dir = dir.SafeNormalize(Vector2.UnitY);
            for (int e = -1; e <= 1; e += 2) {
                Vector2 edge = dir.RotatedBy(spreadRad * e);
                for (int i = 0; i < density; i++) {
                    float d = length * (i + 1) / density;
                    Vector2 p = center + edge * d;
                    var dust = Dust.NewDustPerfect(p, DustID.RedTorch, Vector2.Zero, 100, color, 1.1f);
                    dust.noGravity = true;
                }
            }
        }

        private void ResetPortalState() {
            entrancePortalIndex = -1;
            exitPortalIndex = -1;
            runicLethal = false;
        }

        private void TeleportWholeBody(Vector2 exitPos) {
            Vector2 headOffset = NPC.Center - exitPos;
            NPC.Center = exitPos;
            NPC.netUpdate = true;

            NPC current = NPC;
            int segmentIndex = 0;
            const int maxSegments = 100;
            while (current.ai[2] > 0 && current.ai[2] < Main.maxNPCs && segmentIndex < maxSegments) {
                NPC segment = Main.npc[(int)current.ai[2]];
                if (!segment.active || segment.ModNPC is not NetherDragon)
                    break;
                Vector2 oldOffset = segment.Center - (current.Center + headOffset);
                segment.Center = current.Center + oldOffset;
                segment.netUpdate = true;
                current = segment;
                segmentIndex++;
            }
        }

        private void ClosePortals() {
            if (entrancePortalIndex >= 0 && entrancePortalIndex < Main.maxProjectiles) {
                Projectile portal = Main.projectile[entrancePortalIndex];
                if (portal.active && portal.type == ModContent.ProjectileType<NetherPortal>())
                    (portal.ModProjectile as NetherPortal)?.StartClosing();
            }
            if (exitPortalIndex >= 0 && exitPortalIndex < Main.maxProjectiles) {
                Projectile portal = Main.projectile[exitPortalIndex];
                if (portal.active && portal.type == ModContent.ProjectileType<NetherPortal>())
                    (portal.ModProjectile as NetherPortal)?.StartClosing();
            }
            entrancePortalIndex = -1;
            exitPortalIndex = -1;
        }

        public override void OnKill() {
            base.OnKill();
            ClosePortals();
            KillScaleOrbs();

            if (Main.netMode != NetmodeID.Server) {
                // 死亡: 冥雾散尽的标点 (后续由 NetherDragonDownedSystem 矿脉显形收束)
                for (int i = 0; i < 3; i++)
                    NetherDragonFogSystem.CreateRipple(NPC.Center + Main.rand.NextVector2Circular(50f, 50f), 2.5f - i * 0.1f);
                NetherDragonFogSystem.Deactivate();
                ACMUtils.AddScreenShake(14f);
            }

            NetherDragonDownedSystem.OnNetherDragonKilled();
        }

        // ===== 全屏 screenTarget 扭曲 (GenericWarp · fog/rift) — 占唯一全屏名额 (§C.4#2) =====
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            // 传送门裂隙优先, 否则限视冥雾
            bool useRift = riftWarp > 0.04f;
            float intensity = useRift ? riftWarp : fogWarp;
            if (intensity <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            if (useRift) {
                fx.Parameters["uRadius"]?.SetValue(0.55f);
                fx.Parameters["uWarpScale"]?.SetValue(1.3f);
                fx.Parameters["uChroma"]?.SetValue(0.7f);
                fx.Parameters["uRadialPull"]?.SetValue(0.6f);   // 向心吸入 = 裂隙
                fx.Parameters["uMode"]?.SetValue(3f);           // rift
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.55f));
            }
            else {
                fx.Parameters["uRadius"]?.SetValue(1.0f);
                fx.Parameters["uWarpScale"]?.SetValue(0.8f);
                fx.Parameters["uChroma"]?.SetValue(0.25f);
                fx.Parameters["uRadialPull"]?.SetValue(0f);
                fx.Parameters["uMode"]?.SetValue(2f);           // fog
                fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.4f));
            }

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            float fogDensity = 0f;
            if (Main.netMode != NetmodeID.Server && NetherDragonFogSystem.IsActive)
                fogDensity = NetherDragonFogSystem.GetFogDensityAt(NPC.Center);

            // 幽蓝紫 → 暴怒/相变时偏鬼绿亮
            Color netherColor = Color.Lerp(drawColor, new Color(120, 90, 200), 0.5f);
            if (enrageTimer > 0 || phaseInvuln > 0)
                netherColor = Color.Lerp(netherColor, new Color(150, 255, 190), 0.4f);
            if (fogDensity > 0.6f)
                netherColor = Color.Lerp(netherColor, new Color(70, 50, 140), fogDensity * 0.2f);

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                float fade = 0.3f * (1f - i / (float)NPC.oldPos.Length);
                spriteBatch.Draw(tex, pos, null, netherColor * fade, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale,
                    NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            }

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, NPC.rotation + MathHelper.PiOver2, origin, NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);

            return false;
        }
    }
}

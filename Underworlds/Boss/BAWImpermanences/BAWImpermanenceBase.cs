using AncientChineseMythology.Systems;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常共享编排基类 —— "二重奏"双 Boss 战的指挥层 (V3 重做核心)。
    ///
    /// 编排原则 (PACING §5 多 Boss 分工):
    ///  - 任一时刻恒有一使缠斗 (striker, ai[3]=0)、一使控场 (support, ai[3]=1);
    ///  - 缠斗使每完成 2 招 → 指挥 (黑无常存活时恒为黑) 触发「阴阳易位」换岗对穿;
    ///  - P2 (任一 ≤60%) 后, 每第 2 次换岗改插协同技 (C1 阴阳勾魂分屏 / C2 勾魂链锁 轮替);
    ///  - 一方死亡且同伴 >30% → 引魂复活 (幸存者可被打的演出); 一方真死 → 幸存者「孤使怒」强化;
    ///  - 最后一使拥有完整死亡演出。
    ///
    /// ai 槽位: ai[0]=状态, ai[1]=状态计时器, ai[2]=子状态/段计数, ai[3]=岗位 (0 缠斗 / 1 控场)。
    /// 编排字段经 SendExtraAI 同步; 弹幕生成一律服务端判定; 状态切换 netUpdate。
    /// </summary>
    public abstract class BAWImpermanenceBase : ModNPC
    {
        #region 状态机与槽位

        public enum DuetState
        {
            Intro = 0,            // 入场演出
            Recompose = 1,        // 连接拍: 归位/选招/指挥调度
            Attack = 2,           // 出招 (currentAttack 指定具体招式)
            RoleSwap = 3,         // 阴阳易位换岗对穿
            P2Rite = 4,           // 阴阳易位·仪 (P2 换阶段演出)
            SynergyYinYang = 5,   // C1 阴阳勾魂 (分屏)
            SynergyChainLock = 6, // C2 勾魂链锁
            Reviving = 7,         // 为同伴引魂 (幸存者, 受伤 +30%)
            BeingRevived = 8,     // 化魂待引 (死者)
            SoloTransform = 9,    // 孤使怒形态切换
            DeathAnim = 10        // 死亡演出 (最后一使)
        }

        public DuetState State {
            get => (DuetState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float StateTimer => ref NPC.ai[1];
        public ref float SubState => ref NPC.ai[2];

        /// <summary>岗位: true=缠斗使 (贴身压迫), false=控场使 (远程布景)。</summary>
        public bool IsStriker => NPC.ai[3] < 0.5f;

        public const float Phase2Threshold = 0.60f;
        protected const int AttacksPerStint = 2;

        #endregion

        #region 编排字段 (SendExtraAI 同步)

        /// <summary>当前招式 id: 0/1=缠斗招, 2/3(/4)=控场招, 5=孤使专属。</summary>
        protected int currentAttack;
        /// <summary>本岗位循环内招式索引。</summary>
        protected int attackIndex;
        /// <summary>作为缠斗使已完成的招数 (换岗节拍)。</summary>
        protected int leadAttacksDone;
        /// <summary>指挥用: 距上次协同技的换岗次数。</summary>
        protected int swapsSinceSynergy;
        /// <summary>协同技轮替索引 (C1/C2)。</summary>
        protected int synergyIndex;
        /// <summary>已执行过 P2 仪式。</summary>
        protected bool didP2;
        /// <summary>已被引魂复活过一次。</summary>
        protected bool hasRespawned;
        /// <summary>孤使强化形态。</summary>
        public bool Unleashed { get; protected set; }
        /// <summary>死亡演出已完成 (CheckDead 放行真死)。</summary>
        protected bool deathAnimDone;
        /// <summary>亡魂位置: 引魂目标 / 孤使吞魂来源。</summary>
        protected Vector2 soulPos;
        /// <summary>同伴真死时留下的残魂位置 (孤使吞魂演出)。</summary>
        public Vector2 partnerSoulPos;

        // —— 演出/运动辅助 (无需精确同步, 服务器权威纠偏) ——
        protected Vector2 swapDest;      // 换岗目的地 / 仪式锚点
        protected float synAngle0;       // 协同技对峙基准角
        protected Vector2 lockCenter;    // C2 公转中心 (软追踪玩家)

        #endregion

        #region 视觉字段 (纯客户端)

        /// <summary>本体实体度 (0=魂 1=实体), 驱动 DissolveBurn。</summary>
        protected float drawAlpha = 1f;
        /// <summary>全屏白闪 (冲击帧/交错闪光), 每帧 ×0.86 衰减。</summary>
        protected float whiteFlash;
        /// <summary>体表魂焰罩强度。</summary>
        protected float auraIntensity = 0.55f;
        /// <summary>死亡演出魂焰柱强度。</summary>
        protected float soulPillar;
        /// <summary>阴阳分屏包络 (BAWFX 读取, 双使取 max)。</summary>
        public float SplitDriveTarget { get; protected set; }

        #endregion

        #region 兼容保留的公开成员

        public Player Target => Main.player[NPC.target];
        public BAWPlayer ScreenPlayer => Target?.GetModPlayer<BAWPlayer>();

        /// <summary>同伴 NPC 索引。</summary>
        public int PartnerIndex { get; set; } = -1;

        /// <summary>同伴 NPC 引用 (校验类型, 防 NPC 槽位复用错认)。</summary>
        public NPC Partner => PartnerIndex >= 0 && PartnerIndex < Main.npc.Length && Main.npc[PartnerIndex].active &&
            Main.npc[PartnerIndex].type == PartnerType ? Main.npc[PartnerIndex] : null;

        /// <summary>是否处于协同攻击状态 (兼容旧 API; 亦供演出层读取)。</summary>
        public bool InSynergyAttack { get; set; }

        /// <summary>重置攻击计时 (兼容旧 API)。</summary>
        public void ResetAI() {
            NPC.ai[1] = 0;
            NPC.ai[2] = 0;
        }

        #endregion

        #region 子类契约

        /// <summary>指挥优先级: 黑无常 true (存活时恒为指挥)。</summary>
        protected abstract bool ConductorPriority { get; }
        /// <summary>习惯侧 (入场/站位): 黑=-1 白=+1。</summary>
        protected abstract int SideSign { get; }
        /// <summary>孤使播报本地化键。</summary>
        protected abstract string SoloAnnounceKey { get; }
        /// <summary>同伴的 NPC 类型。</summary>
        protected abstract int PartnerType { get; }
        /// <summary>阴阳勾魂里本使的压力节拍间隔 (帧)。</summary>
        protected abstract int YinYangPressureInterval { get; }

        /// <summary>执行具体招式 (id 见 <see cref="currentAttack"/>)。完成时调用 <see cref="EndAttack"/>。</summary>
        protected abstract void RunAttack(int id, Player target);
        /// <summary>阴阳勾魂: 在本使侧生成一拍压力 (服务端调用)。</summary>
        protected abstract void SpawnYinYangPressure(Player target, Vector2 mid, Vector2 tangent, Vector2 myNormal, int beat);
        /// <summary>阴阳勾魂: 每帧域内维护 (如清除侵入安全缝的自家弹)。</summary>
        protected virtual void MaintainYinYang(Vector2 mid, Vector2 myNormal) { }
        /// <summary>每帧收尾视觉 (体表垂饰物理/朝向/呼吸)。</summary>
        protected virtual void PostAIVisuals(Player target) { }
        /// <summary>控场循环选招 (id 2/3, 白 P2 加 4)。</summary>
        protected virtual int PickSupportAttack(int idx) => 2 + idx % 2;
        /// <summary>孤使循环选招。</summary>
        protected virtual int PickSoloAttack(int idx) {
            return (idx % 4) switch {
                0 => 0,
                1 => 3,
                2 => 1,
                _ => 5
            };
        }

        #endregion

        #region 同伴与指挥

        protected BAWImpermanenceBase PartnerBoss => Partner?.ModNPC as BAWImpermanenceBase;

        /// <summary>同伴已彻底离场 (真死/不存在)。</summary>
        protected bool PartnerGone => Partner == null;

        /// <summary>指挥: 黑无常存活时为黑, 否则幸存者。</summary>
        public bool IsConductor => PartnerGone || ConductorPriority;

        /// <summary>可被指挥打断 (处于连接拍)。</summary>
        public bool Interruptible => State == DuetState.Recompose;

        protected void RefreshPartner() {
            if (Partner != null)
                return;
            PartnerIndex = -1;
            // 节流扫描: 同伴阵亡后无需每帧扫全表
            if (Main.GameUpdateCount % 30 != 0 && State != DuetState.Intro)
                return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n != null && n.active && n.type == PartnerType) {
                    PartnerIndex = i;
                    if (n.ModNPC is BAWImpermanenceBase twin)
                        twin.PartnerIndex = NPC.whoAmI;
                    break;
                }
            }
        }

        protected static float LifeFrac(NPC npc) => npc.lifeMax > 0 ? npc.life / (float)npc.lifeMax : 0f;

        #endregion

        #region 声音

        protected static readonly SoundStyle RoarSound = SoundID.Roar with { PitchVariance = 0.2f };
        protected static readonly SoundStyle ChainSound = SoundID.Item20 with { Volume = 0.7f };
        protected static readonly SoundStyle DashSound = SoundID.DD2_EtherianPortalDryadTouch with { Volume = 0.9f };
        protected static readonly SoundStyle ChargeSound = SoundID.ForceRoar with { Volume = 0.8f, PitchVariance = 0.3f };
        protected static readonly SoundStyle BeepSound = SoundID.MaxMana with { Volume = 0.9f, Pitch = -0.4f };

        #endregion

        #region 生命周期

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            RefreshPartner();
            State = DuetState.Intro;
            StateTimer = 0;
            SubState = 0;
            NPC.ai[3] = ConductorPriority ? 0 : 1; // 黑先缠斗, 白先控场
            NPC.dontTakeDamage = true;
            drawAlpha = 0f;

            // 入场落位: 玩家侧上方 (服务端权威)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.TargetClosest();
                if (NPC.target >= 0 && NPC.target < Main.maxPlayers && Main.player[NPC.target].active)
                    NPC.Center = Main.player[NPC.target].Center + new Vector2(SideSign * 380f, -280f);
                NPC.netUpdate = true;
            }

            // 地府身份层: 怨念账 (玩家造业 → 分屏强度上探)
            UnderworldField.SetGrudgeMax(NPC, 100);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)currentAttack);
            writer.Write((byte)attackIndex);
            writer.Write((byte)leadAttacksDone);
            writer.Write((byte)swapsSinceSynergy);
            writer.Write((byte)synergyIndex);
            writer.Write(didP2);
            writer.Write(hasRespawned);
            writer.Write(Unleashed);
            writer.Write(deathAnimDone);
            writer.WriteVector2(soulPos);
            writer.WriteVector2(partnerSoulPos);
            writer.WriteVector2(swapDest);
            writer.Write(synAngle0);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            currentAttack = reader.ReadByte();
            attackIndex = reader.ReadByte();
            leadAttacksDone = reader.ReadByte();
            swapsSinceSynergy = reader.ReadByte();
            synergyIndex = reader.ReadByte();
            didP2 = reader.ReadBoolean();
            hasRespawned = reader.ReadBoolean();
            Unleashed = reader.ReadBoolean();
            deathAnimDone = reader.ReadBoolean();
            soulPos = reader.ReadVector2();
            partnerSoulPos = reader.ReadVector2();
            swapDest = reader.ReadVector2();
            synAngle0 = reader.ReadSingle();
        }

        #endregion

        #region 主循环

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;

            if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }

            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) {
                // 无目标: 上飘离场
                NPC.velocity.Y -= 0.4f;
                NPC.velocity.X *= 0.97f;
                NPC.EncourageDespawn(30);
                return;
            }

            RefreshPartner();
            StateTimer++;

            switch (State) {
                case DuetState.Intro: RunIntro(target); break;
                case DuetState.Recompose: RunRecompose(target); break;
                case DuetState.Attack: RunAttack(currentAttack, target); break;
                case DuetState.RoleSwap: RunRoleSwap(target); break;
                case DuetState.P2Rite: RunP2Rite(target); break;
                case DuetState.SynergyYinYang: RunSynergyYinYang(target); break;
                case DuetState.SynergyChainLock: RunSynergyChainLock(target); break;
                case DuetState.Reviving: RunReviving(target); break;
                case DuetState.BeingRevived: RunBeingRevived(target); break;
                case DuetState.SoloTransform: RunSoloTransform(target); break;
                case DuetState.DeathAnim: RunDeathAnim(target); break;
            }

            SplitDriveTarget = ComputeSplitEnvelope();
            whiteFlash *= 0.86f;
            PostAIVisuals(target);
        }

        /// <summary>切换状态并复位计时器 (netUpdate 同步)。</summary>
        public void SwitchState(DuetState s, float role = -1f) {
            State = s;
            StateTimer = 0;
            SubState = 0;
            if (role >= 0f)
                NPC.ai[3] = role;
            InSynergyAttack = s == DuetState.SynergyYinYang || s == DuetState.SynergyChainLock;
            NPC.netUpdate = true;
        }

        private void CommandBoth(DuetState s) {
            SwitchState(s);
            PartnerBoss?.SwitchState(s);
        }

        #endregion

        #region 通用运动

        /// <summary>平滑飞向目标点 (近处减速, 远处不超过 topSpeed)。</summary>
        protected void SmoothFly(Vector2 dest, float topSpeed, float inertia) {
            Vector2 to = dest - NPC.Center;
            float dist = to.Length();
            float speed = MathF.Min(topSpeed, dist * 0.08f + 2f);
            Vector2 want = to.SafeNormalize(Vector2.Zero) * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, want, inertia);
        }

        /// <summary>距离栓绳: 离目标过远时强力拉回 (防脱屏绕圈)。</summary>
        protected void Leash(Player target) {
            if (NPC.Distance(target.Center) > 2400f)
                SmoothFly(target.Center + new Vector2(0f, -300f), 36f, 0.1f);
        }

        protected void FaceTarget(Player target) {
            NPC.direction = NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
        }

        /// <summary>缠斗站位: 玩家近旁侧上方。</summary>
        protected Vector2 StrikerStance(Player t) {
            float side = NPC.Center.X >= t.Center.X ? 1f : -1f;
            return t.Center + new Vector2(420f * side, -140f);
        }

        /// <summary>控场站位: 习惯侧远处高位。</summary>
        protected Vector2 SupportStance(Player t) => t.Center + new Vector2(640f * SideSign, -360f);

        #endregion

        #region 入场

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            drawAlpha = MathF.Min(1f, StateTimer / 110f);

            if (StateTimer < 110f) {
                // 魂凝: 收束粒子 (charge-up 语汇: 密度递增, 尾段安静)
                NPC.velocity = new Vector2(0f, -0.35f);
                if (!Main.dedServ && StateTimer < 85f && Main.rand.NextBool(2)) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(220f, 220f);
                    var d = Dust.NewDustPerfect(from, ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = (NPC.Center - from) * 0.075f;
                }
            }
            else if (StateTimer < 165f) {
                // 全静止对视 —— 威压来自静止本身
                NPC.velocity *= 0.82f;
                FaceTarget(target);
            }
            else if (StateTimer == 165f) {
                if (IsConductor) {
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ACMScreenShakeSystem.Add(9f);
                }
                whiteFlash = 0.3f;
            }
            else if (StateTimer >= 200f) {
                NPC.dontTakeDamage = false;
                if (PartnerGone && !Unleashed) {
                    // 独自被召唤: 直接孤使化 (无同伴可协奏)
                    Unleashed = true;
                    NPC.damage = (int)(NPC.damage * 1.15f);
                    Announce(SoloAnnounceKey, ConductorPriority ? BAWFX.YinColor : BAWFX.YangColor);
                }
                SwitchState(DuetState.Recompose);
            }

            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.08f);
        }

        #endregion

        #region 连接拍与指挥调度

        private void RunRecompose(Player target) {
            Vector2 stance = (IsStriker || Unleashed) ? StrikerStance(target) : SupportStance(target);
            SmoothFly(stance, IsStriker ? 16f : 12f, 0.06f);
            Leash(target);
            FaceTarget(target);
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.03f, -0.25f, 0.25f), 0.08f);

            // 孤使检测: 同伴真死 → 吞魂强化
            if (!Unleashed && PartnerGone && StateTimer > 10f) {
                if (partnerSoulPos == Vector2.Zero)
                    partnerSoulPos = NPC.Center + new Vector2(0f, -160f);
                SwitchState(DuetState.SoloTransform);
                return;
            }

            // 指挥调度 (换岗 / P2 仪式 / 协同)
            if (IsConductor && !Unleashed) {
                RunConductor();
                if (State != DuetState.Recompose)
                    return; // 已被调度进演出/换岗
            }

            // 出招节拍: 缠斗使干脆, 控场使有呼吸间隙
            float readyTime = (IsStriker || Unleashed) ? 26f : 84f;
            bool holdForSwap = IsStriker && !Unleashed && leadAttacksDone >= AttacksPerStint;

            if (holdForSwap) {
                // 等待指挥换岗; 超时保底继续输出 (防死锁; 上限 > 最长控场招周期)
                if (StateTimer > 170f) {
                    leadAttacksDone = 0;
                    StartNextAttack();
                }
                return;
            }

            if (StateTimer >= readyTime)
                StartNextAttack();
        }

        private void RunConductor() {
            BAWImpermanenceBase pb = PartnerBoss;
            if (pb == null)
                return;

            if (!Interruptible || !pb.Interruptible)
                return;

            // P2 仪式 (一次性)
            if (!didP2 && (LifeFrac(NPC) <= Phase2Threshold || LifeFrac(pb.NPC) <= Phase2Threshold)) {
                CommandBoth(DuetState.P2Rite);
                return;
            }

            // 换岗节拍: 缠斗使完成一轮
            BAWImpermanenceBase leader = IsStriker ? this : pb;
            if (leader.leadAttacksDone < AttacksPerStint)
                return;

            leadAttacksDone = 0;
            pb.leadAttacksDone = 0;

            bool synergyReady = didP2 &&
                LifeFrac(NPC) <= Phase2Threshold && LifeFrac(pb.NPC) <= Phase2Threshold &&
                swapsSinceSynergy >= 2;

            if (synergyReady) {
                swapsSinceSynergy = 0;
                CommandBoth(synergyIndex % 2 == 0 ? DuetState.SynergyYinYang : DuetState.SynergyChainLock);
                synergyIndex++;
            }
            else {
                swapsSinceSynergy++;
                CommandBoth(DuetState.RoleSwap);
            }
        }

        private void StartNextAttack() {
            if (Unleashed)
                currentAttack = PickSoloAttack(attackIndex);
            else if (IsStriker)
                currentAttack = attackIndex % 2;
            else
                currentAttack = PickSupportAttack(attackIndex);
            attackIndex++;
            SwitchState(DuetState.Attack);
        }

        /// <summary>招式收尾: 计入换岗节拍并回连接拍。子类招式完成时调用。</summary>
        protected void EndAttack() {
            if (IsStriker && !Unleashed)
                leadAttacksDone++;
            SwitchState(DuetState.Recompose);
        }

        #endregion

        #region 阴阳易位 (换岗对穿)

        private void RunRoleSwap(Player target) {
            BAWImpermanenceBase pb = PartnerBoss;

            if (StateTimer == 1f) {
                swapDest = pb != null ? pb.NPC.Center : StrikerStance(target);
                SoundEngine.PlaySound(BeepSound, NPC.Center);
                NPC.netUpdate = true;
            }

            Vector2 dir = (swapDest - NPC.Center).SafeNormalize(Vector2.UnitX);

            if (StateTimer <= 8f) {
                // 反向蓄势: 后 8 帧急速后仰 (对穿的"吸气")
                float t = StateTimer / 8f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, -dir * t * t * 13f, 0.35f);
                FaceTarget(target);
            }
            else if (StateTimer == 9f) {
                // 瞬发: launch is a set
                float d = Vector2.Distance(NPC.Center, swapDest);
                NPC.velocity = dir * MathF.Max(26f, d / 11f);
                SoundEngine.PlaySound(DashSound, NPC.Center);
            }
            else if (StateTimer < 24f) {
                // 交错瞬间: 白闪 + 震屏 (指挥触发一次)
                if (SubState == 0f && pb != null && NPC.Distance(pb.NPC.Center) < 160f) {
                    SubState = 1f;
                    if (IsConductor) {
                        whiteFlash = 0.45f;
                        ACMScreenShakeSystem.Add(6f);
                        SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = 0.3f }, NPC.Center);
                    }
                }
            }
            else {
                NPC.velocity *= 0.7f;
                if (StateTimer >= 30f) {
                    NPC.ai[3] = IsStriker ? 1f : 0f; // 阴阳易位
                    attackIndex = 0;
                    SwitchState(DuetState.Recompose);
                }
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.02f, 0.15f);
        }

        #endregion

        #region P2 阴阳易位·仪

        private void RunP2Rite(Player target) {
            BAWImpermanenceBase pb = PartnerBoss;

            if (StateTimer == 1f) {
                didP2 = true;
                BAWFX.ClearBAWProjectiles();
                swapDest = pb != null ? (NPC.Center + pb.NPC.Center) * 0.5f : NPC.Center + new Vector2(0f, -120f);
                if (IsConductor) {
                    Announce("YinYangSwap", BAWFX.YangColor);
                    SoundEngine.PlaySound(RoarSound with { Pitch = -0.3f }, NPC.Center);
                }
                NPC.netUpdate = true;
            }

            float phase = SideSign > 0 ? 0f : MathHelper.Pi;

            if (StateTimer < 30f) {
                // 合流
                SmoothFly(swapDest + phase.ToRotationVector2() * 70f, 22f, 0.12f);
            }
            else if (StateTimer < 90f) {
                // 背靠背太极互绕, 转速渐快
                float t = StateTimer - 30f;
                float ang = phase + t * 0.02f + t * t * 0.0006f;
                Vector2 want = swapDest + ang.ToRotationVector2() * 70f;
                NPC.velocity = (want - NPC.Center) * 0.35f;

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f),
                        ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = (ang + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }
            else if (StateTimer == 90f) {
                // 对穿分开 (岗位互换)
                NPC.ai[3] = IsStriker ? 1f : 0f;
                Vector2 stance = IsStriker ? StrikerStance(target) : SupportStance(target);
                NPC.velocity = (stance - NPC.Center).SafeNormalize(Vector2.UnitX) * 26f;
                if (IsConductor) {
                    whiteFlash = 0.6f;
                    ACMScreenShakeSystem.Add(8f);
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.2f }, NPC.Center);
                }
            }
            else if (StateTimer < 150f) {
                NPC.velocity *= 0.92f;
                FaceTarget(target);
            }
            else {
                leadAttacksDone = 0;
                attackIndex = 0;
                SwitchState(DuetState.Recompose);
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.02f, 0.12f);
        }

        #endregion

        #region C1 阴阳勾魂 (分屏)

        private void RunSynergyYinYang(Player target) {
            BAWImpermanenceBase pb = PartnerBoss;
            if (pb == null) {
                // 同伴离场: 立刻收场
                SwitchState(DuetState.Recompose);
                return;
            }

            if (StateTimer == 1f) {
                InSynergyAttack = true;
                if (ConductorPriority)
                    synAngle0 = (NPC.Center - target.Center).ToRotation();
                else
                    synAngle0 = (pb.NPC.Center - target.Center).ToRotation() + MathHelper.Pi;
                if (IsConductor) {
                    Announce("YinYangHook", BAWFX.YinColor);
                    SoundEngine.PlaySound(RoarSound with { Pitch = 0.15f }, NPC.Center);
                }
                NPC.netUpdate = true;
            }

            float prog = StateTimer;
            float ang = synAngle0 + MathF.Max(0f, prog - 40f) * 0.0035f;

            // 分屏几何 (两使连线)
            Vector2 mid = (NPC.Center + pb.NPC.Center) * 0.5f;
            Vector2 myNormal = (NPC.Center - pb.NPC.Center).SafeNormalize(Vector2.UnitX);
            Vector2 tangent = myNormal.RotatedBy(MathHelper.PiOver2);

            if (prog < 410f) {
                Vector2 post = target.Center + ang.ToRotationVector2() * 820f;
                SmoothFly(post, 26f, 0.09f);
                FaceTarget(target);

                // 各侧压力节拍 (服务端)
                if (Main.netMode != NetmodeID.MultiplayerClient && prog >= 60f && prog < 396f) {
                    int beat = (int)prog;
                    if (beat % YinYangPressureInterval == 0)
                        SpawnYinYangPressure(target, mid, tangent, myNormal, beat / YinYangPressureInterval);
                }

                MaintainYinYang(mid, myNormal);
            }
            else if (prog < 434f) {
                // 收束: 双使对穿冲向缝心
                if (prog == 411f && IsConductor && Main.netMode != NetmodeID.MultiplayerClient)
                    BAWFX.ClearBAWProjectiles();

                SmoothFly(mid, 42f, 0.2f);
                if (SubState == 0f && NPC.Distance(pb.NPC.Center) < 170f) {
                    SubState = 1f;
                    if (IsConductor) {
                        whiteFlash = 0.6f;
                        ACMScreenShakeSystem.Add(9f);
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                    }
                }
            }
            else if (prog == 434f) {
                // 阴阳合璧: 双色幽魂环 (旋转缺口 = 公平缝)
                if (IsConductor && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 c = (NPC.Center + pb.NPC.Center) * 0.5f;
                    int ringType = ModContent.ProjectileType<GhostWaveProjectile>();
                    float gap = Main.rand.NextFloat(MathHelper.TwoPi);
                    var p1 = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), c, Vector2.Zero, ringType, 110, 0f, -1, 0f, gap, 7f);
                    p1.netUpdate = true;
                    var p2 = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), c, Vector2.Zero, ringType, 110, 0f, -1, 1f, gap + MathHelper.Pi, 7f);
                    p2.netUpdate = true;
                }
                NPC.velocity *= 0.5f;
            }
            else if (prog >= 470f) {
                InSynergyAttack = false;
                NPC.ai[3] = IsStriker ? 1f : 0f; // 协同后顺势易位
                attackIndex = 0;
                SwitchState(DuetState.Recompose);
            }
            else {
                NPC.velocity *= 0.92f;
            }

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.015f, 0.1f);
        }

        #endregion

        #region C2 勾魂链锁

        private void RunSynergyChainLock(Player target) {
            BAWImpermanenceBase pb = PartnerBoss;
            if (pb == null) {
                SwitchState(DuetState.Recompose);
                return;
            }

            if (StateTimer == 1f) {
                InSynergyAttack = true;
                lockCenter = target.Center;
                if (ConductorPriority)
                    synAngle0 = (NPC.Center - target.Center).ToRotation();
                else
                    synAngle0 = (pb.NPC.Center - target.Center).ToRotation() + MathHelper.Pi;
                if (IsConductor) {
                    Announce("ChainLock", BAWFX.YinColor);
                    SoundEngine.PlaySound(ChargeSound, NPC.Center);
                }
                NPC.netUpdate = true;
            }

            // 公转中心软追踪玩家 (可拉扯但不可甩脱)
            lockCenter = Vector2.Lerp(lockCenter, target.Center, 0.01f);

            if (StateTimer < 40f) {
                Vector2 post = lockCenter + synAngle0.ToRotationVector2() * 1250f;
                SmoothFly(post, 30f, 0.12f);
            }
            else if (StateTimer == 40f) {
                // 结链 (指挥生成; 链弹幕自理松垂→绷直→崩断)
                if (IsConductor && Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC black = ConductorPriority ? NPC : pb.NPC;
                    NPC white = ConductorPriority ? pb.NPC : NPC;
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), (NPC.Center + pb.NPC.Center) * 0.5f,
                        Vector2.Zero, ModContent.ProjectileType<SoulChainProjectile>(), 130, 0f, -1, black.whoAmI, white.whoAmI);
                    p.timeLeft = 378;
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(ChainSound, NPC.Center);
            }
            else if (StateTimer < 100f) {
                // 持链对峙 (链松垂无伤害, 读条期)
                Vector2 post = lockCenter + synAngle0.ToRotationVector2() * 1250f;
                SmoothFly(post, 14f, 0.08f);
            }
            else if (StateTimer < 400f) {
                // 恒速公转 (0.012 rad/f, 切向速度可被跑赢) + 半径缓收
                float t = StateTimer - 100f;
                float angle = synAngle0 + t * 0.012f;
                float radius = MathF.Max(1000f, 1250f - t * 0.8f);
                Vector2 want = lockCenter + angle.ToRotationVector2() * radius;
                NPC.velocity = (want - NPC.Center) * 0.25f;
            }
            else if (StateTimer == 400f) {
                // 链崩断: 反冲
                NPC.velocity = (NPC.Center - lockCenter).SafeNormalize(Vector2.UnitX) * 9f;
                if (IsConductor)
                    ACMScreenShakeSystem.Add(7f);
            }
            else if (StateTimer >= 440f) {
                InSynergyAttack = false;
                NPC.ai[3] = IsStriker ? 1f : 0f;
                attackIndex = 0;
                SwitchState(DuetState.Recompose);
            }
            else {
                NPC.velocity *= 0.93f;
            }

            FaceTarget(target);
            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.015f, 0.1f);
        }

        #endregion

        #region 引魂复活

        /// <summary>同伴阵亡触发: 我进入引魂咏唱 (被打断 = 双亡)。</summary>
        public void BeginReviveChannel(Vector2 corpsePos) {
            soulPos = corpsePos;
            SwitchState(DuetState.Reviving);
        }

        private void RunReviving(Player target) {
            if (StateTimer == 1f) {
                Announce("SoulRevive", BAWFX.YangColor);
                SoundEngine.PlaySound(ChargeSound with { Pitch = 0.4f }, NPC.Center);
            }

            // 停手引魂: 移动到尸位侧, 期间受伤 +30% (ModifyIncomingHit)
            SmoothFly(soulPos + new Vector2(SideSign * 260f, -140f), 14f, 0.07f);
            FaceTarget(target);
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);

            // 引魂粒子流: 我 → 尸位
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(NPC.Center, soulPos, t);
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(14f, 14f), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = (soulPos - NPC.Center).SafeNormalize(Vector2.Zero) * 4f;
            }

            BAWImpermanenceBase pb = PartnerBoss;
            if (pb == null || pb.State != DuetState.BeingRevived || StateTimer >= 165f) {
                // 复活流程结束 (或魂已散) → 回到缠斗岗
                attackIndex = 0;
                SwitchState(DuetState.Recompose, role: 0f);
            }
        }

        private void RunBeingRevived(Player target) {
            NPC.dontTakeDamage = StateTimer < 150f;
            NPC.Center = soulPos;
            NPC.velocity = Vector2.Zero;
            drawAlpha = StateTimer < 150f
                ? MathF.Max(0f, drawAlpha - 0.06f)
                : MathHelper.Lerp(drawAlpha, 1f, 0.08f);

            // 魂位残影粒子
            if (!Main.dedServ && StateTimer < 150f && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(soulPos + Main.rand.NextVector2Circular(26f, 40f),
                    ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = new Vector2(0f, -1.6f);
            }

            if (StateTimer == 150f) {
                NPC.life = (int)(NPC.lifeMax * 0.4f);
                BAWFX.ClearBAWProjectiles();
                ACMScreenShakeSystem.Add(8f);
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                whiteFlash = 0.4f;
                NPC.netUpdate = true;
            }
            else if (StateTimer >= 190f) {
                attackIndex = 0;
                SwitchState(DuetState.Recompose, role: 1f); // 复活者从控场岗温和再入
            }
        }

        #endregion

        #region 孤使怒

        private void RunSoloTransform(Player target) {
            if (StateTimer == 1f) {
                if (partnerSoulPos == Vector2.Zero)
                    partnerSoulPos = NPC.Center + new Vector2(0f, -160f);
                BAWFX.ClearBAWProjectiles();
                Announce(SoloAnnounceKey, ConductorPriority ? BAWFX.YinColor : BAWFX.YangColor);
                SoundEngine.PlaySound(ChargeSound with { Pitch = -0.4f }, NPC.Center);
                NPC.netUpdate = true;
            }

            NPC.velocity *= 0.9f;
            FaceTarget(target);
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);

            if (StateTimer < 60f) {
                // 亡魂化流光飞入幸存者
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        float t = Main.rand.NextFloat();
                        Vector2 pos = Vector2.Lerp(partnerSoulPos, NPC.Center, t * t);
                        var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(20f, 20f),
                            ConductorPriority ? DustID.SpectreStaff : DustID.Shadowflame); // 吸的是对方的魂色
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }
            }
            else if (StateTimer == 60f) {
                ACMScreenShakeSystem.Add(6f);
                whiteFlash = 0.3f;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f }, NPC.Center);
            }
            else if (StateTimer == 120f) {
                SoundEngine.PlaySound(RoarSound with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(10f);
                whiteFlash = 0.5f;
            }
            else if (StateTimer >= 140f) {
                Unleashed = true;
                NPC.damage = (int)(NPC.damage * 1.15f);
                attackIndex = 0;
                SwitchState(DuetState.Recompose, role: 0f);
            }
        }

        #endregion

        #region 死亡演出

        private void RunDeathAnim(Player target) {
            NPC.dontTakeDamage = true;

            if (StateTimer == 1f) {
                BAWFX.ClearBAWProjectiles();
                NPC.velocity *= 0.3f;
                NPC.netUpdate = true;
            }

            if (StateTimer < 24f) {
                // 踉跄
                NPC.velocity = Main.rand.NextVector2Circular(1.6f, 1.2f);
                if (StateTimer == 10f)
                    ACMScreenShakeSystem.Add(3f);
            }
            else if (StateTimer == 24f) {
                SoundEngine.PlaySound(ChainSound with { Pitch = -0.7f, Volume = 1.1f }, NPC.Center); // 链/幡坠地
                NPC.velocity = Vector2.Zero;
            }
            else if (StateTimer < 100f) {
                // 缓缓坠飘 + 体表明灭 (drawAlpha 抖动由绘制端包络)
                NPC.velocity = new Vector2(0f, MathF.Min(1.2f, NPC.velocity.Y + 0.03f));
            }
            else if (StateTimer == 100f) {
                SoundEngine.PlaySound(RoarSound with { Pitch = 0.35f, Volume = 1.3f }, NPC.Center);
                NPC.velocity = Vector2.Zero;
            }
            else if (StateTimer < 150f) {
                // 魂焰涌出 + 渐强低鸣
                soulPillar = MathHelper.Lerp(soulPillar, 1f, 0.06f);
                if ((int)StateTimer % 10 == 0)
                    ACMScreenShakeSystem.Add(4f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 44f),
                        ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 2f;
                    d.velocity = new Vector2(0f, -Main.rand.NextFloat(6f, 12f));
                }
            }
            else if (StateTimer == 150f) {
                // 本战唯一冲击帧
                whiteFlash = 1f;
                ACMScreenShakeSystem.Add(14f);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 1.2f }, NPC.Center);
            }
            else if (StateTimer < 170f) {
                drawAlpha = MathF.Max(0f, drawAlpha - 0.07f);
                soulPillar *= 0.88f;
                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40f, 60f),
                            ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = Main.rand.NextVector2Circular(7f, 7f) - new Vector2(0f, 4f);
                    }
                }
            }
            else {
                ForceRealDeath();
            }
        }

        /// <summary>立即真死 (掉落/downed 正常结算)。</summary>
        public void ForceRealDeath() {
            deathAnimDone = true;
            NPC.dontTakeDamage = false;
            NPC.life = 0;
            NPC.HitEffect();
            NPC.checkDead();
        }

        public override bool CheckDead() {
            if (deathAnimDone)
                return true;

            BAWImpermanenceBase pb = PartnerBoss;
            bool partnerActive = pb != null && pb.NPC.active;

            // 同伴正化魂待引 → 我阵亡 = 双亡 (魂散 + 我走死亡演出)
            if (partnerActive && pb.State == DuetState.BeingRevived) {
                pb.ForceRealDeath();
                partnerActive = false;
            }

            bool partnerFighting = partnerActive && pb.State != DuetState.DeathAnim;

            // 引魂窗口: 未复活过 & 同伴健在 (>30%) 且不在引魂中
            if (!hasRespawned && partnerFighting && pb.State != DuetState.Reviving &&
                pb.NPC.life > pb.NPC.lifeMax * 0.3f) {
                hasRespawned = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.velocity = Vector2.Zero;
                soulPos = NPC.Center;
                drawAlpha = 0.6f;
                pb.BeginReviveChannel(NPC.Center);
                SwitchState(DuetState.BeingRevived);
                SoundEngine.PlaySound(SoundID.NPCDeath52, NPC.Center);
                return false;
            }

            // 同伴仍在战斗 → 我立即真死 (掉落照常), 幸存者稍后吞魂孤使化
            if (partnerFighting) {
                pb.partnerSoulPos = NPC.Center;
                if (!Main.dedServ) {
                    for (int i = 0; i < 24; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center,
                            ConductorPriority ? DustID.Shadowflame : DustID.SpectreStaff);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = Main.rand.NextVector2Circular(9f, 9f);
                    }
                }
                deathAnimDone = true;
                return true;
            }

            // 最后一使 → 完整死亡演出
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            SwitchState(DuetState.DeathAnim);
            return false;
        }

        #endregion

        #region 公平阀门与身份层

        /// <summary>接触伤害是否激活 (子类按冲刺爆发帧门控)。</summary>
        protected virtual bool ContactDamageActive => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ContactDamageActive;

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            // 引魂咏唱是可被打的演出: 受伤 +30% (风险回报: 打死引魂者 = 双亡)
            if (State == DuetState.Reviving)
                modifiers.FinalDamage *= 1.3f;
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) {
            // 地府身份层·怨念账: 玩家造业累积 (驱动分屏强度上探)
            UnderworldField.AddGrudge(NPC, Math.Max(1, damageDone / 150));
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) {
            UnderworldField.AddGrudge(NPC, Math.Max(1, damageDone / 150));
        }

        public override bool CheckActive() => false;

        #endregion

        #region 演出辅助

        /// <summary>战斗播报 (本地化键在 NPCs.类名 下)。客户端各自播报。</summary>
        protected void Announce(string key, Color color) {
            if (Main.dedServ)
                return;
            string text = Language.GetTextValue($"Mods.AncientChineseMythology.NPCs.{Name}.{key}");
            CombatText.NewText(NPC.getRect(), color, text, true);
        }

        /// <summary>分屏包络: 按状态节拍给出目标强度 (双端同构, BAWFX 取双使 max)。</summary>
        private float ComputeSplitEnvelope() {
            static float Bump(float x) => x <= 0f || x >= 1f ? 0f : MathF.Sin(MathHelper.Pi * x);

            float t = StateTimer;
            return State switch {
                DuetState.Intro => 0.35f * Bump((t - 165f) / 30f),
                DuetState.P2Rite => 0.5f * Bump((t - 30f) / 60f),
                DuetState.SynergyYinYang => 0.62f * MathF.Min(Utils.GetLerpValue(10f, 50f, t, true), Utils.GetLerpValue(462f, 420f, t, true)),
                DuetState.SoloTransform => 0.45f * Bump((t - 110f) / 34f),
                DuetState.DeathAnim => t < 152f ? 0.5f * Utils.GetLerpValue(100f, 150f, t, true) : 0.5f * Utils.GetLerpValue(170f, 152f, t, true),
                _ => 0f
            };
        }

        #endregion
    }
}

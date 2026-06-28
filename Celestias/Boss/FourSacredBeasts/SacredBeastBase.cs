using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts
{
    /// <summary>
    /// 四圣兽共享升级骨架 / Shared elevated framework for the Four Sacred Beasts
    /// （青龙 Wood · 白虎 Metal · 朱雀 Fire · 玄武 Water）。
    ///
    /// 抽象 <see cref="ModNPC"/> 基类（abstract ⇒ tModLoader 不会自动加载，无需贴图），把 V2 审计点名的
    /// 三件共享逻辑「只写一次」，供后续各圣兽 V2 线程继承复用：
    ///
    ///   1) 确定性攻击轮替 Deterministic rotation —— <see cref="NextAttack"/> + 子类填的 <see cref="GetPhaseRotation"/>
    ///      数组，替代被审计批评的随机 <c>GetRandomPhaseN()</c> hub 与 <c>Fury*/Nirvana*</c> 纯加速巡逻。
    ///   2) 预警子状态机 Telegraph sub-state —— <see cref="TelegraphPhase"/>（Windup→Strike→Recover）+
    ///      <see cref="AdvanceTelegraph"/>，给每招统一「预告→释放→收招」结构（预告时长 ∝ 伤害，§6.1/§6.3）。
    ///   3) 五行主题接线 Per-element theming —— <see cref="Element"/> 驱动 <see cref="Theme"/>（色/方位）、
    ///      <see cref="SkyName"/>（天幕）、以及经 <see cref="SacredBeastFX"/> 守卫调用着色器地基。
    ///
    /// ai 槽位约定（与现有四兽完全一致，便于机械式迁移）：
    ///   ai[0]=状态枚举(int) · ai[1]=<see cref="PhaseTimer"/> · ai[2]=<see cref="AttackTimer"/> · ai[3]=<see cref="SubStateRaw"/>(预警子状态)。
    ///
    /// 子类（各圣兽）需实现/重写：<see cref="Element"/>、<see cref="GetPhaseRotation"/>；
    /// 建议重写：<see cref="SkyName"/>；并在自身 <c>SendExtraAI/ReceiveExtraAI</c> 调
    /// <see cref="SendSacredBeastAI"/>/<see cref="ReceiveSacredBeastAI"/> 同步轮替游标。
    /// 各招的具体内容（弹幕/位移/签名 set-piece）仍由各圣兽自实现 —— 本骨架共享的是「骨架与表现层」，不是攻击内容。
    /// </summary>
    public abstract class SacredBeastBase : ModNPC
    {
        // ================= 五行身份 Element identity =================

        /// <summary>本兽五行属性（子类必须指定）。</summary>
        public abstract SacredElement Element { get; }

        /// <summary>本兽视觉主题包（色/方位），由 <see cref="Element"/> 解析。</summary>
        public SacredElementTheme Theme => SacredBeastColors.GetTheme(Element);

        /// <summary>本兽专属天幕 SkyName（如 "ACM:QinglongSky"）；为 null 则不驱动天幕。子类建议重写。</summary>
        public virtual string SkyName => null;

        // ================= 阶段阈值 Phase thresholds =================

        public virtual float Phase2Threshold => 0.60f;
        public virtual float Phase3Threshold => 0.30f;

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        /// <summary>当前阶段档位：1 / 2 / 3。供 <see cref="GetPhaseRotation"/> 与天幕取阶段。</summary>
        public int PhaseTier => IsPhase3 ? 3 : IsPhase2 ? 2 : 1;

        // ================= 共享状态槽 Shared state slots =================

        /// <summary>当前状态机原始值（子类把自己的 enum 强转 int 存这里 = ai[0]）。</summary>
        public int RawState {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }

        /// <summary>当前状态已经过的帧数（ai[1]）。</summary>
        public ref float PhaseTimer => ref NPC.ai[1];
        /// <summary>当前攻击/子状态已经过的帧数（ai[2]）。预警子状态切换时清零。</summary>
        public ref float AttackTimer => ref NPC.ai[2];
        /// <summary>预警子状态原始值（ai[3]）。</summary>
        public ref float SubStateRaw => ref NPC.ai[3];

        /// <summary>本地累计秒数（视觉用，非权威）。<see cref="RunStandardPrologue"/> 自动推进。</summary>
        protected float GlobalTime;

        private bool OnServerAuthority => Main.netMode != NetmodeID.MultiplayerClient;

        // ================= 预警子状态机 Telegraph sub-state =================

        /// <summary>预警子状态：预告 → 释放 → 收招。</summary>
        public enum TelegraphPhase
        {
            /// <summary>预告/前摇 —— 显示落点/方向线，伤害源尚未生效。</summary>
            Windup = 0,
            /// <summary>释放 —— 实际造成伤害的窗口。</summary>
            Strike = 1,
            /// <summary>收招/余波 —— 短暂硬直，可被下一招轮替接续。</summary>
            Recover = 2
        }

        /// <summary>当前预警子状态。</summary>
        public TelegraphPhase Telegraph => (TelegraphPhase)(int)SubStateRaw;
        public bool InWindup => Telegraph == TelegraphPhase.Windup;
        public bool InStrike => Telegraph == TelegraphPhase.Strike;
        public bool InRecover => Telegraph == TelegraphPhase.Recover;

        /// <summary>切换预警子状态并清零 <see cref="AttackTimer"/>。</summary>
        public void SetTelegraph(TelegraphPhase phase) {
            SubStateRaw = (int)phase;
            AttackTimer = 0;
            if (OnServerAuthority) NPC.netUpdate = true;
        }

        /// <summary>开始一招：进入 <see cref="TelegraphPhase.Windup"/>。</summary>
        public void BeginAttack() => SetTelegraph(TelegraphPhase.Windup);

        /// <summary>
        /// 按各阶段帧预算自动推进预警子状态机：Windup(windupTicks)→Strike(strikeTicks)→Recover(recoverTicks)。
        /// 每帧调用一次；当整招（含收招）走完时返回 true，调用方据此轮替到下一招。
        /// 预告时长 ∝ 伤害：小压制弹 ≤20、中等 ~35–55、处决级 60–90（§6.3）。
        /// </summary>
        protected bool AdvanceTelegraph(int windupTicks, int strikeTicks, int recoverTicks) {
            switch (Telegraph) {
                case TelegraphPhase.Windup:
                    if (AttackTimer >= windupTicks) SetTelegraph(TelegraphPhase.Strike);
                    return false;
                case TelegraphPhase.Strike:
                    if (AttackTimer >= strikeTicks) SetTelegraph(TelegraphPhase.Recover);
                    return false;
                default: // Recover
                    return AttackTimer >= recoverTicks;
            }
        }

        // ================= 确定性轮替 Deterministic rotation =================

        /// <summary>
        /// 子类按阶段档位（1/2/3）返回该阶段的**可读固定攻击序列**（其自身状态 enum 强转 int 的数组），
        /// 替代随机 hub。返回 null/空表示该阶段不轮替。
        /// </summary>
        protected abstract int[] GetPhaseRotation(int phaseTier);

        // 每阶段独立游标（index 0 不用；用 1/2/3 对齐 PhaseTier）。
        private readonly int[] rotationCursor = new int[4];

        /// <summary>取该阶段轮替的下一招（确定性、循环），并推进游标。无轮替返回 -1。</summary>
        public int NextAttack(int phaseTier) {
            int[] rot = GetPhaseRotation(phaseTier);
            if (rot == null || rot.Length == 0) return -1;
            int idx = rotationCursor[phaseTier] % rot.Length;
            int id = rot[idx];
            rotationCursor[phaseTier] = (idx + 1) % rot.Length;
            if (OnServerAuthority) NPC.netUpdate = true;
            return id;
        }

        /// <summary>取该阶段轮替的下一招但**不**推进游标（用于预告下一招）。无轮替返回 -1。</summary>
        public int PeekNextAttack(int phaseTier) {
            int[] rot = GetPhaseRotation(phaseTier);
            if (rot == null || rot.Length == 0) return -1;
            return rot[rotationCursor[phaseTier] % rot.Length];
        }

        /// <summary>重置某阶段轮替游标（进入新阶段时调用，确保签名节拍从头开始）。</summary>
        public void ResetRotation(int phaseTier) {
            if (phaseTier >= 0 && phaseTier < rotationCursor.Length) rotationCursor[phaseTier] = 0;
        }

        /// <summary>重置全部阶段轮替游标。</summary>
        public void ResetAllRotations() {
            for (int i = 0; i < rotationCursor.Length; i++) rotationCursor[i] = 0;
        }

        // ================= 状态转换 State transition =================

        /// <summary>
        /// 切到新状态并清零 PhaseTimer/AttackTimer/SubState。子类传入自己的 enum 强转 int。
        /// </summary>
        public virtual void TransitionToState(int newState) {
            RawState = newState;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubStateRaw = 0;
            if (OnServerAuthority) NPC.netUpdate = true;
        }

        // ================= AI 公共序幕 Shared AI prologue =================

        /// <summary>
        /// 标准 AI 序幕：推进本地时间、锁定目标、无目标时升空脱战、推进 PhaseTimer/AttackTimer。
        /// 返回 false 表示本帧应直接 return（无有效目标）。子类在自身 <c>AI()</c> 起手调用，随后做状态 switch。
        /// </summary>
        protected bool RunStandardPrologue(out Player target, float despawnAscend = 0.8f) {
            GlobalTime += 1f / 60f;

            NPC.TargetClosest();
            target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= despawnAscend;
                    NPC.EncourageDespawn(30);
                    return false;
                }
            }

            PhaseTimer++;
            AttackTimer++;
            return true;
        }

        // ================= 表现层助手 Presentation helpers =================

        /// <summary>
        /// 统一屏幕震动（封装 <see cref="PunchCameraModifier"/>，服务端零绘制）。强度参考 §6.2 预算：
        /// 落地 4–6 / 相变·大招 8–12 / 入场·死亡 ≤16。
        /// TODO: 待 <c>ACMUtils.AddScreenShake</c>（另一地基 agent 授权，同帧取 max）落地后改为转调以统一预算。
        /// </summary>
        public void ShakeScreen(float strength, float vibration = 8f, int time = 20, float range = 2000f) {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 dir = (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(NPC.Center, dir, strength, vibration, time, range, FullName));
        }

        /// <summary>本兽元素色径向泛光（蓄力/爆发/相变），经守卫式 <see cref="SacredBeastFX"/> 绘制。</summary>
        public void ElementBloom(SpriteBatch sb, float intensity, float worldRadius = 220f)
            => SacredBeastFX.RadialBloom(sb, NPC.Center, Theme.Primary, worldRadius, intensity);

        /// <summary>本兽预警色落点圈（致命=红，非致命=元素色），经守卫式 <see cref="SacredBeastFX"/> 绘制。</summary>
        public void ElementTelegraphCircle(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity, bool lethal)
            => SacredBeastFX.TelegraphCircle(sb, worldCenter, SacredBeastColors.Telegraph(Element, lethal), worldRadius, intensity);

        /// <summary>取本兽预警色：致命攻击恒红，非致命用元素色（§6.1）。</summary>
        public Color GetTelegraphColor(bool lethal) => SacredBeastColors.Telegraph(Element, lethal);

        // ================= 网络同步 Netcode helpers =================

        /// <summary>同步轮替游标 + 本地时间。子类在 <c>SendExtraAI</c> 内调用。</summary>
        public void SendSacredBeastAI(BinaryWriter writer) {
            writer.Write(GlobalTime);
            for (int i = 1; i < rotationCursor.Length; i++) writer.Write(rotationCursor[i]);
        }

        /// <summary>读取轮替游标 + 本地时间。子类在 <c>ReceiveExtraAI</c> 内调用。</summary>
        public void ReceiveSacredBeastAI(BinaryReader reader) {
            GlobalTime = reader.ReadSingle();
            for (int i = 1; i < rotationCursor.Length; i++) rotationCursor[i] = reader.ReadInt32();
        }
    }
}

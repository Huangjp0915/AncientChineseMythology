using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.DataStructures;
using Terraria.ModLoader;
using AncientChineseMythology.Underworlds.Boss.AwakeningNethers;
using AncientChineseMythology.Underworlds.Boss.YinEmperors;

namespace AncientChineseMythology.Underworlds
{
    /// <summary>
    /// 地府身份层 (Underworld Identity Field) —— 全地府 Boss 共享的"地府战斗身份"。
    ///
    /// 把审计点名的空壳 <see cref="UnderworldPlayer.UnderworldEffect"/> 升级为三条真实主轴：
    ///   1. 魂蚀 Soul Erosion —— 叠层 DoT（层数随时间衰减；层数越高烧得越快）。
    ///   2. 冥律 Nether Decree —— 判定标记（命中叠层；满层触发可读的"定魂"短暂锁定 / 处决开口）。
    ///   3. 怨念 Grudge —— Boss 端的怨念账（记录玩家造业，可被 Boss 读取以强化终结技；玩家可清账反制）。
    ///
    /// 这是一个**系统**，不是单 Boss 改写：Boss AI 只需调用本类的一行静态方法即可消费身份层。
    /// 数值/层数承载在 <see cref="UnderworldFieldPlayer"/>（玩家轴）与 <see cref="UnderworldGrudgeNPC"/>（Boss 轴）。
    /// </summary>
    public static class UnderworldField
    {
        // 统一配色（与既有 AwakeningNetherHelper.SoulPink / YinEmperorHelper 取色一致，保证观感连贯）。
        public static readonly Color SoulErosionColor = new Color(255, 120, 200);
        public static readonly Color DecreeColor = new Color(100, 30, 160);
        public static readonly Color SoulBoundColor = new Color(200, 30, 50);

        // ---------------- 魂蚀 Soul Erosion ----------------

        /// <summary>给玩家叠加魂蚀 DoT 层数（站位/受击调用）。Boss 代码一行即可。</summary>
        public static void AddSoulErosion(Player player, int amount) =>
            player?.GetModPlayer<UnderworldFieldPlayer>().AddSoulErosion(amount);

        /// <summary>读取玩家当前魂蚀层数。</summary>
        public static int GetSoulErosion(Player player) =>
            player == null ? 0 : player.GetModPlayer<UnderworldFieldPlayer>().SoulErosionStacks;

        // ---------------- 冥律 Nether Decree ----------------

        /// <summary>给玩家叠加冥律标记（被 Boss"判定/记账"时调用）。满层触发定魂。</summary>
        public static void AddNetherDecree(Player player, int amount = 1) =>
            player?.GetModPlayer<UnderworldFieldPlayer>().AddNetherDecree(amount);

        /// <summary>读取玩家当前冥律层数。</summary>
        public static int GetNetherDecree(Player player) =>
            player == null ? 0 : player.GetModPlayer<UnderworldFieldPlayer>().DecreeStacks;

        /// <summary>玩家是否处于"定魂"移动锁定（满层处决开口）。Boss 可据此插入处决招。</summary>
        public static bool IsSoulBound(Player player) =>
            player != null && player.GetModPlayer<UnderworldFieldPlayer>().DingHunLock > 0;

        /// <summary>玩家是否正处于满层定魂预告窗口（Boss 可同步预告处决）。</summary>
        public static bool IsDecreeTelegraphing(Player player) =>
            player != null && player.GetModPlayer<UnderworldFieldPlayer>().DingHunTelegraph > 0;

        // ---------------- 怨念 Grudge（Boss 端账本）----------------

        /// <summary>
        /// 向 Boss 的怨念账累积（玩家造业：高 DPS 爆发 / 停留 / 清场等由 Boss 自行判定时机调用）。
        /// 与 Items/ 下武器施加的 <c>NetherGrudgeGlobalNPC</c>（敌方魂蚀 DoT）不同：这是 Boss 记账玩家行为。
        /// </summary>
        public static void AddGrudge(NPC npc, int amount) =>
            npc?.GetGlobalNPC<UnderworldGrudgeNPC>().Add(amount);

        /// <summary>玩家清账反制：降低 Boss 怨念（如击杀被召唤的冤魂）。</summary>
        public static void ReduceGrudge(NPC npc, int amount) =>
            npc?.GetGlobalNPC<UnderworldGrudgeNPC>().Add(-amount);

        /// <summary>Boss 读取当前怨念点数（用于强化终结技强度）。</summary>
        public static int GetGrudge(NPC npc) =>
            npc == null ? 0 : npc.GetGlobalNPC<UnderworldGrudgeNPC>().Grudge;

        /// <summary>Boss 读取归一化怨念 0–1（驱动着色器强度 / 终结技规模）。</summary>
        public static float GetGrudgeNormalized(NPC npc) {
            if (npc == null) return 0f;
            var g = npc.GetGlobalNPC<UnderworldGrudgeNPC>();
            return g.MaxGrudge <= 0 ? 0f : MathHelper.Clamp(g.Grudge / (float)g.MaxGrudge, 0f, 1f);
        }

        /// <summary>设置某 Boss 怨念账的上限（Boss 可在 SetDefaults 调一次以定制清算阈值）。</summary>
        public static void SetGrudgeMax(NPC npc, int max) {
            if (npc != null) npc.GetGlobalNPC<UnderworldGrudgeNPC>().MaxGrudge = Math.Max(1, max);
        }

        // ---------------- 统一 0–1 标量（驱动身份层视觉/着色器降级）----------------

        /// <summary>玩家身上地府压力的统一 0–1 标量（魂蚀 + 冥律），供全屏 shader 单点驱动。</summary>
        public static float FieldIntensity(Player player) {
            if (player == null) return 0f;
            var mp = player.GetModPlayer<UnderworldFieldPlayer>();
            float erosion = mp.SoulErosionStacks / (float)UnderworldFieldPlayer.MaxSoulErosion;
            float decree = mp.DecreeStacks / (float)UnderworldFieldPlayer.MaxDecree;
            float bound = mp.DingHunLock > 0 ? 1f : 0f;
            return MathHelper.Clamp(Math.Max(bound, Math.Max(erosion, decree)), 0f, 1f);
        }
    }

    /// <summary>
    /// 玩家轴身份层：承载 魂蚀 DoT 与 冥律标记/定魂。
    /// 既有的 <see cref="AwakeningNetherPlayer"/> / <see cref="YinJudgmentPlayer"/> 已改为委托到本类，
    /// 因此两 Boss 的现有调用点（AddSoulErosion / AddDecreeStack）行为完全保留、且共享同一套机制。
    /// </summary>
    public class UnderworldFieldPlayer : ModPlayer
    {
        // —— 魂蚀 Soul Erosion 调参（沿用觉醒冥龙 P0 调参，行为保持一致）——
        public const int MaxSoulErosion = 10;

        public int SoulErosionStacks { get; private set; }
        private int erosionDotTimer;
        private int erosionDecayTimer;

        // —— 冥律 Nether Decree 调参（沿用阴天子 P0 调参，行为保持一致）——
        public const int MaxDecree = 3;

        public int DecreeStacks { get; private set; }
        private int decreeDecay;
        public int DingHunTelegraph { get; private set; }
        public int DingHunLock { get; private set; }

        public override void ResetEffects() {
            // 层数为持续状态，不在此清零（仅由各自衰减逻辑管理）。
        }

        // ================= 魂蚀 Soul Erosion =================

        public void AddSoulErosion(int amount) {
            if (amount <= 0) return;
            SoulErosionStacks = Math.Min(MaxSoulErosion, SoulErosionStacks + amount);
            erosionDecayTimer = 150; // 离开魂雾/不再被叠时才开始衰减
            Player.AddBuff(ModContent.BuffType<SoulErosion>(), 300);
        }

        private void UpdateSoulErosion() {
            if (SoulErosionStacks <= 0) {
                erosionDotTimer = 0;
                return;
            }

            // 层数越高，灼蚀越快越疼。
            int interval = Math.Max(14, 36 - SoulErosionStacks * 2);
            erosionDotTimer++;
            if (erosionDotTimer >= interval) {
                erosionDotTimer = 0;
                int dmg = 4 + SoulErosionStacks * 3;
                if (Player.statLife > dmg + 1) {
                    Player.statLife -= dmg;
                    if (Main.myPlayer == Player.whoAmI)
                        CombatText.NewText(Player.Hitbox, UnderworldField.SoulErosionColor, dmg);
                }
            }

            if (--erosionDecayTimer <= 0) {
                SoulErosionStacks--;
                erosionDecayTimer = 70;
                if (SoulErosionStacks <= 0)
                    Player.ClearBuff(ModContent.BuffType<SoulErosion>());
            }
        }

        // ================= 冥律 Nether Decree =================

        public void AddNetherDecree(int amount = 1) {
            // 定魂期间不再叠层（已经在受罚）
            if (DingHunLock > 0) return;

            DecreeStacks = Math.Min(MaxDecree, DecreeStacks + amount);
            decreeDecay = 300;
            Player.AddBuff(ModContent.BuffType<NetherDecreeMark>(), 300);

            if (Player.whoAmI == Main.myPlayer)
                CombatText.NewText(Player.Hitbox, UnderworldField.DecreeColor, "冥律 " + DecreeStacks + "/" + MaxDecree);

            // 满层 -> 进入定魂预告（给玩家可读窗口走位/拉开）
            if (DecreeStacks >= MaxDecree && DingHunTelegraph <= 0 && DingHunLock <= 0)
                DingHunTelegraph = 28;
        }

        private void UpdateDecreeStacks() {
            if (DecreeStacks > 0 && DingHunTelegraph <= 0 && DingHunLock <= 0) {
                decreeDecay--;
                if (decreeDecay <= 0) {
                    DecreeStacks = 0;
                    Player.ClearBuff(ModContent.BuffType<NetherDecreeMark>());
                }
            }
        }

        private void UpdateDingHun() {
            if (DingHunTelegraph > 0) {
                DingHunTelegraph--;

                // 预告：玩家身周收束的紫金符环（可读）
                if (!Main.dedServ && DingHunTelegraph % 2 == 0) {
                    float r = 60f * (DingHunTelegraph / 28f) + 16f;
                    for (int i = 0; i < 3; i++) {
                        float a = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 pos = Player.Center + a.ToRotationVector2() * r;
                        var d = Dust.NewDustPerfect(pos, i % 2 == 0 ? DustID.GoldFlame : DustID.Shadowflame);
                        d.noGravity = true;
                        d.scale = 1.2f;
                        d.velocity = (Player.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
                    }
                }

                if (DingHunTelegraph == 0) {
                    DingHunLock = 30;
                    DecreeStacks = 0;
                    Player.ClearBuff(ModContent.BuffType<NetherDecreeMark>());
                    if (Player.whoAmI == Main.myPlayer)
                        CombatText.NewText(Player.Hitbox, UnderworldField.SoulBoundColor, "定魂!");
                }
            }

            if (DingHunLock > 0) {
                DingHunLock--;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(24, 30), DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = new Vector2(0, -1.5f);
                }
            }
        }

        public override void PreUpdateMovement() {
            // 定魂：移动锁定（telegraphed），呼应"审判定魂"
            if (DingHunLock > 0) {
                Player.velocity = Vector2.Zero;
                Player.frozen = true;
            }
        }

        public override void PostUpdate() {
            UpdateSoulErosion();
            UpdateDecreeStacks();
            UpdateDingHun();
        }
    }

    /// <summary>
    /// Boss 轴身份层：怨念账 Grudge Ledger。
    /// 每个 NPC 实例持有一份怨念点数；Boss AI 通过 <see cref="UnderworldField"/> 静态方法读写。
    /// 怨念不自动衰减（它是一笔"账"），仅由玩家清账（<see cref="UnderworldField.ReduceGrudge"/>）下降，
    /// 供 Boss 在终幕一次性"清算"成强化的终结技。
    /// </summary>
    public class UnderworldGrudgeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public const int DefaultMaxGrudge = 100;

        public int Grudge;
        public int MaxGrudge = DefaultMaxGrudge;

        public void Add(int amount) {
            Grudge = (int)MathHelper.Clamp(Grudge + amount, 0, MaxGrudge);
        }
    }
}

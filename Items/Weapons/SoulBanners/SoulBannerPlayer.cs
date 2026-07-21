using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡成长系统 —— 存储在玩家身上的灵魂数据
    /// · soulCount：已吸收的灵魂数量（通过吸魂攻击击杀获得）
    /// · soulCap：灵魂上限（通过击败 Boss 提升）
    /// · defeatedBossTiers：已击败的 Boss 阶层记录
    /// 多人安全：灵魂结算/消费全部只发生在 owner 客户端（伤害计算亦在 owner 端），数据本地存档，零网络包。
    /// </summary>
    public class SoulBannerPlayer : ModPlayer
    {
        /// <summary>当前吸收的灵魂数量</summary>
        public int soulCount;

        /// <summary>灵魂上限（由 Boss 击杀解锁）</summary>
        public int soulCap;

        /// <summary>已解锁的 Boss 阶层（用于防止重复计算）</summary>
        public HashSet<int> defeatedBossTiers = new();

        // ══════════════════════════════════════════════
        //  大招「万魂齐哭」资源接口
        // ══════════════════════════════════════════════

        /// <summary>大招最低灵魂需求</summary>
        public const int UltMinSouls = 80;

        /// <summary>大招是否就绪（灵魂足够）</summary>
        public bool UltReady => soulCount >= UltMinSouls;

        /// <summary>
        /// 结算大招灵魂消耗：当前灵魂的 40%（至少 <see cref="UltMinSouls"/>）。
        /// 返回实际消耗量；不足时返回 0 且不扣除。仅 owner 客户端调用。
        /// </summary>
        public int TrySpendUltSouls() {
            if (!UltReady)
                return 0;
            int cost = Math.Max(UltMinSouls, (int)(soulCount * 0.4f));
            cost = Math.Min(cost, soulCount);
            soulCount -= cost;
            return cost;
        }

        // ══════════════════════════════════════════════
        //  UI 即时反馈脉冲（纯本地表现层）
        // ══════════════════════════════════════════════

        /// <summary>最近一次增魂后的倒计时帧（30→0，驱动 UI 脉冲/数字弹跳）</summary>
        public int lastGainTimer;

        /// <summary>最近一次增魂量（UI 浮动显示）</summary>
        public int lastGainAmount;

        /// <summary>灵魂飞线到达玩家时的身上柔光倒计时帧</summary>
        public int absorbFlashTimer;

        public override void PostUpdateMiscEffects() {
            if (lastGainTimer > 0)
                lastGainTimer--;
            if (absorbFlashTimer > 0)
                absorbFlashTimer--;
        }

        /// <summary>登记一次增魂（刷新 UI 脉冲）。仅 owner 客户端调用。</summary>
        public void RegisterGain(int amount) {
            if (amount <= 0)
                return;
            lastGainAmount = amount;
            lastGainTimer = 30;
        }

        // ══════════════════════════════════════════════
        //  Boss 阶层定义（v3.3.1 · PROGRESSION_DESIGN_SPEC §3.3）
        // ══════════════════════════════════════════════

        public struct BossTier
        {
            public int TierId;
            public int CapValue;
            public Func<int> GetNPCType;
            /// <summary>可选的替代 NPC（如世界吞噬者/克苏鲁之脑，双子魔眼双体）</summary>
            public Func<int> GetAltNPCType;
            /// <summary>中文显示名（UI 直接读取）</summary>
            public string NameZh;

            public BossTier(int tierId, int capValue, Func<int> getNPCType, string nameZh,
                Func<int> getAltNPCType = null) {
                TierId = tierId;
                CapValue = capValue;
                GetNPCType = getNPCType;
                GetAltNPCType = getAltNPCType;
                NameZh = nameZh;
            }

            public bool MatchesNPC(int npcType) {
                if (GetNPCType() == npcType) return true;
                return GetAltNPCType != null && GetAltNPCType() == npcType;
            }
        }

        // 延迟初始化，避免在静态构造期调用 ModContent
        private static BossTier[] _tiers;

        public static BossTier[] Tiers {
            get {
                _tiers ??= new BossTier[]
                {
                    // ── 肉山前（T1–8 · 无赢勾）──
                    new( 1,    50, () => NPCID.KingSlime,        "史莱姆王"),
                    new( 2,   120, () => NPCID.EyeofCthulhu,    "克苏鲁之眼"),
                    new( 3,   200, () => ModContent.NPCType<NPCs.Boss.BlackBear.BlackBear>(), "黑熊精"),
                    new( 4,   300, () => NPCID.EaterofWorldsHead,"世吞/克脑",  () => NPCID.BrainofCthulhu),
                    new( 5,   420, () => NPCID.QueenBee,         "蜂后"),
                    new( 6,   560, () => NPCID.SkeletronHead,    "骷髅王"),
                    new( 7,   880, () => NPCID.Deerclops,        "鹿角怪"),
                    new( 8,  1100, () => NPCID.WallofFlesh,      "血肉墙"),

                    // ── 困难模式 · Plantera 前/邻接（T9–16）──
                    new( 9,  1350, () => ModContent.NPCType<NPCs.Boss.NiutouMamian.NiuTou>(), "牛头马面",
                        () => ModContent.NPCType<NPCs.Boss.NiutouMamian.MaMian>()),
                    new(10,  1600, () => NPCID.QueenSlimeBoss,   "史莱姆皇后"),
                    new(11,  1900, () => NPCID.TheDestroyer,     "毁灭者"),
                    new(12,  2200, () => NPCID.Retinazer,        "双子魔眼",   () => NPCID.Spazmatism),
                    new(13,  2550, () => NPCID.SkeletronPrime,   "机械骷髅王"),
                    new(15,  2900, () => ModContent.NPCType<NPCs.Boss.KyuubiKitsunes.KyuubiKitsune>(), "九尾狐"),
                    new(16,  3300, () => NPCID.Plantera,         "世纪之花"),

                    // ── 困难模式 · Plantera 后 → 月灵（T17–21）──
                    new(17,  3700, () => NPCID.Golem,            "石巨人"),
                    new(18,  4150, () => NPCID.HallowBoss,       "光之女皇"),
                    new(19,  4600, () => NPCID.DukeFishron,      "猪龙鱼公爵"),
                    new(20,  6150, () => NPCID.CultistBoss,      "拜月教邪教徒"),
                    new(21,  6700, () => NPCID.MoonLordCore,     "月亮领主"),

                    // ── 月后 · 四大僵尸（T24–27）──
                    new(24,  7400, () => ModContent.NPCType<NPCs.Boss.Hanbas.Hanba>(), "旱魃"),
                    new(25,  8150, () => ModContent.NPCType<NPCs.Boss.Hoqings.Hoqing>(), "后卿"),
                    new(26,  8900, () => ModContent.NPCType<NPCs.Boss.Yingous.Yingou>(), "赢勾"),
                    new(27,  9650, () => ModContent.NPCType<NPCs.Boss.Jiangcens.Jiangcen>(), "将臣"),

                    // ── 月后 · 天庭线（T28–44）──
                    new(28, 11200, () => ModContent.NPCType<Celestias.Boss.Vigors.Vigor>(), "神威"),
                    new(29, 12050, () => ModContent.NPCType<Celestias.Boss.Arguses.Argus>(), "百目"),
                    new(30, 12900, () => ModContent.NPCType<Celestias.Boss.AoGuangs.AoGuang>(), "敖广"),
                    new(31, 13750, () => ModContent.NPCType<Celestias.Boss.Aokins.Aokin>(), "敖钦"),
                    new(32, 14600, () => ModContent.NPCType<Celestias.Boss.Aoyuans.Aoyuan>(), "敖闰"),
                    new(33, 15450, () => ModContent.NPCType<Celestias.Boss.Aoshuns.Aoshun>(), "敖顺"),
                    new(34, 16300, () => ModContent.NPCType<Celestias.Boss.Vaisravanas.Vaisravana>(), "毗沙门天"),
                    new(35, 17150, () => ModContent.NPCType<Celestias.Boss.Dryades.Dryads>(), "树精"),
                    new(36, 18000, () => ModContent.NPCType<Celestias.Boss.Dazhengs.Dazheng>(), "大椿"),
                    new(37, 18950, () => ModContent.NPCType<Celestias.Boss.FourSacredBeasts.Qinlongs.Qinlong>(), "青龙"),
                    new(38, 19900, () => ModContent.NPCType<Celestias.Boss.FourSacredBeasts.Baihus.Baihu>(), "白虎"),
                    new(39, 20850, () => ModContent.NPCType<Celestias.Boss.FourSacredBeasts.Suzakus.Suzaku>(), "朱雀"),
                    new(40, 21800, () => ModContent.NPCType<Celestias.Boss.FourSacredBeasts.Xuanwus.Xuanwu>(), "玄武"),
                    new(41, 22750, () => ModContent.NPCType<Celestias.Boss.AncestralDragonSouls.AncestralDragonSoulHead>(), "祖龙残魂"),
                    new(42, 23700, () => ModContent.NPCType<Celestias.Boss.CelestialDragons.CelestialDragonsHead>(), "天御金龙"),
                    new(43, 24650, () => ModContent.NPCType<Celestias.Boss.CelestialOverseers.CelestialOverseer>(), "天庭观察者"),
                    new(44, 25600, () => ModContent.NPCType<NPCs.Boss.AzureDragons.AzureDragonHead>(), "苍龙真身"),

                    // ── 月后 · 地府线（T46–52）──
                    new(46, 28350, () => ModContent.NPCType<Underworlds.Boss.BAWImpermanences.BlackImpermanence>(), "黑白无常",
                        () => ModContent.NPCType<Underworlds.Boss.BAWImpermanences.WhiteImpermanence>()),
                    new(47, 29750, () => ModContent.NPCType<Underworlds.Boss.Spectres.Spectre>(), "怨灵"),
                    new(48, 31200, () => ModContent.NPCType<Underworlds.Boss.NetherKitsunes.NetherKitsune>(), "幽冥妖狐"),
                    new(49, 32700, () => ModContent.NPCType<Underworlds.Boss.NetherDragons.NetherDragonHead>(), "幽冥龙"),
                    new(50, 34250, () => ModContent.NPCType<Underworlds.Boss.Corpseses.Corpses>(), "尸骸"),
                    new(51, 35850, () => ModContent.NPCType<Underworlds.Boss.AwakeningNethers.AwakeningNetherHead>(), "觉醒幽冥龙"),
                    new(52, 37500, () => ModContent.NPCType<Underworlds.Boss.YinEmperors.YinEmperor>(), "阴天子"),
                };
                return _tiers;
            }
        }

        // ══════════════════════════════════════════════
        //  成长收益计算
        // ══════════════════════════════════════════════

        /// <summary>成长比例：soulCount / 当前最高可达上限（0~1，上限解锁前按已有上限计）</summary>
        public float GrowthRatio => soulCap > 0 ? Math.Clamp((float)soulCount / soulCap, 0f, 1f) : 0f;

        /// <summary>成长等级（方便做离散化的阶段判断）：0~10</summary>
        public int GrowthLevel {
            get {
                if (soulCap <= 0) return 0;
                // 找到当前 cap 对应的最高阶层
                int level = 0;
                foreach (var tier in Tiers) {
                    if (defeatedBossTiers.Contains(tier.TierId))
                        level = tier.TierId + 1;
                }
                return level;
            }
        }

        /// <summary>伤害倍率加成：0% ~ +200%（上限8000灵魂时）</summary>
        public float DamageMultiplier => 1f + 2f * GrowthRatio;

        /// <summary>吸魂范围倍率：1x ~ 2.5x</summary>
        public float AbsorbRadiusMultiplier => 1f + 1.5f * GrowthRatio;

        /// <summary>引魂阶段持续时间倍率：1x ~ 1.8x</summary>
        public float ChannelTimeMultiplier => 1f + 0.8f * GrowthRatio;

        /// <summary>生命回复量倍率：1x ~ 4x</summary>
        public float HealMultiplier => 1f + 3f * GrowthRatio;

        /// <summary>击退倍率：1x ~ 2x</summary>
        public float KnockbackMultiplier => 1f + 1f * GrowthRatio;

        /// <summary>额外穿透数（引魂阶段碰撞范围内可命中的额外目标）</summary>
        public int BonusAbsorbTargets => (int)(GrowthRatio * 5);

        // ══════════════════════════════════════════════
        //  灵魂吸收
        // ══════════════════════════════════════════════

        /// <summary>
        /// 击杀敌人时调用，返回实际获得的灵魂数
        /// </summary>
        public int AbsorbSoul(NPC npc) {
            if (soulCap <= 0 || soulCount >= soulCap)
                return 0;

            // 灵魂获取量与敌人强度挂钩
            int gain = Math.Max(1, (int)(npc.lifeMax / 200f));
            // Boss 给大量灵魂
            if (npc.boss)
                gain = Math.Max(10, npc.lifeMax / 50);

            int before = soulCount;
            soulCount = Math.Min(soulCount + gain, soulCap);
            return soulCount - before;
        }

        /// <summary>
        /// Boss 击杀时调用——解锁对应阶层上限
        /// 返回新解锁的上限值，0 表示未匹配或已解锁
        /// </summary>
        public int TryUnlockBossTier(int npcType) {
            foreach (var tier in Tiers) {
                if (tier.MatchesNPC(npcType) && !defeatedBossTiers.Contains(tier.TierId)) {
                    defeatedBossTiers.Add(tier.TierId);
                    RecalculateCap();
                    return tier.CapValue;
                }
            }
            return 0;
        }

        private void RecalculateCap() {
            int maxCap = 0;
            foreach (var tier in Tiers) {
                if (defeatedBossTiers.Contains(tier.TierId) && tier.CapValue > maxCap)
                    maxCap = tier.CapValue;
            }
            soulCap = maxCap;
        }

        // ══════════════════════════════════════════════
        //  存档
        // ══════════════════════════════════════════════

        public override void SaveData(TagCompound tag) {
            tag["soulBanner_soulCount"] = soulCount;
            tag["soulBanner_soulCap"] = soulCap;
            tag["soulBanner_tiers"] = new List<int>(defeatedBossTiers);
        }

        public override void LoadData(TagCompound tag) {
            soulCount = tag.GetInt("soulBanner_soulCount");
            soulCap = tag.GetInt("soulBanner_soulCap");
            var tiers = tag.Get<List<int>>("soulBanner_tiers");
            defeatedBossTiers = tiers != null ? new HashSet<int>(tiers) : new HashSet<int>();
        }
    }
}

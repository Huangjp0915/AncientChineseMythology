using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus;
using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs;
using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus;
using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts
{
    /// <summary>
    /// 「四象归位」仪式控制器 —— <see cref="Items.FourSymbolsTablet"/> 触发的四圣兽组曲挑战。
    /// 按方位序 青龙(东)→朱雀(南)→白虎(西)→玄武(北) 依次召唤；前一兽被击败后经一段间奏再召下一兽。
    ///
    /// 设计边界（并行纪律）：本系统只做「监视存活 → 间奏 → 生成下一兽」，不触碰任何圣兽自身的
    /// AI/掉落/downed 标记 —— 每兽的入场/死亡演出由其自身状态机完成，本系统只是报幕人。
    /// 逻辑全部服务器权威（<c>Main.netMode != MultiplayerClient</c>）；播报经本地化键广播；
    /// 仪式状态不落盘（跨存档中断即取消），无需额外 netcode。
    /// </summary>
    public class FourSymbolsRiteSystem : ModSystem
    {
        /// <summary>间奏时长（上一兽陨落 → 下一兽降临）。</summary>
        private const int InterludeTicks = 240;

        /// <summary>仪式是否进行中（服务器权威；客户端只在物品 CanUseItem 里参考本地近似判断）。</summary>
        public static bool RiteActive { get; private set; }

        private static int stage;              // 0=青龙 1=朱雀 2=白虎 3=玄武
        private static int interludeTimer;     // >0 表示处于间奏倒计时
        private static int watchedNpc = -1;    // 当前在场圣兽的 NPC 槽位
        private static int watchedType;        // 槽位复用校验
        private static int watchedLifeMax = 1; // 生成时血量上限（离场后判定用，槽位可能已被复用）
        private static int lastObservedLife;   // 兽消失瞬间用于区分「被击败」与「脱战消失」
        private static bool downedSnapshot;    // 生成时 downed 标记快照（首杀场景的可靠击败信号）

        private static readonly Color RiteGold = new(255, 225, 130);

        /// <summary>四兽方位序（类型 + 播报键后缀 + 方位主题色）。</summary>
        private static (int type, string key, Color color) StageInfo(int s) => s switch {
            0 => (ModContent.NPCType<Qinlong>(), "East", new Color(80, 235, 150)),
            1 => (ModContent.NPCType<Suzaku>(), "South", new Color(255, 120, 60)),
            2 => (ModContent.NPCType<Baihu>(), "West", new Color(225, 235, 245)),
            _ => (ModContent.NPCType<Xuanwu>(), "North", new Color(140, 199, 242)),
        };

        private static bool DownedFlagFor(int s) => s switch {
            0 => Systems.DownedBossSystem.downedQinlong,
            1 => Systems.DownedBossSystem.downedSuzaku,
            2 => Systems.DownedBossSystem.downedBaihu,
            _ => Systems.DownedBossSystem.downedXuanwu,
        };

        /// <summary>任一四圣兽当前在场（物品端拦截重复召唤用，客户端可安全调用）。</summary>
        public static bool AnySacredBeastAlive =>
            NPC.AnyNPCs(ModContent.NPCType<Qinlong>()) ||
            NPC.AnyNPCs(ModContent.NPCType<Suzaku>()) ||
            NPC.AnyNPCs(ModContent.NPCType<Baihu>()) ||
            NPC.AnyNPCs(ModContent.NPCType<Xuanwu>());

        /// <summary>
        /// 服务器端开始仪式。已在进行/有圣兽在场则拒绝（返回 false）。
        /// </summary>
        public static bool TryStartRite(Player user) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return false;
            if (RiteActive || AnySacredBeastAlive)
                return false;

            RiteActive = true;
            stage = 0;
            interludeTimer = 0;
            Announce("Begin", RiteGold);
            SpawnStageBoss(user);
            return true;
        }

        public override void OnWorldUnload() => CancelRite();

        private static void CancelRite() {
            RiteActive = false;
            stage = 0;
            interludeTimer = 0;
            watchedNpc = -1;
        }

        public override void PostUpdateNPCs() {
            if (!RiteActive || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 全员离场/死绝 → 仪式静默中止（圣兽自身会脱战升天）
            if (!AnyActivePlayer()) {
                CancelRite();
                return;
            }

            // —— 间奏：倒计时后召下一兽 ——
            if (interludeTimer > 0) {
                interludeTimer--;
                if (interludeTimer == 0) {
                    Player target = NearestActivePlayer();
                    if (target == null) { CancelRite(); return; }
                    SpawnStageBoss(target);
                }
                return;
            }

            // —— 监视当前在场圣兽 ——
            if (watchedNpc < 0)
                return;
            NPC npc = Main.npc[watchedNpc];
            if (npc.active && npc.type == watchedType) {
                lastObservedLife = npc.life;
                return;
            }

            // 兽已离场：downed 标记翻转（首杀）或临终血量极低（复战）判定为「被击败」，否则视为脱战、仪式中断
            bool defeated = (!downedSnapshot && DownedFlagFor(stage)) ||
                            lastObservedLife <= watchedLifeMax / 20;
            watchedNpc = -1;

            if (!defeated) {
                Announce("Broken", Color.Gray);
                CancelRite();
                return;
            }

            if (stage >= 3) {
                // 玄武陨落 → 四象归位，仪式圆满
                Announce("Complete", RiteGold);
                CancelRite();
                return;
            }

            // 报幕：X方已定 → 间奏后召下一兽
            Announce("Fall" + StageInfo(stage).key, StageInfo(stage).color);
            stage++;
            interludeTimer = InterludeTicks;
        }

        private static void SpawnStageBoss(Player target) {
            (int type, string key, Color color) = StageInfo(stage);
            // 各圣兽 Intro 状态会在首帧自行落位（相对目标定位），此处出生点只需在附近
            int idx = NPC.NewNPC(target.GetSource_FromThis(), (int)target.Center.X, (int)target.Center.Y - 600, type);
            if (idx < 0 || idx >= Main.maxNPCs) {
                CancelRite();
                return;
            }
            watchedNpc = idx;
            watchedType = type;
            watchedLifeMax = Main.npc[idx].lifeMax;
            lastObservedLife = Main.npc[idx].lifeMax;
            downedSnapshot = DownedFlagFor(stage);
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: idx);
            Announce("Rise" + key, color);
        }

        private static bool AnyActivePlayer() {
            foreach (Player p in Main.ActivePlayers) {
                if (!p.dead)
                    return true;
            }
            return false;
        }

        private static Player NearestActivePlayer() {
            foreach (Player p in Main.ActivePlayers) {
                if (!p.dead)
                    return p;
            }
            return null;
        }

        /// <summary>本地化播报（单机直显 / 服务器广播）。键区：Mods.AncientChineseMythology.Misc.FourSymbolsRite.*</summary>
        private static void Announce(string suffix, Color color) {
            string key = "Mods.AncientChineseMythology.Misc.FourSymbolsRite." + suffix;
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(Language.GetTextValue(key), color);
            else if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);
        }
    }
}

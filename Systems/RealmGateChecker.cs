using Terraria;

namespace AncientChineseMythology.Systems
{
    /// <summary>修仙大境界突破门控 G0–G7（spec §3.2）。</summary>
    public static class RealmGateChecker
    {
        /// <summary>
        /// 检测从 <paramref name="currentMajor"/> 突破至下一境界是否满足 Boss 进度。
        /// </summary>
        public static bool CanAdvance(int currentMajor, out string failReason) {
            int targetMajor = currentMajor + 1;
            failReason = null;

            switch (targetMajor) {
                case 1: // G0 → 炼气化神
                    return true;
                case 2:
                case 3: // 炼神返虚 / 炼虚合道 — 无额外 Boss 门控
                    return true;
                case 4: // G1 → 人仙
                    if (!DownedBossSystem.downedBlackBear) {
                        failReason = "需击败黑熊精方可引劫破境。";
                        return false;
                    }
                    return true;
                case 5: // G2 → 地仙
                    if (!Main.hardMode) {
                        failReason = "需击败血肉墙，方入地仙之境。";
                        return false;
                    }
                    return true;
                case 6: // G3 → 天仙
                    if (!NPC.downedPlantBoss) {
                        failReason = "需击败世纪之花，方可冲击天仙。";
                        return false;
                    }
                    return true;
                case 7: // G4 → 金仙
                    if (!DownedBossSystem.downedKyuubi) {
                        failReason = "需击败九尾妖狐，方可冲击金仙。";
                        return false;
                    }
                    return true;
                case 8: // G5 → 太乙
                    if (!NPC.downedMoonlord) {
                        failReason = "需击败月亮领主，方可冲击太乙。";
                        return false;
                    }
                    if (!AnyFourZombieDowned()) {
                        failReason = "需击败四大僵尸之一（旱魃/后卿/赢勾/将臣）。";
                        return false;
                    }
                    if (!DownedBossSystem.downedDazheng) {
                        failReason = "需击败大椿，方可冲击太乙。";
                        return false;
                    }
                    return true;
                case 9: // G6 → 大罗
                    if (!DownedBossSystem.downedNetherDragon && !DownedBossSystem.downedCelestialDragon) {
                        failReason = "需击败幽冥龙或天御金龙，方可冲击大罗。";
                        return false;
                    }
                    return true;
                case 10: // G7 → 准圣
                    if (!DownedBossSystem.downedYinEmperor) {
                        failReason = "需击败阴天子，方可冲击准圣。";
                        return false;
                    }
                    if (!DownedBossSystem.downedAzureDragon) {
                        failReason = "需击败苍龙真身，方可冲击准圣。";
                        return false;
                    }
                    return true;
                default:
                    return true;
            }
        }

        private static bool AnyFourZombieDowned()
            => DownedBossSystem.downedHanba
            || DownedBossSystem.downedHoqing
            || DownedBossSystem.downedYingou
            || DownedBossSystem.downedJiangcen;
    }
}

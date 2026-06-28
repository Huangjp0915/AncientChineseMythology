using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡全局 NPC 钩子 ——
    /// 1. Boss 击杀时解锁灵魂上限阶层
    /// 2. 被万魂幡弹幕击杀的敌人触发灵魂吸收
    /// </summary>
    public class SoulBannerGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (Main.netMode == NetmodeID.Server)
                return;

            int killerIndex = npc.lastInteraction;
            if (killerIndex < 0 || killerIndex >= Main.maxPlayers)
                return;

            Player killer = Main.player[killerIndex];
            if (!killer.active)
                return;

            var sbPlayer = killer.GetModPlayer<SoulBannerPlayer>();

            // ── Boss 击杀 → 解锁灵魂上限 ──
            if (npc.boss) {
                int newCap = sbPlayer.TryUnlockBossTier(npc.type);
                if (newCap > 0 && Main.myPlayer == killerIndex) {
                    Main.NewText($"[万魂幡] 幡旗共鸣！灵魂上限提升至 {newCap}", 180, 80, 255);
                }
            }

            // ── 吸魂击杀 → 累积灵魂（Boss 仅提升上限，不给灵魂）──
            // 判定：击杀者当前持有万魂幡，且非 Boss（Boss 只解锁阶层）
            if (!npc.boss && !npc.friendly && !npc.SpawnedFromStatue && IsHoldingSoulBanner(killer)) {
                int gained = sbPlayer.AbsorbSoul(npc);
                if (gained > 0 && Main.myPlayer == killerIndex) {
                    CombatText.NewText(npc.Hitbox, new Microsoft.Xna.Framework.Color(180, 100, 255),
                        $"+{gained} 魂", true);
                }
            }
        }

        private static bool IsHoldingSoulBanner(Player player) {
            // 手持万魂幡
            if (player.HeldItem != null && player.HeldItem.type == ModContent.ItemType<SoulBanner>())
                return true;

            // 场上有万魂幡弹幕（minion 或 held proj）
            int heldType = ModContent.ProjectileType<SoulBannerHeldProj>();
            int minionType = ModContent.ProjectileType<SoulBannerMinion>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI
                    && (proj.type == heldType || proj.type == minionType))
                    return true;
            }

            return false;
        }
    }
}

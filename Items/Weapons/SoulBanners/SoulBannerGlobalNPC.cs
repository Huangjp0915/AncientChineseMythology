using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡全局 NPC 钩子 ——
    /// 1. Boss 死亡时解锁灵魂上限阶层
    /// 2. 被击杀的敌人向持幡者提供灵魂 + 生成"灵魂归幡"飞线
    ///
    /// 多人安全：改走 <see cref="GlobalNPC.HitEffect"/>（每个客户端都会随击打同步回放，
    /// 含致死一击），以 <c>Main.myPlayer == npc.lastInteraction</c> 守卫——灵魂数据只在
    /// 击杀者本地结算（旧版 OnKill 只在服务器执行却被 server-return 拦截，联机下完全失效）。
    /// 性能：仅受击帧触发；持幡判定用 ownedProjectileCounts O(1)，不再扫全弹幕数组。
    /// </summary>
    public class SoulBannerGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>防止同一 NPC 的致死回放触发多次结算</summary>
        private bool deathHandled;

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            // 只关心致死一击；服务器无灵魂数据
            if (Main.dedServ || npc.life > 0 || deathHandled)
                return;
            deathHandled = true;

            int killerIndex = npc.lastInteraction;
            if (killerIndex != Main.myPlayer || killerIndex < 0 || killerIndex >= Main.maxPlayers)
                return;

            Player killer = Main.player[killerIndex];
            if (!killer.active || killer.dead)
                return;

            var sbPlayer = killer.GetModPlayer<SoulBannerPlayer>();

            // ── Boss 击杀 → 解锁灵魂上限（无需手持幡）──
            if (npc.boss) {
                int newCap = sbPlayer.TryUnlockBossTier(npc.type);
                if (newCap > 0)
                    Main.NewText($"[万魂幡] 幡旗共鸣！灵魂上限提升至 {newCap}", 180, 80, 255);
            }

            // ── 吸魂击杀 → 累积灵魂（Boss 仅提升上限；雕像怪/小动物不给魂）──
            if (npc.boss || npc.friendly || npc.SpawnedFromStatue || NPCID.Sets.CountsAsCritter[npc.type] || npc.lifeMax <= 5)
                return;
            if (!IsWieldingSoulBanner(killer))
                return;

            int gained = sbPlayer.AbsorbSoul(npc);
            if (gained <= 0)
                return;

            sbPlayer.RegisterGain(gained);
            CombatText.NewText(npc.Hitbox, new Color(180, 100, 255), $"+{gained} 魂", true);

            // 灵魂归幡飞线（纯视觉，同屏限流）
            if (killer.ownedProjectileCounts[ModContent.ProjectileType<SoulWispVFX>()] < 12) {
                Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<SoulWispVFX>(), 0, 0f, killerIndex,
                    npc.Center.X, npc.Center.Y);
            }
        }

        /// <summary>持幡判定：手持万魂幡，或场上有自己的手持弹幕/悬浮幡（O(1)）。</summary>
        private static bool IsWieldingSoulBanner(Player player) {
            if (player.HeldItem != null && player.HeldItem.type == ModContent.ItemType<SoulBanner>())
                return true;
            return player.ownedProjectileCounts[ModContent.ProjectileType<SoulBannerHeldProj>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<SoulBannerMinion>()] > 0;
        }
    }
}

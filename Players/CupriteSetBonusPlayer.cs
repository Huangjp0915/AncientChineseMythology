//Players/CupriteSetBonusPlayer.cs
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Players
{
    public class CupriteSetBonusPlayer : ModPlayer
    {
        ///<summary>在三件套装备检查里设为 true。</summary>
        public bool cupriteSet;

        public override void ResetEffects() {
            cupriteSet = false;
        }

        //────────── 近战或接触伤害 ──────────
        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) {
            TryInflictBurn(npc);
        }

        //────────── 远程 / 魔法 / 投射物伤害 ──────────
        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) {
            if (!cupriteSet || proj.friendly || !proj.hostile) return;

            int npcIndex = ResolveShooterIndex(proj);
            if (npcIndex != -1) TryInflictBurn(Main.npc[npcIndex]);
        }

        //根据 owner / ai[0] / ai[1] 三种惯例尝试找出发射者
        private static int ResolveShooterIndex(Projectile proj) {
            //最常见：npcProj==true 且 owner 保存 NPC 索引 :contentReference[oaicite:2]{index=2}
            if (proj.npcProj && proj.owner >= 0 && proj.owner < Main.maxNPCs)
                return proj.owner;

            //次常见：owner = 255 或 -1，但 ai[0] 写了索引（如食人花种子、亡灵骷髅骨头等）
            int ai0 = (int)proj.ai[0];
            if (ai0 >= 0 && ai0 < Main.maxNPCs) return ai0;

            //个别 Boss（如巨鹿）会占用 ai[1]
            int ai1 = (int)proj.ai[1];
            if (ai1 >= 0 && ai1 < Main.maxNPCs) return ai1;

            return -1; //实在找不到就放弃
        }

        //────────── 公共工具方法 ──────────
        private void TryInflictBurn(NPC npc) {
            if (!cupriteSet || npc.friendly || !npc.active)
                return;

            if (Main.rand.NextFloat() < CupriteArmorConstants.BurnChance) {
                npc.AddBuff(BuffID.OnFire, CupriteArmorConstants.BurnDurationTicks);
            }
        }
    }
}

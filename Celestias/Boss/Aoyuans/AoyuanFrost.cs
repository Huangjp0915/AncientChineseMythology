using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 永冻立场（Permafrost Field）的玩家侧状态：冰冻叠层 / 冻结 / 地面打滑
    /// 由 AoyuanPermafrostTrail / AoyuanAbsoluteZeroBurst 在各客户端对本地玩家施加
    /// </summary>
    public class AoyuanFrostPlayer : ModPlayer
    {
        /// <summary>当前冰冻叠层（0~3）</summary>
        public int chillStacks;
        /// <summary>冰冻残留计时（离开地痕后仍减速一段时间）</summary>
        public int chillTimer;
        /// <summary>完全冻结计时（无法移动）</summary>
        public int frozenTimer;
        /// <summary>地面打滑计时（二阶段破境）</summary>
        public int slipperyTimer;

        /// <summary>叠层节流，避免每帧暴涨</summary>
        private int chillAddCooldown;

        /// <summary>叠加一层冰冻；满3层触发短暂冻结</summary>
        public void AddChill() {
            chillTimer = 120;
            if (frozenTimer > 0) return;
            if (chillAddCooldown > 0) return;
            chillAddCooldown = 28;

            chillStacks++;
            if (chillStacks >= 3) {
                frozenTimer = 70;
                chillStacks = 0;
                chillTimer = 0;
            }
        }

        public override void ResetEffects() {
            // 计时与叠层在 PostUpdate 维护，这里不清零
        }

        public override void PostUpdate() {
            if (chillAddCooldown > 0)
                chillAddCooldown--;

            if (chillTimer > 0) {
                chillTimer--;
                Player.AddBuff(ModContent.BuffType<AoyuanChill>(), 3);
                if (chillTimer == 0)
                    chillStacks = 0;
            }

            if (frozenTimer > 0) {
                frozenTimer--;
                Player.AddBuff(ModContent.BuffType<AoyuanDeepFreeze>(), 3);
            }

            if (slipperyTimer > 0) {
                slipperyTimer--;
                Player.AddBuff(ModContent.BuffType<AoyuanSlippery>(), 3);
            }
        }
    }

    /// <summary>冰冻 - 减速，随叠层加深</summary>
    public class AoyuanChill : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            var fp = player.GetModPlayer<AoyuanFrostPlayer>();
            int stacks = Math.Max(fp.chillStacks, 1);
            float factor = Math.Clamp(1f - 0.16f * stacks, 0.4f, 1f);
            player.maxRunSpeed *= factor;
            player.runAcceleration *= factor;
            player.moveSpeed -= 0.08f * stacks;
        }
    }

    /// <summary>深度冻结 - 完全无法移动（约1秒）</summary>
    public class AoyuanDeepFreeze : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.frozen = true;
        }
    }

    /// <summary>地面打滑 - 二阶段破境后场地结冰</summary>
    public class AoyuanSlippery : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.slippy = true;
        }
    }
}

using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子审判系统玩家组件 —— 承载“冥律标记 / 定魂 / 阴阳判罚 / 酆都处决”签名机制。
    /// 这是阴天子作为“机制/审判 Boss”的核心：玩家不是靠抗伤，而是要在审判循环中存活，
    /// 并通过控制被标记的节奏来避免“定魂”，以及在阴阳诏书时站对半场。
    /// </summary>
    public class YinJudgmentPlayer : ModPlayer
    {
        /// <summary>冥律满层触发定魂所需层数（已统一到共享身份层，保留常量供旧引用）。</summary>
        public const int MaxDecreeStacks = UnderworldFieldPlayer.MaxDecree;

        // —— 冥律标记 / 定魂 已统一到共享地府身份层 UnderworldFieldPlayer ——
        // 以下访问器读取共享层，使阴天子既有的层数/定魂读取点（若有）行为完全保留。
        /// <summary>当前冥律层数（读取共享身份层）。</summary>
        public int decreeStacks => Player.GetModPlayer<UnderworldFieldPlayer>().DecreeStacks;
        /// <summary>定魂预告计时（读取共享身份层）。</summary>
        public int dingHunTelegraph => Player.GetModPlayer<UnderworldFieldPlayer>().DingHunTelegraph;
        /// <summary>定魂锁定计时（读取共享身份层）。</summary>
        public int dingHunLock => Player.GetModPlayer<UnderworldFieldPlayer>().DingHunLock;

        /// <summary>是否装备“酆都”套（G7 处决资格）。每帧由护甲/饰品重新置位。</summary>
        public bool fengduSetActive;

        private int yinYangDoT;

        public override void ResetEffects() {
            fengduSetActive = false;
        }

        /// <summary>被冥眼 / 封印 / 帝冥弹命中时叠加冥律（委托共享身份层，调参/定魂行为不变）。</summary>
        public void AddDecreeStack(int amount = 1) =>
            Player.GetModPlayer<UnderworldFieldPlayer>().AddNetherDecree(amount);

        public override void PostUpdate() {
            // 冥律/定魂由共享身份层在其 PostUpdate 处理；阴天子仅保留阴阳半场判罚。
            UpdateYinYangJudgment();
        }

        private void UpdateYinYangJudgment() {
            if (!YinEmperor.YinYangActive) {
                yinYangDoT = 0;
                return;
            }

            bool onYangSide = Player.Center.X >= YinEmperor.YinYangCenterX;
            int playerSide = onYangSide ? 1 : 0;
            bool wrongHalf = playerSide != YinEmperor.YinYangSafeSide;

            if (!wrongHalf) {
                yinYangDoT = 0;
                return;
            }

            // 站错半场 -> 酆帝诏书灼魂 DoT
            yinYangDoT++;
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(26, 34),
                    YinEmperor.YinYangSafeSide == 1 ? DustID.Shadowflame : DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = new Vector2(0, -2f);
            }

            if (yinYangDoT >= 22 && Player.whoAmI == Main.myPlayer) {
                yinYangDoT = 0;
                int dmg = Main.masterMode ? 70 : Main.expertMode ? 55 : 40;
                if (Player.statLife > 0 && !Player.dead) {
                    Player.statLife -= dmg;
                    CombatText.NewText(Player.Hitbox, YinEmperorHelper.NetherBloodRed, dmg.ToString());
                    if (Player.statLife <= 0) {
                        Player.KillMe(PlayerDeathReason.ByCustomReason(
                            NetworkText.FromLiteral(Player.name + " was judged by the Yin Emperor's decree.")), dmg, 0);
                    }
                }
            }
        }
    }
}

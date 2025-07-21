using AncientChineseMythology.Players;
using AncientChineseMythology.Projectiles;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    public class TribulationCloudBlack : ModNPC
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_black";

        private const int TotalStrikes = 18;   // 总攻击次数
        private const int StrikeInterval = 120; // 2 秒（60 帧 = 1 s）
        private int attackTimer;
        private int strikesDone;
        private bool tribulationEnded = false;   // 防止重复结算
        private const int BaseStrikeDamage = 40;   // 所有难度共同的基础值
        private const int PerMajorIncrement = 60;   // 每提升 1 大境界额外加多少
        private const int PerStrikeIncrement = 30;   // 每多 1 道闪电额外加多少

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.lifeMax = 2_000_000;
            NPC.damage = 0;                    // 本体不造成接触伤害
            NPC.defense = 100;
            NPC.dontTakeDamage = true;              // 完全免疫所有外部伤害
            NPC.dontTakeDamageFromHostiles = true;  // 避免被其它怪/炮台误伤
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            Music = MusicID.Boss3;
            NPC.value = Item.buyPrice(0, 25, 0, 0);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void AI() {
            // 确保锁定一个有效目标
            if (!Main.player[NPC.target].active || Main.player[NPC.target].dead)
                NPC.TargetClosest();

            Player player = Main.player[NPC.target];

            // 若已完成结算，直接消失
            if (tribulationEnded) {
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // 玩家死亡 ⇒ 失败
            if (player.dead) {
                FailTribulation(player);
                tribulationEnded = true;
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // 9 次闪电后玩家仍存活 ⇒ 成功
            if (strikesDone >= TotalStrikes) {
                SuccessTribulation(player);
                tribulationEnded = true;
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // -------- 悬浮跟随 --------
            Vector2 desiredPos = player.Center + new Vector2(0f, -240f);
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.12f);

            // -------- 攻击逻辑 --------
            if (strikesDone < TotalStrikes) {
                attackTimer++;
                if (attackTimer >= StrikeInterval) {
                    attackTimer = 0;
                    DoLightningStrike(player);
                }
            }
        }


        private void DoLightningStrike(Player player) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();
            int damage = BaseStrikeDamage + PerMajorIncrement ^ mp.Major +
                         PerStrikeIncrement * strikesDone;

            // 难度系数
            if (Main.masterMode)
                damage = (int)(damage * 1.6f);
            else if (Main.expertMode)
                damage = (int)(damage * 1.3f);

            // 获取 IEntitySource —— 用本 NPC 的 AI 源
            IEntitySource src = NPC.GetSource_FromAI();

            // 生成闪电投射物（起点 = 劫云中心，速度 0）
            int projID = Projectile.NewProjectile(
                src,                         // 生成来源
                NPC.Center,                  // 起点（劫云自身）
                Vector2.Zero,                // 初速度由 Proj2 AI 控制
                ModContent.ProjectileType<TribulationLightningBlack>(),
                damage, 2f,                  // 伤害 & 击退
                Main.myPlayer,               // owner
                NPC.whoAmI                   // ai[0] = 劫云 ID，供 Proj2 读取
            );

            //多人模式同步
            if (projID >= 0 && Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncProjectile, number: projID);

            //远距离轰鸣声
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 1.2f }, player.Center);

            strikesDone++;                  // 记得在外部调用后递增已劈次数
        }

        public override void OnKill() {
            Player p = Main.player[NPC.target];
            if (p.active)
                p.GetModPlayer<MythologyPlayer>().AdvanceMajor(p); // 正式突破
        }

        private void FailTribulation(Player player) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();

            if (mp.Minor > 0) {
                mp.Minor--;              // 小境界 -1，至少保底 0
                mp.StageExp = 0;
            }
            SoundEngine.PlaySound(SoundID.Item62, player.Center);
            Main.NewText($"{player.name} 的渡劫失败，小境界下降！", Color.OrangeRed);
        }

        private void SuccessTribulation(Player player) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();

            mp.Major++;        // 大境界 +1
            mp.Minor = 0;      // 重置小境界
            mp.StageExp = 0;   // 清经验
            mp.KillsThisMajor = 0;

            mp.ApplyMajorBonus();                      // 发放一次性奖励

            SoundEngine.PlaySound(SoundID.Roar, player.Center);
            Main.NewText($"{player.name} 成功渡过劫云，突破到新的大境界！", Color.Gold);
        }
    }
}

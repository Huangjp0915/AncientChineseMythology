using AncientChineseMythology.Players;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>渡劫三色 (黑/赤/紫) 的劫云种类 —— 决定落雷的预警节奏与考验模式。</summary>
    public enum TribulationKind
    {
        /// <summary>玄雷: 固定节奏点雷 (教学计时) —— 站位被记录, 蓄力后原地落雷, 节奏稳定可预读。</summary>
        Black,
        /// <summary>赤雷: 佯攻点雷 —— 假蓄力 (非红) 诱你提前闪避, 0.5s 后真雷追到你新站位 (红色) 才落。</summary>
        Red,
        /// <summary>紫霄: 移动安全区扫雷 —— 一道雷幕横扫, 缝隙(法眼)是唯一安全带, 玩家须站进缝里。</summary>
        Purple
    }

    /// <summary>
    /// 劫云 (Tribulation Cloud) V2 共享基类 —— 把三份近乎复制粘贴的 Black/Red/Purple 合并为参数化逻辑,
    /// 三色仅以 <see cref="Kind"/> / <see cref="ThemeColor"/> / 落雷次数区分, 杜绝复制粘贴漂移。
    ///
    /// <para><b>本质:</b> 这是修真渡劫的<b>生存仪式事件</b>而非 DPS Boss —— 云体完全免伤, 撑过 N 记天雷=突破,
    /// 玩家死亡=境界跌。深度来自<b>可读的落雷躲避模式</b>而非血量:</para>
    /// <list type="bullet">
    ///   <item>黑=固定节奏 (教计时)</item>
    ///   <item>赤=佯攻 (假蓄力 + 0.5s 后真落)</item>
    ///   <item>紫=移动安全区 (雷幕扫, 站进缝)</item>
    /// </list>
    ///
    /// <para><b>修真集成 (必须原样保留):</b> <see cref="SuccessTribulation"/> 直接推进 Major / 重置 Minor /
    /// 发奖, <see cref="FailTribulation"/> 跌小境界; <see cref="OnKill"/> 调 <see cref="MythologyPlayer.AdvanceMajor"/>。
    /// 天气钩子 <see cref="TribulationWeather"/> 与生成钩子 (MythologySidebar / TribulationSpawnSystem) 不变。</para>
    ///
    /// <para><b>演出:</b> 每记落雷严格预警 (TelegraphColors.Lethal 红 + ArenaRunic 落点法阵 + DrawBeam 雷柱 +
    /// RadialBloom 命中泛光); 紫扫雷用 DrawBeam 雷幕; 风暴压暗经 <see cref="TribulationScreenSystem"/>
    /// (ElementalScreenTint, 不占全屏后处理名额)。震屏走 <see cref="ACMScreenShakeSystem"/>, 受 MythologyConfig 降级。</para>
    /// </summary>
    public abstract class TribulationCloudBase : ModNPC
    {
        // —— 子类参数化点 ——
        /// <summary>劫云种类 —— 决定落雷模式 (黑/赤/紫)。</summary>
        public abstract TribulationKind Kind { get; }
        /// <summary>主题色 (天幕染屏 / 落点法阵副色 / 氛围)。红只留给致命预警, 此色为非红主题色。</summary>
        public abstract Color ThemeColor { get; }
        /// <summary>本场渡劫总落雷数 (生成时随机/固定) —— 子类决定考验长度。</summary>
        protected abstract int RollTotalStrikes();

        // —— 伤害公式 (三色统一; 一迭代点名 XOR→加法, 此处即加法) ——
        private const int BaseStrikeDamage = 40;    // 所有难度共同的基础值
        private const int PerMajorIncrement = 60;   // 每提升 1 大境界额外加多少
        private const int PerStrikeIncrement = 30;  // 每多 1 道闪电额外加多少

        // —— 节奏 (三波渐强: 试探→紧逼→终雷, 波间留喘息) ——
        private static readonly int[] WaveInterval = { 110, 80, 60 }; // 各波落雷间隔(tick)
        private const int WaveBreather = 55;        // 换波时额外喘息(tick)
        private const int FinalCharge = 45;         // 终雷额外预蓄(tick)

        private int TotalStrikes;
        private int attackTimer;
        private int strikesDone;
        private int lastWave = -1;
        private bool tribulationEnded = false;      // 防止重复结算

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.lifeMax = 2_000_000;
            NPC.damage = 0;                         // 本体不造成接触伤害
            NPC.defense = 100;
            NPC.dontTakeDamage = true;              // 完全免疫所有外部伤害 (生存事件)
            NPC.dontTakeDamageFromHostiles = true;  // 避免被其它怪/炮台误伤
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            Music = MusicID.Boss3;
            NPC.value = Item.buyPrice(0, 25, 0, 0);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            TotalStrikes = System.Math.Max(3, RollTotalStrikes());
        }

        public override void AI() {
            // 锁定有效目标
            if (!Main.player[NPC.target].active || Main.player[NPC.target].dead)
                NPC.TargetClosest();

            Player player = Main.player[NPC.target];

            // 已结算 → 直接消失
            if (tribulationEnded) {
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // ① 玩家死亡 ⇒ 失败 (境界跌)
            if (player.dead) {
                FailTribulation(player);
                tribulationEnded = true;
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // ② 撑过全部落雷 ⇒ 成功 (突破)
            if (strikesDone >= TotalStrikes) {
                SuccessTribulation(player);
                tribulationEnded = true;
                NPC.active = false;
                TribulationWeather.Stop();
                return;
            }

            // 风暴压暗氛围 (本地视觉, 同帧取 max; 越接近终雷越浓)
            float stormI = MathHelper.Lerp(0.32f, 0.6f, strikesDone / (float)System.Math.Max(1, TotalStrikes));
            TribulationScreenSystem.Publish(ThemeColor, stormI);

            // 悬浮跟随 (略带飘移, 渡劫之云压顶)
            Vector2 desiredPos = player.Center + new Vector2(0f, -300f);
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.08f);
            NPC.rotation = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 0.6f) * 0.04f;

            // —— 三波渐强落雷调度 ——
            int wave = CurrentWave();
            if (wave != lastWave) {
                // 换波: 留一口喘息, 表达"波次推进"的呼吸节奏
                if (lastWave >= 0)
                    attackTimer = -WaveBreather;
                lastWave = wave;
            }

            bool isFinal = strikesDone == TotalStrikes - 1;
            int interval = WaveInterval[wave] + (isFinal ? FinalCharge : 0);

            attackTimer++;
            if (attackTimer >= interval) {
                attackTimer = 0;
                DoLightningStrike(player, isFinal);
            }
        }

        /// <summary>当前波次 0/1/2 (试探/紧逼/终雷)。</summary>
        private int CurrentWave() {
            int t1 = System.Math.Max(1, TotalStrikes / 3);
            int t2 = System.Math.Max(t1 + 1, TotalStrikes * 2 / 3);
            if (strikesDone < t1) return 0;
            if (strikesDone < t2) return 1;
            return 2;
        }

        private void DoLightningStrike(Player player, bool isFinal) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();
            int damage = BaseStrikeDamage + PerMajorIncrement * mp.Major + PerStrikeIncrement * strikesDone;

            // 难度系数
            if (Main.masterMode)
                damage = (int)(damage * 1.6f);
            else if (Main.expertMode)
                damage = (int)(damage * 1.3f);

            // 终雷更重 (但仍是可撑过的考验, 不喧宾夺主成 DPS)
            if (isFinal)
                damage = (int)(damage * 1.5f);

            IEntitySource src = NPC.GetSource_FromAI();
            int owner = NPC.target;     // 渡劫者本人 (供弹幕追踪/伤害归属, 多人安全)

            int projID;
            if (Kind == TribulationKind.Purple) {
                // 紫: 移动安全区雷幕扫 —— 站进缝(法眼)。flags: bit1=终雷
                int flags = isFinal ? StrikeFlags.Final : 0;
                projID = Projectile.NewProjectile(src, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TribulationSweep>(),
                    damage, 0f, owner,
                    ai0: NPC.whoAmI, ai1: flags, ai2: (int)ThemeColorPacked());
            }
            else {
                // 黑: 固定节奏点雷 / 赤: 佯攻点雷。flags: bit0=佯攻 bit1=终雷
                int flags = isFinal ? StrikeFlags.Final : 0;
                if (Kind == TribulationKind.Red)
                    flags |= StrikeFlags.Feint;
                projID = Projectile.NewProjectile(src, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TribulationLightningStrike>(),
                    damage, 0f, owner,
                    ai0: NPC.whoAmI, ai1: flags, ai2: (int)ThemeColorPacked());
            }

            // 多人同步
            if (projID >= 0 && Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncProjectile, number: projID);

            // 远处雷鸣 (预警之声; 终雷更沉)
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = isFinal ? 1.45f : 1.1f, Pitch = isFinal ? -0.5f : 0f }, player.Center);

            strikesDone++;
        }

        /// <summary>把主题色打包成可经 ai 传递的 float (RGB 24bit)。供弹幕侧解出非红主题副色。</summary>
        private float ThemeColorPacked() => (ThemeColor.R << 16) | (ThemeColor.G << 8) | ThemeColor.B;

        // ===== 修真结算钩子 (原样保留, 切勿改语义) =====

        public override void OnKill() {
            Player p = Main.player[NPC.target];
            if (p.active)
                p.GetModPlayer<MythologyPlayer>().AdvanceMajor(p); // 正式突破
        }

        private void FailTribulation(Player player) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();

            if (mp.Minor > 0) {
                mp.Minor--;              // 小境界 -1, 至少保底 0
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

            // 金光冲天 (成功的有重量收尾): 生成一枚纯演出的庆典泛光弹 (damage=0)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int id = Projectile.NewProjectile(NPC.GetSource_Death(), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TribulationLightningStrike>(),
                    0, 0f, NPC.target,
                    ai0: NPC.whoAmI, ai1: StrikeFlags.SuccessFinale, ai2: 0);
                if (id >= 0 && Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncProjectile, number: id);
            }
        }
    }

    /// <summary>劫雷弹幕 ai[1] 标志位 (经 NewProjectile 传入)。</summary>
    public static class StrikeFlags
    {
        public const int Feint = 1;          // bit0: 赤雷佯攻
        public const int Final = 2;          // bit1: 终雷 (更强预警/泛光/震屏)
        public const int SuccessFinale = 4;  // bit2: 渡劫成功金光 (纯演出, 无伤)
    }
}

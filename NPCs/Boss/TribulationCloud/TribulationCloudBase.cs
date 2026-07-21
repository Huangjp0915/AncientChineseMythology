using AncientChineseMythology.Players;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>渡劫三色 (黑/赤/紫) 的劫云种类 —— 决定落雷的预警节奏与考验模式。</summary>
    public enum TribulationKind
    {
        /// <summary>玄雷: 固定节奏点雷 (教学计时) —— 站位被记录, 蓄力后原地落雷; 中段双联雷考校两次走位。</summary>
        Black,
        /// <summary>赤雷: 佯攻点雷 —— 假蓄力 (非红) 诱你提前闪避, 真雷追到你新站位 (红色) 才落; 终局双重佯攻。</summary>
        Red,
        /// <summary>紫霄: 移动安全区扫雷 —— 雷幕横扫, 缝隙(法眼)是唯一安全带; 中段缝会漂移, 终局双幕对扫。</summary>
        Purple
    }

    /// <summary>
    /// 劫云 (Tribulation Cloud) V3 共享基类 —— 事件型天灾 Boss (渡劫生存仪式, 非 DPS Boss)。
    /// 云体完全免伤, 撑过 ai[3] 记天雷 = 突破大境界; 玩家死亡 = 小境界跌。
    ///
    /// <para><b>状态机 (多人安全, 全走 npc.ai[]):</b>
    /// ai[0]=状态 (0 聚云 / 1 天宣 / 2 天罚 / 3 审判(成功) / 4 息怒(失败/失target)),
    /// ai[1]=状态计时器, ai[2]=已落雷数, ai[3]=总雷数 (服务器首帧 roll 后 netUpdate)。
    /// 弹幕仅服务器生成; 血条被征用为"剩余考验"进度条 (免伤, 不会真正被打死)。</para>
    ///
    /// <para><b>修真集成 (必须原样保留):</b> <see cref="SuccessTribulation"/> 直接推进 Major / 重置 Minor /
    /// 发奖, <see cref="FailTribulation"/> 跌小境界; <see cref="OnKill"/> 调 <see cref="MythologyPlayer.AdvanceMajor"/>。
    /// 天气钩子 <see cref="TribulationWeather"/> 与生成钩子 (MythologySidebar / TribulationSpawnSystem) 不变。</para>
    ///
    /// <para><b>演出:</b> 云盖 = TribulationCloudDeck 着色器 (翻滚活云 + 云内电光散射 + 裂开/消散);
    /// 每记落雷走"充能→先导→死寂→轰落→余烬"全链 (TribulationLightningStrike / TribulationSweep);
    /// 轰落瞬间全屏白闪 (TribulationScreenSystem.Flash) + 云体反冲上弹。震屏走 ACMScreenShakeSystem。</para>
    /// </summary>
    public abstract class TribulationCloudBase : ModNPC
    {
        // —— 子类参数化点 ——
        /// <summary>劫云种类 —— 决定落雷模式 (黑/赤/紫)。</summary>
        public abstract TribulationKind Kind { get; }
        /// <summary>主题色 (云体/天幕染屏/氛围)。红只留给致命预警, 此色为非红主题色。</summary>
        public abstract Color ThemeColor { get; }
        /// <summary>本场渡劫总落雷数 (仅服务器调用一次)。</summary>
        protected abstract int RollTotalStrikes();

        // —— 伤害公式 (三色统一) ——
        private const int BaseStrikeDamage = 40;    // 所有难度共同的基础值
        private const int PerMajorIncrement = 60;   // 每提升 1 大境界额外加多少
        private const int PerStrikeIncrement = 30;  // 每多 1 道闪电额外加多少

        // —— 状态 (ai[0]) ——
        private const int StateGather = 0;     // 聚云入场
        private const int StateDecree = 1;     // 天宣 (云内横闪宣告)
        private const int StateTrial = 2;      // 天罚 (三波落雷)
        private const int StateJudge = 3;      // 审判 (成功收尾)
        private const int StateAbort = 4;      // 息怒 (失败/失效收尾)

        private const int GatherDur = 90;
        private const int DecreeDur = 150;
        private const int JudgeDur = 230;
        private const int AbortDur = 46;
        private const int WaveBreather = 55;   // 换波喘息
        private const int FinalPrelude = 70;   // 终雷前奏 (云内预闪四连的额外等待)
        // 终雷前奏预闪节拍 (距落雷时刻的提前量, 间隔加速 22→18→14→12)
        private static readonly int[] PreludeBeats = { 66, 44, 26, 12 };

        private float State { get => NPC.ai[0]; set => NPC.ai[0] = value; }
        private float Timer { get => NPC.ai[1]; set => NPC.ai[1] = value; }
        private int StrikesDone { get => (int)NPC.ai[2]; set => NPC.ai[2] = value; }
        private int TotalStrikes { get => (int)NPC.ai[3]; set => NPC.ai[3] = value; }

        private bool tribulationEnded = false;  // 防止重复结算 (各端本地各自保证一次)

        // —— 纯视觉状态 (客户端, 不参与同步) ——
        private float ambientFlash;             // 云内常态微光
        private float pubFlash;                 // 弹幕本帧发布的电光强度
        private float pubFlashWorldX;           // 电光世界 X
        private ulong pubFlashFrame;
        private float recoilOff;                // 轰落反冲 (向上位移, ×0.85/f 衰减)
        private float breakProgress;            // 云盖裂开 0~1 (审判)
        private float dissolveProgress;         // 云体消散 0~1

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            // 碰撞箱放大到接近云盖视觉尺寸: 本体无接触伤害/免伤, 仅用于绘制裁剪与站位判定
            NPC.width = 760;
            NPC.height = 480;
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
        }

        // ============================================================
        //  AI 状态机
        // ============================================================

        public override void AI() {
            // 锁定有效目标
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
                NPC.TargetClosest(false);

            Player player = Main.player[NPC.target];

            // 保底出口: 世上已无可渡之人 → 息怒散去 (无惩罚)
            if ((!player.active || player.dead) && State != StateAbort && State != StateJudge) {
                if (player.active && player.dead) {
                    // ① 渡劫者身死 ⇒ 失败 (境界跌)
                    FailOnce(player);
                }
                EnterState(StateAbort);
            }

            // 血条 = 剩余考验进度 (免伤 Boss 的血条语义重定义; 下限 1 防 OnKill 误触)
            int total = Math.Max(1, TotalStrikes);
            NPC.life = Math.Max(1, (int)((long)NPC.lifeMax * Math.Max(0, total - StrikesDone) / total));

            // 悬浮跟随 (渡劫之云压顶; 天宣后微微压低, 但保持足够高度让天雷有"自天而降"的长度)
            float hoverY = State == StateGather ? -560f : -520f;
            Vector2 desiredPos = player.Center + new Vector2(0f, hoverY);
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.06f);
            NPC.rotation = MathF.Sin(Main.GlobalTimeWrappedHourly * 0.6f) * 0.03f;

            Timer++;

            switch ((int)State) {
                case StateGather: GatherAI(player); break;
                case StateDecree: DecreeAI(player); break;
                case StateTrial: TrialAI(player); break;
                case StateJudge: JudgeAI(player); break;
                case StateAbort: AbortAI(); break;
                default: EnterState(StateAbort); break;
            }

            // 反冲衰减 (纯视觉)
            recoilOff *= 0.85f;

            // 云体照明 (电光越盛越亮)
            float flash = MathF.Max(ambientFlash, CurrentPublishedFlash());
            Lighting.AddLight(NPC.Center, ThemeColor.ToVector3() * (0.25f + flash * 0.8f));
        }

        private void EnterState(int state) {
            State = state;
            Timer = 0f;
            NPC.netUpdate = true;
        }

        /// <summary>聚云入场: 云体从稀薄聚拢成形 (90f), 分子云向心汇聚。</summary>
        private void GatherAI(Player player) {
            // 服务器首帧 roll 总雷数
            if (TotalStrikes <= 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                TotalStrikes = Math.Max(3, RollTotalStrikes());
                NPC.netUpdate = true;
            }

            float prog = MathHelper.Clamp(Timer / GatherDur, 0f, 1f);
            TribulationScreenSystem.Publish(ThemeColor, 0.35f * prog);
            ambientFlash = 0.05f * prog;

            // 分子云向心汇聚 (converging streaks)
            if (!Main.dedServ && Main.rand.NextFloat() < 0.7f) {
                Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(760f, 320f);
                Vector2 vel = (NPC.Center + Main.rand.NextVector2Circular(300f, 70f) - from) * 0.05f;
                Dust d = Dust.NewDustPerfect(from, DustID.Smoke, vel, 160, ThemeColor, 1.7f);
                d.noGravity = true;
            }

            if (Timer == 2)
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.9f, Pitch = -0.6f }, NPC.Center);

            if (Timer >= GatherDur)
                EnterState(StateDecree);
        }

        /// <summary>天宣: 两轮云内横闪宣告天威 (50f/100f), 末段 50f 刻意死寂。</summary>
        private void DecreeAI(Player player) {
            TribulationScreenSystem.Publish(ThemeColor, 0.4f);
            int t = (int)Timer;

            // 电光游走渐密 → 第 100 帧后骤然熄灭 (死寂即宣告)
            ambientFlash = t < 100 ? 0.1f + 0.25f * (t / 100f) * (0.5f + 0.5f * MathF.Sin(t * 0.23f)) : 0f;

            if (t == 30)
                Main.NewText("天威临世，尔当受劫！", ThemeColor);

            // 两声云内横闪 (不落地, 白闪 + 远雷 + 震屏)
            if (t == 50 || t == 100) {
                bool second = t == 100;
                PublishFlash(NPC.Center.X + (second ? 260f : -300f), second ? 0.85f : 0.6f);
                TribulationScreenSystem.Flash(second ? 0.3f : 0.2f);
                ACMScreenShakeSystem.Add(second ? 6f : 4f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = second ? 1.1f : 0.85f, Pitch = second ? -0.35f : -0.15f }, NPC.Center);
            }

            if (Timer >= DecreeDur) {
                EnterState(StateTrial);
                // 死寂即宣告 —— 第一记雷不再重复长等待, 30f 后即起充能
                Timer = StrikeInterval(0, false) - 30;
            }
        }

        /// <summary>天罚: 三波递进落雷; 全部落完后等待最后一记结算, 转审判。</summary>
        private void TrialAI(Player player) {
            int done = StrikesDone;
            int total = Math.Max(1, TotalStrikes);

            // 风暴压暗随进度加深
            float stormI = MathHelper.Lerp(0.42f, 0.62f, done / (float)total);
            TribulationScreenSystem.Publish(ThemeColor, stormI);

            // —— 全部落完: 等最后一记走完 (含余烬) 再判成功 ——
            if (done >= total) {
                ambientFlash = 0.05f;
                int resolveDelay = Kind == TribulationKind.Purple ? 215 : 150;
                if (Timer >= resolveDelay) {
                    SuccessOnce(player);
                    EnterState(StateJudge);
                }
                return;
            }

            bool isFinal = done == total - 1;
            int wave = WaveOf(done);
            int interval = StrikeInterval(wave, isFinal);

            // 云内常态微光 (随波次渐盛)
            ambientFlash = 0.05f + wave * 0.03f + 0.04f * (0.5f + 0.5f * MathF.Sin((int)Timer * 0.06f));

            // 换波喘息起点的低吼 (Timer 为负 = 喘息期)
            if ((int)Timer == -WaveBreather + 2 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 1.2f, Pitch = -0.55f }, NPC.Center);

            // 偶发远雷氛围 (不与落雷预警混淆的低强度闪)
            if ((int)Timer % 190 == 130 && Timer > 0 && !isFinal) {
                PublishFlash(NPC.Center.X + MathF.Sin((int)Timer * 7.77f) * 560f, 0.22f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.35f, Pitch = -0.7f }, NPC.Center + new Vector2(600f, 0f));
            }

            // 终雷前奏: 云内预闪四连加速 (音调渐升 —— "天正在攒最后一击")
            if (isFinal) {
                int tt = (int)Timer;
                for (int i = 0; i < PreludeBeats.Length; i++) {
                    if (tt == interval - PreludeBeats[i]) {
                        PublishFlash(NPC.Center.X + ((i % 2 == 0) ? -1f : 1f) * (320f - i * 80f), 0.45f + i * 0.15f);
                        ACMScreenShakeSystem.Add(2f + i);
                        if (!Main.dedServ)
                            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.7f + i * 0.12f, Pitch = -0.2f + i * 0.25f }, NPC.Center);
                    }
                }
            }

            // —— 落雷调度 ——
            if (Timer >= interval) {
                int consumed = SpawnStrikes(player, wave, isFinal);
                int newDone = done + consumed;
                StrikesDone = newDone;

                // 跨波边界 → 换波喘息; 否则正常清零
                Timer = (newDone < total && WaveOf(newDone) != wave) ? -WaveBreather : 0f;
                NPC.netUpdate = true;
            }
        }

        /// <summary>审判 (成功收尾): 染屏转金 → 云盖裂开 → 金光灌顶 → 消散离场。</summary>
        private void JudgeAI(Player player) {
            int t = (int)Timer;

            // 天幕由风暴色转金
            float goldLerp = MathHelper.Clamp(t / 60f, 0f, 1f);
            Color sky = Color.Lerp(ThemeColor, TelegraphColors.Gold, goldLerp);
            float fade = t > JudgeDur - 50 ? MathHelper.Clamp((JudgeDur - t) / 50f, 0f, 1f) : 1f;
            TribulationScreenSystem.Publish(sky, 0.5f * fade);

            // 云盖从中心裂开 (10~60f), 尾段消散 (140f~)
            breakProgress = MathHelper.Clamp((t - 10) / 50f, 0f, 1f);
            breakProgress = ACMUtils.QuadInOut(breakProgress);
            dissolveProgress = MathHelper.Clamp((t - 140) / 80f, 0f, 1f);
            ambientFlash = 0.15f * (1f - dissolveProgress);

            if (t == 10) {
                TribulationScreenSystem.Flash(0.25f);
                ACMScreenShakeSystem.Add(6f);
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.1f, Pitch = 0.4f }, NPC.Center);
            }

            if (Timer >= JudgeDur)
                Depart();
        }

        /// <summary>息怒 (失败/失效收尾): 雷声渐远, 云快速消散 —— 不拖死者时间。</summary>
        private void AbortAI() {
            float prog = MathHelper.Clamp(Timer / AbortDur, 0f, 1f);
            dissolveProgress = prog;
            ambientFlash = 0f;
            TribulationScreenSystem.Publish(ThemeColor, 0.35f * (1f - prog));

            if ((int)Timer == 2 && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.7f, Pitch = -0.8f }, NPC.Center - new Vector2(0f, 500f));

            if (Timer >= AbortDur)
                Depart();
        }

        private void Depart() {
            NPC.active = false;
            TribulationWeather.Stop();
        }

        // ============================================================
        //  三波调度参数
        // ============================================================

        /// <summary>当前波次 0/1/2 (试探/紧逼/终局)。</summary>
        private int WaveOf(int strikeIdx) {
            int total = Math.Max(1, TotalStrikes);
            int t1 = Math.Max(1, total / 3);
            int t2 = Math.Max(t1 + 1, total * 2 / 3);
            if (strikeIdx < t1) return 0;
            if (strikeIdx < t2) return 1;
            return 2;
        }

        /// <summary>本波落雷间隔 (tick); 终雷额外加前奏时长。</summary>
        private int StrikeInterval(int wave, bool isFinal) {
            int iv = Kind switch {
                TribulationKind.Black => wave switch { 0 => 105, 1 => 150, _ => 68 },
                TribulationKind.Red => wave switch { 0 => 130, 1 => 110, _ => 125 },
                _ => wave switch { 0 => 195, 1 => 175, _ => 225 },
            };
            if (isFinal)
                iv += FinalPrelude;
            return iv;
        }

        /// <summary>
        /// 生成本次落雷弹幕 (仅服务器 NewProjectile; 计数推进各端确定性一致)。
        /// 返回本次消耗的雷数 (玄雷双联=2, 其余=1)。
        /// </summary>
        private int SpawnStrikes(Player player, int wave, bool isFinal) {
            int done = StrikesDone;
            int total = Math.Max(1, TotalStrikes);

            // 玄雷波2 双联雷: 两枚错开 26f 各自完整预警 (不允许终雷被并入双联)
            bool pair = Kind == TribulationKind.Black && wave == 1 && done + 2 <= total - 1;

            int flags = isFinal ? StrikeFlags.Final : 0;
            switch (Kind) {
                case TribulationKind.Red:
                    // 终雷不佯攻 —— "最后一记, 天不骗你"
                    if (!isFinal) {
                        flags |= StrikeFlags.Feint;
                        if (wave >= 2)
                            flags |= StrikeFlags.DoubleFeint;
                    }
                    break;
                case TribulationKind.Purple:
                    if (wave >= 1)
                        flags |= StrikeFlags.DriftGap;
                    if (wave >= 2 || isFinal)
                        flags |= StrikeFlags.DualSweep;
                    break;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                IEntitySource src = NPC.GetSource_FromAI();
                int owner = NPC.target;     // 渡劫者本人 (供弹幕追踪/伤害归属, 多人安全)

                int projType = Kind == TribulationKind.Purple
                    ? ModContent.ProjectileType<TribulationSweep>()
                    : ModContent.ProjectileType<TribulationLightningStrike>();

                SpawnOne(src, player.Center, projType, ComputeDamage(player, done, isFinal), flags, owner);
                if (pair)
                    SpawnOne(src, player.Center, projType, ComputeDamage(player, done + 1, false), StrikeFlags.SecondOfPair, owner);
            }

            return pair ? 2 : 1;
        }

        private void SpawnOne(IEntitySource src, Vector2 pos, int type, int damage, int flags, int owner) {
            int id = Projectile.NewProjectile(src, pos, Vector2.Zero, type, damage, 0f, owner,
                ai0: NPC.whoAmI, ai1: flags, ai2: ThemeColorPacked());
            if (id >= 0 && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncProjectile, number: id);
        }

        private int ComputeDamage(Player player, int strikeIdx, bool isFinal) {
            MythologyPlayer mp = player.GetModPlayer<MythologyPlayer>();
            int damage = BaseStrikeDamage + PerMajorIncrement * mp.Major + PerStrikeIncrement * strikeIdx;

            if (Main.masterMode)
                damage = (int)(damage * 1.6f);
            else if (Main.expertMode)
                damage = (int)(damage * 1.3f);

            // 终雷更重 (但仍是可撑过的考验, 不喧宾夺主成 DPS)
            if (isFinal)
                damage = (int)(damage * 1.5f);
            return damage;
        }

        /// <summary>把主题色打包成可经 ai 传递的 float (RGB 24bit)。供弹幕侧解出非红主题副色。</summary>
        private float ThemeColorPacked() => (ThemeColor.R << 16) | (ThemeColor.G << 8) | ThemeColor.B;

        // ============================================================
        //  视觉发布通道 (弹幕 → 云体, 纯客户端)
        // ============================================================

        /// <summary>弹幕发布云内电光 (蓄力脉冲/先导/轰落瞬间照亮云体)。同帧多源取 max。</summary>
        public void PublishFlash(float worldX, float intensity) {
            if (Main.dedServ)
                return;
            if (pubFlashFrame != Main.GameUpdateCount) {
                pubFlashFrame = Main.GameUpdateCount;
                pubFlash = 0f;
            }
            if (intensity > pubFlash) {
                pubFlash = intensity;
                pubFlashWorldX = worldX;
            }
        }

        /// <summary>弹幕发布轰落反冲 (云体向上弹跳, 雷的后坐力)。</summary>
        public void PublishRecoil(float amount) {
            if (Main.dedServ)
                return;
            recoilOff = MathF.Max(recoilOff, amount);
        }

        private float CurrentPublishedFlash() => pubFlashFrame == Main.GameUpdateCount ? pubFlash : 0f;

        // ============================================================
        //  绘制 —— 云盖着色器 + 贴图核心 + 电弧游走
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return false;

            float gather = State == StateGather ? ACMUtils.QuadOut(MathHelper.Clamp(Timer / GatherDur, 0f, 1f)) : 1f;
            float flash = MathF.Max(ambientFlash, CurrentPublishedFlash());
            float flashX = CurrentPublishedFlash() > ambientFlash ? pubFlashWorldX : NPC.Center.X + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f) * 300f;

            Vector2 deckCenter = NPC.Center - new Vector2(0f, recoilOff);
            float deckW = MathHelper.Lerp(760f, 1900f, gather);
            float deckH = MathHelper.Lerp(240f, 520f, gather);

            // —— 1) 贴图核心层 (云的"实体感"内芯, 在着色器云盖之下) ——
            Texture2D coreTex = TextureAssets.Npc[Type].Value;
            float coreAlpha = 0.5f * gather * (1f - dissolveProgress) * (1f - breakProgress * 0.8f);
            if (coreAlpha > 0.02f) {
                Color coreC = Color.Lerp(ThemeColor, Color.Black, 0.45f) * coreAlpha;
                spriteBatch.Draw(coreTex, deckCenter - screenPos, null, coreC, NPC.rotation,
                    coreTex.Size() / 2f, gather * 1.55f, SpriteEffects.None, 0f);
            }

            // —— 2) 云盖着色器 quad ——
            Effect deck = TribulationFX.Deck;
            bool inJudge = State == StateJudge;
            if (deck != null && gather > 0.02f) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, deck, Main.GameViewMatrix.TransformationMatrix);

                deck.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                deck.Parameters["uIntensity"]?.SetValue(gather);
                deck.Parameters["uSeed"]?.SetValue(NPC.whoAmI * 0.37f + 3.1f);
                deck.Parameters["uColor"]?.SetValue(ThemeColor.ToVector4());
                deck.Parameters["uColorDark"]?.SetValue(new Color(14, 14, 26).ToVector4());
                deck.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
                deck.Parameters["uFlashX"]?.SetValue(MathHelper.Clamp((flashX - (deckCenter.X - deckW * 0.5f)) / deckW, 0f, 1f));
                deck.Parameters["uFlashColor"]?.SetValue((inJudge ? TelegraphColors.Gold : TelegraphColors.Lightning).ToVector4());
                deck.Parameters["uBreak"]?.SetValue(breakProgress);
                deck.Parameters["uBreakColor"]?.SetValue(TelegraphColors.Gold.ToVector4());
                deck.Parameters["uDissolve"]?.SetValue(dissolveProgress);

                Vector2 tl = deckCenter - screenPos - new Vector2(deckW, deckH) * 0.5f;
                spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                    new Rectangle((int)tl.X, (int)tl.Y, (int)deckW, (int)deckH), Color.White);

                spriteBatch.End();
                ACMShaders.RestoreDefaultBatch(spriteBatch);
            }

            // —— 3) 云内电弧游走 (ElectricArcSheet 随机段, 频率 ∝ 电光强度) ——
            if (flash > 0.12f && dissolveProgress < 0.85f)
                DrawCloudArcs(spriteBatch, screenPos, deckCenter, deckW, flash, flashX, inJudge);

            return false;
        }

        private void DrawCloudArcs(SpriteBatch sb, Vector2 screenPos, Vector2 deckCenter, float deckW, float flash, float flashX, bool gold) {
            Texture2D arcTex = ACMAsset.ElectricArcSheet;
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (arcTex == null || glowTex == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color arcC = (gold ? TelegraphColors.Gold : TelegraphColors.Lightning) with { A = 0 };
            int arcCount = flash > 0.55f ? 3 : (flash > 0.3f ? 2 : 1);
            int seg = arcTex.Height / 4;
            float t = Main.GlobalTimeWrappedHourly;

            for (int i = 0; i < arcCount; i++) {
                // 时间驱动的伪随机段/位置 (每 4 帧跳段, 电的癫痫感)
                int hop = (int)(t * 15f) + i * 7;
                Rectangle src = new(0, (hop % 4) * seg, arcTex.Width, seg);
                float ox = MathF.Sin(hop * 12.9898f) * 0.5f + 0.5f;
                Vector2 pos = new(MathHelper.Lerp(flashX - 260f, flashX + 260f, ox), deckCenter.Y + MathF.Sin(hop * 3.71f) * 60f);
                pos.X = MathHelper.Clamp(pos.X, deckCenter.X - deckW * 0.42f, deckCenter.X + deckW * 0.42f);
                float rot = MathF.Sin(hop * 7.13f) * 0.5f;
                sb.Draw(arcTex, pos - screenPos, src, arcC * (flash * 0.75f), rot,
                    new Vector2(src.Width / 2f, src.Height / 2f), new Vector2(0.55f, 0.4f), SpriteEffects.None, 0f);
            }

            // 电光中心的柔光底晕 (照亮云腹)
            sb.Draw(glowTex, new Vector2(flashX, deckCenter.Y + 40f) - screenPos, null, arcC * (flash * 0.6f), 0f,
                glowTex.Size() / 2f, new Vector2(6.5f, 3.2f) * flash + Vector2.One, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        // ===== 修真结算钩子 (原样保留, 切勿改语义) =====

        public override void OnKill() {
            Player p = Main.player[NPC.target];
            if (p.active)
                p.GetModPlayer<MythologyPlayer>().AdvanceMajor(p); // 正式突破
        }

        private void FailOnce(Player player) {
            if (tribulationEnded)
                return;
            tribulationEnded = true;
            FailTribulation(player);
        }

        private void SuccessOnce(Player player) {
            if (tribulationEnded)
                return;
            tribulationEnded = true;
            SuccessTribulation(player);
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

            // 金光灌顶 (成功的有重量收尾): 生成一枚纯演出的天光弹 (damage=0)
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
        public const int Feint = 1;          // bit0: 赤雷佯攻 (假蓄力后追新站位)
        public const int Final = 2;          // bit1: 终雷 (超长充能/最重轰落)
        public const int SuccessFinale = 4;  // bit2: 渡劫成功金光灌顶 (纯演出, 无伤)
        public const int SecondOfPair = 8;   // bit3: 玄雷双联的第二记 (延迟 26f 后在新站位起充)
        public const int DoubleFeint = 16;   // bit4: 赤雷双重佯攻 (假→假→真)
        public const int DriftGap = 32;      // bit5: 紫霄雷幕的法眼随扫漂移
        public const int DualSweep = 64;     // bit6: 紫霄双幕对扫 (两侧向中心合拢)
    }

    /// <summary>
    /// 劫云专属着色器缓存 + 共享折线雷柱绘制原语 (Xuanwu 静态缓存写法; 不注册进 ACMShaders)。
    /// </summary>
    internal static class TribulationFX
    {
        private const string Path = "AncientChineseMythology/Effects/";
        private static Asset<Effect> boltRef;
        private static Asset<Effect> deckRef;

        /// <summary>程序化折线闪电 (主雷柱/先导/雷幕电弧)。</summary>
        public static Effect Bolt => Get(ref boltRef, "TribulationBolt");
        /// <summary>翻滚云盖 (云内电光散射/裂开/消散)。</summary>
        public static Effect Deck => Get(ref deckRef, "TribulationCloudDeck");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        /// <summary>
        /// 绘制一根折线雷柱 quad (worldTop→worldBottom)。须在已有活动批的绘制阶段调用
        /// (内部 End→Begin(Immediate, Additive)→恢复默认批)。
        /// </summary>
        /// <param name="widthPx">quad 世界像素宽 (折线在其中游走)。</param>
        /// <param name="glow">主题辉光色。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="seed">折线种子 (变 seed = 换形)。</param>
        /// <param name="life">余辉进度 0=轰落峰值 1=熄灭。</param>
        /// <param name="widthScale">芯宽系数 (主雷 1 / 先导 ~0.35)。</param>
        /// <param name="branch">分叉可见度 0~1。</param>
        /// <param name="flicker">高频闪烁幅度 (先导用)。</param>
        public static void DrawBolt(Vector2 worldTop, Vector2 worldBottom, float widthPx,
            Color glow, float intensity, float seed, float life,
            float widthScale = 1f, float branch = 1f, float flicker = 0f) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = Bolt;
            if (fx == null)
                return;

            Vector2 dir = worldBottom - worldTop;
            float len = dir.Length();
            if (len < 8f)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uLife"]?.SetValue(MathHelper.Clamp(life, 0f, 1f));
            fx.Parameters["uColor"]?.SetValue(glow.ToVector4());
            fx.Parameters["uWidthScale"]?.SetValue(widthScale);
            fx.Parameters["uBranch"]?.SetValue(branch);
            fx.Parameters["uFlicker"]?.SetValue(flicker);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float rot = dir.ToRotation() - MathHelper.PiOver2;
            // origin 在贴图顶端中心: 未旋转时 quad 沿 +Y 延伸, 旋转后对齐 top→bottom
            sb.Draw(pixel, worldTop - Main.screenPosition, null, Color.White, rot,
                new Vector2(pixel.Width * 0.5f, 0f),
                new Vector2(widthPx / pixel.Width, len / pixel.Height), SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}

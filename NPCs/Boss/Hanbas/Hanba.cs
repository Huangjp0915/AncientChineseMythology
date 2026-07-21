using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Helpers;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    /// <summary>
    /// 旱魃 — 上古大旱之神 / 僵尸始祖 (V3 全面重做)。
    /// 双重身份 = 两套运动语言:
    ///   ● 封尸期 (符纸在): 僵直 — 长静止 → 瞬间僵跳直线突进 → 硬停, 躯体锁死竖直, 色调灰败。
    ///   ● 旱神期 (解封后): 热浪漂浮 + 烈日爆发突进, 招式伴随蒸发/焦痕/蝗祸, 世界随战斗枯竭。
    /// 三大演出: 入场棺醒 / 解封八眼点睛 (签名节拍) / 焚天坠日 (唯一冲击帧) / 死亡"旱魃死, 天将雨"。
    /// </summary>
    [AutoloadBossHead]
    internal class Hanba : ModNPC
    {
        #region 状态与字段

        /// <summary>主状态机。Intro 必须为 0 (新生成 NPC 的 ai[0] 默认值)。</summary>
        internal enum HState
        {
            Intro = 0,       // 入场棺醒
            Connect,         // 连接语: 喘息 + 选招
            SealedHop,       // P1 僵跃突进
            SealedLocust,    // P1 尸气蝗群
            SealedGaze,      // P1 焦目双珠
            Unseal,          // 解封演出 (八眼点睛)
            ScorchDash,      // P2 赤地突进
            LocustSweep,     // P2 蝗虫过境
            EyeVolley,       // P2 焦目连珠
            Drain,           // P2 干渴汲取
            SunRite,         // ≤60% 蚀日演出 (一次性)
            CageLasers,      // P3 鬼域焦笼
            SunLance,        // P3 烈日灼柱
            MirageRush,      // P3 蜃景分身突进
            LocustCross,     // P3 蝗神蔽日
            SunFall,         // ≤30% 焚天坠日 set-piece (一次性)
            Death,           // 死亡演出
            Despawn          // 脱战
        }

        private HState State {
            get => (HState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float Timer => ref NPC.ai[1];
        private ref float GlobalTimer => ref NPC.ai[2];
        private ref float SubState => ref NPC.ai[3];

        // otherAI (SendExtraAI 同步): [0]=选招游标, [1]=一次性演出位掩码(1=蚀日 2=坠日), [2]=上一招(抗重复), [3]=方向符号等通用参数
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;

        /// <summary>符纸封印中 (Boss 无敌, 打符纸)。经 SendExtraAI 同步 (旧版仅服务器为 true)。</summary>
        internal bool HasTalisman;
        private Vector2 OrigRestrictionPos; // 鬼域焦笼中心 (经 SendExtraAI 同步供激光裁剪)

        [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hanbas/")]
        internal static Asset<Texture2D> Talisman = null;

        private static readonly List<Vector2> EyesOffset = [];
        private int frame;
        private const int maxFrame = 4;

        // —— 纯本地视觉 (由同步计时器确定性推导, 不联网) ——
        private readonly float[] eyeGlow = new float[8];
        private float dissolve;        // 0=完整 1=全消散 (入场反向/死亡正向)
        private float talismanBurn;    // 解封符纸烧毁进度 0~1
        private float droughtVisual;   // 旱情标量 (随阶段推进)
        private float heatAura;        // 解封后体表热浪
        private float sealGrey;        // 封尸降饱和 1→0
        private float bloomPulse;      // 径向泛光脉冲 (解封尖啸等)
        private Vector2 riteScarPos;   // 蚀日干裂场中心 (各端由地形确定性推导)

        // 焚天坠日焦土场 (static: 同屏唯一旱魃; 客户端视觉)
        private static Vector2 sunScarCenter;
        private static bool sunScarActive;

        private float LifeFrac => NPC.lifeMax > 0 ? NPC.life / (float)NPC.lifeMax : 1f;
        private bool IsPhase3 => !HasTalisman && LifeFrac <= 0.6f;
        /// <summary>终章 (坠日已完成)。</summary>
        private bool IsFinale => !HasTalisman && (otherAI[1] & 2) != 0;

        public static int ReelBackTime => Main.masterMode ? 50 : 60;

        #endregion

        #region 生命周期与默认值

        public override void Load() {
            // 重载安全: 清空再填 (旧版重载会重复 Add)
            EyesOffset.Clear();
            EyesOffset.Add(new Vector2(0, -44));
            EyesOffset.Add(new Vector2(0, 50));
            EyesOffset.Add(new Vector2(34, 34));
            EyesOffset.Add(new Vector2(-46, -26));
            EyesOffset.Add(new Vector2(44, -26));
            EyesOffset.Add(new Vector2(-34, 34));
            EyesOffset.Add(new Vector2(-54, 12));
            EyesOffset.Add(new Vector2(54, 12));
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = maxFrame;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 400000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Hanba");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HanbaBook>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedHanba = true;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            for (int i = 0; i < aiSlot; i++) {
                writer.Write(otherAI[i]);
            }
            writer.Write(HasTalisman);
            writer.Write(OrigRestrictionPos.X);
            writer.Write(OrigRestrictionPos.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            for (int i = 0; i < aiSlot; i++) {
                otherAI[i] = reader.ReadInt32();
            }
            HasTalisman = reader.ReadBoolean();
            OrigRestrictionPos.X = reader.ReadSingle();
            OrigRestrictionPos.Y = reader.ReadSingle();
        }

        public override bool CheckActive() => false;

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (HasTalisman) {
                return false; //封印期不绘制本体血条 (打符纸)
            }
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        /// <summary>死亡演出拦截: 首次致死 → 进入死亡编排, 演出末尾才真正结算。</summary>
        public override bool CheckDead() {
            if (State != HState.Death) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.velocity *= 0.2f;
                State = HState.Death;
                Timer = 0;
                SubState = 0;
                ClearMyProjectiles();
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        internal Vector2 GetOrigPos() => OrigRestrictionPos;

        /// <summary>符纸被摧毁 (Talisman.OnKill, 服务器语境) → 解封演出。</summary>
        internal void TalismanKill() {
            HasTalisman = false;
            if (State != HState.Death && State != HState.Despawn) {
                State = HState.Unseal;
                Timer = 0;
                SubState = 0;
                otherAI[0] = 0;
            }
            NPC.netUpdate = true;
        }

        /// <summary>坠日冲击回调 (HanbaSunOrb): 记录常驻焦土场中心 (客户端视觉)。</summary>
        internal static void NotifySunImpact(Vector2 impactCenter) {
            sunScarCenter = impactCenter;
            sunScarActive = true;
        }

        private void ClearMyProjectiles() {
            if (VaultUtils.isClient)
                return;
            HanbaFireBall.KillAll();
            HanbaLaser.AllVanish();
            HanbaBigLaser.AllVanish();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == ModContent.ProjectileType<LocustSet>()
                    || proj.type == ModContent.ProjectileType<HanbaScorchTrail>()
                    || proj.type == ModContent.ProjectileType<HanbaCrackRing>()
                    || proj.type == ModContent.ProjectileType<HanbaMirage>()) {
                    proj.Kill();
                    proj.netUpdate = true;
                }
            }
        }

        private int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = NPC.damage;
            if (getOrigDamage || num <= 0) {
                num = NPC.defDamage;
            }
            return (int)(num * scaling);
        }

        private static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)(searchStartY / 16f);
            for (int tileY = startTileY; tileY < startTileY + 70; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return searchStartY + 600f;
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives() && State != HState.Despawn && State != HState.Death) {
                    State = HState.Despawn;
                    Timer = 0;
                }
            }

            if (GlobalTimer == 0) {
                OnFightStart();
            }
            GlobalTimer++;
            Timer++;

            // 伤害基线: 默认 0, 各状态窗口内自行开启 (§6.1 伤害窗口与视觉对齐)
            // 接触伤害一律按 defDamage 比例给出, 保留专家/大师难度缩放
            NPC.damage = 0;
            NPC.dontTakeDamage = HasTalisman || State == HState.Intro || State == HState.Death;

            bool overrideRotation = false;

            switch (State) {
                case HState.Intro: RunIntro(target); break;
                case HState.Connect: RunConnect(target); break;
                case HState.SealedHop: RunSealedHop(target); break;
                case HState.SealedLocust: RunSealedLocust(target); break;
                case HState.SealedGaze: RunSealedGaze(target); break;
                case HState.Unseal: RunUnseal(target); break;
                case HState.ScorchDash: RunScorchDash(target); break;
                case HState.LocustSweep: RunLocustSweep(target); break;
                case HState.EyeVolley: RunEyeVolley(target); break;
                case HState.Drain: RunDrain(target); break;
                case HState.SunRite: RunSunRite(target); break;
                case HState.CageLasers: RunCageLasers(target, ref overrideRotation); break;
                case HState.SunLance: RunSunLance(target); break;
                case HState.MirageRush: RunMirageRush(target); break;
                case HState.LocustCross: RunLocustCross(target); break;
                case HState.SunFall: RunSunFall(target); break;
                case HState.Death: RunDeath(); break;
                case HState.Despawn: RunDespawn(); break;
            }

            // 距离栓绳: 攻击状态里被拉太远 → 温和拉回 (防"飞屏外绕圈")
            if (State != HState.Intro && State != HState.Death && State != HState.Despawn
                && State != HState.SunFall && !NPC.WithinRange(target.Center, 2600f)) {
                NPC.velocity += NPC.SafeDirectionTo(target.Center) * 0.7f;
                if (NPC.velocity.Length() > 30f)
                    NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * 30f;
            }

            // 姿态: 封尸期躯体锁死竖直 (僵直); 旱神期随速度倾斜; 焦笼期自旋 (状态内处理)
            if (!overrideRotation) {
                bool stiff = HasTalisman || State == HState.Intro || State == HState.Unseal || State == HState.Death;
                float targetRot = stiff ? 0f : NPC.velocity.X * 0.02f;
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, stiff ? 0.2f : 0.1f);
            }

            UpdateVisuals(target);

            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        private void OnFightStart() {
            if (!VaultUtils.isServer && !SkyManager.Instance[HanbaSky.name].IsActive()) {
                SkyManager.Instance.Activate(HanbaSky.name);
            }
            sunScarActive = false;
            dissolve = 1f;
            sealGrey = 1f;
            NPC.velocity = Vector2.Zero;
        }

        private void SwitchState(HState next) {
            State = next;
            Timer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        /// <summary>连接语末尾的选招 (仅服务器)。一次性演出优先, 之后按阶段取池。</summary>
        private HState PickNextAttack() {
            if (HasTalisman) {
                // 封尸教学循环: 僵跃 → 蝗群 → 僵跃 → 焦目
                int idx = otherAI[0] % 4;
                otherAI[0]++;
                return idx switch {
                    0 => HState.SealedHop,
                    1 => HState.SealedLocust,
                    2 => HState.SealedHop,
                    _ => HState.SealedGaze
                };
            }

            float lifeFrac = LifeFrac;
            if (lifeFrac <= 0.6f && (otherAI[1] & 1) == 0) {
                otherAI[1] |= 1;
                return HState.SunRite;
            }
            if (lifeFrac <= 0.3f && (otherAI[1] & 2) == 0) {
                otherAI[1] |= 2;
                return HState.SunFall;
            }

            if (lifeFrac > 0.6f) {
                // P2 洗牌袋 + 抗重复
                HState[] pool = [HState.ScorchDash, HState.LocustSweep, HState.EyeVolley, HState.Drain];
                HState pick;
                int guard = 0;
                do {
                    pick = pool[Main.rand.Next(pool.Length)];
                } while ((int)pick == otherAI[2] && ++guard < 8);
                otherAI[2] = (int)pick;
                return pick;
            }

            if (!IsFinale) {
                // P3 手排循环: 重招不相邻, 强度波形有意编排
                HState[] cycle = [HState.CageLasers, HState.ScorchDash, HState.SunLance,
                    HState.MirageRush, HState.LocustCross, HState.EyeVolley];
                HState pick = cycle[otherAI[0] % cycle.Length];
                otherAI[0]++;
                otherAI[2] = (int)pick;
                return pick;
            }

            // P4 终章: 全池洗牌 + 抗重复
            HState[] finale = [HState.ScorchDash, HState.MirageRush, HState.SunLance,
                HState.EyeVolley, HState.LocustCross, HState.CageLasers, HState.Drain];
            HState fpick;
            int fguard = 0;
            do {
                fpick = finale[Main.rand.Next(finale.Length)];
            } while ((int)fpick == otherAI[2] && ++fguard < 8);
            otherAI[2] = (int)fpick;
            return fpick;
        }

        #endregion

        #region 入场 / 连接语 / 封尸三式

        // 入场棺醒 (~200f): 尸气汇聚 → 溶解显形 → 静止威压 → 双主眼点亮 → 低吼落幅
        private void RunIntro(Player target) {
            NPC.velocity = new Vector2(0, MathF.Sin(GlobalTimer * 0.05f) * 0.3f); // 悬尸微浮

            if ((int)Timer == 1) {
                SoundEngine.PlaySound(SoundID.Zombie1 with { Pitch = -0.7f, Volume = 0.8f }, NPC.Center);
            }

            // 尸气与灰烬向躯体汇聚 (显形期)
            if (!Main.dedServ && Timer < 110 && Main.rand.NextBool(2)) {
                Vector2 spawn = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(160f, 380f);
                Dust d = Dust.NewDustPerfect(spawn, Main.rand.NextBool(3) ? DustID.Smoke : DustID.Torch,
                    (NPC.Center - spawn) * 0.04f, 150, default, Main.rand.NextFloat(1.0f, 1.8f));
                d.noGravity = true;
            }

            if ((int)Timer == 120) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.4f, Volume = 0.5f }, NPC.Center);
            }
            if ((int)Timer == 140) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.6f, Volume = 0.5f }, NPC.Center);
            }

            if ((int)Timer == 160) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.55f }, NPC.Center);
                ACMUtils.AddScreenShake(8f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 34; i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center, Main.rand.NextBool(3) ? DustID.Smoke : DustID.Torch,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 9f), 120, default, Main.rand.NextFloat(1.4f, 2.6f));
                        d.noGravity = true;
                    }
                }
                // 符纸封印显现
                if (!HasTalisman && !VaultUtils.isClient) {
                    HasTalisman = true;
                    NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center,
                        ModContent.NPCType<Talisman>(), ai0: NPC.whoAmI, target: NPC.target);
                    NPC.netUpdate = true;
                }
            }

            if (Timer >= 200 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        // 连接语: 喘息 + 缓漂定位 + 选招 (显式节奏标点)
        private void RunConnect(Player target) {
            float duration = HasTalisman ? 50f : (IsFinale ? 36f : 46f);

            // 缓漂向玩家侧上方 (保持当前侧, 避免莫名横穿)
            float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
            Vector2 hoverPos = target.Center + new Vector2(side * 420f, -170f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverPos) * MathF.Min(18f, NPC.Distance(hoverPos) * 0.08f), 0.08f);

            NPC.damage = HasTalisman ? 0 : GetBossDamage(0.75f, true); // 旱神期悬停余温 (轻)

            // 已就位 → 提前出招 (不等表)
            if (Timer > 14 && NPC.WithinRange(hoverPos, 70f) && Timer < duration - 1) {
                Timer = duration - 1;
            }

            if (Timer >= duration && !VaultUtils.isClient) {
                SwitchState(PickNextAttack());
            }
        }

        // P1 僵跃突进 ×2: 长僵直 → pow8 反向抽身 → 瞬时 54px/f 直线 → 硬刹 + 僵停
        private void RunSealedHop(Player target) {
            const int launchFrame = 40;

            if (Timer < launchFrame) {
                NPC.velocity *= 0.9f; // 慢启动阀门

                // 最小起手距阀门: 贴脸时先缓缓退开 (给足读招空间)
                if (Timer < launchFrame - 12 && NPC.WithinRange(target.Center, 240f)) {
                    NPC.velocity = -NPC.SafeDirectionTo(target.Center) * 6f;
                }

                if ((int)Timer == 4) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.6f, Volume = 0.65f }, NPC.Center); // 36f 定拍预警音
                }

                // 末 12f: pow8 反向抽身 (静→骤缩的吸气)
                if (Timer >= launchFrame - 12) {
                    float t = (Timer - (launchFrame - 12)) / 12f;
                    NPC.velocity = -NPC.SafeDirectionTo(target.Center) * MathF.Pow(t, 8f) * 16f;
                }
            }
            else if ((int)Timer == launchFrame) {
                // 瞬时起跳 (set, 非 ramp)
                Vector2 dir = NPC.SafeDirectionTo(target.Center + target.velocity * 10f);
                NPC.velocity = dir * 54f;
                NPC.oldPos = new Vector2[NPC.oldPos.Length];
                SoundEngine.PlaySound(SoundID.Zombie2 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(4f);
                NPC.netUpdate = true;
            }
            else if (Timer <= launchFrame + 10) {
                // 冲刺 10f: 零转向 (直线才快)
            }
            else if (Timer <= launchFrame + 24) {
                NPC.velocity *= 0.62f; // 硬刹
            }
            else {
                NPC.velocity *= 0.9f; // 僵停 20f
            }

            // 伤害窗口与冲刺视觉严格对齐
            if (NPC.velocity.Length() > 18f && Timer >= launchFrame) {
                NPC.damage = GetBossDamage(1.35f, true);
            }

            if (Timer >= launchFrame + 44 && !VaultUtils.isClient) {
                SubState++;
                if (SubState >= 2) {
                    SwitchState(HState.Connect);
                }
                else {
                    Timer = 0;
                    NPC.netUpdate = true;
                }
            }
        }

        // P1 尸气蝗群: 收拢 → 3 发缓速蝗团
        private void RunSealedLocust(Player target) {
            NPC.velocity *= 0.92f;

            // 蝗虫向躯体收拢 (预告)
            if (!Main.dedServ && Timer < 30 && Timer % 4 == 0) {
                Vector2 spawn = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(140f, 260f);
                PRTLoader.NewParticle<LocustPRT>(spawn, (NPC.Center - spawn) * 0.05f);
            }

            if ((int)Timer == 30 && !VaultUtils.isClient) {
                SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.3f }, NPC.Center);
                for (int i = -1; i <= 1; i++) {
                    Vector2 dir = NPC.SafeDirectionTo(target.Center).RotatedBy(i * 0.42f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 8f,
                        ModContent.ProjectileType<LocustSet>(), GetBossDamage(0.5f, true), 2f, Main.myPlayer, 1f);
                }
            }

            if (Timer >= 62 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        // P1 焦目双珠: 双主眼读条 → 2 波直线火球 (无追踪)
        private void RunSealedGaze(Player target) {
            NPC.velocity *= 0.93f;

            if (((int)Timer == 40 || (int)Timer == 64) && !VaultUtils.isClient) {
                SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                Vector2 aim = NPC.SafeDirectionTo(target.Center);
                for (int e = 3; e <= 4; e++) { //两只主眼
                    Vector2 shootPos = NPC.Center + EyesOffset[e].RotatedBy(NPC.rotation);
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = aim.RotatedBy(i * 0.13f) * 11f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), shootPos, vel,
                            ModContent.ProjectileType<HanbaFireBall>(), GetBossDamage(0.5f, true), 2f, Main.myPlayer, 0, 0);
                    }
                }
                NPC.velocity -= aim * 8f; // 后坐力
                NPC.netUpdate = true;
            }

            if (Timer >= 104 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        #endregion

        #region 解封演出 (签名节拍)

        // 解封 (~150f): 符纸烧尽 → 静默垂落 → 八眼逐一点亮 → 尖啸热浪爆发
        private void RunUnseal(Player target) {
            if ((int)Timer == 1) {
                ClearMyProjectiles();
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.8f, Volume = 0.9f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.4f }, NPC.Center);
            }

            // 垂落静默
            NPC.velocity *= 0.9f;
            if (Timer < 40) {
                NPC.velocity.Y += 0.12f;
            }

            // 八眼逐一点亮的小节拍音 (音阶递升)
            int igniteIdx = (int)((Timer - 40f) / 9f);
            if (Timer >= 40 && Timer < 112 && (int)(Timer - 40f) % 9 == 0 && igniteIdx < 8) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f + igniteIdx * 0.12f, Volume = 0.5f }, NPC.Center);
                if (!Main.dedServ) {
                    Vector2 eyePos = NPC.Center + EyesOffset[igniteIdx].RotatedBy(NPC.rotation);
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustPerfect(eyePos, DustID.Torch,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f), 120, Color.OrangeRed, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            // 112~130: 蓄力语法的"静默收束" — 什么都不发生, 玩家屏息
            if ((int)Timer == 130) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.35f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                HanbaScorchScreenSystem.PulseHeat(0.55f);
                bloomPulse = 1f;
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 2; i++) {
                        Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f + i * 0.4f);
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 50; i++) {
                        Dust d = Dust.NewDustPerfect(NPC.Center, Main.rand.NextBool() ? DustID.Torch : DustID.GoldFlame,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 15f), 100, default, Main.rand.NextFloat(1.6f, 3f));
                        d.noGravity = true;
                    }
                }
            }

            if (Timer >= 150 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        #endregion

        #region P2 旱神四式

        // 赤地突进 ×3: 反向拉锁线 → 62px/f 复利冲刺, 沿途布焦痕 (30f 后延燃)
        private void RunScorchDash(Player target) {
            const int launchFrame = 46;
            int totalDash = IsFinale ? 4 : 3;

            if (Timer < 34) {
                // 悬停重摆位: 每段绕玩家旋转 90° 换角度
                float baseAng = SubState * MathHelper.PiOver2 + (otherAI[3] % 2 == 0 ? 0f : MathHelper.PiOver4);
                Vector2 hoverPos = target.Center + baseAng.ToRotationVector2() * 430f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverPos) * MathF.Min(22f, NPC.Distance(hoverPos) * 0.09f), 0.1f);

                if ((int)Timer == 10) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 0.7f }, NPC.Center); // 36f 定拍
                }
            }
            else if (Timer < launchFrame) {
                // 反向拉: InverseLerp² 渐强反推, 锁线可读
                float t = (Timer - 34f) / 12f;
                NPC.velocity = -NPC.SafeDirectionTo(target.Center) * t * t * 24f;
            }
            else if ((int)Timer == launchFrame) {
                Vector2 dir = NPC.SafeDirectionTo(target.Center + target.velocity * 12f);
                NPC.velocity = dir * 62f;
                NPC.oldPos = new Vector2[NPC.oldPos.Length];
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.15f, Volume = 0.85f }, NPC.Center);
                ACMUtils.AddScreenShake(5f);
                NPC.netUpdate = true;
            }
            else if (Timer <= launchFrame + 9) {
                NPC.velocity *= 1.02f; // 复利加速
                // 沿途布焦痕 (服务器): 引信按段递增 → 依次延燃
                if (!VaultUtils.isClient && (int)Timer % 2 == 0) {
                    int nodeIdx = ((int)Timer - launchFrame) / 2;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<HanbaScorchTrail>(), GetBossDamage(0.45f, true), 1f, Main.myPlayer,
                        30 + nodeIdx * 4);
                }
            }
            else if (Timer <= launchFrame + 23) {
                NPC.velocity *= 0.66f; // 硬刹
            }
            else {
                NPC.velocity *= 0.92f; // 段间隙 26f
            }

            if (NPC.velocity.Length() > 20f && Timer >= launchFrame) {
                NPC.damage = GetBossDamage(1.55f, true);
            }

            if (Timer >= launchFrame + 49 && !VaultUtils.isClient) {
                SubState++;
                if (SubState >= totalDash) {
                    SwitchState(HState.Connect);
                }
                else {
                    Timer = 0;
                    NPC.netUpdate = true;
                }
            }
        }

        // 蝗虫过境: 2 道横扫蝗墙, 每道 45f 预警带 + 240px 逃逸缺口
        private void RunLocustSweep(Player target) {
            if ((int)Timer == 1 && !VaultUtils.isClient) {
                otherAI[3] = Main.rand.NextBool() ? 1 : -1; //首波方向
                NPC.netUpdate = true;
            }

            // Boss 退至场上侧压阵 (全程可打)
            Vector2 hoverPos = target.Center + new Vector2(-otherAI[3] * 380f, -320f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverPos) * MathF.Min(16f, NPC.Distance(hoverPos) * 0.06f), 0.07f);

            // 蝗鸣渐强 + 场边蝗云聚集
            if (!Main.dedServ && Timer < 60 && Timer % 5 == 0) {
                float dirSign = otherAI[3];
                Vector2 spawn = target.Center + new Vector2(-dirSign * 1500f, Main.rand.NextFloat(-500f, 500f));
                PRTLoader.NewParticle<LocustPRT>(spawn, new Vector2(dirSign * Main.rand.NextFloat(4f, 9f), 0));
            }

            if (((int)Timer == 60 || (int)Timer == 175) && !VaultUtils.isClient) {
                int wave = (int)Timer == 60 ? 0 : 1;
                float dirSign = wave == 0 ? otherAI[3] : -otherAI[3];
                SpawnLocustWall(target, dirSign, vertical: false);
            }

            if (Timer >= 300 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        /// <summary>生成一道蝗墙 (9 行 110px 段, 随机连开 2 行缺口 = 240px 逃逸带; 判定与预警带逐段一致)。</summary>
        private void SpawnLocustWall(Player target, float dirSign, bool vertical) {
            const int rows = 9;
            const float seg = 110f;
            int gap = Main.rand.Next(1, rows - 2); //缺口起始行 (避开两端)

            for (int r = 0; r < rows; r++) {
                if (r == gap || r == gap + 1) {
                    continue;
                }
                float lateral = (r - (rows - 1) * 0.5f) * seg;
                Vector2 spawnPos;
                Vector2 vel;
                if (!vertical) {
                    spawnPos = target.Center + new Vector2(-dirSign * 1700f, lateral);
                    vel = new Vector2(dirSign * 14f, 0);
                }
                else {
                    spawnPos = target.Center + new Vector2(lateral, -1500f);
                    vel = new Vector2(0, 12f);
                }
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<LocustSet>(), GetBossDamage(0.5f, true), 2f, Main.myPlayer,
                    0f, 45f);
            }
            SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.15f, Volume = 1.1f }, target.Center);
        }

        // 焦目连珠: 八眼顺序点亮读条 → 8 发收束扇火球 (延迟缓追)
        private void RunEyeVolley(Player target) {
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(target.Center) * 4f, 0.05f);

            if (Timer < 50 && (int)Timer % 6 == 0 && !Main.dedServ) {
                int k = (int)(Timer / 6f);
                if (k < 8) {
                    Vector2 eyePos = NPC.Center + EyesOffset[k].RotatedBy(NPC.rotation);
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(eyePos, DustID.Torch,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f), 120, Color.OrangeRed, 1.3f);
                        d.noGravity = true;
                    }
                }
            }

            bool secondVolley = IsFinale;
            if (((int)Timer == 50 || (secondVolley && (int)Timer == 92)) && !VaultUtils.isClient) {
                SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f, Volume = 0.8f }, NPC.Center);
                Vector2 aim = NPC.SafeDirectionTo(target.Center);
                for (int k = 0; k < 8; k++) {
                    Vector2 shootPos = NPC.Center + EyesOffset[k].RotatedBy(NPC.rotation);
                    float fan = MathHelper.Lerp(-0.5f, 0.5f, k / 7f);
                    Vector2 vel = aim.RotatedBy(fan) * 10.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), shootPos, vel,
                        ModContent.ProjectileType<HanbaFireBall>(), GetBossDamage(0.5f, true), 2f, Main.myPlayer, 0, 1f);
                }
                NPC.velocity -= aim * 9f; // 齐射后坐
                NPC.netUpdate = true;
            }

            float endTime = secondVolley ? 140f : 118f;
            if (Timer >= endTime && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        // 干渴汲取: 蒸发读条 → 三环干裂之环由内向外延迟起爆 (站桩必中, 移动必过)
        private void RunDrain(Player target) {
            // 最小施法距阀门: 太近直接放弃 (防贴脸炸)
            if ((int)Timer == 1 && NPC.WithinRange(target.Center, 200f) && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
                return;
            }

            NPC.velocity *= 0.9f;

            if ((int)Timer == 6 && !VaultUtils.isClient) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 0.7f }, target.Center);
                float[] radii = [260f, 520f, 780f];
                float[] fuses = [64f, 82f, 100f];
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<HanbaCrackRing>(), GetBossDamage(0.45f, true), 1f, Main.myPlayer,
                        radii[i], fuses[i]);
                }
                NPC.netUpdate = true;
            }

            // 汲取读条: 水汽自玩家方位蒸腾流向旱魃 (纯视觉)
            if (!Main.dedServ && Timer < 46 && Timer % 2 == 0) {
                Vector2 spawn = target.Center + Main.rand.NextVector2Circular(200f, 120f);
                Dust d = Dust.NewDustPerfect(spawn, DustID.Cloud, (NPC.Center - spawn) * 0.03f, 160, default, Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = true;
            }

            if ((int)Timer == 46) {
                ACMUtils.AddScreenShake(3f);
                HanbaScorchScreenSystem.PulseHeat(0.25f);
            }

            if (Timer >= 132 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        #endregion

        #region 蚀日 / P3 四式 / 焚天坠日

        // 蚀日 (≤60% 一次性, ~140f): 升空 + 焦日增辉 + 大地干裂场展开
        private void RunSunRite(Player target) {
            if ((int)Timer == 1) {
                ClearMyProjectiles();
                SoundEngine.PlaySound(SoundID.Zombie3 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
            }

            if (Timer < 20) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(target.Center + new Vector2(0, -380f)) * 14f, 0.1f);
            }
            else {
                NPC.velocity *= 0.9f;
            }

            // 灰烬上腾 (蒸发感)
            if (!Main.dedServ && Timer > 20 && Timer % 3 == 0) {
                Vector2 spawn = NPC.Center + new Vector2(Main.rand.NextFloat(-600f, 600f), Main.rand.NextFloat(150f, 420f));
                Dust d = Dust.NewDustPerfect(spawn, DustID.Torch, new Vector2(0, -Main.rand.NextFloat(2f, 5f)), 150, default, 1.3f);
                d.noGravity = true;
            }

            if ((int)Timer == 90) {
                SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.3f }, NPC.Center); //干雷
                ACMUtils.AddScreenShake(10f);
                bloomPulse = 0.85f;
                riteScarPos = new Vector2(NPC.Center.X, FindGroundY(NPC.Center.X, NPC.Center.Y));
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 2; i++) {
                        Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.7f + i * 0.5f);
                    }
                }
            }

            // 干裂场自落点扩张 (纯视觉贴花)
            if (Timer > 90 && riteScarPos != Vector2.Zero) {
                float t = MathHelper.Clamp((Timer - 90f) / 50f, 0f, 1f);
                HanbaScorchScreenSystem.AddScorchMark(riteScarPos, 720f, t, 0.6f * (1f - (Timer - 90f) / 220f));
            }

            if (Timer >= 140 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        // 鬼域焦笼 (重制): 有形移动落位 → 方形焦笼 + 8 眼激光依次张开 + 缓旋缓追
        private void RunCageLasers(Player target, ref bool overrideRotation) {
            const int slamFrame = 30;
            const int laserStart = 42;
            const int cageEnd = 430;

            if (Timer < slamFrame) {
                // 有形移动 (非瞬移): 快速冲至玩家上方
                Vector2 anchor = target.Center + new Vector2(0, -260f);
                NPC.velocity = NPC.SafeDirectionTo(anchor) * MathF.Min(46f, NPC.Distance(anchor) * 0.25f);
            }
            else if ((int)Timer == slamFrame) {
                NPC.velocity *= 0.2f;
                OrigRestrictionPos = NPC.Center;
                SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                ACMUtils.AddScreenShake(9f);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);
                    NPC.netUpdate = true;
                }
            }
            else if (Timer < cageEnd) {
                // 缓旋 (渐加速) + 缓追 (4.5px/f 恒可走位)
                overrideRotation = true;
                float spinSpeed = MathF.Min(0.012f + (Timer - slamFrame) * 0.00003f, 0.02f);
                NPC.rotation += spinSpeed;

                if (Timer > 90) {
                    NPC.ChasingBehavior(target.Center, 4.5f);
                }
                else {
                    NPC.velocity *= 0.85f;
                }
                NPC.damage = GetBossDamage(0.9f, true); //自旋躯体是缓慢的近身威胁

                // 8 眼激光依次张开 (每 12f 一根)
                if (Timer >= laserStart && Timer < laserStart + 8 * 12 && ((int)Timer - laserStart) % 12 == 0 && !VaultUtils.isClient) {
                    int k = ((int)Timer - laserStart) / 12;
                    Vector2 eyeOffset = EyesOffset[k];
                    Vector2 eyePos = NPC.Center + eyeOffset;
                    int proj = Projectile.NewProjectile(NPC.FromObjectGetParent(), eyePos, eyeOffset.UnitVector(),
                        ModContent.ProjectileType<HanbaLaser>(), GetBossDamage(0.55f, true), 2, -1, NPC.whoAmI);
                    if (Main.projectile[proj].ModProjectile is HanbaLaser laser) {
                        laser.offsetData = eyeOffset;
                        Main.projectile[proj].netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 0.6f }, eyePos);
                }
            }
            else if ((int)Timer == cageEnd) {
                overrideRotation = true;
                HanbaLaser.AllVanish();
                NPC.rotation = MathHelper.WrapAngle(NPC.rotation);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f }, NPC.Center);
            }
            else {
                // 笼碎收招: 角度归圆
                NPC.velocity *= 0.9f;
                NPC.rotation *= 0.85f;
                overrideRotation = true;
            }

            if (Timer >= cageEnd + 40 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        // 烈日灼柱: 定身 Execution 读条 → 金色太阳柱单向匀速扫射
        private void RunSunLance(Player target) {
            const int fireFrame = 24;

            if (Timer < fireFrame) {
                // 快速滑向舞台位 (屏内校正)
                float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
                Vector2 anchor = target.Center + new Vector2(side * 540f, -150f);
                NPC.Center = Vector2.Lerp(NPC.Center, anchor, 0.1f);
                NPC.velocity = Vector2.Zero;
            }
            else if ((int)Timer == fireFrame && !VaultUtils.isClient) {
                Vector2 aim = NPC.SafeDirectionTo(target.Center);
                // 扫向偏向玩家当前移动方向 (给出可读的逃逸路线)
                float sweepSign = MathF.Abs(target.velocity.X) > 1f
                    ? MathF.Sign(aim.RotatedBy(MathHelper.PiOver2).X * target.velocity.X)
                    : (Main.rand.NextBool() ? 1f : -1f);
                if (sweepSign == 0)
                    sweepSign = 1f;

                int proj = Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, aim,
                    ModContent.ProjectileType<HanbaBigLaser>(), GetBossDamage(0.7f, true), 0, -1, NPC.whoAmI);
                if (Main.projectile[proj].ModProjectile is HanbaBigLaser lance) {
                    lance.sweepDir = sweepSign;
                    lance.sweepArc = MathHelper.ToRadians(IsFinale ? 140f : 110f);
                    Main.projectile[proj].netUpdate = true;
                }
                NPC.netUpdate = true;
            }
            else {
                NPC.velocity *= 0.85f;
                // 灼柱张开瞬间的后坐 (t=fireFrame+78 预警结束)
                if ((int)Timer == fireFrame + 80) {
                    NPC.velocity = -NPC.SafeDirectionTo(target.Center) * 7f;
                }
            }

            if (Timer >= 360) {
                if ((int)Timer == 360) {
                    HanbaBigLaser.AllVanish(); //保底收束
                }
                if (Timer >= 380 && !VaultUtils.isClient) {
                    SwitchState(HState.Connect);
                }
            }
        }

        // 蜃景分身突进: 两侧蜃景显形 → 三体齐抽身 → 平行突进 (仅本体有判定)
        private void RunMirageRush(Player target) {
            const int launchFrame = 56;
            int rounds = IsFinale ? 2 : 1;

            if ((int)Timer == 12 && !VaultUtils.isClient) {
                int mirages = IsFinale ? 4 : 2;
                Vector2 perp = NPC.SafeDirectionTo(target.Center).RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < mirages; i++) {
                    float offset = (i % 2 == 0 ? 1f : -1f) * (420f + i / 2 * 380f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + perp * offset, Vector2.Zero,
                        ModContent.ProjectileType<HanbaMirage>(), 0, 0, Main.myPlayer, launchFrame - 12, 58f);
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (Timer < launchFrame - 12) {
                NPC.velocity *= 0.9f;
            }
            else if (Timer < launchFrame) {
                float t = (Timer - (launchFrame - 12)) / 12f;
                NPC.velocity = -NPC.SafeDirectionTo(target.Center) * MathF.Pow(t, 8f) * 18f;
            }
            else if ((int)Timer == launchFrame) {
                Vector2 dir = NPC.SafeDirectionTo(target.Center + target.velocity * 10f);
                NPC.velocity = dir * 58f;
                NPC.oldPos = new Vector2[NPC.oldPos.Length];
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(5f);
            }
            else if (Timer <= launchFrame + 10) {
                // 直线突进
            }
            else {
                NPC.velocity *= 0.68f;
            }

            if (NPC.velocity.Length() > 20f && Timer >= launchFrame) {
                NPC.damage = GetBossDamage(1.55f, true);
            }

            if (Timer >= launchFrame + 40 && !VaultUtils.isClient) {
                SubState++;
                if (SubState >= rounds) {
                    SwitchState(HState.Connect);
                }
                else {
                    Timer = 0;
                    NPC.netUpdate = true;
                }
            }
        }

        // 蝗神蔽日: 横竖两道蝗墙错拍十字交扫
        private void RunLocustCross(Player target) {
            if ((int)Timer == 1 && !VaultUtils.isClient) {
                otherAI[3] = Main.rand.NextBool() ? 1 : -1;
                NPC.netUpdate = true;
            }

            Vector2 hoverPos = target.Center + new Vector2(0, -400f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverPos) * MathF.Min(15f, NPC.Distance(hoverPos) * 0.06f), 0.07f);

            // 双向蝗云预兆
            if (!Main.dedServ && Timer < 70 && Timer % 5 == 0) {
                bool horizontal = Main.rand.NextBool();
                Vector2 spawn = horizontal
                    ? target.Center + new Vector2(-otherAI[3] * 1500f, Main.rand.NextFloat(-450f, 450f))
                    : target.Center + new Vector2(Main.rand.NextFloat(-450f, 450f), -1300f);
                Vector2 vel = horizontal ? new Vector2(otherAI[3] * 6f, 0) : new Vector2(0, 5f);
                PRTLoader.NewParticle<LocustPRT>(spawn, vel);
            }

            if ((int)Timer == 70 && !VaultUtils.isClient) {
                SpawnLocustWall(target, WallDirSign(), vertical: false);
            }
            if ((int)Timer == 94 && !VaultUtils.isClient) {
                SpawnLocustWall(target, 1f, vertical: true);
            }

            if (Timer >= 310 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        private float WallDirSign() => otherAI[3] == 0 ? 1f : otherAI[3];

        // 焚天坠日 (≤30% 一次性 set-piece): 巨日凝聚坠地 — 全场唯一冲击帧
        private void RunSunFall(Player target) {
            if ((int)Timer == 1) {
                ClearMyProjectiles();
                SoundEngine.PlaySound(SoundID.Zombie3 with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
            }

            if (Timer < 30) {
                // 冲上高位旁观 (有形移动)
                float side = NPC.Center.X >= target.Center.X ? 1f : -1f;
                Vector2 anchor = target.Center + new Vector2(side * 680f, -520f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(anchor) * MathF.Min(34f, NPC.Distance(anchor) * 0.15f), 0.12f);
            }
            else if ((int)Timer == 30 && !VaultUtils.isClient) {
                float landY = FindGroundY(target.Center.X, target.Center.Y);
                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                    new Vector2(target.Center.X, target.Center.Y - 700f), Vector2.Zero,
                    ModContent.ProjectileType<HanbaSunOrb>(), GetBossDamage(0.75f, true), 4f, Main.myPlayer, landY);
                NPC.netUpdate = true;
            }
            else if (Timer < 200) {
                NPC.velocity *= 0.9f; //静观巨日坠落 (本体零威胁)
            }
            else if ((int)Timer == 200) {
                // 坠日后俯冲回场
                Vector2 dir = NPC.SafeDirectionTo(target.Center + target.velocity * 10f);
                NPC.velocity = dir * 54f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
            }
            else if (Timer > 212) {
                NPC.velocity *= 0.7f;
            }

            if (NPC.velocity.Length() > 20f && Timer >= 200) {
                NPC.damage = GetBossDamage(1.55f, true);
            }

            if (Timer >= 252 && !VaultUtils.isClient) {
                SwitchState(HState.Connect);
            }
        }

        #endregion

        #region 死亡 / 脱战

        // 死亡编排 (~180f): 定格 → 八眼逐一熄灭 → 躯体自下而上灰化 → 天幕退红 → 轻白闪真死
        private void RunDeath() {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;

            // 八眼熄灭小爆 (与解封点睛互为镜像; 熄灭顺序取反)
            if (Timer >= 10 && Timer < 122 && ((int)Timer - 10) % 14 == 0) {
                int k = 7 - ((int)Timer - 10) / 14;
                if (k >= 0 && k < 8) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.5f - k * 0.12f, Volume = 0.45f }, NPC.Center);
                    if (!Main.dedServ) {
                        Vector2 eyePos = NPC.Center + EyesOffset[k].RotatedBy(NPC.rotation);
                        for (int i = 0; i < 6; i++) {
                            Dust d = Dust.NewDustPerfect(eyePos, DustID.Smoke,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 150, default, 1.3f);
                            d.noGravity = true;
                        }
                    }
                }
            }

            // 灰化余烬随风 (密度 ∝ 进度)
            if (!Main.dedServ && Timer > 60) {
                float t = MathHelper.Clamp((Timer - 60f) / 108f, 0f, 1f);
                int count = 1 + (int)(t * 3);
                for (int i = 0; i < count; i++) {
                    Vector2 spawn = NPC.Center + new Vector2(Main.rand.NextFloat(-70f, 70f),
                        MathHelper.Lerp(70f, -80f, t) + Main.rand.NextFloat(-20f, 20f));
                    Dust d = Dust.NewDustPerfect(spawn, Main.rand.NextBool() ? DustID.Smoke : DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-0.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)), 160, default, Main.rand.NextFloat(1f, 1.8f));
                    d.noGravity = true;
                }
            }

            if ((int)Timer == 168) {
                HanbaScorchScreenSystem.FlashWhite(0.5f);
                ACMUtils.AddScreenShake(10f);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.4f }, NPC.Center);
            }

            if (Timer >= 178 && !VaultUtils.isClient) {
                sunScarActive = false;
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead(); //State==Death → CheckDead 放行, OnKill/掉落正常结算
            }
        }

        private void RunDespawn() {
            if ((int)Timer == 1) {
                ClearMyProjectiles();
            }
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            if (Timer > 120 && !VaultUtils.isClient) {
                NPC.active = false;
                NPC.netUpdate = true;
            }
        }

        #endregion

        #region 视觉状态推导

        /// <summary>由同步状态确定性推导各端视觉标量 (眼光/灰化/旱情/热浪/天幕联动), 不联网。</summary>
        private void UpdateVisuals(Player target) {
            // —— 溶解: 入场反向显形 / 死亡·脱战正向灰化 ——
            if (State == HState.Intro) {
                dissolve = MathHelper.Clamp(1f - Timer / 100f, 0f, 1f);
            }
            else if (State == HState.Death) {
                dissolve = MathHelper.Clamp((Timer - 60f) / 108f, 0f, 0.96f);
            }
            else if (State == HState.Despawn) {
                dissolve = MathHelper.Clamp(Timer / 90f, 0f, 1f);
            }
            else {
                dissolve = 0f;
            }

            // —— 符纸烧毁进度 (解封演出) ——
            talismanBurn = State == HState.Unseal ? MathHelper.Clamp(Timer / 40f, 0f, 1f) : (HasTalisman ? 0f : 1f);

            // —— 封尸灰化 ——
            float greyTarget = HasTalisman || State == HState.Intro ? 1f : 0f;
            if (State == HState.Unseal)
                greyTarget = 1f - MathHelper.Clamp(Timer / 130f, 0f, 1f);
            sealGrey = MathHelper.Lerp(sealGrey, greyTarget, 0.05f);

            // —— 眼光目标 ——
            for (int k = 0; k < 8; k++) {
                eyeGlow[k] = MathHelper.Lerp(eyeGlow[k], EyeGlowTarget(k), 0.15f);
            }

            // —— 旱情/热浪 ——
            float droughtTarget = HasTalisman ? 0.22f : (IsFinale ? 0.8f : (IsPhase3 ? 0.6f : 0.42f));
            if (State == HState.Death)
                droughtTarget = MathHelper.Clamp(1f - Timer / 140f, 0f, 1f) * 0.5f;
            droughtVisual = MathHelper.Lerp(droughtVisual, droughtTarget, 0.01f);

            heatAura = MathHelper.Lerp(heatAura, HasTalisman || State == HState.Death ? 0f : (IsFinale ? 0.8f : 0.5f), 0.02f);
            bloomPulse *= 0.94f;

            // 符纸看门狗: 封印中但符纸 NPC 意外消失 → 强制解封 (防永久无敌死局; 服务器权威)
            if (HasTalisman && State != HState.Intro && !VaultUtils.isClient && (int)GlobalTimer % 60 == 0) {
                bool found = false;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Talisman>() && (int)npc.ai[0] == NPC.whoAmI) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    TalismanKill();
                }
            }

            if (Main.dedServ)
                return;

            // —— 屏幕系统与天幕联动 (仅客户端发布) ——
            float ambientWarp = IsFinale && State != HState.Death ? 0.13f : 0f;
            HanbaScorchScreenSystem.Publish(droughtVisual, ambientWarp, GlobalTimer / 60f);

            float sunFlare = State switch {
                HState.SunRite => MathHelper.Clamp(Timer / 90f, 0f, 1f),
                HState.SunFall => 1f,
                _ => IsFinale ? 0.55f : (IsPhase3 ? 0.35f : 0f)
            };
            float sunAsh = State == HState.Death ? MathHelper.Clamp(Timer / 140f, 0f, 1f) : 0f;
            HanbaSky.PublishSunState(sunFlare, sunAsh);

            // 坠日焦土场常驻印记
            if (sunScarActive) {
                HanbaScorchScreenSystem.AddScorchMark(sunScarCenter, 900f, 1f, 0.5f);
            }

            Lighting.AddLight(NPC.Center, Color.Orange.ToVector3() * NPC.scale * (1f - dissolve));
        }

        // 各眼目标亮度 — 全部由同步计时器推导, 各端一致
        private float EyeGlowTarget(int k) {
            switch (State) {
                case HState.Intro:
                    if (k == 3 && Timer >= 120) return 0.8f;
                    if (k == 4 && Timer >= 140) return 0.8f;
                    return 0f;
                case HState.SealedHop:
                    return k is 3 or 4 ? (Timer < 40 ? 0.5f + 0.5f * MathF.Sin(Timer * 0.5f) : 1f) : 0f;
                case HState.SealedGaze:
                    return k is 3 or 4 ? (0.5f + 0.5f * MathF.Sin(Timer * 0.35f + k)) : 0f;
                case HState.SealedLocust:
                case HState.Connect when HasTalisman:
                    return k is 3 or 4 ? 0.4f : 0f;
                case HState.Unseal:
                    return Timer >= 40 + k * 9 ? (Timer is >= 112 and < 130 ? 0.35f : 1f) : (k is 3 or 4 ? 0.4f : 0f);
                case HState.EyeVolley:
                    return Timer >= k * 6 ? 1f : 0.4f;
                case HState.CageLasers:
                case HState.SunRite:
                case HState.SunFall:
                    return 1f;
                case HState.ScorchDash:
                case HState.MirageRush:
                    return Timer > 30 ? 1f : 0.6f;
                case HState.Death:
                    return Timer > 10 + (7 - k) * 14 ? 0f : 0.9f;
                case HState.Despawn:
                    return 0f;
                default:
                    return HasTalisman ? (k is 3 or 4 ? 0.4f : 0f) : 0.55f;
            }
        }

        #endregion

        #region 绘制

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                return;
            }
            int Hanba_Body = Mod.Find<ModGore>("Hanba_Body2").Type;
            int Hanba_Body2 = Mod.Find<ModGore>("Hanba_Body2").Type;
            int Hanba_Eye = Mod.Find<ModGore>("Hanba_Eye").Type;
            int Hanba_Top = Mod.Find<ModGore>("Hanba_Top").Type;

            var entitySource = NPC.GetSource_Death();

            for (int i = 0; i < 2; i++) {
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Body);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Body2);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Top);
            }
            foreach (var pos in EyesOffset) {
                Gore.NewGore(entitySource, NPC.Center + pos, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Eye);
            }
        }

        //鬼域焦笼边界贴花 (ArenaRunic 方形), 让 800 半笼范围可读。自管批次并恢复默认批。
        private static void DrawCageDecal(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity) {
            if (Main.dedServ || intensity <= 0.01f)
                return;

            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 centerUV, out float radiusUV, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uRadius"]?.SetValue(radiusUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Flame.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(150, 20, 20).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(1f); //方形竞技场

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.NonPremultiplied);
        }

        // 冲刺预警线 (SealedHop / ScorchDash / MirageRush 蓄势窗口)
        private void DrawDashTelegraph(Player target) {
            int launchFrame = State == HState.SealedHop ? 40 : (State == HState.ScorchDash ? 46 : 56);
            int showFrom = launchFrame - 28;
            if (Timer < showFrom || Timer > launchFrame)
                return;

            float t = MathHelper.Clamp((Timer - showFrom) / (launchFrame - (float)showFrom), 0f, 1f);
            Vector2 dir = NPC.SafeDirectionTo(target.Center + target.velocity * 10f);
            Color warn = TelegraphColors.Lethal * (0.25f + 0.5f * t);
            ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1100f, 4f + t * 4f, warn, TelegraphColors.Lethal * 0.4f, 0.35f + t * 0.45f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //鬼域焦笼贴花 (底层)
            if (State == HState.CageLasers) {
                float at = Timer;
                float fadeIn = MathHelper.Clamp((at - 30f) / 60f, 0f, 1f);
                float fadeOut = MathHelper.Clamp((470f - at) / 60f, 0f, 1f);
                DrawCageDecal(spriteBatch, OrigRestrictionPos, 800f, fadeIn * fadeOut * 0.85f);
            }

            if (NPC.target >= 0 && NPC.target < Main.maxPlayers
                && (State == HState.SealedHop || State == HState.ScorchDash || State == HState.MirageRush)) {
                Player target = Main.player[NPC.target];
                if (target.Alives()) {
                    DrawDashTelegraph(target);
                }
            }

            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            Vector2 origin = rectangle.Size() / 2;

            // 封尸尸抖 (确定性高频微颤, 纯视觉)
            Vector2 jitter = Vector2.Zero;
            if (sealGrey > 0.1f && dissolve <= 0.01f) {
                jitter = new Vector2(MathF.Sin(GlobalTimer * 2.7f) * 1.2f, MathF.Cos(GlobalTimer * 3.9f) * 0.8f) * sealGrey;
            }
            Vector2 drawCenter = NPC.Center + jitter - Main.screenPosition;

            // 速度门控残影 (仅爆发帧, 随旋转, 暖色加性)
            if (NPC.velocity.Length() > 20f) {
                float sengs = 0.34f;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    Color trailColor = Color.Lerp(HanbaVFX.EmberOrange, HanbaVFX.SunGold, i / (float)NPC.oldPos.Length) * sengs;
                    trailColor.A = 0;
                    spriteBatch.Draw(mainValue, drawOldPos, rectangle, trailColor,
                        NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0);
                    sengs *= 0.78f;
                }
            }

            // 体表热浪蜃影 (解封后): 双层偏移加性重影
            if (heatAura > 0.05f && dissolve <= 0.01f) {
                for (int i = 0; i < 2; i++) {
                    float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 6.5f + i * 2.4f) * (2.5f + i * 2f);
                    Color aura = HanbaVFX.EmberOrange * (0.16f * heatAura);
                    aura.A = 0;
                    spriteBatch.Draw(mainValue, drawCenter + new Vector2(wobble, -i * 2f), rectangle, aura,
                        NPC.rotation, origin, NPC.scale * (1f + i * 0.012f), SpriteEffects.None, 0);
                }
            }

            // 本体: 溶解显形/灰化 (DissolveBurn) 或常规绘制 (封尸期降饱和)
            if (dissolve > 0.01f) {
                Vector2 sweepDir = State == HState.Intro ? Vector2.Zero : new Vector2(0, -1f);
                float sweepStr = State == HState.Intro ? 0f : 0.8f;
                WeaponVFX.ApplyDissolveBurn(mainValue, NPC.Center + jitter, rectangle, drawColor,
                    NPC.rotation, origin, NPC.scale, dissolve, 1f, HanbaVFX.EmberOrange,
                    0.09f, 2.2f, sweepDir, sweepStr);
            }
            else {
                // 封尸降饱和: 向灰绿尸色靠拢
                Color bodyColor = drawColor;
                if (sealGrey > 0.01f) {
                    byte lum = (byte)((drawColor.R * 3 + drawColor.G * 5 + drawColor.B * 2) / 10);
                    Color grey = new Color(lum, lum, lum).MultiplyRGB(HanbaVFX.CorpseGrey);
                    bodyColor = Color.Lerp(drawColor, grey, sealGrey * 0.72f);
                }
                spriteBatch.Draw(mainValue, drawCenter, rectangle, bodyColor,
                    NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0);
            }

            // 八眼状态化辉光
            if (dissolve < 0.6f) {
                for (int k = 0; k < 8; k++) {
                    float glow = eyeGlow[k] * (1f - dissolve);
                    if (glow <= 0.03f)
                        continue;
                    Vector2 eyePos = NPC.Center + jitter + EyesOffset[k].RotatedBy(NPC.rotation);
                    float breath = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + k * 1.3f) * 0.12f;
                    HanbaVFX.DrawGlow(spriteBatch, eyePos, (0.16f + glow * 0.2f) * breath, HanbaVFX.EmberOrange * (0.9f * glow));
                    HanbaVFX.DrawGlow(spriteBatch, eyePos, (0.07f + glow * 0.08f) * breath, HanbaVFX.SunGold * glow);
                }
            }

            // 解封演出: 符纸就地烧尽 (符纸 NPC 已死, 由本体接管绘制)
            if (State == HState.Unseal && talismanBurn < 1f && Talisman?.Value != null) {
                Texture2D talis = Talisman.Value;
                WeaponVFX.ApplyDissolveBurn(talis, NPC.Center + jitter, null, drawColor,
                    NPC.rotation, talis.Size() / 2f, 0.4f, talismanBurn, 1f, HanbaVFX.EmberOrange,
                    0.12f, 2.6f, new Vector2(0, 1f), 0.5f);
            }

            // 活体符纸 (封印期由符纸 NPC 提供摆动数据)
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != ModContent.NPCType<Talisman>()) {
                    continue;
                }
                if (npc.ai[0] != NPC.whoAmI) {
                    continue;
                }
                if (npc.ModNPC is Talisman talisman) {
                    talisman.DoDraw(spriteBatch, drawColor);
                }
            }

            // 泛光脉冲 (解封尖啸/蚀日; 内部自动申请全屏名额)
            if (bloomPulse > 0.05f) {
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.2f, bloomPulse * 0.85f, HanbaVFX.SunGold);
            }

            return false;
        }

        /// <summary>签名时刻的全屏热浪扭曲 (GenericWarp heat)。占用唯一全屏名额, 平时强度 0 直接早退。</summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            float warp = HanbaScorchScreenSystem.CurrentHeatWarp;
            if (warp <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(warp, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(1.0f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uWarpScale"]?.SetValue(1.25f);
            fx.Parameters["uChroma"]?.SetValue(0.35f);
            fx.Parameters["uRadialPull"]?.SetValue(-0.18f); //轻微外推 = 热浪上腾
            fx.Parameters["uMode"]?.SetValue(0f);           //heat 主题
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.5f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion
    }

    /// <summary>
    /// 符纸封印 — 僵尸始祖额上的封印纸符 (击破解封)。V3: 悬摆动画 + 受击闪 + 空引用修复 + 血量随人数缩放。
    /// </summary>
    internal class Talisman : ModNPC
    {
        private Hanba Host { get; set; }
        private float swayPhase;
        private float hitFlash;

        public override void SetDefaults() {
            NPC.npcSlots = 4f;
            NPC.width = 40;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 0; //符纸本身无接触伤害 (它悬在 Boss 身上, 归本体管)
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            NPC.lifeMax = 60000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(50000 * balance * bossAdjustment);
            if (Main.expertMode) {
                NPC.lifeMax += 5000;
            }
            if (Main.masterMode) {
                NPC.lifeMax += 5000;
            }
        }

        public override void AI() {
            NPC host = Main.npc[(int)NPC.ai[0]];
            if (host.Alives() && host.ModNPC is Hanba boss) {
                Host = boss;
                NPC.Center = boss.NPC.Center;
                NPC.rotation = boss.NPC.rotation;
            }
            else if (!VaultUtils.isClient) {
                // 宿主没了 → 符纸静默消失 (服务器权威, 不触发解封)
                NPC.active = false;
                NPC.netUpdate = true;
            }

            swayPhase += 0.045f;
            hitFlash *= 0.9f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            hitFlash = 1f;
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 44f),
                    DustID.Torch, new Vector2(hit.HitDirection * 1.5f, -Main.rand.NextFloat(1f, 2f)), 140, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnKill() {
            if (Host != null && Host.NPC.Alives()) {
                Host.TalismanKill();
            }
        }

        public void DoDraw(SpriteBatch spriteBatch, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            // 悬摆: 纸符钉在额上随风轻摆
            float sway = MathF.Sin(swayPhase) * 0.09f + MathF.Sin(swayPhase * 2.3f) * 0.025f;
            Color color = Color.Lerp(drawColor, Color.White, hitFlash * 0.8f);
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition + new Vector2(0, -8f), null, color,
                NPC.rotation + sway, new Vector2(mainValue.Width / 2f, mainValue.Height * 0.12f), 0.4f, SpriteEffects.None, 0);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}

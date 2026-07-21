using AncientChineseMythology.Underworlds;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    /// <summary>
    /// 玩家侧支援: 轻量演出镜头 (入场专用, 变焦钳制) 与「勾魂」机制载体。
    /// 勾魂 = 马面标记玩家 3s, 期间攻击牛头即可打断; 到期被拽向马面 (锁命角色分工的反制课题)。
    /// </summary>
    public class NiuMaPlayer : ModPlayer
    {
        // ===== 演出镜头 (仅入场/本地) =====
        private Vector2 ScrPos;
        private bool Start_SetScrPos = false;
        private float Timer_SetScrPos = 1;
        public void SetScreenPos(Vector2 ToVec) {
            ScrPos = Vector2.Lerp(ScrPos, ToVec - Main.ScreenSize.ToVector2() * .5f, .04f);
            Start_SetScrPos = true;
            Timer_SetScrPos = 0;
        }
        public void SetScreenShake(double _ShakeScale, double _ShakeTime) {
            ACMScreenShakeSystem.Add((float)_ShakeScale);
        }
        private float OldZoom;
        private float Target_SetZoom = 1, Timer_SetZoom = 1;
        private bool Start_SetZoom = false;
        public void SetZoom(float zoom) {
            zoom = MathHelper.Clamp(zoom, 0.8f, 1.35f); // 变焦钳制: 演出不夺操作
            Target_SetZoom = MathHelper.Lerp(Target_SetZoom, zoom, .02f);
            Start_SetZoom = true;
            Timer_SetZoom = 0;
        }

        public override void ModifyScreenPosition() {
            if (!Start_SetScrPos) {
                Timer_SetScrPos = 1;
                ScrPos = Main.screenPosition;
            }
            else {
                Main.screenPosition = ScrPos;
                if (Timer_SetScrPos < 0.9) {
                    Timer_SetScrPos = MathHelper.Lerp(Timer_SetScrPos, 1, .05f);
                    ScrPos = Vector2.Lerp(ScrPos, Player.Center - Main.ScreenSize.ToVector2() * .5f, Timer_SetScrPos * .1f);
                }
                else Start_SetScrPos = false;
            }

            if (Start_SetZoom) {
                Main.GameZoomTarget = Target_SetZoom;
                if (Timer_SetZoom < .9f || Math.Abs(Main.GameZoomTarget - OldZoom) > .08) {
                    Timer_SetZoom = MathHelper.Lerp(Timer_SetZoom, 1, .05f);
                    Target_SetZoom = MathHelper.Lerp(Target_SetZoom, OldZoom, Timer_SetScrPos * .1f);
                }
                else {
                    Main.GameZoomTarget = OldZoom;
                    Start_SetZoom = false;
                }
            }
            else {
                Target_SetZoom = OldZoom = Main.GameZoomTarget;
            }
            base.ModifyScreenPosition();
        }

        // ================= 勾魂 Soul Hook =================
        // 马面标记玩家 3s; 期间若未攻击牛头打断 -> 到期被拉向马面 200px。
        public int SoulHookTimer;        // >0 = 标记倒计时 (180 起)
        public int SoulHookCaster = -1;  // 施放马面 whoAmI
        private int SoulHookPull;        // >0 = 正在被拽
        private Vector2 SoulHookPullTarget;

        public void ApplySoulHook(int casterWho) {
            if (SoulHookTimer > 0 || SoulHookPull > 0)
                return;
            SoulHookTimer = 180;
            SoulHookCaster = casterWho;
            Player.AddBuff(ModContent.BuffType<SoulHookBuff>(), 180);
            UnderworldField.AddNetherDecree(Player, 1); // 冥律: 灵魂牵引判定
            if (Player.whoAmI == Main.myPlayer)
                CombatText.NewText(Player.Hitbox, TelegraphColors.NetherViolet, "勾魂");
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 反制: 攻击牛头即可打断勾魂 (锁命角色分工)。
            if (SoulHookTimer > 0 && target.type == ModContent.NPCType<NiuTou>()) {
                SoulHookTimer = 0;
                SoulHookCaster = -1;
                Player.ClearBuff(ModContent.BuffType<SoulHookBuff>());
                if (Player.whoAmI == Main.myPlayer)
                    CombatText.NewText(Player.Hitbox, TelegraphColors.Safe, "勾魂已破");
            }
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void PostUpdate() {
            UpdateSoulHook();
        }

        private void UpdateSoulHook() {
            if (SoulHookPull > 0) {
                SoulHookPull--;
                Player.velocity *= 0.55f;
                Player.Center = Vector2.Lerp(Player.Center, SoulHookPullTarget, 0.22f);
                if (!Main.dedServ && Main.rand.NextBool()) {
                    var d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(22, 22), DustID.Shadowflame);
                    d.noGravity = true;
                    d.velocity = (SoulHookPullTarget - Player.Center).SafeNormalize(Vector2.Zero) * 4f;
                }
                return;
            }
            if (SoulHookTimer <= 0)
                return;

            SoulHookTimer--;
            // 预告: 玩家周身收束的幽紫符环 (越近到期越亮)。
            if (!Main.dedServ && SoulHookTimer % 3 == 0) {
                float frac = SoulHookTimer / 180f;
                float r = 38f + 54f * frac;
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Player.Center + a.ToRotationVector2() * r;
                var d = Dust.NewDustPerfect(pos, DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = (Player.Center - pos).SafeNormalize(Vector2.Zero) * 2.6f;
            }

            if (SoulHookTimer == 0) {
                // 未被打断 -> 拽向马面 (最多 200px)。
                Player.ClearBuff(ModContent.BuffType<SoulHookBuff>());
                NPC ma = (SoulHookCaster >= 0 && SoulHookCaster < Main.maxNPCs) ? Main.npc[SoulHookCaster] : null;
                if (ma != null && ma.active) {
                    Vector2 toMa = ma.Center - Player.Center;
                    float dist = toMa.Length();
                    Vector2 dir = toMa.SafeNormalize(Vector2.UnitX);
                    SoulHookPullTarget = Player.Center + dir * Math.Min(dist, 200f);
                    SoulHookPull = 14;
                    UnderworldField.AddSoulErosion(Player, 2); // 魂蚀
                    ACMScreenShakeSystem.Add(5f);
                    if (Player.whoAmI == Main.myPlayer)
                        CombatText.NewText(Player.Hitbox, TelegraphColors.Execution, "勾魂!");
                }
                SoulHookCaster = -1;
            }
        }
    }

    /// <summary>
    /// 牛头马面共用状态机骨架 —— 「缠斗者/炮台」岗位轮换双 Boss (PACING §5):
    /// 同一时刻一吏贴身压迫、一吏远程控场, 每 2 招换岗对穿; 半血结链解锁合体技;
    /// 双低血「阎罗令」狂怒; 一方阵亡由同伴引魂复生 (尸位反制圈可打断, 断则双亡)。
    /// 牛头为指挥 (节拍/合体/换岗由其推动), 马面读取镜像。
    /// ai[0]=状态 ai[1]=状态内计时 ai[2]=子相位 ai[3]=岗位 (两吏同值, 各自反相解读)。
    /// </summary>
    public abstract class NiuMaBoss : ModNPC
    {
        // ===== 状态常量 (两吏共用语义) =====
        public const int StIntro = 0, StSelect = 1, StSwap = 2, StP2 = 3, StP3 = 4,
            StReviving = 5, StReborn = 6, StDeath = 7;
        public const int StLane = 30, StLink = 31;

        public ref float State => ref NPC.ai[0];
        public ref float Timer => ref NPC.ai[1];
        public ref float Sub => ref NPC.ai[2];
        public ref float Duty => ref NPC.ai[3];

        // ===== 同步旗标 (SendExtraAI) =====
        public bool DidP2;                 // 合体技已解锁
        public bool DidP3;                 // 阎罗令狂怒
        public bool HasRespawn;            // 已用过一次复生
        protected bool deathStarted;
        protected bool deathFinished;
        protected int attackAlt;           // 岗位内两招交替指针
        protected int attacksInDuty;       // 指挥: 本岗已完成招数
        protected bool comboDue;           // 指挥: 下一节拍是合体技
        protected int comboAlt;            // 指挥: 合体技交替指针

        // ===== 搭档 =====
        protected int partnerWho = -1;
        public abstract int PartnerType { get; }
        public NPC Partner => NiuMaHelper.FindBoss(PartnerType, ref partnerWho);
        public NiuMaBoss PartnerBoss => Partner?.ModNPC as NiuMaBoss;

        /// <summary>指挥者 (牛头)。节拍/换岗/合体/全局阶段由指挥推动, 搭档被强制同步。</summary>
        public abstract bool IsConductor { get; }
        /// <summary>本吏当前是否缠斗岗 (两吏对同一 Duty 值反相解读)。</summary>
        public abstract bool IsBrawler { get; }
        /// <summary>入场/站位偏好侧 (指挥 -1 左, 搭档 +1 右)。</summary>
        protected int HomeSide => IsConductor ? -1 : 1;
        protected abstract Color ThemeColor { get; }
        protected abstract Color ThemeCore { get; }
        protected abstract SoundStyle RoarSound { get; }

        public float LifeFrac => NPC.life / (float)NPC.lifeMax;
        public Player Target => Main.player[NPC.target];
        //protected internal: 兄弟实例经基类型引用互查状态 (纯 protected 会触发 CS1540)
        protected internal bool InNormalFlow => State == StSelect || State >= 10;

        // ===== 视觉 (纯本地) =====
        protected float drawAlpha = 1f;
        protected float flashInten;        // 受击/演出闪白脉冲
        protected float chargeInten;       // 蓄力增焰
        protected bool drawTail;
        private Vector2[][] hangChains;    // 垂坠锁链 (重量感次级运动)
        protected int hitChainImpulse;     // >0: 垂链受冲击甩尾 (发招/急停时设置)

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults() {
            NPC.width = 70;
            NPC.height = 70;
            NPC.lifeMax = 14000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0;
            NPC.damage = 45;
            NPC.defense = 13;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.rarity = 2;
            NPC.scale = 2.4f;
            NPC.value = Item.buyPrice(gold: 6);
            NPC.npcSlots = 12f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            Music = MusicID.Boss2;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.85f * balance * bossAdjustment);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(DidP2);
            writer.Write(DidP3);
            writer.Write(HasRespawn);
            writer.Write(deathStarted);
            writer.Write(deathFinished);
            writer.Write((byte)attackAlt);
            writer.Write((byte)attacksInDuty);
            writer.Write(comboDue);
            writer.Write((byte)comboAlt);
        }
        public override void ReceiveExtraAI(BinaryReader reader) {
            DidP2 = reader.ReadBoolean();
            DidP3 = reader.ReadBoolean();
            HasRespawn = reader.ReadBoolean();
            deathStarted = reader.ReadBoolean();
            deathFinished = reader.ReadBoolean();
            attackAlt = reader.ReadByte();
            attacksInDuty = reader.ReadByte();
            comboDue = reader.ReadBoolean();
            comboAlt = reader.ReadByte();
        }

        public override bool CheckActive() => false;

        // ================= 状态切换 =================

        public void SwitchState(int st) {
            State = st;
            Timer = 0;
            Sub = 0;
            NPC.netUpdate = true;
        }

        /// <summary>攻击完成 → 连接节拍 (Select)。指挥在此累计岗内招数。</summary>
        protected void EndAttack() {
            if (IsConductor)
                attacksInDuty++;
            SwitchState(StSelect);
        }

        /// <summary>指挥: 双吏同帧进入同一状态 (换岗/合体/阶段演出)。</summary>
        protected void ForceBoth(int st) {
            SwitchState(st);
            NPC p = Partner;
            if (p != null && p.ModNPC is NiuMaBoss pb) {
                pb.SwitchState(st);
                p.netUpdate = true;
            }
        }

        // ================= AI 主循环 =================

        public override void AI() {
            // 目标失效 → 重选; 无人可战 → 升天撤离 (死亡演出不弃演, 保证掉落)
            if (NPC.target < 0 || NPC.target >= 255 || Target.dead || !Target.active)
                NPC.TargetClosest();
            if ((Target.dead || !Target.active) && State != StDeath) {
                NPC.velocity.Y -= 0.4f;
                NPC.velocity.X *= 0.98f;
                NPC.EncourageDespawn(30);
                return;
            }

            // 地府氛围染屏 (廉价层, 同帧取 max)
            float tint = 0.2f;
            if (DidP2) tint += 0.1f;
            if (DidP3) tint += 0.08f;
            if (State == StP2 || State == StLink) tint += 0.08f;
            NiuMaScreenSystem.Publish(NPC.Center, tint);
            if (DidP3)
                NiuMaScreenSystem.PublishVignette(State == StP3 ? 0.85f : 0.4f);

            Lighting.AddLight(NPC.Center, ThemeColor.ToVector3() * 0.55f);

            // 接触伤害默认关闭; 只有明确的攻击窗口 (冲撞/闪步) 显式开启 —— 伤害窗口与视觉严格对齐
            NPC.damage = 0;

            Timer++;
            switch ((int)State) {
                case StIntro: RunIntro(); break;
                case StSelect: RunSelect(); break;
                case StSwap: RunSwap(); break;
                case StP2: RunP2Transition(); break;
                case StP3: RunP3Rage(); break;
                case StReviving: RunReviving(); break;
                case StReborn: RunReborn(); break;
                case StDeath: RunDeathCinematic(); break;
                default: RunAttack((int)State); break;
            }

            // 全局节拍检查 (指挥推动; 搭档缺席时各自兜底狂怒)
            if (IsConductor || Partner == null)
                CheckGlobalBeats();

            UpdateVisuals();
        }

        protected abstract void RunAttack(int state);
        /// <summary>Select 结束时选择下一状态 (指挥在此裁决换岗/合体节拍)。</summary>
        protected abstract int ChooseNext();

        // ================= 全局节拍 (P2/P3) =================

        private void CheckGlobalBeats() {
            NPC p = Partner;
            NiuMaBoss pb = PartnerBoss;

            if (p != null && pb != null && IsConductor) {
                bool bothNormal = InNormalFlow && pb.InNormalFlow;
                // P2: 任一 ≤55% → 结链演出, 解锁合体技
                if (!DidP2 && bothNormal && (LifeFrac <= 0.55f || pb.LifeFrac <= 0.55f)) {
                    DidP2 = true;
                    pb.DidP2 = true;
                    comboDue = false;
                    NiuMaHelper.ClearHostileProjectiles(); // 换阶段清弹 (公平阀门)
                    ForceBoth(StP2);
                }
                // P3: 双方 ≤28% → 阎罗令狂怒
                else if (DidP2 && !DidP3 && bothNormal && LifeFrac <= 0.28f && pb.LifeFrac <= 0.28f) {
                    DidP3 = true;
                    pb.DidP3 = true;
                    NiuMaHelper.ClearHostileProjectiles();
                    ForceBoth(StP3);
                }
            }
            else if (p == null) {
                // 孤军: 自触发狂怒 (阶段演出压缩版)
                if (!DidP3 && InNormalFlow && LifeFrac <= 0.25f) {
                    DidP3 = true;
                    NiuMaHelper.ClearHostileProjectiles();
                    SwitchState(StP3);
                }
                if (!DidP2 && LifeFrac <= 0.55f)
                    DidP2 = true; // 孤军无结链演出, 直接吃 P2 数值
            }
        }

        // ================= 入场 =================

        private void RunIntro() {
            NPC.dontTakeDamage = false;
            if (Timer <= 18) {
                // 跃出鬼门: 一步到侧翼站位
                if (Timer == 1) {
                    Vector2 anchor = Target.Center + new Vector2(HomeSide * 480, -220);
                    NPC.velocity = (anchor - NPC.Center) / 18f + new Vector2(0, -3.5f);
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.3f }, NPC.Center);
                }
                drawTail = true;
            }
            else if (Timer < 92) {
                // 完全静止对视 —— 威压来自静止
                NPC.velocity *= 0.82f;
                NPC.direction = Target.Center.X > NPC.Center.X ? 1 : -1;
                chargeInten = MathHelper.Lerp(chargeInten, 0.35f, 0.02f);
            }
            else if (Timer == 92) {
                // 同帧齐吼
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                if (IsConductor)
                    ACMScreenShakeSystem.Add(10f);
                flashInten = 1f;
                hitChainImpulse = 10;
                if (!Main.dedServ) {
                    for (int i = 0; i < 22; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dust_1>());
                        d.color = ThemeColor;
                        d.color.A = 255;
                        d.scale *= 2.4f;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(3, 9)).RotatedByRandom(8);
                    }
                }
            }
            else if (Timer < 140) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, (Target.Center + new Vector2(HomeSide * 430, -180) - NPC.Center) * 0.02f, 0.06f);
            }
            else {
                SwitchState(StSelect);
            }
        }

        // ================= 连接节拍 Select =================

        protected Vector2 StanceAnchor() {
            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            return IsBrawler
                ? Target.Center + new Vector2(side * 420, -120)
                : Target.Center + new Vector2(side * 640, -320);
        }

        private void RunSelect() {
            HoverTo(StanceAnchor(), 15f, 0.08f);
            FacePlayer();

            // 距离栓绳: 防"飞屏外绕圈"
            if (NPC.Distance(Target.Center) > 2600f)
                NPC.velocity = Vector2.Lerp(NPC.velocity, (Target.Center - NPC.Center).NormalizeVector() * 26f, 0.2f);

            int dur = DidP3 ? 26 : 40;
            if (Timer >= dur) {
                int next = ChooseNext();
                if (next >= 0 && State == StSelect) // ChooseNext 可能已 ForceBoth 切走
                    SwitchState(next);
            }
        }

        // ================= 换岗对穿 =================

        private Vector2 swapTargetPos;

        private void RunSwap() {
            NPC p = Partner;
            if (p == null) { SwitchState(StSelect); return; }

            if (Timer == 1)
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f, Volume = 0.8f }, NPC.Center);

            if (Timer <= 16) {
                // 蓄势闪耀
                NPC.velocity *= 0.88f;
                chargeInten = MathHelper.Lerp(chargeInten, 0.9f, 0.12f);
                if (Timer == 16) {
                    swapTargetPos = p.Center;
                    NPC.velocity = (swapTargetPos - NPC.Center) / 22f;
                    hitChainImpulse = 8;
                }
            }
            else if (Timer <= 44) {
                drawTail = true;
                // 交汇火花 (指挥判定一次)
                if (IsConductor && Sub == 0 && NPC.Distance(p.Center) < 130f) {
                    Sub = 1;
                    ACMScreenShakeSystem.Add(6f);
                    Vector2 mid = (NPC.Center + p.Center) * 0.5f;
                    NiuMaScreenSystem.AddGateMark(mid, 150f, 32);
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f }, mid);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 16; i++) {
                            var d = Dust.NewDustPerfect(mid, DustID.Shadowflame);
                            d.noGravity = true;
                            d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 8)).RotatedByRandom(8);
                        }
                    }
                }
            }
            else if (Timer < 72) {
                NPC.velocity *= 0.82f; // 硬刹落位
                if (Timer == 59 && IsConductor) {
                    float newDuty = Duty == 0 ? 1 : 0;
                    Duty = newDuty;
                    if (p.ModNPC is NiuMaBoss pb2) {
                        pb2.Duty = newDuty;
                        p.netUpdate = true;
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                SwitchState(StSelect);
            }
            FacePlayer();
        }

        // ================= P2 结链换阶段演出 =================

        private void RunP2Transition() {
            NPC p = Partner;
            if (p == null) {
                // 孤军压缩版: 怒吼即走
                if (Timer == 1) {
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ACMScreenShakeSystem.Add(8f);
                    flashInten = 1f;
                }
                NPC.velocity *= 0.9f;
                if (Timer >= 60)
                    SwitchState(StSelect);
                return;
            }

            Vector2 shoulder = Target.Center + new Vector2(HomeSide * 620, -260);
            Vector2 mid = (NPC.Center + p.Center) * 0.5f;

            if (Timer == 1 && IsConductor) {
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                ACMScreenShakeSystem.Add(6f);
            }

            if (Timer <= 35) {
                HoverTo(shoulder, 20f, 0.12f);
            }
            else if (Timer <= 125) {
                HoverTo(shoulder, 7f, 0.07f);
                float link = MathHelper.Clamp((Timer - 35) / 60f, 0f, 1f);
                chargeInten = MathHelper.Lerp(chargeInten, link, 0.1f);

                if (IsConductor) {
                    // 结链中枢法印 + 渐强轰鸣 (t² 曲线)
                    NiuMaScreenSystem.PublishGate(mid, 170f, link * 0.8f, link);
                    ACMScreenShakeSystem.Add(link * link * 3.5f);
                    // 收束魂火: 密度 ∝ sqrt, 72% 后静默
                    if (!Main.dedServ && link < 0.72f && Main.rand.NextFloat() < 0.5f * MathF.Sqrt(link + 0.05f)) {
                        Vector2 pos = mid + Main.rand.NextVector2Circular(300, 200);
                        var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                        d.noGravity = true;
                        d.velocity = (mid - pos).NormalizeVector() * 5f;
                    }
                }

                if (Timer == 125 && IsConductor) {
                    // 链桥崩断: 权柄交接完成
                    ACMScreenShakeSystem.Add(8f);
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.6f, Volume = 1f }, mid);
                    NiuMaScreenSystem.AddGateMark(mid, 220f, 40);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 26; i++) {
                            var d = Dust.NewDustPerfect(mid, ModContent.DustType<Dust_1>());
                            d.color = i % 2 == 0 ? NiuMaHelper.EmberRed : NiuMaHelper.GhostViolet;
                            d.color.A = 255;
                            d.scale *= 2.2f;
                            d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 9)).RotatedByRandom(8);
                        }
                    }
                }
            }
            else if (Timer <= 140) {
                // 双吏同时向玩家逼近一步 (落幅, 无伤)
                if (Timer == 126) {
                    NPC.velocity = (Target.Center - NPC.Center).NormalizeVector() * 15f;
                    flashInten = 0.7f;
                }
                NPC.velocity *= 0.9f;
                drawTail = true;
            }
            else if (Timer >= 176) {
                if (IsConductor) {
                    comboDue = true;
                    attacksInDuty = 0;
                }
                SwitchState(StSelect);
            }
            else {
                NPC.velocity *= 0.9f;
            }
            FacePlayer();
        }

        // ================= P3 阎罗令狂怒演出 =================

        private void RunP3Rage() {
            Vector2 anchor = Target.Center + new Vector2(HomeSide * 170, -330);

            if (Timer <= 40) {
                HoverTo(anchor, 18f, 0.12f);
                chargeInten = MathHelper.Lerp(chargeInten, Timer / 40f, 0.15f);
            }
            else if (Timer < 50) {
                NPC.velocity *= 0.7f; // 屏息
            }
            else if (Timer == 50) {
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                if (IsConductor || Partner == null) {
                    ACMScreenShakeSystem.Add(12f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                }
                flashInten = 1f;
                hitChainImpulse = 12;
                if (!Main.dedServ) {
                    for (int i = 0; i < 30; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dust_1>());
                        d.color = i % 2 == 0 ? ThemeColor : TelegraphColors.Execution;
                        d.color.A = 255;
                        d.scale *= 2.6f;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(4, 12)).RotatedByRandom(8);
                    }
                }
            }
            else if (Timer >= 92) {
                if (IsConductor) {
                    attacksInDuty = 0;
                    comboDue = false;
                }
                SwitchState(StSelect);
            }
            else {
                NPC.velocity *= 0.92f;
            }
            FacePlayer();
        }

        // ================= 引魂 (Reviving) / 复生 (Reborn) =================

        private int FindMyRevivalCircle() {
            foreach (var pr in Main.ActiveProjectiles) {
                if (pr.type == ModContent.ProjectileType<NiuMaRevivalCircle>() && (int)pr.ai[0] == NPC.whoAmI)
                    return pr.whoAmI;
            }
            return -1;
        }

        private void RunReviving() {
            if (Timer == 1)
                SoundEngine.PlaySound(RoarSound, NPC.Center);
            // 引魂者: 悬于尸位反制圈上方施法; 圈灭 (250f) = 引魂完成。
            // 宽限 45f: 多人下圈弹幕到达客户端可能滞后数帧, 避免误判提前退出。
            int circle = FindMyRevivalCircle();
            if (circle < 0 && Timer > 45) {
                NPC.dontTakeDamage = false;
                SwitchState(StSelect);
                return;
            }
            if (circle >= 0) {
                Vector2 anchor = Main.projectile[circle].Center + new Vector2(0, -230);
                HoverTo(anchor, 10f, 0.08f);
                chargeInten = MathHelper.Lerp(chargeInten, 0.9f, 0.06f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 pos = Vector2.Lerp(NPC.Center, Main.projectile[circle].Center, Main.rand.NextFloat());
                    var d = Dust.NewDustPerfect(pos, DustID.GreenTorch);
                    d.noGravity = true;
                    d.velocity = (Main.projectile[circle].Center - NPC.Center).NormalizeVector() * 2f;
                }
            }
            // 超时保底出口
            if (Timer > 300) {
                NPC.dontTakeDamage = false;
                SwitchState(StSelect);
            }
        }

        private void RunReborn() {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            drawAlpha = MathHelper.Clamp((Timer - 60) / 170f, 0f, 1f);

            NPC p = Partner;
            NiuMaBoss pb = PartnerBoss;
            // 引魂被打断 (引魂者阵亡) → 双亡: 直接进入速朽死亡演出
            if (p == null || pb == null || (pb.State != StReviving && Timer < 240)) {
                deathStarted = true;
                NPC.life = 1;
                SwitchState(StDeath);
                Sub = 1; // 速朽变体
                return;
            }

            if (Timer >= 250) {
                drawAlpha = 1f;
                NPC.dontTakeDamage = false;
                flashInten = 1f;
                ACMScreenShakeSystem.Add(5f);
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 20; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dust_1>());
                        d.color = ThemeColor;
                        d.color.A = 255;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 8)).RotatedByRandom(8);
                    }
                }
                SwitchState(StSelect);
            }
        }

        // ================= 死亡演出 =================

        private void RunDeathCinematic() {
            NPC.dontTakeDamage = true;
            bool quick = Sub == 1; // 引魂被断的速朽变体
            float breakAt = quick ? 16 : 60;
            float flashAt = quick ? 52 : 132;
            float dieAt = quick ? 58 : 150;

            // 失控: 缓旋上飘, 魂火外泄 ∝ 进度
            float prog = MathHelper.Clamp(Timer / dieAt, 0f, 1f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -1.6f), 0.05f);
            NPC.rotation += 0.002f + prog * 0.05f * (IsConductor ? 1 : -1);
            chargeInten = prog;

            if (!Main.dedServ && Main.rand.NextFloat() < 0.25f + prog * 0.5f) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(60, 60),
                    Main.rand.NextBool() ? DustID.Smoke : DustID.Shadowflame);
                d.noGravity = Main.rand.NextBool();
                d.velocity = new Vector2(0, -NiuMaHelper.Rand_Float(1, 4));
                d.scale = 1.4f;
            }

            if (Timer == breakAt) {
                // 锁链崩断
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.7f, Volume = 1f }, NPC.Center);
                ACMScreenShakeSystem.Add(8f);
                hitChainImpulse = 14;
                if (!Main.dedServ) {
                    for (int i = 0; i < 20; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40, 40), DustID.Iron);
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 7)).RotatedByRandom(8);
                    }
                }
            }

            if (Timer == flashAt) {
                // 本战唯一冲击帧: 魂躯崩解
                NiuMaScreenSystem.FlashWhite(4, 0.85f);
                ACMScreenShakeSystem.Add(14f);
                SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1f }, NPC.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 44; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dust_1>());
                        d.color = i % 2 == 0 ? ThemeColor : ThemeCore;
                        d.color.A = 255;
                        d.scale *= 2.8f;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(3, 14)).RotatedByRandom(8);
                    }
                }
            }

            if (Timer >= flashAt)
                drawAlpha = MathHelper.Clamp(1f - (Timer - flashAt) / 10f, 0f, 1f);

            if (Timer >= dieAt) {
                deathFinished = true;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.StrikeInstantKill();
            }
        }

        public override bool CheckDead() {
            if (deathFinished)
                return true;

            NPC p = Partner;
            NiuMaBoss pb = PartnerBoss;
            bool canRevive = !HasRespawn && p != null && pb != null &&
                p.life > p.lifeMax * 0.3f &&
                pb.State != StReviving && pb.State != StReborn && pb.State != StDeath && !pb.deathStarted;

            if (canRevive) {
                // —— 引魂复生: 同伴化引魂者, 尸位生成反制圈 ——
                HasRespawn = true;
                NPC.life = (int)(NPC.lifeMax * 0.5f);
                NPC.dontTakeDamage = true;
                NPC.velocity = Vector2.Zero;
                SwitchState(StReborn);

                p.velocity = Vector2.Zero;
                p.dontTakeDamage = true; // 圈内站人时由反制圈逐帧解除
                pb.SwitchState(StReviving);
                p.netUpdate = true;

                NiuMaHelper.ClearHostileProjectiles(); // 复生节拍清弹
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                ACMScreenShakeSystem.Add(7f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaRevivalCircle>(), 0, 0f, Main.myPlayer, p.whoAmI);
                return false;
            }

            if (!deathStarted) {
                // —— 终幕死亡演出 ——
                deathStarted = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                SwitchState(StDeath);
                NiuMaHelper.ClearHostileProjectiles();
                return false;
            }
            NPC.life = Math.Max(NPC.life, 1); // 演出期间任何漏网致死打击不得清零生命
            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            flashInten = Math.Max(flashInten, 0.45f);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, hit.HitDirection * 2f, -1f);
                d.noGravity = true;
            }
        }

        // ================= 运动/朝向助手 =================

        protected void HoverTo(Vector2 anchor, float maxSpeed, float lerp) {
            Vector2 diff = anchor - NPC.Center;
            float dist = diff.Length();
            Vector2 want = diff.SafeNormalize(Vector2.Zero) * Math.Min(maxSpeed, dist * 0.05f + 1.5f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, want, lerp);
        }

        protected void FacePlayer() {
            NPC.direction = Target.Center.X > NPC.Center.X ? 1 : -1;
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.05f, -0.45f, 0.45f), 0.08f);
        }

        // ================= 视觉更新与绘制 =================

        private void UpdateVisuals() {
            flashInten *= 0.88f;
            if (State != StP2 && State != StReviving && State != StDeath)
                chargeInten *= 0.93f;

            if (Main.dedServ)
                return;

            // 垂坠锁链 Verlet (重量感: 冲刺/急停时链条甩尾)
            hangChains ??= [InitChain(-1), InitChain(1)];
            for (int c = 0; c < 2; c++) {
                Vector2[] chain = hangChains[c];
                chain[0] = NPC.Center + new Vector2((c == 0 ? -1 : 1) * 30f * NPC.scale * 0.5f, 30f * NPC.scale * 0.5f).RotatedBy(NPC.rotation);
                if (hitChainImpulse > 0)
                    chain[^1] += Main.rand.NextVector2Circular(6f, 6f);
                ACMUtils.VerletStep(chain, 0.55f, 14f, 3);
            }
            if (hitChainImpulse > 0)
                hitChainImpulse--;
        }

        private Vector2[] InitChain(int side) {
            var arr = new Vector2[5];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = NPC.Center + new Vector2(side * 30f, 30f + i * 14f);
            return arr;
        }

        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            var tex = TextureAssets.Npc[Type].Value;
            var rec = NPC.frame;
            var spe = NPC.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            DrawStateTelegraphs();

            // 垂坠锁链 (本体之下)
            if (hangChains != null && drawAlpha > 0.15f) {
                foreach (var chain in hangChains)
                    NiuMaHelper.DrawHangChain(sb, chain, scrPos, Color.Lerp(Color.DarkGray, ThemeColor, 0.3f), 0.85f * drawAlpha);
            }

            // 残影 (速度门控: 只在冲刺态出现)
            if (drawTail) {
                var tailCol = ThemeColor * 0.5f;
                tailCol.A = 0;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    float fade = 1f - i / (float)NPC.oldPos.Length;
                    sb.Draw(tex, NPC.oldPos[i] + rec.Size() * .5f * NPC.scale - scrPos, rec, tailCol * drawAlpha * fade,
                        NPC.rotation, rec.Size() * .5f, NPC.scale * (1f - i / (float)NPC.oldPos.Length * .3f), spe, 0);
                }
            }

            // 本体 (SoulFlame 着色: 魂焰描边 + 闪白 + 蓄力增辉)
            NiuMaHelper.DrawBodySoulFlame(sb, tex, NPC.Center - scrPos, rec, col, NPC.rotation, NPC.scale, spe,
                ThemeColor, ThemeCore, flashInten, chargeInten, drawAlpha);

            // 外发光
            var glowCol = ThemeColor;
            glowCol.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glowCol * .35f * drawAlpha, NPC.rotation, rec.Size() * .5f, NPC.scale * 1.07f, spe, 0);
            return false;
        }

        /// <summary>状态相关预警绘制 (瞄线/车道/链桥), 由子类补充。</summary>
        protected virtual void DrawStateTelegraphs() { }

        public override void OnKill() {
            ACMScreenShakeSystem.Add(10f);
        }

        public override bool PreAI() {
            drawTail = false;
            return base.PreAI();
        }
    }

    /// <summary>
    /// 牛头 —— 力之吏 (指挥)。缠斗岗: 裁决三连撞 / 拘魂锁链扇; 炮台岗: 燃角链锤 / 怒目凝视。
    /// 负责推动换岗、合体技 (黄泉车道/勾魂锁命) 与 P2/P3 全局节拍。
    /// </summary>
    public class NiuTou : NiuMaBoss
    {
        private static readonly SoundStyle roarSound = SoundID.Roar with { PitchVariance = .2f };
        private static readonly SoundStyle chargeWindupSound = SoundID.ForceRoar with { Volume = .8f, PitchVariance = .3f };
        private static readonly SoundStyle chainLaunchSound = SoundID.Item20 with { Volume = .7f };
        private static readonly SoundStyle eyeBlastSound = SoundID.Item74 with { Volume = 1f };
        private static readonly SoundStyle comboDashSound = SoundID.DD2_EtherianPortalDryadTouch with { Volume = .9f };

        public override int PartnerType => ModContent.NPCType<MaMian>();
        public override bool IsConductor => true;
        public override bool IsBrawler => Duty == 0;
        protected override Color ThemeColor => NiuMaHelper.EmberRed;
        protected override Color ThemeCore => NiuMaHelper.EmberCore;
        protected override SoundStyle RoarSound => roarSound;

        // 攻击状态
        private const int AtkTripleRam = 10, AtkChainFan = 11, AtkChainMace = 12, AtkGaze = 13;

        // —— 冲撞 —— (瞄线/爆发向量)
        private Vector2 aimDir = Vector2.UnitX;
        private bool telegraphLine;
        private bool telegraphLethal;

        // —— 合体技: 黄泉车道 (马面读取) ——
        public int SynergyPhase;      // 0=铺垫 1=预告 2=冲锋 3=收招
        public float LaneY;
        public Vector2 LaneStart, LaneEnd;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            NiuMaLoot.AddBossLoot(npcLoot, ModContent.NPCType<MaMian>());
        }

        protected override int ChooseNext() {
            NPC p = Partner;
            if (p == null || PartnerBoss == null || !PartnerBoss.InNormalFlow) {
                // 孤军: 四招全轮换, 无节拍
                int[] soloPool = [AtkTripleRam, AtkChainFan, AtkChainMace, AtkGaze];
                return soloPool[attackAlt++ % soloPool.Length];
            }

            // 节拍裁决: 每 2 招 → 合体 (若解锁且轮到) 或换岗
            if (attacksInDuty >= 2) {
                attacksInDuty = 0;
                if (DidP2 && comboDue) {
                    comboDue = false;
                    int combo = comboAlt++ % 2 == 0 ? StLane : StLink;
                    NiuMaHelper.ClearHostileProjectiles(); // 合体开场清残留 (无旧账干扰读屏)
                    ForceBoth(combo);
                    return -1;
                }
                if (DidP2)
                    comboDue = true;
                ForceBoth(StSwap);
                return -1;
            }

            int[] pool = IsBrawler ? [AtkTripleRam, AtkChainFan] : [AtkChainMace, AtkGaze];
            return pool[attackAlt++ % 2];
        }

        public override bool PreAI() {
            telegraphLine = false; // 每帧复位: 被节拍强制打断时不残留预警线
            return base.PreAI();
        }

        protected override void RunAttack(int state) {
            switch (state) {
                case AtkTripleRam: RunTripleRam(); break;
                case AtkChainFan: RunChainFan(); break;
                case AtkChainMace: RunChainMace(); break;
                case AtkGaze: RunGaze(); break;
                case StLane: RunLaneSweep(); break;
                case StLink: RunSoulLink(); break;
                default: SwitchState(StSelect); break;
            }
        }

        // ================= 裁决三连撞 (缠斗) =================
        // 波形: 悬停瞄准 34f → pow8 反向抽身 10f → 瞬发 40px/f 直线 11f (复利加速) → ×0.68 硬刹。
        // 公平: 预警音固定提前 36f; 红线锁定后 10f 才起跳; 伤害窗口仅速度 >20。

        private void RunTripleRam() {
            int cycles = DidP3 ? 4 : 3;
            const int cycleLen = 78;
            int cycle = (int)Timer / cycleLen;
            int t = (int)Timer % cycleLen;

            if (cycle >= cycles) {
                EndAttack();
                return;
            }

            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            if (t < 34) {
                Vector2 anchor = Target.Center + new Vector2(side * 520, -50);
                HoverTo(anchor, 17f, 0.1f);
                if (t > 24)
                    NPC.velocity *= 0.9f; // 慢启动阀门: 蓄势期减速
                if (t == 8)
                    SoundEngine.PlaySound(chargeWindupSound, NPC.Center); // 固定 36f 预警节拍
                if (t >= 6) {
                    telegraphLine = true;
                    telegraphLethal = false;
                    aimDir = (Target.Center + Target.velocity * 10f - NPC.Center).NormalizeVector(Vector2.UnitX);
                }
                FacePlayer();
            }
            else if (t < 44) {
                // 反向抽身: pow8 迟滞 → 最后几帧猛然后吸
                telegraphLine = true;
                telegraphLethal = true;
                float k = (t - 34) / 10f;
                NPC.velocity = -aimDir * MathF.Pow(k, 8f) * 20f;
                chargeInten = MathHelper.Lerp(chargeInten, 1f, 0.2f);
                NPC.direction = aimDir.X > 0 ? 1 : -1;
            }
            else if (t < 55) {
                if (t == 44) {
                    NPC.velocity = aimDir * (DidP3 ? 44f : 40f);
                    SoundEngine.PlaySound(comboDashSound, NPC.Center);
                    ACMScreenShakeSystem.Add(5f);
                    flashInten = 0.6f;
                    hitChainImpulse = 8;
                }
                drawTail = true;
                NPC.velocity *= 1.03f;
                NPC.damage = 95;
                // 沿途血焰刻痕 (服务器)
                if (t % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, aimDir.RotatedBy(NiuMaHelper.Rand_Float(-0.4f, 0.4f)),
                        ModContent.ProjectileType<Proj_756_Adjust>(), 40, 1f, NPC.target);
                    p.ai[1] = NiuMaHelper.Rand_Float(0.5f, 1f);
                    p.netUpdate = true;
                }
            }
            else if (t < 71) {
                NPC.velocity *= 0.68f; // 硬刹 = 撞停的重量
                if (NPC.velocity.Length() > 20f)
                    NPC.damage = 95;
            }
            else {
                NPC.velocity *= 0.9f;
                FacePlayer();
            }
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * .04f, -.5f, .5f), .1f);
        }

        // ================= 拘魂锁链扇 (缠斗) =================

        private void RunChainFan() {
            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            if (Timer < 40) {
                HoverTo(Target.Center + new Vector2(side * 400, -280), 15f, 0.09f);
                if (Timer == 10)
                    SoundEngine.PlaySound(chargeWindupSound, NPC.Center);
                // 收束聚气 (sqrt 密度, 72% 静默)
                float c = (float)(Timer / 40f);
                if (!Main.dedServ && c < 0.72f && Main.rand.NextFloat() < 0.55f * MathF.Sqrt(c + 0.05f)) {
                    Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(180, 180);
                    var d = Dust.NewDustPerfect(pos, DustID.CrimsonTorch);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - pos).NormalizeVector() * 5f;
                }
                FacePlayer();
            }
            else if (Timer < 70) {
                NPC.velocity *= 0.85f;
                chargeInten = MathHelper.Lerp(chargeInten, 0.9f, 0.12f);
                telegraphLine = true;
                if (Timer < 60) {
                    telegraphLethal = false;
                    aimDir = (Target.Center - NPC.Center).NormalizeVector(Vector2.UnitX);
                }
                else {
                    telegraphLethal = true; // 锁定后 10f 红线
                }
                if (Timer == 69) {
                    int count = DidP3 ? 5 : (DidP2 ? 4 : 3);
                    SoundEngine.PlaySound(chainLaunchSound, NPC.Center);
                    ACMScreenShakeSystem.Add(6f);
                    NPC.velocity = -aimDir * 7f; // 后坐
                    hitChainImpulse = 8;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < count; i++) {
                            float off = (i - (count - 1) * 0.5f) * 0.42f;
                            var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, aimDir.RotatedBy(off) * 26f,
                                ModContent.ProjectileType<ChainProj>(), 45, 1f, NPC.target);
                            p.ai[2] = NPC.whoAmI;
                            p.netUpdate = true;
                        }
                    }
                }
            }
            else if (Timer >= 116) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.93f;
                FacePlayer();
            }
        }

        // ================= 燃角链锤 (炮台) =================

        private void RunChainMace() {
            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            if (Timer < 46) {
                HoverTo(Target.Center + new Vector2(side * 560, -420), 15f, 0.09f);
                chargeInten = MathHelper.Lerp(chargeInten, (float)(Timer / 46f), 0.1f);
                if (Timer == 20)
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.8f, Pitch = -0.4f }, NPC.Center);
                FacePlayer();
            }
            else if (Timer == 46) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = -0.6f }, NPC.Center);
                Vector2 vel = new(MathHelper.Clamp((Target.Center.X + Target.velocity.X * 24f - NPC.Center.X) / 38f, -16f, 16f), -6.5f);
                NPC.velocity = -vel * 0.6f; // 掷锤后坐
                hitChainImpulse = 10;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center + new Vector2(0, 20), vel,
                        ModContent.ProjectileType<NiuMaChainMace>(), 50, 1f, NPC.target);
                    p.ai[2] = NPC.whoAmI;
                    p.netUpdate = true;
                }
            }
            else if (Timer >= 100) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.94f;
                FacePlayer();
            }
        }

        // ================= 怒目凝视 (炮台) =================

        private void RunGaze() {
            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            Vector2 eyePos = NPC.Center + new Vector2(NPC.direction * 30f, -20f);

            if (Timer < 40) {
                HoverTo(Target.Center + new Vector2(side * 620, -300), 13f, 0.08f);
                chargeInten = MathHelper.Lerp(chargeInten, (float)(Timer / 40f), 0.12f);
                if (Timer == 20)
                    SoundEngine.PlaySound(chargeWindupSound, NPC.Center);
                float c = (float)(Timer / 40f);
                if (!Main.dedServ && c < 0.72f && Main.rand.NextFloat() < 0.6f * MathF.Sqrt(c + 0.05f)) {
                    Vector2 pos = eyePos + Main.rand.NextVector2Circular(140, 140);
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.velocity = (eyePos - pos).NormalizeVector() * 4.5f;
                }
                FacePlayer();
            }
            else if (Timer == 40) {
                SoundEngine.PlaySound(eyeBlastSound, NPC.Center);
                flashInten = 0.7f;
                ACMScreenShakeSystem.Add(4f);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, (Target.Center - eyePos).NormalizeVector() * 4f,
                        ModContent.ProjectileType<EyeProj>(), 42, 1f, NPC.target);
            }
            else if (Timer < 96) {
                NPC.velocity *= 0.92f;
                // 三波扇形魂火 (44/62/80)
                if ((Timer == 44 || Timer == 62 || Timer == 80) && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 aim = (Target.Center - NPC.Center).NormalizeVector(Vector2.UnitX);
                    int n = DidP3 ? 4 : 3;
                    for (int i = 0; i < n; i++) {
                        float off = (i - (n - 1) * 0.5f) * 0.3f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy(off) * 7f,
                            ModContent.ProjectileType<DarkGreenProj>(), 38, 1f, NPC.target);
                    }
                }
                if (Timer == 44 || Timer == 62 || Timer == 80) {
                    NPC.velocity -= (Target.Center - NPC.Center).NormalizeVector() * 3f; // 逐波后坐
                    SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.6f }, NPC.Center);
                }
                FacePlayer();
            }
            else {
                EndAttack();
            }
        }

        // ================= 合体技 A: 黄泉车道 =================
        // 牛头压可读水平车道 (铺垫→预告→冲锋→收招 ×2), 马面在空档填慢速魂火帘。

        private void RunLaneSweep() {
            const int cycleLen = 190;
            const int cycles = 2;
            int cycle = (int)Timer / cycleLen;
            int local = (int)Timer % cycleLen;

            if (cycle >= cycles) {
                SynergyPhase = 0;
                if (IsConductor) {
                    attacksInDuty = 0;
                    ForceBoth(StSelect);
                }
                return;
            }

            bool fromLeft = cycle % 2 == 0;
            float laneHalf = 1400f;

            if (local < 45) {
                SynergyPhase = 0;
                // Timer 从 1 起跳, 首循环用 Timer==1 兜住车道高度锁定
                if (local == 0 || Timer == 1) {
                    LaneY = Target.Center.Y;
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                }
                LaneStart = new Vector2(Target.Center.X - laneHalf, LaneY);
                LaneEnd = new Vector2(Target.Center.X + laneHalf, LaneY);
                Vector2 startPos = fromLeft ? LaneStart : LaneEnd;
                startPos.X += fromLeft ? -180f : 180f;
                HoverTo(startPos, 26f, 0.12f);
                NPC.direction = fromLeft ? 1 : -1;
            }
            else if (local < 105) {
                SynergyPhase = 1;
                NPC.velocity *= 0.85f;
                // 末 10f 反向抽身
                if (local >= 95) {
                    Vector2 dir = new(fromLeft ? 1 : -1, 0);
                    float k = (local - 95) / 10f;
                    NPC.velocity = -dir * MathF.Pow(k, 8f) * 16f;
                }
                if (local == 69)
                    SoundEngine.PlaySound(chargeWindupSound, NPC.Center); // 固定 36f 预警
                chargeInten = MathHelper.Lerp(chargeInten, 0.9f, 0.1f);
            }
            else if (local < 135) {
                SynergyPhase = 2;
                NPC.damage = 95;
                drawTail = true;
                if (local == 105) {
                    NPC.velocity = new Vector2(fromLeft ? 40f : -40f, 0);
                    SoundEngine.PlaySound(comboDashSound, NPC.Center);
                    ACMScreenShakeSystem.Add(7f);
                    flashInten = 0.6f;
                    hitChainImpulse = 8;
                }
                if (Math.Abs(NPC.velocity.X) < 46f)
                    NPC.velocity.X *= 1.02f;
                NPC.Center = new Vector2(NPC.Center.X, MathHelper.Lerp(NPC.Center.Y, LaneY, 0.3f));
            }
            else {
                SynergyPhase = 3;
                NPC.velocity *= 0.88f;
            }
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * .06f, -.55f, .55f), .1f);
        }

        // ================= 合体技 B: 勾魂锁命 =================
        // 二吏对峙两侧, 结链 → 链绷直致命, 绕中点匀速旋转; 中点 165px 翠玉命门可穿。

        private Vector2 linkCenter;

        private void RunSoulLink() {
            NPC p = Partner;
            if (p == null) {
                SwitchState(StSelect);
                return;
            }

            const float startRadius = 700f;
            const float endRadius = 560f;

            if (Timer < 50) {
                if (Timer == 1)
                    SoundEngine.PlaySound(chargeWindupSound, NPC.Center);
                linkCenter = Target.Center;
                Vector2 anchor = linkCenter + new Vector2(-startRadius, 0);
                HoverTo(anchor, 26f, 0.14f);
                chargeInten = MathHelper.Lerp(chargeInten, 0.7f, 0.08f);
            }
            else if (Timer == 50) {
                SoundEngine.PlaySound(chainLaunchSound, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    var link = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), linkCenter, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaLinkChain>(), 55, 2f, NPC.target);
                    link.ai[0] = NPC.whoAmI;
                    link.ai[1] = p.whoAmI;
                    link.netUpdate = true;
                }
            }
            else if (Timer < 382) {
                if (linkCenter == Vector2.Zero)
                    linkCenter = Target.Center; // 中途入场兜底
                // 旋转编队: 位置直书 (velocity=位移增量, 保尾迹)
                float phi = MathF.PI + MathHelper.Clamp(((float)Timer - 110f) * 0.0165f, 0f, 999f);
                float radius = MathHelper.Lerp(startRadius, endRadius, MathHelper.Clamp(((float)Timer - 110f) / 80f, 0f, 1f));
                if (Timer < 110)
                    phi = MathF.PI;
                Vector2 want = linkCenter + phi.ToRotationVector2() * radius;
                NPC.velocity = (want - NPC.Center) * 0.2f;
                if (Timer == 110) {
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.5f, Volume = 1f }, linkCenter);
                    ACMScreenShakeSystem.Add(6f);
                }
                chargeInten = MathHelper.Lerp(chargeInten, 0.55f, 0.05f);
            }
            else if (Timer < 410) {
                if (Timer == 382) {
                    NPC.velocity = (NPC.Center - linkCenter).NormalizeVector() * 9f; // 断链反冲
                    ACMScreenShakeSystem.Add(6f);
                }
                NPC.velocity *= 0.9f;
            }
            else {
                if (IsConductor) {
                    attacksInDuty = 0;
                    ForceBoth(StSelect);
                }
            }
            FacePlayer();
        }

        // ================= 预警绘制 =================

        protected override void DrawStateTelegraphs() {
            if (Main.dedServ)
                return;

            // 冲撞/锁链瞄准线 (锁链扇画整扇预警)
            if (telegraphLine) {
                Color core = telegraphLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                float w = telegraphLethal ? 16f : 10f;
                int fan = State == AtkChainFan ? (DidP3 ? 5 : (DidP2 ? 4 : 3)) : 1;
                for (int i = 0; i < fan; i++) {
                    float off = (i - (fan - 1) * 0.5f) * 0.42f;
                    Vector2 dir = aimDir.RotatedBy(off);
                    ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1600f, fan > 1 ? w * 0.7f : w, core,
                        new Color(80, 20, 30), telegraphLethal ? 0.9f : 0.5f, 1.4f, 2f, 2.2f);
                }
            }

            // 黄泉车道
            if (State == StLane && (SynergyPhase == 1 || SynergyPhase == 2)) {
                bool lethal = SynergyPhase == 2 || Timer % 190 >= 95;
                Color core = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                float w = lethal ? 38f : 18f;
                ACMShaders.DrawBeam(LaneStart, LaneEnd, w, core, new Color(90, 20, 30), lethal ? 1f : 0.6f);
            }

            // P2 结链光桥 + 权柄魂灯 (指挥绘制)
            NPC p = Partner;
            if (State == StP2 && p != null && Timer > 35 && Timer < 126) {
                float link = MathHelper.Clamp(((float)Timer - 35f) / 60f, 0f, 1f);
                ACMShaders.DrawBeam(NPC.Center, p.Center, 10f + 6f * link,
                    Color.Lerp(NiuMaHelper.EmberRed, NiuMaHelper.GhostViolet, 0.5f), new Color(70, 30, 80), link * 0.9f, 1.8f);
                if (Timer > 95) {
                    float slide = MathHelper.Clamp(((float)Timer - 95f) / 30f, 0f, 1f);
                    Vector2 lantern = Vector2.Lerp(NPC.Center, p.Center, slide);
                    var glow = ACMAsset.SoftGlow;
                    var lc = NiuMaHelper.EmberCore with { A = 0 };
                    Main.spriteBatch.Draw(glow, lantern - Main.screenPosition, null, lc, 0, glow.Size() * 0.5f, 1.6f, default, 0);
                    Main.spriteBatch.Draw(glow, lantern - Main.screenPosition, null, Color.White with { A = 0 } * 0.6f, 0, glow.Size() * 0.5f, 0.7f, default, 0);
                }
            }
        }

        public override void PostDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            if (Main.dedServ)
                return;
            // 凝视蓄力 / 死亡收光: 径向泛光 (走全屏名额契约)
            if (State == AtkGaze && Timer < 42 && chargeInten > 0.15f) {
                Vector2 eyePos = NPC.Center + new Vector2(NPC.direction * 30f, -20f);
                ACMShaders.DrawRadialBloomAt(eyePos, 0.06f + chargeInten * 0.08f, chargeInten * 0.7f, NiuMaHelper.EmberRed, 8f, 2.6f);
            }
            else if (State == StDeath && Timer > 100) {
                float k = MathHelper.Clamp(((float)Timer - 100f) / 32f, 0f, 1f);
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.2f * (1f - k * 0.6f), 0.5f + k * 0.5f, NiuMaHelper.EmberCore, 6f, 2.2f);
            }
        }
    }

    /// <summary>
    /// 马面 —— 魂之吏。炮台岗: 魂火三连 / 忘川引潮; 缠斗岗: 无常三闪 / 拘魂令环。
    /// 读取牛头节拍镜像行动; 控场期周期性施放「勾魂」(打牛头可破)。
    /// </summary>
    public class MaMian : NiuMaBoss
    {
        private static readonly SoundStyle volleySound = SoundID.Item73 with { Volume = .8f };
        private static readonly SoundStyle soulPullSound = SoundID.DD2_MonkStaffGroundImpact with { Volume = .9f };
        private static readonly SoundStyle roarSound = SoundID.Roar with { Pitch = 0.35f, Volume = 0.9f };

        public override int PartnerType => ModContent.NPCType<NiuTou>();
        public override bool IsConductor => false;
        public override bool IsBrawler => Duty == 1;
        protected override Color ThemeColor => NiuMaHelper.GhostViolet;
        protected override Color ThemeCore => NiuMaHelper.GhostCore;
        protected override SoundStyle RoarSound => roarSound;

        private const int AtkTripleVolley = 20, AtkTide = 21, AtkTripleFlash = 22, AtkWritRing = 23;

        // —— 无常三闪 ——
        private Vector2 flashAim = Vector2.UnitX;
        private bool flashLineOn;
        private bool flashLineLethal;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            NiuMaLoot.AddBossLoot(npcLoot, ModContent.NPCType<NiuTou>());
        }

        protected override int ChooseNext() {
            int pick;
            if (Partner == null) {
                // 孤军: 四招全轮换
                int[] soloPool = [AtkTripleVolley, AtkTripleFlash, AtkTide, AtkWritRing];
                pick = soloPool[attackAlt++ % soloPool.Length];
            }
            else {
                // 马面不裁决节拍 (换岗/合体由牛头强制); 只选自己岗位内的招
                int[] pool = IsBrawler ? [AtkTripleFlash, AtkWritRing] : [AtkTripleVolley, AtkTide];
                pick = pool[attackAlt++ % 2];
            }
            // 场上已有忘川水域 → 换魂火三连 (避免叠域)
            if (pick == AtkTide && AnyTideField())
                pick = AtkTripleVolley;
            return pick;
        }

        private static bool AnyTideField() {
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == ModContent.ProjectileType<NiuMaTideField>())
                    return true;
            }
            return false;
        }

        public override void AI() {
            base.AI();

            // 勾魂: 炮台岗常规流程中周期标记玩家 (打牛头可破) —— 交叉反制课题
            // 牛头无敌期 (演出/引魂) 不施放: 反制对象必须可被攻击 (公平阀门)
            if (InNormalFlow && !IsBrawler && Partner != null && !Partner.dontTakeDamage && !Target.dead) {
                NPC.localAI[2] -= 1f;
                if (NPC.localAI[2] <= 0f) {
                    NPC.localAI[2] = DidP3 ? 560f : 780f;
                    Target?.GetModPlayer<NiuMaPlayer>()?.ApplySoulHook(NPC.whoAmI);
                    SoundEngine.PlaySound(soulPullSound, NPC.Center);
                }
            }
        }

        public override bool PreAI() {
            flashLineOn = false; // 每帧复位: 被节拍强制打断时不残留锁线
            return base.PreAI();
        }

        protected override void RunAttack(int state) {
            switch (state) {
                case AtkTripleVolley: RunTripleVolley(); break;
                case AtkTide: RunTide(); break;
                case AtkTripleFlash: RunTripleFlash(); break;
                case AtkWritRing: RunWritRing(); break;
                case StLane: RunLaneCurtain(); break;
                case StLink: RunSoulLinkFollow(); break;
                default: SwitchState(StSelect); break;
            }
        }

        // ================= 魂火三连 (炮台) =================

        private void RunTripleVolley() {
            int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
            int waves = DidP3 ? 4 : 3;
            int lastWave = 26 + (waves - 1) * 24;

            Vector2 anchor = Target.Center + new Vector2(side * 600, -280 + 40f * MathF.Sin((float)Timer * 0.05f));
            HoverTo(anchor, 12f, 0.08f);
            FacePlayer();

            if (Timer < 26) {
                chargeInten = MathHelper.Lerp(chargeInten, (float)(Timer / 26f) * 0.7f, 0.15f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 muzzle = NPC.Center + new Vector2(NPC.direction * 40, 0);
                    Vector2 pos = muzzle + Main.rand.NextVector2Circular(90, 90);
                    var d = Dust.NewDustPerfect(pos, DustID.CorruptTorch);
                    d.noGravity = true;
                    d.velocity = (muzzle - pos).NormalizeVector() * 3.5f;
                }
            }

            if (Timer >= 26 && Timer <= lastWave && (Timer - 26) % 24 == 0) {
                Vector2 aim = (Target.Center - NPC.Center).NormalizeVector(Vector2.UnitX);
                SoundEngine.PlaySound(volleySound, NPC.Center);
                NPC.velocity -= aim * 6f; // 逐波后坐
                int n = DidP3 ? 6 : 5;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < n; i++) {
                        float off = (i - (n - 1) * 0.5f) * 0.25f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy(off) * 5.5f,
                            ModContent.ProjectileType<DarkGreenProj>(), 38, 1f, NPC.target);
                    }
                    // 狂怒: 末波附赠爆裂魂核 (可读大威胁)
                    if (DidP3 && Timer == lastWave)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim * 4f,
                            ModContent.ProjectileType<DarkGreenBoomProj>(), 40, 1f, NPC.target);
                }
            }

            if (Timer >= lastWave + 30)
                EndAttack();
        }

        // ================= 忘川引潮 (炮台) =================

        private void RunTide() {
            if (Timer < 34) {
                HoverTo(Target.Center + new Vector2(0, -380), 14f, 0.09f);
                chargeInten = MathHelper.Lerp(chargeInten, (float)(Timer / 34f), 0.12f);
                FacePlayer();
            }
            else if (Timer == 34) {
                SoundEngine.PlaySound(soulPullSound, NPC.Center);
                NPC.velocity = new Vector2(0, -6f); // 展域后坐上浮
                flashInten = 0.6f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), Target.Center + new Vector2(0, -40), Vector2.Zero,
                        ModContent.ProjectileType<NiuMaTideField>(), 36, 1f, NPC.target);
                    p.ai[1] = DidP3 ? 1f : 0f;
                    p.netUpdate = true;
                }
            }
            else if (Timer >= 60) {
                EndAttack();
            }
            else {
                NPC.velocity *= 0.93f;
            }
        }

        // ================= 无常三闪 (缠斗) =================
        // 段结构: 22f 锁线 (幽紫→16f 转红) → 6f 46px/f 瞬步对穿 → 14f 硬刹重摆。伤害窗口=爆发帧。

        private void RunTripleFlash() {
            int segments = DidP3 ? 4 : 3;
            const int segLen = 48;
            int seg = (int)Timer / segLen;
            int t = (int)Timer % segLen;

            if (seg >= segments) {
                EndAttack();
                return;
            }

            if (t < 28) {
                // 锁线: 缓慢漂移到玩家侧向, 慢启动阀门; 红线定格 10f 后才闪步
                int side = NPC.Center.X >= Target.Center.X ? 1 : -1;
                HoverTo(Target.Center + new Vector2(side * 380, -60), 13f, 0.1f);
                if (t > 16)
                    NPC.velocity *= 0.88f;
                flashLineOn = t > 4;
                if (t <= 18) {
                    flashLineLethal = false;
                    flashAim = (Target.Center - NPC.Center).NormalizeVector(Vector2.UnitX);
                }
                else {
                    flashLineLethal = true;
                }
                if (t == 6)
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.1f }, NPC.Center);
                FacePlayer();
            }
            else if (t < 34) {
                if (t == 28) {
                    NPC.velocity = flashAim * 46f;
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 1f }, NPC.Center);
                    ACMScreenShakeSystem.Add(4f);
                    flashInten = 0.55f;
                    hitChainImpulse = 8;
                }
                drawTail = true;
                NPC.damage = 80;
            }
            else {
                NPC.velocity *= 0.7f; // 硬刹
                if (NPC.velocity.Length() > 24f)
                    NPC.damage = 80;
                NPC.rotation = NPC.rotation.AngleLerp(0, 0.15f);
            }
        }

        // ================= 拘魂令环 (缠斗) =================

        private float orbitAngle;

        private void RunWritRing() {
            int count = DidP3 ? 7 : 5;

            // 绕玩家弧线巡游
            if (Timer == 1)
                orbitAngle = (NPC.Center - Target.Center).ToRotation();
            orbitAngle += 0.028f;
            Vector2 want = Target.Center + orbitAngle.ToRotationVector2() * 380f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.15f, 0.2f);
            FacePlayer();

            // 沿半弧布符 (每 8f 一张, 显形即预告)
            if (Timer >= 8 && Timer <= 8 * count && Timer % 8 == 0) {
                int i = (int)Timer / 8 - 1;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float arc = orbitAngle + MathHelper.Pi + (i - (count - 1) * 0.5f) * 0.38f;
                    Vector2 pos = Target.Center + arc.ToRotationVector2() * 380f;
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaWritProj>(), 42, 1f, NPC.target);
                    p.ai[1] = 78 - i * 8 + i * 6; // 布符错帧补偿 → 齐读秒, 微错开开火
                    p.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = 0.4f }, NPC.Center);
            }

            if (Timer >= 8 * count + 92)
                EndAttack();
        }

        // ================= 合体技镜像: 黄泉车道帘幕 =================

        private void RunLaneCurtain() {
            NPC niu = Partner;
            if (niu == null || niu.ModNPC is not NiuTou nt || niu.ai[0] != StLane) {
                SwitchState(StSelect);
                return;
            }

            // 悬于车道上方外侧, 不抢冲锋读屏
            HoverTo(Target.Center + new Vector2(0, -560), 11f, 0.07f);
            FacePlayer();
            drawTail = true;

            int local = (int)niu.ai[1] % 190;
            // 铺垫/收招空档各布一次双排慢帘 (上排下坠 / 下排上升, 交错留缝)
            if ((local == 20 || local == 150) && Main.netMode != NetmodeID.MultiplayerClient) {
                float laneY = nt.LaneY;
                bool second = local == 150;
                for (int i = 0; i < 6; i++) {
                    float x = Target.Center.X - 570f + i * 190f + (second ? 95f : 0f);
                    var top = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), new Vector2(x, laneY - 430f), new Vector2(0, 2.2f),
                        ModContent.ProjectileType<SoulOrbProj>(), 34, 1f, NPC.target);
                    top.ai[0] = 2.4f;
                    var bottom = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), new Vector2(x + 95f, laneY + 430f), new Vector2(0, -2.2f),
                        ModContent.ProjectileType<SoulOrbProj>(), 34, 1f, NPC.target);
                    bottom.ai[0] = 2.4f;
                }
                SoundEngine.PlaySound(volleySound, NPC.Center);
            }
            if (local == 20 || local == 150)
                chargeInten = 0.8f;
        }

        // ================= 合体技镜像: 勾魂锁命对位 =================

        private Vector2 linkCenter;

        private void RunSoulLinkFollow() {
            NPC niu = Partner;
            if (niu == null || niu.ai[0] != StLink) {
                SwitchState(StSelect);
                return;
            }

            const float startRadius = 700f;
            const float endRadius = 560f;

            if (Timer < 50) {
                linkCenter = Target.Center;
                HoverTo(linkCenter + new Vector2(startRadius, 0), 26f, 0.14f);
                chargeInten = MathHelper.Lerp(chargeInten, 0.7f, 0.08f);
            }
            else if (Timer < 382) {
                if (linkCenter == Vector2.Zero)
                    linkCenter = Target.Center; // 中途入场兜底
                float phi = MathHelper.Clamp(((float)Timer - 110f) * 0.0165f, 0f, 999f);
                if (Timer < 110)
                    phi = 0f;
                float radius = MathHelper.Lerp(startRadius, endRadius, MathHelper.Clamp(((float)Timer - 110f) / 80f, 0f, 1f));
                Vector2 want = linkCenter + phi.ToRotationVector2() * radius;
                NPC.velocity = (want - NPC.Center) * 0.2f;
                chargeInten = MathHelper.Lerp(chargeInten, 0.55f, 0.05f);
            }
            else if (Timer < 410) {
                if (Timer == 382)
                    NPC.velocity = (NPC.Center - linkCenter).NormalizeVector() * 9f;
                NPC.velocity *= 0.9f;
            }
            // 出口由牛头 ForceBoth(StSelect); 若牛头先亡, 上方 Partner 校验兜底
            FacePlayer();
        }

        // ================= 预警绘制 =================

        protected override void DrawStateTelegraphs() {
            if (Main.dedServ)
                return;
            // 无常三闪锁线
            if (flashLineOn) {
                Color core = flashLineLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                ACMShaders.DrawBeam(NPC.Center - flashAim * 100f, NPC.Center + flashAim * 1500f,
                    flashLineLethal ? 10f : 6f, core, new Color(55, 30, 105), flashLineLethal ? 0.9f : 0.45f);
            }
            // 勾魂牵引索: 标记中 = 幽紫 → 近到期渐红
            var tgt = Target;
            if (tgt != null && tgt.active && !tgt.dead) {
                var np = tgt.GetModPlayer<NiuMaPlayer>();
                if (np.SoulHookTimer > 0 && np.SoulHookCaster == NPC.whoAmI) {
                    float frac = 1f - np.SoulHookTimer / 180f;
                    Color core = Color.Lerp(TelegraphColors.NetherViolet, TelegraphColors.Execution, frac * 0.55f);
                    ACMShaders.DrawBeam(NPC.Center, tgt.Center, 6f, core, new Color(60, 30, 110), 0.45f + 0.5f * frac, 1.6f);
                }
            }
        }

        public override void PostDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            if (Main.dedServ)
                return;
            // 死亡收光 (全屏名额契约)
            if (State == StDeath && Timer > 100) {
                float k = MathHelper.Clamp(((float)Timer - 100f) / 32f, 0f, 1f);
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.2f * (1f - k * 0.6f), 0.5f + k * 0.5f, NiuMaHelper.GhostCore, 6f, 2.2f);
            }
        }
    }
}

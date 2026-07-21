using AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    /// <summary>
    /// 朱雀 Suzaku —— 南方·火·涅槃凤凰（四圣兽 V3「一轮会俯冲的太阳」）。
    ///
    /// V3 重做要点（见 Docs/BossRedo/FourSacredBeasts.md §1.4/§4.3/§5.3）：
    ///  ● 程序化焰翼（SuzakuFireWing.fx quad ×2）+ 振翅物理 Hub：重振翅冲量 → 缓慢下沉的 2Hz 节律,
    ///    振翅节拍即弹幕预警（翎雨轮舞的甩羽与振翅同步）。
    ///  ● 凤凰俯冲重做：两次重振翅爬升 → 折翼 12 帧自由落体滞空（粒子骤停 = 最后逃逸窗口）→
    ///    velocity set 55px/f 爆发 → 20 帧拉起弧线（速度旋转）+ 后坐。
    ///  ● 赤日审判蓄力语法（MOTION §6）：收束粒子 + 日冕 charge³ 生长 + 第 54 帧粒子硬切静默 +
    ///    爆发前收缩闪烁；爆发瞬间清一次屏边弹幕。
    ///  ● 新招「凤翔天火 Phoenix Strafe」（涅槃形态）：fake-Z 退入背景横掠 + 顶部余烬柱雨 → 破景真俯冲收尾。
    ///  ● 涅槃重生升格为五幕（PACING §6 死亡演出配方）：顿帧 → 翼熄坠落 → 灰烬寂静 →
    ///    心跳复燃（手调递减间隔数组 + pitch 递升 + 收束硬切 + 一拍黑）→ 爆燃日轮。
    ///  ● 真死「火尽星沉」：二次拦截 CheckDead, 焰翼逐段熄灭、余烬逆重力上升的挽歌式安静演出,
    ///    演出毕服务器 StrikeInstantKill 真实死亡; OnKill 震屏走 ACMUtils.AddScreenShake(10)。
    ///  ● 专属着色器 ×3：SuzakuFireWing（焰翼）/ SuzakuHeatHaze（全屏热浪, 走名额契约,
    ///    涅槃 PaletteLUT 生效时让位）/ SuzakuSolarFlare（日冕盘 decal, 不占名额）。
    /// </summary>
    [AutoloadBossHead]
    public class Suzaku : SacredBeastBase
    {
        #region 五行身份 / 阈值

        public override SacredElement Element => SacredElement.Fire;
        public override string SkyName => SuzakuSky.SkyName;

        // 供天幕等无实例引用的血量阈值常量（基类虚属性据此返回）。
        public const float HpPhase2 = 0.60f;
        public const float HpPhase3 = 0.30f;

        public override float Phase2Threshold => HpPhase2;
        public override float Phase3Threshold => HpPhase3;

        #endregion

        #region 状态枚举（写入 RawState=ai[0]；只追加不重排，保持既有值稳定）

        public enum St
        {
            Intro,
            Hub,                 // 确定性轮替枢纽（振翅物理悬停）
            P1_FeatherFan,
            P1_EmberBarrage,
            P1_SunPillars,
            Trans2,
            P2_PhoenixDive,
            P2_SolarBeams,
            P2_FeatherStorm,
            P2_SunPillars,
            Trans3,
            P3_SolarJudgment,
            P3_PhoenixDance,
            P3_SunPillarChess,
            Rebirth,             // 涅槃重生签名（五幕）
            P3_PhoenixStrafe,    // 凤翔天火（涅槃形态新招, fake-Z 背景横掠）
            DeathTrue            // 真死「火尽星沉」（涅槃后第二次死亡演出）
        }

        private St State => (St)RawState;
        private void Goto(St s) => TransitionToState((int)s);

        #endregion

        #region 持久字段（SendExtraAI 同步）

        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool didRebirth;
        private bool nirvanaForm;     // 涅槃形态（重生后）
        private bool dyingTrue;       // 真死演出进行中（第二次 CheckDead 拦截）
        private int diveCount;
        private Vector2 diveTarget;   // 锁定俯冲落点（固定，非逐帧追踪）
        private int strafeSide = 1;   // 凤翔天火起始侧
        private float glowIntensity = 1f;

        #endregion

        #region 本地视觉（不需同步）

        private int frameCounter;
        private float fxBloom;        // RadialBloom 瞬态
        private float fxRunic;        // ArenaRunic 法阵

        // —— PaletteLUT 涅槃/挽歌 grade ——
        private float rebirthLut;
        private float rebirthSat = 1f;
        private Vector4 rebirthShadow;
        private Vector4 rebirthHi;

        // —— 焰翼（SuzakuFireWing）——
        private float wingVis;        // 火焰强度可视值（0=熄灭）
        private float wingSpan = 1f;  // 展距弹簧
        private float wingSpanVel;
        private float wingFoldVis;    // 折翼可视值（1=收拢）
        private float wingFoldTarget;
        private float wingFlash;      // 翼焰爆闪
        private float flapKick;       // 振翅下压脉冲（驱动翼角与火舌）
        private float nirvanaVis;     // 涅槃金白换色
        private int lastFlapIndex = -1;

        // —— 屏幕/世界层 ——
        private float heatHaze;       // SuzakuHeatHaze 强度（与 PaletteLUT 互斥仲裁）
        private float solarCharge;    // SuzakuSolarFlare 蓄力（状态每帧显式驱动）
        private float solarBurst;     // SuzakuSolarFlare 爆燃（自衰减）
        private Vector2 solarCenter;
        private float solarRadiusWorld = 300f;
        private float skyAshen;       // 天幕去饱和（涅槃灰烬期）
        private float skyAshenTarget;
        private float skySunBurst;    // 天幕日轮爆亮（自衰减）
        private float heartPulse;     // 心跳胸口红光脉冲

        // —— 演出控制 ——
        private bool quietFrame;      // 本帧粒子骤停（爆发前静默）
        private bool useCineRotation; // 演出接管 rotation（后仰翻身等）
        private float cineRotation;
        private float bgZ;            // fake-Z 假深度可视值（0=前景 1=背景）
        private float bgZTarget;
        private int pullSide = 1;     // 俯冲拉起弧线方向
        private bool allowRealDeath;  // 服务器：真死演出毕放行 CheckDead

        // —— 节拍常量 ——
        private const int DiveClimb = 28;      // 俯冲前摇A：两次重振翅爬升
        private const int DiveStall = 12;      // 俯冲前摇B：折翼滞空（静默逃逸窗口）
        private const int DiveWindup = DiveClimb + DiveStall; // 40 帧地面影子预警总长
        private const int StrafeReentry = 26;  // 凤翔天火破景回前景

        // 心跳复燃：手调递减间隔数组（PACING §6 递进 beep 配方），累计时刻
        private static readonly int[] HeartBeats = [20, 36, 48, 57, 64, 70];
        private const int HeartCut = 70;       // 收束粒子硬切 + 一拍黑
        private const int HeartEnd = 72;

        #endregion

        #region 专属着色器静态缓存（照抄 Xuanwu 写法，不注册 ACMShaders）

        private static Asset<Effect> fireWingRef;
        private static Asset<Effect> heatHazeRef;
        private static Asset<Effect> solarFlareRef;

        private static Effect GetFireWingEffect() {
            fireWingRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/SuzakuFireWing",
                AssetRequestMode.ImmediateLoad);
            return fireWingRef?.Value;
        }

        private static Effect GetHeatHazeEffect() {
            heatHazeRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/SuzakuHeatHaze",
                AssetRequestMode.ImmediateLoad);
            return heatHazeRef?.Value;
        }

        private static Effect GetSolarFlareEffect() {
            solarFlareRef ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/SuzakuSolarFlare",
                AssetRequestMode.ImmediateLoad);
            return solarFlareRef?.Value;
        }

        #endregion

        #region SetDefaults / 静态

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 4;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 170;
            NPC.height = 170;
            NPC.damage = 240;
            NPC.defense = 65;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 5);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;
            NPC.lavaImmune = true;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.Boss2;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SuzakuSpirit>(), 1, 6, 10));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<StarfireAnnihilator>(),
                ModContent.ItemType<SolarisEternalVerdict>(),
                ModContent.ItemType<PhoenixFlameStaff>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            Goto(St.Intro);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            SendSacredBeastAI(writer);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didRebirth);
            writer.Write(nirvanaForm);
            writer.Write(dyingTrue);
            writer.Write(diveCount);
            writer.WriteVector2(diveTarget);
            writer.Write(strafeSide);
            writer.Write(glowIntensity);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ReceiveSacredBeastAI(reader);
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didRebirth = reader.ReadBoolean();
            nirvanaForm = reader.ReadBoolean();
            dyingTrue = reader.ReadBoolean();
            diveCount = reader.ReadInt32();
            diveTarget = reader.ReadVector2();
            strafeSide = reader.ReadInt32();
            glowIntensity = reader.ReadSingle();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>接触伤害与视觉对齐：演出/背景期无接触伤害（§硬性纪律·公平）。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if (State is St.Intro or St.Trans2 or St.Trans3 or St.Rebirth or St.DeathTrue)
                return false;
            if (State == St.P3_PhoenixStrafe && SubStateRaw <= 2)
                return false; // 背景横掠 + 破景重整期无碰撞
            return true;
        }

        #endregion

        #region 涅槃重生 / 真死拦截（CheckDead 契约保留）

        public override bool CheckDead() {
            // 真死演出播放完毕 → 放行真实死亡（服务器 StrikeInstantKill 触发）
            if (allowRealDeath)
                return true;

            // 首次坠亡 → 涅槃重生（核心概念保留，V3 升格为五幕演出）
            if (!didRebirth) {
                didRebirth = true;
                nirvanaForm = true;
                NPC.life = (int)(NPC.lifeMax * 0.22f);
                NPC.dontTakeDamage = true;

                ClearHostileProjectiles(); // 清场：抹去所有敌意弹幕（"重生时刻"的留白）

                ResetRotation(3);
                diveCount = 0;
                Goto(St.Rebirth);
                NPC.netUpdate = true;
                return false;
            }

            // 第二次坠亡 → 真死「火尽星沉」演出，播毕才真死
            if (!dyingTrue) {
                dyingTrue = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                ClearHostileProjectiles();
                Goto(St.DeathTrue);
                NPC.netUpdate = true;
                return false;
            }

            return true;
        }

        private static void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.hostile && p.damage > 0) p.Kill();
            }
        }

        #endregion

        #region OnKill / HitEffect

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, hit.HitDirection * 2f, -1f, 100, default, 2f);
            if (NPC.life <= 0) {
                if (dyingTrue) {
                    // 火尽星沉：安静的灰烬散逸（与涅槃的轰烈对比）
                    for (int i = 0; i < 16; i++) {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Ash, 0, 0, 140, default, 1.4f);
                        d.noGravity = true;
                        d.velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 2.2f));
                    }
                }
                else {
                    for (int i = 0; i < 50; i++) {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.SolarFlare, 0, 0, 100, default, 3f);
                        d.noGravity = true;
                        d.velocity *= 5f;
                    }
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedSuzaku = true;
            // 统一震屏预算（≤10）：替换 V2 的裸 PunchCameraModifier(25)
            ACMUtils.AddScreenShake(10f);
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            // —— 瞬态衰减 / 帧级复位 ——
            fxBloom *= 0.9f;
            fxRunic = MathHelper.Lerp(fxRunic, 0f, 0.05f);
            wingFlash *= 0.86f;
            flapKick *= 0.88f;
            heartPulse *= 0.90f;
            solarBurst *= 0.90f;
            skySunBurst *= 0.94f;
            solarCharge = 0f;       // 状态每帧显式驱动
            skyAshenTarget = 0f;
            quietFrame = false;
            useCineRotation = false;
            bgZTarget = 0f;

            if (!RunStandardPrologue(out Player target))
                return;

            if (State != St.Intro && State != St.Trans2 && State != St.Trans3 &&
                State != St.Rebirth && State != St.DeathTrue)
                CheckPhaseTransition();

            switch (State) {
                case St.Intro: RunIntro(target); break;
                case St.Hub: RunHub(target); break;
                case St.P1_FeatherFan: RunFeatherFan(target); break;
                case St.P1_EmberBarrage: RunEmberBarrage(target); break;
                case St.P1_SunPillars: RunSunPillars(target, 4, 130); break;
                case St.Trans2: RunTransition2(target); break;
                case St.P2_PhoenixDive: RunDiveAttack(target, 2); break;
                case St.P2_SolarBeams: RunSolarBeams(target, 3); break;
                case St.P2_FeatherStorm: RunFeatherStorm(target); break;
                case St.P2_SunPillars: RunSunPillars(target, 6, 150); break;
                case St.Trans3: RunTransition3(target); break;
                case St.P3_SolarJudgment: RunSolarJudgment(target); break;
                case St.P3_PhoenixDance: RunDiveAttack(target, nirvanaForm ? 4 : 3); break;
                case St.P3_SunPillarChess: RunSunPillarChess(target); break;
                case St.Rebirth: RunRebirth(target); break;
                case St.P3_PhoenixStrafe: RunPhoenixStrafe(target); break;
                case St.DeathTrue: RunDeathTrue(target); break;
            }

            UpdateWingDynamics();
            UpdateRebirthGrade();
            UpdateOrientation();

            float fireMul = nirvanaForm ? 2.4f : IsPhase3 ? 2f : IsPhase2 ? 1.5f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.4f, 0.1f) * glowIntensity * fireMul * MathF.Max(wingVis, 0.15f));

            // 翼焰余烬（振翅节律的呼吸感；静默帧硬切 = 爆发预告）
            if (!Main.dedServ && !quietFrame && wingVis > 0.15f && State != St.Intro) {
                int n = 1 + (int)(wingVis * 1.5f);
                for (int i = 0; i < n; i++) {
                    Vector2 wpos = NPC.Center + new Vector2(Main.rand.NextFloat(-70, 70), Main.rand.NextFloat(-40, 20));
                    Dust d = Dust.NewDustDirect(wpos, 0, 0, DustID.Torch, 0, -2f, 100, default, 1.7f);
                    d.noGravity = true;
                    d.velocity += -NPC.velocity * 0.05f;
                }
            }

            // 发布屏幕氛围标量（赤焰火幕 + 瞬态泛光/法阵 + 涅槃灰/日轮）
            skyAshen = MathHelper.Lerp(skyAshen, skyAshenTarget, 0.05f);
            float tint = (nirvanaForm || IsPhase3) ? 0.62f : IsPhase2 ? 0.5f : 0.38f;
            if (State == St.Intro || State == St.Rebirth || State == St.DeathTrue) tint *= 0.6f;
            SuzakuScreenSystem.Publish(NPC.Center, tint, MathHelper.Clamp(fxBloom, 0f, 1f),
                MathHelper.Clamp(fxRunic, 0f, 1f), GlobalTime,
                MathHelper.Clamp(skyAshen, 0f, 1f), MathHelper.Clamp(skySunBurst, 0f, 1f));
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3) {
                didPhase2Transition = true;
                Goto(St.Trans2);
            }
            else if (!didPhase3Transition && IsPhase3) {
                didPhase3Transition = true;
                Goto(St.Trans3);
            }
        }

        // —— 确定性轮替表（涅槃形态加入凤翔天火）——
        protected override int[] GetPhaseRotation(int phaseTier) {
            if (nirvanaForm)
                return [(int)St.P3_PhoenixDance, (int)St.P3_SolarJudgment, (int)St.P3_PhoenixStrafe, (int)St.P3_SunPillarChess];
            return phaseTier switch {
                1 => [(int)St.P1_FeatherFan, (int)St.P1_EmberBarrage, (int)St.P1_SunPillars],
                2 => [(int)St.P2_PhoenixDive, (int)St.P2_SolarBeams, (int)St.P2_FeatherStorm, (int)St.P2_SunPillars],
                _ => [(int)St.P3_SolarJudgment, (int)St.P3_PhoenixDance, (int)St.P3_SunPillarChess],
            };
        }

        #endregion

        #region 运动 / 视觉动力学助手

        /// <summary>切换演出子幕并清零 AttackTimer（涅槃五幕 / 俯冲子段共用）。</summary>
        private void SetAct(int act) {
            SubStateRaw = act;
            AttackTimer = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        /// <summary>
        /// 振翅物理悬停：重振翅一次（向上冲量 + 翼脉冲 + 后坐）→ 缓慢下沉 → 再振翅（约 2Hz）。
        /// 水平仍缓动跟踪。振翅节拍由 GlobalTime 驱动（SendSacredBeastAI 已同步, 多端一致）。
        /// </summary>
        private void FlapHover(Player target, Vector2 anchorOffset, float xSpeedCap = 14f) {
            Vector2 anchor = target.Center + anchorOffset;

            // 水平：缓动跟踪
            float desiredVx = MathHelper.Clamp((anchor.X - NPC.Center.X) * 0.045f, -xSpeedCap, xSpeedCap);
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, desiredVx, 0.08f);

            // 垂直：重力缓沉 + 周期振翅冲量
            NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.16f, 3.2f);

            int flapPeriod = nirvanaForm ? 26 : 32; // ≈2Hz 节律，涅槃更急
            int flapIdx = (int)(GlobalTime * 60f) / flapPeriod;
            if (flapIdx != lastFlapIndex) {
                lastFlapIndex = flapIdx;
                float err = NPC.Center.Y - anchor.Y; // >0 = 低于锚点 → 振得更猛
                if (err > -220f) {
                    float impulse = MathHelper.Clamp(2.6f + err * 0.018f, 0.6f, 7.5f);
                    NPC.velocity.Y = -impulse;
                    NPC.velocity.X *= 0.86f; // 振翅后坐
                    OnFlap(0.8f, flapIdx % 2 == 0);
                }
            }
        }

        /// <summary>振翅瞬间的表现（翼脉冲 + 焰羽喷发 + 可选扑翼声）。</summary>
        private void OnFlap(float strength, bool sound = true) {
            flapKick = MathF.Max(flapKick, MathHelper.Clamp(strength, 0f, 1.5f));
            wingSpanVel += 0.38f * strength;
            if (sound)
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.32f * strength, Pitch = 0.15f }, NPC.Center);
            if (!Main.dedServ && wingVis > 0.1f) {
                for (int i = 0; i < 5; i++) {
                    int side = i % 2 == 0 ? 1 : -1;
                    Vector2 tip = NPC.Center + new Vector2(side * Main.rand.NextFloat(55, 95), Main.rand.NextFloat(-45, -5));
                    Dust d = Dust.NewDustDirect(tip, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = new Vector2(side * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(1.5f, 4f) * strength);
                }
            }
        }

        /// <summary>向下搜索最近地面 Y（世界像素；参照玄武 FindGroundY 的地面搜索）。</summary>
        private static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)(searchStartY / 16f);
            for (int tileY = startTileY; tileY < startTileY + 80; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return searchStartY + 600f;
        }

        /// <summary>焰翼/假深度等视觉量的逐帧动力学（全部客户端可见字段, 由同步的 ai 状态确定性驱动）。</summary>
        private void UpdateWingDynamics() {
            // —— 翼焰目标强度 ——
            float targetIntensity;
            bool fastGrade = false;
            switch (State) {
                case St.Intro:
                    targetIntensity = PhaseTimer <= 120 ? 0f : 1f;
                    fastGrade = true;
                    break;
                case St.Rebirth: {
                    int act = (int)SubStateRaw;
                    if (act == 1 && AttackTimer <= 20)
                        targetIntensity = MathF.Max(0f, 0.75f - (int)(AttackTimer / 5f) * 0.25f); // 逐羽熄灭阶梯
                    else if (act <= 3)
                        targetIntensity = 0f;
                    else
                        targetIntensity = 1.45f; // 爆燃满焰翼
                    fastGrade = true;
                    break;
                }
                case St.DeathTrue: {
                    if (PhaseTimer < 8) targetIntensity = wingVis;
                    else if (PhaseTimer <= 48)
                        targetIntensity = MathF.Max(0f, 1f - (int)((PhaseTimer - 8) / 8f) * 0.2f); // 逐段熄灭
                    else targetIntensity = 0f;
                    fastGrade = true;
                    break;
                }
                default:
                    targetIntensity = nirvanaForm ? 1.45f : IsPhase3 ? 1.3f : IsPhase2 ? 1.12f : 1f;
                    break;
            }
            wingVis = MathHelper.Lerp(wingVis, targetIntensity, fastGrade ? 0.3f : 0.06f);

            // —— 展距弹簧（振翅回弹）与折翼 ——
            wingSpan += wingSpanVel;
            wingSpanVel += (1f - wingSpan) * 0.22f - wingSpanVel * 0.24f;
            wingFoldVis = MathHelper.Lerp(wingFoldVis, wingFoldTarget,
                wingFoldTarget > wingFoldVis ? 0.28f : 0.18f);

            // 涅槃换色 / 假深度
            nirvanaVis = MathHelper.Lerp(nirvanaVis, nirvanaForm ? 1f : 0f, 0.04f);
            bgZ = MathHelper.Lerp(bgZ, bgZTarget, 0.12f);

            // —— 热浪目标（涅槃 PaletteLUT 生效时由 PostDraw 仲裁让位）——
            float hazeTarget = State is St.Rebirth or St.DeathTrue or St.Intro
                ? 0f
                : nirvanaForm ? 0.55f : IsPhase3 ? 0.45f : IsPhase2 ? 0.3f : 0.22f;
            heatHaze = MathHelper.Lerp(heatHaze, hazeTarget, 0.05f);
        }

        /// <summary>朝向与倾角：俯冲期贴合速度方向，演出期由 cineRotation 接管，平时按横速轻摆。</summary>
        private void UpdateOrientation() {
            bool aligned =
                ((State == St.P2_PhoenixDive || State == St.P3_PhoenixDance) && SubStateRaw >= 2) ||
                (State == St.P3_PhoenixStrafe && SubStateRaw >= 3);

            if (useCineRotation) {
                NPC.rotation = cineRotation;
                return;
            }

            if (MathF.Abs(NPC.velocity.X) > 0.8f)
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;

            if (aligned && NPC.velocity.LengthSquared() > 30f) {
                float a = NPC.velocity.ToRotation();
                float wanted = NPC.spriteDirection == 1 ? a : MathF.PI - a;
                NPC.rotation += MathHelper.WrapAngle(wanted - NPC.rotation) * 0.45f;
            }
            else {
                float wanted = NPC.velocity.X * 0.015f;
                NPC.rotation += MathHelper.WrapAngle(wanted - NPC.rotation) * 0.2f;
            }
        }

        #endregion

        #region 弹幕助手

        private int Fire(Vector2 pos, Vector2 vel, int type, int dmg, float ai0 = 0f, float ai1 = 0f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            return Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel, type, dmg, 0f, Main.myPlayer, ai0, ai1);
        }

        private int EmberType => ModContent.ProjectileType<SuzakuEmber>();
        private int FeatherType => ModContent.ProjectileType<SuzakuFeather>();
        private int PillarType => ModContent.ProjectileType<SuzakuSunPillar>();
        private int BeamType => ModContent.ProjectileType<SuzakuSolarBeam>();

        private int EmberDmg => NPC.damage / 5;
        private int FeatherDmg => NPC.damage / 4;
        private int PillarDmg => NPC.damage / 3;
        private int BeamDmg => NPC.damage / 3;

        /// <summary>在玩家脚下一带生成一根自预警火柱（落点 = 玩家所在水平 + 偏移）。</summary>
        private void SpawnPillarAt(Player target, float xOffset) {
            Vector2 ground = new(target.Center.X + xOffset, target.Center.Y + 230f);
            // SunPillar 以 Bottom 锚地：Center 上移半高
            Vector2 center = ground + new Vector2(0, -280f);
            Fire(center, Vector2.Zero, PillarType, PillarDmg);
        }

        #endregion

        #region 入场「日轮开屏」(~150f)

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;

            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -900);
                NPC.velocity = Vector2.Zero;
                wingVis = 0f;
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 1.2f }, target.Center);
            }

            // 日冕盘缓降（本体隐形在盘内, 绘制层跳过本体）
            float descendT = MathHelper.Clamp(PhaseTimer / 96f, 0f, 1f);
            Vector2 anchor = new(target.Center.X, target.Center.Y + MathHelper.Lerp(-900f, -350f, ACMUtils.SineInOut(descendT)));
            NPC.Center = Vector2.Lerp(NPC.Center, anchor, 0.08f);
            NPC.velocity = Vector2.Zero;

            solarCenter = NPC.Center;
            solarRadiusWorld = 300f;
            skySunBurst = MathF.Max(skySunBurst, 0.28f * descendT); // 天幕先染赤

            if (PhaseTimer <= 96) {
                solarCharge = 0.35f + 0.65f * descendT;
            }
            else if (PhaseTimer <= 120) {
                // 收缩闪烁（MOTION §6 pre-explosion collapse：爆发前先变小）
                float ct = (PhaseTimer - 96f) / 24f;
                solarCharge = MathHelper.SmoothStep(1f, 0.42f + MathF.Cos(ct * 18f) * 0.06f, ct);
                if (PhaseTimer > 108) quietFrame = true; // 爆前静默
            }

            // 收束粒子（静默前）
            if (!Main.dedServ && PhaseTimer < 108 && PhaseTimer % 2 == 0) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(280, 280);
                Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2.2f);
                d.noGravity = true;
                d.velocity = (NPC.Center - dp) * 0.06f;
            }

            if (PhaseTimer == 120) {
                // 日轮爆开，朱雀展翼冲出
                solarBurst = 1f;
                skySunBurst = 1f;
                fxBloom = 1f;
                ACMUtils.AddScreenShake(12f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 1.5f }, NPC.Center);
                wingSpanVel += 1.2f; // 展翼爆开
                OnFlap(1.5f, false);
                if (!Main.dedServ) {
                    for (int i = 0; i < 40; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 3.2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(16, 16);
                    }
                }
            }

            if (PhaseTimer >= 150) {
                NPC.dontTakeDamage = false;
                ResetRotation(1);
                Goto(St.Hub);
            }
        }

        #endregion

        #region 轮替枢纽（振翅物理悬停）

        private void RunHub(Player target) {
            NPC.dontTakeDamage = false; // 兜底：任何演出遗留的无敌在此解除
            int tier = nirvanaForm ? 3 : PhaseTier;
            int window = nirvanaForm ? 38 : tier == 1 ? 70 : tier == 2 ? 58 : 52;

            // 凤翔环绕：横向缓摆锚点 + 振翅物理垂直节律
            float xR = nirvanaForm ? 300f : 360f;
            Vector2 offset = new(MathF.Sin(GlobalTime * 0.9f) * xR, -310f);
            FlapHover(target, offset);

            if (PhaseTimer >= window) {
                int next = NextAttack(tier);
                if (next >= 0) {
                    diveCount = 0;
                    Goto((St)next);
                }
            }
        }

        #endregion

        #region 一阶段招式

        // 焰羽扇（慢宽弹，中等预告；每波振翅后坐 + 翼焰同步爆亮）
        private void RunFeatherFan(Player target) {
            FlapHover(target, new Vector2(MathF.Sin(GlobalTime * 2f) * 150f, -330f));

            if (AttackTimer < 35) {
                // 预告：聚焰
                if (!Main.dedServ && AttackTimer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 4f;
                }
                if (AttackTimer == 30) fxBloom = 0.4f;
            }
            else if (AttackTimer == 35 || AttackTimer == 50 || AttackTimer == 65) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f }, NPC.Center);
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 7 : 5;
                float spread = MathHelper.ToRadians(42f);
                for (int i = 0; i < count; i++) {
                    float a = -spread / 2 + spread / (count - 1) * i;
                    Fire(NPC.Center, dir.RotatedBy(a) * 7.5f, FeatherType, FeatherDmg);
                }
                NPC.velocity -= dir * 8f;  // 发射后坐（MOTION §4：后坐给每次发射）
                wingFlash = 1f;
                OnFlap(1.1f, false);
            }

            if (AttackTimer > 88) Goto(St.Hub);
        }

        // 余烬弹幕（快窄弹，小预告）
        private void RunEmberBarrage(Player target) {
            FlapHover(target, new Vector2(MathF.Sin(GlobalTime * 2.5f) * 250f, -300f));

            if (AttackTimer == 20) fxBloom = 0.3f;
            if (AttackTimer == 30) {
                // 同步两根火柱牵制（自预警）
                SpawnPillarAt(target, -260);
                SpawnPillarAt(target, 260);
            }

            if (AttackTimer > 20 && AttackTimer < 78 && AttackTimer % 6 == 0) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 3 : 2;
                for (int i = 0; i < count; i++) {
                    Fire(NPC.Center, dir.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * (16f + Main.rand.NextFloat(0, 3f)), EmberType, EmberDmg);
                }
                NPC.velocity -= dir * 1.2f; // 连发微后坐
                if (Main.rand.NextBool(3)) SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f }, NPC.Center);
            }

            if (AttackTimer > 90) Goto(St.Hub);
        }

        // 火柱阵（自预警地面太阳符）
        private void RunSunPillars(Player target, int count, int duration) {
            FlapHover(target, new Vector2(0, -400f));
            fxRunic = MathHelper.Max(fxRunic, 0.4f);

            if (AttackTimer == 20) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.1f }, target.Center);
                float span = 520f;
                for (int i = 0; i < count; i++) {
                    float x = -span + (span * 2f) * i / (count - 1) + Main.rand.NextFloat(-40, 40);
                    SpawnPillarAt(target, x);
                }
            }
            // 余烬牵制
            if (AttackTimer > 30 && AttackTimer % 18 == 0) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                Fire(NPC.Center, dir * 14f, EmberType, EmberDmg);
            }

            if (AttackTimer > duration) Goto(St.Hub);
        }

        #endregion

        #region 阶段过渡

        // P2 相变 (~90f)：空中后仰翻身一周甩焰羽环，翼焰亮度上档
        private void RunTransition2(Player target) {
            NPC.dontTakeDamage = true;
            int spinSign = -NPC.spriteDirection; // 后仰 = 逆着面朝方向翻
            if (PhaseTimer == 1)
                ClearHostileProjectiles(); // 相变清弹（公平阀门）

            if (PhaseTimer < 24) {
                // 制动 + 后仰前倾（anticipation）
                NPC.velocity *= 0.86f;
                useCineRotation = true;
                cineRotation = MathHelper.Lerp(0f, 0.5f * spinSign, ACMUtils.QuadOut(PhaseTimer / 24f));
            }
            else if (PhaseTimer < 56) {
                // 翻身一周（32 帧整圆, 收尾恰为 2π ≡ 0）
                float spinT = (PhaseTimer - 24f) / 32f;
                useCineRotation = true;
                cineRotation = MathHelper.Lerp(0.5f * spinSign, spinSign * MathHelper.TwoPi, ACMUtils.SineInOut(spinT));
                NPC.velocity = new Vector2(NPC.velocity.X * 0.92f, -1.4f * (1f - spinT));

                if (PhaseTimer == 40) {
                    // 翻身顶点甩出焰羽环
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 1.5f }, NPC.Center);
                    ACMUtils.AddScreenShake(10f);
                    fxBloom = 0.85f;
                    wingFlash = 1f;
                    OnFlap(1.4f, false);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 16; i++) {
                            float a = MathHelper.TwoPi / 16 * i;
                            Fire(NPC.Center, a.ToRotationVector2() * 7f, FeatherType, FeatherDmg);
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (!Main.dedServ && PhaseTimer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(90, 90), 0, 0, DustID.SolarFlare, 0, 0, 60, default, 2.2f);
                    d.noGravity = true;
                    d.velocity = new Vector2(0, -Main.rand.NextFloat(1f, 4f));
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.2f);
                glowIntensity = 1.5f;
                ResetRotation(2);
                Goto(St.Hub);
            }
        }

        // P3 相变 (~120f)：短蓄力 + 日冕环爆（克制——高潮留给涅槃）
        private void RunTransition3(Player target) {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.85f;
            if (PhaseTimer == 1)
                ClearHostileProjectiles();

            solarCenter = NPC.Center;
            solarRadiusWorld = 260f;

            if (PhaseTimer <= 66) {
                float charge = PhaseTimer / 66f;
                solarCharge = charge;
                fxBloom = MathHelper.Max(fxBloom, charge * 0.7f);
                ACMUtils.AddScreenShake(charge * charge * 3f); // 渐强 rumble（同帧取 max）

                if (PhaseTimer <= 54) {
                    if (!Main.dedServ) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(60, 300);
                            Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                            d.noGravity = true;
                            d.velocity = (NPC.Center - dp) * 0.08f; // proportional pull 收束
                        }
                    }
                }
                else {
                    quietFrame = true; // 爆发前静默
                }
            }

            if (PhaseTimer == 66) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                fxBloom = 1f;
                solarBurst = 0.85f;
                skySunBurst = 0.7f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 20; i++) {
                        float a = MathHelper.TwoPi / 20 * i;
                        Fire(NPC.Center, a.ToRotationVector2() * 9f, FeatherType, FeatherDmg);
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 2.5f;
                ResetRotation(3);
                Goto(St.Hub);
            }
        }

        #endregion

        #region 凤凰俯冲（重做：重振翅爬升 → 折翼滞空静默 → 55px/f 爆发 → 拉起弧线）

        private void RunDiveAttack(Player target, int maxDives) {
            if (SubStateRaw == 0) {
                // —— 前摇A：两次重振翅爬升 (0..28)，同时地面影子预警亮起 ——
                if (AttackTimer == 1) {
                    diveTarget = target.Center; // 固定落点（非逐帧追踪）
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        NPC.netUpdate = true;
                }
                Vector2 apex = diveTarget + new Vector2(0, -560);
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X,
                    MathHelper.Clamp((apex.X - NPC.Center.X) * 0.06f, -16f, 16f), 0.12f);
                NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.18f, 3f);
                wingFoldTarget = 0f;

                if (AttackTimer == 4 || AttackTimer == 16) {
                    // 重振翅爬升（counter-motion：先向上, 才有骤落的对比）
                    NPC.velocity.Y = AttackTimer == 4 ? -8.5f : -11f;
                    OnFlap(1.5f);
                }

                if (AttackTimer >= DiveClimb)
                    SetAct(1);
            }
            else if (SubStateRaw == 1) {
                // —— 前摇B：折翼 12 帧自由落体滞空，粒子骤停 = 最后逃逸窗口 ——
                quietFrame = true;
                wingFoldTarget = 1f;
                NPC.velocity.X *= 0.82f;
                NPC.velocity.Y += 0.30f;

                if (AttackTimer >= DiveStall) {
                    SetAct(2);
                    NPC.velocity = (diveTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * 55f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f }, NPC.Center);
                    ACMUtils.AddScreenShake(5f);
                }
            }
            else if (SubStateRaw == 2) {
                // —— 爆发：俯冲（沿途焰羽剪切保留）——
                if (DiveActiveFrame())
                    SetAct(3);
            }
            else {
                // —— 收招：20 帧拉起弧线 + 后坐 ——
                PullUpFrame(target);
                if (AttackTimer >= 20) {
                    diveCount++;
                    if (diveCount < maxDives) SetAct(0);
                    else Goto(St.Hub);
                }
            }
        }

        /// <summary>俯冲激活帧（凤凰俯冲 / 凤翔天火破景共用）：焰羽剪切 + 到达检测 + 落点冲击。</summary>
        private bool DiveActiveFrame() {
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 80, default, 3f);
                    d.noGravity = true;
                    d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                }
            }

            // 焰羽尾迹（剪切）
            if (AttackTimer % 4 == 0) {
                Vector2 perp = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                Fire(NPC.Center + perp * 40f, perp * 6f, FeatherType, FeatherDmg);
                Fire(NPC.Center - perp * 40f, -perp * 6f, FeatherType, FeatherDmg);
            }

            bool reached = NPC.Center.Y >= diveTarget.Y - 20f || AttackTimer > 26;
            if (reached) {
                // 落点冲击：硬刹 + 冲击环 + 震屏 8
                fxBloom = 0.85f;
                heatHaze = MathF.Max(heatHaze, 0.7f);
                ACMUtils.AddScreenShake(8f);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, NPC.Center);
                NPC.velocity *= 0.35f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int n = nirvanaForm ? 14 : 10;
                    for (int i = 0; i < n; i++) {
                        float angle = MathHelper.TwoPi / n * i;
                        Fire(NPC.Center, angle.ToRotationVector2() * 8f, EmberType, EmberDmg);
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 24; i++) {
                        float a = MathHelper.TwoPi / 24 * i;
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Torch, 0, 0, 90, default, 2.4f);
                        d.noGravity = true;
                        d.velocity = a.ToRotationVector2() * Main.rand.NextFloat(7f, 12f);
                    }
                }
            }
            return reached;
        }

        /// <summary>拉起弧线帧：速度旋转扫出弧线 + 展翼回弹（俯冲 / 破景共用收招）。</summary>
        private void PullUpFrame(Player target) {
            if (AttackTimer == 1) {
                pullSide = NPC.Center.X < target.Center.X ? -1 : 1; // 拉向远离玩家一侧, 留出身位
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitY) * 26f;
                wingFoldTarget = 0f;
                wingSpanVel += 0.8f;
                OnFlap(1.3f, false);
            }
            NPC.velocity = NPC.velocity.RotatedBy(-0.085f * pullSide);
            NPC.velocity *= 0.97f;
        }

        #endregion

        #region 赤日光束 / 赤日审判（蓄力语法）

        // 二阶段：少量扇形扫掠光束（发射瞬间后坐）
        private void RunSolarBeams(Player target, int beamCount) {
            FlapHover(target, new Vector2(0, -360f));

            if (AttackTimer == 25) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f }, NPC.Center);
                fxBloom = 0.5f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 baseDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    float spread = MathHelper.ToRadians(28f);
                    for (int i = 0; i < beamCount; i++) {
                        float a = beamCount == 1 ? 0 : -spread + (spread * 2f) * i / (beamCount - 1);
                        Vector2 dir = baseDir.RotatedBy(a);
                        float sweep = (i % 2 == 0 ? 1f : -1f) * 0.006f;
                        Fire(NPC.Center, dir, BeamType, BeamDmg, NPC.whoAmI, sweep);
                    }
                }
                NPC.velocity -= (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 4f;
                wingFlash = 0.8f;
            }

            if (AttackTimer > 120) Goto(St.Hub);
        }

        // 三阶段签名：径向审判（处决级 75 帧蓄力语法：收束 → 硬切静默 → 收缩闪烁 → 爆发）
        private void RunSolarJudgment(Player target) {
            if (SubStateRaw == 0) {
                NPC.velocity *= 0.9f;
                float charge = AttackTimer / 75f;
                solarCenter = NPC.Center;
                solarRadiusWorld = 320f;
                fxRunic = MathHelper.Max(fxRunic, charge * 0.8f);
                fxBloom = MathHelper.Max(fxBloom, charge * 0.6f);
                ACMUtils.AddScreenShake(charge * charge * 3f); // 渐强 rumble（取 max, 非离散抖）

                if (AttackTimer <= 54) {
                    solarCharge = charge;
                    // 收束粒子（proportional pull 向心）
                    if (!Main.dedServ && AttackTimer % 4 == 0) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(320, 320);
                            Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                            d.noGravity = true;
                            d.velocity = (NPC.Center - dp) * 0.085f;
                        }
                    }
                }
                else {
                    // 第 54 帧粒子硬切 = 静默；日冕收缩闪烁（爆发前先变小）
                    quietFrame = true;
                    float ct = (AttackTimer - 54f) / 21f;
                    solarCharge = MathHelper.SmoothStep(charge, 0.40f + MathF.Cos(ct * 14f) * 0.07f, ct);
                }

                if (AttackTimer >= 75) {
                    SetAct(1);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                    ACMUtils.AddScreenShake(11f);
                    fxBloom = 1f;
                    solarBurst = 1f;
                    heatHaze = MathF.Max(heatHaze, 0.75f);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int beams = nirvanaForm ? 8 : 6;
                        for (int i = 0; i < beams; i++) {
                            float a = MathHelper.TwoPi / beams * i;
                            float sweep = (i % 2 == 0 ? 1f : -1f) * 0.008f;
                            Fire(NPC.Center, a.ToRotationVector2(), BeamType, BeamDmg, NPC.whoAmI, sweep);
                        }
                        // 环绕落点火柱（审判落地）
                        for (int i = 0; i < 5; i++)
                            SpawnPillarAt(target, -480 + 240 * i);

                        // 公平阀门：爆发瞬间清一次屏边余弹, 让径向审判可读
                        for (int i = 0; i < Main.maxProjectiles; i++) {
                            Projectile p = Main.projectile[i];
                            if (p.active && p.hostile && (p.type == EmberType || p.type == FeatherType) &&
                                p.Distance(target.Center) > 860f)
                                p.Kill();
                        }
                    }
                }
            }
            else {
                // 释放 42f + 低压收招 63f（= 105f, 全程无新增压力）
                NPC.velocity *= 0.92f;
                fxRunic = MathHelper.Max(fxRunic, 0.4f);
                if (AttackTimer > 105) Goto(St.Hub);
            }
        }

        #endregion

        #region 翎雨轮舞（重做：4 次振翅甩羽弧） / 火柱棋局

        private void RunFeatherStorm(Player target) {
            NPC.velocity *= 0.9f;

            if (AttackTimer < 25) {
                // 前摇：聚焰起翼
                if (!Main.dedServ && AttackTimer % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 4f;
                }
                if (AttackTimer == 20) {
                    fxBloom = 0.4f;
                    wingSpanVel += 0.5f;
                }
            }

            // 4 次振翅甩羽：25 / 48 / 71 / 94（振翅间 12+ 帧空隙, 音画同步 = 预警）
            if (AttackTimer >= 25 && AttackTimer <= 94 && (AttackTimer - 25) % 23 == 0) {
                int swing = (int)(AttackTimer - 25) / 23;
                int side = swing % 2 == 0 ? 1 : -1; // 左右交替
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                Vector2 perp = new(-dir.Y, dir.X);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 甩出一道朝向玩家的羽弧（非全向）：7 枚, 弧内速度呈拱形 → 弯曲羽弧
                    const int arcCount = 7;
                    float fan = MathHelper.ToRadians(52f);
                    for (int i = 0; i < arcCount; i++) {
                        float a = -fan / 2 + fan / (arcCount - 1) * i + side * MathHelper.ToRadians(6f);
                        float speed = 6.2f + MathF.Sin(i / (arcCount - 1f) * MathHelper.Pi) * 2.6f;
                        Fire(NPC.Center + perp * side * 46f, dir.RotatedBy(a) * speed, FeatherType, FeatherDmg);
                    }
                }

                NPC.velocity -= dir * 12f;  // 甩羽后坐 12px
                wingFlash = 1f;             // 翼焰爆闪
                OnFlap(1.3f, false);
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.75f, Pitch = -0.1f + swing * 0.12f }, NPC.Center);
            }

            if (AttackTimer > 118) Goto(St.Hub);
        }

        // 火柱"棋局"：交错太阳符，留安全格
        private void RunSunPillarChess(Player target) {
            FlapHover(target, new Vector2(0, -420f));
            fxRunic = MathHelper.Max(fxRunic, 0.7f);

            const float spacing = 200f;
            // 第一波：偶数格
            if (AttackTimer == 20) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 1.1f }, target.Center);
                for (int i = -3; i <= 3; i += 2)
                    SpawnPillarAt(target, i * spacing);
            }
            // 第二波：奇数格（错位 → 形成棋盘可走缝）
            if (AttackTimer == 20 + SuzakuSunPillar.WindupTicks + SuzakuSunPillar.StrikeTicks) {
                for (int i = -2; i <= 2; i += 2)
                    SpawnPillarAt(target, i * spacing);
            }

            if (AttackTimer > 170) Goto(St.Hub);
        }

        #endregion

        #region 凤翔天火 Phoenix Strafe（涅槃形态：fake-Z 背景横掠 + 余烬柱雨 → 破景真俯冲）

        private void RunPhoenixStrafe(Player target) {
            if (SubStateRaw == 0) {
                // —— 退入背景 (30f)：缩小、变淡、无碰撞 ——
                NPC.dontTakeDamage = true;
                bgZTarget = 1f;
                if (AttackTimer == 1) {
                    strafeSide = NPC.Center.X < target.Center.X ? -1 : 1;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        NPC.netUpdate = true;
                }
                Vector2 anchor = target.Center + new Vector2(strafeSide * 640f, -430f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.05f, 0.1f);

                if (AttackTimer >= 30)
                    SetAct(1);
            }
            else if (SubStateRaw == 1) {
                // —— 背景横掠 160f（2 个来回）+ 顶部余烬柱雨 ——
                NPC.dontTakeDamage = true;
                bgZTarget = 1f;
                float t = AttackTimer / 160f;
                float phase = MathHelper.TwoPi * t + (strafeSide > 0 ? 0f : MathHelper.Pi);
                Vector2 want = new(
                    target.Center.X + MathF.Cos(phase) * 640f,
                    target.Center.Y - 430f + MathF.Sin(t * 12f) * 22f);
                NPC.velocity = (want - NPC.Center) * 0.14f;

                // 余烬柱雨：每 40f 一波 ×4, 每波 3 柱, 柱间 220px（≥180）, 波间半格错位, 柱自带 45f 太阳符预告
                if ((int)AttackTimer % 40 == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                    int wave = (int)AttackTimer / 40;
                    float shift = wave % 2 == 0 ? -55f : 55f;
                    for (int i = -1; i <= 1; i++)
                        SpawnPillarAt(target, i * 220f + shift);
                }
                // 剪影尾焰：高空余烬顺着掠过路径洒落（可视预警 = 剪影自身）
                if (AttackTimer % 9 == 0 && t > 0.05f && t < 0.95f)
                    Fire(new Vector2(NPC.Center.X, target.Center.Y - 720f),
                        new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), 10.5f), EmberType, EmberDmg);

                if (AttackTimer >= 160) {
                    diveTarget = target.Center; // 破景落点此刻锁定（影子预警立即亮起）
                    SetAct(2);
                }
            }
            else if (SubStateRaw == 2) {
                // —— 破景回前景 26f：放大回材质、定位落点上空、末段折翼 ——
                bgZTarget = 0f;
                Vector2 apex = diveTarget + new Vector2(0, -540);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (apex - NPC.Center) * 0.11f, 0.16f);
                if (AttackTimer >= 14) NPC.dontTakeDamage = false;
                if (AttackTimer >= 20) {
                    wingFoldTarget = 1f;
                    quietFrame = true; // 破景俯冲同样给静默拍
                }

                if (AttackTimer >= StrafeReentry) {
                    SetAct(3);
                    NPC.velocity = (diveTarget - NPC.Center).SafeNormalize(Vector2.UnitY) * 55f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f }, NPC.Center);
                    ACMUtils.AddScreenShake(5f);
                }
            }
            else if (SubStateRaw == 3) {
                // —— 一次真俯冲收尾 ——
                if (DiveActiveFrame())
                    SetAct(4);
            }
            else {
                PullUpFrame(target);
                if (AttackTimer >= 20) Goto(St.Hub);
            }
        }

        #endregion

        #region 涅槃重生 set-piece（五幕, ~300f）

        private void RunRebirth(Player target) {
            NPC.dontTakeDamage = true;
            int act = (int)SubStateRaw;

            switch (act) {
                case 0:
                    // —— 幕一·顿帧 6f：HP=0 瞬间速度归零、粒子骤停 ——
                    NPC.velocity = Vector2.Zero;
                    quietFrame = true;
                    if (AttackTimer >= 6) SetAct(1);
                    break;

                case 1:
                    // —— 幕二·翼焰逐羽熄灭 20f → 真自由落体坠地 ——
                    quietFrame = true;
                    if (AttackTimer <= 20) {
                        NPC.velocity *= 0.8f;
                        if (!Main.dedServ && AttackTimer % 5 == 0) {
                            // 每熄一羽, 散出一撮灰屑
                            for (int i = 0; i < 4; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 40), 0, 0, DustID.Ash, 0, 0, 130, default, 1.5f);
                                d.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f));
                            }
                        }
                    }
                    else {
                        // 重力加速度坠落
                        NPC.velocity.X *= 0.98f;
                        NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.5f, 18f);

                        // 以玩家所站高度为参照搜地（空岛竞技场下方无实体块时不会无限坠落）,
                        // 且相对玩家最多下坠 620px 兜底
                        float ground = FindGroundY(NPC.Center.X, MathF.Max(NPC.Center.Y, target.Center.Y - 60f));
                        ground = MathF.Min(ground, target.Center.Y + 620f);
                        if (NPC.Bottom.Y >= ground - 4f || AttackTimer > 150) {
                            NPC.position.Y = ground - NPC.height;
                            NPC.velocity = Vector2.Zero;
                            // 坠地闷响 + 尘土
                            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                            ACMUtils.AddScreenShake(8f);
                            if (!Main.dedServ) {
                                for (int i = 0; i < 26; i++) {
                                    Dust d = Dust.NewDustDirect(NPC.Bottom - new Vector2(70, 8), 140, 12, DustID.Smoke, 0, 0, 120, default, 2f);
                                    d.velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(0.5f, 3f));
                                }
                            }
                            SetAct(2);
                        }
                    }
                    break;

                case 2:
                    // —— 幕三·灰烬寂静 100f：全屏去饱和、灰烬上升、无任何攻击 ——
                    NPC.velocity = Vector2.Zero;
                    skyAshenTarget = 1f;
                    if (!Main.dedServ && Main.rand.NextBool(3)) {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Ash,
                            Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.4f, 1.4f), 140, default, 1.5f);
                        d.noGravity = true;
                    }
                    if (AttackTimer >= 100) SetAct(3);
                    break;

                case 3:
                    // —— 幕四·心跳复燃：递减间隔脉动 + pitch 递升 → 收束硬切 + 一拍黑 ——
                    NPC.velocity = Vector2.Zero;
                    skyAshenTarget = 1f;
                    for (int b = 0; b < HeartBeats.Length; b++) {
                        if ((int)AttackTimer == HeartBeats[b]) {
                            heartPulse = 1f;
                            fxBloom = MathF.Max(fxBloom, 0.10f + b * 0.05f);
                            SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.55f + b * 0.14f, Volume = 0.85f }, NPC.Center);
                        }
                    }
                    // 后半程收束粒子（吸向胸口）
                    if (!Main.dedServ && AttackTimer > 44 && AttackTimer < HeartCut && AttackTimer % 2 == 0) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(260, 260);
                        Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 60, default, 2.4f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dp) * 0.09f;
                    }
                    if (AttackTimer >= HeartCut) quietFrame = true; // 硬切 + 一拍黑（grade 侧处理）
                    if (AttackTimer >= HeartEnd) SetAct(4);
                    break;

                case 4:
                    // —— 幕五·爆燃 20f：日轮全屏绽放, 全场最强震屏（全战斗唯一一次 14）——
                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 1f, Volume = 2f }, NPC.Center);
                        ACMUtils.AddScreenShake(14f);
                        fxBloom = 1f;
                        fxRunic = 1f;
                        glowIntensity = 3f;
                        solarCenter = NPC.Center;
                        solarRadiusWorld = 560f;
                        solarBurst = 1f;
                        skySunBurst = 1f;
                        wingSpanVel += 1.4f;
                        OnFlap(1.5f, false);
                        if (!Main.dedServ) {
                            for (int i = 0; i < 60; i++) {
                                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 4f);
                                d.noGravity = true;
                                d.velocity = Main.rand.NextVector2Circular(22, 22);
                            }
                        }
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 双层同心焰环（保留）
                            for (int i = 0; i < 30; i++) {
                                float a = MathHelper.TwoPi / 30 * i;
                                Fire(NPC.Center, a.ToRotationVector2() * 13f, FeatherType, FeatherDmg);
                            }
                            for (int i = 0; i < 22; i++) {
                                float a = MathHelper.TwoPi / 22 * i + MathHelper.ToRadians(8f);
                                Fire(NPC.Center, a.ToRotationVector2() * 8f, EmberType, EmberDmg);
                            }
                            // 竞技场点燃：环形火柱（保留）
                            for (int i = 0; i < 6; i++)
                                SpawnPillarAt(target, -600 + 240 * i);
                        }
                    }
                    solarCenter = NPC.Center;
                    NPC.velocity.Y = -5.2f * (1f - AttackTimer / 20f); // 满焰翼升起
                    if (AttackTimer >= 20) SetAct(5);
                    break;

                default:
                    // —— 复生缓冲 40f：升空归位, 不出招 ——
                    Vector2 hover = target.Center + new Vector2(0, -320);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (hover - NPC.Center) * 0.05f, 0.07f);
                    fxRunic = MathHelper.Max(fxRunic, 0.5f);
                    if (!Main.dedServ && AttackTimer % 4 == 0) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(220, 220);
                            Dust d = Dust.NewDustDirect(dp, 0, 0, DustID.SolarFlare, 0, 0, 50, default, 3f);
                            d.noGravity = true;
                            d.velocity = new Vector2(0, -Main.rand.NextFloat(2, 6));
                        }
                    }
                    if (AttackTimer >= 40) {
                        NPC.dontTakeDamage = false;
                        ResetRotation(3);
                        diveCount = 0;
                        Goto(St.Hub);
                    }
                    break;
            }
        }

        /// <summary>PaletteLUT grade（涅槃五幕 / 真死挽歌共用调色驱动）。</summary>
        private void UpdateRebirthGrade() {
            if (State == St.Rebirth) {
                int act = (int)SubStateRaw;
                if (act <= 1) {
                    // 坠落途中渐灰
                    rebirthLut = MathHelper.Lerp(rebirthLut, 0.55f, 0.05f);
                    rebirthSat = MathHelper.Lerp(rebirthSat, 0.45f, 0.05f);
                    rebirthShadow = new Vector4(new Color(64, 60, 58).ToVector3(), 0.5f);
                    rebirthHi = new Vector4(new Color(120, 116, 112).ToVector3(), 0.5f);
                }
                else if (act == 2) {
                    float g = MathHelper.Clamp(AttackTimer / 40f, 0f, 1f);
                    rebirthLut = MathHelper.Lerp(rebirthLut, 1f, 0.08f);
                    rebirthSat = MathHelper.Lerp(rebirthSat, 0.1f, 0.08f);
                    rebirthShadow = new Vector4(new Color(64, 60, 58).ToVector3(), 0.6f * MathF.Max(g, 0.5f));
                    rebirthHi = new Vector4(new Color(120, 116, 112).ToVector3(), 0.6f * MathF.Max(g, 0.5f));
                }
                else if (act == 3) {
                    if (AttackTimer >= HeartCut) {
                        // 一拍黑（1~2 帧低亮度）
                        rebirthLut = 1f;
                        rebirthSat = 0f;
                        rebirthShadow = new Vector4(0f, 0f, 0f, 0.95f);
                        rebirthHi = new Vector4(0.05f, 0.04f, 0.04f, 0.95f);
                    }
                    else {
                        // 灰底随心跳缓慢回暖
                        float warm = AttackTimer / HeartCut * 0.25f;
                        rebirthLut = 1f;
                        rebirthSat = 0.1f + warm * 0.3f;
                        rebirthShadow = new Vector4(new Color(70, 58, 54).ToVector3(), 0.6f);
                        rebirthHi = Vector4.Lerp(
                            new Vector4(new Color(120, 116, 112).ToVector3(), 0.6f),
                            new Vector4(new Color(150, 110, 96).ToVector3(), 0.6f), warm * 4f);
                    }
                }
                else if (act == 4) {
                    // 爆燃：PaletteLUT 甩红
                    float g = MathHelper.Clamp(AttackTimer / 20f, 0f, 1f);
                    rebirthLut = MathHelper.Lerp(1f, 0.85f, g);
                    rebirthSat = MathHelper.Lerp(0.1f, 1.5f, g);
                    rebirthShadow = new Vector4(new Color(120, 18, 12).ToVector3(), 0.7f);
                    rebirthHi = new Vector4(new Color(255, 150, 70).ToVector3(), 0.7f);
                }
                else {
                    // 甩红衰减回常规
                    rebirthLut = MathHelper.Lerp(rebirthLut, 0f, 0.06f);
                    rebirthSat = MathHelper.Lerp(rebirthSat, 1f, 0.06f);
                }
            }
            else if (State == St.DeathTrue) {
                // 火尽星沉：暖灰挽歌调（比涅槃灰更柔和）
                rebirthLut = MathHelper.Lerp(rebirthLut, 0.5f, 0.02f);
                rebirthSat = MathHelper.Lerp(rebirthSat, 0.35f, 0.02f);
                rebirthShadow = new Vector4(new Color(58, 46, 44).ToVector3(), 0.55f);
                rebirthHi = new Vector4(new Color(190, 150, 120).ToVector3(), 0.5f);
            }
            else {
                rebirthLut = MathHelper.Lerp(rebirthLut, 0f, 0.08f);
                rebirthSat = MathHelper.Lerp(rebirthSat, 1f, 0.08f);
            }
        }

        #endregion

        #region 真死「火尽星沉」(~170f)

        private void RunDeathTrue(Player target) {
            NPC.dontTakeDamage = true;
            skyAshenTarget = PhaseTimer > 48 ? 0.6f : 0.2f;
            quietFrame = PhaseTimer > 48;

            if (PhaseTimer == 1) {
                NPC.velocity = Vector2.Zero;
                ClearHostileProjectiles();
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center); // 力竭哀鸣
            }

            // 翼焰逐段熄灭（8..48, 每 8f 一档, 由 UpdateWingDynamics 取档）
            if (!Main.dedServ && PhaseTimer >= 8 && PhaseTimer <= 48 && PhaseTimer % 8 == 0) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.4f, Pitch = 0.3f }, NPC.Center);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 40), 0, 0, DustID.Ash, 0, 0, 130, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.6f));
                }
            }

            // 身体缓缓下沉
            if (PhaseTimer > 20) {
                NPC.velocity.X *= 0.96f;
                NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, 0.42f, 0.05f);
            }

            // 余烬逆重力上升（挽歌式安静）
            if (!Main.dedServ && PhaseTimer > 48 && PhaseTimer < 150 && PhaseTimer % 3 == 0) {
                Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(110, 70);
                Dust d = Dust.NewDustDirect(dp, 0, 0, Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.Ash, 0, 0, 120, default, 1.5f);
                d.noGravity = true;
                d.velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 2.4f));
            }

            // 最后一次微弱暖光脉动
            if (PhaseTimer == 118) {
                heartPulse = 1f;
                fxBloom = 0.22f;
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.6f, Volume = 0.6f }, NPC.Center);
            }

            // 化作一粒上升余烬（绘制层处理本体淡出与余烬点）
            if (PhaseTimer == 136)
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.4f, Volume = 0.55f }, NPC.Center);

            if (PhaseTimer >= 170 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 演出毕：服务器端真实死亡（掉落 / downedSuzaku 照常）
                allowRealDeath = true;
                NPC.StrikeInstantKill();
            }
        }

        #endregion

        #region 绘制

        public override void FindFrame(int frameHeight) {
            bool missile =
                ((State == St.P2_PhoenixDive || State == St.P3_PhoenixDance) && SubStateRaw == 2) ||
                (State == St.P3_PhoenixStrafe && SubStateRaw == 3);
            bool grounded = State == St.Rebirth && SubStateRaw >= 1 && SubStateRaw <= 3;
            if (missile || grounded) {
                NPC.frame.Y = 0;
                frameCounter = 0;
                return;
            }

            bool slow = State is St.Intro or St.Trans2 or St.Trans3 or St.Rebirth or St.DeathTrue;
            int rate = slow ? 10 : (NPC.velocity.LengthSquared() > 100f ? 4 : 6);
            frameCounter++;
            if (frameCounter >= rate) {
                frameCounter = 0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * 4)
                    NPC.frame.Y = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 日冕盘 decal（入场日轮 / 审判蓄力 / 涅槃爆燃, 不占全屏名额）
            DrawSolarFlareDecal(spriteBatch);

            // 俯冲蓄力：固定落点地面影子预警（非致命赤 → 末段转金）
            DrawDiveTelegraph(spriteBatch, screenPos);

            // 入场日轮内：本体隐形（只画日冕盘）
            if (State == St.Intro && PhaseTimer <= 120)
                return false;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool facingRight = NPC.spriteDirection >= 0;
            SpriteEffects effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRotation = facingRight ? NPC.rotation : -NPC.rotation;

            // fake-Z / 真死淡出
            float bodyScale = NPC.scale * (1f - 0.55f * bgZ);
            float bodyAlpha = 1f - 0.45f * bgZ;
            if (State == St.DeathTrue && PhaseTimer > 136)
                bodyAlpha *= MathHelper.Clamp(1f - (PhaseTimer - 136f) / 30f, 0f, 1f);

            // 涅槃灰烬 / 真死挽歌：本体压暗
            Color bodyColor = drawColor;
            if (State == St.Rebirth) {
                int act = (int)SubStateRaw;
                float g = act >= 2 && act <= 3 ? 1f : act == 1 ? MathHelper.Clamp(AttackTimer / 40f, 0f, 0.7f) : 0f;
                bodyColor = Color.Lerp(drawColor, new Color(70, 65, 62), g);
            }
            else if (State == St.DeathTrue) {
                bodyColor = Color.Lerp(drawColor, new Color(96, 82, 76), MathHelper.Clamp(PhaseTimer / 60f, 0f, 0.8f));
            }
            bodyColor *= bodyAlpha;

            // 程序化焰翼（本体之后、拖尾之前点亮）
            DrawFireWings(spriteBatch, screenPos, drawRotation, bodyScale, bodyAlpha);

            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float alpha = 0.5f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]) * bodyAlpha;
                Color trailColor = bodyColor * alpha;
                trailColor.G = (byte)Math.Min(trailColor.G * 1.1f, 255);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, drawRotation, origin,
                    bodyScale * (1f - i * 0.015f), effects, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, bodyColor, drawRotation, origin, bodyScale, effects, 0f);

            // 涅槃形态：轻微金边（加性剪影）
            if (nirvanaVis > 0.05f && wingVis > 0.1f) {
                Color rim = new Color(255, 220, 140, 0) * (0.30f * nirvanaVis * bodyAlpha);
                spriteBatch.Draw(texture, drawPos, frame, rim, drawRotation, origin, bodyScale * 1.05f, effects, 0f);
            }

            // 心跳复燃：胸口微弱红光脉动（扩散圈）
            if (heartPulse > 0.03f) {
                Texture2D glowT = ACMAsset.SoftGlow;
                if (glowT != null) {
                    Vector2 chest = drawPos + new Vector2(NPC.spriteDirection * 6f, 8f);
                    float spread = 0.45f + (1f - heartPulse) * 0.5f;
                    Color hc = new Color(255, 62, 40, 0) * (heartPulse * 0.85f);
                    spriteBatch.Draw(glowT, chest, null, hc, 0f, glowT.Size() / 2f, spread, SpriteEffects.None, 0f);
                    spriteBatch.Draw(glowT, chest, null, new Color(255, 170, 120, 0) * (heartPulse * 0.5f), 0f, glowT.Size() / 2f, spread * 0.45f, SpriteEffects.None, 0f);
                }
            }

            // 真死终章：化作一粒上升余烬熄灭
            if (State == St.DeathTrue && PhaseTimer > 136) {
                float t = MathHelper.Clamp((PhaseTimer - 136f) / 32f, 0f, 1f);
                Texture2D glowT = ACMAsset.SoftGlow;
                if (glowT != null) {
                    Vector2 ep = drawPos + new Vector2(0, -t * 120f);
                    float ea = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                    spriteBatch.Draw(glowT, ep, null, new Color(255, 200, 120, 0) * (ea * 0.9f), 0f, glowT.Size() / 2f, 0.20f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(glowT, ep, null, new Color(255, 255, 230, 0) * (ea * 0.8f), 0f, glowT.Size() / 2f, 0.09f, SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        /// <summary>程序化焰翼：SuzakuFireWing quad ×2（左右对称旋转复用同一 quad）。</summary>
        private void DrawFireWings(SpriteBatch sb, Vector2 screenPos, float drawRotation, float bodyScale, float bodyAlpha) {
            if (Main.dedServ || wingVis <= 0.03f) return;
            float span = (0.55f + 0.45f * MathHelper.Clamp(wingSpan, 0.4f, 1.6f)) * (1f - wingFoldVis);
            if (span <= 0.05f) return;

            Effect fx = GetFireWingEffect();
            Texture2D carrier = ACMShaders.NoiseTexture;
            if (fx == null || carrier == null) return;

            // 振翅下压曲线：flapKick 脉冲 → 翼角从上抬(-1.05)拍向下压(0.42)
            float down = MathHelper.Clamp(flapKick, 0f, 1f);
            float baseAng = MathHelper.Lerp(-1.05f, 0.42f, down);
            // 折翼时整体上收
            baseAng -= wingFoldVis * 0.9f;

            float wingLen = 300f * bodyScale * span;
            float wingTh = 190f * bodyScale * (0.75f + 0.25f * span);
            Vector2 quadScale = new(wingLen / carrier.Width, wingTh / carrier.Height);
            Vector2 root = NPC.Center - screenPos + new Vector2(0, -10f * NPC.scale).RotatedBy(drawRotation);
            Vector2 quadOrigin = new(0, carrier.Height / 2f);
            Color tint = Color.White * bodyAlpha;

            fx.Parameters["uTime"]?.SetValue(GlobalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(wingVis * (1f + wingFlash * 0.8f), 0f, 1.8f));
            fx.Parameters["uNirvana"]?.SetValue(MathHelper.Clamp(nirvanaVis, 0f, 1f));
            fx.Parameters["uFlap"]?.SetValue(MathHelper.Clamp(down + wingFlash * 0.3f, 0f, 1f));

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = carrier;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            // 右翼 + 左翼（π-θ 镜像方向；翼形 SDF 上下对称, 颠倒无碍）
            sb.Draw(carrier, root, null, tint, drawRotation + baseAng, quadOrigin, quadScale, SpriteEffects.None, 0f);
            sb.Draw(carrier, root, null, tint, drawRotation + MathF.PI - baseAng, quadOrigin, quadScale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>日冕盘 decal（SuzakuSolarFlare, 屏幕空间, Additive, 不占全屏名额）。</summary>
        private void DrawSolarFlareDecal(SpriteBatch sb) {
            if (Main.dedServ) return;
            if (solarCharge <= 0.01f && solarBurst <= 0.02f) return;
            Effect fx = GetSolarFlareEffect();
            if (fx == null) return;

            ACMShaders.WorldDecalParams(solarCenter, solarRadiusWorld, out Vector2 uvCenter, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(GlobalTime);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(0.25f + solarCharge * 0.75f + solarBurst, 0f, 1f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(solarCharge, 0f, 1f));
            fx.Parameters["uBurst"]?.SetValue(MathHelper.Clamp(solarBurst, 0f, 1f));
            Color edge = nirvanaVis > 0.5f ? TelegraphColors.Gold : TelegraphColors.Vermilion;
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uColorHot"]?.SetValue(new Color(255, 240, 200).ToVector4());

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        private void DrawDiveTelegraph(SpriteBatch sb, Vector2 screenPos) {
            if (Main.dedServ) return;

            // 俯冲前摇（爬升+滞空）与凤翔天火破景期显示影子
            float grow = -1f;
            if ((State == St.P2_PhoenixDive || State == St.P3_PhoenixDance) && SubStateRaw <= 1 && AttackTimer >= 1)
                grow = MathHelper.Clamp((SubStateRaw == 0 ? AttackTimer : DiveClimb + AttackTimer) / (float)DiveWindup, 0f, 1f);
            else if (State == St.P3_PhoenixStrafe && SubStateRaw == 2)
                grow = MathHelper.Clamp(AttackTimer / (float)StrafeReentry, 0f, 1f);
            if (grow < 0f) return;

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 pos = diveTarget - screenPos;
            Vector2 go = glow.Size() / 2f;

            // 非致命赤 → 临击转金（提示"即将致命"）
            Color c = Color.Lerp(TelegraphColors.Vermilion, TelegraphColors.Gold, grow * grow);
            c.A = 0;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            // 椭圆地影（横向压扁）
            sb.Draw(glow, pos, null, c * (0.4f + grow * 0.5f), 0f, go, new Vector2(2.0f, 0.7f) * (0.5f + grow * 0.6f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, c * 0.6f, 0f, go, new Vector2(1.1f, 0.4f) * (0.4f + grow * 0.5f), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // 全屏后处理仲裁：PaletteLUT（涅槃/挽歌 grade）优先, 否则 SuzakuHeatHaze；同帧只申请一个名额
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ) return;

            if (rebirthLut > 0.01f) {
                DrawPaletteGrade(spriteBatch);
                return;
            }
            DrawHeatHaze(spriteBatch);
        }

        private void DrawPaletteGrade(SpriteBatch spriteBatch) {
            if (!MythologyConfig.FullscreenShadersEnabled) return;
            if (!ACMShaders.RequestFullscreenSlot()) return;

            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null) return;

            fx.Parameters["uTime"]?.SetValue(GlobalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(rebirthLut, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uSaturation"]?.SetValue(rebirthSat);
            fx.Parameters["uHueShift"]?.SetValue(0f);
            fx.Parameters["uShadowTint"]?.SetValue(rebirthShadow);
            fx.Parameters["uHighlightTint"]?.SetValue(rebirthHi);
            fx.Parameters["uSplit"]?.SetValue(0f);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        private void DrawHeatHaze(SpriteBatch spriteBatch) {
            if (heatHaze <= 0.02f) return;
            if (!MythologyConfig.FullscreenShadersEnabled) return;
            if (!ACMShaders.RequestFullscreenSlot()) return;

            Effect fx = GetHeatHazeEffect();
            if (fx == null) return;

            Vector2 uvCenter = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue(GlobalTime);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(heatHaze, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.42f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx, bindNoise: true);
        }

        #endregion
    }
}

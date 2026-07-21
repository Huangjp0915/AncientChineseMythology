using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 赢勾之手 —— 锯齿冥刃 (V3 重做)。
    /// 姿态完全由 Boss 已同步态 (Phase/SubState/SubTimer/ComboCount/AimAngle/AimPoint) 确定性推导,
    /// 无需额外发包; 弹幕生成只走服务器。
    /// 运动语法: 弹簧-阻尼追锚 (慢=重) → pow8 反向蓄势 (吸气) → 单帧瞬发 (快照) → 硬刹 + 角冲量余摆 (收势)。
    /// 接触伤害仅在爆发帧窗口且速度达标时开启, 与视觉严格对齐。
    /// </summary>
    internal class YingouHand : ModNPC
    {
        [VaultLoaden("AncientChineseMythology/NPCs/Boss/Yingous/")]
        private static Asset<Texture2D> SwordSlashTexture;

        //===== 运动状态 (全部本地推导, 不联网) =====
        private Vector2 springVel;          //弹簧速度
        private float angVel;               //角速度 (余摆用)
        private float impactFlash;          //冲击白闪
        private float heat;                 //刃热 0~1 (蓄势→出鞘, 驱动条带色)
        private float trailWiden = 1f;      //拖尾展宽
        private float emergeAlpha;          //入场显形
        private bool striking;              //本帧伤害窗口
        private bool launchedThisSub;       //当前子状态是否已瞬发
        private int lastSubKey = -1;        //子状态切换检测 (phase*100+sub)
        private bool shattered;             //死亡演出中已碎裂
        private float mergeVisual;          //黄泉三裂合璧强度

        //拖尾环缓冲 (零分配)
        private const int TrailLen = 26;
        private readonly Vector2[] trailPos = new Vector2[TrailLen];
        private readonly Vector2[] trailWork = new Vector2[TrailLen]; //展开缓冲 (避免每帧 new)
        private int trailHead;
        private int trailCount;

        private int counter1 = 6; //等待 Boss 同步

        public int Direction {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.NPCBestiaryDrawModifiers nPCBestiaryDrawModifiers = new();
            nPCBestiaryDrawModifiers.Hide = true;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = nPCBestiaryDrawModifiers;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            NPC.width = 76;
            NPC.height = 76;
            NPC.damage = 70;
            NPC.defense = 60;
            NPC.lifeMax = 60000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCHit1;
            NPC.value = 0f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override bool CheckActive() => false;

        //刀刃碰撞箱: 沿刃向偏移的紧凑盒 (与可见刀身对齐)
        public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox) {
            Vector2 c = NPC.Center + NPC.rotation.ToRotationVector2() * 46f * NPC.scale;
            float w = NPC.width * NPC.scale;
            npcHitbox = new Rectangle((int)(c.X - w / 2), (int)(c.Y - w / 2), (int)w, (int)w);
            return true;
        }

        //接触伤害严格对齐爆发帧: striking 由姿态推导设定, 且要求实际速度达标
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return striking && NPC.velocity.Length() > 24f;
        }

        public override void AI() {
            if (counter1-- > 0) return;

            NPC bossNPC = Main.npc[(int)NPC.ai[0]];
            if (!bossNPC.Alives() || bossNPC.ModNPC is not Yingou boss) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }
            if (shattered) {
                //碎裂后等待 Boss 收尾 (不绘制不碰撞)
                NPC.Center = bossNPC.Center;
                NPC.velocity = Vector2.Zero;
                striking = false;
                return;
            }

            Player target = Main.player[bossNPC.target];
            NPC.realLife = bossNPC.whoAmI;
            NPC.target = bossNPC.target;

            striking = false;
            heat = MathHelper.Lerp(heat, 0f, 0.08f);
            trailWiden = MathHelper.Lerp(trailWiden, 1f, 0.1f);
            mergeVisual = MathHelper.Lerp(mergeVisual, 0f, 0.06f);
            impactFlash *= 0.9f;
            if (impactFlash < 0.04f) impactFlash = 0f;

            //子状态切换检测 → 重置单发标志
            int subKey = (int)boss.Phase * 100 + boss.SubState;
            if (subKey != lastSubKey) {
                lastSubKey = subKey;
                launchedThisSub = false;
            }

            switch (boss.Phase) {
                case Yingou.BossPhase.Intro: DoIntro(boss, bossNPC); break;
                case Yingou.BossPhase.Reposition: DoGuard(boss, bossNPC, target, sheathed: true); break;
                case Yingou.BossPhase.CrossLunge: DoCrossLunge(boss, bossNPC, target); break;
                case Yingou.BossPhase.CorpseFan: DoCorpseFan(boss, bossNPC, target); break;
                case Yingou.BossPhase.IaiLine: DoIaiLine(boss, bossNPC, target); break;
                case Yingou.BossPhase.ViceClamp: DoViceClamp(boss, bossNPC, target); break;
                case Yingou.BossPhase.BladeMatrix: DoSealPose(boss, bossNPC, target, 1f); break;
                case Yingou.BossPhase.FrenzyPursuit: DoLooseFollow(boss, bossNPC, target); break;
                case Yingou.BossPhase.BladeStorm: DoSealPose(boss, bossNPC, target, 1.5f); break;
                case Yingou.BossPhase.NetherCleave: DoNetherCleave(boss, bossNPC, target); break;
                case Yingou.BossPhase.Transition2: DoTransition2(boss, bossNPC); break;
                case Yingou.BossPhase.Transition3: DoTransition3(boss, bossNPC); break;
                case Yingou.BossPhase.Death: DoDeath(boss, bossNPC); break;
                default: DoGuard(boss, bossNPC, target, sheathed: false); break;
            }

            //角速度余摆: 指数衰减 + 重力回正项 (事件之后仍在动 = 有质量)
            if (MathF.Abs(angVel) > 0.0005f) {
                NPC.rotation += angVel;
                angVel -= angVel * 0.09f + MathF.Sin(NPC.rotation) * 0.0006f;
            }

            //防离散 (距离栓绳)
            if (NPC.Center.Distance(bossNPC.Center) > 4200f)
                NPC.Center = bossNPC.Center;

            //拖尾环缓冲
            trailPos[trailHead] = NPC.Center;
            trailHead = (trailHead + 1) % TrailLen;
            if (trailCount < TrailLen) trailCount++;
        }

        #region 运动原语

        /// <summary>弹簧-阻尼追锚: 刚度=快慢, 高阻尼=稳。速度即位移, 供拖尾/碰撞使用。</summary>
        private void SpringChase(Vector2 anchor, float stiffness, float damping) {
            Vector2 next = ACMUtils.SpringDamp2D(NPC.Center, anchor, ref springVel, stiffness, damping, 1f / 60f);
            NPC.velocity = next - NPC.Center;
        }

        /// <summary>朝向平滑转动 (最短弧)。</summary>
        private void AimToward(float targetRot, float rate) {
            float diff = MathHelper.WrapAngle(targetRot - NPC.rotation);
            NPC.rotation += diff * rate;
        }

        /// <summary>单帧瞬发 (launch is a set, not a ramp)。</summary>
        private void Launch(Vector2 dir, float speed) {
            NPC.velocity = dir.SafeNormalize(Vector2.UnitX) * speed;
            springVel = NPC.velocity;
            NPC.rotation = NPC.velocity.ToRotation();
            heat = 1f;
            impactFlash = 1f;
            trailWiden = 2.2f;
            launchedThisSub = true;
            SpawnLaunchBurst();
        }

        private void SpawnLaunchBurst() {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 14; i++) {
                Vector2 vel = NPC.rotation.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(6f, 14f);
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.CorruptTorch, vel.X, vel.Y, 110, default, Main.rand.NextFloat(1.6f, 2.6f));
                d.noGravity = true;
            }
        }

        /// <summary>守位锚点: 面具两侧斜下, 轻微呼吸摆动。</summary>
        private Vector2 GuardAnchor(NPC bossNPC, bool sheathed) {
            float sway = MathF.Sin(Main.GameUpdateCount * 0.035f + Direction * 2f) * 14f;
            return sheathed
                ? bossNPC.Center + new Vector2(Direction * 128, -16 + sway * 0.5f)
                : bossNPC.Center + new Vector2(Direction * 172, 6 + sway);
        }

        #endregion

        #region 各阶段姿态

        private void DoIntro(Yingou boss, NPC bossNPC) {
            float emergeAt = Direction > 0 ? 76f : 96f;
            if (boss.PhaseTimer < emergeAt) {
                //还在裂隙深处: 隐形驻留
                NPC.Center = boss.RiftCenter;
                NPC.velocity = Vector2.Zero;
                emergeAlpha = 0f;
                return;
            }
            emergeAlpha = MathHelper.Clamp(emergeAlpha + 0.06f, 0f, 1f);
            if (!launchedThisSub) {
                //自裂隙横刺而出 (一次性瞬发)
                launchedThisSub = true;
                NPC.Center = boss.RiftCenter;
                Launch(new Vector2(Direction, 0.25f), 46f);
                boss.SpawnBlinkFlash(NPC.Center);
                striking = false;
                impactFlash = 1f;
            }
            //出鞘后弹性落位
            Vector2 anchor = bossNPC.Center + new Vector2(Direction * 240, 20);
            if (boss.PhaseTimer > 130)
                anchor = GuardAnchor(bossNPC, false);
            SpringChase(anchor, 26f, 7.5f);
            AimToward(Direction > 0 ? 0.35f : MathHelper.Pi - 0.35f, 0.12f);
        }

        private void DoGuard(Yingou boss, NPC bossNPC, Player target, bool sheathed) {
            SpringChase(GuardAnchor(bossNPC, sheathed), 34f, 9f);
            //守位朝向: 斜向外下 (归鞘时收拢贴身)
            float baseAng = sheathed
                ? (Direction > 0 ? -0.9f : MathHelper.Pi + 0.9f)
                : (Direction > 0 ? 0.5f : MathHelper.Pi - 0.5f);
            AimToward(baseAng, 0.1f);
        }

        //A1 双刃剪杀: 侧翼蓄势 → pow8 收拢 → 相隔 6f 交叉瞬发 → 硬刹
        private void DoCrossLunge(Yingou boss, NPC bossNPC, Player target) {
            Vector2 toPlayer = (target.Center - bossNPC.Center).SafeNormalize(Vector2.UnitY);
            Vector2 perp = toPlayer.RotatedBy(MathHelper.PiOver2);

            switch (boss.SubState) {
                case 0: {
                    //侧翼锚点 470px (固定起手距离阀)
                    Vector2 flank = target.Center + perp * Direction * 470f;
                    float t = MathHelper.Clamp(boss.SubTimer / Yingou.CrossWindupFrames, 0f, 1f);
                    Vector2 aim = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                    //末 12f 沿瞄准线反向收拢 — 吸气
                    Vector2 reel = -aim * MathF.Pow(t, 8) * 150f;
                    SpringChase(flank + reel, 42f, 10f);
                    AimToward(aim.ToRotation(), 0.25f);
                    heat = Math.Max(heat, t * 0.7f);
                    break;
                }
                case 1: {
                    //右刃 1f / 左刃 7f 先后瞬发 (相位错列)
                    float launchFrame = Direction > 0 ? 1f : 7f;
                    if (!launchedThisSub && boss.SubTimer >= launchFrame) {
                        Vector2 aim = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                        Launch(aim, 92f);
                    }
                    if (launchedThisSub) {
                        striking = true;
                        //行程封顶后转入滑行
                        if (boss.SubTimer > launchFrame + 13f)
                            NPC.velocity *= 0.86f;
                    }
                    else {
                        NPC.velocity *= 0.9f;
                    }
                    break;
                }
                case 2: {
                    //硬刹 + 角冲量余摆
                    if (boss.SubTimer <= 8) {
                        NPC.velocity *= 0.72f;
                        striking = NPC.velocity.Length() > 24f;
                        if (boss.SubTimer == 2)
                            angVel += Direction * 0.11f;
                    }
                    else {
                        SpringChase(GuardAnchor(bossNPC, false), 26f, 8f);
                        AimToward((target.Center - NPC.Center).ToRotation(), 0.06f);
                    }
                    break;
                }
            }
        }

        //A2 尸火喷吐: 宽守位, 每轮喷吐同步外弹 (同情反冲)
        private void DoCorpseFan(Yingou boss, NPC bossNPC, Player target) {
            Vector2 anchor = bossNPC.Center + new Vector2(Direction * 196, 4);
            SpringChase(anchor, 30f, 8.5f);
            AimToward(Direction > 0 ? -0.35f : MathHelper.Pi + 0.35f, 0.1f);
            if (boss.SubState == 1 && (boss.SubTimer == 1 || boss.SubTimer == 17 || boss.SubTimer == 33)) {
                springVel += new Vector2(Direction * 9f, -3f);
                angVel += Direction * 0.06f;
            }
        }

        //A3 居合·一文字: 打手闪至锁线位, pow8 反拉, 沿线瞬斩; 副手留守
        private void DoIaiLine(Yingou boss, NPC bossNPC, Player target) {
            bool isStriker = Direction == MathF.Sign(boss.AimAngle == 0 ? 1 : boss.AimAngle);
            if (!isStriker) {
                //副手护面 (一压迫一存在)
                SpringChase(bossNPC.Center + new Vector2(-MathF.Sign(boss.AimAngle) * 150, -30), 30f, 8.5f);
                AimToward((target.Center - NPC.Center).ToRotation(), 0.08f);
                return;
            }

            float side = MathF.Sign(boss.AimAngle);
            Vector2 linePos = boss.AimPoint;             //锁定线端点 (player 侧方 640px)
            Vector2 slashDir = new(-side, 0f);           //斩向: 穿过玩家

            switch (boss.SubState) {
                case 0:
                    //裂隙闪至线位
                    if (!launchedThisSub && boss.SubTimer >= 3) {
                        launchedThisSub = true;
                        boss.SpawnBlinkFlash(NPC.Center);
                        NPC.Center = linePos;
                        springVel = Vector2.Zero;
                        NPC.velocity = Vector2.Zero;
                        boss.SpawnBlinkFlash(NPC.Center);
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.1f, Volume = 0.7f, MaxInstances = 4 }, NPC.Center);
                    }
                    NPC.rotation = slashDir.ToRotation();
                    break;
                case 1: {
                    //全线预告 + pow8 反拉 170px (远离斩向 — 吸气)
                    float tellTime = Main.masterMode ? 34f : 42f;
                    float t = MathHelper.Clamp(boss.SubTimer / tellTime, 0f, 1f);
                    Vector2 reel = -slashDir * (MathF.Pow(t, 8) * 170f);
                    SpringChase(linePos + reel, 46f, 11f);
                    NPC.rotation = slashDir.ToRotation();
                    heat = Math.Max(heat, t * 0.8f);
                    break;
                }
                case 2:
                    //居合斩 10f
                    if (!launchedThisSub)
                        Launch(slashDir, 118f);
                    striking = true;
                    break;
                case 3:
                    //刹车 → 裂隙闪回
                    if (boss.SubTimer <= 8) {
                        NPC.velocity *= 0.7f;
                        striking = NPC.velocity.Length() > 30f;
                    }
                    else if (!launchedThisSub) {
                        launchedThisSub = true;
                        boss.SpawnBlinkFlash(NPC.Center);
                        NPC.Center = GuardAnchor(bossNPC, false);
                        springVel = Vector2.Zero;
                        NPC.velocity = Vector2.Zero;
                        boss.SpawnBlinkFlash(NPC.Center);
                    }
                    else {
                        SpringChase(GuardAnchor(bossNPC, false), 30f, 9f);
                        AimToward((target.Center - NPC.Center).ToRotation(), 0.08f);
                    }
                    break;
            }
        }

        //B1 冥狱合葬: 闪至轴两端 → 相向收势 → 相向瞬发合拢 → 穿过硬刹
        private void DoViceClamp(Yingou boss, NPC bossNPC, Player target) {
            Vector2 axis = boss.AimAngle.ToRotationVector2();
            Vector2 myEnd = boss.AimPoint + axis * Direction * 560f;
            Vector2 inward = -axis * Direction;

            switch (boss.SubState) {
                case 0: {
                    if (!launchedThisSub && boss.SubTimer >= 3) {
                        launchedThisSub = true;
                        boss.SpawnBlinkFlash(NPC.Center);
                        NPC.Center = myEnd;
                        springVel = Vector2.Zero;
                        NPC.velocity = Vector2.Zero;
                        boss.SpawnBlinkFlash(NPC.Center);
                    }
                    //缓慢内压 + 高充能震颤
                    float charge = MathHelper.Clamp(boss.SubTimer / 55f, 0f, 1f);
                    Vector2 hold = boss.AimPoint + axis * Direction * MathHelper.Lerp(560f, 522f, charge);
                    if (charge > 0.7f)
                        hold += Main.rand.NextVector2Circular(2.5f, 2.5f) * (charge - 0.7f) * 3f;
                    SpringChase(hold, 40f, 10f);
                    NPC.rotation = inward.ToRotation();
                    heat = Math.Max(heat, charge * 0.75f);
                    break;
                }
                case 1:
                    //爆前静默: 定住
                    NPC.velocity *= 0.8f;
                    NPC.rotation = inward.ToRotation();
                    break;
                case 2:
                    if (!launchedThisSub)
                        Launch(inward, 88f);
                    striking = true;
                    if (boss.SubTimer > 9)
                        NPC.velocity *= 0.8f;
                    break;
                case 3:
                    if (boss.SubTimer <= 10) {
                        NPC.velocity *= 0.78f;
                        striking = NPC.velocity.Length() > 26f;
                        if (boss.SubTimer == 3)
                            angVel += Direction * 0.13f;
                    }
                    else {
                        SpringChase(GuardAnchor(bossNPC, false), 24f, 8f);
                        AimToward((target.Center - NPC.Center).ToRotation(), 0.06f);
                    }
                    break;
            }
        }

        //结印姿态 (万刃冥阵/万刃归宗): 面具上方描小圈, 释放拍外弹反冲
        private void DoSealPose(Yingou boss, NPC bossNPC, Player target, float energy) {
            float circle = Main.GameUpdateCount * 0.06f * energy + (Direction > 0 ? 0f : MathHelper.Pi);
            Vector2 anchor = bossNPC.Center + new Vector2(Direction * 78, -168) + circle.ToRotationVector2() * 22f * energy;
            SpringChase(anchor, 36f, 9f);
            AimToward(-MathHelper.PiOver2 + Direction * 0.32f, 0.12f);
            heat = Math.Max(heat, 0.35f * energy);

            //释放拍: 外弹反冲 (与 Boss 图案节拍对齐)
            bool releaseBeat = boss.Phase == Yingou.BossPhase.BladeMatrix
                ? (boss.SubState == 1 && (int)boss.SubTimer % 85 == 1)
                : (boss.SubState == 1 && ((int)boss.SubTimer == 1 || (int)boss.SubTimer == 81 || (int)boss.SubTimer == 161));
            if (releaseBeat) {
                springVel += new Vector2(Direction * 13f, -5f);
                angVel += Direction * 0.09f;
                impactFlash = Math.Max(impactFlash, 0.7f);
            }
            //结印鬼火
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustDirect(NPC.Center + NPC.rotation.ToRotationVector2() * Main.rand.NextFloat(30, 90), 0, 0,
                    DustID.CorruptTorch, 0, 0, 130, default, 1.3f);
                d.noGravity = true;
                d.velocity = new Vector2(0, -1.5f);
            }
        }

        //B3 狂暴追猎: 松弹簧跟随 — Boss 瞬发时自然滞后, 硬刹时甩过再荡回 (次级运动)
        private void DoLooseFollow(Yingou boss, NPC bossNPC, Player target) {
            Vector2 anchor = bossNPC.Center + new Vector2(Direction * 150, -8);
            SpringChase(anchor, 16f, 5f); //低刚度低阻尼 = 明显滞后 + 甩尾
            //刃向绑速度方向 (被拖拽感)
            if (NPC.velocity.LengthSquared() > 4f)
                AimToward(NPC.velocity.ToRotation(), 0.2f);
            //Boss 冲刺窗口拖尾展宽
            if (boss.SubState == 2) {
                trailWiden = Math.Max(trailWiden, 1.8f);
                heat = Math.Max(heat, 0.8f);
            }
        }

        //C2 黄泉三裂: 合璧成巨刃 → 收势 → 同线总劈
        private void DoNetherCleave(Yingou boss, NPC bossNPC, Player target) {
            mergeVisual = MathHelper.Lerp(mergeVisual, 1f, 0.08f);
            Vector2 aimDir = boss.SubState == 0
                ? (target.Center - bossNPC.Center).SafeNormalize(Vector2.UnitX)
                : boss.AimAngle.ToRotationVector2();
            //两刃微错叠出"一柄巨刃"的厚度
            Vector2 mergeOffset = aimDir.RotatedBy(MathHelper.PiOver2) * Direction * 9f;

            switch (boss.SubState) {
                case 0: //合璧 40f
                    SpringChase(bossNPC.Center + aimDir * 128f + mergeOffset, 30f, 8f);
                    AimToward(aimDir.ToRotation(), 0.18f);
                    heat = Math.Max(heat, 0.4f);
                    break;
                case 1: { //收势 46f: pow8 反拉 200px + 收缩颤
                    float t = MathHelper.Clamp(boss.SubTimer / 46f, 0f, 1f);
                    Vector2 reel = -aimDir * MathF.Pow(t, 8) * 200f;
                    SpringChase(bossNPC.Center + aimDir * 128f + mergeOffset + reel, 44f, 10.5f);
                    NPC.rotation = aimDir.ToRotation();
                    heat = Math.Max(heat, t);
                    break;
                }
                case 2: //总劈 10f
                    if (!launchedThisSub)
                        Launch(aimDir, 105f);
                    striking = true;
                    break;
                case 3: //回收
                    if (boss.SubTimer <= 8) {
                        NPC.velocity *= 0.72f;
                        striking = NPC.velocity.Length() > 30f;
                        if (boss.SubTimer == 3)
                            angVel += Direction * 0.1f;
                    }
                    else {
                        SpringChase(bossNPC.Center + aimDir * 128f + mergeOffset, 30f, 8.5f);
                        AimToward(aimDir.ToRotation(), 0.15f);
                    }
                    break;
            }
        }

        //T2: 绕面急旋 (角速三次方升) → 100f 冲击外弹 → 弹性回位
        private void DoTransition2(Yingou boss, NPC bossNPC) {
            if (boss.PhaseTimer < 100) {
                float t = boss.PhaseTimer / 100f;
                float spin = Main.GameUpdateCount * (0.05f + t * t * t * 0.45f) + (Direction > 0 ? 0f : MathHelper.Pi);
                float radius = MathHelper.Lerp(200f, 120f, t);
                Vector2 anchor = bossNPC.Center + spin.ToRotationVector2() * radius;
                SpringChase(anchor, 60f, 12f);
                NPC.rotation = spin + MathHelper.PiOver2; //切向
                heat = Math.Max(heat, t);
                trailWiden = Math.Max(trailWiden, 1f + t);
            }
            else {
                if (!launchedThisSub && boss.PhaseTimer >= 100) {
                    launchedThisSub = true;
                    Launch(new Vector2(Direction, -0.4f), 52f);
                    striking = false; //演出不伤人
                }
                if (boss.PhaseTimer > 112)
                    SpringChase(GuardAnchor(bossNPC, false), 20f, 6.5f);
            }
        }

        //T3: 翼位震颤 → 白闪帧外张
        private void DoTransition3(Yingou boss, NPC bossNPC) {
            Vector2 wing = bossNPC.Center + new Vector2(Direction * 220, -140);
            if (boss.PhaseTimer < 120) {
                float t = boss.PhaseTimer / 120f;
                Vector2 shiver = Main.rand.NextVector2Circular(1f, 1f) * t * 3f;
                SpringChase(wing + shiver, 32f, 9f);
                AimToward(-MathHelper.PiOver2 + Direction * 0.5f, 0.1f);
                heat = Math.Max(heat, t * 0.8f);
            }
            else {
                if (!launchedThisSub) {
                    launchedThisSub = true;
                    Launch(new Vector2(Direction * 0.7f, -1f), 40f);
                    striking = false;
                }
                if (boss.PhaseTimer > 132)
                    SpringChase(GuardAnchor(bossNPC, false), 22f, 7f);
            }
        }

        //死亡: 失控漂离 + 加速自旋 → 依刻表碎裂 (右 44f / 左 70f)
        private void DoDeath(Yingou boss, NPC bossNPC) {
            float shatterFrame = Direction > 0 ? 44f : 70f;
            if (boss.PhaseTimer >= shatterFrame) {
                Shatter();
                return;
            }
            float t = boss.PhaseTimer / shatterFrame;
            Vector2 driftAnchor = bossNPC.Center + new Vector2(Direction * MathHelper.Lerp(180f, 400f, t), MathHelper.Lerp(-20f, -110f, t));
            SpringChase(driftAnchor, 14f, 4.5f);
            angVel += Direction * 0.004f; //越转越快 — 失控
            heat = Math.Max(heat, t);
        }

        private void Shatter() {
            if (shattered) return;
            shattered = true;
            striking = false;
            ACMUtils.AddScreenShake(6f);
            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
            SoundEngine.PlaySound(SoundID.Item89 with { Pitch = 0.2f, Volume = 0.8f }, NPC.Center);
            if (!VaultUtils.isServer) {
                //刃身碎片: 沿刃向抛掷 + 鬼火散逸
                Vector2 bladeDir = NPC.rotation.ToRotationVector2();
                for (int i = 0; i < 30; i++) {
                    Vector2 off = bladeDir * Main.rand.NextFloat(-40f, 110f);
                    Vector2 vel = Main.rand.NextVector2Circular(7f, 7f) + new Vector2(0, -2f);
                    Dust d = Dust.NewDustDirect(NPC.Center + off, 0, 0, Main.rand.NextBool() ? DustID.Titanium : DustID.CorruptTorch,
                        vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.4f, 2.6f));
                    d.noGravity = Main.rand.NextBool(3);
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || shattered)
                return false;

            //Intro 未出鞘: 不绘制
            if (emergeAlpha <= 0.01f && NPC.ai[0] >= 0 && Main.npc[(int)NPC.ai[0]].ModNPC is Yingou b0 && b0.Phase == Yingou.BossPhase.Intro) {
                if (b0.PhaseTimer < (Direction > 0 ? 76f : 96f))
                    return false;
            }
            float alpha = emergeAlpha > 0.01f ? emergeAlpha : 1f;

            DrawAttackTells(spriteBatch);
            DrawRibbonTrail();

            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = tex.Size() / 2;
            //刃口朝向一致性: 指向左半平面时垂直翻转
            bool flip = MathF.Cos(NPC.rotation) < 0f;
            SpriteEffects fx = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;

            //速度门控残影
            float speed = NPC.velocity.Length();
            if (speed > 26f) {
                float ghostAlpha = MathHelper.Clamp((speed - 26f) / 70f, 0f, 0.55f);
                for (int i = 1; i < NPC.oldPos.Length; i += 2) {
                    Vector2 old = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    float fade = ghostAlpha * (1f - i / (float)NPC.oldPos.Length) * alpha;
                    spriteBatch.Draw(tex, old, null, new Color(140, 230, 190, 0) * fade, NPC.oldRot[i], origin, NPC.scale, fx, 0);
                }
            }

            //本体 (冲击白闪增亮 + 热度泛红)
            Color body = Color.White;
            if (impactFlash > 0.05f)
                body = Color.Lerp(body, new Color(255, 240, 220), impactFlash * 0.8f);
            if (heat > 0.35f)
                body = Color.Lerp(body, new Color(255, 190, 170), (heat - 0.35f) * 0.55f);
            Main.EntitySpriteDraw(tex, NPC.Center - Main.screenPosition, null, body * alpha, NPC.rotation, origin, NPC.scale, fx);

            //黄泉三裂: 合璧幻影巨刃 (加性放大重影)
            if (mergeVisual > 0.05f) {
                Color phantom = new Color(255, 205, 120, 0) * (mergeVisual * 0.5f);
                Main.EntitySpriteDraw(tex, NPC.Center - Main.screenPosition, null, phantom, NPC.rotation, origin, NPC.scale * (1.45f + heat * 0.35f), fx);
            }

            return false;
        }

        //攻击预告 (由手部本地绘制: 手自己最清楚瞄准线)
        private void DrawAttackTells(SpriteBatch sb) {
            if (NPC.ai[0] < 0 || Main.npc[(int)NPC.ai[0]].ModNPC is not Yingou boss)
                return;
            Player target = Main.player[boss.NPC.target];
            if (!target.Alives())
                return;

            //A1 侧翼蓄势预告线: 手 → 穿过玩家
            if (boss.Phase == Yingou.BossPhase.CrossLunge && boss.SubState == 0 && boss.SubTimer > 4) {
                float ramp = MathHelper.Clamp(boss.SubTimer / Yingou.CrossWindupFrames, 0f, 1f);
                Vector2 aim = (target.Center + target.velocity * 10f - NPC.Center).SafeNormalize(Vector2.UnitX);
                Color core = Color.Lerp(new Color(150, 230, 205), TelegraphColors.Lethal, ramp);
                Color edge = new Color(120, 30, 40) { A = 0 };
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + aim * 1500f, MathHelper.Lerp(3f, 14f, ramp),
                    core, edge, 0.2f + ramp * 0.55f, flowSpeed: 2.8f, flowScale: 2.4f, coreSharp: 2.8f);
            }

            //A3 居合锁线: 全线预告 (只由打手绘制)
            if (boss.Phase == Yingou.BossPhase.IaiLine && boss.SubState == 1 &&
                Direction == MathF.Sign(boss.AimAngle == 0 ? 1 : boss.AimAngle)) {
                float tellTime = Main.masterMode ? 34f : 42f;
                float ramp = MathHelper.Clamp(boss.SubTimer / tellTime, 0f, 1f);
                float side = MathF.Sign(boss.AimAngle);
                Vector2 start = boss.AimPoint + new Vector2(side * 220f, 0f);
                Vector2 end = boss.AimPoint - new Vector2(side * 2200f, 0f);
                Color core = Color.Lerp(new Color(150, 230, 205), TelegraphColors.Lethal, ramp);
                Color edge = new Color(130, 25, 35) { A = 0 };
                ACMShaders.DrawBeam(start, end, MathHelper.Lerp(4f, 20f, ramp),
                    core, edge, 0.25f + ramp * 0.6f, flowSpeed: 3f, flowScale: 2f, coreSharp: 2.6f);
            }
        }

        //冥刃条带拖尾 (YingouBladeRibbon; 无着色器时退化为顶点色带)
        private void DrawRibbonTrail() {
            if (trailCount < 4)
                return;
            float speed = NPC.velocity.Length();
            float baseIntensity = MathHelper.Clamp(speed / 40f, 0f, 1f) * 0.55f + heat * 0.45f;
            if (impactFlash > 0.1f)
                baseIntensity = MathF.Max(baseIntensity, impactFlash);
            if (baseIntensity < 0.06f)
                return;

            //从环缓冲展开 (新→旧)
            int n = Math.Min(trailCount, TrailLen);
            for (int i = 0; i < n; i++) {
                int idx = (trailHead - 1 - i + TrailLen * 2) % TrailLen;
                trailWork[i] = trailPos[idx] - Main.screenPosition;
            }
            Vector2[] pts = trailWork;
            if (n < TrailLen) {
                pts = new Vector2[n];
                Array.Copy(trailWork, pts, n);
            }

            float width = (16f + 20f * heat) * trailWiden * NPC.scale;
            var verts = ACMUtils.BuildRibbonStrip(pts,
                p => width * (1f - p * 0.85f),
                p => Color.White * (1f - p),
                0f, 2);
            if (verts.Length < 4)
                return;

            Effect fx = Yingou.BladeRibbon;
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (fx != null) {
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(baseIntensity, 0f, 1f));
                fx.Parameters["uColorCore"]?.SetValue(new Vector4(0.62f, 0.95f, 0.82f, 1f));  //鬼火青
                fx.Parameters["uColorEdge"]?.SetValue(new Vector4(0.38f, 0.24f, 0.66f, 0.55f)); //幽紫
                fx.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(heat, 0f, 1f));
                fx.Parameters["uFlowSpeed"]?.SetValue(2.4f);
                fx.Parameters["uFlowScale"]?.SetValue(1.8f);
                fx.Parameters["uCoreSharp"]?.SetValue(2.6f);
                fx.Parameters["uTaper"]?.SetValue(1.6f);
                gd.Textures[0] = SwordSlashTexture?.Value ?? ACMShaders.NoiseTexture;
                gd.Textures[1] = ACMShaders.NoiseTexture;
                gd.SamplerStates[0] = SamplerState.LinearWrap;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                fx.CurrentTechnique.Passes[0].Apply();
            }
            else {
                gd.Textures[0] = SwordSlashTexture?.Value ?? TextureAssets.MagicPixel.Value;
            }
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        #endregion
    }
}

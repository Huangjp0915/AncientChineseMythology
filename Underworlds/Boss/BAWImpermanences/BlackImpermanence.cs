using AncientChineseMythology.Underworlds.Boss.BAWImpermanences.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑无常 - 锁链近战攻击风格
    /// 与白无常配合的双体Boss之一
    /// 攻击方式：锁链横扫、锁链抓取、锁链突刺、锁链牵引
    /// </summary>
    [AutoloadBossHead]
    public class BlackImpermanence : ModNPC
    {
        #region 声音资源

        private static readonly SoundStyle RoarSound = SoundID.Roar with { PitchVariance = 0.2f };
        private static readonly SoundStyle ChainSound = SoundID.Item20 with { Volume = 0.7f };
        private static readonly SoundStyle DashSound = SoundID.DD2_EtherianPortalDryadTouch with { Volume = 0.9f };
        private static readonly SoundStyle ChargeSound = SoundID.ForceRoar with { Volume = 0.8f, PitchVariance = 0.3f };

        #endregion

        #region 属性

        public Player Target => Main.player[NPC.target];
        public BAWPlayer ScreenPlayer => Target?.GetModPlayer<BAWPlayer>();

        /// <summary>白无常伙伴索引</summary>
        public int PartnerIndex { get; set; } = -1;

        /// <summary>白无常NPC引用</summary>
        public NPC Partner => PartnerIndex >= 0 && PartnerIndex < Main.npc.Length ? Main.npc[PartnerIndex] : null;

        /// <summary>是否已复活过</summary>
        private bool hasRespawned = false;

        /// <summary>绘制透明度</summary>
        private float drawAlpha = 1f;

        /// <summary>是否绘制拖尾</summary>
        private bool drawTail = false;

        /// <summary>锁链目标位置</summary>
        private Vector2 chainTargetPos;

        /// <summary>是否处于协同攻击状态</summary>
        public bool InSynergyAttack { get; set; } = false;

        #endregion

        #region 初始化

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 70;
            NPC.height = 100;
            NPC.lifeMax = 45000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.damage = 60;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 30;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(0, 8, 0, 0);
            NPC.scale = 1.5f;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.HealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DemonicAnnihilation>(), 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherworldSickle>(), 2));
        }

        public override void OnSpawn(IEntitySource source) {
            // 寻找白无常伙伴
            FindPartner();

            // 出场演出
            NPC.ai[3] = -1;
            NPC.dontTakeDamage = true;
            drawAlpha = 0f;

            base.OnSpawn(source);
        }

        /// <summary>
        /// 寻找白无常伙伴
        /// </summary>
        private void FindPartner() {
            foreach (var npc in Main.npc) {
                if (npc != null && npc.active && npc.type == ModContent.NPCType<WhiteImpermanence>()) {
                    PartnerIndex = npc.whoAmI;
                    // 同时设置白无常的伙伴为自己
                    if (npc.ModNPC is WhiteImpermanence white) {
                        white.PartnerIndex = NPC.whoAmI;
                    }
                    break;
                }
            }
        }

        #endregion

        #region AI状态重置

        public void ResetAI() {
            for (int i = 0; i <= 2; i++) {
                NPC.ai[i] = 0;
            }
        }

        private float GetAI(int index) => NPC.ai[index];

        #endregion

        #region 主AI循环

        public override bool PreAI() {
            drawTail = false;
            return base.PreAI();
        }

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            // 添加光照
            Lighting.AddLight(NPC.Center, new Color(30, 30, 40).ToVector3());

            // 目标选择
            if (Target == null || NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }

            // 确保有伙伴
            if (PartnerIndex < 0 || Partner == null || !Partner.active) {
                FindPartner();
            }

            // 特殊状态（出场/复活演出）
            if (NPC.ai[3] < 0) {
                HandleSpecialState();
                return;
            }

            // 正常战斗AI
            if (Target != null) {
                ExecuteCombatAI();
            }
            else {
                NPC.velocity *= 0.9f;
            }

            base.AI();
        }

        /// <summary>
        /// 处理特殊状态（出场/复活）
        /// </summary>
        private void HandleSpecialState() {
            var screenPlayer = Main.LocalPlayer.GetModPlayer<BAWPlayer>();

            if (NPC.ai[3] == -1) // 出场演出
            {
                screenPlayer.SetScreenPos(NPC.Center + new Vector2(0, -100));
                screenPlayer.SetZoom(1.4f);
                NPC.ai[0]++;

                if (NPC.ai[0] < 60) {
                    NPC.velocity = new Vector2(0, -2);
                }
                else {
                    NPC.velocity *= 0.9f;
                    // 黑色幽魂粒子
                    for (int i = 0; i < 5; i++) {
                        var d = Dust.NewDustDirect(NPC.position, NPC.width, 10, DustID.Shadowflame);
                        d.noGravity = true;
                        d.velocity = new Vector2(0, -15).RotatedByRandom(1);
                        d.scale = 1.5f;
                    }
                }

                if (NPC.ai[0] > 180) {
                    ResetAI();
                    NPC.ai[3] = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    screenPlayer.SetScreenShake(10, 20);
                }

                drawAlpha = MathHelper.Lerp(drawAlpha, 1, 0.05f);
            }
            else if (NPC.ai[3] == -2) // 被复活演出
            {
                if (Partner != null && Partner.active) {
                    NPC.Center = Partner.Center + new Vector2(0, -200);
                    if (Partner.ai[0] > 60)
                        drawAlpha = MathHelper.Lerp(drawAlpha, 1, 0.02f);
                    if (Partner.ai[0] > 180) {
                        ResetAI();
                        NPC.ai[3] = 0;
                        NPC.dontTakeDamage = false;
                    }
                }
            }

            NPC.rotation = NPC.rotation.AngleLerp(0, 0.08f);
        }

        /// <summary>
        /// 执行战斗AI
        /// </summary>
        private void ExecuteCombatAI() {
            bool isPhase2 = NPC.life < NPC.lifeMax * 0.5f;
            bool bothHalfHealth = Partner != null && Partner.active &&
                                  Partner.life < Partner.lifeMax * 0.5f && isPhase2;

            // 一阶段AI
            if (!isPhase2) {
                switch ((int)NPC.ai[3]) {
                    case 0:
                        AI_ChainDash(5, 150); // 锁链冲刺
                        break;
                    case 1:
                        AI_ChainGrab(); // 锁链抓取
                        break;
                    case 2:
                        AI_ChainSweep(); // 锁链横扫
                        break;
                }
            }
            // 二阶段AI
            else {
                switch ((int)NPC.ai[3]) {
                    case 0:
                        AI_ChainDash(8, 200); // 加强版锁链冲刺
                        break;
                    case 1:
                        AI_ChainGrab(); // 锁链抓取
                        break;
                    case 2:
                        AI_ChainPull(); // 锁链牵引
                        break;
                    case 3:
                        AI_SynergyAttack(); // 协同攻击（双方都半血时）
                        break;
                }

                // 双方都半血时触发协同攻击
                if (bothHalfHealth && NPC.ai[3] == 0 && NPC.ai[0] % 300 == 0) {
                    NPC.ai[3] = 3;
                    ResetAI();
                }
            }
        }

        #endregion

        #region 攻击AI

        /// <summary>
        /// 锁链冲刺攻击
        /// </summary>
        private void AI_ChainDash(float timeScale, int damage) {
            NPC.ai[0]++;
            int chainProjType = ModContent.ProjectileType<ChainProjectile>();

            if (GetAI(0) < 50) {
                // 准备阶段
                NPC.rotation = 0;
                if (GetAI(0) == 1)
                    SoundEngine.PlaySound(ChargeSound, NPC.Center);

                // 飘向玩家方向
                Vector2 targetPos = Target.Center + new Vector2(-300 * NPC.direction, -100);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 8, 0.05f);
            }
            else if (GetAI(0) < 80) {
                // 淡出准备瞬移
                drawAlpha = MathHelper.Lerp(drawAlpha, 0, 0.08f);
            }
            else if (GetAI(0) < 100) {
                // 瞬移到玩家侧面
                if (GetAI(0) == 80) {
                    NPC.direction = BAWHelper.RandInt(-1, 1, 0);
                    NPC.Center = Target.Center + new Vector2(500, 0) * -NPC.direction;
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ScreenPlayer?.SetZoom(1.6f);
                }
                drawAlpha = MathHelper.Lerp(drawAlpha, 1, 0.15f);
            }
            else if (GetAI(0) < 140) {
                // 冲刺阶段
                drawTail = true;
                if (GetAI(0) == 100) {
                    // 冲刺粒子
                    for (int i = 0; i < 8; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame);
                        d.scale = 2f;
                        d.velocity = new Vector2(BAWHelper.RandFloat(3, 8)).RotatedByRandom(MathHelper.TwoPi);
                        d.noGravity = true;
                    }
                    NPC.velocity = (Target.Center - NPC.Center).NormalizeVector() * 3;
                    ScreenPlayer?.SetScreenShake(5, 15);
                }

                // 冲刺中发射锁链
                if (GetAI(0) % 4 == 0 && NPC.velocity.Length() > 8) {
                    for (int i = 0; i < 2; i++) {
                        var vel = new Vector2(NPC.direction, -1).RotatedByRandom(0.8f);
                        var pos = Vector2.Lerp(NPC.oldPos[3] + new Vector2(35) * NPC.scale, NPC.Center, BAWHelper.RandFloat(1));
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos, vel * 3, chainProjType, damage, 1, -1, 0, 0, NPC.whoAmI);
                        p.ai[0] = -timeScale * 60;
                        p.ai[1] = BAWHelper.RandFloat(0.5f, 1f);
                        p.friendly = false;
                        p.hostile = true;
                    }
                }

                if (NPC.velocity.Length() < 25)
                    NPC.velocity *= 1.15f;
            }
            else if (GetAI(0) < 200) {
                // 减速
                if (GetAI(0) % 4 == 0 && NPC.velocity.Length() > 10) {
                    var vel = new Vector2(NPC.direction, -1).RotatedByRandom(0.8f);
                    var pos = Vector2.Lerp(NPC.oldPos[3] + new Vector2(35) * NPC.scale, NPC.Center, BAWHelper.RandFloat(1));
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos, vel * 3, chainProjType, damage, 1);
                    p.ai[0] = -timeScale * 60;
                    p.friendly = false;
                    p.hostile = true;
                }
                NPC.velocity *= 0.95f;
                if (GetAI(0) == 199)
                    NPC.direction *= -1;
            }
            else {
                ResetAI();
                NPC.ai[3] = (NPC.ai[3] + 1) % 3;
            }

            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.06f, -0.5f, 0.5f), 0.07f);
        }

        /// <summary>
        /// 锁链抓取攻击
        /// </summary>
        private void AI_ChainGrab() {
            NPC.ai[0]++;
            var dis = Vector2.Distance(NPC.Center, Target.Center);
            int chainCount = NPC.life < NPC.lifeMax * 0.5f ? 5 : 3;
            int forStep = (chainCount - 1) / 2;

            if (GetAI(0) < 70) {
                // 移动到攻击位置
                Vector2 targetPos = Target.Center + new Vector2(-400 * NPC.direction, -80);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 10 * Math.Clamp(dis * 0.03f, 0, 1), 0.07f);

                if (GetAI(0) == 1)
                    SoundEngine.PlaySound(ChargeSound, NPC.Center);
            }
            else {
                NPC.velocity *= 0f;
            }

            // 预判线显示
            if (GetAI(0) < 60) {
                chainTargetPos = (Target.Center - NPC.Center).NormalizeVector() * 60;

                for (int j = -forStep; j <= forStep; j++) {
                    for (float i = 0; i < GetAI(0); i++) {
                        var v = i / GetAI(0);
                        var pos = NPC.Center + chainTargetPos.NormalizeVector().RotatedBy(j * 0.4f) * v *
                                  Math.Min(Math.Abs(j) * 1500 + (Target.Center - NPC.Center).Length(), 1500);
                        Dust.NewDustPerfect(pos, DustID.Shadowflame).noGravity = true;
                    }
                }
            }

            // 发射锁链
            if (GetAI(0) == 70) {
                var v = chainTargetPos;
                for (int i = -forStep; i <= forStep; i++) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center,
                        v.RotatedBy(i * 0.4f), ModContent.ProjectileType<ChainProjectile>(), 80, 0);
                    p.ai[2] = NPC.whoAmI;
                }
                SoundEngine.PlaySound(ChainSound, NPC.Center);
                ScreenPlayer?.SetScreenShake(6, 12);
            }

            if (GetAI(0) > 130) {
                ResetAI();
                NPC.ai[3] = (NPC.ai[3] + 1) % 3;
            }
        }

        /// <summary>
        /// 锁链横扫攻击
        /// </summary>
        private void AI_ChainSweep() {
            NPC.ai[0]++;
            int sweepProjType = ModContent.ProjectileType<ChainSweepProjectile>();

            if (GetAI(0) < 60) {
                // 移动到玩家上方
                Vector2 targetPos = Target.Center + new Vector2(0, -300);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 12, 0.06f);
            }
            else if (GetAI(0) == 60) {
                // 发射横扫锁链
                SoundEngine.PlaySound(ChainSound, NPC.Center);
                ScreenPlayer?.SetScreenShake(8, 15);

                // 左右两道横扫
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(-15, 5), sweepProjType, 100, 2, -1, NPC.whoAmI);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(15, 5), sweepProjType, 100, 2, -1, NPC.whoAmI);
            }
            else if (GetAI(0) < 120) {
                NPC.velocity *= 0.95f;
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
            }
        }

        /// <summary>
        /// 锁链牵引攻击（二阶段）
        /// </summary>
        private void AI_ChainPull() {
            NPC.ai[0]++;

            if (GetAI(0) < 40) {
                // 准备
                NPC.velocity *= 0.9f;
                if (GetAI(0) == 1)
                    SoundEngine.PlaySound(ChargeSound, NPC.Center);
            }
            else if (GetAI(0) == 40) {
                // 发射牵引锁链
                var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center,
                    (Target.Center - NPC.Center).NormalizeVector() * 20,
                    ModContent.ProjectileType<ChainPullProjectile>(), 60, 0);
                p.ai[0] = NPC.whoAmI;
                SoundEngine.PlaySound(ChainSound, NPC.Center);
            }
            else if (GetAI(0) < 150) {
                // 等待锁链效果
                NPC.velocity *= 0.95f;
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
            }
        }

        /// <summary>
        /// 协同攻击（与白无常配合）
        /// </summary>
        private void AI_SynergyAttack() {
            NPC.ai[0]++;
            InSynergyAttack = true;

            if (NPC.ai[0] < 60) {
                // 蓄力
                NPC.velocity *= 0.9f;
                if (NPC.ai[0] == 1) {
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ScreenPlayer?.SetZoom(2f);
                }

                // 蓄力粒子
                if (NPC.ai[0] % 5 == 0) {
                    var d = Dust.NewDustPerfect(NPC.Center + new Vector2(100).RotatedByRandom(MathHelper.TwoPi), DustID.Shadowflame);
                    d.scale = 2f;
                    d.velocity = (Target.Center - d.position).NormalizeVector() * 5;
                    d.noGravity = true;
                }
            }
            else if (NPC.ai[0] < 180) {
                // 多段冲刺
                drawTail = true;
                if (NPC.ai[0] % 15 == 0) {
                    SoundEngine.PlaySound(DashSound, NPC.Center);
                    ScreenPlayer?.SetScreenShake(4, 8);
                    NPC.direction = Target.Center.X > NPC.Center.X ? 1 : -1;
                    NPC.velocity = (Target.Center + new Vector2(BAWHelper.RandFloat(-100, 100), BAWHelper.RandFloat(-50, 50)) - NPC.Center).NormalizeVector() * 28;

                    // 冲刺粒子
                    for (int i = 0; i < 6; i++) {
                        var dust = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame);
                        dust.scale = 2f;
                        dust.velocity = new Vector2(BAWHelper.RandFloat(3, 8)).RotatedByRandom(MathHelper.TwoPi);
                        dust.noGravity = true;
                    }
                }
                NPC.velocity *= 1.01f;
            }
            else if (NPC.ai[0] < 220) {
                // 发射协同锁链
                NPC.velocity *= 0.92f;
                if (NPC.ai[0] == 190) {
                    // 发射灵魂锁链（连接黑白无常）
                    if (Partner != null && Partner.active) {
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center,
                            (Partner.Center - NPC.Center).NormalizeVector() * 15,
                            ModContent.ProjectileType<SoulChainProjectile>(), 120, 0);
                        p.ai[0] = NPC.whoAmI;
                        p.ai[1] = PartnerIndex;
                        SoundEngine.PlaySound(ChainSound, NPC.Center);
                    }
                }
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
                InSynergyAttack = false;
                ScreenPlayer?.SetZoom(1.2f);
            }

            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * 0.08f, -0.6f, 0.6f), 0.1f);
        }

        #endregion

        #region 死亡与复活

        public override bool CheckDead() {
            // 如果白无常还有较多生命值，则复活
            if (!hasRespawned && Partner != null && Partner.active && Partner.life > Partner.lifeMax * 0.3f) {
                hasRespawned = true;
                drawAlpha = 0;
                NPC.dontTakeDamage = true;
                NPC.ai[3] = -2;
                NPC.velocity *= 0;

                // 触发白无常的复活演出
                Partner.dontTakeDamage = true;
                Partner.velocity *= 0;
                Partner.ai[3] = -1;
                ResetAI();

                if (Partner.ModNPC is WhiteImpermanence white) {
                    white.ResetAI();
                }

                NPC.life = (int)(NPC.lifeMax * 0.4f);
                SoundEngine.PlaySound(RoarSound, NPC.Center);
                return false;
            }
            return base.CheckDead();
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            var tex = TextureAssets.Npc[Type].Value;
            var rec = NPC.frame;
            var spe = NPC.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 绘制拖尾
            if (drawTail) {
                var tailCol = Color.DarkSlateGray * 0.5f;
                tailCol.A = 0;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    var scale = NPC.scale * (1f - i / (float)NPC.oldPos.Length * 0.3f);
                    sb.Draw(tex, NPC.oldPos[i] + rec.Size() * 0.5f * NPC.scale - scrPos, rec,
                        tailCol * drawAlpha, NPC.rotation, rec.Size() * 0.5f, scale, spe, 0);
                }
            }

            // 绘制主体
            sb.Draw(tex, NPC.Center - scrPos, rec, col * drawAlpha, NPC.rotation, rec.Size() * 0.5f, NPC.scale, spe, 0);

            // 外发光
            var glowCol = new Color(30, 30, 50);
            glowCol.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glowCol * 0.4f * drawAlpha, NPC.rotation,
                rec.Size() * 0.5f, NPC.scale * 1.08f, spe, 0);

            return false;
        }

        #endregion
    }
}

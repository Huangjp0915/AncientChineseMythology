using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 白无常 - 幽灵法术远程攻击风格
    /// 与黑无常配合的双体Boss之一
    /// 攻击方式：幽魂弹幕、冥界法阵、灵魂吸取、幽灵分身
    /// </summary>
    [AutoloadBossHead]
    public class WhiteImpermanence : ModNPC
    {
        #region 声音资源

        private static readonly SoundStyle GhostSound = SoundID.Item8 with { PitchVariance = 0.3f };
        private static readonly SoundStyle SpellSound = SoundID.Item73 with { Volume = 0.8f };
        private static readonly SoundStyle SoulPullSound = SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f };
        private static readonly SoundStyle RoarSound = SoundID.Roar with { PitchVariance = 0.2f };

        #endregion

        #region 属性

        public Player Target => Main.player[NPC.target];
        public BAWPlayer ScreenPlayer => Target?.GetModPlayer<BAWPlayer>();

        /// <summary>黑无常伙伴索引</summary>
        public int PartnerIndex { get; set; } = -1;

        /// <summary>黑无常NPC引用</summary>
        public NPC Partner => PartnerIndex >= 0 && PartnerIndex < Main.npc.Length ? Main.npc[PartnerIndex] : null;

        /// <summary>是否已复活过</summary>
        private bool hasRespawned = false;

        /// <summary>绘制透明度</summary>
        private float drawAlpha = 1f;

        /// <summary>是否绘制拖尾</summary>
        private bool drawTail = false;

        /// <summary>移动目标位置</summary>
        private Vector2 moveTargetPos;

        /// <summary>是否正在移动</summary>
        private bool isMoving = false;

        /// <summary>减速领域半径</summary>
        private float slowFieldRadius = 0f;

        /// <summary>减速领域计时器</summary>
        private float slowFieldTimer = 0f;

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
            NPC.lifeMax = 40000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.damage = 50;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 25;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = Item.buyPrice(0, 8, 0, 0);
            NPC.scale = 1.5f;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
        }

        public override void OnSpawn(IEntitySource source) {
            // 寻找黑无常伙伴
            FindPartner();

            // 出场演出
            NPC.ai[3] = -1;
            NPC.dontTakeDamage = true;
            drawAlpha = 0f;
            moveTargetPos = NPC.Center;

            base.OnSpawn(source);
        }

        /// <summary>
        /// 寻找黑无常伙伴
        /// </summary>
        private void FindPartner() {
            foreach (var npc in Main.npc) {
                if (npc != null && npc.active && npc.type == ModContent.NPCType<BlackImpermanence>()) {
                    PartnerIndex = npc.whoAmI;
                    // 同时设置黑无常的伙伴为自己
                    if (npc.ModNPC is BlackImpermanence black) {
                        black.PartnerIndex = NPC.whoAmI;
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
            // 添加光照（白色幽光）
            Lighting.AddLight(NPC.Center, new Color(200, 200, 255).ToVector3() * 0.5f);

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
                HandleMovement();
                HandleSlowField();
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

            if (NPC.ai[3] == -1) // 出场/复活演出
            {
                screenPlayer.SetScreenPos(NPC.Center + new Vector2(0, -100));
                screenPlayer.SetZoom(1.4f);
                NPC.ai[0]++;

                if (NPC.ai[0] < 60) {
                    NPC.velocity = new Vector2(0, -2);
                }
                else {
                    NPC.velocity *= 0.9f;
                    // 白色幽魂粒子
                    for (int i = 0; i < 5; i++) {
                        var d = Dust.NewDustDirect(NPC.position, NPC.width, 10, DustID.SpectreStaff);
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
        /// 处理移动逻辑
        /// </summary>
        private void HandleMovement() {
            // 保持与玩家的距离
            if (isMoving) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, (moveTargetPos - NPC.Center).NormalizeVector() * 40, 0.03f);
                if (NPC.Distance(moveTargetPos) < 80)
                    isMoving = false;
            }
            else {
                // 远离太近的玩家
                if (Target.Distance(NPC.Center) > 800) {
                    moveTargetPos = Target.Center + new Vector2(0, -BAWHelper.RandFloat(350, 500)).RotatedByRandom(0.8f);
                    moveTargetPos.X += Target.velocity.X * 40;
                    isMoving = true;
                }
            }

            // 缓慢飘向目标位置
            var dis = (NPC.Center - moveTargetPos).Length();
            NPC.velocity = Vector2.Lerp(NPC.velocity, (moveTargetPos - NPC.Center).NormalizeVector() * 10 * Math.Clamp(dis * 0.03f, 0, 1), 0.05f);
            NPC.direction = NPC.velocity.X > 0 ? 1 : -1;
        }

        /// <summary>
        /// 处理减速领域
        /// </summary>
        private void HandleSlowField() {
            bool isPhase2 = NPC.life < NPC.lifeMax * 0.5f;
            float targetRadius = isPhase2 ? 500f : 400f;
            float fieldDuration = isPhase2 ? -1 : 15 * 60; // 二阶段永久开启

            slowFieldTimer++;

            if (fieldDuration > 0) {
                if (slowFieldTimer > fieldDuration && slowFieldTimer < fieldDuration + 20 * 60) {
                    slowFieldRadius = MathHelper.Lerp(slowFieldRadius, targetRadius, 0.05f);
                }
                else if (slowFieldTimer > fieldDuration + 20 * 60) {
                    slowFieldRadius = MathHelper.Lerp(slowFieldRadius, 0, 0.05f);
                    if (slowFieldRadius < 10)
                        slowFieldTimer = 0;
                }
            }
            else {
                slowFieldRadius = MathHelper.Lerp(slowFieldRadius, targetRadius, 0.03f);
            }

            // 应用减速效果
            if (slowFieldRadius > 50) {
                foreach (var p in Main.player) {
                    if (p != null && p.active && !p.dead) {
                        if (p.Distance(NPC.Center) < slowFieldRadius) {
                            var bawPlayer = p.GetModPlayer<BAWPlayer>();
                            if (isPhase2) {
                                bawPlayer.ApplyYinQiCorrosion(10);
                                // 二阶段脉冲效果
                                if (slowFieldRadius > targetRadius * 0.6f && NPC.ai[0] % 40 == 0)
                                    SoundEngine.PlaySound(SoulPullSound, NPC.Center);
                            }
                            else {
                                bawPlayer.ApplyYinQiCorrosion(6);
                            }
                        }
                    }
                }

                // 绘制领域粒子
                if (Main.netMode != NetmodeID.Server) {
                    for (float j = 0; j < 1; j += 0.25f) {
                        for (float i = 0; i < 1; i += 0.2f) {
                            var dust = Dust.NewDustPerfect(
                                NPC.Center + new Vector2(0, slowFieldRadius).RotatedBy(j * MathHelper.TwoPi + i * 0.2f + Main.timeForVisualEffects * 0.08f),
                                DustID.SpectreStaff);
                            dust.noGravity = true;
                            dust.scale = 0.8f;
                        }
                    }
                }
            }
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
                        AI_GhostBarrage(1, 250, 4); // 幽魂弹幕
                        break;
                    case 1:
                        AI_SpiritCircle(); // 幽灵法阵
                        break;
                    case 2:
                        AI_GhostWave(); // 幽魂波
                        break;
                }
            }
            // 二阶段AI
            else {
                switch ((int)NPC.ai[3]) {
                    case 0:
                        AI_GhostBarrage(2, 300, 6); // 加强版幽魂弹幕
                        break;
                    case 1:
                        AI_SpiritCircle(); // 幽灵法阵
                        break;
                    case 2:
                        AI_SoulDrain(); // 灵魂吸取
                        break;
                    case 3:
                        AI_SynergyAttack(); // 协同攻击
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
        /// 幽魂弹幕攻击
        /// </summary>
        private void AI_GhostBarrage(int waves, int damage, int projectileCount) {
            NPC.ai[0]++;
            int ghostProjType = ModContent.ProjectileType<GhostProjectile>();

            if (NPC.ai[1] < waves) {
                if (NPC.ai[0] > 50) {
                    if (NPC.ai[0] % 50 == 0) {
                        NPC.ai[1]++;
                        // 发射多个幽魂弹幕
                        for (int i = 0; i < projectileCount; i++) {
                            var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center,
                                (Target.Center - NPC.Center).NormalizeVector().RotatedByRandom(1.5f) * 6,
                                ghostProjType, damage, 2);
                            p.ai[2] = NPC.whoAmI;
                        }
                        SoundEngine.PlaySound(SpellSound, NPC.Center);
                    }
                }
            }
            else {
                NPC.ai[1]++;
                if (NPC.ai[1] > 180) {
                    ResetAI();
                    NPC.ai[3] = (NPC.ai[3] + 1) % 3;
                }
            }
        }

        /// <summary>
        /// 幽灵法阵攻击
        /// </summary>
        private void AI_SpiritCircle() {
            NPC.ai[0]++;
            int circleProjType = ModContent.ProjectileType<SpiritCircleProjectile>();

            if (GetAI(0) < 60) {
                // 蓄力
                NPC.velocity *= 0.95f;

                // 蓄力粒子
                if (GetAI(0) % 3 == 0) {
                    for (int i = 0; i < 3; i++) {
                        var pos = NPC.Center + new Vector2(120).RotatedByRandom(MathHelper.TwoPi);
                        var d = Dust.NewDustPerfect(pos, DustID.SpectreStaff);
                        d.velocity = (NPC.Center - pos).NormalizeVector() * 6;
                        d.scale = 1.5f;
                        d.noGravity = true;
                    }
                }
            }
            else if (GetAI(0) == 60) {
                // 在玩家周围生成法阵
                SoundEngine.PlaySound(GhostSound, NPC.Center);
                ScreenPlayer?.SetScreenShake(6, 15);

                // 创建环绕玩家的法阵
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8;
                    Vector2 pos = Target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 300;
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos,
                        (Target.Center - pos).NormalizeVector() * 8, circleProjType, 200, 0);
                    p.ai[0] = angle;
                    p.ai[1] = NPC.whoAmI;
                }
            }
            else if (GetAI(0) < 180) {
                // 等待
                drawTail = true;
            }
            else {
                ResetAI();
                NPC.ai[3] = (NPC.ai[3] + 1) % 3;
            }
        }

        /// <summary>
        /// 幽魂波攻击
        /// </summary>
        private void AI_GhostWave() {
            NPC.ai[0]++;
            int waveProjType = ModContent.ProjectileType<GhostWaveProjectile>();

            if (GetAI(0) < 40) {
                // 准备
                Vector2 targetPos = Target.Center + new Vector2(0, -400);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 15, 0.08f);
            }
            else if (GetAI(0) == 40) {
                // 发射幽魂波
                SoundEngine.PlaySound(SpellSound, NPC.Center);
                ScreenPlayer?.SetScreenShake(8, 12);

                // 扇形发射
                for (int i = -3; i <= 3; i++) {
                    Vector2 vel = new Vector2(0, 12).RotatedBy(i * 0.15f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, vel, waveProjType, 180, 2);
                }
            }
            else if (GetAI(0) < 100) {
                NPC.velocity *= 0.95f;
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
            }
        }

        /// <summary>
        /// 灵魂吸取攻击（二阶段）
        /// </summary>
        private void AI_SoulDrain() {
            NPC.ai[0]++;
            int drainProjType = ModContent.ProjectileType<SoulDrainProjectile>();

            if (GetAI(0) < 60) {
                // 移动到玩家附近
                Vector2 targetPos = Target.Center + new Vector2(BAWHelper.RandFloat(-200, 200), -300);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 12, 0.06f);

                if (GetAI(0) == 1)
                    SoundEngine.PlaySound(SoulPullSound, NPC.Center);
            }
            else if (GetAI(0) == 60) {
                // 发射灵魂吸取射线
                var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center,
                    (Target.Center - NPC.Center).NormalizeVector() * 15, drainProjType, 150, 0);
                p.ai[0] = NPC.whoAmI;
                SoundEngine.PlaySound(SpellSound, NPC.Center);
                ScreenPlayer?.SetScreenShake(10, 20);
            }
            else if (GetAI(0) < 180) {
                // 吸取期间减速
                NPC.velocity *= 0.9f;

                // 吸取粒子效果
                if (GetAI(0) % 5 == 0) {
                    var pos = Target.Center + new Vector2(50).RotatedByRandom(MathHelper.TwoPi);
                    var d = Dust.NewDustPerfect(pos, DustID.SpectreStaff);
                    d.velocity = (NPC.Center - pos).NormalizeVector() * 10;
                    d.scale = 1.5f;
                    d.noGravity = true;
                }
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
            }
        }

        /// <summary>
        /// 协同攻击（与黑无常配合）
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
                    var d = Dust.NewDustPerfect(NPC.Center + new Vector2(100).RotatedByRandom(MathHelper.TwoPi), DustID.SpectreStaff);
                    d.scale = 2f;
                    d.velocity = (Target.Center - d.position).NormalizeVector() * 5;
                    d.noGravity = true;
                }
            }
            else if (NPC.ai[0] < 100) {
                // 移动到黑无常对面
                if (Partner != null && Partner.active) {
                    Vector2 midPoint = (NPC.Center + Partner.Center) / 2;
                    Vector2 targetPos = midPoint + (NPC.Center - midPoint).NormalizeVector() * 400;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center).NormalizeVector() * 20, 0.1f);
                }
            }
            else if (NPC.ai[0] < 180) {
                // 持续发射弹幕
                drawTail = true;
                if (NPC.ai[0] % 20 == 0) {
                    SoundEngine.PlaySound(SpellSound, NPC.Center);
                    // 发射追踪弹幕
                    for (int i = -2; i <= 2; i++) {
                        var vel = (Target.Center - NPC.Center).NormalizeVector().RotatedBy(i * 0.2f) * 8f;
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, vel,
                            ModContent.ProjectileType<GhostProjectile>(), 250, 2);
                        p.ai[2] = NPC.whoAmI;
                    }
                }
            }
            else if (NPC.ai[0] < 220) {
                NPC.velocity *= 0.92f;
            }
            else {
                ResetAI();
                NPC.ai[3] = 0;
                InSynergyAttack = false;
                ScreenPlayer?.SetZoom(1.2f);
            }
        }

        #endregion

        #region 死亡与复活

        public override bool CheckDead() {
            // 如果黑无常还有较多生命值，则复活
            if (!hasRespawned && Partner != null && Partner.active && Partner.life > Partner.lifeMax * 0.3f) {
                hasRespawned = true;
                drawAlpha = 0;
                NPC.dontTakeDamage = true;
                NPC.ai[3] = -2;
                NPC.velocity *= 0;

                // 触发黑无常的复活演出
                Partner.dontTakeDamage = true;
                Partner.velocity *= 0;
                Partner.ai[3] = -1;
                ResetAI();

                if (Partner.ModNPC is BlackImpermanence black) {
                    black.ResetAI();
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
                var tailCol = Color.LightCyan * 0.5f;
                tailCol.A = 0;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    var scale = NPC.scale * (1f - i / (float)NPC.oldPos.Length * 0.3f);
                    sb.Draw(tex, NPC.oldPos[i] + rec.Size() * 0.5f * NPC.scale - scrPos, rec,
                        tailCol * drawAlpha, NPC.rotation, rec.Size() * 0.5f, scale, spe, 0);
                }
            }

            // 绘制主体
            sb.Draw(tex, NPC.Center - scrPos, rec, col * drawAlpha, NPC.rotation, rec.Size() * 0.5f, NPC.scale, spe, 0);

            // 外发光（幽灵般的白光）
            var glowCol = new Color(200, 200, 255);
            glowCol.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glowCol * 0.4f * drawAlpha, NPC.rotation,
                rec.Size() * 0.5f, NPC.scale * 1.1f, spe, 0);

            // 绘制减速领域指示
            if (slowFieldRadius > 50) {
                // 简单的圆形指示
                for (int i = 0; i < 36; i++) {
                    float angle = MathHelper.TwoPi * i / 36;
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * slowFieldRadius;
                    var d = Dust.NewDustPerfect(pos, DustID.SpectreStaff);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                    d.scale = 0.5f;
                }
            }

            return false;
        }

        #endregion
    }
}

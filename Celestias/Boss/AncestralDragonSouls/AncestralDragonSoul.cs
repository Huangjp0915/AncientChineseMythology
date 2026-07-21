using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂基类 — 「星海归墟」虚实对比核心:
    /// 龙身由星尘聚形 (AncestralSoulBody 着色器: 溶解显形/星散 + 幽魂虚化 + 体内流光),
    /// **透明度即威胁读法**: 虚化=无接触伤害可穿行, 凝实=猎杀线。
    /// 整龙由头部统一合批绘制 (段节 PreDraw 让渡), 段节只保留逻辑与备用自绘。
    /// </summary>
    public abstract class AncestralDragonSoul : BasicWorm
    {
        public override bool IsUseSpriteDirection => true;

        /// <summary>获取当前目标玩家</summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>全局时间计数器</summary>
        protected float globalTime;

        /// <summary>雾气透明度</summary>
        protected float mistAlpha = 0.6f;

        /// <summary>灵魂脉动相位</summary>
        protected float soulPulsePhase;

        /// <summary>体节索引,用于蛇形波计算。头部为0,尾部为SummonMax</summary>
        public int segmentIndex = 0;

        /// <summary>是否为分裂出的副本龙</summary>
        public bool IsTwin;

        // ===== 虚实对比 视觉标量 (各端本地由同步状态确定性推导, 不入包) =====

        /// <summary>幽魂虚化程度 0=凝实 1=全虚化。头部每帧计算, 段节从宿主头复制。</summary>
        public float GhostLevel;

        /// <summary>出生显形溶解 1→0 (逐节 36 帧, 长龙天然形成"从头到尾编织成形")。</summary>
        protected float spawnDissolve = 1f;

        /// <summary>死亡星散溶解 0→1 (由宿主头 Death 状态驱动, 尾梢先散)。</summary>
        protected float deathDissolve;

        /// <summary>合体太初金染 0→1。</summary>
        protected float mergeGold;

        /// <summary>供着色器使用的最终溶解值。</summary>
        public float DissolveLevel => MathF.Max(spawnDissolve, deathDissolve);

        /// <summary>取本段所属的龙头 ModNPC (头部返回自身), 失效返回 null。</summary>
        public AncestralDragonSoulHead OwnerHead {
            get {
                if (this is AncestralDragonSoulHead selfHead)
                    return selfHead;
                if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                    NPC owner = Main.npc[NPC.realLife];
                    if (owner.active && owner.ModNPC is AncestralDragonSoulHead h)
                        return h;
                }
                return null;
            }
        }

        /// <summary>
        /// 双魂回拢「合体」后的视觉放大倍率 (纯绘制, 不改接触判定箱; 由所属龙头同步的 Merged 标志驱动,
        /// 全客户端一致)。让合体后的"太初真身"显得更巨大, 而不引入失衡的判定箱变化。
        /// </summary>
        protected float MergeScaleMul() {
            AncestralDragonSoulHead h = OwnerHead;
            return h != null && h.Merged ? 1.2f : 1f;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 80;
            NPC.height = 80;
            NPC.lifeMax = 8000000; // 超级Boss血量
            NPC.damage = 320;
            NPC.defense = 120;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;
            SummonMax = 80; // 超长身体

            // 难度调整
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.4f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation + MathHelper.PiOver2 * NPC.spriteDirection;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (NPCWormType != WormType.Head) {
                return false;
            }
            return null;
        }

        /// <summary>
        /// 接触伤害窗口 = 宿主头的凝实状态 (虚化的幽魂可以穿过玩家, 与透明度读法严格对齐)。
        /// </summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            AncestralDragonSoulHead h = OwnerHead;
            return h == null || h.ContactDamageActive;
        }

        public override void AI() {
            base.AI();

            globalTime += 1f / 60f;
            soulPulsePhase += 0.08f;

            // 出生显形: 36 帧从星尘编织成形
            if (spawnDissolve > 0f)
                spawnDissolve = MathF.Max(0f, spawnDissolve - 1f / 36f);

            // 如果跟随父级，更新连接
            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;
            }

            // 段节视觉标量从宿主头复制 (各端本地确定性推导)
            AncestralDragonSoulHead head = OwnerHead;
            if (head != null && !ReferenceEquals(head, this)) {
                GhostLevel = head.GhostLevel;
                mergeGold = MathHelper.Lerp(mergeGold, head.Merged ? 1f : 0f, 0.04f);
                deathDissolve = head.DeathDissolveFor(segmentIndex);
            }

            // 身体段连接粒子效果
            if (FatherNPC.Alives()) {
                SpawnConnectionParticles();
            }

            // 龙魂发光效果: 越虚化越暗淡 (读法一致)
            float pulseIntensity = (0.6f + MathF.Sin(soulPulsePhase) * 0.2f) * (1f - GhostLevel * 0.55f) * (1f - deathDissolve);
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.95f, 1f) * pulseIntensity);
        }

        /// <summary>
        /// 蛇形鞭梢运动:父节点锚点+垂直正弦摆动+速度传递+拖尾延迟
        /// 相比原版的硬Lerp,这种算法让身体拥有更强的惯性感和鞭打张力,体现超级Boss的压迫感
        /// </summary>
        public override void ChangePos() {
            if (FatherNPC == null || !FatherNPC.active) {
                return;
            }

            Vector2 toParent = FatherNPC.Center - NPC.Center;
            float targetDist = (FatherNPC.width + NPC.width) / 2f;
            Vector2 dirToParent = toParent.SafeNormalize(Vector2.UnitY);

            // 锚点:父节点后方固定距离
            Vector2 anchor = FatherNPC.Center - dirToParent * targetDist;

            // 蛇形波:沿身体传导,越靠近尾部幅度越大; 虚化时波幅放大 (幽魂更飘忽)
            float segPhase = globalTime * 5.2f - segmentIndex * 0.42f;
            float parentSpeed = FatherNPC.velocity.Length();
            float speedFactor = MathHelper.Clamp(parentSpeed / 18f, 0.35f, 1.5f);
            float segFactor = MathHelper.Clamp(segmentIndex / 30f, 0.4f, 1.3f);
            float waveAmp = 15f * speedFactor * segFactor * (1f + GhostLevel * 0.8f);
            Vector2 perp = dirToParent.RotatedBy(MathHelper.PiOver2);
            anchor += perp * MathF.Sin(segPhase) * waveAmp;

            // 拖尾式插值,保留惯性
            Vector2 newCenter = Vector2.Lerp(NPC.Center, anchor, 0.4f);
            Vector2 delta = anchor - newCenter;

            // 继承父节点速度让整条龙体甩动更富张力
            NPC.velocity = delta + FatherNPC.velocity * 0.18f;

            // 限速保护
            const float maxSpeed = 55f;
            if (NPC.velocity.LengthSquared() > maxSpeed * maxSpeed) {
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            NPC.Center = new Vector2((int)newCenter.X, (int)newCenter.Y);
        }

        /// <summary>生成身体段之间的连接粒子</summary>
        protected virtual void SpawnConnectionParticles() {
            if (Main.netMode == NetmodeID.Server) return;
            // 隔节生成, 双子期两条长龙也不至于 dust 爆表
            if ((segmentIndex & 1) == 1) return;

            Vector2 midPoint = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;

            // 白色仙气粒子
            for (int i = 0; i < (int)(NPC.velocity.Length() / 3); i++) {
                if (Main.rand.NextBool(3)) {
                    int dustType = Main.rand.NextBool(3) ? DustID.Cloud : DustID.WhiteTorch;
                    int dust = Dust.NewDust(midPoint + Main.rand.NextVector2Circular(15, 15), 1, 1, dustType, 0, 0, 200, new Color(240, 245, 255), 1.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = NPC.velocity.RotatedByRandom(0.4f) * 0.3f;
                    Main.dust[dust].fadeIn = 1.2f;
                }
            }

            // 虚化期: 星尘从体侧剥离 (视觉=正在化作星屑)
            if (GhostLevel > 0.4f && Main.rand.NextBool(6)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                int dust = Dust.NewDust(dustPos, 1, 1, DustID.Clentaminator_Cyan, 0, 0, 150, Color.White, 0.9f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = new Vector2(0, -Main.rand.NextFloat(0.5f, 1.6f));
                Main.dust[dust].alpha = 180;
            }
        }

        /// <summary>贴图轴向补正 (尾部贴图纵向, 绘制时需 +PiOver2); 头部合批与备用自绘共用。</summary>
        internal virtual float DrawRotationOffset => 0f;

        /// <summary>宿主头存活时段节绘制全部让渡给头部合批; 否则回退自绘。</summary>
        protected bool HeadHandlesDrawing {
            get {
                if (NPC.IsABestiaryIconDummy)
                    return false;
                AncestralDragonSoulHead h = OwnerHead;
                return h != null && !ReferenceEquals(h, this) && h.NPC.active;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadHandlesDrawing)
                return false;

            // 备用自绘 (宿主头缺失 / 图鉴)
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float soulPulse = 1f + MathF.Sin(soulPulsePhase + NPC.whoAmI * 0.3f) * 0.08f;

            Color mistColor = Color.Lerp(drawColor, new Color(230, 240, 255), 0.5f);
            mistColor = Color.Lerp(mistColor, Color.White, 0.3f);

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation + DrawRotationOffset, origin, NPC.scale * soulPulse, effects, 0f);

            Color innerGlow = new Color(255, 255, 255) * 0.3f * soulPulse;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation + DrawRotationOffset, origin, NPC.scale * 0.9f, effects, 0f);

            return false;
        }
    }
}

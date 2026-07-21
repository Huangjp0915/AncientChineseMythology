using AncientChineseMythology.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 基础类
    /// 终局Boss，是幽冥龙的觉醒形态。
    /// V3: 体节持有脊波弹簧链（头部注入冲量 → 波沿 44 节躯体传播），
    /// 绘制/接触伤害/可见度由头部演出状态统一驱动；体节粒子按速度门控。
    /// </summary>
    public abstract class AwakeningNether : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/AwakeningNethers/" + Name;

        /// <summary>
        /// 启用SPDir翻转 - 由于纹理不对称，需要特殊处理
        /// </summary>
        public override bool IsUseSpriteDirection => true;

        // ===== 脊波弹簧链（纯视觉，MOTION §4 whip chain）=====
        // 每节缓存一个垂直于体轴的偏移量；子节以弹簧追踪父节偏移 → 头部一次冲量成为沿身传播的行波。
        /// <summary>脊波偏移（世界像素，垂直于体轴，绘制用）。</summary>
        public float SpringOffset;
        /// <summary>脊波速度分量。</summary>
        public float SpringVel;

        /// <summary>死亡演出中该节是否已爆裂（爆裂后隐藏且不再发光）。</summary>
        public bool Detonated;

        // 视觉效果参数
        protected float segmentPulsePhase = 0f;
        protected float segmentGlowIntensity = 1f;
        private int defaultContactDamage;

        /// <summary>
        /// 目标玩家
        /// </summary>
        public Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        /// <summary>头部实例（realLife 指向），可能为 null。</summary>
        protected AwakeningNetherHead HeadOwner {
            get {
                if (NPC.realLife >= 0 && NPC.realLife < Main.maxNPCs) {
                    NPC head = Main.npc[NPC.realLife];
                    if (head.active && head.ModNPC is AwakeningNetherHead h)
                        return h;
                }
                return null;
            }
        }

        /// <summary>本节可见度 — 头部取自身 SegmentAlpha，体节取头部值（入场破土前全体隐形）。</summary>
        protected float VisibleAlpha => this is AwakeningNetherHead selfHead
            ? selfHead.SegmentAlpha
            : HeadOwner?.SegmentAlpha ?? 1f;

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 80;
            NPC.height = 80;
            NPC.lifeMax = 800000; // 月后级别血量
            NPC.damage = 180;
            NPC.defense = 80;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0f;
            NPC.behindTiles = true; // 入场破土/埋地阶段由前景物块自然遮蔽

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
            SummonMax = 44; // V3: 80→44，视觉密度与性能双赢，仍是全场最长的巨龙
        }

        public override bool CheckActive() => false; // 永远不自动销毁

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (NPCWormType != WormType.Head) {
                return false;
            }
            return null;
        }

        public override void AI() {
            base.AI();

            segmentPulsePhase += 0.06f + SummonCount * 0.002f; // 每个体节稍微不同的相位

            // 首帧惰性捕获缩放后的默认接触伤害（专家/大师加成发生在 SetDefaults 之后）
            if (defaultContactDamage == 0)
                defaultContactDamage = NPC.damage;

            AwakeningNetherHead head = HeadOwner;
            if (head != null && NPCWormType != WormType.Head) {
                NPC.dontTakeDamage = head.NPC.dontTakeDamage;
                // 演出节拍（入场/死亡/转换）中体节接触伤害清零 — 伤害窗口与视觉对齐
                NPC.damage = head.ContactDamageActive ? defaultContactDamage : 0;

                // 根据头部血量调整发光强度
                float lifePercent = (float)head.NPC.life / head.NPC.lifeMax;
                segmentGlowIntensity = MathHelper.Lerp(segmentGlowIntensity, 1f + (1f - lifePercent) * 0.5f, 0.02f);

                UpdateSpineSpring(head);
            }

            if (!Main.dedServ && !Detonated)
                CreateSegmentParticles();

            if (!Detonated) {
                float lightMod = 0.8f + MathF.Sin(segmentPulsePhase) * 0.2f;
                float visible = VisibleAlpha;
                Lighting.AddLight(NPC.Center, 0.3f * lightMod * segmentGlowIntensity * visible,
                    0.1f * lightMod * segmentGlowIntensity * visible, 0.5f * lightMod * segmentGlowIntensity * visible);
            }
        }

        /// <summary>
        /// 脊波传播：子节以弹簧追踪父节偏移。头部注入一次冲量后，
        /// 波以约 2~3 帧/节的速度沿身传播并自然衰减（一次输入换一秒有机运动）。
        /// </summary>
        private void UpdateSpineSpring(AwakeningNetherHead head) {
            float parentOffset = head.SpringOffset;
            if (FatherNPC != null && FatherNPC.active && FatherNPC.ModNPC is AwakeningNether father)
                parentOffset = father.SpringOffset;

            SpringVel += (parentOffset - SpringOffset) * 0.32f;
            SpringVel *= 0.86f;
            SpringOffset += SpringVel;
            SpringOffset *= 0.985f;
        }

        /// <summary>
        /// 体节粒子 — 速度门控（MOTION §7：常态安静，高速/脊波经过时才发射，特效随动能缩放）。
        /// </summary>
        protected virtual void CreateSegmentParticles() {
            float speed = NPC.velocity.Length();
            bool waveActive = MathF.Abs(SpringOffset) > 7f;

            // 高速拖尾（穿刺时才亮起来）
            if (speed > 16f && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.15f;
                d.scale = 1.1f + speed * 0.02f;
                d.alpha = 90;
            }

            // 脊波经过时鳞缝喷出魂火
            if (waveActive && Main.rand.NextBool(3)) {
                Vector2 perp = NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                var d = Dust.NewDustPerfect(NPC.Center + perp * SpringOffset * 0.5f, DustID.CursedTorch);
                d.noGravity = true;
                d.velocity = perp * MathF.Sign(SpringOffset) * 2f;
                d.scale = 1.2f;
                d.alpha = 60;
            }
        }

        /// <summary>
        /// 自定义绘制 - 单层辉光 + 主体 + 脊波偏移（V3 精简：44 节 × 每节 2 draw）
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Detonated)
                return false; // 死亡演出中已爆裂的体节不再绘制

            float alpha = VisibleAlpha;
            if (alpha <= 0.01f)
                return false; // 入场破土前埋于地下不可见

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            if (NPCWormType == WormType.Head)
                origin.Y = tex.Height * 0.4f;
            else if (NPCWormType == WormType.Tail)
                origin.Y = tex.Height * 0.6f;

            // 脊波偏移（纯视觉，不动 hitbox）
            Vector2 perp = NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            Vector2 drawCenter = NPC.Center + perp * SpringOffset;

            Color netherColor = Color.Lerp(drawColor, AwakeningNetherHelper.AwakeningPurple, 0.4f * segmentGlowIntensity) * alpha;

            // 头部纹理不对称: 朝左时垂直翻转并回退 PostAI 补的 Pi (与 V2 头部绘制一致)
            SpriteEffects effects = SpriteEffects.None;
            float rotation = NPC.rotation;
            if (NPCWormType == WormType.Head && NPC.spriteDirection == -1) {
                effects = SpriteEffects.FlipVertically;
                rotation -= MathHelper.Pi;
            }
            float pulse = 1f + MathF.Sin(segmentPulsePhase) * 0.05f;

            // 单层辉光（脊波经过时增亮 — 波峰即可见的能量流）
            float waveGlow = MathHelper.Clamp(MathF.Abs(SpringOffset) / 18f, 0f, 1f);
            Color glowColor = Color.Lerp(AwakeningNetherHelper.VoidDarkPurple, TelegraphColors.GhostGreen, waveGlow * 0.6f);
            glowColor.A = 0;
            spriteBatch.Draw(tex, drawCenter - screenPos, null,
                glowColor * ((0.22f + waveGlow * 0.45f) * segmentGlowIntensity * alpha),
                rotation, origin, NPC.scale * pulse * (1.25f + waveGlow * 0.15f), effects, 0);

            // 主体
            spriteBatch.Draw(tex, drawCenter - screenPos, null, netherColor, rotation,
                origin, NPC.scale * pulse, effects, 0);

            return false;
        }
    }
}

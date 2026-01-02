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
    /// 终局Boss，是幽冥龙的觉醒形态
    /// </summary>
    public abstract class AwakeningNether : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/AwakeningNethers/" + Name;

        /// <summary>
        /// 启用SPDir翻转 - 由于纹理不对称，需要特殊处理
        /// </summary>
        public override bool IsUseSpriteDirection => true;

        // 视觉效果参数
        protected float segmentPulsePhase = 0f;
        protected float segmentGlowIntensity = 1f;

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
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
            SummonMax = 80; // 更长的身体
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

            // 更新视觉效果参数
            segmentPulsePhase += 0.06f + SummonCount * 0.002f; // 每个体节稍微不同的相位

            if (NPC.realLife >= 0 && Main.npc[NPC.realLife].active) {
                NPC.dontTakeDamage = Main.npc[NPC.realLife].dontTakeDamage;

                // 根据头部血量调整发光强度
                float lifePercent = (float)Main.npc[NPC.realLife].life / Main.npc[NPC.realLife].lifeMax;
                segmentGlowIntensity = MathHelper.Lerp(segmentGlowIntensity, 1f + (1f - lifePercent) * 0.5f, 0.02f);
            }

            // 增强的幽冥粒子效果
            if (FatherNPC.Alives()) {
                CreateSegmentParticles();
            }

            // 强烈的发光效果 - 根据体节位置变化
            float lightMod = 0.8f + MathF.Sin(segmentPulsePhase) * 0.2f;
            Lighting.AddLight(NPC.Center, 0.3f * lightMod * segmentGlowIntensity,
                0.1f * lightMod * segmentGlowIntensity, 0.5f * lightMod * segmentGlowIntensity);
        }

        /// <summary>
        /// 创建体节粒子效果
        /// </summary>
        protected virtual void CreateSegmentParticles() {
            Vector2 pos = NPC.Center + NPC.Center.To(FatherNPC.Center) / 2;

            // 连接粒子
            int particleCount = (int)(NPC.velocity.Length() / 4);
            for (int i = 0; i < particleCount; i++) {
                int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(10, 10), dustType);
                d.noGravity = true;
                d.velocity = NPC.velocity.RotatedByRandom(0.5f) * 0.5f;
                d.scale = 1.3f * segmentGlowIntensity;
                d.alpha = 80;
            }

            // 觉醒态特有的能量粒子 - 环绕效果
            if (Main.rand.NextBool(4)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 30f + Main.rand.NextFloat(15f);
                Vector2 energyPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                var d = Dust.NewDustPerfect(energyPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.0f * segmentGlowIntensity;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                d.alpha = 100;
            }

            // 能量脉冲粒子
            if (Main.rand.NextBool(8)) {
                var pulse = Dust.NewDustPerfect(NPC.Center, DustID.PurpleCrystalShard);
                pulse.noGravity = true;
                pulse.scale = 0.6f;
                pulse.velocity = Main.rand.NextVector2Circular(1, 1);
            }
        }

        /// <summary>
        /// 自定义绘制 - 处理不对称纹理的翻转 + 高级视觉效果
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / 2);

            // 根据不同部位调整原点
            if (NPCWormType == WormType.Head) {
                origin.Y = tex.Height * 0.4f;
            }
            else if (NPCWormType == WormType.Tail) {
                origin.Y = tex.Height * 0.6f;
            }

            // 幽冥紫色色调 - 根据发光强度变化
            Color netherColor = Color.Lerp(drawColor, AwakeningNetherHelper.AwakeningPurple, 0.4f * segmentGlowIntensity);

            // 处理不对称纹理的翻转
            SpriteEffects effects = SpriteEffects.None;
            float rotation = NPC.rotation;

            // 脉动效果
            float pulse = 1f + MathF.Sin(segmentPulsePhase) * 0.05f;

            // 外层光晕
            Color glowColor = AwakeningNetherHelper.VoidDarkPurple;
            glowColor.A = 0;
            for (int i = 2; i >= 0; i--) {
                float glowScale = NPC.scale * pulse * (1.2f + i * 0.1f);
                float glowAlpha = (0.1f / (i + 1)) * segmentGlowIntensity;
                spriteBatch.Draw(tex, NPC.Center - screenPos, null, glowColor * glowAlpha, rotation,
                    origin, glowScale, effects, 0);
            }

            // 主体
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, netherColor, rotation,
                origin, NPC.scale * pulse, effects, 0);

            // 体节之间的能量连接线（非头部时绘制）
            if (NPCWormType != WormType.Head && FatherNPC != null && FatherNPC.active) {
                DrawEnergyConnection(spriteBatch, screenPos);
            }

            return false;
        }

        /// <summary>
        /// 绘制体节之间的能量连接
        /// </summary>
        protected virtual void DrawEnergyConnection(SpriteBatch sb, Vector2 screenPos) {
            if (FatherNPC == null) return;

            Vector2 start = NPC.Center;
            Vector2 end = FatherNPC.Center;

            // 简单的能量连接效果
            Color connectionColor = AwakeningNetherHelper.NetherCyan * 0.15f * segmentGlowIntensity;
            AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, connectionColor, 4f, segmentPulsePhase);
        }
    }
}
